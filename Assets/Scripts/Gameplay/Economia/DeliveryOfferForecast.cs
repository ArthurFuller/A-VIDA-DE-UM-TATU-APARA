using System;
using System.Collections.Generic;
using UnityEngine;

// Estimates a possible plan for the offers. It never changes the actual farm or inventory.
public sealed class DeliveryOfferForecast
{
    public enum Feasibility
    {
        Shortfall,
        Tight,
        WithReserve
    }

    private sealed class ActionEstimate
    {
        public ActivityKind Card;
        public SaleItem Item;
        public int Amount;
        public int Eggs;
        public List<Plot> Plots;
    }

    private static readonly SaleItem[] Ingredients =
    {
        SaleItem.Fish, SaleItem.Corn, SaleItem.Pumpkin, SaleItem.Jerimum, SaleItem.Milk, SaleItem.Eggs
    };

    private const int ItemCount = (int)SaleItem.Eggs + 1;
    private readonly int[] stock = new int[ItemCount];
    private readonly int[] cards = new int[(int)ActivityKind.FeedAnimals + 1];
    private readonly Plot[] plots;
    private readonly SeedCatalog seeds;
    private readonly DayEventController events;
    private readonly GameBalance balance;
    private readonly int day;
    private readonly int finalDay;
    private readonly int workers;
    private readonly int workersToday;
    private readonly int actionsToday;
    private readonly int todayMilk;
    private readonly int todayEggs;
    private readonly int milkPerCow;
    private readonly int eggsPerChicken;

    public DeliveryOfferForecast(ResourceBank resources, Plot[] plots, WorkerAssignment[] workers,
        SeedCatalog seeds, ActivityCardAvailability hand, DayEventController events, int day,
        int finalDay, int todayMilk, int todayEggs, int milkPerCow, int eggsPerChicken)
    {
        this.plots = plots ?? Array.Empty<Plot>();
        this.seeds = seeds;
        this.events = events;
        this.day = day;
        this.finalDay = finalDay;
        this.todayMilk = todayMilk;
        this.todayEggs = todayEggs;
        this.milkPerCow = milkPerCow;
        this.eggsPerChicken = eggsPerChicken;
        balance = GameBalance.Current;

        for (int i = 0; i < stock.Length; i++)
            stock[i] = resources != null ? resources.Stock((SaleItem)i) : 0;
        stock[(int)SaleItem.Milk] += todayMilk;
        stock[(int)SaleItem.Eggs] += todayEggs;

        if (hand != null)
            for (int i = 0; i < cards.Length; i++) cards[i] = hand.CountOwnedCards((ActivityKind)i);

        if (workers != null)
            foreach (WorkerAssignment worker in workers)
            {
                if (worker == null) continue;
                this.workers++;
                if (worker.IsUnavailableToday) continue;
                workersToday++;
                actionsToday += worker.ActionsRemaining;
            }
    }

    public Feasibility Evaluate(IReadOnlyList<DeliveryMission> accepted, DeliveryMission candidate)
    {
        int lastDay = candidate.dueDay;
        foreach (DeliveryMission mission in accepted) lastDay = Mathf.Max(lastDay, mission.dueDay);
        int[][] due = new int[lastDay - day + 1][];
        for (int i = 0; i < due.Length; i++) due[i] = new int[ItemCount];
        foreach (DeliveryMission mission in accepted) AddDemand(due[mission.dueDay - day], mission);
        AddDemand(due[candidate.dueDay - day], candidate);

        if (Simulate(due, keepFood: true, preferHighYield: false) == 0) return Feasibility.WithReserve;
        bool canDeliver = Simulate(due, keepFood: false, preferHighYield: false) == 0 ||
            Simulate(due, keepFood: false, preferHighYield: true) == 0;
        return canDeliver ? Feasibility.Tight : Feasibility.Shortfall;
    }

    private static void AddDemand(int[] demand, DeliveryMission mission)
    {
        foreach (DeliveryRequirement requirement in mission.requirements)
            demand[(int)requirement.item] += requirement.quantity;
    }

    private int Simulate(int[][] due, bool keepFood, bool preferHighYield)
    {
        var available = (int[])stock.Clone();
        var harvested = new HashSet<Plot>();
        var extraGrowth = new Dictionary<Plot, int>();
        var outstanding = new int[ItemCount];
        foreach (int[] date in due)
            for (int item = 0; item < ItemCount; item++)
                outstanding[item] += date[item];

        int unmet = 0;
        for (int offset = 0; offset < due.Length; offset++)
        {
            int date = day + offset;
            int meals = keepFood && date < finalDay ? (offset == 0 ? workersToday : workers) : 0;
            int milk = todayMilk, eggs = todayEggs;
            if (offset > 0)
            {
                ProjectAnimals(date, harvested, out milk, out eggs);
                available[(int)SaleItem.Milk] += milk;
                available[(int)SaleItem.Eggs] += eggs;
                ProjectGrowth(date, harvested, extraGrowth);
            }
            int actions = offset == 0 ? actionsToday : workers;
            var remainingCards = (int[])cards.Clone();
            List<ActionEstimate> options = Options(date, harvested, extraGrowth, milk, eggs, preferHighYield);
            while (actions > 0)
            {
                ActionEstimate best = null;
                int bestScore = 0;
                int foodSurplus = FoodSurplus(available, outstanding);
                foreach (ActionEstimate option in options)
                {
                    if (remainingCards[(int)option.Card] == 0) continue;
                    int score = GainScore(option.Item, option.Amount, available, outstanding, due[offset], out int spare);
                    if (option.Eggs > 0)
                    {
                        score += GainScore(SaleItem.Eggs, option.Eggs, available, outstanding, due[offset], out int spareEggs);
                        spare += spareEggs;
                    }
                    score += 7 * Mathf.Min(spare, Mathf.Max(0, meals - foodSurplus));
                    if (score <= bestScore) continue;
                    best = option;
                    bestScore = score;
                }

                if (TryCook(available, outstanding, due[offset], meals, remainingCards, bestScore))
                {
                    actions--;
                    continue;
                }
                if (best == null) break;
                available[(int)best.Item] += best.Amount;
                available[(int)SaleItem.Eggs] += best.Eggs;
                remainingCards[(int)best.Card]--;
                actions--;
                if (best.Plots != null)
                {
                    harvested.UnionWith(best.Plots);
                    options = Options(date, harvested, extraGrowth, milk, eggs, preferHighYield);
                }
            }

            for (int i = 0; i < ItemCount; i++)
            {
                int quantity = due[offset][i];
                unmet += Mathf.Max(0, quantity - available[i]);
                available[i] = Mathf.Max(0, available[i] - quantity);
                outstanding[i] -= quantity;
            }

            // Feed the team from surplus across all edible items. Commitments compete for the same stock.
            for (int i = 0; i < meals; i++)
            {
                int item = available[(int)SaleItem.Food] > outstanding[(int)SaleItem.Food]
                    ? (int)SaleItem.Food : -1;
                foreach (SaleItem ingredient in Ingredients)
                    if (item == -1 && available[(int)ingredient] > outstanding[(int)ingredient])
                        item = (int)ingredient;
                if (item < 0)
                {
                    // Food already needed later can still be used if future production replenishes it.
                    int prepared = (int)SaleItem.Food;
                    int largestStock = 0;
                    foreach (SaleItem ingredient in Ingredients)
                    {
                        int index = (int)ingredient;
                        if (available[index] <= largestStock) continue;
                        largestStock = available[index];
                        item = index;
                    }
                    if (item < 0 && available[prepared] > 0) item = prepared;
                }
                if (item < 0) unmet++;
                else available[item]--;
            }
        }
        return unmet;
    }

    private static int FoodSurplus(int[] available, int[] outstanding)
    {
        int amount = Mathf.Max(0, available[(int)SaleItem.Food] - outstanding[(int)SaleItem.Food]);
        foreach (SaleItem ingredient in Ingredients)
            amount += Mathf.Max(0, available[(int)ingredient] - outstanding[(int)ingredient]);
        return amount;
    }

    private static int GainScore(SaleItem item, int amount, int[] available, int[] outstanding,
        int[] dueToday, out int spareFood)
    {
        int index = (int)item;
        int shortage = Mathf.Max(0, outstanding[index] - available[index]);
        int urgent = Mathf.Min(amount, Mathf.Max(0, dueToday[index] - available[index]));
        spareFood = item == SaleItem.Food || Array.IndexOf(Ingredients, item) >= 0
            ? Mathf.Max(0, amount - shortage) : 0;
        return 12 * urgent + 3 * Mathf.Min(shortage, amount);
    }

    private bool TryCook(int[] available, int[] outstanding, int[] dueToday, int meals,
        int[] remainingCards, int alternativeScore)
    {
        int food = (int)SaleItem.Food;
        int shortage = Mathf.Max(0, outstanding[food] - available[food]);
        if (shortage == 0) return false;
        bool plus = remainingCards[(int)ActivityKind.FoodPlus] > 0;
        ActivityKind card = plus ? ActivityKind.FoodPlus : ActivityKind.PrepareFood;
        if (remainingCards[(int)card] == 0) return false;
        int bestScore = alternativeScore;
        int bestOutput = 0;
        int first = 0, second = 0, units = 0;
        int spareBefore = FoodSurplus(available, outstanding);
        foreach (SaleItem a in Ingredients)
        {
            int index = (int)a;
            foreach (SaleItem b in Ingredients)
            {
                int other = (int)b;
                for (int total = 2; total <= 3; total++)
                {
                    int fromFirst = total - 1;
                    if (available[index] - outstanding[index] < fromFirst + (index == other ? 1 : 0) ||
                        available[other] - outstanding[other] < 1) continue;
                    int output = InventoryTrading.RecipeYield(total, a != b, plus, balance);
                    int spareAfter = spareBefore - total + Mathf.Max(0, output - shortage);
                    if (spareAfter < meals && spareAfter < spareBefore) continue;
                    int score = GainScore(SaleItem.Food, output, available, outstanding, dueToday, out _);
                    if (score < bestScore || score == bestScore && (units == 0 || total >= units)) continue;
                    bestScore = score;
                    bestOutput = output;
                    first = index;
                    second = other;
                    units = total;
                }
            }
        }
        if (bestOutput == 0) return false;
        available[first] -= units - 1;
        available[second]--;
        available[food] += bestOutput;
        remainingCards[(int)card]--;
        return true;
    }

    private List<ActionEstimate> Options(int date, HashSet<Plot> harvested,
        Dictionary<Plot, int> extraGrowth, int milk, int eggs, bool preferHighYield)
    {
        int wood = (balance != null ? balance.woodReward : 1) + (preferHighYield ? 2 : 1);
        var options = new List<ActionEstimate>
        {
            new ActionEstimate { Card = ActivityKind.CollectWood, Item = SaleItem.Wood, Amount = Mathf.Max(0, wood) },
            new ActionEstimate { Card = ActivityKind.WoodPlus, Item = SaleItem.Wood, Amount = Mathf.Max(0, wood + 1) },
            new ActionEstimate { Card = ActivityKind.Fish, Item = SaleItem.Fish, Amount = 2 },
            new ActionEstimate { Card = ActivityKind.FishPlus, Item = SaleItem.Fish, Amount = preferHighYield ? 4 : 2 },
            new ActionEstimate { Card = ActivityKind.FeedAnimals, Item = SaleItem.Milk, Amount = milk, Eggs = eggs }
        };
        if (cards[(int)ActivityKind.Harvest] == 0 || seeds == null) return options;

        var visited = new HashSet<Plot>(harvested);
        MandalaSynergy mandala = MandalaSynergy.Instance;
        foreach (Plot plot in plots)
        {
            if (plot == null || !Ready(plot, date, extraGrowth) || !visited.Add(plot)) continue;
            List<Plot> batch = HarvestBatch(plot, date, harvested, extraGrowth, visited);
            int amount = 0;
            foreach (Plot member in batch)
            {
                int yield = member.HarvestYield(date, seeds);
                if (mandala != null)
                {
                    yield -= mandala.ExtraCropYield(member);
                    if (member.Crop != CropType.Corn && HasReadyCorn(member, date, harvested))
                        yield += balance != null ? balance.cropYieldNearCorn : 2;
                }
                int pestLoss = events != null ? events.HarvestReductionFor(member) : 0;
                amount += Mathf.Max(0, yield - pestLoss);
            }

            SaleItem item = plot.Crop == CropType.Corn ? SaleItem.Corn :
                plot.Crop == CropType.Pumpkin ? SaleItem.Pumpkin : SaleItem.Jerimum;
            options.Add(new ActionEstimate { Card = ActivityKind.Harvest, Item = item, Amount = amount, Plots = batch });
        }
        return options;
    }

    private List<Plot> HarvestBatch(Plot first, int date, HashSet<Plot> harvested,
        Dictionary<Plot, int> extraGrowth, HashSet<Plot> visited)
    {
        var batch = new List<Plot> { first };
        if (first.JoinedPlots == null) return batch;

        // A plot removed in a previous harvest can split the original group.
        var connected = new List<Plot> { first };
        var seen = new HashSet<Plot> { first };
        for (int i = 0; i < connected.Count; i++)
            foreach (Plot member in first.JoinedPlots)
            {
                if (member == null || harvested.Contains(member) || seen.Contains(member) ||
                    !connected[i].IsAdjacentTo(member) || !member.IsAdjacentTo(connected[i])) continue;
                seen.Add(member);
                connected.Add(member);
                if (Ready(member, date, extraGrowth) && visited.Add(member)) batch.Add(member);
            }
        return batch;
    }

    private bool HasReadyCorn(Plot crop, int date, HashSet<Plot> harvested)
    {
        foreach (Plot neighbor in plots)
            if (neighbor != null && neighbor.Crop == CropType.Corn && !harvested.Contains(neighbor) &&
                neighbor.IsReady(date, seeds) && crop.IsAdjacentTo(neighbor)) return true;
        return false;
    }

    private bool Ready(Plot plot, int date, Dictionary<Plot, int> extraGrowth) =>
        plot.IsReady(date + (extraGrowth.TryGetValue(plot, out int growth) ? growth : 0), seeds);

    private void ProjectGrowth(int date, HashSet<Plot> harvested, Dictionary<Plot, int> extraGrowth)
    {
        if (MandalaSynergy.Instance == null || seeds == null) return;
        int bonus = balance != null ? balance.cropGrowthNearCorn : 1;
        if (bonus <= 0) return;
        foreach (Plot plot in plots)
        {
            if (plot == null || (plot.Crop != CropType.Pumpkin && plot.Crop != CropType.Jerimum) ||
                harvested.Contains(plot) || !HasReadyCorn(plot, date, harvested)) continue;
            extraGrowth.TryGetValue(plot, out int growth);
            CropSettings settings = seeds.Get(plot.Crop);
            if (settings == null) continue;
            int remaining = settings.growthDays - (plot.GrowthAge(date) + growth);
            if (remaining > 0) extraGrowth[plot] = growth + Mathf.Min(remaining, bonus);
        }
    }

    private void ProjectAnimals(int date, HashSet<Plot> harvested, out int milk, out int eggs)
    {
        milk = eggs = 0;
        int bonus = MandalaSynergy.Instance == null ? 0 : balance != null ? balance.animalProductionNearCorn : 1;
        CropSettings corn = seeds != null ? seeds.Get(CropType.Corn) : null;
        foreach (Plot plot in plots)
        {
            if (plot == null || plot.Animal == AnimalKind.None) continue;
            int extra = 0;
            if (bonus > 0)
                foreach (Plot neighbor in plots)
                    if (neighbor != null && neighbor.Crop == CropType.Corn && !harvested.Contains(neighbor) &&
                        plot.IsAdjacentTo(neighbor) && (corn == null || neighbor.GrowthAge(date - 1) <
                        corn.growthDays + corn.witherDaysAfterMaturity))
                    {
                        extra = bonus;
                        break;
                    }
            if (plot.Animal == AnimalKind.Cow) milk += milkPerCow + extra;
            else eggs += eggsPerChicken + extra;
        }
    }
}
