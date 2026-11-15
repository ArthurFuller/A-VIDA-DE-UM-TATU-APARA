using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveMissionCard : MonoBehaviour
{
    [SerializeField] private Text description;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Text quantityText;
    [SerializeField] private Text rewardText;

    public void Bind(DeliveryMission mission, int day, int stock, Sprite icon)
    {
        if (description != null) description.text = mission.title;
        if (itemIcon != null) itemIcon.sprite = icon;
        if (quantityText != null) quantityText.text = mission.quantity + "x " + ItemName(mission.item) +
            " · estoque " + stock;
        if (rewardText != null) rewardText.text = "+" + mission.coinReward + " moedas  · dia " + mission.dueDay;
    }

    // Apenas layout: não altera o vínculo dos dados da missão nem o sprite escolhido.
    public void ConfigureIconLayout(Vector2 size, Vector2 position, float textGap,
        float rightMargin, float cardWidth)
    {
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

    private static string ItemName(DeliveryItem item) => item switch
    {
        DeliveryItem.Wood => "madeira",
        DeliveryItem.Food => "comida",
        DeliveryItem.CornSeeds => "sementes de milho",
        DeliveryItem.Fish => "peixe",
        DeliveryItem.Corn => "milho",
        DeliveryItem.Pumpkin => "macaxeira",
        _ => "sementes de macaxeira"
    };
}
