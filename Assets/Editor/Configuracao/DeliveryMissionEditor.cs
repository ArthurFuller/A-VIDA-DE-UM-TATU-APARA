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
        EditorGUILayout.HelpBox("Cada missão pode pedir vários recursos. O balanceamento define se as ofertas aparecem todo dia ou nos dias de comércio. As edições ficam salvas no projeto e nas builds.", MessageType.Info);
        if (GUILayout.Button("Nova missão"))
        {
            selected = entries.arraySize;
            entries.InsertArrayElementAtIndex(selected);
            SerializedProperty created = entries.GetArrayElementAtIndex(selected);
            created.FindPropertyRelative("title").stringValue = "Nova encomenda";
            created.FindPropertyRelative("description").stringValue = "";
            created.FindPropertyRelative("coinReward").intValue = 1;
            created.FindPropertyRelative("availableFromDay").intValue = 1;
            created.FindPropertyRelative("deliveryDays").intValue = 2;
            created.FindPropertyRelative("largeOrder").boolValue = false;
            SerializedProperty requirements = created.FindPropertyRelative("requirements");
            requirements.arraySize = 0;
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
        SerializedProperty title = entry.FindPropertyRelative("title");
        EditorGUILayout.PropertyField(title, new GUIContent("Título"));
        if (string.IsNullOrWhiteSpace(title.stringValue))
            EditorGUILayout.HelpBox("Escreva um título para a missão entrar no sorteio.", MessageType.Warning);
        SerializedProperty description = entry.FindPropertyRelative("description");
        EditorGUILayout.LabelField("Descrição");
        description.stringValue = EditorGUILayout.TextArea(description.stringValue, GUILayout.MinHeight(85f));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Itens para entregar", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("0 = não exigir este recurso", EditorStyles.miniLabel);
        SerializedProperty requirements = entry.FindPropertyRelative("requirements");
        for (int itemIndex = 0; itemIndex < ItemNames.Length; itemIndex++)
        {
            int requirementIndex = -1;
            for (int i = 0; i < requirements.arraySize; i++)
                if (requirements.GetArrayElementAtIndex(i).FindPropertyRelative("item").enumValueIndex == itemIndex)
                {
                    requirementIndex = i;
                    break;
                }
            int quantity = requirementIndex >= 0 ? requirements.GetArrayElementAtIndex(requirementIndex)
                .FindPropertyRelative("quantity").intValue : 0;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(ItemNames[itemIndex], EditorStyles.wordWrappedLabel,
                GUILayout.MinWidth(160f), GUILayout.ExpandWidth(true));
            int edited = Mathf.Max(0, EditorGUILayout.IntField(quantity, GUILayout.Width(65f)));
            EditorGUILayout.EndHorizontal();
            if (edited == quantity) continue;
            if (edited == 0 && requirementIndex >= 0)
                requirements.DeleteArrayElementAtIndex(requirementIndex);
            else if (requirementIndex >= 0)
                requirements.GetArrayElementAtIndex(requirementIndex)
                    .FindPropertyRelative("quantity").intValue = edited;
            else
            {
                int index = requirements.arraySize;
                requirements.InsertArrayElementAtIndex(index);
                SerializedProperty added = requirements.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("item").enumValueIndex = itemIndex;
                added.FindPropertyRelative("quantity").intValue = edited;
            }
        }

        long totalUnits = 0;
        for (int i = 0; i < requirements.arraySize; i++)
            totalUnits += requirements.GetArrayElementAtIndex(i).FindPropertyRelative("quantity").intValue;
        if (totalUnits < 2)
            EditorGUILayout.HelpBox("O pedido precisa de pelo menos duas unidades no total para entrar no sorteio.", MessageType.Warning);
        if (requirements.arraySize == 0)
            EditorGUILayout.HelpBox("Adicione pelo menos um item. Missões vazias não são sorteadas.", MessageType.Warning);

        EditorGUILayout.Space(8f);
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("largeOrder"),
            new GUIContent("Encomenda grande", "Uma das ofertas do dia prioriza estas encomendas após a liberação."));
        SerializedProperty firstDay = entry.FindPropertyRelative("availableFromDay");
        firstDay.intValue = Mathf.Max(1, EditorGUILayout.IntField("Disponível a partir do dia", firstDay.intValue));
        SerializedProperty duration = entry.FindPropertyRelative("deliveryDays");
        duration.intValue = Mathf.Max(0, EditorGUILayout.IntField("Dias adicionais para entregar", duration.intValue));
        SerializedProperty reward = entry.FindPropertyRelative("coinReward");
        reward.intValue = Mathf.Max(0, EditorGUILayout.IntField("Moedas de recompensa", reward.intValue));
    }
}
