using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

internal static class SertaoFlorestaEMateriais
{
    private const string Operacao = "Montar Caatinga e revisar materiais";
    private const string Mata = "Mata da Caatinga";
    private const string Corte = "Árvores de coleta";
    private const string EstratoAlto = "Árvores e cactos";
    private const string EstratoBaixo = "Vegetação baixa e pedras";
    private const string Modelos = "Assets/Modelos/";
    private const string TerraAntiga = Modelos + "Modelos GS4/TerraArada.fbx";
    private const string ArvoresCortadas = Modelos + "NovasArvores/NovasArvores.prefab";
    private static readonly Vector2[] FundoAlto = {
        new Vector2(-5.29f, 2.76f), new Vector2(-4.89f, 3.94f), new Vector2(-4.02f, 4.67f),
        new Vector2(-3.02f, 5.53f), new Vector2(-1.96f, 4.87f), new Vector2(-.90f, 5.27f),
        new Vector2(.47f, 3.86f), new Vector2(1.60f, 4.63f), new Vector2(2.43f, 3.18f)
    };
    private static readonly Vector2[] FundoBaixo = {
        new Vector2(-5.54f, .48f), new Vector2(-4.55f, -.34f), new Vector2(-4.61f, 1.79f),
        new Vector2(-3.70f, 2.11f), new Vector2(-3.51f, 3.46f), new Vector2(-2.09f, 3.96f),
        new Vector2(-1.16f, 3.63f), new Vector2(.12f, 2.61f), new Vector2(1.13f, 2.91f),
        new Vector2(2.15f, 2.10f), new Vector2(2.97f, 2.55f), new Vector2(3.60f, 1.43f)
    };
    private static readonly Vector2[] Cactos = {
        new Vector2(-4.52f, -.92f), new Vector2(-5.80f, 1.28f), new Vector2(-4.53f, 2.95f),
        new Vector2(-3.94f, 3.86f), new Vector2(-2.71f, 4.40f), new Vector2(-1.39f, 4.68f),
        new Vector2(.82f, 3.37f), new Vector2(1.51f, 2.07f), new Vector2(2.82f, 1.31f),
        new Vector2(3.14f, .29f), new Vector2(-5.03f, .58f)
    };
    private static readonly Vector2[] Touceiras = {
        new Vector2(-5.40f, -.34f), new Vector2(-4.35f, -1.24f), new Vector2(-3.62f, -.72f),
        new Vector2(-2.82f, .07f), new Vector2(-4.18f, .57f), new Vector2(-4.85f, 1.29f),
        new Vector2(-5.40f, 1.90f), new Vector2(-5.03f, 2.34f), new Vector2(-4.05f, 2.88f),
        new Vector2(-3.19f, 2.69f), new Vector2(-4.55f, 3.47f), new Vector2(-3.65f, 4.12f),
        new Vector2(-2.92f, 4.72f), new Vector2(-2.03f, 4.36f), new Vector2(-1.43f, 3.73f),
        new Vector2(-1.52f, 3.08f), new Vector2(-.48f, 2.73f), new Vector2(-.22f, 3.53f),
        new Vector2(.67f, 2.31f), new Vector2(.94f, 3.83f), new Vector2(1.72f, 3.00f),
        new Vector2(2.58f, 3.67f), new Vector2(2.56f, 2.50f), new Vector2(3.33f, 1.85f),
        new Vector2(2.57f, 1.04f), new Vector2(3.59f, .71f), new Vector2(2.83f, -.52f),
        new Vector2(1.94f, -1.17f), new Vector2(.81f, -1.43f), new Vector2(-.81f, -1.28f)
    };
    private static readonly Vector2[] GruposDePedras = {
        new Vector2(-5.00f, -.78f), new Vector2(-3.66f, -.99f), new Vector2(-4.89f, 1.74f),
        new Vector2(-4.17f, 3.23f), new Vector2(-3.32f, 4.09f), new Vector2(-1.88f, 3.25f),
        new Vector2(-.52f, 4.08f), new Vector2(.63f, 2.93f), new Vector2(1.86f, 2.69f),
        new Vector2(3.30f, 1.02f), new Vector2(1.82f, -1.23f), new Vector2(-1.24f, -1.04f)
    };

    [MenuItem("Tools/Sertão/Montar Caatinga e revisar materiais")]
    private static void Aplicar()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (cena.path != "Assets/Scenes/PrototypeLayout.unity" || EditorApplication.isPlayingOrWillChangePlaymode ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(Operacao, "Abra a cena PrototypeLayout fora do Play Mode.", "OK");
            return;
        }

        var raizes = cena.GetRootGameObjects();
        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Operacao);
        try
        {
            var resolvedor = raizes.SelectMany(r => r.GetComponentsInChildren<ActionResolver>(true)).SingleOrDefault();
            if (resolvedor == null) throw new InvalidOperationException("O ActionResolver da comunidade está ausente.");

            var dados = new SerializedObject(resolvedor);
            var destino = dados.FindProperty("forestDestination")?.objectReferenceValue as Transform;
            var queda = dados.FindProperty("forestTrees")?.objectReferenceValue as ForestTreeFall;
            if (destino == null || destino.parent == null || queda == null)
                throw new InvalidOperationException("A coleta precisa de forestDestination e forestTrees atribuídos.");

            Transform mata = AtualizarMata(raizes, destino, queda, dados);

            int cercas = CorrigirCercas(raizes);
            int solos = RemoverSolosAntigos(raizes);
            Undo.CollapseUndoOperations(grupo);
            EditorSceneManager.MarkSceneDirty(cena);
            Selection.activeGameObject = mata.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"Sertão: Caatinga com três árvores de corte; {cercas} cercas corrigidas e {solos} visuais antigos removidos.", mata);
            EditorUtility.DisplayDialog(Operacao,
                $"Três árvores de corte, {cercas} cercas corrigidas e {solos} visuais antigos removidos. Confira o Game View e salve com Ctrl+S.", "OK");
        }
        catch (Exception erro)
        {
            Undo.RevertAllDownToGroup(grupo);
            Debug.LogException(erro);
            EditorUtility.DisplayDialog(Operacao, erro.Message, "OK");
        }
    }

    private static Transform AtualizarMata(GameObject[] raizes, Transform destino, ForestTreeFall queda,
        SerializedObject dados)
    {
        var arv1 = Carregar<GameObject>(Modelos + "Arvore1/Arvore1.prefab");
        var arv2 = Carregar<GameObject>(Modelos + "Arvore2/Arvore2_PivoCentral.prefab");
        var grama = Carregar<GameObject>(Modelos + "Grama/Grama.prefab");
        var cacto = Carregar<GameObject>(Modelos + "Cactus/Modelo/Cactus.fbx");
        var matCacto = Carregar<Material>(Modelos + "Cactus/Materiais/M_Cactus.mat");
        var origem = Carregar<GameObject>(ArvoresCortadas);
        var partes = origem.GetComponentsInChildren<MeshFilter>(true);
        var parA = new Par(origem.transform, partes, "Cylinder.456", "Cylinder.348", "Cylinder.458", "Cylinder.349");
        var parB = new Par(origem.transform, partes, "Cylinder.318", "Cylinder.233", "Cylinder.459", "Cylinder.347");

        using var solo = new SoloDaFloresta(raizes, destino.position.y);
        Transform mata = destino.parent.Find(Mata);
        if (mata == null)
        {
            mata = Novo(Mata, destino.parent);
            mata.position = destino.position;
        }
        var corteAnterior = mata.Find(Corte);
        var fundoAnterior = mata.Find(EstratoAlto);
        var baixoAnterior = mata.Find(EstratoBaixo);
        Transform corte = Novo(Corte, mata);
        var antigos = MontarCorte(queda, corte, corteAnterior, solo, new[] { parA, parB, parA });
        ComporMata(mata, corte, solo, arv1, arv2, grama, cacto, matCacto, dados, destino.position);

        foreach (GameObject item in antigos) Undo.DestroyObjectImmediate(item);
        foreach (Transform grupo in new[] { corteAnterior, fundoAnterior, baixoAnterior })
        {
            if (grupo == null) continue;
            // Elementos adicionados manualmente ficam na mata.
            foreach (Transform filho in grupo.Cast<Transform>().ToArray())
            {
                bool gerado = grupo != corteAnterior && GeradoPeloComando(filho.name);
                if (!gerado) Undo.SetTransformParent(filho, mata, Operacao);
            }
            Undo.DestroyObjectImmediate(grupo.gameObject);
        }

        Transform area = destino.parent.Find("Área da floresta");
        if (area != null && area.TryGetComponent<MeshRenderer>(out var pisoProvisorio) && pisoProvisorio.enabled)
        {
            Undo.RecordObject(pisoProvisorio, Operacao);
            pisoProvisorio.enabled = false;
        }
        return mata;
    }

    private static bool GeradoPeloComando(string nome)
    {
        string[] prefixos = { "Árvore de fundo ", "Árvore menor ", "Cacto ", "Mato ", "Pedra " };
        return prefixos.Any(prefixo => nome.StartsWith(prefixo, StringComparison.Ordinal) &&
            nome.Length > prefixo.Length && nome.Substring(prefixo.Length).All(c => char.IsDigit(c) || c == '.'));
    }

    private static T Carregar<T>(string caminho) where T : UnityEngine.Object
    {
        var obj = AssetDatabase.LoadAssetAtPath<T>(caminho);
        if (obj == null) throw new InvalidOperationException("Asset ausente ou ainda não importado: " + caminho);
        return obj;
    }

    private static Transform Novo(string nome, Transform pai, params Type[] tipos)
    {
        var obj = new GameObject(nome, tipos);
        obj.transform.SetParent(pai, false);
        Undo.RegisterCreatedObjectUndo(obj, Operacao);
        Undo.RecordObject(obj.transform, Operacao);
        return obj.transform;
    }

    private static List<GameObject> MontarCorte(ForestTreeFall queda, Transform grupo, Transform anterior,
        SoloDaFloresta solo, Par[] pares)
    {
        var ser = new SerializedObject(queda);
        SerializedProperty arvores = ser.FindProperty("trees");
        if (arvores == null || arvores.arraySize != 3)
            throw new InvalidOperationException("A cena de coleta precisa ter três pares de tronco e copa configurados.");

        float[] alturas = { 2.18f, 2.52f, 2.07f };
        float[] giros = { -15f, 32f, 58f };
        var antigos = new List<GameObject>(6);
        for (int i = 0; i < 3; ++i)
        {
            SerializedProperty item = arvores.GetArrayElementAtIndex(i);
            SerializedProperty tronco = item.FindPropertyRelative("trunk");
            SerializedProperty copa = item.FindPropertyRelative("canopy");
            SerializedProperty ponta = item.FindPropertyRelative("impactTip");
            var tocoAntigo = tronco?.objectReferenceValue as Transform;
            var copaAntiga = copa?.objectReferenceValue as Transform;
            if (tocoAntigo == null || copaAntiga == null || ponta == null)
                throw new InvalidOperationException("Uma árvore antiga está sem referência. Confira o ForestTreeFall na cena.");

            Transform raizAntiga = tocoAntigo.parent;
            bool jaMontada = anterior != null && raizAntiga != null && raizAntiga.parent == anterior && copaAntiga.parent == raizAntiga;
            Vector3 baseTronco = jaMontada ? raizAntiga.position : tocoAntigo.position;
            baseTronco.y = solo.Altura(baseTronco) - .012f;
            Transform arvore = Novo($"Árvore de coleta {i + 1:00}", grupo);
            arvore.position = baseTronco;
            arvore.rotation = jaMontada ? raizAntiga.rotation : Quaternion.Euler(0f, giros[i], 0f);

            Par par = pares[i];
            float escala = alturas[i] / (par.Inferior.Volume.size.y + par.Superior.Volume.size.y);
            Bounds bordaToco = par.Inferior.FaceCorte;
            Bounds bordaCopa = par.Superior.FaceCorte;
            Vector3 baseCorte = bordaToco.center;
            Vector3 baseModelo = new Vector3(baseCorte.x, par.Inferior.Volume.min.y, baseCorte.z);
            Transform toco = Novo("Toco fixo", arvore);
            par.Inferior.Instalar(toco, escala, baseModelo, "Malha do toco");

            Transform pivo = Novo("Copa - pivô no corte", arvore);
            pivo.localPosition = Vector3.up * ((baseCorte.y - baseModelo.y) * escala);
            Transform encaixe = Novo("Encaixe da copa", pivo);
            // As duas faces são inclinadas. A face da copa entra sob a do toco antes de cair.
            encaixe.localPosition = Vector3.up * ((bordaToco.min.y - baseCorte.y - bordaCopa.extents.y) * escala + .006f);
            encaixe.localScale = new Vector3(
                Mathf.Clamp(bordaToco.size.x / bordaCopa.size.x * .99f, .92f, 1.40f), 1f,
                Mathf.Clamp(bordaToco.size.z / bordaCopa.size.z * .99f, .92f, 1.40f));
            par.Superior.Instalar(encaixe, escala, bordaCopa.center, "Malha da copa");
            Transform impacto = Novo("Ponto da madeira", pivo);
            Vector3 topo = par.Superior.Volume.center;
            topo.y = par.Superior.Volume.max.y - par.Superior.Volume.size.y * .10f;
            impacto.localPosition = encaixe.localPosition + Vector3.Scale((topo - bordaCopa.center) * escala, encaixe.localScale);

            antigos.Add(jaMontada ? raizAntiga.gameObject : tocoAntigo.gameObject);
            if (!jaMontada) antigos.Add(copaAntiga.gameObject);
            tronco.objectReferenceValue = toco;
            copa.objectReferenceValue = pivo;
            ponta.objectReferenceValue = impacto;
        }
        ser.ApplyModifiedProperties();
        return antigos;
    }

    private static void ComporMata(Transform mata, Transform corte, SoloDaFloresta solo, GameObject arv1, GameObject arv2,
        GameObject grama, GameObject cacto, Material matCacto, SerializedObject dados, Vector3 destino)
    {
        var fundo = Novo(EstratoAlto, mata);
        var baixo = Novo(EstratoBaixo, mata);
        var random = new System.Random(12289);
        var ocupadas = corte.Cast<Transform>()
            .Select(arvore => Limites(arvore.Find("Toco fixo"), Matrix4x4.identity)).ToList();
        int postos = Mathf.Clamp(dados.FindProperty("forestPositions").intValue, 1, 6);
        float espacamento = dados.FindProperty("forestSpacing").floatValue;
        Vector3 deslocamento = dados.FindProperty("forestLineOffset").vector3Value;
        var posicoesDeTrabalho = new Vector3[postos];
        for (int i = 0; i < postos; ++i)
            posicoesDeTrabalho[i] = destino + deslocamento + Vector3.forward * ((i - (postos - 1) * .5f) * espacamento);
        var rota = dados.FindProperty("forestWaypoints");
        var caminho = new List<Vector3>();
        for (int i = 0; rota != null && i < rota.arraySize; ++i)
            if (rota.GetArrayElementAtIndex(i).objectReferenceValue is Transform ponto)
                caminho.Add(ponto.position);
        caminho.Add(destino);

        bool Livre(Vector3 p, float margem, bool respeitarQueda = false)
        {
            foreach (Vector3 posto in posicoesDeTrabalho)
            {
                if (Distancia2D(p, posto) < margem + .45f) return false;
            }
            for (int i = 1; i < caminho.Count; ++i)
                if (DistanciaAteTrecho(p, caminho[i - 1], caminho[i]) < margem + .34f) return false;
            if (!respeitarQueda) return true;

            foreach (Transform arvore in corte)
            {
                Vector3 raiz = arvore.position;
                foreach (Vector3 posto in posicoesDeTrabalho)
                {
                    Vector3 direcao = raiz - posto;
                    direcao.y = 0f;
                    Vector3 fim = raiz + direcao.normalized * 2.60f;
                    if (DistanciaAteTrecho(p, raiz, fim) < margem + .25f) return false;
                }
            }
            return true;
        }
        Vector3 Ponto(Vector2 p)
        {
            Vector3 mundo = destino + new Vector3(p.x, 0f, p.y);
            mundo.y = solo.Altura(mundo) - .008f;
            return mundo;
        }
        bool Cabe(Bounds novo, float folga)
        {
            foreach (Bounds existente in ocupadas)
            {
                if (Mathf.Abs(novo.center.x - existente.center.x) < novo.extents.x + existente.extents.x + folga &&
                    Mathf.Abs(novo.center.z - existente.center.z) < novo.extents.z + existente.extents.z + folga)
                    return false;
            }
            return true;
        }
        void Arvores(Vector2[] pontos, float minima, float maxima, string nome, bool alternar)
        {
            for (int i = 0; i < pontos.Length; ++i)
            {
                Vector3 pos = Ponto(pontos[i]);
                if (!Livre(pos, .45f, true)) continue;
                var fonte = (i % 3 == 1) == alternar ? arv1 : arv2;
                Bounds? pe = Plantar(fundo, fonte, $"{nome} {i + 1:00}", pos, Sorteio(random, minima, maxima),
                    i * 137.508f + Sorteio(random, -27f, 27f), true, true, b => Cabe(b, .27f));
                if (pe.HasValue) ocupadas.Add(pe.Value);
            }
        }
        Arvores(FundoAlto, 2.29f, 3.16f, "Árvore de fundo", true);
        Arvores(FundoBaixo, 1.27f, 2.12f, "Árvore menor", false);
        for (int i = 0; i < Cactos.Length; ++i)
        {
            Vector3 p = Ponto(Cactos[i]);
            if (!Livre(p, .31f, true)) continue;
            Bounds? volume = Plantar(fundo, cacto, $"Cacto {i + 1:00}", p, Sorteio(random, .51f, 1.23f),
                Sorteio(random, 0f, 360f), true, false, b => Cabe(b, .16f), matCacto);
            if (volume.HasValue) ocupadas.Add(volume.Value);
        }
        Mesh[] pedras = AssetDatabase.LoadAllAssetsAtPath(Modelos + "Pedras/Modelo/Pedras.fbx")
            .OfType<Mesh>().OrderBy(m => m.name, StringComparer.Ordinal).ToArray();
        if (pedras.Length < 3) throw new InvalidOperationException("O FBX Pedras precisa conter as três variações.");
        var clara = Carregar<Material>(Modelos + "Pedras/Materiais/M_Pedras_Clara.mat");
        var escura = Carregar<Material>(Modelos + "Pedras/Materiais/M_Pedras_Escura.mat");
        for (int i = 0; i < GruposDePedras.Length; ++i)
        {
            int quantidade = i % 3 == 0 ? 3 : 2;
            for (int j = 0; j < quantidade; ++j)
            {
                float angulo = (i * 2.4f + j * 2.1f);
                Vector2 desloc = j == 0 ? Vector2.zero : new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * (.34f + j * .10f);
                Vector3 p = Ponto(GruposDePedras[i] + desloc);
                if (!Livre(p, .23f)) continue;
                var obj = new GameObject($"Pedra {i + 1:00}.{j + 1}", typeof(MeshFilter), typeof(MeshRenderer));
                try
                {
                    Transform pedra = obj.transform;
                    pedra.SetParent(baixo, false);
                    pedra.GetComponent<MeshFilter>().sharedMesh = pedras[(i + j) % 3];
                    pedra.GetComponent<MeshRenderer>().sharedMaterial = (i + j) % 4 == 1 ? clara : escura;
                    pedra.rotation = Quaternion.Euler(0f, Sorteio(random, 0f, 360f), 0f) * Quaternion.Euler(-90f, 0f, 0f);
                    float altura = j == 0 ? Sorteio(random, .15f, .28f) : Sorteio(random, .065f, .135f);
                    Bounds volume = AjustarBase(pedra, p, altura, false, j == 0 ? .78f : .38f);
                    if (!Cabe(volume, .09f)) continue;
                    Undo.RegisterCreatedObjectUndo(obj, Operacao);
                    ocupadas.Add(volume);
                    obj = null;
                }
                finally
                {
                    if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }
        for (int i = 0; i < Touceiras.Length; ++i)
        {
            int quantidade = i % 5 == 0 ? 6 : 3 + i % 2;
            float giroTouceira = Sorteio(random, 0f, Mathf.PI * 2f);
            for (int j = 0; j < quantidade; ++j)
            {
                float giro = giroTouceira + j * 2.4f;
                float distancia = j == 0 ? 0f : .11f + j * .077f;
                Vector2 desloc = new Vector2(Mathf.Cos(giro), Mathf.Sin(giro) * .67f) * distancia;
                Vector3 p = Ponto(Touceiras[i] + desloc);
                if (Livre(p, .12f))
                    Plantar(baixo, grama, $"Mato {i + 1:00}.{j + 1}", p,
                        Sorteio(random, j == 0 ? .26f : .11f, j == 0 ? .46f : .34f),
                        Sorteio(random, 0f, 360f), false, false, b => Cabe(b, .015f));
            }
        }
    }

    private static float Distancia2D(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    private static float DistanciaAteTrecho(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 trecho = new Vector2(b.x - a.x, b.z - a.z);
        Vector2 delta = new Vector2(p.x - a.x, p.z - a.z);
        float t = trecho.sqrMagnitude <= .0001f ? 0f : Mathf.Clamp01(Vector2.Dot(delta, trecho) / trecho.sqrMagnitude);
        return (delta - trecho * t).magnitude;
    }

    private static Bounds? Plantar(Transform pai, GameObject fonte, string nome, Vector3 p,
        float altura, float giro, bool sombra, bool alinharPe, Func<Bounds, bool> cabe, Material material = null)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(fonte, pai);
        if (obj == null) throw new InvalidOperationException("Falha ao instanciar " + fonte.name);
        try
        {
            obj.name = nome;
            obj.transform.rotation = Quaternion.Euler(0f, giro, 0f) * fonte.transform.localRotation;
            Bounds baseModelo = AjustarBase(obj.transform, p, altura, alinharPe);
            if (!cabe(baseModelo)) return null;
            foreach (MeshRenderer r in obj.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = sombra ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            Undo.RegisterCreatedObjectUndo(obj, Operacao);
            obj = null;
            return baseModelo;
        }
        finally
        {
            if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    private static Bounds AjustarBase(Transform t, Vector3 p, float altura, bool alinharPe, float larguraMaxima = float.PositiveInfinity)
    {
        t.position = p;
        Bounds b = Limites(t, Matrix4x4.identity);
        if (b.size.y < .0001f) throw new InvalidOperationException("O modelo não tem altura: " + t.name);
        t.localScale *= Mathf.Min(altura / b.size.y, larguraMaxima / Mathf.Max(.0001f, Mathf.Max(b.size.x, b.size.z)));
        b = Limites(t, Matrix4x4.identity);
        Bounds pe = alinharPe ? LimitesDoPe(t, b) : b;
        Vector3 ajuste = p - new Vector3(pe.center.x, b.min.y, pe.center.z);
        t.position += ajuste;
        pe.center += ajuste;
        return pe;
    }

    private static int CorrigirCercas(GameObject[] raizes)
    {
        var pequena = Carregar<Material>(Modelos + "CercaPequena/Materiais/M_CercaPequena.mat");
        var grande = Carregar<Material>(Modelos + "CercaGrande/Materiais/M_CercaGrande.mat");
        int total = 0;
        foreach (MeshFilter f in raizes.SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)))
        {
            if (f.sharedMesh == null || !f.TryGetComponent<MeshRenderer>(out var render)) continue;
            string path = AssetDatabase.GetAssetPath(f.sharedMesh);
            Material material;
            switch (path)
            {
                case Modelos + "CercaPequena/Modelo/CercaPequena.fbx":
                case Modelos + "Modelos GS4/Cerca Pequena.fbx":
                    material = pequena;
                    break;
                case Modelos + "CercaGrande/Modelo/CercaGrande.fbx":
                case Modelos + "Modelos GS4/Cerca Grande.fbx":
                    material = grande;
                    break;
                default: continue;
            }
            if (render.sharedMaterials.Length == 1 && render.sharedMaterial == material) continue;
            Undo.RecordObject(render, Operacao);
            render.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(render);
            ++total;
        }
        return total;
    }

    private static int RemoverSolosAntigos(GameObject[] raizes)
    {
        var config = Resources.Load("MandalaPlowedGround");
        var material = config != null ? new SerializedObject(config).FindProperty("material")?.objectReferenceValue as Material : null;
        if (material == null || !material.HasProperty("_MandalaGround"))
        {
            Debug.LogWarning("Sertão: os visuais legados foram mantidos porque o material da Mandala não está disponível.");
            return 0;
        }

        int total = 0;
        foreach (Plot plot in raizes.SelectMany(g => g.GetComponentsInChildren<Plot>(true)))
        {
            var state = new SerializedObject(plot);
            var visual = state.FindProperty("plowedVisual");
            if (!(visual?.objectReferenceValue is GameObject objeto) ||
                !objeto.transform.IsChildOf(plot.transform) || objeto == plot.gameObject) continue;
            MeshFilter[] filtros = objeto.GetComponentsInChildren<MeshFilter>(true);
            if (filtros.Length == 0 || filtros.Any(f => f.sharedMesh == null ||
                AssetDatabase.GetAssetPath(f.sharedMesh) != TerraAntiga) ||
                objeto.GetComponentsInChildren<MonoBehaviour>(true).Length != 0) continue;
            visual.objectReferenceValue = null;
            state.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(objeto);
            ++total;
        }
        return total;
    }

    private static Bounds Limites(Transform t, Matrix4x4 paraQuadro)
    {
        var volume = new Bounds();
        bool temMalha = false;
        foreach (MeshFilter filtro in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filtro.sharedMesh == null || filtro.sharedMesh.bounds.size.sqrMagnitude < .00000001f) continue;
            Bounds b = Caixa(filtro.sharedMesh.bounds, paraQuadro * filtro.transform.localToWorldMatrix);
            if (temMalha) volume.Encapsulate(b);
            else { volume = b; temMalha = true; }
        }
        if (!temMalha) throw new InvalidOperationException("Modelo sem malha válida: " + t.name);
        return volume;
    }

    private static Bounds LimitesDoPe(Transform t, Bounds volume)
    {
        Bounds pe = new Bounds();
        bool temVertices = false;
        float teto = volume.min.y + Mathf.Min(volume.size.y * .38f, .58f);
        foreach (MeshFilter filtro in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filtro.sharedMesh == null) continue;
            foreach (Vector3 p in LerVertices(filtro.sharedMesh, filtro.transform.localToWorldMatrix))
            {
                if (p.y > teto) continue;
                if (temVertices) pe.Encapsulate(p);
                else { pe = new Bounds(p, Vector3.zero); temVertices = true; }
            }
        }
        return temVertices ? pe : volume;
    }

    private static Vector3[] LerVertices(Mesh malha, Matrix4x4 matriz)
    {
        using var dados = MeshUtility.AcquireReadOnlyMeshData(malha);
        if (!dados[0].HasVertexAttribute(VertexAttribute.Position)) return Array.Empty<Vector3>();
        using var vertices = new Unity.Collections.NativeArray<Vector3>(dados[0].vertexCount, Unity.Collections.Allocator.Temp);
        dados[0].GetVertices(vertices);
        var resultado = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; ++i)
            resultado[i] = matriz.MultiplyPoint3x4(vertices[i]);
        return resultado;
    }

    private static Bounds Caixa(Bounds local, Matrix4x4 matriz)
    {
        Vector3 e = local.extents;
        Vector3 a = matriz.MultiplyVector(Vector3.right * e.x);
        Vector3 b = matriz.MultiplyVector(Vector3.up * e.y);
        Vector3 c = matriz.MultiplyVector(Vector3.forward * e.z);
        Vector3 d = new Vector3(Mathf.Abs(a.x) + Mathf.Abs(b.x) + Mathf.Abs(c.x),
            Mathf.Abs(a.y) + Mathf.Abs(b.y) + Mathf.Abs(c.y), Mathf.Abs(a.z) + Mathf.Abs(b.z) + Mathf.Abs(c.z));
        return new Bounds(matriz.MultiplyPoint3x4(local.center), d * 2f);
    }

    private static float Sorteio(System.Random r, float a, float b) => Mathf.Lerp(a, b, (float)r.NextDouble());

    private sealed class Par
    {
        public readonly Peca Inferior;
        public readonly Peca Superior;
        public Par(Transform raiz, MeshFilter[] partes, string baseNome, string baseMalha, string copaNome,
            string copaMalha)
        {
            Inferior = new Peca(raiz, Encontrar(partes, baseNome, baseMalha), true);
            Superior = new Peca(raiz, Encontrar(partes, copaNome, copaMalha), false);
            if (Inferior.Volume.size.y >= Superior.Volume.size.y)
                throw new InvalidOperationException("O modelo de árvore cortada precisa manter a orientação vertical do FBX.");
        }

        private static MeshFilter Encontrar(MeshFilter[] filtros, string objeto, string malha)
        {
            var resultado = filtros.FirstOrDefault(f => f.sharedMesh != null && f.name == objeto) ??
                filtros.FirstOrDefault(f => f.sharedMesh != null && f.sharedMesh.name == malha);
            if (resultado == null) throw new InvalidOperationException("Peça ausente em NovasArvores.fbx: " + objeto);
            return resultado;
        }
    }

    private sealed class Peca
    {
        private readonly Mesh malha;
        private readonly Material[] materiais;
        private readonly Matrix4x4 quadro;
        public readonly Bounds Volume;
        public readonly Bounds FaceCorte;

        public Peca(Transform raiz, MeshFilter filtro, bool inferior)
        {
            malha = filtro.sharedMesh;
            if (!filtro.TryGetComponent<MeshRenderer>(out var renderer))
                throw new InvalidOperationException("Peça sem MeshRenderer no prefab NovasArvores: " + filtro.name);
            materiais = renderer.sharedMaterials;
            if (materiais.Length == 0 || materiais.Any(m => m == null))
                throw new InvalidOperationException("Peça sem material no prefab NovasArvores: " + filtro.name);
            quadro = raiz.worldToLocalMatrix * filtro.transform.localToWorldMatrix;
            Volume = Caixa(malha.bounds, quadro);
            FaceCorte = MedirCorte(inferior);
        }

        private Bounds MedirCorte(bool inferior)
        {
            Vector3[] vertices = LerVertices(malha, quadro);
            if (vertices.Length == 0)
                throw new InvalidOperationException("A peça não tem vértices para medir a face de corte: " + malha.name);
            float extremo = inferior ? vertices.Max(v => v.y) : vertices.Min(v => v.y);
            float faixa = Volume.size.y * (inferior ? .20f : .042f);
            Bounds face = new Bounds();
            bool iniciou = false;
            foreach (Vector3 p in vertices)
            {
                if (Mathf.Abs(p.y - extremo) > faixa) continue;
                if (iniciou) face.Encapsulate(p);
                else { face = new Bounds(p, Vector3.zero); iniciou = true; }
            }
            if (face.size.x <= .00001f || face.size.z <= .00001f)
                throw new InvalidOperationException("A face de corte é inválida em " + malha.name);
            return face;
        }

        public void Instalar(Transform pai, float escala, Vector3 origem, string nome)
        {
            Transform t = Novo(nome, pai, typeof(MeshFilter), typeof(MeshRenderer));
            var filtro = t.GetComponent<MeshFilter>();
            MeshRenderer r = t.GetComponent<MeshRenderer>();
            Undo.RecordObjects(new UnityEngine.Object[] { filtro, r }, Operacao);
            filtro.sharedMesh = malha;
            r.sharedMaterials = materiais;
            r.receiveShadows = true;
            Vector3 x = quadro.GetColumn(0), y = quadro.GetColumn(1), z = quadro.GetColumn(2);
            t.localRotation = Quaternion.LookRotation(z, y);
            float sinal = Vector3.Dot(Vector3.Cross(y, z), x) < 0f ? -1f : 1f;
            t.localScale = new Vector3(x.magnitude * sinal, y.magnitude, z.magnitude) * escala;
            t.localPosition = ((Vector3)quadro.GetColumn(3) - origem) * escala;
        }
    }

    private sealed class SoloDaFloresta : IDisposable
    {
        private readonly List<MeshCollider> superficies = new List<MeshCollider>();
        private readonly GameObject temporario;
        private readonly float padrao;
        private readonly bool backfaces;

        public SoloDaFloresta(GameObject[] raizes, float altura)
        {
            padrao = altura;
            backfaces = Physics.queriesHitBackfaces;
            temporario = new GameObject("Amostra temporária do chão") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Physics.queriesHitBackfaces = true;
                foreach (MeshFilter f in raizes.SelectMany(g => g.GetComponentsInChildren<MeshFilter>()))
                {
                    if (f.sharedMesh == null || !f.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)
                        continue;
                    string path = AssetDatabase.GetAssetPath(f.sharedMesh);
                    if (!path.StartsWith(Modelos + "ChaoAreia/", StringComparison.Ordinal) &&
                        !path.StartsWith(Modelos + "ChaoGrande2/", StringComparison.Ordinal)) continue;
                    var sonda = new GameObject(f.name) { hideFlags = HideFlags.HideAndDontSave };
                    sonda.transform.SetParent(temporario.transform, false);
                    sonda.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation);
                    sonda.transform.localScale = f.transform.lossyScale;
                    var colisor = sonda.AddComponent<MeshCollider>();
                    colisor.sharedMesh = f.sharedMesh;
                    superficies.Add(colisor);
                }
                Physics.SyncTransforms();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public float Altura(Vector3 p)
        {
            float encontrada = float.NegativeInfinity;
            var ray = new Ray(new Vector3(p.x, padrao + 6f, p.z), Vector3.down);
            foreach (MeshCollider c in superficies)
                if (c.Raycast(ray, out var hit, 12f)) encontrada = Mathf.Max(encontrada, hit.point.y);
            return float.IsNegativeInfinity(encontrada) ? padrao : encontrada;
        }

        public void Dispose()
        {
            Physics.queriesHitBackfaces = backfaces;
            if (temporario != null) UnityEngine.Object.DestroyImmediate(temporario);
        }
    }
}
