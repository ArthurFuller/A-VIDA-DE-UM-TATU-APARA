using UnityEngine;

public sealed class InventoryTrading : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private GameFeedback feedback;
    [Header("Preços provisórios por unidade")]
    [SerializeField, HideInInspector, Min(0)] private int woodPrice = 1;
    [SerializeField, HideInInspector, Min(0)] private int fishPrice = 2;
    [SerializeField, HideInInspector, Min(0)] private int cornPrice = 4;
    [SerializeField, HideInInspector, Min(0)] private int pumpkinPrice = 1;
    [SerializeField, HideInInspector, Min(0)] private int foodPrice = 1;
    [SerializeField, HideInInspector, Min(0)] private int cornSeedPrice = 1;
    [SerializeField, HideInInspector, Min(0)] private int pumpkinSeedPrice = 2;
    [SerializeField, HideInInspector, Min(0)] private int jerimumPrice = 3;
    [SerializeField, HideInInspector, Min(0)] private int jerimumSeedPrice = 2;
    [SerializeField, HideInInspector, Min(0)] private int milkPrice = 2;
    [SerializeField, HideInInspector, Min(0)] private int eggPrice = 2;

    public int GetStock(SaleItem item) => resources != null ? resources.Stock(item) : 0;

    public int GetPrice(SaleItem item) => GetPrice(item, out _);

    internal int GetPrice(SaleItem item, out bool hasSaleBonus)
    {
        GameBalance balance = GameBalance.Current;
        int price = item switch
        {
            SaleItem.Wood => balance != null ? balance.woodPrice : woodPrice,
            SaleItem.Fish => balance != null ? balance.fishPrice : fishPrice,
            SaleItem.Corn => balance != null ? balance.cornPrice : cornPrice,
            SaleItem.Pumpkin => balance != null ? balance.pumpkinPrice : pumpkinPrice,
            SaleItem.Jerimum => balance != null ? balance.jerimumPrice : jerimumPrice,
            SaleItem.Food => balance != null ? balance.foodPrice : foodPrice,
            SaleItem.CornSeeds => balance != null ? balance.cornSeedPrice : cornSeedPrice,
            SaleItem.PumpkinSeeds => balance != null ? balance.pumpkinSeedPrice : pumpkinSeedPrice,
            SaleItem.JerimumSeeds => balance != null ? balance.jerimumSeedPrice : jerimumSeedPrice,
            SaleItem.Milk => balance != null ? balance.milkPrice : milkPrice,
            SaleItem.Eggs => balance != null ? balance.eggPrice : eggPrice,
            _ => 0
        };
        int basePrice = price;
        if (item == SaleItem.Corn || item == SaleItem.Pumpkin || item == SaleItem.Jerimum)
        {
            CropType crop = item == SaleItem.Corn ? CropType.Corn :
                item == SaleItem.Pumpkin ? CropType.Pumpkin : CropType.Jerimum;
            price = (int)System.Math.Min(int.MaxValue, (long)price +
                (MandalaSynergy.Instance != null ?
                    MandalaSynergy.Instance.CropSalePriceBonus(crop) : 0));
        }
        hasSaleBonus = price > basePrice;
        return price;
    }

    public bool TrySellBatch(int[] quantities)
    {
        if (merchant == null || !merchant.CanTrade || !merchant.IsOpen || resources == null)
        {
            if (feedback != null) feedback.ShowMessage("Visite a barraca aberta para vender.");
            return false;
        }
        int[] prices = new int[(int)SaleItem.Eggs + 1];
        for (int i = 0; i < prices.Length; i++) prices[i] = GetPrice((SaleItem)i);
        if (!resources.TrySellBatch(quantities, prices))
        {
            if (feedback != null) feedback.ShowMessage("Confira as quantidades escolhidas para venda.");
            return false;
        }
        if (feedback != null) feedback.ShowMessage("Venda concluída.");
        return true;
    }

    public bool TryPrepare(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs,
        bool plus, out int food)
    {
        food = 0;
        if (!TryGetRecipe(fish, corn, pumpkin, jerimum, milk, eggs, plus,
            out _, out int output)) return false;
        return resources != null && resources.TryPrepareFood(fish, corn, pumpkin, jerimum,
            milk, eggs, output, out food);
    }

    public bool TryGetRecipe(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs,
        bool plus, out string recipeName, out int output) =>
        EvaluateRecipe(fish, corn, pumpkin, jerimum, milk, eggs, plus, out recipeName, out output);

    public static bool EvaluateRecipe(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs,
        bool plus, out string recipeName, out int output)
    {
        recipeName = null;
        output = 0;
        System.ReadOnlySpan<int> selected = stackalloc int[] { fish, corn, pumpkin, jerimum, milk, eggs };
        int amount = 0;
        int types = 0;
        foreach (int count in selected)
        {
            if (count < 0 || count > 3) return false;
            if (count > 0) types++;
            amount += count;
            if (amount > 3 || types > 2) return false;
        }
        if (amount < 2) return false;
        recipeName = types == 1 ? "Alimentos iguais" : "Alimentos diferentes";
        output = RecipeYield(amount, types == 2, plus, GameBalance.Current);
        return true;
    }

    internal static int RecipeYield(int amount, bool mixed, bool plus, GameBalance balance)
    {
        int preparationLimit = balance != null ? Mathf.Max(1, balance.maxPreparedFoodPerAction) : 3;
        int output = Mathf.Min(mixed ? amount : amount - 1, preparationLimit);
        if (!plus) return output;
        long bonus = balance != null ? Mathf.Max(0, balance.foodPlusBonus) : 1;
        if (amount == 3 && mixed)
            bonus += balance != null ? Mathf.Max(0, balance.foodPlusMixedTripleBonus) : 1;
        return (int)System.Math.Min(int.MaxValue, output + bonus);
    }
}
