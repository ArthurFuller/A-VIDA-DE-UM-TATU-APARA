using UnityEditor;
using UnityEngine;

public sealed class DeliveryMissionEditor : EditorWindow
{
    private const string AssetPath = "Assets/Resources/DeliveryMissions.asset";
    private static readonly string[] ItemNames = { "Madeira", "Peixe", "Milho", "Macaxeira",
        "Comida pronta", "Sementes de milho", "Sementes de macaxeira", "Jerimum",
        "Sementes de jerimum", "Leite", "Ovos" };

    private DeliveryMissionCatalog catalog;
    private int selected;
    private Vector2 scroll;

    [MenuItem("Tools/Encomendas/Editar missões")]
    private static void Open() => GetWindow<DeliveryMissionEditor>("Encomendas");

    private void OnEnable()
    {
        minSize = new Vector2(400f, 360f);
        catalog = AssetDatabase.LoadAssetAtPath<DeliveryMissionCatalog>(AssetPath);
        if (catalog == null)
        {
            catalog = CreateInstance<DeliveryMissionCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
            AssetDatabase.SaveAssets();
        }
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

    private void OnGUI()
    {
        if (catalog == null) return;
        var data = new SerializedObject(catalog);
        data.Update();
        SerializedProperty entries = data.FindProperty("entries");

        EditorGUILayout.LabelField("Missões de encomenda", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Cada missão pode pedir vários recursos. O jogo sorteia estas missões nos dias de mercearia. As edições ficam salvas no projeto e nas builds.", MessageType.Info);
        if (GUILayout.Button("Nova missão"))
        {
            selected = entries.arraySize;
            entries.InsertArrayElementAtIndex(selected);
            SerializedProperty created = entries.GetArrayElementAtIndex(selected);
            created.FindPropertyRelative("title").stringValue = "Nova encomenda";
            created.FindPropertyRelative("description").stringValue = "";
            created.FindPropertyRelative("coinReward").intValue = 1;
            SerializedProperty requirements = created.FindPropertyRelative("requirements");
            requirements.arraySize = 1;
            requirements.GetArrayElementAtIndex(0).FindPropertyRelative("item").enumValueIndex = 0;
            requirements.GetArrayElementAtIndex(0).FindPropertyRelative("quantity").intValue = 1;
        }

        if (entries.arraySize > 0)
        {
            selected = Mathf.Clamp(selected, 0, entries.arraySize - 1);
            var titles = new string[entries.arraySize];
            for (int i = 0; i < titles.Length; i++)
                titles[i] = entries.GetArrayElementAtIndex(i).FindPropertyRelative("title").stringValue;
            selected = EditorGUILayout.Popup("Missão", selected, titles);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawMission(entries.GetArrayElementAtIndex(selected));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Excluir esta missão"))
            {
                entries.DeleteArrayElementAtIndex(selected);
                selected = Mathf.Clamp(selected, 0, entries.arraySize - 1);
            }
        }
        else EditorGUILayout.HelpBox("Crie uma missão para começar.", MessageType.None);

        if (data.ApplyModifiedProperties()) AssetDatabase.SaveAssets();
    }

    private static void DrawMission(SerializedProperty entry)
    {
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("title"), new GUIContent("Título"));
        SerializedProperty description = entry.FindPropertyRelative("description");
        EditorGUILayout.LabelField("Descrição");
        description.stringValue = EditorGUILayout.TextArea(description.stringValue, GUILayout.MinHeight(85f));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Itens para entregar", EditorStyles.boldLabel);
        SerializedProperty requirements = entry.FindPropertyRelative("requirements");
        for (int i = 0; i < requirements.arraySize; i++)
        {
            SerializedProperty requirement = requirements.GetArrayElementAtIndex(i);
            SerializedProperty item = requirement.FindPropertyRelative("item");
            SerializedProperty quantity = requirement.FindPropertyRelative("quantity");
            EditorGUILayout.BeginHorizontal();
            item.enumValueIndex = EditorGUILayout.Popup(item.enumValueIndex, ItemNames, GUILayout.MinWidth(165f));
            quantity.intValue = Mathf.Max(1, EditorGUILayout.IntField(quantity.intValue, GUILayout.Width(60f)));
            if (GUILayout.Button("×", GUILayout.Width(28f)))
            {
                requirements.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Adicionar item"))
        {
            int index = requirements.arraySize;
            requirements.InsertArrayElementAtIndex(index);
            SerializedProperty added = requirements.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("item").enumValueIndex = 0;
            added.FindPropertyRelative("quantity").intValue = 1;
        }
        if (requirements.arraySize == 0)
            EditorGUILayout.HelpBox("Adicione pelo menos um item. Missões vazias não são sorteadas.", MessageType.Warning);

        EditorGUILayout.Space(8f);
        SerializedProperty reward = entry.FindPropertyRelative("coinReward");
        reward.intValue = Mathf.Max(0, EditorGUILayout.IntField("Moedas de recompensa", reward.intValue));
    }
}
