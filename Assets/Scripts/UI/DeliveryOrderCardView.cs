using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeliveryOrderCardView : MonoBehaviour, ICanvasElement
{
    [System.Serializable]
    private sealed class ItemIcon
    {
        public SaleItem item;
        public Sprite sprite;
        [Tooltip("Largura e altura deste recurso à esquerda.")]
        public Vector2 size = new(16f, 16f);
        public Vector2 leftPosition = new(1f, -1f);
        [Tooltip("Largura e altura deste recurso à direita.")]
        public Vector2 rightSize = new(16f, 16f);
        public Vector2 rightPosition = new(1f, -1f);
        public Vector2 thirdSize;
        public Vector2 thirdPosition;
    }

    [SerializeField] private Text title;
    [SerializeField] private Text description;
    [SerializeField] private Text deadline;
    [SerializeField] private Text rewardAmount;
    [SerializeField] private Text community;
    [SerializeField] private RectTransform itemsRoot;
    [SerializeField] private RectTransform itemTemplate;
    [SerializeField] private GridLayoutGroup itemLayout;
    [SerializeField] private Image rewardIcon;
    [Header("Prévia dos itens no Editor")]
    [SerializeField] private SaleItem previewLeft = SaleItem.Fish;
    [SerializeField] private SaleItem previewRight = SaleItem.Wood;
    [SerializeField] private SaleItem previewThird = SaleItem.Corn;
    [Header("Ícones por recurso")]
    [SerializeField] private List<ItemIcon> icons = new();

    private readonly List<RectTransform> items = new();
    private DeliveryMission shownMission;
    private const int MaxCenteredItems = 3;
    private const float QuantityGap = 5f;
    private const float MinimumItemGap = 8f;
    private readonly float[] centeredRowX = new float[MaxCenteredItems];
    private readonly float[] itemCenters = new float[MaxCenteredItems];
    private readonly float[] itemWidths = new float[MaxCenteredItems];
    private readonly float[] quantityOffsetsX = new float[MaxCenteredItems];
    private int visibleItemCount;
    private bool centeringDirty = true;
    private readonly float[] iconOffsetsY = new float[MaxCenteredItems];
    public GridLayoutGroup ItemLayout => itemLayout;

    private void Awake()
    {
        CacheItems();
        if (shownMission == null && items.Count > 2)
        {
            items[2].gameObject.SetActive(false);
            if (itemLayout != null) itemLayout.constraintCount = 2;
        }
    }

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += QueueCentering;
        QueueCentering();
    }

    private void OnDisable() => Canvas.preWillRenderCanvases -= QueueCentering;

    private void OnRectTransformDimensionsChange() => centeringDirty = true;

    private void CacheItems()
    {
        if (itemsRoot == null || items.Count != 0) return;
        for (int i = 0; i < itemsRoot.childCount; i++)
            if (itemsRoot.GetChild(i) is RectTransform row) items.Add(row);
    }

    public void Show(DeliveryMission mission, string communityText)
    {
        CacheItems();
        RestoreIconPositions();
        RestoreQuantityPositions();
        shownMission = mission;
        if (title != null) title.text = mission != null ? mission.title : "Nenhum pedido disponível";
        if (description != null)
            description.text = mission != null ? mission.description : "Novas ofertas chegam com a barraca.";
        if (deadline != null)
            deadline.text = mission != null ? "Prazo: dia " + mission.dueDay : "";
        if (rewardAmount != null) rewardAmount.text = mission != null ? mission.coinReward.ToString() : "";
        if (rewardIcon != null) rewardIcon.enabled = mission != null && rewardIcon.sprite != null;
        if (community != null) community.text = communityText;

        int count = mission?.requirements?.Count ?? 0;
        visibleItemCount = count;
        centeringDirty = true;
        if (itemsRoot == null || itemTemplate == null) return;
        if (itemLayout != null) itemLayout.constraintCount = Mathf.Clamp(count, 1, 4);
        while (items.Count < count)
            items.Add(Instantiate(itemTemplate, itemsRoot));
        for (int i = 0; i < items.Count; i++)
        {
            RectTransform row = items[i];
            row.gameObject.SetActive(i < count);
            if (i >= count) continue;
            DeliveryRequirement requirement = mission.requirements[i];
            ApplyIcon(row, requirement.item, i);
            Text quantity = row.GetComponentInChildren<Text>(true);
            if (quantity != null) quantity.text = requirement.quantity.ToString();
        }
        QueueCentering();
    }

    private void RestoreIconPositions()
    {
        for (int i = 0; i < items.Count && i < MaxCenteredItems; i++)
        {
            float offset = iconOffsetsY[i];
            if (offset == 0f) continue;
            Image icon = items[i].GetComponentInChildren<Image>(true);
            if (icon != null) icon.rectTransform.anchoredPosition -= Vector2.up * offset;
            iconOffsetsY[i] = 0f;
        }
    }

    private void RestoreQuantityPositions()
    {
        for (int i = 0; i < items.Count && i < MaxCenteredItems; i++)
        {
            float offset = quantityOffsetsX[i];
            if (offset == 0f) continue;
            Text quantity = items[i].GetComponentInChildren<Text>(true);
            if (quantity != null)
                quantity.rectTransform.anchoredPosition -= Vector2.right * offset;
            quantityOffsetsX[i] = 0f;
        }
    }

    private void QueueCentering()
    {
        if (isActiveAndEnabled && visibleItemCount > 0 && visibleItemCount <= MaxCenteredItems)
            CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(this);
    }

    void ICanvasElement.Rebuild(CanvasUpdate phase)
    {
        if (phase == CanvasUpdate.PostLayout && isActiveAndEnabled &&
            itemLayout != null && itemLayout.isActiveAndEnabled)
            CenterVisibleItems();
    }

    void ICanvasElement.LayoutComplete() { }
    void ICanvasElement.GraphicUpdateComplete() { }
    bool ICanvasElement.IsDestroyed() => this == null;

    private void CenterVisibleItems()
    {
        int count = visibleItemCount;
        if (count <= 0 || count > MaxCenteredItems || items.Count < count) return;
        if (!centeringDirty)
        {
            // O GridLayoutGroup pode reposicionar as linhas durante um rebuild do Canvas.
            bool gridMoved = false;
            for (int i = 0; i < count; i++)
                gridMoved |= !Mathf.Approximately(centeredRowX[i], items[i].anchoredPosition.x);
            if (!gridMoved) return;
        }

        RestoreIconPositions();
        RestoreQuantityPositions();
        float totalWidth = 0f;
        for (int i = 0; i < count; i++)
        {
            AlignIconToResourceLine(i);
            PlaceAndMeasureItem(i, out itemCenters[i], out itemWidths[i]);
            totalWidth += itemWidths[i];
        }

        float gap = Mathf.Max(MinimumItemGap, itemLayout.cellSize.x / 3f + itemLayout.spacing.x);
        if (count > 1)
            gap = Mathf.Min(gap, Mathf.Max(0f, (itemsRoot.rect.width - totalWidth) / (count - 1)));
        RectTransform card = (RectTransform)transform;
        float centerX = itemsRoot.InverseTransformPoint(card.TransformPoint(card.rect.center)).x;
        float left = centerX - (totalWidth + gap * (count - 1)) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            RectTransform row = items[i];
            Vector2 position = row.anchoredPosition;
            position.x += left + itemWidths[i] * 0.5f - itemCenters[i];
            row.anchoredPosition = position;
            centeredRowX[i] = position.x;
            left += itemWidths[i] + gap;
        }
        centeringDirty = false;
    }

    private void PlaceAndMeasureItem(int index, out float center, out float width)
    {
        RectTransform row = items[index];
        Image image = row.GetComponentInChildren<Image>(true);
        Text quantity = row.GetComponentInChildren<Text>(true);
        bool hasIcon = image != null && image.enabled && image.sprite != null;
        if (!hasIcon && quantity == null)
        {
            center = itemsRoot.InverseTransformPoint(row.position).x;
            width = 0f;
            return;
        }
        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        if (hasIcon)
        {
            Bounds icon = VisibleIconBounds(itemsRoot, image);
            left = icon.min.x;
            right = icon.max.x;
        }
        if (quantity != null)
        {
            RectTransform numberRect = quantity.rectTransform;
            Bounds number = RectTransformUtility.CalculateRelativeRectTransformBounds(itemsRoot, numberRect);
            float scale = itemsRoot.InverseTransformVector(
                numberRect.TransformVector(Vector3.right)).magnitude;
            float textWidth = Mathf.Min(number.size.x, quantity.preferredWidth * scale);
            float alignment = (int)quantity.alignment % 3 * 0.5f;
            float start = Mathf.Lerp(number.min.x, number.max.x - textWidth, alignment);
            if (hasIcon)
            {
                float nextToIcon = right + QuantityGap;
                float shift = nextToIcon - start;
                Vector3 localShift = numberRect.parent.InverseTransformVector(
                    itemsRoot.TransformVector(Vector3.right * shift));
                quantityOffsetsX[index] = localShift.x;
                numberRect.anchoredPosition += Vector2.right * localShift.x;
                start = nextToIcon;
            }
            left = Mathf.Min(left, start);
            right = Mathf.Max(right, start + textWidth);
        }
        center = (left + right) * 0.5f;
        width = right - left;
    }

    private void AlignIconToResourceLine(int index)
    {
        RectTransform row = items[index];
        Image image = row.GetComponentInChildren<Image>(true);
        Text quantity = row.GetComponentInChildren<Text>(true);
        if (image == null || !image.enabled || image.sprite == null || quantity == null) return;
        Bounds icon = VisibleIconBounds(row, image);
        Bounds number = RectTransformUtility.CalculateRelativeRectTransformBounds(row, quantity.rectTransform);
        float lineY = number.center.y + number.extents.y * 0.5f;
        // Mantém pequenos ajustes manuais e traz os ícones fora da linha para a faixa dos números.
        const float allowedOffset = 3f;
        float newY = Mathf.Clamp(icon.center.y, lineY - allowedOffset, lineY + allowedOffset);
        Vector3 offset = row.TransformVector(Vector3.up * (newY - icon.center.y));
        float offsetY = image.rectTransform.parent.InverseTransformVector(offset).y;
        iconOffsetsY[index] = offsetY;
        image.rectTransform.anchoredPosition += Vector2.up * offsetY;
    }

    private static Bounds VisibleIconBounds(RectTransform row, Image image)
    {
        Sprite sprite = image.sprite;
        Vector2 spriteSize = sprite.rect.size;
        Rect drawing = image.GetPixelAdjustedRect();
        RectTransform rect = image.rectTransform;

        if (image.preserveAspect && drawing.width > 0f && drawing.height > 0f)
        {
            float scale = Mathf.Min(drawing.width / spriteSize.x, drawing.height / spriteSize.y);
            Vector2 size = spriteSize * scale;
            drawing.position += Vector2.Scale(drawing.size - size, rect.pivot);
            drawing.size = size;
        }

        // Os vértices da malha tight excluem a margem transparente do PNG.
        Matrix4x4 toRow = row.worldToLocalMatrix * rect.localToWorldMatrix;
        Bounds bounds = default;
        Vector2[] vertices = sprite.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 pixel = vertices[i] * sprite.pixelsPerUnit + sprite.pivot;
            Vector2 uv = new Vector2(pixel.x / spriteSize.x, pixel.y / spriteSize.y);
            Vector2 point = drawing.position + Vector2.Scale(drawing.size, uv);
            Vector3 local = toRow.MultiplyPoint3x4(point);
            if (i == 0) bounds = new Bounds(local, Vector3.zero);
            else bounds.Encapsulate(local);
        }

        return vertices.Length > 0 ? bounds :
            RectTransformUtility.CalculateRelativeRectTransformBounds(row, rect);
    }

    private void ApplyIcon(RectTransform row, SaleItem item, int index)
    {
        Image image = row.GetComponentInChildren<Image>(true);
        ItemIcon icon = IconFor(item);
        if (image == null) return;
        image.sprite = icon?.sprite;
        image.enabled = image.sprite != null;
        if (icon == null) return;

        RectTransform iconRect = image.rectTransform;
        bool left = index % 2 == 0;
        bool hasThirdLayout = index == 2 && icon.thirdSize.x > 0f && icon.thirdSize.y > 0f;
        Vector2 size = hasThirdLayout ? icon.thirdSize : left ? icon.size : icon.rightSize;
        if (size.x > 0f && size.y > 0f) iconRect.sizeDelta = size;
        iconRect.anchoredPosition = hasThirdLayout ? icon.thirdPosition : left ? icon.leftPosition : icon.rightPosition;
    }

#if UNITY_EDITOR
    public void QueueEditorCentering() => QueueCentering();

    private SaleItem PreviewItem(int index) => index switch
    {
        0 => previewLeft,
        1 => previewRight,
        _ => previewThird
    };

    public void RefreshEditorPreview()
    {
        if (itemsRoot == null || itemsRoot.childCount < 2) return;
        CacheItems();
        centeringDirty = true;
        RestoreIconPositions();
        if (Application.isPlaying)
        {
            int count = shownMission?.requirements?.Count ?? 0;
            for (int i = 0; i < count && i < items.Count; i++)
                ApplyIcon(items[i], shownMission.requirements[i].item, i);
            QueueCentering();
            return;
        }
        int previewCount = Mathf.Min(MaxCenteredItems, items.Count);
        visibleItemCount = previewCount;
        for (int i = 0; i < previewCount; i++)
        {
            RectTransform row = items[i];
            row.gameObject.SetActive(true);
            ApplyIcon(row, PreviewItem(i), i);
        }
        if (itemLayout == null) return;
        itemLayout.constraintCount = previewCount;
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRoot);
        CenterVisibleItems();
        QueueCentering();
    }

    public Vector2 EditorItemOffset(RectTransform rect)
    {
        if (rect == null) return Vector2.zero;
        CacheItems();
        for (int i = 0; i < items.Count && i < MaxCenteredItems; i++)
        {
            if (rect.parent != items[i]) continue;
            if (rect.GetComponent<Text>() != null) return Vector2.right * quantityOffsetsX[i];
            if (rect.GetComponent<Image>() != null) return Vector2.up * iconOffsetsY[i];
        }
        return Vector2.zero;
    }

    public Image EditorIconAt(int index) =>
        itemsRoot != null && index >= 0 && index < itemsRoot.childCount
            ? itemsRoot.GetChild(index).GetComponentInChildren<Image>(true) : null;

    public void CaptureEditorPreview(int index)
    {
        if (index < 0 || index > 2) return;
        Image image = EditorIconAt(index);
        if (image == null) return;
        SaleItem item = PreviewItem(index);
        if (shownMission?.requirements != null && index < shownMission.requirements.Count)
            item = shownMission.requirements[index].item;
        ItemIcon icon = IconFor(item);
        if (icon == null) return;
        icon.sprite = image.sprite;
        if (index == 0)
        {
            icon.size = image.rectTransform.sizeDelta;
            icon.leftPosition = image.rectTransform.anchoredPosition - EditorItemOffset(image.rectTransform);
        }
        else if (index == 1)
        {
            icon.rightSize = image.rectTransform.sizeDelta;
            icon.rightPosition = image.rectTransform.anchoredPosition - EditorItemOffset(image.rectTransform);
        }
        else
        {
            icon.thirdSize = image.rectTransform.sizeDelta;
            icon.thirdPosition = image.rectTransform.anchoredPosition - EditorItemOffset(image.rectTransform);
        }
    }
#endif

    private ItemIcon IconFor(SaleItem item)
    {
        foreach (ItemIcon icon in icons)
            if (icon != null && icon.item == item) return icon;
        return null;
    }
}
