using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ResourceBank : MonoBehaviour
{
    [Header("Recursos iniciais")]
    [SerializeField, HideInInspector, Min(0)] private int startingWood;
    [SerializeField, HideInInspector, Min(0)] private int startingFood = 2;
    [SerializeField, HideInInspector] private int startingCoins = 2;
    [SerializeField, HideInInspector, Min(0)] private int startingFish;
    [SerializeField, HideInInspector, Min(0)] private int startingCorn;
    [SerializeField, HideInInspector, Min(0)] private int startingPumpkin;
    [SerializeField, HideInInspector, Min(0)] private int startingJerimum;
    [SerializeField, HideInInspector, Min(0)] private int startingMilk;
    [SerializeField, HideInInspector, Min(0)] private int startingEggs;
    [SerializeField, HideInInspector, Min(0)] private int startingCornSeeds;
    [SerializeField, HideInInspector, Min(0)] private int startingPumpkinSeeds;
    [SerializeField, HideInInspector, Min(0)] private int startingJerimumSeeds;

    public int Wood { get; private set; }
    public int Food { get; private set; }
    public int Coins { get; private set; }
    public int CornSeeds { get; private set; }
    public int PumpkinSeeds { get; private set; }
    public int JerimumSeeds { get; private set; }
    public int Fish { get; private set; }
    public int Corn { get; private set; }
    public int Pumpkin { get; private set; }
    public int Jerimum { get; private set; }
    public int Milk { get; private set; }
    public int Eggs { get; private set; }
    public event Action Changed;

    private void Awake()
    {
        GameBalance balance = GameBalance.Current;
        if (balance != null)
        {
            startingWood = balance.startingWood; startingFood = balance.startingFood;
            startingCoins = balance.startingCoins; startingFish = balance.startingFish;
            startingCorn = balance.startingCorn; startingPumpkin = balance.startingPumpkin;
            startingJerimum = balance.startingJerimum; startingMilk = balance.startingMilk;
            startingEggs = balance.startingEggs; startingCornSeeds = balance.startingCornSeeds;
            startingPumpkinSeeds = balance.startingPumpkinSeeds;
            startingJerimumSeeds = balance.startingJerimumSeeds;
        }
        Wood = startingWood;
        Food = startingFood;
        Coins = startingCoins;
        CornSeeds = startingCornSeeds;
        PumpkinSeeds = startingPumpkinSeeds;
        JerimumSeeds = startingJerimumSeeds;
        Fish = startingFish;
        Corn = startingCorn;
        Pumpkin = startingPumpkin;
        Jerimum = startingJerimum;
        Milk = startingMilk;
        Eggs = startingEggs;
    }

    public void AddWood(int amount)
    {
        Wood += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public void AddFood(int amount)
    {
        amount = Mathf.Max(0, amount);
        Food += amount;
        Changed?.Invoke();
    }

    public void AddFish(int amount) { Fish += Mathf.Max(0, amount); Changed?.Invoke(); }
    public void AddAnimalProducts(int milk, int eggs)
    {
        Milk += Mathf.Max(0, milk);
        Eggs += Mathf.Max(0, eggs);
        Changed?.Invoke();
    }

    public bool TryDeliverAnimalProduct(SaleItem item)
    {
        if (item == SaleItem.Milk && Milk > 0) Milk--;
        else if (item == SaleItem.Eggs && Eggs > 0) Eggs--;
        else return false;
        Changed?.Invoke();
        return true;
    }
    public void AddCrop(CropType crop, int amount)
    {
        if (crop == CropType.Corn) Corn += Mathf.Max(0, amount);
        else if (crop == CropType.Pumpkin) Pumpkin += Mathf.Max(0, amount);
        else if (crop == CropType.Jerimum) Jerimum += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TryPrepareFood(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs,
        int recipeYield, out int preparedFood)
    {
        preparedFood = 0;
        if (fish < 0 || corn < 0 || pumpkin < 0 || jerimum < 0 || milk < 0 || eggs < 0 ||
            fish > Fish || corn > Corn || pumpkin > Pumpkin || jerimum > Jerimum ||
            milk > Milk || eggs > Eggs ||
            (long)fish + corn + pumpkin + jerimum + milk + eggs == 0 || recipeYield <= 0 ||
            recipeYield > int.MaxValue - Food) return false;
        Fish -= fish;
        Corn -= corn;
        Pumpkin -= pumpkin;
        Jerimum -= jerimum;
        Milk -= milk;
        Eggs -= eggs;
        preparedFood = recipeYield;
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverFish(int amount) => TryRemove(SaleItem.Fish, amount);

    public int Stock(SaleItem item) => StockForSale(item);

    public bool CanDeliverMission(IReadOnlyList<DeliveryRequirement> requirements) =>
        MissionAmounts(requirements, out _);

    private bool MissionAmounts(IReadOnlyList<DeliveryRequirement> requirements, out int[] amounts)
    {
        amounts = new int[(int)SaleItem.Eggs + 1];
        if (requirements == null || requirements.Count == 0) return false;
        foreach (DeliveryRequirement requirement in requirements)
        {
            if (requirement == null || requirement.quantity <= 0 ||
                (int)requirement.item < 0 || (int)requirement.item >= amounts.Length ||
                requirement.quantity > int.MaxValue - amounts[(int)requirement.item]) return false;
            amounts[(int)requirement.item] += requirement.quantity;
        }
        for (int i = 0; i < amounts.Length; i++)
            if (amounts[i] > StockForSale((SaleItem)i)) return false;
        return true;
    }

    public bool TryDeliverMission(IReadOnlyList<DeliveryRequirement> requirements)
    {
        if (!MissionAmounts(requirements, out int[] amounts)) return false;
        Wood -= amounts[(int)SaleItem.Wood];
        Fish -= amounts[(int)SaleItem.Fish];
        Corn -= amounts[(int)SaleItem.Corn];
        Pumpkin -= amounts[(int)SaleItem.Pumpkin];
        RemoveFood(amounts[(int)SaleItem.Food]);
        CornSeeds -= amounts[(int)SaleItem.CornSeeds];
        PumpkinSeeds -= amounts[(int)SaleItem.PumpkinSeeds];
        Jerimum -= amounts[(int)SaleItem.Jerimum];
        JerimumSeeds -= amounts[(int)SaleItem.JerimumSeeds];
        Milk -= amounts[(int)SaleItem.Milk];
        Eggs -= amounts[(int)SaleItem.Eggs];
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverCrop(CropType crop, int amount) => crop switch
    {
        CropType.Corn => TryRemove(SaleItem.Corn, amount),
        CropType.Pumpkin => TryRemove(SaleItem.Pumpkin, amount),
        CropType.Jerimum => TryRemove(SaleItem.Jerimum, amount),
        _ => false
    };

    public bool TrySellBatch(int[] quantities, int[] prices)
    {
        int itemCount = (int)SaleItem.Eggs + 1;
        if (quantities == null || prices == null || quantities.Length != itemCount ||
            prices.Length != itemCount) return false;
        if (quantities[(int)SaleItem.CornSeeds] != 0 ||
            quantities[(int)SaleItem.PumpkinSeeds] != 0 ||
            quantities[(int)SaleItem.JerimumSeeds] != 0) return false;
        long total = 0;
        long coinRoom = int.MaxValue - (long)Coins;
        int selected = 0;
        for (int i = 0; i < quantities.Length; i++)
        {
            int quantity = quantities[i];
            if (quantity < 0 || prices[i] < 0 || quantity > StockForSale((SaleItem)i)) return false;
            if (quantity == 0) continue;
            selected++;
            long earnings = (long)quantity * prices[i];
            if (earnings > coinRoom - total) return false;
            total += earnings;
        }
        if (selected == 0) return false;
        Wood -= quantities[(int)SaleItem.Wood];
        Fish -= quantities[(int)SaleItem.Fish];
        Corn -= quantities[(int)SaleItem.Corn];
        Pumpkin -= quantities[(int)SaleItem.Pumpkin];
        RemoveFood(quantities[(int)SaleItem.Food]);
        Jerimum -= quantities[(int)SaleItem.Jerimum];
        Milk -= quantities[(int)SaleItem.Milk];
        Eggs -= quantities[(int)SaleItem.Eggs];
        Coins = (int)((long)Coins + total);
        Changed?.Invoke();
        return true;
    }

    private int StockForSale(SaleItem item) => item switch
    {
        SaleItem.Wood => Wood,
        SaleItem.Fish => Fish,
        SaleItem.Corn => Corn,
        SaleItem.Pumpkin => Pumpkin,
        SaleItem.Food => Food,
        SaleItem.CornSeeds => CornSeeds,
        SaleItem.PumpkinSeeds => PumpkinSeeds,
        SaleItem.Jerimum => Jerimum,
        SaleItem.JerimumSeeds => JerimumSeeds,
        SaleItem.Milk => Milk,
        SaleItem.Eggs => Eggs,
        _ => 0
    };

    public int ConsumeFood(int amount)
    {
        int consumed = Mathf.Min(Food, Mathf.Max(0, amount));
        RemoveFood(consumed);
        if (consumed > 0) Changed?.Invoke();
        return consumed;
    }

    private void RemoveFood(int amount)
    {
        Food -= amount;
    }

    public void AddCoins(int amount)
    {
        int gain = Mathf.Max(0, amount);
        Coins = (int)System.Math.Min(int.MaxValue, (long)Coins + gain);
        Changed?.Invoke();
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || (long)Coins - amount < int.MinValue) return false;
        Coins -= amount;
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverWood(int amount) => TryRemove(SaleItem.Wood, amount);
    public bool TryDeliverFood(int amount) => TryRemove(SaleItem.Food, amount);
    public bool TrySpendWood(int amount) => TryRemove(SaleItem.Wood, amount);

    public void AddSeeds(CropType type, int amount)
    {
        if (type == CropType.Corn) CornSeeds += Mathf.Max(0, amount);
        if (type == CropType.Pumpkin) PumpkinSeeds += Mathf.Max(0, amount);
        if (type == CropType.Jerimum) JerimumSeeds += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TrySpendSeed(CropType type) => TryDeliverSeeds(type, 1);

    public bool TryDeliverSeeds(CropType type, int amount) => type switch
    {
        CropType.Corn => TryRemove(SaleItem.CornSeeds, amount),
        CropType.Pumpkin => TryRemove(SaleItem.PumpkinSeeds, amount),
        CropType.Jerimum => TryRemove(SaleItem.JerimumSeeds, amount),
        _ => false
    };

    private bool TryRemove(SaleItem item, int amount)
    {
        if (amount < 0 || StockForSale(item) < amount) return false;
        switch (item)
        {
            case SaleItem.Wood: Wood -= amount; break;
            case SaleItem.Fish: Fish -= amount; break;
            case SaleItem.Corn: Corn -= amount; break;
            case SaleItem.Pumpkin: Pumpkin -= amount; break;
            case SaleItem.Food: RemoveFood(amount); break;
            case SaleItem.CornSeeds: CornSeeds -= amount; break;
            case SaleItem.PumpkinSeeds: PumpkinSeeds -= amount; break;
            case SaleItem.Jerimum: Jerimum -= amount; break;
            case SaleItem.JerimumSeeds: JerimumSeeds -= amount; break;
            default: return false;
        }
        Changed?.Invoke();
        return true;
    }

    public int Seeds(CropType type)
    {
        return type == CropType.Corn ? CornSeeds : type == CropType.Pumpkin ? PumpkinSeeds :
            type == CropType.Jerimum ? JerimumSeeds : 0;
    }
}
