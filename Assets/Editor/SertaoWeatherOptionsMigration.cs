using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class SertaoWeatherOptionsMigration
{
    private const string MenuPath = "Sertão/Interface/Instalar controles de Clima";
    private const string UndoLabel = "Instalar controles de Clima";
    private const float InitialRowY = 22f;

    [MenuItem(MenuPath, true)]
    private static bool CanInstall() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(MenuPath)]
    private static void Install()
    {
        InstallInScene("Assets/Scenes/MainMenu.unity");
        InstallInScene("Assets/Scenes/PrototypeLayout.unity");
    }

    private static void InstallInScene(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            Debug.LogWarning("Cena de Opções não encontrada: " + path);
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(path);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        bool hadUnsavedEdits = alreadyLoaded && scene.isDirty;
        if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        bool changed = false;
        int found = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (AudioSettingsUI settings in root.GetComponentsInChildren<AudioSettingsUI>(true))
        {
            found++;
            changed |= InstallRow(settings);
        }

        if (found == 0)
            Debug.LogWarning("Nenhuma tela com AudioSettingsUI em " + path);
        if (changed) EditorSceneManager.MarkSceneDirty(scene);
        if (changed && hadUnsavedEdits)
        {
            Debug.Log("Clima instalado em " + scene.name + ". Salve a cena com suas edições.");
            return;
        }
        if (changed && !EditorSceneManager.SaveScene(scene))
        {
            Debug.LogWarning("Clima preparado. Salve a cena manualmente: " + path);
            return;
        }
        if (changed) Debug.Log("Controles de Clima instalados em " + scene.name + ".");
        if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    private static bool InstallRow(AudioSettingsUI settings)
    {
        var properties = new SerializedObject(settings);
        var weatherLess = properties.FindProperty("weatherLess");
        var weatherMore = properties.FindProperty("weatherMore");
        var weatherMute = properties.FindProperty("weatherMute");
        var weatherValue = properties.FindProperty("weatherValue");
        var weatherMuteLabel = properties.FindProperty("weatherMuteLabel");
        if (weatherLess.objectReferenceValue != null && weatherMore.objectReferenceValue != null &&
            weatherMute.objectReferenceValue != null && weatherValue.objectReferenceValue != null &&
            weatherMuteLabel.objectReferenceValue != null) return false;

        var effectsLess = properties.FindProperty("effectsLess").objectReferenceValue as Button;
        var effectsMore = properties.FindProperty("effectsMore").objectReferenceValue as Button;
        var effectsMute = properties.FindProperty("effectsMute").objectReferenceValue as Button;
        var effectsValue = properties.FindProperty("effectsValue").objectReferenceValue as Text;
        if (effectsLess == null || effectsMore == null || effectsMute == null || effectsValue == null)
        {
            Debug.LogWarning("Configure os controles de Efeitos antes de instalar Clima.", settings);
            return false;
        }

        Transform rowParent = effectsLess.transform.parent;
        Text effectsLabel = rowParent.Find("Efeitos")?.GetComponent<Text>() ??
            rowParent.Find("Efeitos sonoros")?.GetComponent<Text>();
        if (effectsLabel == null)
        {
            Debug.LogWarning("Rótulo de Efeitos ausente na Hierarquia.", settings);
            return false;
        }

        FindOrCopy(effectsLabel, "Clima", "Clima");
        weatherLess.objectReferenceValue = weatherLess.objectReferenceValue ??
            FindOrCopy(effectsLess, "Clima menos");
        weatherMore.objectReferenceValue = weatherMore.objectReferenceValue ??
            FindOrCopy(effectsMore, "Clima mais");
        weatherValue.objectReferenceValue = weatherValue.objectReferenceValue ??
            FindOrCopy(effectsValue, "Valor clima", "40%");
        Button muteButton = weatherMute.objectReferenceValue as Button ??
            FindOrCopy(effectsMute, "Clima silenciar");
        weatherMute.objectReferenceValue = muteButton;
        if (weatherMuteLabel.objectReferenceValue == null)
            weatherMuteLabel.objectReferenceValue = muteButton.GetComponentInChildren<Text>(true);
        properties.ApplyModifiedProperties();
        return true;
    }

    private static T FindOrCopy<T>(T template, string name, string text = null) where T : Component
    {
        Transform parent = template.transform.parent;
        T existing = parent.Find(name)?.GetComponent<T>();
        if (existing != null) return existing;
        T copy = Object.Instantiate(template, parent, false);
        copy.gameObject.name = name;
        var rect = (RectTransform)copy.transform;
        Vector2 position = rect.anchoredPosition;
        position.y = InitialRowY;
        rect.anchoredPosition = position;
        if (copy is Button button) button.onClick = new Button.ButtonClickedEvent();
        if (copy is Text label && text != null) label.text = text;
        Undo.RegisterCreatedObjectUndo(copy.gameObject, UndoLabel);
        return copy;
    }
}
