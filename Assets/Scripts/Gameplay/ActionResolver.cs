using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActionResolver : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private SeedCatalog seedCatalog;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private DayEventController dayEvents;
    [SerializeField] private GatheringMinigame gatheringGame;
    [SerializeField] private CommunityCamera communityCamera;
    [SerializeField] private Button forestVisitButton;
    [SerializeField] private Text forestVisitLabel;
    [SerializeField] private Button fishingVisitButton;
    [SerializeField] private Text fishingVisitLabel;
    [SerializeField] private FoodPreparationPanel foodPreparationPanel;
    [SerializeField] private InventoryTrading inventoryTrading;
    [SerializeField] private WorkerAssignment[] workers;
    [SerializeField] private Transform forestDestination;
    [SerializeField] private ForestTreeFall forestTrees;
    [SerializeField] private Transform fishingDestination;
    [SerializeField, Range(1, 6)] private int forestPositions = 4;
    [SerializeField, Min(0.1f)] private float forestSpacing = 0.55f;
    [SerializeField] private Vector3 forestLineOffset = Vector3.zero;
    [SerializeField, Range(1, 6)] private int fishingPositions = 4;
    [SerializeField, Min(0.1f)] private float fishingSpacing = 0.55f;
    [SerializeField] private Vector3 fishingShoreOffset = new Vector3(0.25f, 0f, 0f);
    [SerializeField] private Transform fencePassage;
    [SerializeField, Tooltip("Ponto A: passagem das casas 2, 3 e 5 antes do ponto B.")]
    private Transform workerRoutePointA;
    [SerializeField, Tooltip("Ponto B: passagem comum antes das atividades gerais.")]
    private Transform workerRoutePointB;
    [SerializeField, Tooltip("Pontos até a floresta, na ordem da ida. Vazio usa a passagem da cerca.")]
    private Transform[] forestWaypoints;
    [SerializeField, Tooltip("Pontos até a pesca, na ordem da ida. Vazio usa a passagem da cerca.")]
    private Transform[] fishingWaypoints;
    [SerializeField, Min(0f), Tooltip("Zero usa a velocidade geral.")]
    private float forestTravelSpeed;
    [SerializeField, Min(0f), Tooltip("Zero usa a velocidade geral.")]
    private float fishingTravelSpeed;
    [SerializeField, HideInInspector, Min(0)] private int woodReward = 2;
    [SerializeField, HideInInspector, Min(1)] private int gambleStake = 10;
    [SerializeField, Min(0.1f)] private float coinTossDuration = 0.65f;
    [SerializeField, Min(0.1f), Tooltip("Velocidade geral dos funcionários em unidades da cena por segundo.")]
    private float movementSpeed = 2.4f;
    [SerializeField, Min(0.01f)] private float plowWorkDuration = 5f;
    [SerializeField, Min(0f)] private float arrivalStillTime = 0.2f;

    private sealed class GatheringTask
    {
        public readonly ActivityKind Activity;
        public readonly Transform Destination;
        public readonly Button Button;
        public readonly Text Label;
        public readonly Vector3 Offset;
        public WorkerAssignment Worker;
        public ActivityKind SelectedActivity;
        public bool Arrived;
        public bool Started;
        public float StillTime;
        public Vector3 LastPosition;
        public Vector3? PositionOverride;

        public Vector3 TargetPosition => PositionOverride ?? Destination.position + Offset;

        public GatheringTask(ActivityKind activity, Transform destination, Button button, Text label,
            Vector3 offset)
        {
            Activity = activity;
            Destination = destination;
            Button = button;
            Label = label;
            Offset = offset;
        }
    }

    private WorkerAssignment pendingPlotWorker;
    private ActivityCardDrag pendingPlotCard;
    private ActivityKind pendingPlotAction;
    private readonly System.Collections.Generic.HashSet<Plot> plotsBeingPlowed = new();
    private int busyWorkers;
    private bool housePreviewBusy;
    private int replayAnimations;
    private WorkerAssignment pendingFoodWorker;
    private ActivityCardDrag pendingFoodCard;
    private bool pendingFoodPlus;
    private bool servingPreparedFood;
    private GatheringTask[] forestTasks;
    private GatheringTask[] fishingTasks;
    private GatheringTask activeMinigame;
    private SertaoCoinToss coinToss;
    private readonly System.Collections.Generic.Dictionary<WorkerAssignment, Tween> movements = new();
    private readonly System.Collections.Generic.HashSet<WorkerAssignment> busyMovements = new();
    private readonly System.Collections.Generic.HashSet<WorkerAssignment> returningHome = new();
    private readonly System.Collections.Generic.HashSet<WorkerAssignment> working = new();
    private readonly System.Collections.Generic.Dictionary<WorkerAssignment, Vector3[]> workRoutes = new();
    private readonly System.Collections.Generic.Dictionary<WorkerAssignment, Vector3[]> workOriginRoutes = new();
    private readonly System.Collections.Generic.Dictionary<WorkerAssignment, float> workTravelSpeeds = new();
    [SerializeField] private Button cancelSelectionButton;

    public bool IsBusy => housePreviewBusy || busyWorkers > 0 || replayAnimations > 0 || pendingFoodWorker != null ||
        pendingPlotWorker != null || servingPreparedFood ||
        HasGatheringWorker(forestTasks) || HasGatheringWorker(fishingTasks);
    public bool HasPendingSelection => pendingPlotWorker != null;
    internal bool TutorialGatheringInView => activeMinigame != null ||
        (communityCamera != null && communityCamera.IsFollowingWorker);

    internal RectTransform TutorialGatheringVisitButton
    {
        get
        {
            if (TutorialGatheringInView) return null;
            return VisibleButton(WaitingTask(forestTasks)) ?? VisibleButton(WaitingTask(fishingTasks));
        }
    }

    private static RectTransform VisibleButton(GatheringTask task)
    {
        Button button = task?.Button;
        return button != null && button.isActiveAndEnabled && button.IsInteractable()
            ? button.transform as RectTransform : null;
    }

    public event System.Action<bool> PendingSelectionChanged;
    public bool GatheringNeedsPlayer => NeedsGatheringPlayer(forestTasks) ||
        NeedsGatheringPlayer(fishingTasks);
    public int WoodGainedToday { get; private set; }
    public int FoodGainedToday { get; private set; }
    public int FishGainedToday { get; private set; }
    public int CropsStoredToday { get; private set; }
    public int PlotsPlowedToday { get; private set; }
    public int CropsHarvestedToday { get; private set; }
    public event System.Action<ActivityCardDrag> CardUsed;
    public event System.Action<int, ActivityCardDrag> CardPlayed;
    public int AssignmentVersion { get; private set; }

    public void SetHousePreviewBusy(bool busy) => housePreviewBusy = busy;

    private void Awake()
    {
        GameBalance balance = GameBalance.Current;
        if (balance != null)
        {
            woodReward = balance.woodReward;
            gambleStake = balance.gambleStake;
        }
        if (cancelSelectionButton == null)
            foreach (Button candidate in FindObjectsByType<Button>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (candidate.name == "Cancelar escolha de terreno")
                {
                    cancelSelectionButton = candidate;
                    break;
                }
        if (cancelSelectionButton == null) return;
        cancelSelectionButton.onClick.AddListener(CancelPendingSelection);
        cancelSelectionButton.gameObject.SetActive(false);
    }

    private void Start()
    {
        forestTasks = CreateGatheringPositions(ActivityKind.CollectWood, forestDestination,
            forestVisitButton, forestVisitLabel, forestPositions, forestSpacing, forestLineOffset);
        fishingTasks = CreateGatheringPositions(ActivityKind.Fish, fishingDestination,
            fishingVisitButton, fishingVisitLabel, fishingPositions, fishingSpacing, fishingShoreOffset);
        if (forestVisitButton != null)
        {
            forestVisitButton.onClick.AddListener(VisitForestWorker);
            forestVisitButton.gameObject.SetActive(false);
        }
        if (fishingVisitButton != null)
        {
            fishingVisitButton.onClick.AddListener(VisitFishingWorker);
            fishingVisitButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (coinToss != null) Destroy(coinToss.gameObject);
        if (forestVisitButton != null)
            forestVisitButton.onClick.RemoveListener(VisitForestWorker);
        if (fishingVisitButton != null)
            fishingVisitButton.onClick.RemoveListener(VisitFishingWorker);
        if (cancelSelectionButton != null)
            cancelSelectionButton.onClick.RemoveListener(CancelPendingSelection);
    }

    private void Update()
    {
        foreach (GatheringTask task in forestTasks) UpdateGathering(task);
        foreach (GatheringTask task in fishingTasks) UpdateGathering(task);
    }

    private static GatheringTask[] CreateGatheringPositions(ActivityKind activity, Transform destination,
        Button button, Text label, int count, float spacing, Vector3 lineOffset)
    {
        var positions = new GatheringTask[Mathf.Clamp(count, 1, 6)];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = new GatheringTask(activity, destination, button, label,
                lineOffset + Vector3.forward * ((i - (positions.Length - 1) * 0.5f) * spacing));
        return positions;
    }

    private static bool HasGatheringWorker(GatheringTask[] tasks)
    {
        if (tasks == null) return false;
        foreach (GatheringTask task in tasks)
            if (task.Worker != null) return true;
        return false;
    }

    private static bool NeedsGatheringPlayer(GatheringTask[] tasks)
    {
        if (tasks == null) return false;
        foreach (GatheringTask task in tasks)
            if (task.Worker != null && task.Arrived && !task.Started) return true;
        return false;
    }

    private static bool HasAssignedWorker(GatheringTask[] tasks, WorkerAssignment worker)
    {
        if (tasks == null) return false;
        foreach (GatheringTask task in tasks)
            if (task.Worker == worker) return true;
        return false;
    }

    public void TryAssign(WorkerAssignment worker, ActivityKind activity, string activityName,
        ActivityCardDrag card = null)
    {
        TryAssignInternal(worker, activity, activityName, card, false);
    }

    private void TryAssignInternal(WorkerAssignment worker, ActivityKind activity,
        string activityName, ActivityCardDrag card, bool replayAnimationComplete)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions || SertaoCoinToss.IsOpen)
            return;
        if (replayAnimations > 0 && !replayAnimationComplete) return;
        if (pendingFoodWorker != null || servingPreparedFood)
        {
            RejectAction("Confirme ou cancele o preparo de comida primeiro.");
            return;
        }
        if (pendingPlotWorker != null)
        {
            RejectAction("Escolha um terreno ou cancele a carta ativa primeiro.");
            return;
        }
        if (activity == ActivityKind.BuySeeds)
        {
            if (merchant != null) merchant.ToggleShop();
            return;
        }
        if (worker.IsUnavailableToday)
        {
            RejectAction("Esse trabalhador está ausente hoje.");
            return;
        }
        if (working.Contains(worker) || busyMovements.Contains(worker) ||
            HasAssignedWorker(forestTasks, worker) || HasAssignedWorker(fishingTasks, worker))
        {
            RejectAction("Aguarde esse trabalhador concluir a ação atual.");
            return;
        }
        bool replaying = activity == ActivityKind.Replay;
        if (replaying)
        {
            ActivityKind? repeated = worker.LastActivity;
            if (repeated == null || !CanReplay(repeated.Value))
            {
                RejectAction("Replay exige uma ação anterior desse trabalhador.");
                card?.PlayRejectedReplay();
                return;
            }
            if (!replayAnimationComplete && card != null)
            {
                ActivityCardAvailability hand = card.transform.parent
                    .GetComponentInChildren<ActivityCardAvailability>(true);
                bool disposable = repeated == ActivityKind.BuildFence ||
                    repeated == ActivityKind.BuildHouse;
                if (disposable && (hand == null || !hand.HasDisposable(repeated.Value)))
                {
                    RejectAction("Replay precisa da carta descartável na mão.");
                    card.PlayRejectedReplay();
                    return;
                }
                AssignmentVersion++;
                replayAnimations++;
                ActivityKind revealed = repeated.Value;
                bool deliverDuringReplay = !RequiresTargetSelection(revealed);
                card.PlayReplay(worker, revealed, deliverDuringReplay, () =>
                {
                    replayAnimations--;
                    TryAssignInternal(worker, ActivityKind.Replay, activityName, card, true);
                    if (pendingFoodCard != card) card.RestoreUnspentReplay();
                });
                if (disposable) hand.ConsumeReplayDisposable(revealed);
                return;
            }
            activity = repeated.Value;
        }
        if (activity == ActivityKind.Coffee)
        {
            worker.GrantCoffeeAction();
            AudioManager.Play(SoundCue.Coffee);
            worker.Select();
            worker.SetStatus("ação extra disponível");
            RecordCard(replaying ? ActivityKind.Replay : activity, worker, card);
            feedback.ShowMessage(replaying ? "Replay do Cafézinho: +1 ação disponível." :
                "Cafézinho: uma ação diferente disponível.");
            return;
        }
        if (!replaying && !worker.CanAct(activity))
        {
            RejectAction(worker.UsedToday ? "Esse trabalhador já agiu hoje." :
                "Cafézinho exige uma ação diferente.");
            return;
        }
        if (activity == ActivityKind.Gamble)
        {
            StartGamble(worker, activityName, card);
            return;
        }
        if (activity == ActivityKind.Rest)
        {
            CommitAction(worker, activity, activityName, card);
            worker.ScheduleExtraAction();
            AudioManager.Play(SoundCue.Rest);
            feedback.ShowMessage("Descanso: ação extra amanhã. Alimentação normal hoje.");
            SendWorkerHome(worker);
            return;
        }
        bool woodActivity = activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus;
        bool fishActivity = activity == ActivityKind.Fish || activity == ActivityKind.FishPlus;
        GatheringTask task = woodActivity ? FreeTask(forestTasks) :
            fishActivity ? FreeTask(fishingTasks) : null;
        if ((woodActivity || fishActivity) && task == null)
        {
            RejectAction("Todos os postos dessa área já estão ocupados.");
            return;
        }

        if (activity == ActivityKind.PrepareFood || activity == ActivityKind.FoodPlus)
        {
            AssignFoodPreparation(worker, activity, card);
            return;
        }

        ClearPendingPlot();

        if (RequiresTargetSelection(activity))
        {
            SelectPlot(worker, activity, activityName, card);
            return;
        }

        if (task == null)
        {
            RejectAction("Essa carta ainda precisa de um alvo válido.");
            return;
        }

        StartGathering(task, worker, activity, activityName, card);
    }

    private static GatheringTask FreeTask(GatheringTask[] tasks)
    {
        if (tasks == null) return null;
        foreach (GatheringTask task in tasks)
            if (task.Worker == null) return task;
        return null;
    }

    private void StartGamble(WorkerAssignment worker, string activityName, ActivityCardDrag card)
    {
        if (activeMinigame != null)
        {
            RejectAction("Conclua o minigame atual antes de apostar.");
            return;
        }
        if (resources == null || (long)resources.Coins - gambleStake < int.MinValue)
        {
            RejectAction("Aposta acima do limite de moedas.");
            return;
        }

        int stake = gambleStake;
        coinToss = SertaoCoinToss.Open(gameObject.scene, stake, coinTossDuration,
            () => worker.SetStatus("lançando moeda"), won =>
            {
                coinToss = null;
                busyWorkers--;
                if (won)
                {
                    resources.AddCoins(stake);
                    if (resourceHud != null) resourceHud.FlyFromWorld(ResourceKind.Coins,
                        stake, worker.transform.position + Vector3.up);
                }
                else resources.TrySpendCoins(stake, allowDebt: true);
                worker.SetStatus(won ? "aposta vencida" : "aposta perdida");
                SendWorkerHome(worker);
            });
        if (coinToss == null)
        {
            RejectAction("Cara ou coroa indisponível.");
            return;
        }
        busyWorkers++;
        CommitAction(worker, ActivityKind.Gamble, activityName, card);
        worker.SetStatus("aguardando cara ou coroa");
    }

    private void RecordCard(ActivityKind activity, WorkerAssignment worker, ActivityCardDrag card)
    {
        AssignmentVersion++;
        if (card != null && dayCycle != null)
            CardPlayed?.Invoke(dayCycle.CurrentDay, card);
        card?.PrepareDelivery(worker);
        CardUsed?.Invoke(card);
    }

    private static bool CanReplay(ActivityKind activity) =>
        activity != ActivityKind.Replay && activity != ActivityKind.BuySeeds;

    private static bool RequiresTargetSelection(ActivityKind activity) => activity is
        ActivityKind.Plow or ActivityKind.PlantCorn or ActivityKind.PlantPumpkin or
        ActivityKind.PlantJerimum or ActivityKind.Harvest or ActivityKind.WateringCan or
        ActivityKind.Fertilizer or ActivityKind.BuildFence or ActivityKind.BuildHouse or
        ActivityKind.PlaceCow or ActivityKind.PlaceChicken or ActivityKind.FeedAnimals;

    private void CommitAction(WorkerAssignment worker, ActivityKind activity, string name,
        ActivityCardDrag card)
    {
        if (card != null && card.Kind == ActivityKind.Replay)
            worker.MarkRepeatedAction(name, activity);
        else
            worker.MarkUsed(name, activity);
        RecordCard(activity, worker, card);
    }

    private void AssignFoodPreparation(WorkerAssignment worker, ActivityKind activity, ActivityCardDrag card)
    {
        if (foodPreparationPanel == null || inventoryTrading == null)
        {
            feedback.ShowMessage("Preparação de comida não configurada.");
            return;
        }
        ClearPendingPlot();
        pendingFoodWorker = worker;
        pendingFoodCard = card;
        AssignmentVersion++;
        pendingFoodPlus = activity == ActivityKind.FoodPlus;
        worker.Select();
        worker.SetStatus("escolha os ingredientes");
        foodPreparationPanel.Show(pendingFoodPlus);
    }

    private void ClearPendingPlot()
    {
        bool hadSelection = pendingPlotWorker != null;
        Plot.ClearHighlights();
        HouseSite.ClearHighlights();
        pendingPlotCard?.ReturnFromSelection();
        if (pendingPlotWorker != null && !pendingPlotWorker.UsedToday)
            pendingPlotWorker.SetStatus("livre");
        pendingPlotWorker = null;
        pendingPlotCard = null;
        if (cancelSelectionButton != null) cancelSelectionButton.gameObject.SetActive(false);
        if (hadSelection) PendingSelectionChanged?.Invoke(false);
    }

    public void CancelPendingSelection()
    {
        if (pendingPlotWorker == null) return;
        ClearPendingPlot();
        feedback.ShowMessage("Escolha cancelada. A carta voltou para a mão.");
    }

    private static bool IsPlanting(ActivityKind activity) =>
        activity is ActivityKind.PlantCorn or ActivityKind.PlantPumpkin or ActivityKind.PlantJerimum;

    private void SelectPlot(WorkerAssignment worker, ActivityKind activity, string activityName,
        ActivityCardDrag card)
    {
        bool available = activity == ActivityKind.BuildHouse ? HouseSite.HasAvailable() :
            Plot.HasAvailable(activity, dayCycle.CurrentDay, seedCatalog);
        bool canWaitForPlowing = IsPlanting(activity) && plotsBeingPlowed.Count > 0;
        if (!available && !canWaitForPlowing)
        {
            RejectAction("Não há alvo disponível para essa carta.");
            card?.PlayRejectedReplay();
            return;
        }
        AssignmentVersion++;
        pendingPlotWorker = worker;
        pendingPlotCard = card;
        pendingPlotAction = activity;
        card?.FlyToSelection(activity);
        if (cancelSelectionButton != null)
        {
            cancelSelectionButton.gameObject.SetActive(true);
            cancelSelectionButton.transform.SetAsLastSibling();
        }
        PendingSelectionChanged?.Invoke(true);
        Plot.ShowAvailable(activity, dayCycle.CurrentDay, seedCatalog);
        if (activity == ActivityKind.BuildHouse) HouseSite.ShowAvailable();
        worker.Select();
        bool buildingHouse = activity == ActivityKind.BuildHouse;
        worker.SetStatus(buildingHouse ? "escolha um lote" : "escolha um terreno");
        feedback.ShowMessage(!available ? "Aguarde o término do arado e escolha o terreno." :
            "Clique no " + (buildingHouse ? "lote" : "terreno") + " para " +
            activityName.ToLowerInvariant() + ".");
    }

    private void StartGathering(GatheringTask task, WorkerAssignment worker, ActivityKind activity, string activityName,
        ActivityCardDrag card)
    {
        if (task == null || task.Destination == null ||
            fencePassage == null || resources == null)
        {
            feedback.ShowMessage("Destino ou recursos não configurados.");
            return;
        }
        if (gatheringGame == null || !gatheringGame.CanStart() ||
            communityCamera == null || task.Button == null)
        {
            feedback.ShowMessage("Minigame de coleta não configurado.");
            return;
        }

        task.Worker = worker;
        task.PositionOverride = null;
        task.Arrived = false;
        task.Started = false;
        task.StillTime = 0f;
        task.SelectedActivity = activity;
        CommitAction(worker, activity, activityName, card);
        feedback.ShowMessage(activityName + " em andamento. Use o botão para acompanhar.");
        UpdateVisitButton(task);
        MoveToGatheringArea(task);
    }

    private void CompleteGathering(WorkerAssignment worker, ActivityKind activity, int result,
        System.Action onResourcesCollected, ResourceHud.DropBatch drops = null)
    {
        if (result < 0)
        {
            worker.SetStatus("ação cancelada");
            feedback.ShowMessage("Minigame de coleta indisponível.");
            onResourcesCollected?.Invoke();
            return;
        }
        worker.SetStatus("ação concluída");
        if (activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus)
        {
            int gained = woodReward + result + (activity == ActivityKind.WoodPlus ? 1 : 0);
            gained = Mathf.Max(0, gained - worker.ProductionReduction);
            resources.AddWood(gained);
            resources.RecordProduction(SaleItem.Wood, gained);
            if (resourceHud != null && drops != null && drops.Icons.Count > 0)
                resourceHud.FlyDrops(drops, onResourcesCollected);
            else if (resourceHud != null) resourceHud.FlyFromWorld(ResourceKind.Wood,
                gained, worker.transform.position + Vector3.up, 0f, onResourcesCollected);
            else onResourcesCollected?.Invoke();
            if (dayEvents != null) dayEvents.RecordWood(gained);
            WoodGainedToday += gained;
            feedback.ShowMessage("+" + gained + " madeira");
        }
        else
        {
            int gained = result;
            resources.AddFish(gained);
            resources.RecordProduction(SaleItem.Fish, gained);
            if (resourceHud != null && drops != null && drops.Icons.Count > 0)
                resourceHud.FlyDrops(drops, onResourcesCollected);
            else if (resourceHud != null) resourceHud.FlyFromWorld(ResourceKind.Fish,
                gained, worker.transform.position + Vector3.up, 0f, onResourcesCollected);
            else onResourcesCollected?.Invoke();
            FishGainedToday += gained;
            feedback.ShowMessage("+" + gained + " peixe(s) guardados");
        }
    }

    public void TryUsePlot(Plot plot)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        if (pendingFoodWorker != null)
        {
            RejectAction("Confirme ou cancele o preparo de comida primeiro.");
            return;
        }
        if (pendingPlotWorker == null)
        {
            feedback.ShowMessage("Atribua primeiro um card de terreno a um trabalhador.");
            return;
        }

        switch (pendingPlotAction)
        {
            case ActivityKind.Plow: PlowPlot(plot); break;
            case ActivityKind.PlantCorn: PlantPlot(plot, CropType.Corn); break;
            case ActivityKind.PlantPumpkin: PlantPlot(plot, CropType.Pumpkin); break;
            case ActivityKind.PlantJerimum: PlantPlot(plot, CropType.Jerimum); break;
            case ActivityKind.Harvest: HarvestPlot(plot); break;
            case ActivityKind.WateringCan:
            case ActivityKind.Fertilizer: ApplyCropImprovement(plot); break;
            case ActivityKind.BuildFence: FencePlot(plot); break;
            case ActivityKind.PlaceCow: PlaceAnimal(plot, AnimalKind.Cow); break;
            case ActivityKind.PlaceChicken: PlaceAnimal(plot, AnimalKind.Chicken); break;
            case ActivityKind.FeedAnimals: FeedAnimals(plot); break;
        }
    }

    private void FeedAnimals(Plot plot)
    {
        if (plot.Animal == AnimalKind.None)
        {
            RejectAction("Escolha um cercado com animal.");
            return;
        }
        SoundCue feedingCue = plot.Animal == AnimalKind.Chicken ? SoundCue.FeedChicken : SoundCue.FeedAnimals;
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, ActivityKind.FeedAnimals, "distribuindo ração", card);
        MoveAndWork(worker, plot, plowWorkDuration, () =>
        {
            if (dayCycle.TryFeedAnimal(plot, out int milk, out int eggs))
                feedback.ShowMessage(milk > 0 ? $"Ração: +{milk} leite{(milk == 1 ? "" : "s")}." :
                    $"Ração: +{eggs} ovo{(eggs == 1 ? "" : "s")}.");
            AudioManager.Play(feedingCue);
            SendWorkerHome(worker);
        });
    }

    private void PlaceAnimal(Plot plot, AnimalKind animal)
    {
        if (!plot.IsFenced || !plot.IsEmpty || plot.Animal != AnimalKind.None)
        {
            RejectAction("Escolha um cercado vazio para o animal.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, animal == AnimalKind.Cow ? ActivityKind.PlaceCow :
            ActivityKind.PlaceChicken, "levando animal ao cercado", card);
        MoveAndWork(worker, plot, plowWorkDuration, () =>
        {
            if (plot != null && plot.PlaceAnimal(animal))
            {
                AudioManager.Play(animal == AnimalKind.Cow ? SoundCue.CowArrive : SoundCue.ChickenArrive);
                feedback.ShowMessage(animal == AnimalKind.Cow ? "Vaca no cercado." : "Galinha no cercado.");
            }
            else RejectAction("O cercado já está ocupado ou indisponível.");
            SendWorkerHome(worker);
        });
    }

    private void FencePlot(Plot plot)
    {
        if (!plot.CanUse(ActivityKind.BuildFence, dayCycle.CurrentDay, seedCatalog))
        {
            RejectAction("A cerca exige um terreno vazio e não arado.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, ActivityKind.BuildFence, "construindo cerca", card);
        MoveAndWork(worker, plot, plowWorkDuration, () =>
        {
            plot.BuildFence();
            worker.SetStatus("cerca construída");
            feedback.ShowMessage("Terreno cercado.");
            SendWorkerHome(worker);
        }, () => ConstructionSmoke.Play(plot.transform, plot.PlowedGroundBounds, plowWorkDuration));
    }

    public void TryBuildHouse(HouseSite site)
    {
        if (site == null || site.IsBuilt || pendingPlotWorker == null ||
            pendingPlotAction != ActivityKind.BuildHouse || dayCycle == null ||
            !dayCycle.CanAcceptActions) return;
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment builder = TakePendingWorker();
        CommitAction(builder, ActivityKind.BuildHouse, "construindo casa", card);
        MoveAndWork(builder, site.ConstructionPosition, site.ConstructionFacingPosition,
            site.GetConstructionWaypoints(builder.OriginHouseNumber), site.ConstructionTravelSpeed,
            plowWorkDuration, false, () =>
        {
            if (site.Build(dayCycle.CurrentDay))
            {
                AudioManager.Play(SoundCue.HouseBuilt);
                feedback.ShowMessage("Casa construída. O novo trabalhador chegará amanhã.");
            }
            SendWorkerHome(builder);
        }, () => site.PlayConstructionSmoke(plowWorkDuration));
    }

    public void RegisterWorker(WorkerAssignment worker)
    {
        if (worker == null || System.Array.IndexOf(workers, worker) >= 0) return;
        var updated = new System.Collections.Generic.List<WorkerAssignment>(workers) { worker };
        workers = updated.ToArray();
    }

    private void ApplyCropImprovement(Plot plot)
    {
        ActivityKind activity = pendingPlotAction;
        if (!plot.CanUse(activity, dayCycle.CurrentDay, seedCatalog))
        {
            RejectAction(activity == ActivityKind.Fertilizer ?
                "Escolha uma plantação que ainda não recebeu adubo." :
                "Escolha uma plantação ainda em crescimento.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        var targets = plot.ConnectedPlotsFor(activity, dayCycle.CurrentDay, seedCatalog);
        CommitAction(worker, activity, activity.ToString(), card);
        ImproveNext(0);

        void ImproveNext(int index)
        {
            if (index >= targets.Count)
            {
                worker.SetStatus(activity == ActivityKind.Fertilizer ? "plantação adubada" :
                    "crescimento acelerado");
                feedback.ShowMessage(activity == ActivityKind.Fertilizer ?
                    "Adubo: aumenta a produção na colheita." : "Plantação avançou um dia.");
                SendWorkerHome(worker);
                return;
            }
            Plot target = targets[index];
            MoveAndWork(worker, target, plowWorkDuration, () =>
            {
                if (activity == ActivityKind.Fertilizer)
                {
                    target.Fertilize();
                    AudioManager.Play(SoundCue.Fertilize);
                }
                else
                {
                    AudioManager.Play(SoundCue.Water);
                    target.AdvanceGrowth(1);
                    target.RefreshGrowth(dayCycle.CurrentDay, seedCatalog);
                    if (MandalaSynergy.Instance != null)
                        MandalaSynergy.Instance.ApplyGrowthBonuses(dayCycle.CurrentDay, seedCatalog);
                }
                ImproveNext(index + 1);
            });
        }
    }

    private void PlowPlot(Plot plot)
    {
        if (plotsBeingPlowed.Contains(plot))
        {
            RejectAction("Esse terreno já está sendo arado.");
            return;
        }
        if (plot.IsPlowed || plot.IsFenced || plot.Animal != AnimalKind.None)
        {
            RejectAction("Escolha um terreno livre para arar.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        plotsBeingPlowed.Add(plot);
        CommitAction(worker, ActivityKind.Plow, "arando", card);
        MoveAndWork(worker, plot, plowWorkDuration, () =>
        {
            plotsBeingPlowed.Remove(plot);
            if (plot == null)
            {
                SendWorkerHome(worker);
                return;
            }
            plot.Plow();
            if (pendingPlotWorker != null && IsPlanting(pendingPlotAction) && dayCycle != null)
                Plot.ShowAvailable(pendingPlotAction, dayCycle.CurrentDay, seedCatalog);
            AudioManager.Play(SoundCue.Plow);
            PlotsPlowedToday++;
            worker.SetStatus("terreno arado");
            feedback.ShowMessage("Terreno preparado para plantio.");
            SendWorkerHome(worker);
        });
    }

    private void PlantPlot(Plot plot, CropType crop)
    {
        if (!plot.IsPlowed || plot.IsFenced || !plot.IsEmpty || plot.Animal != AnimalKind.None)
        {
            RejectAction("Plante em um terreno arado e vazio.");
            return;
        }
        if (seedCatalog == null)
        {
            feedback.ShowMessage("Culturas não configuradas.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, pendingPlotAction, "plantando", card);
        MoveAndWork(worker, plot, plowWorkDuration, () =>
        {
            plot.Plant(crop, dayCycle.CurrentDay, seedCatalog);
            AudioManager.Play(SoundCue.Plant);
            worker.SetStatus("plantio concluído");
            feedback.ShowMessage(seedCatalog.Get(crop).displayName + " plantado.");
            SendWorkerHome(worker);
        });
    }

    private void HarvestPlot(Plot plot)
    {
        if (seedCatalog == null || resources == null || !plot.IsReady(dayCycle.CurrentDay, seedCatalog))
        {
            RejectAction("A plantação ainda não está pronta.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        var targets = plot.ReadyConnectedPlots(dayCycle.CurrentDay, seedCatalog);
        CommitAction(worker, ActivityKind.Harvest, "colhendo", card);
        HarvestNext(0);

        void HarvestNext(int index)
        {
            if (index >= targets.Count)
            {
                worker.SetStatus("colheita concluída");
                SendWorkerHome(worker);
                return;
            }
            Plot target = targets[index];
            if (target == null || !target.IsReady(dayCycle.CurrentDay, seedCatalog))
            {
                HarvestNext(index + 1);
                return;
            }
            MoveAndWork(worker, target, plowWorkDuration, () =>
            {
                int harvestYield = target.HarvestYield(dayCycle.CurrentDay, seedCatalog);
                int pestLoss = dayEvents != null ? dayEvents.HarvestReductionFor(target) : 0;
                CropType crop = target.Harvest();
                AudioManager.Play(SoundCue.Harvest);
                CropSettings settings = seedCatalog.Get(crop);
                int gained = Mathf.Max(0, harvestYield - worker.ProductionReduction - pestLoss);
                resources.AddCrop(crop, gained);
                SaleItem harvestedCrop = crop == CropType.Corn ? SaleItem.Corn :
                    crop == CropType.Pumpkin ? SaleItem.Pumpkin : SaleItem.Jerimum;
                resources.RecordProduction(harvestedCrop, gained);
                if (resourceHud != null)
                {
                    ResourceKind kind = crop == CropType.Corn ? ResourceKind.Corn :
                        crop == CropType.Pumpkin ? ResourceKind.Pumpkin : ResourceKind.Jerimum;
                    resourceHud.FlyFromWorld(kind, gained, target.transform.position + Vector3.up,
                        0.8f);
                }
                CropsStoredToday += gained;
                CropsHarvestedToday++;
                feedback.ShowMessage("+" + gained + " " + settings.displayName + " guardado(s)");
                HarvestNext(index + 1);
            });
        }
    }

    public bool ConfirmFoodPreparation(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs,
        RectTransform resultOrigin = null, System.Action onAllArrived = null)
    {
        if (servingPreparedFood || pendingFoodWorker == null ||
            (pendingFoodWorker.UsedToday &&
             (pendingFoodCard == null || pendingFoodCard.Kind != ActivityKind.Replay)) ||
            inventoryTrading == null ||
            resources == null) return false;
        if (!inventoryTrading.TryPrepare(fish, corn, pumpkin, jerimum, milk, eggs,
            pendingFoodPlus, out int preparedFood))
        {
            feedback.ShowMessage("Selecione uma receita válida com ingredientes disponíveis.");
            return false;
        }
        WorkerAssignment worker = pendingFoodWorker;
        ActivityCardDrag card = pendingFoodCard;
        bool plus = pendingFoodPlus;
        pendingFoodWorker = null;
        pendingFoodCard = null;
        pendingFoodPlus = false;
        servingPreparedFood = true;
        CommitAction(worker, plus ? ActivityKind.FoodPlus : ActivityKind.PrepareFood, "comida preparada", card);
        resources.AddFood(preparedFood);
        resources.RecordProduction(SaleItem.Food, preparedFood);
        FoodGainedToday += preparedFood;
        AudioManager.Play(SoundCue.FoodPrepared);
        void CompleteServing()
        {
            servingPreparedFood = false;
            feedback?.ShowMessage("+" + preparedFood + " comida preparada");
            onAllArrived?.Invoke();
        }

        if (resourceHud == null) CompleteServing();
        else if (resultOrigin != null)
            resourceHud.FlyFromRect(ResourceKind.Food, preparedFood, resultOrigin, onAllArrived: CompleteServing);
        else resourceHud.FlyFromWorld(ResourceKind.Food, preparedFood,
            worker.transform.position + Vector3.up, onAllArrived: CompleteServing);
        return true;
    }

    public void CancelFoodPreparation()
    {
        if (pendingFoodWorker != null && !pendingFoodWorker.UsedToday) pendingFoodWorker.SetStatus("livre");
        pendingFoodCard?.RestoreUnspentReplay();
        pendingFoodWorker = null;
        pendingFoodCard = null;
        pendingFoodPlus = false;
    }

    private WorkerAssignment TakePendingWorker()
    {
        WorkerAssignment worker = pendingPlotWorker;
        pendingPlotWorker = null;
        pendingPlotCard = null;
        if (cancelSelectionButton != null) cancelSelectionButton.gameObject.SetActive(false);
        PendingSelectionChanged?.Invoke(false);
        Plot.ClearHighlights();
        HouseSite.ClearHighlights();
        pendingFoodCard = null;
        pendingFoodWorker = null;
        return worker;
    }

    public void ReturnWorkersHome()
    {
        ClearPendingPlot();
        foreach (WorkerAssignment worker in workers)
        {
            if (worker == null || movements.ContainsKey(worker) ||
                Vector3.Distance(worker.transform.position, worker.HomePosition) < 0.01f)
                continue;
            SendWorkerHome(worker);
        }
    }

    public void BeginNextDay()
    {
        plotsBeingPlowed.Clear();
        if (forestTrees != null) forestTrees.ResetTrees();
        ClearPendingPlot();
        pendingFoodCard?.RestoreUnspentReplay();
        pendingFoodWorker = null;
        pendingFoodCard = null;
        WoodGainedToday = 0;
        FoodGainedToday = 0;
        FishGainedToday = 0;
        CropsStoredToday = 0;
        PlotsPlowedToday = 0;
        CropsHarvestedToday = 0;
        foreach (WorkerAssignment worker in workers)
            if (worker != null) worker.BeginNextDay();
    }

    private void StopMovement(WorkerAssignment worker)
    {
        if (movements.TryGetValue(worker, out Tween previous))
        {
            previous.Kill();
            movements.Remove(worker);
        }
        if (busyMovements.Remove(worker)) busyWorkers--;
        returningHome.Remove(worker);
    }

    private void MoveWorker(WorkerAssignment worker, Vector3 destination, System.Action onArrived,
        bool countAsBusy = true, bool isReturn = false, float speedOverride = 0f)
    {
        bool braking = returningHome.Contains(worker) && !isReturn;
        StopMovement(worker);
        if (countAsBusy)
        {
            busyWorkers++;
            busyMovements.Add(worker);
        }
        if (isReturn) returningHome.Add(worker);
        float speed = speedOverride > 0f ? speedOverride : movementSpeed;
        float duration = Mathf.Max(0.01f, Vector3.Distance(worker.transform.position, destination) / speed);
        worker.FaceForRoute(isReturn);
        Tween travel = worker.transform.DOMove(destination, duration).SetEase(Ease.InOutSine);
        if (braking) travel = DOTween.Sequence().AppendInterval(0.1f).Append(travel);
        movements[worker] = travel;
        travel.OnComplete(() =>
        {
            movements.Remove(worker);
            if (busyMovements.Remove(worker)) busyWorkers--;
            returningHome.Remove(worker);
            onArrived?.Invoke();
        });
    }

    public void BringWorkerOutOfHouse(WorkerAssignment worker, Vector3 doorwayPosition)
    {
        if (worker == null) return;
        Vector3 door = worker.DoorwayMarker != null ? worker.DoorwayMarker.position : doorwayPosition;
        Vector3 outside = worker.OutsideMarker != null ? worker.OutsideMarker.position : door;
        worker.transform.position = door;
        worker.SetStatus("saindo de casa");
        Transform visual = worker.SpriteVisual;
        Vector3 outdoorScale = visual != null ? visual.localScale : Vector3.one;
        if (visual != null) visual.localScale = outdoorScale * worker.IndoorScale;
        MoveAlongRoute(worker, new[] { outside }, () =>
        {
            void FinishExit() => MoveAlongRoute(worker, new[] { worker.HomePosition },
                () =>
                {
                    worker.RestoreHomeFacing();
                    worker.SetStatus("em casa");
                }, false);
            if (visual != null)
                visual.DOScale(outdoorScale, 0.3f).SetEase(Ease.OutBack).OnComplete(FinishExit);
            else FinishExit();
        }, false);
    }

    private void SendWorkerHome(WorkerAssignment worker)
    {
        if (worker == null) return;
        bool hasRoute = workRoutes.TryGetValue(worker, out Vector3[] returnRoute);
        workRoutes.Remove(worker);
        workOriginRoutes.Remove(worker);
        workTravelSpeeds.TryGetValue(worker, out float travelSpeed);
        workTravelSpeeds.Remove(worker);
        if (Vector3.Distance(worker.transform.position, worker.HomePosition) < 0.05f)
        {
            worker.RestoreHomeFacing();
            worker.SetStatus("em casa");
            return;
        }
        worker.SetStatus("voltando para casa");
        void Arrived()
        {
            worker.RestoreHomeFacing();
            worker.SetStatus("em casa");
        }
        if (hasRoute)
        {
            var points = new Vector3[returnRoute.Length + 1];
            System.Array.Copy(returnRoute, points, returnRoute.Length);
            points[points.Length - 1] = worker.HomePosition;
            MoveAlongRoute(worker, points, Arrived, false, travelSpeed, isReturn: true);
        }
        else MoveWorker(worker, worker.HomePosition, Arrived, false, true, travelSpeed);
    }

    private void MoveAndWork(WorkerAssignment worker, Plot plot, float workDuration,
        System.Action onFinished, System.Action onStarted = null) =>
        MoveAndWork(worker, plot.WorkPosition, plot.transform.position,
            plot.WorkWaypoints, plot.WorkTravelSpeed, workDuration, true, onFinished, onStarted);

    private void MoveAndWork(WorkerAssignment worker, Vector3 destination, Vector3 facingTarget,
        Vector3[] waypoints, float travelSpeed, float workDuration, bool useGeneralAccess,
        System.Action onFinished, System.Action onStarted = null)
    {
        bool continuing = workOriginRoutes.TryGetValue(worker, out Vector3[] originRoute);
        if (!continuing)
        {
            originRoute = useGeneralAccess ? GeneralAccessRoute(worker) : System.Array.Empty<Vector3>();
            workOriginRoutes[worker] = originRoute;
        }
        Vector3[] access = continuing ? System.Array.Empty<Vector3>() : originRoute;
        var route = new Vector3[access.Length + waypoints.Length + 1];
        System.Array.Copy(access, route, access.Length);
        System.Array.Copy(waypoints, 0, route, access.Length, waypoints.Length);
        route[route.Length - 1] = destination;
        var reverse = new Vector3[waypoints.Length + originRoute.Length];
        for (int i = 0; i < waypoints.Length; i++) reverse[i] = waypoints[waypoints.Length - 1 - i];
        for (int i = 0; i < originRoute.Length; i++)
            reverse[waypoints.Length + i] = originRoute[originRoute.Length - 1 - i];
        workRoutes[worker] = reverse;
        workTravelSpeeds[worker] = travelSpeed;
        void BeginWork()
        {
            worker.FaceTowards(facingTarget);
            onStarted?.Invoke();
            WorkAtStation(worker, workDuration, onFinished);
        }
        if (Vector3.Distance(worker.transform.position, destination) <= 0.15f)
        {
            StopMovement(worker);
            BeginWork();
            return;
        }
        worker.SetStatus("caminhando");
        MoveAlongRoute(worker, route, BeginWork, true, travelSpeed);
    }

    private void WorkAtStation(WorkerAssignment worker, float workDuration, System.Action onFinished)
    {
        busyWorkers++;
        working.Add(worker);
        worker.SetWorking(true);
        worker.SetStatus("trabalhando");
        bool soilWork = worker.LastActivity.HasValue &&
            (worker.LastActivity.Value == ActivityKind.Plow ||
             worker.LastActivity.Value == ActivityKind.PlantCorn ||
             worker.LastActivity.Value == ActivityKind.PlantPumpkin ||
             worker.LastActivity.Value == ActivityKind.PlantJerimum ||
             worker.LastActivity.Value == ActivityKind.BuildFence ||
             worker.LastActivity.Value == ActivityKind.Harvest ||
             worker.LastActivity.Value == ActivityKind.Fertilizer ||
             worker.LastActivity.Value == ActivityKind.WateringCan);
        if (worker.Progress != null) worker.Progress.Show(soilWork);
        SoundCue? workSound = worker.LastActivity switch
        {
            ActivityKind.BuildFence or ActivityKind.BuildHouse => SoundCue.HammerWork,
            ActivityKind.Plow => SoundCue.PlowWork,
            ActivityKind.PlantCorn or ActivityKind.PlantPumpkin or ActivityKind.PlantJerimum or
            ActivityKind.PlaceCow or ActivityKind.PlaceChicken => SoundCue.SoilWork,
            _ => null
        };
        float nextStrike = 0.16f;
        float progress = 0;
        DOTween.To(() => progress, value =>
        {
            progress = value;
            if (worker.Progress != null) worker.Progress.SetProgress(value);
            float elapsed = value * workDuration;
            if (workSound.HasValue && value < 0.94f && elapsed >= nextStrike)
            {
                AudioManager.Play(workSound.Value);
                nextStrike = elapsed + (workSound == SoundCue.HammerWork ? 0.57f : 0.75f);
            }
        }, 1f, workDuration).SetEase(Ease.Linear).OnComplete(() =>
        {
            if (worker.Progress != null) worker.Progress.Hide();
            busyWorkers--;
            working.Remove(worker);
            worker.SetWorking(false);
            onFinished?.Invoke();
        });
    }

    private void RejectAction(string message)
    {
        AudioManager.Play(SoundCue.UiRejected);
        feedback.ShowMessage(message);
    }

    private void MoveToGatheringArea(GatheringTask task)
    {
        WorkerAssignment worker = task.Worker;
        worker.SetStatus("caminhando");
        MoveAlongRoute(worker, GatheringRoute(worker, task.Activity, task.TargetPosition), () =>
        {
            task.Arrived = true;
            task.LastPosition = worker.transform.position;
            task.StillTime = 0f;
            worker.SetStatus("aguardando jogador");
            UpdateVisitButton(task);
        }, speedOverride: GatheringTravelSpeed(task.Activity));
    }

    private void VisitForestWorker() => VisitGatheringWorker(WaitingTask(forestTasks), forestVisitButton);
    private void VisitFishingWorker() => VisitGatheringWorker(WaitingTask(fishingTasks), fishingVisitButton);

    private GatheringTask WaitingTask(GatheringTask[] tasks)
    {
        if (tasks == null) return null;
        GatheringTask nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (GatheringTask task in tasks)
        {
            if (task.Worker == null || task.Started) continue;
            float distance = (task.Worker.transform.position - task.TargetPosition).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = task;
        }
        return nearest;
    }

    private void VisitGatheringWorker(GatheringTask task, Button button)
    {
        if (task?.Worker != null && activeMinigame == null)
        {
            button?.GetComponent<GatheringVisitButtonVisual>()?.ExitLeftFast();
            communityCamera.Follow(task.Worker.transform);
        }
    }

    private void UpdateGathering(GatheringTask task)
    {
        if (task?.Worker == null || task.Started) return;
        if (task.Arrived)
        {
            Vector3 position = task.Worker.transform.position;
            bool stopped = !movements.ContainsKey(task.Worker) &&
                Vector3.Distance(position, task.TargetPosition) < 0.03f &&
                (position - task.LastPosition).sqrMagnitude < 0.000001f;
            task.StillTime = stopped ? task.StillTime + Time.deltaTime : 0f;
            task.LastPosition = position;
        }
        UpdateVisitButton(task);
        if (task.Arrived && task.StillTime >= arrivalStillTime && activeMinigame == null &&
            !SertaoCoinToss.IsOpen && communityCamera.IsFollowing(task.Worker.transform) && communityCamera.IsFollowReady)
            BeginGatheringGame(task);
    }

    private void UpdateVisitButton(GatheringTask task)
    {
        if (task?.Button == null) return;
        GatheringVisitButtonVisual visual = task.Button.GetComponent<GatheringVisitButtonVisual>();
        if (visual != null && visual.IsExiting) return;
        GatheringTask[] areaTasks = task.Activity == ActivityKind.CollectWood ? forestTasks : fishingTasks;
        GatheringTask waiting = WaitingTask(areaTasks);
        bool visible = waiting != null && activeMinigame == null &&
            !communityCamera.IsFollowingWorker;
        if (task.Button.gameObject.activeSelf != visible)
            task.Button.gameObject.SetActive(visible);
        if (visible && task.Label != null)
        {
            string area = task.Activity == ActivityKind.CollectWood ? "FLORESTA" : "PESCA";
            string action = waiting.Arrived && waiting.StillTime >= arrivalStillTime ? "INICIAR" : "ACOMPANHAR";
            string label = area + " - " + action + "\n" + waiting.Worker.DisplayName;
            if (task.Label.text != label) task.Label.text = label;
        }
    }

    private void BeginGatheringGame(GatheringTask task)
    {
        WorkerAssignment worker = task.Worker;
        ActivityKind activity = task.SelectedActivity;
        bool wood = activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus;
        ResourceHud.DropBatch fishDrops = !wood && resourceHud != null ?
            resourceHud.CreateDropBatch(ResourceKind.Fish) : null;
        task.Started = true;
        activeMinigame = task;
        communityCamera.SetFollowLocked(true);
        if (forestVisitButton != null) forestVisitButton.gameObject.SetActive(false);
        if (fishingVisitButton != null) fishingVisitButton.gameObject.SetActive(false);
        busyWorkers++;
        if (wood && forestTrees != null && forestTrees.TryGetFacingPosition(worker.transform.position, out Vector3 treePosition))
            worker.SetWorkingTowards(treePosition);
        else
        {
            worker.FaceTowards(task.Destination.position);
            worker.SetWorking(true);
        }
        worker.SetStatus("atividade interativa");
        gatheringGame.Begin(activity, result =>
        {
            worker.SetWorking(false);
            task.Worker = null;
            task.Arrived = false;
            task.Started = false;
            communityCamera.HoldCurrentView();
            if (wood && result >= 0)
            {
                ResolveWoodGathering(task, worker, activity, result);
                return;
            }
            SendGatheringWorkerHome(worker, activity);
            CompleteGathering(worker, activity, result, () =>
            {
                FinishGatheringRound(task);
            }, fishDrops);
        }, (activity == ActivityKind.Fish || activity == ActivityKind.FishPlus) &&
            dayEvents != null ? dayEvents.FishingReduction : 0,
            communityCamera.ShakeWoodHit, communityCamera.SetFishingTension,
            () =>
            {
                if (fishDrops != null)
                {
                    Camera camera = Camera.main;
                    Vector2 landing = camera != null
                        ? (Vector2)camera.WorldToScreenPoint(worker.transform.position + Vector3.up)
                        : gatheringGame.FishDropScreenPosition;
                    resourceHud.DropOneAtScreen(fishDrops,
                        gatheringGame.FishDropScreenPosition, landing);
                }
            });
    }

    private void ResolveWoodGathering(GatheringTask task, WorkerAssignment worker,
        ActivityKind activity, int rating)
    {
        System.Action afterFall = () =>
        {
            int gained = Mathf.Max(0, woodReward + rating +
                (activity == ActivityKind.WoodPlus ? 1 : 0) - worker.ProductionReduction);
            ResourceHud.DropBatch drops = resourceHud != null ?
                resourceHud.CreateDropBatch(ResourceKind.Wood) : null;
            Vector3 origin = forestTrees != null ? forestTrees.LastImpactPosition :
                worker.transform.position + Vector3.up;
            Sequence dropSequence = DOTween.Sequence().SetTarget(this);
            for (int i = 0; i < gained; i++)
            {
                dropSequence.AppendCallback(() =>
                {
                    if (drops != null) resourceHud.DropOne(drops, origin);
                });
                dropSequence.AppendInterval(0.11f);
            }
            dropSequence.AppendInterval(0.28f);
            dropSequence.OnComplete(() =>
            {
                SendGatheringWorkerHome(worker, activity);
                CompleteGathering(worker, activity, rating, () =>
                {
                    FinishGatheringRound(task);
                }, drops);
            });
        };
        if (forestTrees != null) forestTrees.FallNear(worker.transform.position, afterFall);
        else afterFall();
    }

    private void FinishGatheringRound(GatheringTask finished)
    {
        busyWorkers--;
        GatheringTask[] area = finished.Activity == ActivityKind.CollectWood ? forestTasks : fishingTasks;
        GatheringTask next = WaitingTask(area);
        activeMinigame = null;
        if (next?.Worker != null)
        {
            bool wasAtArea = next.Arrived;
            next.PositionOverride = finished.TargetPosition;
            next.Arrived = false;
            next.StillTime = 0f;
            WorkerAssignment worker = next.Worker;
            communityCamera.ReturnToCommunity();
            communityCamera.Follow(worker.transform);
            worker.SetStatus("indo para o minigame");
            System.Action arrived = () =>
            {
                next.Arrived = true;
                next.LastPosition = worker.transform.position;
                next.StillTime = 0f;
                worker.SetStatus("aguardando jogador");
            };
            float speed = GatheringTravelSpeed(next.Activity);
            if (wasAtArea) MoveWorker(worker, next.TargetPosition, arrived, speedOverride: speed);
            else MoveAlongRoute(worker, GatheringRoute(worker, next.Activity, next.TargetPosition), arrived,
                speedOverride: speed);
        }
        else communityCamera.ReturnToCommunity();
        UpdateVisitButton(forestTasks[0]);
        UpdateVisitButton(fishingTasks[0]);
    }

    private void SendGatheringWorkerHome(WorkerAssignment worker, ActivityKind activity)
    {
        int returnDay = dayCycle.CurrentDay;
        worker.SetStatus("voltando para casa");
        MoveAlongRoute(worker, GatheringRoute(worker, activity, worker.HomePosition, true), () =>
        {
            worker.RestoreHomeFacing();
            if (dayCycle.CurrentDay == returnDay) worker.SetStatus("em casa");
        }, false, GatheringTravelSpeed(activity), isReturn: true);
    }

    private float GatheringTravelSpeed(ActivityKind activity) =>
        activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus
            ? forestTravelSpeed : fishingTravelSpeed;

    private Vector3[] GeneralAccessRoute(WorkerAssignment worker)
    {
        var points = new System.Collections.Generic.List<Vector3>(2);
        int house = worker.OriginHouseNumber;
        if ((house == 2 || house == 3 || house == 5) && workerRoutePointA != null)
            points.Add(workerRoutePointA.position);
        if (workerRoutePointB != null) points.Add(workerRoutePointB.position);
        return points.ToArray();
    }

    private Vector3[] GatheringRoute(WorkerAssignment worker, ActivityKind activity,
        Vector3 destination, bool returning = false)
    {
        Transform[] markers = activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus
            ? forestWaypoints : fishingWaypoints;
        var points = new System.Collections.Generic.List<Vector3>();
        Vector3[] access = GeneralAccessRoute(worker);
        if (!returning) points.AddRange(access);
        int accessCount = points.Count;
        if (markers != null && markers.Length > 0)
        {
            if (returning)
            {
                for (int i = markers.Length - 1; i >= 0; i--)
                    if (markers[i] != null) points.Add(markers[i].position);
            }
            else foreach (Transform marker in markers)
                if (marker != null) points.Add(marker.position);
        }
        if (points.Count == accessCount && fencePassage != null) points.Add(fencePassage.position);
        if (returning)
            for (int i = access.Length - 1; i >= 0; i--) points.Add(access[i]);
        points.Add(destination);
        return points.ToArray();
    }

    private void MoveAlongRoute(WorkerAssignment worker, Vector3[] points, System.Action onArrived,
        bool countAsBusy = true, float speedOverride = 0f, bool isReturn = false)
    {
        bool braking = returningHome.Contains(worker) && countAsBusy;
        StopMovement(worker);
        if (countAsBusy)
        {
            busyWorkers++;
            busyMovements.Add(worker);
        }
        else returningHome.Add(worker);
        Vector3 from = worker.transform.position;
        float distance = 0f;
        foreach (Vector3 point in points)
        {
            distance += Vector3.Distance(from, point);
            from = point;
        }
        float speed = speedOverride > 0f ? speedOverride : movementSpeed;
        float duration = Mathf.Max(0.01f, distance / speed);
        worker.FaceForRoute(isReturn);
        Tween route = points.Length == 1
            ? worker.transform.DOMove(points[0], duration)
            : worker.transform.DOPath(points, duration, PathType.Linear, PathMode.Full3D);
        route.SetEase(Ease.InOutSine);
        if (braking) route = DOTween.Sequence().AppendInterval(0.1f).Append(route);
        movements[worker] = route;
        route.OnComplete(() =>
        {
            movements.Remove(worker);
            if (busyMovements.Remove(worker)) busyWorkers--;
            returningHome.Remove(worker);
            onArrived?.Invoke();
        });
    }
}
