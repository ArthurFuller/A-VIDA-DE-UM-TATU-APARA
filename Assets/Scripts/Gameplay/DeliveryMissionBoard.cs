using System;
using System.Collections.Generic;
using System.Text;
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
}

public sealed class DeliveryMissionBoard : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private Text orderText;
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
    private readonly List<DeliveryMission> returningOffers = new List<DeliveryMission>();
    private int currentDay = 1;
    private int selectedIndex;
    private int lastGeneratedDay;
    private bool marketOpen;
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
            campaignDays = balance.campaignDays;
            returningOffersPerMarket = balance.returningOffersPerMarket;
        }
        if (resources != null) resources.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
    }

    public void SetDay(int day, bool isMarketDay)
    {
        currentDay = day;
        marketOpen = isMarketDay;
        foreach (DeliveryMission mission in missions)
            if (!mission.accepted && !returningOffers.Contains(mission)) returningOffers.Add(mission);
        missions.RemoveAll(m => !m.accepted);
        int expired = missions.RemoveAll(m => day > m.dueDay);
        if (expired > 0 && feedback != null) feedback.ShowMessage("Pedidos com prazo encerrado foram removidos.");
        if (marketOpen && lastGeneratedDay != day)
        {
            GenerateOffers();
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
        if (index < 0 || index >= missions.Count || resources == null) return;
        DeliveryMission mission = missions[index];
        if (!mission.accepted || currentDay > mission.dueDay) return;
        if (!resources.TryDeliverMission(mission.requirements))
        {
            if (feedback != null) feedback.ShowMessage("Ainda faltam itens para a entrega.");
            return;
        }
        resources.AddCoins(mission.coinReward);
        AudioManager.Play(SoundCue.MissionDelivered);
        if (resourceHud != null && deliverButton != null)
            resourceHud.FlyFromRect(ResourceKind.Coins, mission.coinReward,
                deliverButton.transform as RectTransform);
        if (feedback != null) feedback.ShowMessage("Entrega concluída! +" + mission.coinReward + " moedas.");
        missions.RemoveAt(index);
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
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, missions.Count - 1));
        Refresh();
    }

    private string CommunityReminder()
    {
        if (housesRewarded >= houseRewardLimit)
            return "Comunidade: todas as casas foram liberadas.";

        int remaining = DeliveriesForNextHouse() - communityDeliveries;
        if (remaining <= 0)
            return "Comunidade: nova casa aguardando espaço para ser liberada.";

        return "Comunidade: " + (remaining == 1 ?
            "falta 1 entrega para uma nova casa." :
            "faltam " + remaining + " entregas para uma nova casa.");
    }

    private int DeliveriesForNextHouse() => housesRewarded switch
    {
        0 => deliveriesPerHouse,
        1 => secondHouseDeliveries,
        _ => thirdHouseDeliveries
    };

    private void GenerateOffers()
    {
        DeliveryMissionCatalog catalog = Resources.Load<DeliveryMissionCatalog>("DeliveryMissions");
        if (catalog == null || catalog.entries == null)
        {
            Debug.LogError("Catálogo de encomendas não encontrado em Resources/DeliveryMissions.");
            return;
        }
        var pool = new List<SaleItem>();
        foreach (DeliveryMissionTemplate template in catalog.entries)
            if (DeliveryMissionCatalog.IsUsable(template) &&
                !pool.Contains(template.requirements[0].item))
                pool.Add(template.requirements[0].item);
        if (pool.Count == 0) return;
        int count = Mathf.Clamp(UnityEngine.Random.Range(minimumOffers, maximumOffers + 1), 1, pool.Count);
        int returning = Mathf.Min(returningOffersPerMarket, Mathf.Min(count, returningOffers.Count));
        for (int i = 0; i < returning; i++)
        {
            int pick = UnityEngine.Random.Range(0, returningOffers.Count);
            DeliveryMission offer = returningOffers[pick];
            returningOffers.RemoveAt(pick);
            offer.dueDay = Mathf.Min(campaignDays, currentDay + 2);
            missions.Add(offer);
            if (offer.requirements.Count > 0) pool.Remove(offer.requirements[0].item);
        }
        count = Mathf.Min(count - returning, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = UnityEngine.Random.Range(0, pool.Count);
            SaleItem item = pool[pick];
            pool.RemoveAt(pick);
            var options = new List<DeliveryMissionTemplate>();
            foreach (DeliveryMissionTemplate template in catalog.entries)
                if (DeliveryMissionCatalog.IsUsable(template) &&
                    template.requirements[0].item == item) options.Add(template);
            DeliveryMissionTemplate chosen = options[UnityEngine.Random.Range(0, options.Count)];
            var requirements = new List<DeliveryRequirement>(chosen.requirements.Count);
            foreach (DeliveryRequirement requirement in chosen.requirements)
                requirements.Add(new DeliveryRequirement
                    { item = requirement.item, quantity = requirement.quantity });
            missions.Add(new DeliveryMission
            {
                title = chosen.title,
                description = chosen.description,
                requirements = requirements,
                dueDay = Mathf.Min(campaignDays, currentDay + 2),
                coinReward = chosen.coinReward
            });
        }
    }

    private void Refresh()
    {
        if (resources == null) return;
        if (activePanel != null) activePanel.Refresh(missions, currentDay, resources);
        if (missions.Count == 0)
        {
            if (orderText != null) orderText.text = "Nenhum pedido disponível.\nNovas ofertas chegam com a barraca.\n\n" + CommunityReminder();
            if (acceptButton != null) acceptButton.interactable = false;
            if (deliverButton != null) deliverButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            return;
        }
        DeliveryMission selected = missions[selectedIndex];
        int daysLeft = selected.dueDay - currentDay + 1;
        if (orderText != null)
        {
            var text = new StringBuilder(selected.title);
            if (!string.IsNullOrWhiteSpace(selected.description))
                text.Append('\n').Append(selected.description);
            text.Append("\nEntregar: ");
            bool first = true;
            foreach (DeliveryRequirement requirement in selected.requirements)
            {
                if (!first) text.Append("; ");
                first = false;
                text.Append(requirement.quantity).Append("x ")
                    .Append(DeliveryMissionCatalog.ItemName(requirement.item))
                    .Append(" (tem ").Append(resources.Stock(requirement.item)).Append(')');
            }
            text.Append("\nPrazo: ").Append(daysLeft).Append(daysLeft == 1 ? " dia" : " dias")
                .Append("  ·  +").Append(selected.coinReward).Append(" moedas")
                .Append("\n\n").Append(CommunityReminder());
            orderText.text = text.ToString();
        }
        if (acceptButton != null) acceptButton.interactable = marketOpen && !selected.accepted;
        if (deliverButton != null) deliverButton.interactable = selected.accepted &&
            resources.CanDeliverMission(selected.requirements);
        if (nextButton != null) nextButton.interactable = missions.Count > 1;
    }
}
