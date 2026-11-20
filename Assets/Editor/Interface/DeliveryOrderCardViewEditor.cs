using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(DeliveryOrderCardView))]
public sealed class DeliveryOrderCardViewEditor : Editor
{
    private bool showReferences;

    private void OnEnable()
    {
        EditorApplication.delayCall += RefreshInitialPreview;
        Canvas.preWillRenderCanvases += QueuePreview;
    }

    private void OnDisable()
    {
        EditorApplication.delayCall -= RefreshInitialPreview;
        Canvas.preWillRenderCanvases -= QueuePreview;
    }

    private void RefreshInitialPreview()
    {
        if (!Application.isPlaying && target is DeliveryOrderCardView card && card != null &&
            card.gameObject.scene.IsValid()) card.RefreshEditorPreview();
    }

    private void QueuePreview()
    {
        if (!Application.isPlaying && target is DeliveryOrderCardView card && card != null)
            card.QueueEditorCentering();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DeliveryOrderCardView card = (DeliveryOrderCardView)target;
        EditorGUILayout.LabelField("Ícones visíveis no Editor", EditorStyles.boldLabel);
        DrawPreview("Primeiro item", "previewLeft", "size", "leftPosition");
        EditorGUILayout.Space(6f);
        DrawPreview("Segundo item", "previewRight", "rightSize", "rightPosition");
        EditorGUILayout.Space(6f);
        DrawPreview("Terceiro item", "previewThird", "thirdSize", "thirdPosition");

        bool layoutChanged = false;
        if (card.ItemLayout != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Distribuição dos itens", EditorStyles.boldLabel);
            var layout = new SerializedObject(card.ItemLayout);
            layout.Update();
            EditorGUILayout.PropertyField(layout.FindProperty("m_CellSize"), new GUIContent("Espaço de cada item"));
            EditorGUILayout.PropertyField(layout.FindProperty("m_Spacing"), new GUIContent("Espaçamento entre itens"));
            layoutChanged = layout.ApplyModifiedProperties();
        }

        showReferences = EditorGUILayout.Foldout(showReferences, "Referências do cartão", true);
        if (showReferences)
            DrawPropertiesExcluding(serializedObject, "m_Script", "previewLeft", "previewRight",
                "previewThird", "icons");

        bool cardChanged = serializedObject.ApplyModifiedProperties();
        if (cardChanged || layoutChanged)
            UpdatePreview(card);

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Salvar ícone da esquerda e a cena")) SavePreview(card, 0);
            if (GUILayout.Button("Salvar ícone da direita e a cena")) SavePreview(card, 1);
            if (GUILayout.Button("Salvar terceiro ícone e a cena")) SavePreview(card, 2);
        }
    }

    private void DrawPreview(string label, string selectedItem, string size, string position)
    {
        SerializedProperty selection = serializedObject.FindProperty(selectedItem);
        EditorGUILayout.PropertyField(selection, new GUIContent(label));
        SerializedProperty icons = serializedObject.FindProperty("icons");
        SerializedProperty icon = null;
        for (int i = 0; i < icons.arraySize; i++)
        {
            SerializedProperty entry = icons.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("item").enumValueIndex == selection.enumValueIndex)
            {
                icon = entry;
                break;
            }
        }
        if (icon == null)
        {
            EditorGUILayout.HelpBox("Este recurso não tem ícone configurado.", MessageType.Warning);
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(icon.FindPropertyRelative("sprite"), new GUIContent("Sprite"));
        EditorGUILayout.PropertyField(icon.FindPropertyRelative(size), new GUIContent("Tamanho neste slot"));
        EditorGUILayout.PropertyField(icon.FindPropertyRelative(position), new GUIContent("Posição neste slot"));
        EditorGUI.indentLevel--;
    }

    private static void SavePreview(DeliveryOrderCardView card, int index)
    {
        if (Application.isPlaying || card == null || !card.gameObject.scene.IsValid()) return;
        Undo.RecordObject(card, "Salvar ícone da encomenda");
        card.CaptureEditorPreview(index);
        card.RefreshEditorPreview();
        EditorUtility.SetDirty(card);
        EditorSceneManager.MarkSceneDirty(card.gameObject.scene);
        EditorSceneManager.SaveScene(card.gameObject.scene);
    }

    private static void UpdatePreview(DeliveryOrderCardView card)
    {
        if (card == null || !card.gameObject.scene.IsValid()) return;
        if (Application.isPlaying)
        {
            card.RefreshEditorPreview();
            return;
        }
        Undo.RegisterFullObjectHierarchyUndo(card.gameObject, "Atualizar prévia da encomenda");
        card.RefreshEditorPreview();
        EditorSceneManager.MarkSceneDirty(card.gameObject.scene);
        SceneView.RepaintAll();
    }
}
