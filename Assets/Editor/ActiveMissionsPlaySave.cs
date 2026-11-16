// Temporário: remova este arquivo e o seu .meta para desligar o salvamento em Play Mode.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
internal static class ActiveMissionsPlaySave
{
    [Serializable]
    private sealed class State
    {
        public string scenePath;
        public List<Setting> settings = new();
    }

    [Serializable]
    private sealed class Setting
    {
        public string target;
        public string path;
        public string value;
    }

    private const string BaselineKey = "Sertao.MissionFormatting.Baseline";
    private const string PendingKey = "Sertao.MissionFormatting.Pending";
    private static readonly CultureInfo NumberFormat = CultureInfo.InvariantCulture;

    private static readonly string[] PanelFields =
    {
        "firstCardTopPadding", "cardSpacing", "cardHeight", "listBottomPadding",
        "animationDuration", "hiddenOffset", "cardDropDuration", "cardWaveInterval", "cardDropDistance",
        "previewFirst", "previewSecond", "previewThird", "iconProfiles"
    };
    private static readonly string[] OrderFields = { "previewLeft", "previewRight", "previewThird", "icons" };
    private static readonly string[] GridFields = { "m_CellSize", "m_Spacing", "m_Padding", "m_ChildAlignment" };
    private static readonly string[] TextFields = { "m_FontData", "m_Color", "m_Material" };
    private static readonly string[] ImageFields =
    {
        "m_Color", "m_Material", "m_Type", "m_PreserveAspect", "m_FillCenter", "m_FillMethod",
        "m_FillAmount", "m_FillClockwise", "m_FillOrigin", "m_UseSpriteMesh", "m_PixelsPerUnitMultiplier"
    };
    private static readonly string[] RectFields =
    {
        "m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_SizeDelta", "m_AnchoredPosition",
        "m_LocalScale", "m_LocalRotation"
    };
    private static readonly string[] ProfileRectFields =
    {
        "m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_LocalScale", "m_LocalRotation"
    };
    private static readonly string[] ActiveLabels =
    {
        "description", "quantityText", "secondQuantityText", "thirdQuantityText", "rewardText", "deadlineText", "deliverLabel"
    };
    private static readonly string[] OrderLabels = { "title", "description", "deadline", "rewardAmount", "community" };

    static ActiveMissionsPlaySave()
    {
        EditorApplication.playModeStateChanged += PlayModeChanged;
        Undo.postprocessModifications += FormattingEdited;
        Undo.undoRedoPerformed += RefreshAfterUndoRedo;
        EditorSceneManager.sceneOpened += (_, _) => ApplyPending();
        EditorApplication.delayCall += ApplyPending;
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                ApplyPending();
                Scene scene = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.path)) return;
                var baseline = new State { scenePath = scene.path };
                Capture(baseline);
                SessionState.SetString(BaselineKey, JsonUtility.ToJson(baseline));
                break;
            case PlayModeStateChange.ExitingPlayMode:
                Undo.FlushUndoRecordObjects();
                State original = Read(BaselineKey);
                if (original == null) return;
                var current = new State { scenePath = original.scenePath };
                Capture(current);
                var oldValues = new Dictionary<(string, string), string>();
                foreach (Setting setting in original.settings)
                    oldValues[(setting.target, setting.path)] = setting.value;
                current.settings.RemoveAll(setting =>
                    oldValues.TryGetValue((setting.target, setting.path), out string old) && setting.value == old);
                if (current.settings.Count != 0)
                    SessionState.SetString(PendingKey, JsonUtility.ToJson(current));
                SessionState.EraseString(BaselineKey);
                break;
            case PlayModeStateChange.EnteredEditMode:
                EditorApplication.delayCall += ApplyPending;
                break;
        }
    }

    private static void Capture(State state)
    {
        foreach (ActiveMissionsPanel panel in ViewsInScene<ActiveMissionsPanel>(state.scenePath))
        {
            CaptureFields(state, panel, PanelFields);
            ActiveMissionCard model = panel.EditorPreviewCard;
            if (model == null) continue;
            var data = new SerializedObject(model);
            foreach (string path in ActiveLabels)
            {
                var text = Reference<Text>(data, path);
                float offset = text != null && model.UsesExtraRowOffset(text.rectTransform)
                    ? model.EditorExtraRowOffset : 0f;
                CaptureGraphic(state, text, new Vector2(0f, offset));
            }
            CaptureGraphic(state, Reference<Image>(data, "coinIcon"), new Vector2(0f, model.EditorExtraRowOffset));
            var button = Reference<Button>(data, "deliverButton");
            if (button != null)
            {
                CaptureGraphic(state, button.targetGraphic);
                CaptureFields(state, button, new[] { "m_Colors" });
            }
            CaptureFields(state, model.GetComponent<Image>(), ImageFields);
            CaptureFields(state, model.GetComponent<Image>(), new[] { "m_Sprite" });
            CaptureFields(state, model.transform, new[] { "m_SizeDelta.x" });
            for (int i = 0; i < 3; i++) CaptureGraphic(state, model.IconAt(i), profileDriven: true);
        }

        foreach (DeliveryOrderCardView card in ViewsInScene<DeliveryOrderCardView>(state.scenePath))
        {
            CaptureFields(state, card, OrderFields);
            CaptureFields(state, card.ItemLayout, GridFields);
            var data = new SerializedObject(card);
            foreach (string path in OrderLabels) CaptureGraphic(state, Reference<Text>(data, path));
            CaptureGraphic(state, Reference<Image>(data, "rewardIcon"));
            var itemsRoot = Reference<RectTransform>(data, "itemsRoot");
            CaptureFields(state, itemsRoot, RectFields);
            if (itemsRoot == null) continue;
            for (int i = 0; i < Mathf.Min(3, itemsRoot.childCount); i++)
            {
                Transform row = itemsRoot.GetChild(i);
                Text quantity = row.GetComponentInChildren<Text>(true);
                if (quantity != null)
                    CaptureGraphic(state, quantity, card.EditorItemOffset(quantity.rectTransform));
                CaptureGraphic(state, row.GetComponentInChildren<Image>(true), profileDriven: true);
            }
        }
    }

    private static void CaptureGraphic(State state, Graphic graphic, Vector2 offset = default,
        bool profileDriven = false)
    {
        if (graphic == null) return;
        CaptureFields(state, graphic.rectTransform, profileDriven ? ProfileRectFields : RectFields, offset);
        CaptureFields(state, graphic, graphic is Text ? TextFields : ImageFields);
        if (graphic is Image && !profileDriven) CaptureFields(state, graphic, new[] { "m_Sprite" });
    }

    private static void CaptureFields(State state, Object target, string[] fields, Vector2 offset = default)
    {
        if (target == null) return;
        var source = new SerializedObject(target);
        string id = SceneKey((Component)target);
        foreach (string field in fields)
        {
            SerializedProperty property = source.FindProperty(field);
            if (property == null) continue;
            if (field == "m_AnchoredPosition")
                CaptureVector2(state, id, field, property.vector2Value - offset);
            else
                CaptureProperty(state, id, property);
        }
    }

    private static void CaptureProperty(State state, string target, SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Float:
                AddFloat(state, target, property.propertyPath, property.floatValue);
                return;
            case SerializedPropertyType.Vector2:
                CaptureVector2(state, target, property.propertyPath, property.vector2Value);
                return;
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.ArraySize:
                Add(state, target, property.propertyPath, property.intValue.ToString(NumberFormat));
                return;
            case SerializedPropertyType.Boolean:
                Add(state, target, property.propertyPath, property.boolValue ? "1" : "0");
                return;
            case SerializedPropertyType.ObjectReference:
                Object value = property.objectReferenceValue;
                if (value == null || EditorUtility.IsPersistent(value))
                    Add(state, target, property.propertyPath, value == null ? "" : Id(value));
                return;
            case SerializedPropertyType.Generic:
            case SerializedPropertyType.Color:
            case SerializedPropertyType.Vector3:
                break;
            case SerializedPropertyType.Quaternion:
                Quaternion rotation = property.quaternionValue;
                Add(state, target, property.propertyPath,
                    JsonUtility.ToJson(new Rotation { value = rotation }));
                return;
            default:
                return;
        }

        SerializedProperty end = property.GetEndProperty(true);
        SerializedProperty child = property.Copy();
        if (!child.Next(true)) return;
        while (!SerializedProperty.EqualContents(child, end))
        {
            CaptureProperty(state, target, child);
            if (!child.Next(false)) break;
        }
    }

    [Serializable]
    private struct Rotation { public Quaternion value; }

    private static void CaptureVector2(State state, string target, string path, Vector2 value)
    {
        AddFloat(state, target, path + ".x", value.x);
        AddFloat(state, target, path + ".y", value.y);
    }

    private static void AddFloat(State state, string target, string path, float value) =>
        Add(state, target, path, Math.Round(value, 4).ToString("R", NumberFormat));

    private static void Add(State state, string target, string path, string value) =>
        state.settings.Add(new Setting { target = target, path = path, value = value });

    private static UndoPropertyModification[] FormattingEdited(UndoPropertyModification[] changes)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
            return changes;

        var active = new HashSet<ActiveMissionsPanel>();
        var orders = new HashSet<DeliveryOrderCardView>();
        foreach (UndoPropertyModification change in changes)
        {
            PropertyModification current = change.currentValue;
            if (current?.target is not Component component) continue;
            string path = current.propertyPath;
            if (component is RectTransform &&
                !Array.Exists(RectFields, field => path == field ||
                    path.StartsWith(field + ".", StringComparison.Ordinal))) continue;
            if (component is Button && !path.StartsWith("m_Colors.", StringComparison.Ordinal)) continue;
            if (component is not RectTransform && component is not Graphic && component is not Button) continue;
            if (component is Graphic && path != "m_Sprite" &&
                !Array.Exists(component is Text ? TextFields : ImageFields, field =>
                    path == field || path.StartsWith(field + ".", StringComparison.Ordinal))) continue;

            ActiveMissionCard model = component.GetComponentInParent<ActiveMissionCard>(true);
            if (model != null)
            {
                ActiveMissionsPanel panel = model.GetComponentInParent<ActiveMissionsPanel>(true);
                if (panel == null || panel.EditorPreviewCard != model) continue;
                for (int i = 0; i < 3; i++)
                    if (IsProfileEdit(component, model.IconAt(i), path))
                    {
                        panel.CaptureEditorIcon(i);
                        if (!Application.isPlaying && path == "m_Sprite")
                            panel.RefreshMatchingEditorSprites(i);
                    }
                active.Add(panel);
                continue;
            }
            DeliveryOrderCardView card = component.GetComponentInParent<DeliveryOrderCardView>(true);
            if (card == null) continue;
            for (int i = 0; i < 3; i++)
                if (IsProfileEdit(component, card.EditorIconAt(i), path))
                    card.CaptureEditorPreview(i);
            orders.Add(card);
        }
        foreach (ActiveMissionsPanel panel in active)
        {
            EditorUtility.SetDirty(panel);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            EditorApplication.delayCall += () => { if (panel != null) panel.RefreshEditorCardFormatting(); };
        }
        foreach (DeliveryOrderCardView card in orders)
        {
            EditorUtility.SetDirty(card);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(card.gameObject.scene);
            EditorApplication.delayCall += () => { if (card != null) card.RefreshEditorPreview(); };
        }
        return changes;
    }

    private static bool IsProfileEdit(Component changed, Image icon, string path) =>
        icon != null && ((changed == icon && path == "m_Sprite") ||
            (changed == icon.rectTransform && (path.StartsWith("m_SizeDelta", StringComparison.Ordinal) ||
                path.StartsWith("m_AnchoredPosition", StringComparison.Ordinal))));

    private static void RefreshAfterUndoRedo()
    {
        string scenePath = SceneManager.GetActiveScene().path;
        foreach (ActiveMissionsPanel panel in ViewsInScene<ActiveMissionsPanel>(scenePath))
        {
            if (Application.isPlaying) panel.RefreshEditorCardFormatting();
            else panel.RefreshEditorPreview();
        }
        foreach (DeliveryOrderCardView card in ViewsInScene<DeliveryOrderCardView>(scenePath))
            card.RefreshEditorPreview();
    }

    private static void ApplyPending()
    {
        if (Application.isPlaying) return;
        State saved = Read(PendingKey);
        if (saved == null) return;
        Scene scene = SceneManager.GetSceneByPath(saved.scenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;

        var wanted = new HashSet<string>();
        foreach (Setting setting in saved.settings) wanted.Add(setting.target);
        var targets = new Dictionary<string, Component>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component is RectTransform || component is Graphic || component is ActiveMissionsPanel ||
                    component is DeliveryOrderCardView || component is GridLayoutGroup || component is Button)
                {
                    string key = SceneKey(component);
                    if (wanted.Contains(key)) targets[key] = component;
                }
            }
        }

        string currentId = null;
        SerializedObject destination = null;
        foreach (Setting setting in saved.settings)
        {
            if (setting.target != currentId)
            {
                destination?.ApplyModifiedPropertiesWithoutUndo();
                currentId = setting.target;
                destination = targets.TryGetValue(currentId, out Component target)
                    ? new SerializedObject(target) : null;
            }

            SerializedProperty property = destination?.FindProperty(setting.path);
            if (property == null) continue;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = float.Parse(setting.value, NumberFormat);
                    if (setting.path == "m_AnchoredPosition.y" && destination.targetObject is RectTransform rect)
                    {
                        ActiveMissionCard card = rect.GetComponentInParent<ActiveMissionCard>(true);
                        if (card != null && card.UsesExtraRowOffset(rect))
                            property.floatValue += card.EditorExtraRowOffset;
                    }
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.ArraySize:
                    property.intValue = int.Parse(setting.value, NumberFormat);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = setting.value == "1";
                    break;
                case SerializedPropertyType.ObjectReference:
                    Object value = Resolve(setting.value);
                    if (value != null || setting.value.Length == 0) property.objectReferenceValue = value;
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = JsonUtility.FromJson<Rotation>(setting.value).value;
                    break;
            }
        }
        destination?.ApplyModifiedPropertiesWithoutUndo();

        foreach (ActiveMissionsPanel panel in ViewsInScene<ActiveMissionsPanel>(scene.path))
            panel.RefreshEditorLayoutAfterPlayMode();
        foreach (DeliveryOrderCardView card in ViewsInScene<DeliveryOrderCardView>(scene.path))
            card.RefreshEditorPreview();

        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(PendingKey);
    }

    private static State Read(string key)
    {
        string json = SessionState.GetString(key, "");
        return json.Length == 0 ? null : JsonUtility.FromJson<State>(json);
    }

    private static T Reference<T>(SerializedObject source, string path) where T : Object =>
        source.FindProperty(path)?.objectReferenceValue as T;

    private static IEnumerable<T> ViewsInScene<T>(string scenePath) where T : Component
    {
        foreach (T view in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (view.gameObject.scene.path == scenePath) yield return view;
    }

    private static string SceneKey(Component target)
    {
        // Game UI can reorder siblings at runtime. Only siblings with the same name
        // need an occurrence index to identify authored objects such as both item icons.
        string path = target.GetType().FullName;
        for (Transform node = target.transform; node != null; node = node.parent)
        {
            string name = node.name;
            int occurrence = 0;
            if (node.parent != null)
                for (int i = 0; i < node.GetSiblingIndex(); i++)
                    if (node.parent.GetChild(i).name == name) occurrence++;
            path = name.Length + ":" + name + ":" + occurrence + "/" + path;
        }
        return path;
    }

    private static string Id(Object target) => GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();

    private static Object Resolve(string id) => GlobalObjectId.TryParse(id, out GlobalObjectId parsed)
        ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) : null;
}
