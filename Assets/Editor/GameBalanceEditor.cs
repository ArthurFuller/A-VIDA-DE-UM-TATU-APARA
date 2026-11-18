using UnityEditor;
using UnityEngine;

public sealed class GameBalanceEditor : EditorWindow
{
    private const string AssetPath = "Assets/Resources/GameBalance.asset";
    private GameBalance balance;
    private Vector2 scroll;

    [MenuItem("Tools/Editar balanceamento")]
    private static void Open() => GetWindow<GameBalanceEditor>("Balanceamento");

    private void OnEnable()
    {
        minSize = new Vector2(360f, 300f);
        balance = AssetDatabase.LoadAssetAtPath<GameBalance>(AssetPath);
        if (balance == null)
        {
            balance = CreateInstance<GameBalance>();
            AssetDatabase.CreateAsset(balance, AssetPath);
            AssetDatabase.SaveAssets();
        }
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

    private void OnGUI()
    {
        if (balance == null) { OnEnable(); if (balance == null) return; }
        EditorGUILayout.LabelField("Balanceamento do jogo", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Este arquivo é usado no jogo e nas builds. Alterações feitas aqui ficam salvas também durante o Play Mode; reinicie o teste para aplicar todos os valores.", MessageType.Info);

        var data = new SerializedObject(balance);
        data.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Section(data, "Recursos iniciais", "startingWood", "startingFood", "startingCoins", "startingFish",
            "startingCorn", "startingPumpkin", "startingJerimum", "startingMilk", "startingEggs",
            "startingCornSeeds", "startingPumpkinSeeds", "startingJerimumSeeds");
        Crop(data, "Milho", "corn");
        Crop(data, "Macaxeira", "pumpkin");
        Crop(data, "Jerimum", "jerimum");
        Section(data, "Venda por unidade", "woodPrice", "fishPrice", "cornPrice", "pumpkinPrice",
            "jerimumPrice", "foodPrice", "milkPrice", "eggPrice", "cornSeedPrice",
            "pumpkinSeedPrice", "jerimumSeedPrice");
        Section(data, "Produção e coleta", "totalDays", "milkPerCow", "eggsPerChicken",
            "woodReward", "fertilizerHarvestBonus", "maxPreparedFoodPerAction", "foodPlusBonus", "gambleStake");
        Section(data, "Mandala", "cropGrowthNearCorn", "cropYieldNearCorn",
            "animalProductionNearCorn", "cropSalePriceNearWaterCow");
        Section(data, "Mercearia", "fenceWoodCost", "fenceCoinCost", "foodCoinCost", "marketDays");
        Section(data, "Comunidade", "deliveriesPerHouse", "secondHouseDeliveries",
            "thirdHouseDeliveries", "houseRewardLimit", "minimumOffers",
            "maximumOffers", "campaignDays", "returningOffersPerMarket", "dailyMissionOffers");
        Section(data, "Eventos", "generalEventChance", "droughtDurationDays", "pestDurationDays",
            "rainDurationDays", "droughtFishingReduction", "pestHarvestReduction",
            "pestGrowthRegression", "droughtGrowthDelay", "rainGrowthBonus",
            "forestExploitationThreshold", "comadreChance", "comadreAbsenceDays",
            "rainForestRecovery", "headOfCuiaMinimumFish", "headOfCuiaChancePercent",
            "headOfCuiaTheftPercent", "daysWithoutWoodForReforestation",
            "reforestationWood");
        EditorGUILayout.HelpBox("Os textos das cartas de evento continuam em Tools > Cartas > Editar descrições.", MessageType.None);
        EditorGUILayout.EndScrollView();

        if (data.ApplyModifiedProperties()) AssetDatabase.SaveAssets();
    }

    private static void Section(SerializedObject data, string title, params string[] names)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (string name in names)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property != null) DrawValue(property, name);
        }
    }

    private static void Crop(SerializedObject data, string title, string name)
    {
        SerializedProperty crop = data.FindProperty(name);
        if (crop == null) return;
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (string field in new[] { "growthDays", "foodYield",
            "bonusYieldPerExtraDay", "witherDaysAfterMaturity" })
            DrawValue(crop.FindPropertyRelative(field), field);
    }

    private static void DrawValue(SerializedProperty property, string name)
    {
        var label = new GUIContent(Label(name), Tooltip(name));
        EditorGUILayout.Space(2);
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            EditorGUILayout.LabelField(label, EditorStyles.wordWrappedLabel);
            EditorGUILayout.PropertyField(property, new GUIContent("Dias", label.tooltip), true);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, EditorStyles.wordWrappedLabel, GUILayout.MinWidth(110), GUILayout.ExpandWidth(true));
        if (property.propertyType == SerializedPropertyType.Boolean)
            property.boolValue = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(85));
        else if (property.propertyType == SerializedPropertyType.Float)
            property.floatValue = Mathf.Clamp01(EditorGUILayout.FloatField(property.floatValue, GUILayout.Width(85)));
        else if (property.propertyType == SerializedPropertyType.Integer)
            property.intValue = Mathf.Clamp(EditorGUILayout.IntField(property.intValue, GUILayout.Width(85)),
                Minimum(name), Maximum(name));
        EditorGUILayout.EndHorizontal();
    }

    private static int Minimum(string name) => name switch
    {
        "startingCoins" => int.MinValue,
        "growthDays" or "witherDaysAfterMaturity" or "totalDays" or
        "milkPerCow" or "eggsPerChicken" or "gambleStake" or "foodCoinCost" or "maxPreparedFoodPerAction" or
        "deliveriesPerHouse" or "secondHouseDeliveries" or "thirdHouseDeliveries" or
        "houseRewardLimit" or "minimumOffers" or "maximumOffers" or "campaignDays" or
        "droughtDurationDays" or "pestDurationDays" or "rainDurationDays" or
        "forestExploitationThreshold" or "comadreAbsenceDays" or
        "headOfCuiaMinimumFish" or "headOfCuiaTheftPercent" or "daysWithoutWoodForReforestation" => 1,
        _ => 0
    };

    private static int Maximum(string name) => name switch
    {
        "maxPreparedFoodPerAction" => 2,
        "houseRewardLimit" or "returningOffersPerMarket" => 3,
        "minimumOffers" or "maximumOffers" => 5,
        "headOfCuiaChancePercent" or "headOfCuiaTheftPercent" => 100,
        _ => int.MaxValue
    };

    private static string Tooltip(string name) => name switch
    {
        "startingWood" or "startingFood" or "startingCoins" or "startingFish" or
        "startingCorn" or "startingPumpkin" or "startingJerimum" or "startingMilk" or
        "startingEggs" or "startingCornSeeds" or "startingPumpkinSeeds" or
        "startingJerimumSeeds" => "Quantidade que o jogador possui ao começar uma nova partida.",
        "growthDays" => "Dias de crescimento necessários para esta planta ficar pronta para colheita.",
        "foodYield" => "Unidades recebidas na colheita antes dos bônus de adubo, mandala e dias extras.",
        "bonusYieldPerExtraDay" => "Unidades adicionais por dia que a planta pronta aguarda antes da colheita, até estragar.",
        "witherDaysAfterMaturity" => "Quantos dias após ficar pronta a planta ainda pode ser colhida antes de estragar.",
        "woodPrice" or "fishPrice" or "foodPrice" or "milkPrice" or "eggPrice" or
        "cornSeedPrice" or "pumpkinSeedPrice" or "jerimumSeedPrice" =>
            "Moedas recebidas ao vender uma unidade deste item na mercearia.",
        "cornPrice" or "pumpkinPrice" or "jerimumPrice" =>
            "Preço base de venda por unidade. O bônus da vaca junto ao poço pode aumentar esse valor na loja.",
        "totalDays" => "Último dia da partida. Ao finalizar esse dia, o jogo encerra.",
        "milkPerCow" => "Leite produzido por vaca a cada dia, antes do bônus do milho.",
        "eggsPerChicken" => "Ovos produzidos por galinha a cada dia, antes do bônus do milho.",
        "woodReward" => "Madeira base da coleta normal. A precisão do minigame, a carta Coleta de madeira + e eventuais penalidades ajustam o total.",
        "fertilizerHarvestBonus" => "Unidades extras recebidas ao colher um terreno adubado.",
        "maxPreparedFoodPerAction" => "Limite de porções por preparo comum.",
        "foodPlusBonus" => "Porções adicionais com a carta Comida+.",
        "dailyMissionOffers" => "Abre a aba de encomendas todos os dias. Comprar e vender seguem o calendário da mercearia.",
        "gambleStake" => "Moedas ganhas ao vencer ou perdidas ao perder no Jogo do Bicho.",
        "cropGrowthNearCorn" => "Etapas de crescimento antecipadas por dia para macaxeira ou jerimum ao lado de milho já pronto, até a planta ficar madura.",
        "cropYieldNearCorn" => "Unidades extras por colheita de macaxeira ou jerimum ao lado de milho pronto.",
        "animalProductionNearCorn" => "Leite ou ovo adicional produzido por dia pela vaca ou galinha ao lado de milho.",
        "cropSalePriceNearWaterCow" => "Moedas extras por unidade vendida de cada tipo de planta que esteja ao lado de uma vaca adjacente ao poço. Vale para todo o estoque daquele tipo enquanto a ligação existir.",
        "fenceWoodCost" => "Madeira gasta para comprar uma carta de cerca na mercearia.",
        "fenceCoinCost" => "Moedas gastas para comprar uma carta de cerca na mercearia.",
        "foodCoinCost" => "Moedas gastas para comprar uma unidade de comida pronta na mercearia.",
        "marketDays" => "Dias em que Comprar e Vender ficam disponíveis. O comércio também abre no dia 12.",
        "deliveriesPerHouse" => "Entregas concluídas para receber a primeira carta de casa.",
        "secondHouseDeliveries" => "Entregas adicionais depois da primeira casa para receber a segunda carta de casa.",
        "thirdHouseDeliveries" => "Entregas adicionais depois da segunda casa para receber a terceira carta de casa.",
        "houseRewardLimit" => "Número máximo de cartas de casa liberadas pela comunidade.",
        "minimumOffers" => "Menor número de encomendas oferecidas quando a mercearia gera novos pedidos.",
        "maximumOffers" => "Maior número de encomendas oferecidas quando a mercearia gera novos pedidos.",
        "campaignDays" => "Limite do dia de vencimento das encomendas da comunidade.",
        "returningOffersPerMarket" => "Máximo de encomendas recusadas que entram por dia depois de esgotar as opções inéditas.",
        "generalEventChance" => "Probabilidade diária de sortear um evento geral, se Comadre e Reflorestamento não forem anunciados antes.",
        "droughtDurationDays" => "Dias em que a seca permanece ativa, contando o dia em que aparece.",
        "pestDurationDays" => "Dias em que as pragas permanecem ativas, contando o dia em que aparecem.",
        "rainDurationDays" => "Dias em que a chuva permanece ativa, contando o dia em que aparece.",
        "droughtFishingReduction" => "Redução da recompensa da pesca durante a seca. 1 divide o total por 2, com mínimo de 1 peixe.",
        "pestHarvestReduction" => "Unidades subtraídas da colheita afetada enquanto as pragas estiverem ativas.",
        "pestGrowthRegression" => "Etapas de crescimento que as pragas fazem a plantação afetada perder, inclusive se já estiver pronta.",
        "droughtGrowthDelay" => "Etapas de crescimento perdidas a cada dia de seca pelas plantações sem proteção do poço.",
        "rainGrowthBonus" => "Etapas de crescimento acrescentadas às plantações a cada dia de chuva.",
        "forestExploitationThreshold" => "Madeira coletada acumulada necessária para a Comadre poder aparecer.",
        "comadreChance" => "Probabilidade de a Comadre aparecer quando a exploração da floresta atingir o limite e houver trabalhador elegível.",
        "comadreAbsenceDays" => "Dias de ausência do trabalhador escolhido pela Comadre.",
        "rainForestRecovery" => "Pontos de exploração da floresta removidos a cada dia de chuva.",
        "headOfCuiaMinimumFish" => "Estoque mínimo de peixes no início do dia para o Cabeça de Cuia entrar no sorteio.",
        "headOfCuiaChancePercent" => "Chance fixa dentro do sorteio de eventos gerais, em porcentagem.",
        "headOfCuiaTheftPercent" => "Porcentagem do estoque roubada. Metade exata arredonda para baixo.",
        "daysWithoutWoodForReforestation" => "Dias desde a última coleta de madeira ou reflorestamento necessários para o evento aparecer.",
        "reforestationWood" => "Madeiras recebidas ao fechar a carta do evento de reflorestamento.",
        _ => "Valor usado pelo jogo para este parâmetro."
    };

    private static string Label(string name) => name switch
    {
        "startingWood" => "Madeira", "startingFood" => "Comida pronta",
        "startingCoins" => "Moedas", "startingFish" => "Peixes",
        "startingCorn" => "Milho", "startingPumpkin" => "Macaxeira",
        "startingJerimum" => "Jerimum", "startingMilk" => "Leite",
        "startingEggs" => "Ovos", "startingCornSeeds" => "Sementes de milho",
        "startingPumpkinSeeds" => "Sementes de macaxeira",
        "startingJerimumSeeds" => "Sementes de jerimum",
        "coinCost" => "Preço das sementes", "seedsPerPurchase" => "Sementes por compra",
        "growthDays" => "Dias para crescer", "foodYield" => "Unidades por colheita",
        "bonusYieldPerExtraDay" => "Bônus por dia extra",
        "witherDaysAfterMaturity" => "Dias até estragar depois de pronta",
        "woodPrice" => "Madeira", "fishPrice" => "Peixe", "cornPrice" => "Milho",
        "pumpkinPrice" => "Macaxeira", "jerimumPrice" => "Jerimum",
        "foodPrice" => "Comida pronta", "milkPrice" => "Leite", "eggPrice" => "Ovos",
        "cornSeedPrice" => "Semente de milho", "pumpkinSeedPrice" => "Semente de macaxeira",
        "jerimumSeedPrice" => "Semente de jerimum",
        "totalDays" => "Dias de jogo", "milkPerCow" => "Leite por vaca",
        "eggsPerChicken" => "Ovos por galinha", "woodReward" => "Madeira base por coleta",
        "gambleStake" => "Aposta",
        "fertilizerHarvestBonus" => "Unidades extras com adubo",
        "maxPreparedFoodPerAction" => "Porções por preparo comum",
        "foodPlusBonus" => "Porções extras com Comida+",
        "dailyMissionOffers" => "Ofertas todos os dias",
        "cropGrowthNearCorn" => "Crescimento junto ao milho",
        "cropYieldNearCorn" => "Colheita junto ao milho",
        "animalProductionNearCorn" => "Produção animal junto ao milho",
        "cropSalePriceNearWaterCow" => "Preço com vaca junto ao poço",
        "fenceWoodCost" => "Madeira por cerca", "fenceCoinCost" => "Moedas por cerca",
        "foodCoinCost" => "Moedas por comida", "marketDays" => "Dias da mercearia",
        "deliveriesPerHouse" => "Entregas para a 1ª casa",
        "secondHouseDeliveries" => "Entregas para a 2ª casa",
        "thirdHouseDeliveries" => "Entregas para a 3ª casa",
        "houseRewardLimit" => "Limite de casas",
        "minimumOffers" => "Mínimo de encomendas", "maximumOffers" => "Máximo de encomendas",
        "campaignDays" => "Dias da campanha", "returningOffersPerMarket" => "Encomendas que retornam",
        "generalEventChance" => "Chance de evento geral",
        "droughtDurationDays" => "Duração da seca", "pestDurationDays" => "Duração das pragas",
        "rainDurationDays" => "Duração da chuva", "droughtFishingReduction" => "Redução da pesca na seca",
        "pestHarvestReduction" => "Redução da colheita por pragas",
        "pestGrowthRegression" => "Regressão por pragas",
        "droughtGrowthDelay" => "Atraso do crescimento na seca",
        "rainGrowthBonus" => "Crescimento extra na chuva",
        "forestExploitationThreshold" => "Exploração para Comadre",
        "comadreChance" => "Chance de Comadre", "comadreAbsenceDays" => "Dias de ausência",
        "rainForestRecovery" => "Recuperação da floresta na chuva",
        "headOfCuiaMinimumFish" => "Estoque mínimo de peixes para Cabeça de Cuia",
        "headOfCuiaChancePercent" => "Chance de Cabeça de Cuia no sorteio geral (%)",
        "headOfCuiaTheftPercent" => "Estoque roubado pelo Cabeça de Cuia (%)",
        "daysWithoutWoodForReforestation" => "Dias sem madeira para reflorestamento",
        "reforestationWood" => "Madeira do reflorestamento",
        _ => ObjectNames.NicifyVariableName(name)
    };
}
