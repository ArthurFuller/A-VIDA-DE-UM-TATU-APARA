using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardDescriptionEditor : EditorWindow
{
    private const string AssetPath = "Assets/Resources/CardDescriptions.asset";
    private CardDescriptionCatalog catalog;
    private int selected;
    private int context;
    private int choiceSide;
    private bool showReroll = true;
    private float previewZoom = 1f;
    private Vector2 scroll;

    [MenuItem("Tools/Cartas/Editar descrições")]
    private static void Open() => GetWindow<CardDescriptionEditor>("Descrições das cartas");

    [InitializeOnLoadMethod]
    private static void EnsureOnReload() => EditorApplication.delayCall += () => { EnsureCatalog(); };

    private static CardDescriptionCatalog EnsureCatalog()
    {
        CardDescriptionCatalog existing = AssetDatabase.LoadAssetAtPath<CardDescriptionCatalog>(AssetPath);
        if (existing != null) return existing;
        Directory.CreateDirectory("Assets/Resources");
        var created = CreateInstance<CardDescriptionCatalog>();
        foreach (ActivityKind kind in Enum.GetValues(typeof(ActivityKind)))
        {
            ActivityCardDrag source = FindCard(kind);
            created.entries.Add(new CardDescriptionCatalog.Entry
            {
                key = CardDescriptionCatalog.ActivityKey(kind),
                title = source != null ? source.DisplayName : kind.ToString(),
                text = source != null ? source.Description : SceneCardText(kind),
                art = source != null ? source.GetComponent<Image>()?.sprite : null
            });
        }
        foreach (SertaoEventKind kind in Enum.GetValues(typeof(SertaoEventKind)))
        {
            if (kind == SertaoEventKind.None) continue;
            created.entries.Add(new CardDescriptionCatalog.Entry
            {
                key = CardDescriptionCatalog.EventKey(kind),
                title = EventTitle(kind),
                text = EventText(kind),
                art = EventArt(kind)
            });
        }
        AssetDatabase.CreateAsset(created, AssetPath);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static ActivityCardDrag FindCard(ActivityKind kind)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Cards" }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            ActivityCardDrag card = prefab != null ? prefab.GetComponentInChildren<ActivityCardDrag>(true) : null;
            if (card != null && card.Kind == kind) return card;
        }
        foreach (ActivityCardDrag card in Resources.FindObjectsOfTypeAll<ActivityCardDrag>())
            if (card != null && !EditorUtility.IsPersistent(card) && card.Kind == kind)
                return card;
        return null;
    }

    private static string SceneCardText(ActivityKind kind) => kind switch
    {
        ActivityKind.CollectWood => "Envie um trabalhador à floresta para coletar madeira.",
        ActivityKind.Fish => "Envie um trabalhador ao lago para pescar.",
        ActivityKind.Plow => "Prepare um terreno para o plantio.",
        ActivityKind.Harvest => "Colha uma plantação pronta.",
        ActivityKind.PrepareFood => "Use peixes ou colheitas para preparar comida.",
        _ => string.Empty
    };

    private static string EventTitle(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "Seca",
        SertaoEventKind.Pests => "Pragas",
        SertaoEventKind.Rain => "Chuva",
        SertaoEventKind.Comadre => "Comadre Fulozinha",
        SertaoEventKind.Calm => "Dia tranquilo",
        SertaoEventKind.HeadOfCuia => "Cabeça de Cuia",
        _ => "Reflorestamento"
    };

    private static string EventText(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "A seca reduz a pesca e atrasa o crescimento.",
        SertaoEventKind.Pests => "Cultura afetada: " + DayEventController.PestCropToken +
            ".\n\nA praga atrasa o crescimento das plantas sem proteção, inclusive as que estavam prontas." +
            "\n\nDuração: " + DayEventController.PestDurationToken + ".",
        SertaoEventKind.Rain => "A chuva acelera o crescimento e reduz o impacto da coleta de madeira.",
        SertaoEventKind.Comadre => "A exploração da floresta desorientou um trabalhador por um dia.",
        SertaoEventKind.Calm => "Nenhum novo evento especial começou hoje.",
        SertaoEventKind.HeadOfCuia => "O Cabeça de Cuia roubou " + DayEventController.HeadOfCuiaLossToken + ".",
        _ => "A exploração sustentável permitiu reflorestar parte da mata. Você recebe madeiras."
    };

    private static Sprite EventArt(SertaoEventKind kind)
    {
        DayEventController controller = UnityEngine.Object.FindFirstObjectByType<DayEventController>(FindObjectsInactive.Include);
        if (controller == null) return null;
        string field = kind switch
        {
            SertaoEventKind.Drought => "droughtArt",
            SertaoEventKind.Pests => "pestsArt",
            SertaoEventKind.Rain => "rainArt",
            SertaoEventKind.Comadre => "comadreArt",
            SertaoEventKind.Calm => "calmArt",
            SertaoEventKind.HeadOfCuia => "headOfCuiaArt",
            _ => "reforestationArt"
        };
        return new SerializedObject(controller).FindProperty(field).objectReferenceValue as Sprite;
    }

    private void OnEnable()
    {
        catalog = EnsureCatalog();
        Undo.undoRedoPerformed += OnUndoRedo;
        foreach (CardDescriptionCatalog.Entry entry in catalog.entries)
        {
            if (entry.art != null || !entry.key.StartsWith("A:")) continue;
            if (Enum.TryParse(entry.key.Substring(2), out ActivityKind kind))
                entry.art = FindCard(kind)?.GetComponent<Image>()?.sprite;
        }
        EditorUtility.SetDirty(catalog);
    }

    private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

    private void OnUndoRedo()
    {
        Repaint();
        if (catalog != null) EditorUtility.SetDirty(catalog);
        CardDescriptionCatalog.NotifyChanged();
    }

    private void OnGUI()
    {
        if (catalog == null) catalog = EnsureCatalog();
        if (catalog.entries.Count == 0) return;
        string[] names = catalog.entries.ConvertAll(e => e.title).ToArray();
        selected = EditorGUILayout.Popup("Carta", Mathf.Clamp(selected, 0, names.Length - 1), names);
        CardDescriptionCatalog.Entry entry = catalog.entries[selected];
        bool isEvent = entry.key.StartsWith("E:");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (!isEvent)
        {
            context = GUILayout.Toolbar(context, new[] { "Inspeção", "Escolha de carta" });
            if (context == 1)
                choiceSide = GUILayout.Toolbar(choiceSide, new[] { "Carta da esquerda", "Carta da direita" });
        }
        bool choice = !isEvent && context == 1;
        EditorGUI.BeginChangeCheck();
        string text = EditorGUILayout.TextArea(entry.text, GUILayout.MinHeight(100));
        Vector2 boxSize = choice && entry.choiceBoxSize.x > 0f && entry.choiceBoxSize.y > 0f ?
            entry.choiceBoxSize : entry.boxSize;
        Vector2 boxOffset = isEvent ? catalog.eventOffset :
            choice ? catalog.choiceOffset : catalog.inspectionOffset;
        Font font = entry.font;
        bool bold = entry.bold;
        int fontSize = choice && entry.choiceFontSize > 0 ?
            entry.choiceFontSize : entry.fontSize;
        Vector2 choiceOffset = entry.choiceOffset;
        TextAnchor alignment = entry.alignment;
        Color textColor = entry.textColor;
        Color backgroundColor = entry.backgroundColor;
        if (!isEvent)
        {
            boxSize = EditorGUILayout.Vector2Field(choice ?
                "Tamanho da caixa na escolha" : "Tamanho da caixa", boxSize);
            font = (Font)EditorGUILayout.ObjectField("Fonte", font, typeof(Font), false);
            bold = EditorGUILayout.Toggle("Negrito", bold);
            fontSize = EditorGUILayout.IntField(choice ?
                "Tamanho da fonte na escolha" : "Tamanho da fonte", fontSize);
            alignment = (TextAnchor)EditorGUILayout.EnumPopup("Alinhamento", alignment);
            textColor = EditorGUILayout.ColorField("Cor do texto", textColor);
            backgroundColor = EditorGUILayout.ColorField("Cor da caixa", backgroundColor);
        }
        if (choice)
        {
            choiceOffset = EditorGUILayout.Vector2Field("Posição desta carta (X, Y)", choiceOffset);
            boxOffset = EditorGUILayout.Vector2Field("Deslocamento espelhado (X)", boxOffset);
        }
        else
        {
            boxOffset = EditorGUILayout.Vector2Field(isEvent ?
                "Posição dos eventos (X, Y)" : "Posição da inspeção (X, Y)", boxOffset);
        }
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(catalog, "Editar descrição de " + entry.title);
            entry.text = text;
            if (isEvent) catalog.eventOffset = boxOffset;
            else if (choice) catalog.choiceOffset = boxOffset;
            else catalog.inspectionOffset = boxOffset;
            if (!isEvent)
            {
                if (choice)
                    entry.choiceBoxSize = new Vector2(Mathf.Max(80, boxSize.x),
                        Mathf.Max(50, boxSize.y));
                else
                    entry.boxSize = new Vector2(Mathf.Max(80, boxSize.x),
                        Mathf.Max(50, boxSize.y));
                entry.font = font;
                entry.bold = bold;
                if (choice) entry.choiceFontSize = Mathf.Max(8, fontSize);
                else entry.fontSize = Mathf.Max(8, fontSize);
                entry.alignment = alignment;
                entry.textColor = textColor;
                entry.backgroundColor = backgroundColor;
                if (choice)
                    entry.choiceOffset = choiceOffset;
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            CardDescriptionCatalog.NotifyChanged();
        }
        if (isEvent)
        {
            if (entry.key == CardDescriptionCatalog.EventKey(SertaoEventKind.HeadOfCuia))
                EditorGUILayout.HelpBox("Use " + DayEventController.HeadOfCuiaLossToken +
                    " para mostrar a quantidade roubada, com peixe no singular ou plural.", MessageType.Info);
            if (entry.key == CardDescriptionCatalog.EventKey(SertaoEventKind.Pests))
                EditorGUILayout.HelpBox("Use " + DayEventController.PestCropToken +
                    " para mostrar a cultura escolhida e " + DayEventController.PestDurationToken +
                    " para mostrar a duração do evento.", MessageType.Info);
            EditorGUILayout.HelpBox("Eventos usam o tamanho e a fonte da caixa da cena. O deslocamento acima vale para todas as cartas de evento.", MessageType.Info);
            if (GUILayout.Button("Selecionar caixa do evento na Hierarchy"))
            {
                EventCardView view = UnityEngine.Object.FindFirstObjectByType<EventCardView>(FindObjectsInactive.Include);
                SerializedProperty panel = view != null ?
                    new SerializedObject(view).FindProperty("descriptionGroup") : null;
                if (panel != null && panel.objectReferenceValue != null)
                {
                    Selection.activeObject = panel.objectReferenceValue;
                    EditorGUIUtility.PingObject(panel.objectReferenceValue);
                }
            }
            EditorGUILayout.EndScrollView();
            return;
        }
        GUILayout.Space(12);
        EditorGUILayout.LabelField("Prévia da tela (Canvas 900 × 506)", EditorStyles.boldLabel);
        previewZoom = EditorGUILayout.Slider("Zoom da prévia", previewZoom, 0.5f, 1.5f);
        if (choice) showReroll = EditorGUILayout.Toggle("Mostrar botões de rerrolagem", showReroll);
        DrawPreview(entry, choice);
        EditorGUILayout.EndScrollView();
    }

    private void DrawPreview(CardDescriptionCatalog.Entry entry, bool choice)
    {
        if (choice)
        {
            DrawChoicePreview(entry);
            return;
        }
        ActivityCardDrag source = Enum.TryParse(entry.key.Substring(2), out ActivityKind kind) ?
            FindCard(kind) : null;
        float width = 900f * previewZoom;
        float scale = width / 900f;
        Rect frame = GUILayoutUtility.GetRect(width, 506.25f * scale);
        EditorGUI.DrawRect(frame, new Color(0.33f, 0.42f, 0.31f));
        Vector2 size = source != null ? ((RectTransform)source.transform).rect.size :
            new Vector2(140f, 190f);
        float gap = 32f;
        float rightRoom = 450f - entry.boxSize.x - gap - 16f;
        float cardScale = Mathf.Min(900f * 0.38f / size.x, 506.25f * 0.68f / size.y);
        cardScale = Mathf.Min(cardScale, Mathf.Max(1f, rightRoom * 2f / size.x));
        cardScale = Mathf.Max(1f, cardScale);
        float cardWidth = size.x * cardScale;
        float cardHeight = size.y * cardScale;
        float cardX = 450f;
        float cardY = 253f;
        Rect cardRect = new Rect(frame.x + (cardX - cardWidth * 0.5f) * scale,
            frame.y + (cardY - cardHeight * 0.5f) * scale,
            cardWidth * scale, cardHeight * scale);
        DrawArt(source != null ? source.GetComponent<Image>()?.sprite : entry.art, cardRect);
        Vector2 sizeBox = entry.boxSize;
        if (sizeBox.x <= 0f || sizeBox.y <= 0f)
            sizeBox = new Vector2(240f, 140f);
        Vector2 offset = catalog.inspectionOffset;
        float boxX = cardX + cardWidth * 0.5f + 32f + offset.x;
        float boxY = cardY - sizeBox.y * 0.5f - offset.y;
        Rect box = new Rect(frame.x + boxX * scale, frame.y + boxY * scale,
            sizeBox.x * scale, sizeBox.y * scale);
        HandleBoxDrag(entry, box, scale, choice);
        DrawDescription(entry, box, scale);
    }

    private static void DrawDescription(CardDescriptionCatalog.Entry entry, Rect box,
        float scale, bool choice = false)
    {
        EditorGUI.DrawRect(box, entry.backgroundColor);
        int fontSize = entry.fontSize;
        var style = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true, richText = true,
            alignment = entry.alignment,
            font = entry.font ?? EditorStyles.label.font,
            fontStyle = entry.bold ? FontStyle.Bold : FontStyle.Normal,
            fontSize = Mathf.Max(8, Mathf.RoundToInt(
                (choice && entry.choiceFontSize > 0 ? entry.choiceFontSize : fontSize) * scale)),
            normal = { textColor = entry.textColor }
        };
        float paddingX = (choice ? 8f : 16f) * scale;
        float paddingY = (choice ? 6f : 14f) * scale;
        Rect textRect = new Rect(box.x + paddingX, box.y + paddingY,
            box.width - paddingX * 2f, box.height - paddingY * 2f);
        GUI.Label(textRect, entry.text, style);
    }

    private void DrawChoicePreview(CardDescriptionCatalog.Entry selectedEntry)
    {
        const float canvasWidth = 900f, canvasHeight = 506.25f;
        float scale = previewZoom;
        Rect frame = GUILayoutUtility.GetRect(canvasWidth * scale, canvasHeight * scale);
        EditorGUI.DrawRect(frame, new Color(0.33f, 0.42f, 0.31f));

        CardRewardChoice view = UnityEngine.Object.FindFirstObjectByType<CardRewardChoice>(
            FindObjectsInactive.Include);
        SerializedObject serialized = view != null ? new SerializedObject(view) : null;
        RectTransform root = view != null ?
            view.GetComponentInParent<Canvas>()?.rootCanvas.transform as RectTransform : null;
        RectTransform[] cards = new RectTransform[2];
        Sprite[] previewArt = new Sprite[2];
        Rect[] cardRects = new Rect[2];
        if (serialized != null && root != null)
        {
            CanvasGroup group = serialized.FindProperty("group")?.objectReferenceValue as CanvasGroup;
            DrawSceneImage(group != null ? group.GetComponent<Image>() : null, frame);
            SerializedProperty cardList = serialized.FindProperty("choiceCards");
            SerializedProperty artList = serialized.FindProperty("choiceArt");
            SerializedProperty buttonList = serialized.FindProperty("rerollButtons");
            for (int i = 0; i < 2; i++)
                cards[i] = cardList != null && i < cardList.arraySize ?
                    cardList.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform : null;
            RectTransform panel = cards[0] != null ? cards[0].parent as RectTransform : null;
            if (panel != null)
            {
                DrawSceneImage(panel.GetComponent<Image>(), CanvasRect(root, panel, frame));
                foreach (Text title in panel.GetComponentsInChildren<Text>(true))
                    if (title.transform.parent == panel)
                        DrawSceneText(title, CanvasRect(root, title.rectTransform, frame), scale);
            }
            for (int i = 0; i < 2; i++)
            {
                if (cards[i] == null) continue;
                Rect cardRect = CanvasRect(root, cards[i], frame);
                cardRects[i] = cardRect;
                Image artImage = artList != null && i < artList.arraySize ?
                    artList.GetArrayElementAtIndex(i).objectReferenceValue as Image : null;
                previewArt[i] = i == choiceSide ? selectedEntry.art :
                    artImage != null ? artImage.sprite : null;
                DrawArt(previewArt[i], cardRect);
                if (showReroll && buttonList != null && i < buttonList.arraySize)
                {
                    Button button = buttonList.GetArrayElementAtIndex(i)
                        .objectReferenceValue as Button;
                    if (button != null)
                    {
                        DrawSceneImage(button.image,
                            CanvasRect(root, (RectTransform)button.transform, frame));
                        Text label = button.GetComponentInChildren<Text>(true);
                        if (label != null)
                            DrawSceneText(label, CanvasRect(root, label.rectTransform, frame), scale);
                    }
                }
            }
        }
        else
        {
            EditorGUI.DrawRect(new Rect(frame.x + 190f * scale, frame.y + 83f * scale,
                520f * scale, 340f * scale), new Color(0.95f, 0.94f, 0.87f));
            cards[0] = cards[1] = null;
            cardRects[0] = new Rect(frame.x + 255f * scale, frame.y + 108f * scale,
                170f * scale, 225f * scale);
            cardRects[1] = new Rect(frame.x + 475f * scale, frame.y + 108f * scale,
                170f * scale, 225f * scale);
            DrawArt(selectedEntry.art, cardRects[choiceSide]);
        }

        int hoveredSide = cardRects[0].Contains(Event.current.mousePosition) ? 0 :
            cardRects[1].Contains(Event.current.mousePosition) ? 1 : choiceSide;
        CardDescriptionCatalog.Entry previewEntry = selectedEntry;
        if (hoveredSide != choiceSide && previewArt[hoveredSide] != null)
        {
            CardDescriptionCatalog.Entry match = catalog.entries.Find(candidate =>
                candidate.key.StartsWith("A:") && candidate.art == previewArt[hoveredSide]);
            if (match != null) previewEntry = match;
        }
        Rect box = ChoiceDescriptionRect(previewEntry, cardRects[hoveredSide], frame,
            scale, hoveredSide);
        DrawDescription(previewEntry, box, scale, true);
        if (hoveredSide == choiceSide)
            HandleBoxDrag(selectedEntry, box, scale, true);
        if (Event.current.type == EventType.MouseMove) Repaint();
    }

    private Rect ChoiceDescriptionRect(CardDescriptionCatalog.Entry entry, Rect card,
        Rect frame, float scale, int side)
    {
        Vector2 size = entry.choiceBoxSize.x > 0f && entry.choiceBoxSize.y > 0f ?
            entry.choiceBoxSize : entry.boxSize;
        if (size.x <= 0f || size.y <= 0f) size = new Vector2(240f, 140f);
        float x = side == 0 ? card.xMin - 10f * scale - size.x * scale :
            card.xMax + 10f * scale;
        float y = card.center.y - size.y * scale * 0.5f;
        x = Mathf.Clamp(x, frame.x + 20f * scale,
            frame.xMax - (size.x + 20f) * scale);
        y = Mathf.Clamp(y, frame.y + 20f * scale,
            frame.yMax - (size.y + 20f) * scale);
        float sharedX = side == 0 ? catalog.choiceOffset.x : -catalog.choiceOffset.x;
        x = Mathf.Clamp(x + sharedX * scale, frame.x + 20f * scale,
            frame.xMax - (size.x + 20f) * scale);
        y = Mathf.Clamp(y - catalog.choiceOffset.y * scale, frame.y + 20f * scale,
            frame.yMax - (size.y + 20f) * scale);
        x += entry.choiceOffset.x * scale;
        y -= entry.choiceOffset.y * scale;
        return new Rect(x, y, size.x * scale, size.y * scale);
    }

    private static Rect CanvasRect(RectTransform root, RectTransform element, Rect frame)
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, element);
        float xScale = frame.width / Mathf.Max(1f, root.rect.width);
        float yScale = frame.height / Mathf.Max(1f, root.rect.height);
        return new Rect(frame.x + (bounds.min.x - root.rect.xMin) * xScale,
            frame.y + (root.rect.yMax - bounds.max.y) * yScale,
            bounds.size.x * xScale, bounds.size.y * yScale);
    }

    private static void DrawSceneImage(Image image, Rect rect)
    {
        if (image == null) return;
        Color previous = GUI.color;
        GUI.color = image.color;
        if (image.sprite != null)
        {
            if (image.preserveAspect) DrawArt(image.sprite, rect);
            else DrawArtFill(image.sprite, rect);
        }
        else EditorGUI.DrawRect(rect, image.color);
        GUI.color = previous;
    }

    private static void DrawArtFill(Sprite sprite, Rect rect)
    {
        if (sprite == null) return;
        Texture2D texture = sprite.texture;
        Rect source = sprite.rect;
        GUI.DrawTextureWithTexCoords(rect, texture,
            new Rect(source.x / texture.width, source.y / texture.height,
                source.width / texture.width, source.height / texture.height));
    }

    private static void DrawSceneText(Text text, Rect rect, float scale)
    {
        if (text == null || string.IsNullOrEmpty(text.text)) return;
        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            font = text.font,
            fontSize = Mathf.Max(8, Mathf.RoundToInt(text.fontSize * scale)),
            fontStyle = text.fontStyle,
            alignment = text.alignment,
            wordWrap = true,
            normal = { textColor = text.color }
        };
        GUI.Label(rect, text.text, style);
    }

    private void HandleBoxDrag(CardDescriptionCatalog.Entry entry, Rect box,
        float scale, bool choice)
    {
        int control = GUIUtility.GetControlID(FocusType.Passive, box);
        Event current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 &&
            box.Contains(current.mousePosition))
        {
            GUIUtility.hotControl = control;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && GUIUtility.hotControl == control)
        {
            Undo.RecordObject(catalog, "Mover descrição de " + entry.title);
            Vector2 movement = new Vector2(current.delta.x, -current.delta.y) / scale;
            if (choice) entry.choiceOffset += movement;
            else catalog.inspectionOffset += movement;
            EditorUtility.SetDirty(catalog);
            CardDescriptionCatalog.NotifyChanged();
            Repaint();
            current.Use();
        }
        else if (current.type == EventType.MouseUp && GUIUtility.hotControl == control)
        {
            GUIUtility.hotControl = 0;
            AssetDatabase.SaveAssets();
            current.Use();
        }
    }

    private static void DrawArt(Sprite sprite, Rect rect)
    {
        if (sprite == null) return;
        Texture2D texture = sprite.texture;
        Rect source = sprite.rect;
        float aspect = source.width / source.height;
        if (rect.width / rect.height > aspect)
        {
            float width = rect.height * aspect;
            rect.x += (rect.width - width) * 0.5f;
            rect.width = width;
        }
        else
        {
            float height = rect.width / aspect;
            rect.y += (rect.height - height) * 0.5f;
            rect.height = height;
        }
        GUI.DrawTextureWithTexCoords(rect, texture,
            new Rect(source.x / texture.width, source.y / texture.height,
                source.width / texture.width, source.height / texture.height));
    }
}
