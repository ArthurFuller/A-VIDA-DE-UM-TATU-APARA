using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SertaoRespingosDaChuva
{
    private const string Operacao = "Corrigir respingos da chuva";
    private const string PlanoDoSolo = "Clima - altura do solo";

    [MenuItem("Tools/Sertão/Corrigir respingos da chuva")]
    private static void Aplicar()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (cena.path != "Assets/Scenes/PrototypeLayout.unity" ||
            EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(Operacao, "Abra PrototypeLayout fora do Play Mode.", "OK");
            return;
        }

        GameObject[] raizes = cena.GetRootGameObjects();
        var clima = raizes.SelectMany(r => r.GetComponentsInChildren<ClimateParticleController>(true)).SingleOrDefault();
        var chuva = clima != null
            ? new SerializedObject(clima).FindProperty("rain")?.objectReferenceValue as ParticleSystem
            : null;
        if (chuva == null || !TemRespingosDeColisao(chuva))
        {
            EditorUtility.DisplayDialog(Operacao, "A chuva precisa ter o subemissor de respingos atribuído à colisão.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Operacao);
        try
        {
            Transform plano = raizes.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == PlanoDoSolo);
            if (plano == null)
            {
                var objeto = new GameObject(PlanoDoSolo);
                plano = objeto.transform;
                Undo.RegisterCreatedObjectUndo(objeto, Operacao);
            }

            Vector3 posicao = plano.position;
            bool amostrouSolo = TentarMedirAltura(raizes, chuva, out float altura);
            if (amostrouSolo) posicao.y = altura + .02f;
            Undo.RecordObject(plano, Operacao);
            plano.SetPositionAndRotation(posicao, Quaternion.identity);
            Undo.RecordObject(chuva, Operacao);
            var colisao = chuva.collision;
            colisao.enabled = true;
            colisao.type = ParticleSystemCollisionType.Planes;
            colisao.mode = ParticleSystemCollisionMode.Collision3D;
            colisao.SetPlane(0, plano);
            GarantirTempoDeQueda(chuva, posicao.y);

            Undo.CollapseUndoOperations(grupo);
            EditorSceneManager.MarkSceneDirty(cena);
            string detalhe = amostrouSolo
                ? $"O plano foi alinhado ao chão em Y = {posicao.y:F2}."
                : $"Usei a altura atual de {PlanoDoSolo} em Y = {posicao.y:F2}. Ajuste o Y do objeto se o chão estiver em outra altura.";
            EditorUtility.DisplayDialog(Operacao, detalhe + " Salve a cena com Ctrl+S.", "OK");
        }
        catch (Exception erro)
        {
            Undo.RevertAllDownToGroup(grupo);
            Debug.LogException(erro);
            EditorUtility.DisplayDialog(Operacao, erro.Message, "OK");
        }
    }

    private static bool TemRespingosDeColisao(ParticleSystem chuva)
    {
        var sub = chuva.subEmitters;
        if (!sub.enabled) return false;
        for (int i = 0; i < sub.subEmittersCount; ++i)
        {
            if (sub.GetSubEmitterType(i) == ParticleSystemSubEmitterType.Collision &&
                sub.GetSubEmitterSystem(i) != null) return true;
        }
        return false;
    }

    private static void GarantirTempoDeQueda(ParticleSystem chuva, float ySolo)
    {
        var velocidade = chuva.velocityOverLifetime;
        var particulas = chuva.main;
        if (!velocidade.enabled || velocidade.y.mode != ParticleSystemCurveMode.Constant ||
            particulas.startLifetime.mode != ParticleSystemCurveMode.Constant || velocidade.y.constant >= -.1f) return;

        var forma = chuva.shape;
        float yEmissao = chuva.transform.position.y;
        if (forma.enabled)
            yEmissao = chuva.transform.TransformPoint(forma.position).y +
                Mathf.Abs(forma.scale.y * chuva.transform.lossyScale.y) * .5f;
        float segundos = Mathf.Max(0f, yEmissao - ySolo) / -velocidade.y.constant + .15f;
        particulas.startLifetime = Mathf.Max(particulas.startLifetime.constant, segundos);
    }

    private static bool TentarMedirAltura(GameObject[] raizes, ParticleSystem chuva, out float altura)
    {
        altura = 0f;
        var pisos = new List<MeshCollider>();
        var alturas = new List<float>(9);
        bool backfaces = Physics.queriesHitBackfaces;
        var sondas = new GameObject("Sondas temporárias - chuva") { hideFlags = HideFlags.HideAndDontSave };

        try
        {
            Physics.queriesHitBackfaces = true;
            foreach (MeshFilter filtro in raizes.SelectMany(r => r.GetComponentsInChildren<MeshFilter>()))
            {
                if (filtro.sharedMesh == null || !filtro.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                    continue;
                string caminho = AssetDatabase.GetAssetPath(filtro.sharedMesh);
                if (!caminho.StartsWith("Assets/Modelos/ChaoAreia/", StringComparison.Ordinal) &&
                    !caminho.StartsWith("Assets/Modelos/ChaoGrande2/", StringComparison.Ordinal)) continue;

                var sonda = new GameObject(filtro.name) { hideFlags = HideFlags.HideAndDontSave };
                sonda.transform.SetParent(sondas.transform, false);
                sonda.transform.SetPositionAndRotation(filtro.transform.position, filtro.transform.rotation);
                sonda.transform.localScale = filtro.transform.lossyScale;
                var collider = sonda.AddComponent<MeshCollider>();
                collider.sharedMesh = filtro.sharedMesh;
                pisos.Add(collider);
            }
            Physics.SyncTransforms();
            Vector3 emissor = chuva.transform.position;
            Vector3 tamanho = chuva.shape.scale;
            for (int x = -1; x <= 1; ++x)
            {
                for (int z = -1; z <= 1; ++z)
                {
                    Vector3 origem = emissor + new Vector3(x * tamanho.x * .30f, 0f, z * tamanho.z * .30f);
                    var raio = new Ray(origem, Vector3.down);
                    float y = float.NegativeInfinity;
                    foreach (MeshCollider piso in pisos)
                        if (piso.Raycast(raio, out var hit, 20f)) y = Mathf.Max(y, hit.point.y);
                    if (!float.IsNegativeInfinity(y)) alturas.Add(y);
                }
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
            UnityEngine.Object.DestroyImmediate(sondas);
        }

        if (alturas.Count == 0) return false;
        alturas.Sort();
        altura = alturas[(alturas.Count - 1) * 3 / 4];
        return true;
    }
}
