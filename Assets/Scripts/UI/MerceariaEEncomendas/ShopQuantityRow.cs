using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopQuantityRow : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text quantityText;
    [SerializeField] private Text amountText;
    [SerializeField] private Text unitPriceText;
    [SerializeField] private Color bonusPriceColor = new Color32(145, 84, 16, 255);
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private CanvasGroup rowGroup;
    [SerializeField] private string itemName;

    private int quantity;
    private int limit;
    private int unitPrice;
    private bool hasSaleBonus;
    private Color? normalUnitPriceColor;
    private int woodPrice;
    private int stock = -1;

    public event Action Changed;
    public int Quantity => quantity;

    internal static string FormatCost(long coins, long wood = 0)
    {
        string price = coins + (coins == 1 ? " moeda" : " moedas");
        return wood > 0 ? price + " + " + wood + (wood == 1 ? " madeira" : " madeiras") : price;
    }

    private void Awake()
    {
        if (decreaseButton != null) decreaseButton.onClick.AddListener(Decrease);
        if (increaseButton != null) increaseButton.onClick.AddListener(Increase);
        Refresh();
    }

    internal void SetOffer(int price, int maximum, int available = -1, bool boosted = false, int woodCost = 0)
    {
        unitPrice = Mathf.Max(0, price);
        hasSaleBonus = boosted;
        woodPrice = Mathf.Max(0, woodCost);
        stock = available;
        SetLimit(maximum);
    }
    internal void SetLimit(int maximum)
    {
        limit = Mathf.Max(0, maximum);
        quantity = Mathf.Min(quantity, limit);
        Refresh();
    }

    internal void ResetQuantity()
    {
        quantity = 0;
        Refresh();
    }

    private void Increase()
    {
        if (quantity >= limit) return;
        quantity++;
        Refresh();
        Changed?.Invoke();
    }

    private void Decrease()
    {
        if (quantity <= 0) return;
        quantity--;
        Refresh();
        Changed?.Invoke();
    }

    private void Refresh()
    {
        if (nameText != null)
            nameText.text = stock < 0 ? itemName : itemName + " · " + stock + " no estoque";
        if (quantityText != null) quantityText.text = quantity.ToString();
        if (amountText != null) amountText.text = ((long)quantity * unitPrice).ToString();
        if (unitPriceText != null)
        {
            normalUnitPriceColor ??= unitPriceText.color;
            unitPriceText.text = FormatCost(unitPrice, woodPrice) + "/un.";
            unitPriceText.color = hasSaleBonus ? bonusPriceColor : normalUnitPriceColor.Value;
        }
        if (decreaseButton != null) decreaseButton.interactable = quantity > 0;
        if (increaseButton != null) increaseButton.interactable = quantity < limit;
        if (rowGroup != null) rowGroup.alpha = limit == 0 ? 0.42f : 1f;
    }
}
