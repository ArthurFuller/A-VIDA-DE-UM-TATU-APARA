using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveMissionCard : MonoBehaviour
{
    [SerializeField] private Text description;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Text quantityText;
    [SerializeField] private Text rewardText;
    private Vector2 iconPosition;
    private float iconGap;
    private float rightMargin;
    private float cardWidth;

    public float Bind(DeliveryMission mission, ResourceBank resources, Sprite icon)
    {
        float titleHeight = 20f;
        if (description != null)
        {
            description.text = mission.title +
                (string.IsNullOrWhiteSpace(mission.description) ? "" : "\n" + mission.description);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Overflow;
            titleHeight = Mathf.Max(20f, description.preferredHeight);
            description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, titleHeight);
        }

        bool singleIcon = itemIcon != null && mission.requirements != null &&
            mission.requirements.Count == 1 && icon != null;
        if (itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.enabled = singleIcon;
            if (singleIcon)
            {
                Vector2 position = itemIcon.rectTransform.anchoredPosition;
                position.y = iconPosition.y - (4f + titleHeight + 4f - 28f);
                itemIcon.rectTransform.anchoredPosition = position;
            }
        }
        float quantityTop = 4f + titleHeight + 4f;
        float quantityHeight = 18f;
        if (quantityText != null)
        {
            var lines = new StringBuilder();
            if (mission.requirements != null)
                foreach (DeliveryRequirement requirement in mission.requirements)
                {
                    if (lines.Length > 0) lines.Append('\n');
                    lines.Append(requirement.quantity).Append("x ")
                        .Append(DeliveryMissionCatalog.ItemName(requirement.item))
                        .Append(" · estoque ").Append(resources.Stock(requirement.item));
                }
            quantityText.text = lines.ToString();
            quantityText.horizontalOverflow = HorizontalWrapMode.Wrap;
            quantityText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rect = quantityText.rectTransform;
            Vector2 position = rect.anchoredPosition;
            position.x = singleIcon ? iconPosition.x + itemIcon.rectTransform.rect.width + iconGap : 8f;
            position.y = -quantityTop;
            rect.anchoredPosition = position;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Max(1f, cardWidth - position.x - rightMargin));
            quantityHeight = Mathf.Max(18f, quantityText.preferredHeight);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, quantityHeight);
        }
        if (rewardText != null) rewardText.text = "+" + mission.coinReward + " moedas  · dia " + mission.dueDay;
        if (rewardText != null)
        {
            Vector2 position = rewardText.rectTransform.anchoredPosition;
            position.y = -quantityTop - quantityHeight - 8f;
            rewardText.rectTransform.anchoredPosition = position;
        }
        return quantityTop + quantityHeight + 24f;
    }

    // Apenas layout: não altera o vínculo dos dados da missão nem o sprite escolhido.
    public void ConfigureIconLayout(Vector2 size, Vector2 position, float textGap,
        float rightMargin, float cardWidth)
    {
        iconPosition = position;
        iconGap = textGap;
        this.rightMargin = rightMargin;
        this.cardWidth = cardWidth;
        if (itemIcon == null) return;
        RectTransform iconRect = itemIcon.rectTransform;
        size.x = Mathf.Max(1f, size.x);
        size.y = Mathf.Max(1f, size.y);
        iconRect.sizeDelta = size;
        iconRect.anchoredPosition = position;

        if (quantityText == null) return;
        // O texto acompanha a largura do ícone, para não ficar por baixo dele.
        RectTransform quantityRect = quantityText.rectTransform;
        Vector2 quantityPosition = quantityRect.anchoredPosition;
        quantityPosition.x = position.x + size.x + Mathf.Max(0f, textGap);
        quantityRect.anchoredPosition = quantityPosition;
        quantityRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            Mathf.Max(1f, cardWidth - quantityPosition.x - Mathf.Max(0f, rightMargin)));
    }

}
