using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SertaoCasasELagoCentral
{
    private const string CasaOriginal = "Assets/Modelos/Casa/Modelo/Casa.fbx";
    private const string Envio = "Assets/EnvioAmigo/CasaNova_e_Poca/";
    private const string CasaNova = Envio + "CasaNova/CasaNova.prefab";
    private const string PocaPrefab = Envio + "Poca/Poca.prefab";
    private const string LagoMaterial = Envio + "Poca/Materiais/M_Poca.mat";
    private const string PedraModelo = "Assets/Modelos/Pedras/Modelo/Pedras.fbx";
    private const string GramaPrefab = "Assets/Modelos/Grama/Grama.prefab";
    private const string NomeLago = "Lago central (slot 2-3)";
    private const string NomeVisualLago = "Visual - Poca e margem de pedras";
    private const string NomeSuperficie = "Lago central - superficie provisoria";
    private const string SufixoCasa = " - Casa Nova";
    private const string Operacao = "Atualizar casas e lago central";
    private const string PlanoMargemLivre = Envio + "Poca/Modelo/Plano_Poca_MargemLivre.asset";
    private const string NomeMargem = "Margem de pedras";
    private const string NomeMatos = "Matinhos da margem";

    [MenuItem("Tools/Sertão/Atualizar casas e lago central")]
    private static void Aplicar()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!cena.IsValid() || cena.path != "Assets/Scenes/PrototypeLayout.unity" ||
            EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(Operacao, "Abra a cena PrototypeLayout fora do Play Mode.", "OK");
            return;
        }

        // Guarda os pivôs que sustentam os visuais existentes, inclusive os das casas ainda não construídas.
        Transform[] todos = cena.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform[] antigas = todos.Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == CasaOriginal).ToArray();
        Transform lago = todos.SingleOrDefault(t => t.name == NomeLago && t.TryGetComponent<Plot>(out _));
        if (antigas.Length == 0 && lago != null && lago.Find(NomeVisualLago) != null &&
            todos.Count(t => t.name.EndsWith(SufixoCasa, StringComparison.Ordinal) &&
                t.childCount == 1 && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.GetChild(0).gameObject) == CasaNova) == 5)
        {
            Selection.activeGameObject = lago.gameObject;
            EditorUtility.DisplayDialog(Operacao, "A cena já foi atualizada. Use Refazer margem do lago central para realinhar a margem.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Operacao);
        try
        {
            if (antigas.Length != 5 || lago == null || lago.Find(NomeVisualLago) != null)
                throw new InvalidOperationException($"Esperava cinco modelos antigos e o lago central original. Encontrei {antigas.Length} modelos.");

            GameObject casa = Carregar<GameObject>(CasaNova);
            GameObject poca = Carregar<GameObject>(PocaPrefab);
            Material agua = Carregar<Material>(LagoMaterial);
            GameObject grama = AssetDatabase.LoadAssetAtPath<GameObject>(GramaPrefab);
            MeshFilter[] malhasCasa = casa.GetComponentsInChildren<MeshFilter>(true);
            if (malhasCasa.Length == 0 || malhasCasa.Any(filtro => filtro.sharedMesh == null))
                throw new InvalidOperationException("O prefab CasaNova tem malhas ausentes. Confira a importação dos arquivos da pasta CasaNova.");
            if (agua.shader == null || !agua.HasProperty("_LarguraLama") ||
                !agua.HasProperty("_Achatamento") || !agua.HasProperty("_Tamanho"))
                throw new InvalidOperationException("O material do lago precisa usar o shader original da Poça.");

            Plot slot = lago.GetComponent<Plot>();
            if (!slot.IsWater) throw new InvalidOperationException("O objeto localizado para o lago não está marcado como água.");
            Transform superficie = lago.Find(NomeSuperficie);
            if (superficie == null || !superficie.TryGetComponent<MeshRenderer>(out var visualAntigo) ||
                !superficie.TryGetComponent<BoxCollider>(out var colisorAntigo) || !colisorAntigo.enabled)
                throw new InvalidOperationException("A superfície provisória precisa manter o Mesh Renderer e o Box Collider do slot.");

            var slotSerializado = new SerializedObject(slot);
            SerializedProperty referenciaSuperficie = slotSerializado.FindProperty("surfaceRenderer");
            if (referenciaSuperficie == null || referenciaSuperficie.objectReferenceValue != visualAntigo)
                throw new InvalidOperationException("A referência de superfície do lago central está diferente da cena de referência.");

            Bounds caixaLago = LimitesDoSlot(colisorAntigo, lago);
            Transform grupoPedras = todos.SingleOrDefault(t => t.name == "Pedras lago");
            if (grupoPedras == null) throw new InvalidOperationException("O grupo Pedras lago não foi encontrado.");

            // O grupo original também guarda pedras decorativas da fazenda. Só o anel do slot entra na troca.
            MeshFilter[] pedrasAntigas = grupoPedras.Cast<Transform>().Select(t => t.GetComponent<MeshFilter>())
                .Where(f => f != null && f.sharedMesh != null &&
                    AssetDatabase.GetAssetPath(f.sharedMesh) == PedraModelo &&
                    PertoDoLago(lago.InverseTransformPoint(f.transform.position), caixaLago))
                .ToArray();
            MeshFilter[] variacoes = pedrasAntigas.Where(f => f.TryGetComponent<MeshRenderer>(out var renderer) &&
                    renderer.sharedMaterial != null)
                .GroupBy(f => f.sharedMesh).Select(g => g.First())
                .OrderBy(f => f.sharedMesh.name, StringComparer.Ordinal).ToArray();
            if (variacoes.Length != 3)
                throw new InvalidOperationException($"A borda do lago precisa das três malhas de pedra. Encontrei {variacoes.Length} nesta área.");

            foreach (Transform antiga in antigas) TrocarCasa(antiga, casa);
            MeshRenderer novaSuperficie = MontarLago(lago, caixaLago, poca, agua, variacoes, grama);

            Undo.RecordObject(visualAntigo, Operacao);
            visualAntigo.enabled = false;
            referenciaSuperficie.objectReferenceValue = novaSuperficie;
            slotSerializado.ApplyModifiedProperties();
            foreach (MeshFilter pedra in pedrasAntigas) Undo.DestroyObjectImmediate(pedra.gameObject);
            foreach (Transform antiga in antigas) Undo.DestroyObjectImmediate(antiga.gameObject);

            Undo.CollapseUndoOperations(grupo);
            EditorSceneManager.MarkSceneDirty(cena);
            Selection.activeGameObject = lago.gameObject;
            Debug.Log($"Sertão: cinco casas atualizadas nos pivôs originais. Lago central com a Poça original e três variações de pedra.", lago);
            EditorUtility.DisplayDialog(Operacao,
                "Cinco casas atualizadas e lago central montado. Confira o Game View e salve a cena (Ctrl+S).", "OK");
        }
        catch (Exception erro)
        {
            Undo.RevertAllDownToGroup(grupo);
            Debug.LogException(erro);
            EditorUtility.DisplayDialog(Operacao, erro.Message, "OK");
        }
    }

    [MenuItem("Tools/Sertão/Refazer margem do lago central")]
    private static void RefazerMargem()
    {
        Scene cena = SceneManager.GetActiveScene();
        const string nomeOperacao = "Refazer margem do lago central";
        if (!cena.IsValid() || cena.path != "Assets/Scenes/PrototypeLayout.unity" ||
            EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(nomeOperacao, "Abra a cena PrototypeLayout fora do Play Mode.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(nomeOperacao);
        try
        {
            Transform lago = cena.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .SingleOrDefault(t => t.name == NomeLago && t.TryGetComponent<Plot>(out var plot) && plot.IsWater);
            Transform raiz = lago != null ? lago.Find(NomeVisualLago) : null;
            Transform superficie = lago != null ? lago.Find(NomeSuperficie) : null;
            Transform margemAntiga = raiz != null ? raiz.Find(NomeMargem) : null;
            MeshRenderer aguaRender = raiz != null
                ? raiz.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r =>
                    r.transform.parent == raiz && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject) == PocaPrefab)
                : null;
            if (aguaRender == null || superficie == null || margemAntiga == null)
                throw new InvalidOperationException("Use Atualizar casas e lago central primeiro para instalar a Poça.");
            if (!superficie.TryGetComponent<BoxCollider>(out var colisor))
                throw new InvalidOperationException("O Box Collider original do slot do lago central está ausente.");

            MeshFilter[] variacoes = margemAntiga.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.sharedMesh != null && AssetDatabase.GetAssetPath(f.sharedMesh) == PedraModelo)
                .GroupBy(f => f.sharedMesh).Select(g => g.First())
                .OrderBy(f => f.sharedMesh.name, StringComparer.Ordinal).ToArray();
            if (variacoes.Length != 3)
                throw new InvalidOperationException("A margem anterior precisa ter as três malhas de pedra.");

            Transform matosAntigos = raiz.Find(NomeMatos);
            Material agua = Carregar<Material>(LagoMaterial);
            GameObject grama = AssetDatabase.LoadAssetAtPath<GameObject>(GramaPrefab);
            Vector3 frente = lago.InverseTransformDirection(aguaRender.transform.forward);
            float giro = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;
            ConfigurarLago(raiz, LimitesDoSlot(colisor, lago), aguaRender, agua, variacoes, grama, giro);
            Undo.DestroyObjectImmediate(margemAntiga.gameObject);
            if (matosAntigos != null) Undo.DestroyObjectImmediate(matosAntigos.gameObject);

            Undo.CollapseUndoOperations(grupo);
            EditorSceneManager.MarkSceneDirty(cena);
            Selection.activeGameObject = aguaRender.gameObject;
            EditorUtility.DisplayDialog(nomeOperacao, "Poça e pedras ajustadas. Confira a margem no Game View e salve a cena.", "OK");
        }
        catch (Exception erro)
        {
            Undo.RevertAllDownToGroup(grupo);
            Debug.LogException(erro);
            EditorUtility.DisplayDialog(nomeOperacao, erro.Message, "OK");
        }
    }

    private static T Carregar<T>(string caminho) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(caminho);
        if (asset == null) throw new InvalidOperationException("Asset ausente ou ainda não importado: " + caminho);
        return asset;
    }

    private static Transform CriarRaiz(string nome, Transform pai)
    {
        var obj = new GameObject(nome);
        obj.layer = pai.gameObject.layer;
        obj.transform.SetParent(pai, false);
        Undo.RegisterCreatedObjectUndo(obj, Operacao);
        Undo.RecordObject(obj.transform, Operacao);
        return obj.transform;
    }

    private static GameObject Instanciar(GameObject prefab, Transform pai)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai);
        if (obj == null) throw new InvalidOperationException("Não foi possível instanciar " + prefab.name);
        Undo.RegisterCreatedObjectUndo(obj, Operacao);
        Undo.RecordObject(obj.transform, Operacao);
        return obj;
    }

    private static void TrocarCasa(Transform antiga, GameObject prefab)
    {
        Bounds anterior = LimitesNaRaiz(antiga, antiga);
        Transform pivo = CriarRaiz(antiga.name + SufixoCasa, antiga.parent);
        pivo.SetSiblingIndex(antiga.GetSiblingIndex());
        pivo.localPosition = antiga.localPosition;
        pivo.localRotation = antiga.localRotation;
        pivo.localScale = antiga.localScale;
        pivo.gameObject.layer = antiga.gameObject.layer;

        // O prefab entra inteiro. Corpo, telhado e materiais vêm da montagem publicada.
        Transform arte = Instanciar(prefab, pivo).transform;
        arte.localPosition = Vector3.zero;
        arte.localRotation = Quaternion.identity;
        arte.localScale = Vector3.one;
        Bounds novo = LimitesNaRaiz(arte, pivo);
        Vector3 escala = new Vector3(anterior.size.x / novo.size.x,
            anterior.size.y / novo.size.y, anterior.size.z / novo.size.z);
        arte.localScale = escala;
        arte.localPosition = BaseCentral(anterior) - Vector3.Scale(BaseCentral(novo), escala);
        PrefabUtility.RecordPrefabInstancePropertyModifications(arte);

        Bounds encaixado = LimitesNaRaiz(arte, pivo);
        if ((encaixado.size - anterior.size).sqrMagnitude > 0.000001f ||
            (BaseCentral(encaixado) - BaseCentral(anterior)).sqrMagnitude > 0.000001f)
            throw new InvalidOperationException("O encaixe saiu dos limites do modelo antigo: " + antiga.name);

        if (!antiga.gameObject.activeSelf)
        {
            Undo.RecordObject(pivo.gameObject, Operacao);
            pivo.gameObject.SetActive(false);
        }
    }

    private static Vector3 BaseCentral(Bounds limites) => new Vector3(limites.center.x, limites.min.y, limites.center.z);

    private static Bounds LimitesDoSlot(BoxCollider colisor, Transform slot)
    {
        return TransformarLimites(new Bounds(colisor.center, colisor.size),
            slot.worldToLocalMatrix * colisor.transform.localToWorldMatrix);
    }

    private static Bounds TransformarLimites(Bounds local, Matrix4x4 matriz)
    {
        // A soma dos eixos transformados dá a caixa envolvente no quadro de referência.
        Vector3 dx = matriz.MultiplyVector(new Vector3(local.extents.x, 0f, 0f));
        Vector3 dy = matriz.MultiplyVector(new Vector3(0f, local.extents.y, 0f));
        Vector3 dz = matriz.MultiplyVector(new Vector3(0f, 0f, local.extents.z));
        Vector3 metade = new Vector3(
            Mathf.Abs(dx.x) + Mathf.Abs(dy.x) + Mathf.Abs(dz.x),
            Mathf.Abs(dx.y) + Mathf.Abs(dy.y) + Mathf.Abs(dz.y),
            Mathf.Abs(dx.z) + Mathf.Abs(dy.z) + Mathf.Abs(dz.z));
        return new Bounds(matriz.MultiplyPoint3x4(local.center), metade * 2f);
    }

    private static Bounds LimitesNaRaiz(Transform modelo, Transform quadro)
    {
        var pontos = new Bounds();
        bool encontrado = false;
        foreach (MeshFilter filtro in modelo.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filtro.sharedMesh == null || !filtro.TryGetComponent<MeshRenderer>(out var render) || !render.enabled ||
                !ParteAtiva(filtro.transform, modelo)) continue;
            // Os limites importados também servem para malhas sem um buffer de posições legível.
            Bounds local = filtro.sharedMesh.bounds;
            if (local.size.sqrMagnitude <= 0.00000001f)
                local = render.localBounds;
            if (local.size.sqrMagnitude <= 0.00000001f) continue;

            Bounds parte = TransformarLimites(local, quadro.worldToLocalMatrix * filtro.transform.localToWorldMatrix);
            if (encontrado) pontos.Encapsulate(parte);
            else
            {
                pontos = parte;
                encontrado = true;
            }
        }

        if (!encontrado || pontos.size.x < 0.0001f || pontos.size.y < 0.0001f || pontos.size.z < 0.0001f)
            throw new InvalidOperationException("A malha não tem dimensões válidas: " + modelo.name);
        return pontos;
    }

    private static bool ParteAtiva(Transform parte, Transform modelo)
    {
        for (Transform atual = parte; atual != modelo; atual = atual.parent)
            if (!atual.gameObject.activeSelf) return false;
        return true;
    }

    private static bool PertoDoLago(Vector3 posicao, Bounds caixa)
    {
        Vector3 d = posicao - caixa.center;
        return Mathf.Abs(d.x) <= caixa.size.x * 0.71f && Mathf.Abs(d.z) <= caixa.size.z * 0.71f &&
            Mathf.Abs(d.y) < 0.35f;
    }

    private static MeshRenderer MontarLago(Transform slot, Bounds caixa, GameObject poca, Material agua,
        MeshFilter[] variacoes, GameObject grama)
    {
        Transform raiz = CriarRaiz(NomeVisualLago, slot);
        GameObject instancia = Instanciar(poca, raiz);
        if (!instancia.TryGetComponent<MeshRenderer>(out var renderer))
            throw new InvalidOperationException("O prefab Poça precisa ter um Mesh Renderer na raiz.");
        ConfigurarLago(raiz, caixa, renderer, agua, variacoes, grama, 0f);
        return renderer;
    }

    private static void ConfigurarLago(Transform raiz, Bounds caixa, MeshRenderer renderer,
        Material agua, MeshFilter[] variacoes, GameObject grama, float giro)
    {
        if (!renderer.TryGetComponent<MeshFilter>(out var filtro))
            throw new InvalidOperationException("O prefab Poça precisa ter um Mesh Filter na raiz.");

        var forma = new ContornoPoca(agua, giro);
        Bounds lama = forma.LimitesLama;
        float unidade = Mathf.Min((caixa.size.x - 0.15f) / lama.size.x, (caixa.size.z - 0.15f) / lama.size.z);
        if (unidade <= 0.01f || float.IsNaN(unidade) || float.IsInfinity(unidade))
            throw new InvalidOperationException("A área do slot é insuficiente para encaixar a Poça.");

        Mesh planoExtenso = Carregar<Mesh>(PlanoMargemLivre);
        Undo.RecordObjects(new UnityEngine.Object[] { raiz, filtro, renderer, renderer.transform }, Operacao);
        raiz.localRotation = Quaternion.identity;
        raiz.localScale = Vector3.one;
        filtro.sharedMesh = planoExtenso;
        renderer.sharedMaterial = agua;
        Transform plano = renderer.transform;
        plano.localRotation = Quaternion.Euler(0f, giro, 0f);
        // P do shader usa [-1, 1] a cada 3 unidades de malha.
        plano.localScale = new Vector3(unidade / 1.5f, 1f, unidade / 1.5f);
        plano.localPosition = Vector3.up * (caixa.max.y - caixa.size.y * 0.17f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(filtro);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(plano);

        float basePedras = caixa.center.y - caixa.size.y * 0.22f;
        Transform margem = CriarRaiz(NomeMargem, raiz);
        ColocarPedras(margem, forma, unidade, basePedras, variacoes);

        Bounds ocupado = new Bounds(lama.center * unidade, lama.size * unidade);
        ocupado.Encapsulate(LimitesNaRaiz(margem, raiz));
        if (grama != null)
        {
            Transform matos = CriarRaiz(NomeMatos, raiz);
            ColocarMatos(matos, grama, forma, unidade, basePedras);
            ocupado.Encapsulate(LimitesNaRaiz(matos, raiz));
        }

        // Encaixa a silhueta visível incluindo as pedras, sem recortar a água.
        float ajuste = Mathf.Min(1f, Mathf.Min((caixa.size.x - 0.025f) / ocupado.size.x,
            (caixa.size.z - 0.025f) / ocupado.size.z));
        raiz.localScale = new Vector3(ajuste, 1f, ajuste);
        raiz.localPosition = new Vector3(caixa.center.x - ocupado.center.x * ajuste,
            0f, caixa.center.z - ocupado.center.z * ajuste);
    }

    private static void ColocarPedras(Transform margem, ContornoPoca forma, float unidade, float basePedras,
        MeshFilter[] variacoes)
    {
        float[] pesos = { 1.46f, 0.67f, 0.92f, 1.32f, 0.73f, 0.83f, 1.55f, 0.62f, 1.02f, 0.72f, 1.25f,
            1.42f, 0.71f, 0.79f, 1.59f, 0.66f, 0.96f, 1.26f, 0.64f, 0.93f, 1.43f, 0.72f };
        int[] estilos = { 0, 2, 1, 1, 2, 0, 1, 2, 0, 2, 0, 1, 2, 0, 0, 2, 1, 0, 2, 1, 1, 2 };
        var random = new System.Random(7831);
        float escalaArco = forma.Comprimento / pesos.Sum();
        float inicio = forma.Comprimento * 0.031f;
        for (int i = 0; i < pesos.Length; ++i)
        {
            float trecho = pesos[i] * escalaArco;
            float distancia = inicio + trecho * Sorteio(random, 0.39f, 0.61f);
            Vector2 tangente = forma.Tangente(distancia);
            Vector2 fora = new Vector2(tangente.y, -tangente.x);
            Vector2 centro = forma.Ponto(distancia) * unidade + fora * Sorteio(random, 0.013f, 0.058f);
            float giro = Mathf.Atan2(-tangente.y, tangente.x) * Mathf.Rad2Deg + Sorteio(random, -29f, 29f);
            float largura = trecho * unidade * Sorteio(random, 1.14f, 1.34f);
            float fundo = Sorteio(random, 0.095f, 0.164f) + Mathf.Max(0f, pesos[i] - 1f) * 0.033f;
            float altura = Sorteio(random, 0.095f, 0.153f) + Mathf.Max(0f, pesos[i] - 0.78f) * 0.084f;
            CriarPedra(margem, variacoes[estilos[i]], $"Pedra {i + 1:00}", centro, basePedras - 0.012f,
                new Vector3(largura, fundo, altura), giro, random);
            inicio += trecho;
        }

        float[] laterais = { 0.063f, 0.281f, 0.337f, 0.593f, 0.806f, 0.918f };
        for (int i = 0; i < laterais.Length; ++i)
        {
            float distancia = laterais[i] * forma.Comprimento;
            Vector2 t = forma.Tangente(distancia);
            Vector2 centro = forma.Ponto(distancia) * unidade +
                new Vector2(t.y, -t.x) * Sorteio(random, 0.087f, 0.132f);
            CriarPedra(margem, variacoes[(i + 2) % variacoes.Length], $"Pedrinha {i + 1:00}", centro,
                basePedras - 0.012f,
                new Vector3(Sorteio(random, 0.062f, 0.103f), Sorteio(random, 0.045f, 0.071f),
                    Sorteio(random, 0.04f, 0.075f)),
                Sorteio(random, 0f, 360f), random);
        }
    }

    private static void CriarPedra(Transform margem, MeshFilter forma, string nome, Vector2 centro, float pe,
        Vector3 dimensao, float giro, System.Random random)
    {
        var pedra = new GameObject(nome, typeof(MeshFilter), typeof(MeshRenderer));
        pedra.layer = forma.gameObject.layer;
        Transform t = pedra.transform;
        t.SetParent(margem, false);
        Mesh mesh = forma.sharedMesh;
        pedra.GetComponent<MeshFilter>().sharedMesh = mesh;
        pedra.GetComponent<MeshRenderer>().sharedMaterials = forma.GetComponent<MeshRenderer>().sharedMaterials;

        Bounds b = mesh.bounds;
        Vector3 escala = new Vector3(dimensao.x / b.size.x, dimensao.y / b.size.y, dimensao.z / b.size.z);
        Quaternion orientacao = Quaternion.Euler(Sorteio(random, -17f, 17f), giro, Sorteio(random, -19f, 19f)) *
            Quaternion.Euler(-90f, 0f, 0f);
        Vector3 dx = orientacao * new Vector3(b.extents.x * escala.x, 0f, 0f);
        Vector3 dy = orientacao * new Vector3(0f, b.extents.y * escala.y, 0f);
        Vector3 dz = orientacao * new Vector3(0f, 0f, b.extents.z * escala.z);
        float meioAltura = Mathf.Abs(dx.y) + Mathf.Abs(dy.y) + Mathf.Abs(dz.y);
        t.localRotation = orientacao;
        t.localScale = escala;
        t.localPosition = new Vector3(centro.x, pe + meioAltura, centro.y) -
            orientacao * Vector3.Scale(b.center, escala);
        Undo.RegisterCreatedObjectUndo(pedra, Operacao);
    }

    private static void ColocarMatos(Transform matos, GameObject grama, ContornoPoca forma,
        float unidade, float basePedras)
    {
        float[] lugares = { 0.093f, 0.372f, 0.745f };
        for (int i = 0; i < lugares.Length; i++)
        {
            float distancia = lugares[i] * forma.Comprimento;
            Vector2 t = forma.Tangente(distancia);
            Vector2 ponto = forma.Ponto(distancia) * unidade + new Vector2(t.y, -t.x) * 0.117f;
            Transform mato = Instanciar(grama, matos).transform;
            mato.localPosition = Vector3.zero;
            mato.localRotation = Quaternion.Euler(0f, 35f + i * 107f, 0f) * grama.transform.localRotation;
            mato.localScale = Vector3.one;
            Bounds volume = LimitesNaRaiz(mato, matos);
            float escala = Mathf.Min((0.115f + i * 0.017f) / volume.size.y,
                0.15f / Mathf.Max(volume.size.x, volume.size.z));
            mato.localScale = Vector3.one * escala;
            mato.localPosition = new Vector3(ponto.x, basePedras + 0.008f, ponto.y) - BaseCentral(volume) * escala;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mato);
        }
    }

    private static float Sorteio(System.Random random, float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());

    private sealed class ContornoPoca
    {
        private const int Amostras = 360;
        private readonly Vector2[] pontos = new Vector2[Amostras + 1];
        private readonly float[] acumulado = new float[Amostras + 1];
        private readonly float tamanho, achatamento, irregular, lama, semente;
        private Bounds limitesLama;

        public float Comprimento => acumulado[Amostras];
        public Bounds LimitesLama => limitesLama;

        public ContornoPoca(Material material, float giro)
        {
            tamanho = Mathf.Max(0.01f, material.GetFloat("_Tamanho"));
            achatamento = Mathf.Max(0.1f, material.GetFloat("_Achatamento"));
            irregular = material.GetFloat("_Irregularidade");
            lama = Mathf.Max(0f, material.GetFloat("_LarguraLama"));
            semente = material.GetFloat("_Semente");
            Quaternion rotacao = Quaternion.Euler(0f, giro, 0f);
            for (int i = 0; i <= Amostras; ++i)
            {
                float ang = (i % Amostras) * (2f * Mathf.PI / Amostras);
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 agua = dir * Raio(dir, ang, 0f);
                Vector2 barro = dir * Raio(dir, ang, lama + 0.012f);
                Vector3 limite = rotacao * new Vector3(barro.x, 0f, barro.y);
                if (i == 0) limitesLama = new Bounds(limite, Vector3.zero);
                else limitesLama.Encapsulate(limite);
                Vector3 valor = rotacao * new Vector3(agua.x, 0f, agua.y);
                pontos[i] = new Vector2(valor.x, valor.z);
                if (i > 0) acumulado[i] = acumulado[i - 1] + Vector2.Distance(pontos[i - 1], pontos[i]);
            }
        }

        // A curva usa os mesmos harmônicos e ruído que o shader da Poça.
        private float Raio(Vector2 dir, float angulo, float margem)
        {
            float fase = semente * 1.37f;
            float borda = tamanho * (1f + irregular * (
                0.09f * Mathf.Sin(3f * angulo + 1.3f + fase) +
                0.05f * Mathf.Sin(5f * angulo + 0.4f + fase * 1.7f) +
                0.035f * Mathf.Sin(8f * angulo + 2.0f + fase * 2.3f)));
            float fator = new Vector2(dir.x, dir.y / achatamento).magnitude;
            Vector2 origem = new Vector2(semente * 17.13f + 3.1f, semente * 7.71f + 1.7f);
            float menor = 0f;
            float maior = (tamanho * (1f + Mathf.Abs(irregular) * .175f) + .07f * Mathf.Abs(irregular) + margem) / fator;
            for (int i = 0; i < 18; ++i)
            {
                float r = (menor + maior) * .5f;
                float ruido = (Fbm(dir * (r * 1.6f) + origem) - .5f) * .14f * irregular;
                if (r * fator < borda + ruido + margem) menor = r;
                else maior = r;
            }
            return (menor + maior) * .5f;
        }

        private static float Fracao(float a) => a - Mathf.Floor(a);

        private static float Hash(Vector2 p)
        {
            p = new Vector2(Fracao(p.x * 123.34f), Fracao(p.y * 456.21f));
            float n = Vector2.Dot(p, p + Vector2.one * 45.32f);
            p += Vector2.one * n;
            return Fracao(p.x * p.y);
        }

        private static float Fbm(Vector2 p)
        {
            float soma = 0f;
            float amp = .5f;
            for (int k = 0; k < 3; ++k)
            {
                Vector2 i = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
                Vector2 f = p - i;
                Vector2 u = Vector2.Scale(Vector2.Scale(f, f), Vector2.one * 3f - f * 2f);
                soma += amp * Mathf.Lerp(
                    Mathf.Lerp(Hash(i), Hash(i + Vector2.right), u.x),
                    Mathf.Lerp(Hash(i + Vector2.up), Hash(i + Vector2.one), u.x), u.y);
                p = p * 2.03f + Vector2.one * 17.1f;
                amp *= .5f;
            }
            return soma / .875f;
        }

        public Vector2 Tangente(float distancia) => (Ponto(distancia + .037f) - Ponto(distancia - .037f)).normalized;

        public Vector2 Ponto(float distancia)
        {
            distancia = Mathf.Repeat(distancia, Comprimento);
            int i = Array.BinarySearch(acumulado, distancia);
            if (i >= 0) return pontos[i];
            i = ~i;
            return Vector2.Lerp(pontos[i - 1], pontos[i],
                Mathf.InverseLerp(acumulado[i - 1], acumulado[i], distancia));
        }
    }
}
