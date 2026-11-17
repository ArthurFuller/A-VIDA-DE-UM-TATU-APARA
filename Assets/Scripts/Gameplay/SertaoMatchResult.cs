using System;

internal sealed class SertaoMatchResult
{
    internal readonly struct Deduction
    {
        public readonly int Count;
        public readonly int UnitCost;
        public long Total => (long)Count * UnitCost;

        public Deduction(int count, int unitCost)
        {
            Count = count;
            UnitCost = Math.Max(0, unitCost);
        }
    }

    private readonly long[] production;
    public readonly long CoinsEarned;
    public readonly long StartingBalance;
    public readonly int Delivered;
    public readonly int DevelopedPlots;
    public readonly int TotalPlots;
    public readonly int BuiltHouses;
    public readonly int TotalHouses;
    public readonly Deduction MissedMeals;
    public readonly Deduction UndeliveredOrders;
    public readonly Deduction UndevelopedPlots;
    public readonly Deduction UnbuiltHouses;

    private long TotalDeductions => MissedMeals.Total + UndeliveredOrders.Total +
        UndevelopedPlots.Total + UnbuiltHouses.Total;
    public long FinalCoins => StartingBalance - TotalDeductions;
    public bool Won => FinalCoins > 0;
    public long Produced(SaleItem item) => production[(int)item];

    public SertaoMatchResult(ResourceBank bank, int missedMeals, int delivered, int undelivered,
        int developedPlots, int totalPlots, int builtHouses, int totalHouses, GameBalance balance)
        : this(bank.CopyProduction(), bank.CoinsEarned, bank.Coins, missedMeals, delivered,
            undelivered, developedPlots, totalPlots, builtHouses, totalHouses, balance)
    {
    }

    private SertaoMatchResult(long[] production, long coinsEarned, long startingBalance,
        int missedMeals, int delivered, int undelivered, int developedPlots, int totalPlots,
        int builtHouses, int totalHouses, GameBalance balance, int? previewFinalBalance = null)
    {
        this.production = production;
        CoinsEarned = coinsEarned;
        Delivered = delivered;
        DevelopedPlots = developedPlots;
        TotalPlots = totalPlots;
        BuiltHouses = builtHouses;
        TotalHouses = totalHouses;
        MissedMeals = new Deduction(missedMeals, balance != null ? balance.missedMealPenalty : 2);
        UndeliveredOrders = new Deduction(undelivered, balance != null ? balance.undeliveredOrderPenalty : 3);
        UndevelopedPlots = new Deduction(totalPlots - developedPlots, balance != null ? balance.undevelopedPlotPenalty : 5);
        UnbuiltHouses = new Deduction(totalHouses - builtHouses, balance != null ? balance.unbuiltHousePenalty : 7);
        StartingBalance = previewFinalBalance.HasValue ? TotalDeductions + previewFinalBalance.Value : startingBalance;
    }

#if UNITY_EDITOR
    internal static SertaoMatchResult CreatePreview(int finalBalance, int missedMealsToday, GameBalance balance)
    {
        var production = new long[(int)SaleItem.Eggs + 1];
        production[(int)SaleItem.Wood] = 34;
        production[(int)SaleItem.Fish] = 21;
        production[(int)SaleItem.Corn] = 32;
        production[(int)SaleItem.Pumpkin] = 16;
        production[(int)SaleItem.Jerimum] = 12;
        production[(int)SaleItem.Food] = 19;
        production[(int)SaleItem.Milk] = 9;
        production[(int)SaleItem.Eggs] = 14;
        return new SertaoMatchResult(production, coinsEarned: 142, startingBalance: 0,
            missedMeals: 4 + missedMealsToday, delivered: 5, undelivered: 2,
            developedPlots: 12, totalPlots: 20, builtHouses: 1, totalHouses: 3,
            balance: balance, previewFinalBalance: finalBalance);
    }
#endif
}
