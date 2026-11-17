using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SertaoAjustesParticulas
{
    private const string MenuPath = "Tools/Sertão/Aplicar ajustes de partículas";

    [MenuItem(MenuPath, true)]
    private static bool CanApply() => !EditorApplication.isPlayingOrWillChangePlaymode &&
        SceneManager.GetActiveScene().isLoaded;

    [MenuItem(MenuPath)]
    private static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        ClimateParticleController climate = null;
        foreach (GameObject root in roots)
        {
            climate = root.GetComponentInChildren<ClimateParticleController>(true);
            if (climate != null) break;
        }
        if (climate == null)
        {
            Debug.LogWarning("Abra a cena de gameplay do Sertão para aplicar os ajustes de partículas.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Ajustar partículas do Sertão");
        var climateSettings = new SerializedObject(climate);
        var droughtDust = climateSettings.FindProperty("droughtDust").objectReferenceValue as ParticleSystem;
        if (droughtDust != null)
        {
            Undo.RegisterCompleteObjectUndo(droughtDust, "Ajustar vento da seca");
            var wind = droughtDust.velocityOverLifetime;
            wind.enabled = true;
            wind.space = ParticleSystemSimulationSpace.World;
            wind.x = 0.58f;
            Record(droughtDust);
        }
        climateSettings.FindProperty("tumbleweedDiameter").floatValue = 0.73f;
        climateSettings.ApplyModifiedProperties();

        var bonusLights = new HashSet<ParticleSystem>();
        foreach (GameObject root in roots)
        {
            foreach (MandalaSynergy synergy in root.GetComponentsInChildren<MandalaSynergy>(true))
            {
                var settings = new SerializedObject(synergy);
                SerializedProperty slots = settings.FindProperty("slotEffects");
                for (int i = 0; i < slots.arraySize; i++)
                {
                    var particles = slots.GetArrayElementAtIndex(i).FindPropertyRelative("lightParticles")
                        .objectReferenceValue as ParticleSystem;
                    if (particles != null && bonusLights.Add(particles)) ApplyBonusLights(particles);
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Ajustes de partículas aplicados em {scene.name}. Luzes de bônus: {bonusLights.Count}. Salve a cena.");
    }

    private static void ApplyBonusLights(ParticleSystem particles)
    {
        Undo.RegisterCompleteObjectUndo(particles, "Ajustar luzes de bônus da mandala");
        var main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.72f, 1.12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.082f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.97f, 0.78f, 0.39f, 0.64f), new Color(1f, 0.92f, 0.68f, 0.83f));
        var rise = particles.velocityOverLifetime;
        rise.enabled = true;
        rise.y = 0.36f;
        var shape = particles.shape;
        shape.scale = new Vector3(0.54f, 0.09f, 0.54f);
        var emission = particles.emission;
        emission.rateOverTime = 4.5f;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        var scale = new AnimationCurve(new Keyframe(0f, 0.62f), new Keyframe(0.38f, 1f), new Keyframe(1f, 0.32f));
        size.size = new ParticleSystem.MinMaxCurve(1f, scale);
        FadeParticles(particles, 0.24f, 0.66f);
        Record(particles);
    }

    private static void FadeParticles(ParticleSystem particles, float appearAt, float fadeAt)
    {
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, appearAt),
                new GradientAlphaKey(0.63f, fadeAt), new GradientAlphaKey(0f, 1f) });
        var color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = fade;
    }

    private static void Record(ParticleSystem particles)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
        EditorUtility.SetDirty(particles);
    }
}
