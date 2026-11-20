using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class ActiveMissionCoinMigration
{
    static ActiveMissionCoinMigration()
    {
        EditorApplication.update += MigrateWhenReady;
    }

    private static void MigrateWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("PrototypeLayout.unity")) return;

        ActiveMissionCard[] cards = Object.FindObjectsByType<ActiveMissionCard>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int sceneCards = 0;
        foreach (ActiveMissionCard card in cards)
            if (card.gameObject.scene == scene) sceneCards++;
        if (sceneCards != 12) return;

        Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Arte/Icones/Icon_Dinheiro.png");
        if (coinSprite == null) return;

        int changed = 0;
        foreach (ActiveMissionCard card in cards)
        {
            if (card.gameObject.scene != scene) continue;
            var fields = new SerializedObject(card);
            SerializedProperty coinField = fields.FindProperty("coinIcon");
            SerializedProperty deadlineField = fields.FindProperty("deadlineText");
            Text amount = fields.FindProperty("rewardText")?.objectReferenceValue as Text;
            if (coinField == null || deadlineField == null || amount == null) continue;
            if (coinField.objectReferenceValue != null &&
                deadlineField.objectReferenceValue != null) continue;

            GameObject coinObject = new GameObject("Icone da moeda",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(coinObject, "Adicionar moeda à missão ativa");
            coinObject.layer = card.gameObject.layer;
            coinObject.transform.SetParent(card.transform, false);
            RectTransform coinRect = (RectTransform)coinObject.transform;
            coinRect.anchorMin = coinRect.anchorMax = new Vector2(0f, 1f);
            coinRect.pivot = new Vector2(0f, 1f);
            coinRect.anchoredPosition = new Vector2(8f, -49f);
            coinRect.sizeDelta = new Vector2(20f, 20f);
            Image coinImage = coinObject.GetComponent<Image>();
            coinImage.sprite = coinSprite;
            coinImage.preserveAspect = true;
            coinImage.raycastTarget = false;

            Undo.RecordObject(amount.gameObject, "Separar moedas e prazo");
            Undo.RecordObject(amount, "Separar moedas e prazo");
            Undo.RecordObject(amount.rectTransform, "Separar moedas e prazo");
            amount.gameObject.name = "Quantidade de moedas";
            amount.text = "6";
            amount.rectTransform.anchoredPosition = new Vector2(31f, -54f);
            amount.rectTransform.sizeDelta = new Vector2(50f, 14f);

            GameObject deadlineObject = Object.Instantiate(amount.gameObject,
                card.transform, false);
            Undo.RegisterCreatedObjectUndo(deadlineObject, "Adicionar prazo à missão ativa");
            deadlineObject.name = "Prazo";
            RectTransform deadlineRect = (RectTransform)deadlineObject.transform;
            deadlineRect.anchoredPosition = new Vector2(87f, -54f);
            deadlineRect.sizeDelta = new Vector2(81f, 14f);
            Text deadline = deadlineObject.GetComponent<Text>();
            deadline.text = "Prazo: dia 3";

            coinField.objectReferenceValue = coinImage;
            deadlineField.objectReferenceValue = deadline;
            fields.ApplyModifiedProperties();
            changed++;
        }

        if (changed == 0) { EditorApplication.update -= MigrateWhenReady; return; }
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene))
        {
            Debug.Log("ActiveMissionCoinMigration: atualizados " + changed + " cards.");
            EditorApplication.update -= MigrateWhenReady;
        }
    }
}
