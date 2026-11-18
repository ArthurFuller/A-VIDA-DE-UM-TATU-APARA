using UnityEngine;

// Os valores deste arquivo são salvos em Assets/Resources/GameBalance.asset.
// Componentes sem o arquivo continuam usando os valores da cena.
public sealed class GameBalance : ScriptableObject
{
    private static GameBalance current;
    public static GameBalance Current => current != null ? current :
        (current = Resources.Load<GameBalance>("GameBalance"));

    [Header("Recursos iniciais")]
    [Min(0)] public int startingWood, startingFood = 2;
    public int startingCoins = 4;
    [Min(0)] public int startingFish, startingCorn, startingPumpkin;
    [Min(0)] public int startingJerimum, startingMilk, startingEggs;
    [Min(0)] public int startingCornSeeds, startingPumpkinSeeds, startingJerimumSeeds;

    [Header("Plantações")]
    public CropSettings corn = new CropSettings { type = CropType.Corn, displayName = "Milho", coinCost = 2, seedsPerPurchase = 1, growthDays = 1, foodYield = 2, bonusYieldPerExtraDay = 0 };
    public CropSettings pumpkin = new CropSettings { type = CropType.Pumpkin, displayName = "Macaxeira", coinCost = 2, seedsPerPurchase = 1, growthDays = 2, foodYield = 3, bonusYieldPerExtraDay = 0 };
    public CropSettings jerimum = new CropSettings { type = CropType.Jerimum, displayName = "Jerimum", coinCost = 4, seedsPerPurchase = 1, growthDays = 3, foodYield = 4 };

    [Header("Venda por unidade")]
    [Min(0)] public int woodPrice = 2, fishPrice = 2, cornPrice = 3, pumpkinPrice = 2;
    [Min(0)] public int jerimumPrice = 3, foodPrice = 3, milkPrice = 3, eggPrice = 3;
    [Min(0)] public int cornSeedPrice = 1, pumpkinSeedPrice = 2, jerimumSeedPrice = 2;

    [Header("Produção e coleta")]
    [Min(1)] public int totalDays = 12, milkPerCow = 1, eggsPerChicken = 1;
    [Min(0)] public int woodReward = 1;
    [Min(0)] public int fertilizerHarvestBonus = 1;
    [Range(1, 2)] public int maxPreparedFoodPerAction = 2;
    [Min(0)] public int foodPlusBonus = 1;
    [Min(1)] public int gambleStake = 10;

    [Header("Mandala")]
    [Min(0)] public int cropGrowthNearCorn = 1, cropYieldNearCorn = 1;
    [Min(0)] public int animalProductionNearCorn = 1, cropSalePriceNearWaterCow = 2;

    [Header("Mercearia")]
    [Min(0)] public int fenceWoodCost = 3, fenceCoinCost = 5;
    [Min(1)] public int foodCoinCost = 4;
    public int[] marketDays = { 1, 3, 5, 7, 9, 10, 12 };

    [Header("Comunidade")]
    [Min(1)] public int deliveriesPerHouse = 2;
    [Min(1)] public int secondHouseDeliveries = 3;
    [Min(1)] public int thirdHouseDeliveries = 4;
    [Range(1, 3)] public int houseRewardLimit = 3;
    [Range(1, 5)] public int minimumOffers = 2, maximumOffers = 2;
    [Min(1)] public int campaignDays = 12;
    [Range(0, 3)] public int returningOffersPerMarket = 1;
    public bool dailyMissionOffers = true;

    [Header("Resultado da partida")]
    [Min(0)] public int missedMealPenalty = 2;
    [Min(0)] public int undeliveredOrderPenalty = 3;
    [Min(0)] public int undevelopedPlotPenalty = 5;
    [Min(0)] public int unbuiltHousePenalty = 7;

    [Header("Eventos")]
    [Range(0f, 1f)] public float generalEventChance = 0.35f, comadreChance = 0.5f;
    [Min(1)] public int droughtDurationDays = 2, pestDurationDays = 2, rainDurationDays = 1;
    [Min(0)] public int droughtFishingReduction = 1, pestHarvestReduction = 1;
    [Min(0)] public int pestGrowthRegression = 2, droughtGrowthDelay = 1, rainGrowthBonus = 1;
    [Min(1)] public int forestExploitationThreshold = 6, comadreAbsenceDays = 1;
    [Min(0)] public int rainForestRecovery = 2;
    [Min(1)] public int headOfCuiaMinimumFish = 8;
    [Range(0, 100)] public int headOfCuiaChancePercent = 25;
    [Range(1, 100)] public int headOfCuiaTheftPercent = 20;
    [Min(1)] public int daysWithoutWoodForReforestation = 3;
    [Min(0)] public int reforestationWood = 4;

    private void OnEnable() => current = this;
    private void OnDisable() { if (current == this) current = null; }
}
