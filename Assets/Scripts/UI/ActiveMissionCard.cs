using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public sealed class ActiveMissionIconProfile
{
    public SaleItem item;
    public Sprite sprite;
    public Vector2 firstSize = new(22f, 22f);
    public Vector2 firstPosition = new(8f, -28f);
    public Vector2 secondSize = new(22f, 22f);
    public Vector2 secondPosition = new(86f, -28f);
    public Vector2 thirdSize;
    public Vector2 thirdPosition;

    public void Apply(Image image, int index)
    {
        if (image == null) return;
        Vector2 offset = ActiveMissionCard.ItemOffset(index);
        bool first = index % 2 == 0;
        bool hasThirdLayout = index == 2 && thirdSize.x > 0f && thirdSize.y > 0f;
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.rectTransform.sizeDelta = hasThirdLayout ? thirdSize : first ? firstSize : secondSize;
        image.rectTransform.anchoredPosition = hasThirdLayout ? thirdPosition :
            (first ? firstPosition : secondPosition) + offset;
    }
}

public sealed class ActiveMissionCard : MonoBehaviour
{
    [SerializeField] private Text description;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Text quantityText;
    [SerializeField] private Image secondItemIcon;
    [SerializeField] private Text secondQuantityText;
    [SerializeField] private Image thirdItemIcon;
    [SerializeField] private Text thirdQuantityText;
    [SerializeField] private Image coinIcon;
    [SerializeField] private Text rewardText;
    [SerializeField] private Text deadlineText;
    [SerializeField] private Button deliverButton;
    [SerializeField] private Text deliverLabel;

    private readonly List<Image> icons = new();
    private readonly List<Text> quantities = new();
    private Vector2 rewardPosition;
    private Vector2 coinPosition;
    private Vector2 deadlinePosition;
    private DeliveryMission shownMission;
    private DeliveryMissionBoard deliveryBoard;
    private const float RowHeight = 24f;
    private const float ThirdItemOffset = 64f;
    private const float BaseHeight = 70f;
    private float ExtraRowOffset
    {
        get
        {
            int count = shownMission?.requirements?.Count ?? 0;
            return count > 3 ? -((count - 1) / 2) * RowHeight : 0f;
        }
    }
    public float RequiredHeight => BaseHeight - ExtraRowOffset;

    internal static Vector2 ItemOffset(int index) => index == 2 ? Vector2.right * ThirdItemOffset :
        Vector2.down * (index / 2 * RowHeight);

    private void Awake()
    {
        EnsureTemplates();
        CacheRewardPositions();
        if (shownMission == null)
        {
            if (thirdItemIcon != null) thirdItemIcon.gameObject.SetActive(false);
            if (thirdQuantityText != null) thirdQuantityText.gameObject.SetActive(false);
        }
    }

    private void EnsureTemplates()
    {
        if (icons.Count != 0) return;
        icons.Add(itemIcon);
        icons.Add(secondItemIcon);
        quantities.Add(quantityText);
        quantities.Add(secondQuantityText);
        if (thirdItemIcon != null && thirdQuantityText != null)
        {
            icons.Add(thirdItemIcon);
            quantities.Add(thirdQuantityText);
        }
    }

    private void CacheRewardPositions()
    {
        Vector2 offset = Vector2.up * ExtraRowOffset;
        if (coinIcon != null) coinPosition = coinIcon.rectTransform.anchoredPosition - offset;
        if (rewardText != null) rewardPosition = rewardText.rectTransform.anchoredPosition - offset;
        if (deadlineText != null) deadlinePosition = deadlineText.rectTransform.anchoredPosition - offset;
    }

    public float Bind(DeliveryMission mission, IReadOnlyList<ActiveMissionIconProfile> profiles,
        DeliveryMissionBoard board, bool instantButtonState = false)
    {
        EnsureTemplates();
        shownMission = mission;
        deliveryBoard = board;
        ApplyDeliveryButtonState(board.CanDeliver(mission), instantButtonState);
        if (description != null) description.text = mission.title;

        int count = mission.requirements?.Count ?? 0;
        while (icons.Count < count)
        {
            int index = icons.Count;
            Image icon = Instantiate(index % 2 == 0 ? itemIcon : secondItemIcon, transform);
            Text amount = Instantiate(index % 2 == 0 ? quantityText : secondQuantityText, transform);
            icon.name = "Ícone do item " + (index + 1);
            amount.name = "Quantidade do item " + (index + 1);
            Vector2 offset = ItemOffset(index);
            icon.rectTransform.anchoredPosition += offset;
            amount.rectTransform.anchoredPosition += offset;
            icons.Add(icon);
            quantities.Add(amount);
        }

        RefreshIconProfiles(profiles);

        if (rewardText != null) rewardText.text = mission.coinReward.ToString();
        if (deadlineText != null) deadlineText.text = "Prazo: dia " + mission.dueDay;
        ApplyRewardPositions();
        return RequiredHeight;
    }

    private void ApplyRewardPositions()
    {
        Vector2 offset = Vector2.up * ExtraRowOffset;
        if (coinIcon != null) coinIcon.rectTransform.anchoredPosition = coinPosition + offset;
        if (rewardText != null) rewardText.rectTransform.anchoredPosition = rewardPosition + offset;
        if (deadlineText != null) deadlineText.rectTransform.anchoredPosition = deadlinePosition + offset;
    }

    public void RefreshIconProfiles(IReadOnlyList<ActiveMissionIconProfile> profiles)
    {
        if (shownMission == null) return;
        int count = shownMission.requirements?.Count ?? 0;
        for (int i = 0; i < icons.Count; i++)
        {
            Image icon = icons[i];
            Text amount = quantities[i];
            bool visible = i < count;
            if (icon != null) icon.gameObject.SetActive(visible);
            if (amount != null) amount.gameObject.SetActive(visible);
            if (!visible) continue;

            DeliveryRequirement requirement = shownMission.requirements[i];
            ActiveMissionIconProfile profile = FindProfile(profiles, requirement.item);
            profile?.Apply(icon, i);
            if (icon != null && profile == null) icon.enabled = false;
            if (amount != null)
                amount.text = requirement.quantity.ToString();
        }
    }

    public void Deliver()
    {
        if (deliveryBoard == null || deliverButton == null || !deliverButton.IsInteractable()) return;
        // Teclado e controle podem acionar o botão sem passar pelo raycast.
        if (TryGetComponent(out CanvasGroup entranceGroup) && !entranceGroup.blocksRaycasts) return;
        RectTransform origin = coinIcon != null ? coinIcon.rectTransform : (RectTransform)deliverButton.transform;
        deliveryBoard.DeliverFromCard(this, shownMission, origin);
    }

    private void ApplyDeliveryButtonState(bool canDeliver, bool instant)
    {
        if (deliverButton == null) return;
        ColorBlock colors = deliverButton.colors;
        if (!instant || colors.fadeDuration == 0f)
        {
            deliverButton.interactable = canDeliver;
            return;
        }

        // O card pode ser reaproveitado com o botão ainda tingido pela missão anterior.
        ColorBlock immediateColors = colors;
        immediateColors.fadeDuration = 0f;
        deliverButton.colors = immediateColors;
        deliverButton.interactable = canDeliver;
        deliverButton.colors = colors;
    }

    public void SetDelivering()
    {
        if (deliverButton != null) deliverButton.interactable = false;
    }

    public Image IconAt(int index)
    {
        EnsureTemplates();
        return index >= 0 && index < icons.Count ? icons[index] : null;
    }

    public void CopyFormattingFrom(ActiveMissionCard model)
    {
        if (model == null || model == this) return;
        Vector2 rewardOffset = Vector2.up * (ExtraRowOffset - model.ExtraRowOffset);
        CopyText(model.description, description);
        CopyImage(model.itemIcon, itemIcon);
        CopyImage(model.secondItemIcon, secondItemIcon);
        CopyText(model.quantityText, quantityText);
        CopyText(model.secondQuantityText, secondQuantityText);
        CopyImage(model.thirdItemIcon, thirdItemIcon);
        CopyText(model.thirdQuantityText, thirdQuantityText);
        CopyImage(model.coinIcon, coinIcon, rewardOffset);
        CopyText(model.rewardText, rewardText, rewardOffset);
        CopyText(model.deadlineText, deadlineText, rewardOffset);
        if (model.deliverButton != null && deliverButton != null)
        {
            CopyImage(model.deliverButton.targetGraphic as Image, deliverButton.targetGraphic as Image);
            deliverButton.colors = model.deliverButton.colors;
        }
        CopyText(model.deliverLabel, deliverLabel);
        CopyImage(model.GetComponent<Image>(), GetComponent<Image>(), copyRect: false);
        if (transform is RectTransform rect && model.transform is RectTransform modelRect)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, modelRect.rect.width);

        EnsureTemplates();
        for (int i = AuthoredSlotCount; i < icons.Count; i++)
        {
            Vector2 offset = ItemOffset(i);
            CopyImage(i % 2 == 0 ? model.itemIcon : model.secondItemIcon, icons[i], offset);
            CopyText(i % 2 == 0 ? model.quantityText : model.secondQuantityText, quantities[i], offset);
        }

        CacheRewardPositions();
    }

    private int AuthoredSlotCount => thirdItemIcon != null && thirdQuantityText != null ? 3 : 2;

    private static void CopyRect(RectTransform source, RectTransform target, Vector2 offset)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition3D = source.anchoredPosition3D + (Vector3)offset;
    }

    private static void CopyImage(Image source, Image target, Vector2 offset = default, bool copyRect = true)
    {
        if (source == null || target == null) return;
        if (copyRect) CopyRect(source.rectTransform, target.rectTransform, offset);
        target.color = source.color;
        target.material = source.material == source.defaultMaterial ? null : source.material;
        target.sprite = source.sprite;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillOrigin = source.fillOrigin;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.useSpriteMesh = source.useSpriteMesh;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
    }

    private static void CopyText(Text source, Text target, Vector2 offset = default)
    {
        if (source == null || target == null) return;
        CopyRect(source.rectTransform, target.rectTransform, offset);
        target.color = source.color;
        target.material = source.material == source.defaultMaterial ? null : source.material;
        target.font = source.font;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.alignment = source.alignment;
        target.alignByGeometry = source.alignByGeometry;
        target.lineSpacing = source.lineSpacing;
        target.supportRichText = source.supportRichText;
        target.horizontalOverflow = source.horizontalOverflow;
        target.verticalOverflow = source.verticalOverflow;
        target.resizeTextForBestFit = source.resizeTextForBestFit;
        target.resizeTextMinSize = source.resizeTextMinSize;
        target.resizeTextMaxSize = source.resizeTextMaxSize;
    }

#if UNITY_EDITOR
    public void ShowEditorThirdItem()
    {
        if (Application.isPlaying || AuthoredSlotCount < 3) return;
        thirdItemIcon.gameObject.SetActive(true);
        thirdQuantityText.gameObject.SetActive(true);
    }

    public SaleItem PreviewedItem(int index, SaleItem fallback) =>
        shownMission?.requirements != null && index < shownMission.requirements.Count
            ? shownMission.requirements[index].item : fallback;

    public bool UsesExtraRowOffset(RectTransform rect) =>
        rect == coinIcon?.rectTransform || rect == rewardText?.rectTransform ||
        rect == deadlineText?.rectTransform;

    public float EditorExtraRowOffset => ExtraRowOffset;

    public void RefreshEditorFormatting(IReadOnlyList<ActiveMissionIconProfile> profiles)
    {
        EnsureTemplates();
        // Recalculate the authored baseline while leaving the reward on its current row.
        CacheRewardPositions();
        for (int i = AuthoredSlotCount; i < icons.Count; i++)
        {
            Vector2 row = ItemOffset(i);
            CopyImage(i % 2 == 0 ? itemIcon : secondItemIcon, icons[i], row);
            CopyText(i % 2 == 0 ? quantityText : secondQuantityText, quantities[i], row);
        }
        RefreshIconProfiles(profiles);
    }
#endif

    private static ActiveMissionIconProfile FindProfile(
        IReadOnlyList<ActiveMissionIconProfile> profiles, SaleItem item)
    {
        if (profiles == null) return null;
        for (int i = 0; i < profiles.Count; i++)
            if (profiles[i] != null && profiles[i].item == item) return profiles[i];
        return null;
    }
}
