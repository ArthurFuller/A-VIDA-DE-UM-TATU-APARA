using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopBuyPanel : MonoBehaviour
{
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ShopQuantityRow foodRow;
    [SerializeField] private ShopQuantityRow fenceRow;
    [SerializeField] private Text totalText;
    [SerializeField] private Text coinTotalText;
    [SerializeField] private GameObject woodTotalGroup;
    [SerializeField] private Text woodTotalText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button resetButton;
    private ShopQuantityRow[] rows;

    private void Awake()
    {
        rows = new[] { foodRow, fenceRow };
        if (buyButton != null) buyButton.onClick.AddListener(Buy);
        if (resetButton != null) resetButton.onClick.AddListener(ResetSelection);
    }

    private void OnEnable()
    {
        if (resources != null) resources.Changed += Refresh;
        if (merchant != null) merchant.AvailabilityChanged += Refresh;
        foreach (ShopQuantityRow row in rows)
            if (row != null) row.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
        if (merchant != null) merchant.AvailabilityChanged -= Refresh;
        foreach (ShopQuantityRow row in rows)
            if (row != null) row.Changed -= Refresh;
    }

    public void ResetSelection()
    {
        foreach (ShopQuantityRow row in rows)
            if (row != null) row.ResetQuantity();
        Refresh();
    }

    public void Buy()
    {
        if (merchant == null || foodRow == null || fenceRow == null) return;
        if (merchant.TryBuyCart(0, 0, 0, foodRow.Quantity, fenceRow.Quantity))
            ResetSelection();
    }

    public void Refresh()
    {
        if (merchant == null || resources == null || foodRow == null || fenceRow == null) return;
        ReadOnlySpan<int> prices = stackalloc int[] {
            merchant.GetUnitPrice(SaleItem.Food),
            merchant.FenceCoinCost
        };
        bool canBuy = merchant.CanTrade && resources.Coins > 0;
        bool canBuyFences = merchant.CanBuyFenceCards;
        int woodPerFence = merchant.FenceWoodCost;
        long budget = canBuy ? resources.Coins : 0;
        long spendingLimit = budget;
        int woodBudget = resources.Wood;
        for (int i = 0; i < rows.Length; i++)
        {
            int limit = !canBuy ? 0 : prices[i] == 0 ? int.MaxValue : (int)(budget / prices[i]);
            if (i == 0) limit = Math.Min(limit, int.MaxValue - resources.Food);
            if (i == 1 && !canBuyFences) limit = 0;
            else if (i == 1 && woodPerFence > 0) limit = Math.Min(limit, woodBudget / woodPerFence);
            rows[i].SetOffer(prices[i], limit, woodCost: i == 1 ? woodPerFence : 0);
            budget -= (long)rows[i].Quantity * prices[i];
            woodBudget -= rows[i].Quantity * (i == 1 ? woodPerFence : 0);
        }
        long total = spendingLimit - budget;
        for (int i = 0; i < rows.Length; i++)
        {
            int limit = !canBuy ? 0 : prices[i] == 0 ? int.MaxValue :
                (int)Math.Min(int.MaxValue, rows[i].Quantity + budget / prices[i]);
            if (i == 0) limit = Math.Min(limit, int.MaxValue - resources.Food);
            if (i == 1 && !canBuyFences) limit = 0;
            else if (i == 1 && woodPerFence > 0)
                limit = Math.Min(limit, (int)Math.Min(int.MaxValue, (long)rows[i].Quantity +
                    woodBudget / woodPerFence));
            rows[i].SetLimit(limit);
        }
        int woodTotal = resources.Wood - woodBudget;
        if (coinTotalText != null) coinTotalText.text = total.ToString();
        else if (totalText != null) totalText.text = "TOTAL: " + ShopQuantityRow.FormatCost(total, woodTotal);
        if (woodTotalText != null) woodTotalText.text = woodTotal.ToString();
        if (woodTotalGroup != null) woodTotalGroup.SetActive(woodTotal > 0);
        if (buyButton != null)
            buyButton.interactable = canBuy && (foodRow.Quantity > 0 || fenceRow.Quantity > 0);
    }
}
