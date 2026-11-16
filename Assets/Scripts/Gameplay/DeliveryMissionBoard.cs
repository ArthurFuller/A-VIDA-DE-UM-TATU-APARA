using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class DeliveryMission
{
    public string title;
    public string description;
    public List<DeliveryRequirement> requirements = new();
    public int dueDay;
    public int coinReward;
    public bool accepted;
    [NonSerialized] public DeliveryMissionTemplate template;
}

public sealed class DeliveryMissionBoard : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private DeliveryOrderCardView orderCard;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button deliverButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private ActiveMissionsPanel activePanel;
    [SerializeField] private ShopTabs shopTabs;
    [Header("Comunidade")]
    [SerializeField] private ActivityCardAvailability hand;
    [SerializeField, HideInInspector, Min(1)] private int deliveriesPerHouse = 2;
    [SerializeField, HideInInspector, Min(1)] private int secondHouseDeliveries = 3;
    [SerializeField, HideInInspector, Min(1)] private int thirdHouseDeliveries = 4;
    [SerializeField, HideInInspector, Range(1, 3)] private int houseRewardLimit = 3;
    [SerializeField, HideInInspector, Range(1, 5)] private int minimumOffers = 2;
    [SerializeField, HideInInspector, Range(1, 5)] private int maximumOffers = 3;
    [SerializeField, HideInInspector, Min(1)] private int campaignDays = 12;
    [SerializeField, HideInInspector, Range(0, 3)] private int returningOffersPerMarket = 1;

    private readonly List<DeliveryMission> missions = new List<DeliveryMission>();
    private readonly HashSet<DeliveryMissionTemplate> refusedTemplates = new();
    private readonly HashSet<DeliveryMissionTemplate> shownTemplates = new();
    private int currentDay = 1;
    private int selectedIndex;
    private int lastGeneratedDay;
    private bool marketOpen;
    private bool delivering;
    private int communityDeliveries;
    private int housesRewarded;

    public bool HasDeliveryDueOn(int day)
    {
        foreach (DeliveryMission mission in missions)
            if (mission.accepted && mission.dueDay == day) return true;
        return false;
    }

    private void OnEnable()
    {
        GameBalance balance = GameBalance.Current;
        if (balance != null)
        {
            deliveriesPerHouse = balance.deliveriesPerHouse;
            secondHouseDeliveries = balance.secondHouseDeliveries;
            thirdHouseDeliveries = balance.thirdHouseDeliveries;
            houseRewardLimit = balance.houseRewardLimit;
            minimumOffers = balance.minimumOffers;
            maximumOffers = balance.maximumOffers;
            campaignDays = Mathf.Min(balance.campaignDays, balance.totalDays);
            returningOffersPerMarket = balance.returningOffersPerMarket;
        }
        if (resources != null) resources.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
    }

    public void SetDay(int day, bool isMarketDay, Func<DeliveryOfferForecast> createForecast)
    {
        marketOpen = isMarketDay;
        if (currentDay != day)
        {
            currentDay = day;
            foreach (DeliveryMission mission in missions)
                if (!mission.accepted) refusedTemplates.Add(mission.template);
            missions.RemoveAll(m => !m.accepted);
            int expired = missions.RemoveAll(m => day > m.dueDay);
            if (expired > 0 && feedback != null)
                feedback.ShowMessage("Pedidos com prazo encerrado foram removidos.");
        }
        if (marketOpen && lastGeneratedDay != day)
        {
            GenerateOffers(createForecast());
            lastGeneratedDay = day;
        }
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, missions.Count - 1));
        Refresh();
    }

    public void Next()
    {
        if (missions.Count == 0) return;
        if (shopTabs != null && shopTabs.AnimateOrderChange(AdvanceOrder)) return;
        AdvanceOrder();
    }

    private void AdvanceOrder()
    {
        if (missions.Count == 0) return;
        selectedIndex = (selectedIndex + 1) % missions.Count;
        Refresh();
    }

    public void Accept()
    {
        if (!marketOpen || missions.Count == 0) return;
        DeliveryMission mission = missions[selectedIndex];
        if (mission.accepted) return;
        mission.accepted = true;
        AudioManager.Play(SoundCue.MissionAccepted);
        if (feedback != null) feedback.ShowMessage("Pedido aceito: " + mission.title);
        Refresh();
    }

    public void Deliver()
    {
        DeliverAt(selectedIndex);
    }

    public void DeliverAt(int index)
    {
        RectTransform origin = deliverButton != null ? deliverButton.transform as RectTransform : null;
        if (DeliverAt(index, origin)) Refresh();
    }

    public bool CanDeliver(DeliveryMission mission) =>
        IsDeliverable(mission) && resources.CanDeliverMission(mission.requirements);

    private bool IsDeliverable(DeliveryMission mission) =>
        resources != null && mission != null && mission.accepted && currentDay <= mission.dueDay;

    public void DeliverFromCard(ActiveMissionCard card, DeliveryMission mission, RectTransform origin)
    {
        if (Time.timeScale <= 0f || delivering || activePanel == null) return;
        int index = missions.IndexOf(mission);
        if (index < 0 || !CanDeliver(mission)) return;
        if (!activePanel.BeginDelivery(card, Refresh, out System.Action onLastDeparture)) return;

        if (DeliverAt(index, origin, onLastDeparture))
            Refresh();
        else
            activePanel.FinishDelivery();
    }

    private bool DeliverAt(int index, RectTransform origin, System.Action onLastDeparture = null)
    {
        if (delivering || index < 0 || index >= missions.Count) return false;
        DeliveryMission mission = missions[index];
        if (!IsDeliverable(mission)) return false;

        delivering = true;
        try
        {
            if (!resources.TryDeliverMission(mission.requirements))
            {
                if (feedback != null) feedback.ShowMessage("Ainda faltam itens para a entrega.");
                return false;
            }

            missions.RemoveAt(index);
            if (index < selectedIndex) selectedIndex--;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, missions.Count - 1));
            resources.AddCoins(mission.coinReward);
            // O HUD captura a posição antes de Refresh reposicionar ou esconder o card.
            if (resourceHud != null)
                resourceHud.FlyFromRect(ResourceKind.Coins, mission.coinReward, origin, onLastDeparture, maxIcons: 8);
            else
                onLastDeparture?.Invoke();
            AudioManager.Play(SoundCue.MissionDelivered);
            if (feedback != null) feedback.ShowMessage("Entrega concluída! +" + mission.coinReward + " moedas.");

            if (housesRewarded < houseRewardLimit)
            {
                communityDeliveries++;
                if (communityDeliveries >= DeliveriesForNextHouse() && hand != null &&
                    hand.AddCommunityHouseCard())
                {
                    communityDeliveries = 0;
                    housesRewarded++;
                    AudioManager.Play(SoundCue.NewHouseCard);
                    if (feedback != null) feedback.ShowMessage("Comunidade: carta de casa recebida!");
                }
            }
            return true;
        }
        finally
        {
            // O banco dispara Changed durante o gasto e durante o crédito.
            delivering = false;
        }
    }

    private string CommunityReminder()
    {
        if (housesRewarded >= houseRewardLimit)
            return "Todas as casas foram liberadas.";

        int remaining = DeliveriesForNextHouse() - communityDeliveries;
        if (remaining <= 0)
            return "Nova casa aguardando espaço para ser liberada.";

        return remaining == 1 ?
            "Falta 1 entrega para uma nova casa." :
            "Faltam " + remaining + " entregas para uma nova casa.";
    }

    private int DeliveriesForNextHouse() => housesRewarded switch
    {
        0 => deliveriesPerHouse,
        1 => secondHouseDeliveries,
        _ => thirdHouseDeliveries
    };

    private void GenerateOffers(DeliveryOfferForecast forecast)
    {
        DeliveryMissionCatalog catalog = Resources.Load<DeliveryMissionCatalog>("DeliveryMissions");
        if (catalog == null || catalog.entries == null)
        {
            Debug.LogError("Catálogo de encomendas não encontrado em Resources/DeliveryMissions.");
            return;
        }

        int minimum = Mathf.Clamp(minimumOffers, 1, 5);
        int maximum = Mathf.Clamp(maximumOffers, minimum, 5);
        int count = UnityEngine.Random.Range(minimum, maximum + 1);
        var activeTemplates = new HashSet<DeliveryMissionTemplate>();
        foreach (DeliveryMission mission in missions)
            activeTemplates.Add(mission.template);
        var dailyResources = new HashSet<SaleItem>();
        var accepted = missions.FindAll(mission => mission.accepted);
        var candidates = new Dictionary<DeliveryMissionTemplate, DeliveryMission>();
        var estimates = new Dictionary<DeliveryMissionTemplate, DeliveryOfferForecast.Feasibility>();
        foreach (DeliveryMissionTemplate template in catalog.entries)
        {
            if (!DeliveryMissionCatalog.IsUsable(template) || template.availableFromDay > currentDay ||
                activeTemplates.Contains(template) || candidates.ContainsKey(template)) continue;
            var requirements = new List<DeliveryRequirement>(template.requirements.Count);
            foreach (DeliveryRequirement requirement in template.requirements)
                requirements.Add(new DeliveryRequirement { item = requirement.item, quantity = requirement.quantity });
            var mission = new DeliveryMission
            {
                template = template,
                title = template.title,
                description = template.description,
                requirements = requirements,
                dueDay = currentDay + Mathf.Clamp(template.deliveryDays, 0, Mathf.Max(0, campaignDays - currentDay)),
                coinReward = template.coinReward
            };
            candidates.Add(template, mission);
            estimates.Add(template, forecast.Evaluate(accepted, mission));
        }

        int returns = 0;
        for (int i = 0; i < count; i++)
        {
            bool canReturn = returns < returningOffersPerMarket;
            bool largeSlot = i == 1;
            DeliveryMissionTemplate template = PickTemplate(estimates, activeTemplates, dailyResources,
                largeSlot, canReturn, preferredCategoryOnly: true) ??
                PickTemplate(estimates, activeTemplates, dailyResources, largeSlot, canReturn,
                    preferredCategoryOnly: false);
            if (template == null) break;
            if (refusedTemplates.Remove(template)) returns++;

            activeTemplates.Add(template);
            foreach (DeliveryRequirement requirement in template.requirements)
                dailyResources.Add(requirement.item);
            shownTemplates.Add(template);
            missions.Add(candidates[template]);
        }
    }

    private DeliveryMissionTemplate PickTemplate(
        Dictionary<DeliveryMissionTemplate, DeliveryOfferForecast.Feasibility> estimates,
        HashSet<DeliveryMissionTemplate> activeTemplates, HashSet<SaleItem> dailyResources,
        bool largeSlot, bool allowReturns, bool preferredCategoryOnly)
    {
        DeliveryMissionTemplate next = null;
        int totalWeight = 0;
        foreach (var entry in estimates)
        {
            DeliveryMissionTemplate template = entry.Key;
            if (activeTemplates.Contains(template) ||
                (!allowReturns && refusedTemplates.Contains(template)) ||
                (preferredCategoryOnly && template.largeOrder != largeSlot)) continue;

            int weight = 4 + (int)entry.Value;
            bool overlaps = false;
            foreach (DeliveryRequirement requirement in template.requirements)
            {
                if (!dailyResources.Contains(requirement.item)) continue;
                overlaps = true;
                break;
            }
            if (!overlaps) weight++;
            if (!shownTemplates.Contains(template)) weight++;

            // Cada pedido elegível entra no sorteio com o próprio peso.
            totalWeight += weight;
            if (UnityEngine.Random.Range(0, totalWeight) < weight) next = template;
        }
        return next;
    }

    private void Refresh()
    {
        if (resources == null || delivering) return;
        if (activePanel != null) activePanel.Refresh(missions, this);
        if (missions.Count == 0)
        {
            if (orderCard != null) orderCard.Show(null, CommunityReminder());
            if (acceptButton != null) acceptButton.interactable = false;
            if (deliverButton != null) deliverButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            return;
        }
        DeliveryMission selected = missions[selectedIndex];
        if (orderCard != null) orderCard.Show(selected, CommunityReminder());
        if (acceptButton != null) acceptButton.interactable = marketOpen && !selected.accepted;
        if (deliverButton != null) deliverButton.interactable = CanDeliver(selected);
        if (nextButton != null) nextButton.interactable = missions.Count > 1;
    }
}
