using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ActiveMissionsPanel))]
public sealed class ActiveMissionsPanelEditor : Editor
{
    private bool showPanelSettings;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ActiveMissionsPanel panel = (ActiveMissionsPanel)target;

        EditorGUILayout.LabelField("Prévia no primeiro card de missão ativa", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Edite os textos e os ícones no Card de missão ativa 1. Os outros cards usam a mesma formatação.", MessageType.None);
        DrawResource("Primeiro item", "previewFirst", "firstSize", "firstPosition");
        EditorGUILayout.Space(5f);
        DrawResource("Segundo item", "previewSecond", "secondSize", "secondPosition");
        EditorGUILayout.Space(5f);
        DrawResource("Terceiro item", "previewThird", "thirdSize", "thirdPosition");

        EditorGUILayout.Space(8f);
        showPanelSettings = EditorGUILayout.Foldout(showPanelSettings, "Layout e referências da aba", true);
        if (showPanelSettings)
            DrawPropertiesExcluding(serializedObject, "m_Script", "previewFirst", "previewSecond",
                "previewThird", "iconProfiles");

        if (serializedObject.ApplyModifiedProperties()) UpdatePreview(panel);

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Salvar primeiro ícone e a cena")) SaveIcon(panel, 0);
            if (GUILayout.Button("Salvar segundo ícone e a cena")) SaveIcon(panel, 1);
            if (GUILayout.Button("Salvar terceiro ícone e a cena")) SaveIcon(panel, 2);
        }
    }

    private void DrawResource(string label, string selection, string size, string position)
    {
        SerializedProperty selectedItem = serializedObject.FindProperty(selection);
        EditorGUILayout.PropertyField(selectedItem, new GUIContent(label));
        SerializedProperty profiles = serializedObject.FindProperty("iconProfiles");
        SerializedProperty profile = null;
        for (int i = 0; i < profiles.arraySize; i++)
        {
            SerializedProperty entry = profiles.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("item").enumValueIndex == selectedItem.enumValueIndex)
            {
                profile = entry;
                break;
            }
        }
        if (profile == null)
        {
            EditorGUILayout.HelpBox("Este recurso não tem perfil visual configurado.", MessageType.Warning);
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(profile.FindPropertyRelative("sprite"), new GUIContent("Sprite"));
        EditorGUILayout.PropertyField(profile.FindPropertyRelative(size), new GUIContent("Tamanho neste slot"));
        EditorGUILayout.PropertyField(profile.FindPropertyRelative(position), new GUIContent("Posição neste slot"));
        EditorGUI.indentLevel--;
    }

    private static void UpdatePreview(ActiveMissionsPanel panel)
    {
        if (Application.isPlaying || panel == null || !panel.gameObject.scene.IsValid()) return;
        if (panel.EditorPreviewCard != null)
            Undo.RegisterFullObjectHierarchyUndo(panel.EditorPreviewCard.gameObject,
                "Atualizar prévia da missão ativa");
        panel.RefreshEditorPreview();
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        SceneView.RepaintAll();
    }

    private static void SaveIcon(ActiveMissionsPanel panel, int index)
    {
        if (Application.isPlaying || panel == null || !panel.gameObject.scene.IsValid()) return;
        Undo.RecordObject(panel, "Salvar ícone da missão ativa");
        panel.CaptureEditorIcon(index);
        if (panel.EditorPreviewCard != null)
            Undo.RegisterFullObjectHierarchyUndo(panel.EditorPreviewCard.gameObject, "Atualizar sprite da missão ativa");
        panel.RefreshMatchingEditorSprites(index);
        panel.RefreshEditorCardFormatting();
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        EditorSceneManager.SaveScene(panel.gameObject.scene);
    }
}
