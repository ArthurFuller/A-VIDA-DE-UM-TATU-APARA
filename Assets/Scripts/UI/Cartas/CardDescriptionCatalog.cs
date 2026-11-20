using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "Jogo/Descrições das cartas")]
public sealed class CardDescriptionCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string key;
        public string title;
        public Sprite art;
        [TextArea(4, 12)] public string text;
        public Vector2 boxSize = new Vector2(240f, 140f);
        [Header("Somente na escolha de carta")]
        public Vector2 choiceOffset;
        public Vector2 choiceBoxSize;
        [Min(0)] public int choiceFontSize;
        public Font font;
        public bool bold;
        [Min(8)] public int fontSize = 14;
        public TextAnchor alignment = TextAnchor.MiddleLeft;
        public Color textColor = Color.white;
        public Color backgroundColor = new Color(0.1f, 0.09f, 0.07f, 0.92f);
    }

    [Header("Posição compartilhada das descrições")]
    public Vector2 inspectionOffset;
    public Vector2 choiceOffset;
    public Vector2 eventOffset;
    public List<Entry> entries = new List<Entry>();
    public static event Action Changed;
    public static void NotifyChanged() => Changed?.Invoke();
    private static CardDescriptionCatalog instance;
    public static CardDescriptionCatalog Instance => instance != null ? instance :
        instance = Resources.Load<CardDescriptionCatalog>("CardDescriptions");
    public static string ActivityKey(ActivityKind kind) => "A:" + kind;
    public static string EventKey(SertaoEventKind kind) => "E:" + kind;
    public Entry Find(string key) => entries.Find(item => item.key == key);

    public static void Apply(Entry entry, RectTransform box, Text label,
        Vector2 basePosition, string displayedText, bool choice = false)
    {
        if (entry == null || box == null || label == null) return;
        box.sizeDelta = choice && entry.choiceBoxSize.x > 0f && entry.choiceBoxSize.y > 0f ?
            entry.choiceBoxSize : entry.boxSize;
        box.anchoredPosition = basePosition + (choice || Instance == null ?
            Vector2.zero : Instance.inspectionOffset);
        Image background = box.GetComponent<Image>();
        if (background != null) background.color = entry.backgroundColor;
        label.text = displayedText;
        if (entry.font != null) label.font = entry.font;
        label.fontStyle = entry.bold ? FontStyle.Bold : FontStyle.Normal;
        label.fontSize = choice && entry.choiceFontSize > 0 ?
            entry.choiceFontSize : entry.fontSize;
        label.alignment = entry.alignment;
        label.color = entry.textColor;
        label.resizeTextForBestFit = false;
        label.supportRichText = true;
    }
}
