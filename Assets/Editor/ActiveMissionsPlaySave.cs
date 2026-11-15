// Temporario: para retirar o salvamento automatico no fim do projeto,
// basta excluir este arquivo (e seu .meta) de Assets/Editor.
// Nunca e compilado para as builds do jogo.
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ActiveMissionsPlaySave
{
    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public string hierarchyPath;
        public float[] floats;
        public Vector2[] vectors;
        public string[] spriteIds;
        public bool changed;
    }

    private const string Key = "Sertao.ActiveMissions.PlaySettings.v1";
    private static double nextCaptureTime;

    // Apenas os ajustes visuais, nunca o estado de missoes ou os RectTransforms
    // animados em runtime. Isso impede salvar estados temporarios do gameplay.
    private static readonly string[] FloatFields =
    {
        "firstCardTopPadding", "cardSpacing", "cardHeight", "listBottomPadding",
        "iconToTextGap", "quantityRightMargin", "animationDuration", "hiddenOffset",
        "cardDropDuration", "cardWaveInterval", "cardDropDistance"
    };

    private static readonly string[] VectorFields =
    {
        "itemIconSize", "itemIconPosition"
    };

    static ActiveMissionsPlaySave()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += EditorUpdate;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            SessionState.EraseString(Key);
            ActiveMissionsPanel panel = FindPanel(SceneManager.GetActiveScene().path, null);
            Settings initial = Capture(panel);
            if (initial != null)
                SessionState.SetString(Key, JsonUtility.ToJson(initial));
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Settings previous = Read();
            if (previous == null || previous.changed) return;
            ActiveMissionsPanel panel = FindPanel(previous.scenePath, previous.hierarchyPath);
            Settings current = Capture(panel);
            if (current == null) return;
            current.changed = Different(previous, current);
            SessionState.SetString(Key, JsonUtility.ToJson(current));
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall -= ApplyPending;
            EditorApplication.delayCall += ApplyPending;
        }
    }

    private static void EditorUpdate()
    {
        if (!EditorApplication.isPlaying)
        {
            ApplyPending();
            return;
        }

        if (EditorApplication.timeSinceStartup < nextCaptureTime) return;
        nextCaptureTime = EditorApplication.timeSinceStartup + 0.12d;

        Settings previous = Read();
        if (previous == null) return;
        ActiveMissionsPanel panel = FindPanel(previous.scenePath, previous.hierarchyPath);
        Settings current = Capture(panel);
        if (current == null || !Different(previous, current)) return;
        current.changed = true;
        SessionState.SetString(Key, JsonUtility.ToJson(current));
    }

    private static ActiveMissionsPanel FindPanel(string scenePath, string hierarchyPath)
    {
        if (string.IsNullOrEmpty(scenePath)) return null;
        ActiveMissionsPanel soleMatch = null;
        int matchCount = 0;
        foreach (ActiveMissionsPanel panel in UnityEngine.Object.FindObjectsByType<ActiveMissionsPanel>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (panel.gameObject.scene.path != scenePath) continue;
            matchCount++;
            soleMatch = panel;
            if (string.IsNullOrEmpty(hierarchyPath) || HierarchyPath(panel.transform) == hierarchyPath)
                return panel;
        }
        return matchCount == 1 ? soleMatch : null;
    }

    private static string HierarchyPath(Transform transform)
    {
        // Caminho completo com indices para nao depender de nomes duplicados.
        string path = "";
        while (transform != null)
        {
            path = transform.GetSiblingIndex() + "/" + path;
            transform = transform.parent;
        }
        return path;
    }

    private static Settings Capture(ActiveMissionsPanel panel)
    {
        if (panel == null) return null;
        var source = new SerializedObject(panel);
        var settings = new Settings
        {
            scenePath = panel.gameObject.scene.path,
            hierarchyPath = HierarchyPath(panel.transform),
            floats = new float[FloatFields.Length],
            vectors = new Vector2[VectorFields.Length]
        };

        for (int i = 0; i < FloatFields.Length; i++)
        {
            SerializedProperty property = source.FindProperty(FloatFields[i]);
            if (property == null) return null;
            settings.floats[i] = property.floatValue;
        }
        for (int i = 0; i < VectorFields.Length; i++)
        {
            SerializedProperty property = source.FindProperty(VectorFields[i]);
            if (property == null) return null;
            settings.vectors[i] = property.vector2Value;
        }
        SerializedProperty icons = source.FindProperty("itemIcons");
        if (icons != null && icons.isArray)
        {
            settings.spriteIds = new string[icons.arraySize];
            for (int i = 0; i < icons.arraySize; i++)
            {
                UnityEngine.Object sprite = icons.GetArrayElementAtIndex(i).objectReferenceValue;
                settings.spriteIds[i] = sprite != null
                    ? GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString() : "";
            }
        }
        return settings;
    }

    private static bool Different(Settings a, Settings b)
    {
        if (a.floats == null || b.floats == null ||
            a.floats.Length != b.floats.Length ||
            a.vectors == null || b.vectors == null ||
            a.vectors.Length != b.vectors.Length) return true;

        for (int i = 0; i < a.floats.Length; i++)
            if (!Mathf.Approximately(a.floats[i], b.floats[i])) return true;
        for (int i = 0; i < a.vectors.Length; i++)
            if ((a.vectors[i] - b.vectors[i]).sqrMagnitude > 0.0001f) return true;

        if (a.spriteIds == null || b.spriteIds == null)
            return a.spriteIds != b.spriteIds;
        if (a.spriteIds.Length != b.spriteIds.Length) return true;
        for (int i = 0; i < a.spriteIds.Length; i++)
            if (a.spriteIds[i] != b.spriteIds[i]) return true;
        return false;
    }

    private static Settings Read()
    {
        string data = SessionState.GetString(Key, "");
        return string.IsNullOrEmpty(data) ? null : JsonUtility.FromJson<Settings>(data);
    }

    private static void ApplyPending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Settings saved = Read();
        if (saved == null) return;
        if (!saved.changed)
        {
            SessionState.EraseString(Key);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != saved.scenePath) return;
        ActiveMissionsPanel panel = FindPanel(saved.scenePath, saved.hierarchyPath);
        if (panel == null) return;

        var destination = new SerializedObject(panel);
        for (int i = 0; saved.floats != null && i < FloatFields.Length &&
            i < saved.floats.Length; i++)
        {
            SerializedProperty property = destination.FindProperty(FloatFields[i]);
            if (property != null) property.floatValue = saved.floats[i];
        }
        for (int i = 0; saved.vectors != null && i < VectorFields.Length &&
            i < saved.vectors.Length; i++)
        {
            SerializedProperty property = destination.FindProperty(VectorFields[i]);
            if (property != null) property.vector2Value = saved.vectors[i];
        }

        SerializedProperty icons = destination.FindProperty("itemIcons");
        if (icons != null && icons.isArray && saved.spriteIds != null)
        {
            icons.arraySize = saved.spriteIds.Length;
            for (int i = 0; i < saved.spriteIds.Length; i++)
            {
                UnityEngine.Object sprite = null;
                string id = saved.spriteIds[i];
                if (!string.IsNullOrEmpty(id) && GlobalObjectId.TryParse(id, out GlobalObjectId globalId))
                    sprite = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                // Nao substituir uma referencia valida se o asset nao for encontrado.
                if (string.IsNullOrEmpty(id) || sprite != null)
                    icons.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
            }
        }

        destination.ApplyModifiedPropertiesWithoutUndo();
        panel.RefreshEditorLayoutAfterPlayMode();
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
