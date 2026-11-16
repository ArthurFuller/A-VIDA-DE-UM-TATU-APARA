using UnityEngine;

// Os valores deste arquivo são salvos em Assets/Resources/GameBalance.asset.
// Componentes sem o arquivo continuam usando os valores da cena.
public sealed class GameBalance : ScriptableObject
{
    private static GameBalance current;
    public static GameBalance Current => current != null ? current :
        (current = Resources.Load<GameBalance>("GameBalance"));

    [Header("Recursos iniciais")]
    [Min(0)] public int startingWood = 99, startingFood = 99;
    public int startingCoins = 99;
    [Min(0)] public int startingFish = 99, startingCorn = 99, startingPumpkin = 99;
    [Min(0)] public int startingJerimum = 99, startingMilk = 99, startingEggs = 99;
    [Min(0)] public int startingCornSeeds, startingPumpkinSeeds, startingJerimumSeeds;

    [Header("Plantações")]
    public CropSettings corn = new CropSettings { type = CropType.Corn, displayName = "Milho", coinCost = 2, seedsPerPurchase = 1, growthDays = 1, foodYield = 2 };
    public CropSettings pumpkin = new CropSettings { type = CropType.Pumpkin, displayName = "Macaxeira", coinCost = 2, seedsPerPurchase = 1, growthDays = 1, foodYield = 7 };
    public CropSettings jerimum = new CropSettings { type = CropType.Jerimum, displayName = "Jerimum", coinCost = 4, seedsPerPurchase = 1, growthDays = 3, foodYield = 5 };

    [Header("Venda por unidade")]
    [Min(0)] public int woodPrice = 1, fishPrice = 2, cornPrice = 4, pumpkinPrice = 1;
    [Min(0)] public int jerimumPrice = 3, foodPrice = 1, milkPrice = 2, eggPrice = 2;
    [Min(0)] public int cornSeedPrice = 1, pumpkinSeedPrice = 2, jerimumSeedPrice = 2;

    [Header("Produção e coleta")]
    [Min(1)] public int totalDays = 12, milkPerCow = 1, eggsPerChicken = 1;
    [Min(0)] public int woodReward = 2;
    [Min(1)] public int gambleStake = 10;

    [Header("Mandala")]
    [Min(0)] public int cropGrowthNearCorn = 1, cropYieldNearCorn = 2;
    [Min(0)] public int animalProductionNearCorn = 1, cropSalePriceNearWaterCow = 2;

    [Header("Mercearia")]
    [Min(0)] public int fenceWoodCost = 4, fenceCoinCost = 8;
    [Min(1)] public int foodCoinCost = 1;
    public int[] marketDays = { 1, 3, 5, 7, 9, 11 };

    [Header("Comunidade")]
    [Min(1)] public int deliveriesPerHouse = 2;
    [Min(1)] public int secondHouseDeliveries = 3;
    [Min(1)] public int thirdHouseDeliveries = 4;
    [Range(1, 3)] public int houseRewardLimit = 3;
    [Range(1, 5)] public int minimumOffers = 2, maximumOffers = 3;
    [Min(1)] public int campaignDays = 12;
    [Range(0, 3)] public int returningOffersPerMarket = 1;

    [Header("Eventos")]
    [Range(0f, 1f)] public float generalEventChance = 0.35f, comadreChance = 0.5f;
    [Min(1)] public int droughtDurationDays = 2, pestDurationDays = 2, rainDurationDays = 1;
    [Min(0)] public int droughtFishingReduction = 1, pestHarvestReduction = 1;
    [Min(0)] public int pestGrowthRegression = 2, droughtGrowthDelay = 1, rainGrowthBonus = 1;
    [Min(1)] public int forestExploitationThreshold = 6, comadreAbsenceDays = 1;
    [Min(0)] public int rainForestRecovery = 2;
    [Min(1)] public int headOfCuiaFishTheft = 2;
    [Min(1)] public int daysWithoutWoodForReforestation = 3;
    [Min(0)] public int reforestationWood = 4;

    private void OnEnable() => current = this;
    private void OnDisable() { if (current == this) current = null; }
}
