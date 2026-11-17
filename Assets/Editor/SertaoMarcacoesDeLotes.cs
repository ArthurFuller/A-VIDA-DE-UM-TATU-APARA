using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

internal static class SertaoMarcacoesDeLotes
{
    private const string Operacao = "Aplicar marcações de lotes";
    private const string CenaPrincipal = "Assets/Scenes/PrototypeLayout.unity";
    private const string Pasta = "Assets/Generated/SertaoLotes";
    private const string NomeDaMarca = "Marcação do lote";
    private const int Divisoes = 12;

    [MenuItem("Tools/Sertão/Aplicar marcações de lotes")]
    private static void Aplicar()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (cena.path != CenaPrincipal || PrefabStageUtility.GetCurrentPrefabStage() != null ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(Operacao, "Abra a cena PrototypeLayout fora do Play Mode.", "OK");
            return;
        }

        GameObject[] raizes = cena.GetRootGameObjects();
        var terrenos = raizes.SelectMany(x => x.GetComponentsInChildren<Plot>(true)).ToArray();
        var casas = raizes.SelectMany(x => x.GetComponentsInChildren<HouseSite>(true)).ToArray();
        if (terrenos.Length == 0 && casas.Length == 0)
        {
            EditorUtility.DisplayDialog(Operacao, "Nenhum terreno ou lote de casa foi encontrado.", "OK");
            return;
        }

        Shader shader = Resources.Load<Shader>("PlotOutline");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
        {
            EditorUtility.DisplayDialog(Operacao, "O shader original PlotOutline precisa estar importado e sem erros.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Operacao);
        try
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/Generated", "SertaoLotes");
            Material claro = MaterialDaMarca("M_MarcaCultivo", shader, new Color32(255, 244, 218, 94));
            Material terra = MaterialDaMarca("M_MarcaCasa", shader, new Color32(123, 77, 46, 137));

            int cultivos = 0, lotes = 0;
            using (var solo = new SoloAmostrado(raizes))
            {
                foreach (Plot plot in terrenos)
                {
                    if (plot.IsWater) continue;
                    var dados = new SerializedObject(plot);
                    var visual = dados.FindProperty("surfaceRenderer").objectReferenceValue as Renderer;
                    if (visual != null && visual.TryGetComponent(out BoxCollider area))
                    {
                        Marcar(dados, plot.transform, visual, area, "Cultivo", claro, .025f, solo);
                        ++cultivos;
                    }
                }
                foreach (HouseSite casa in casas)
                {
                    if (casa.TryGetComponent(out Renderer visual) && casa.TryGetComponent(out BoxCollider area))
                    {
                        Marcar(new SerializedObject(casa), casa.transform, visual, area, "Casa", terra, .030f, solo);
                        ++lotes;
                    }
                }
            }
            int pecasOcultas = DesativarCercadoDeTeste(terrenos);
            int cercasCorrigidas = ConferirMateriaisDasCercas(raizes);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(grupo);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorUtility.DisplayDialog(Operacao,
                $"{cultivos} terrenos e {lotes} lotes com marcações. {pecasOcultas} peças do cercado de teste desativadas. " +
                $"{cercasCorrigidas} materiais de cerca reaplicados. Salve a cena com Ctrl+S.", "OK");
        }
        catch (Exception erro)
        {
            Undo.RevertAllDownToGroup(grupo);
            Debug.LogException(erro);
            EditorUtility.DisplayDialog(Operacao, erro.Message, "OK");
        }
    }

    private static Material MaterialDaMarca(string nome, Shader shader, Color cor)
    {
        string caminho = Pasta + "/" + nome + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(shader) { name = nome };
        material.SetColor("_Color", cor);
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }

    private static void Marcar(SerializedObject dados, Transform dono, Renderer visual, BoxCollider area,
        string tipo, Material material, float proporcao, SoloAmostrado solo)
    {
        SerializedProperty referencia = dados.FindProperty("idleOutline");
        var marca = referencia.objectReferenceValue as GameObject;
        if (marca == null)
        {
            marca = new GameObject(NomeDaMarca, typeof(MeshFilter), typeof(MeshRenderer));
            marca.transform.SetParent(dono, false);
            marca.layer = dono.gameObject.layer;
            Undo.RegisterCreatedObjectUndo(marca, Operacao);
        }
        Undo.RecordObject(marca.transform, Operacao);
        marca.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        marca.transform.localScale = Vector3.one;

        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(dono.gameObject);
        string chave = id.targetObjectId.ToString();
        if (id.targetObjectId == 0)
            chave = dono.GetInstanceID().ToString();
        string caminho = $"{Pasta}/{tipo}_{chave}.asset";
        Mesh malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
        if (malha == null)
        {
            malha = new Mesh { name = "Marcação - " + dono.name };
            AssetDatabase.CreateAsset(malha, caminho);
        }
        Undo.RecordObject(malha, Operacao);
        Traçar(malha, marca.transform, area, proporcao, solo);
        EditorUtility.SetDirty(malha);

        MeshFilter filtro = marca.GetComponent<MeshFilter>();
        MeshRenderer renderer = marca.GetComponent<MeshRenderer>();
        Undo.RecordObjects(new UnityEngine.Object[] { filtro, renderer, visual, marca }, Operacao);
        filtro.sharedMesh = malha;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        marca.SetActive(true);
        visual.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        referencia.objectReferenceValue = marca;
        dados.ApplyModifiedProperties();
    }

    private static void Traçar(Mesh malha, Transform marca, BoxCollider area, float proporcao, SoloAmostrado solo)
    {
        Vector3 tamanho = area.size;
        Vector3 centro = area.center;
        float lado = Mathf.Min(tamanho.x, tamanho.z);
        float recuo = lado * .038f;
        float largura = lado * proporcao;
        float esquerda = centro.x - tamanho.x * .5f + recuo;
        float direita = centro.x + tamanho.x * .5f - recuo;
        float frente = centro.z - tamanho.z * .5f + recuo;
        float fundo = centro.z + tamanho.z * .5f - recuo;
        var vertices = new List<Vector3>(Divisoes * 16);
        var indices = new List<int>(Divisoes * 24);

        Vector3 Projetar(float x, float z)
        {
            Vector3 p = area.transform.TransformPoint(new Vector3(x, centro.y, z));
            p.y = solo.Altura(p) + .017f;
            return marca.InverseTransformPoint(p);
        }

        void Faixa(float x0, float x1, float z0, float z1, bool horizontal)
        {
            for (int i = 0; i < Divisoes; ++i)
            {
                float a = i / (float)Divisoes;
                float b = (i + 1) / (float)Divisoes;
                float minX = horizontal ? Mathf.Lerp(x0, x1, a) : x0;
                float maxX = horizontal ? Mathf.Lerp(x0, x1, b) : x1;
                float minZ = horizontal ? z0 : Mathf.Lerp(z0, z1, a);
                float maxZ = horizontal ? z1 : Mathf.Lerp(z0, z1, b);
                int v = vertices.Count;
                vertices.Add(Projetar(minX, minZ));
                vertices.Add(Projetar(maxX, minZ));
                vertices.Add(Projetar(maxX, maxZ));
                vertices.Add(Projetar(minX, maxZ));
                indices.Add(v);
                indices.Add(v + 2);
                indices.Add(v + 1);
                indices.Add(v);
                indices.Add(v + 3);
                indices.Add(v + 2);
            }
        }
        Faixa(esquerda, esquerda + largura, frente, fundo, false);
        Faixa(direita - largura, direita, frente, fundo, false);
        Faixa(esquerda + largura, direita - largura, frente, frente + largura, true);
        Faixa(esquerda + largura, direita - largura, fundo - largura, fundo, true);
        malha.Clear();
        malha.SetVertices(vertices);
        malha.SetTriangles(indices, 0);
        malha.RecalculateBounds();
    }

    private static int DesativarCercadoDeTeste(IEnumerable<Plot> terrenos)
    {
        Plot teste = terrenos.SingleOrDefault(p => p.name == "Terreno 4-2");
        if (teste == null) return 0;
        var dados = new SerializedObject(teste);
        SerializedProperty pecas = dados.FindProperty("fenceParts");
        int total = 0;
        for (int i = 0; i < pecas.arraySize; ++i)
        {
            var peca = pecas.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (peca == null || !peca.activeSelf) continue;
            Undo.RecordObject(peca, Operacao);
            peca.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(peca);
            ++total;
        }
        return total;
    }

    private static int ConferirMateriaisDasCercas(GameObject[] raizes)
    {
        const string pastaModelos = "Assets/Modelos/";
        Material pequena = AssetDatabase.LoadAssetAtPath<Material>(pastaModelos + "CercaPequena/Materiais/M_CercaPequena.mat");
        Material grande = AssetDatabase.LoadAssetAtPath<Material>(pastaModelos + "CercaGrande/Materiais/M_CercaGrande.mat");
        int total = 0;
        foreach (MeshFilter f in raizes.SelectMany(x => x.GetComponentsInChildren<MeshFilter>(true)))
        {
            if (f.sharedMesh == null || !f.TryGetComponent(out MeshRenderer renderer)) continue;
            var destino = AssetDatabase.GetAssetPath(f.sharedMesh) switch
            {
                pastaModelos + "CercaPequena/Modelo/CercaPequena.fbx" => pequena,
                pastaModelos + "Modelos GS4/Cerca Pequena.fbx" => pequena,
                pastaModelos + "CercaGrande/Modelo/CercaGrande.fbx" => grande,
                pastaModelos + "Modelos GS4/Cerca Grande.fbx" => grande,
                _ => null
            };
            if (destino == null || renderer.sharedMaterial == destino) continue;
            Undo.RecordObject(renderer, Operacao);
            renderer.sharedMaterial = destino;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            ++total;
        }
        return total;
    }

    private sealed class SoloAmostrado : IDisposable
    {
        private readonly GameObject sondas = new GameObject("Amostra temporária para as marcações")
            { hideFlags = HideFlags.HideAndDontSave };
        private readonly List<MeshCollider> pisos = new List<MeshCollider>();
        private readonly bool backfaces = Physics.queriesHitBackfaces;

        public SoloAmostrado(GameObject[] raizes)
        {
            try
            {
                Physics.queriesHitBackfaces = true;
                foreach (MeshFilter f in raizes.SelectMany(x => x.GetComponentsInChildren<MeshFilter>()))
                {
                    if (f.sharedMesh == null || !f.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                        continue;
                    string caminho = AssetDatabase.GetAssetPath(f.sharedMesh);
                    if (!caminho.StartsWith("Assets/Modelos/ChaoAreia/", StringComparison.Ordinal) &&
                        !caminho.StartsWith("Assets/Modelos/ChaoGrande2/", StringComparison.Ordinal)) continue;
                    var obj = new GameObject(f.name) { hideFlags = HideFlags.HideAndDontSave };
                    obj.transform.SetParent(sondas.transform, false);
                    obj.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation);
                    obj.transform.localScale = f.transform.lossyScale;
                    var collider = obj.AddComponent<MeshCollider>();
                    collider.sharedMesh = f.sharedMesh;
                    pisos.Add(collider);
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
            var raio = new Ray(p + Vector3.up * 2f, Vector3.down);
            float y = float.NegativeInfinity;
            foreach (MeshCollider c in pisos)
                if (c.Raycast(raio, out var hit, 4f))
                    y = Mathf.Max(hit.point.y, y);
            return float.IsNegativeInfinity(y) ? p.y : y;
        }

        public void Dispose()
        {
            Physics.queriesHitBackfaces = backfaces;
            if (sondas != null) UnityEngine.Object.DestroyImmediate(sondas);
        }
    }
}
