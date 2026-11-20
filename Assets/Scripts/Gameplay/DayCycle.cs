using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public sealed class DayCycle : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private WorkerAssignment[] workers;
    [SerializeField] private HouseSite[] houseSites;
    [SerializeField] private Plot[] plots;
    [SerializeField] private SeedCatalog seedCatalog;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private DayTimeline timeline;
    [SerializeField] private ActiveMissionsPanel activeMissionsPanel;
    [SerializeField] private DeliveryMissionBoard missionBoard;
    [SerializeField] private DayEventController dayEvents;
    [SerializeField] private ActivityCardAvailability hand;
    [SerializeField] private FeedingMinigame feedingGame;
    [SerializeField] private CardRewardChoice cardChoice;
    [SerializeField] private Text dayText;
    [SerializeField] private Button finishDayButton;
    [SerializeField] private SertaoMatchResultsView resultsView;
    [SerializeField, HideInInspector, Min(1)] private int totalDays = 12;
    [SerializeField, HideInInspector, Min(1)] private int milkPerCow = 1;
    [SerializeField, HideInInspector, Min(1)] private int eggsPerChicken = 1;

    private static readonly ProfilerMarker PrepareFirstDayMarker = new("Sertao.FirstDay.Prepare");
    private static readonly ProfilerMarker ShowFirstEventMarker = new("Sertao.FirstDay.ShowEvent");

    private int currentDay = 1;
    private InitialTutorial tutorial;
    private bool skipTutorialDayPending;
    private bool campaignTransitionStarted;
    private bool firstDayOpening = true;
    private bool firstDayEventPresented;
    private HoverPunchButton finishDayFeedback;
    private Tween finishDaySlide;
    private float finishDayHomeX;
    private bool finishDayShifted;
    private bool finishDayWasInteractable;
    private bool feedingOpen;
    private bool gameOver;
    private int todayMilk;
    private int todayEggs;
    private readonly List<(Plot plot, ResourceKind kind, int amount)> animalGains = new();
    private int lastClosedDay = -1;
    private int missedMeals;
#if UNITY_EDITOR
    private int? previewFinalBalance;

    internal bool TryPreviewFinalDay(int finalBalance)
    {
        if (currentDay == 0 || !CanAcceptActions || previewFinalBalance.HasValue || Time.timeScale <= 0f ||
            SertaoCoinToss.IsOpen || actions == null || actions.IsBusy ||
            (hand != null && hand.IsDraggingCard) || finishDayButton == null ||
            !finishDayButton.isActiveAndEnabled || !finishDayButton.IsInteractable()) return false;

        previewFinalBalance = finalBalance;
        finishDayButton.onClick.Invoke();
        if (gameOver) return true;
        previewFinalBalance = null;
        return false;
    }
#endif

    public bool CanAcceptActions => !firstDayOpening && !GS2SceneFade.IsEnteringGameplay &&
        !feedingOpen && !gameOver &&
        (dayEvents == null || !dayEvents.IsPanelOpen);
    public int CurrentDay => currentDay;
    internal bool IncludesTutorialDay { get; private set; }
    internal int TotalDays => totalDays;
    internal EventCardView TutorialEventCard => dayEvents != null ? dayEvents.TutorialCard : null;
    internal Button TutorialFinishButton => finishDayButton;
    internal WorkerAssignment TutorialWorker => workers == null ? null :
        Array.Find(workers, worker => worker != null && worker.isActiveAndEnabled && !worker.UsedToday);
    internal bool FirstDayPreparationFinished { get; private set; }

    public bool TryFeedAnimal(Plot plot, out int milk, out int eggs) =>
        TryFeedAnimals(plot != null ? plot.ConnectedPlotsFor(ActivityKind.FeedAnimals, currentDay, seedCatalog) : null,
            null, out milk, out eggs);

    internal bool TryFeedAnimals(IReadOnlyList<Plot> targets, WorkerAssignment reservationOwner,
        out int milk, out int eggs)
    {
        milk = 0;
        eggs = 0;
        if (targets == null || resources == null) return false;

        foreach (Plot target in targets)
        {
            if (target == null || !target.CanUse(ActivityKind.FeedAnimals, currentDay, seedCatalog, reservationOwner))
                continue;
            if (target.Animal == AnimalKind.Cow) milk++;
            else if (target.Animal == AnimalKind.Chicken) eggs++;
        }
        if (milk + eggs == 0) return false;

        resources.AddAnimalProducts(milk, eggs);
        resources.RecordProduction(SaleItem.Milk, milk);
        resources.RecordProduction(SaleItem.Eggs, eggs);

        // Cada animal alimentado gera seu próprio ícone a partir do cercado de origem,
        // como acontece com a produção diária. O crédito ao estoque ocorre só uma vez, acima.
        const float itemInterval = 0.20f;
        float appearanceDelay = 0f;
        foreach (Plot target in targets)
        {
            if (target == null || !target.CanUse(ActivityKind.FeedAnimals, currentDay, seedCatalog, reservationOwner))
                continue;
            Plot sourcePlot = target;
            if (resourceHud != null)
            {
                ResourceKind kind = target.Animal == AnimalKind.Cow ? ResourceKind.Milk : ResourceKind.Eggs;
                resourceHud.FlyFromWorld(kind, 1, target.transform.position + Vector3.up,
                    0.8f, appearanceDelay: appearanceDelay,
                    onEachAppearance: () => { if (sourcePlot != null) sourcePlot.PulseAnimal(); });
            }
            else
                target.PulseAnimal();
            appearanceDelay += itemInterval;
        }
        return true;
    }

    private void ProduceAnimalResources()
    {
        todayMilk = 0;
        todayEggs = 0;
        animalGains.Clear();
        foreach (Plot plot in plots)
        {
            if (plot == null || plot.Animal == AnimalKind.None) continue;
            bool isCow = plot.Animal == AnimalKind.Cow;
            int bonus = MandalaSynergy.Instance != null ?
                MandalaSynergy.Instance.ExtraAnimalProduction(plot) : 0;
            int amount = (isCow ? milkPerCow : eggsPerChicken) + bonus;
            if (amount <= 0) continue;

            if (isCow) todayMilk += amount;
            else todayEggs += amount;
            animalGains.Add((plot, isCow ? ResourceKind.Milk : ResourceKind.Eggs, amount));
        }
    }

    private void AddDailyAnimalResources()
    {
        if (todayMilk + todayEggs == 0 || resources == null) return;
        resources.AddAnimalProducts(todayMilk, todayEggs);
        resources.RecordProduction(SaleItem.Milk, todayMilk);
        resources.RecordProduction(SaleItem.Eggs, todayEggs);
        AnimateAnimalGains();
        if (feedback != null)
            feedback.ShowMessage("Animais: +" + todayMilk + " leite e +" + todayEggs + " ovos.");
    }

    private void AnimateAnimalGains()
    {
        const float itemInterval = 0.20f;
        float nextItem = 0f;
        foreach (var gain in animalGains)
        {
            Plot plot = gain.plot;
            if (plot == null) continue;
            void OnItem()
            {
                if (plot != null) plot.PulseAnimal();
                AudioManager.Play(SoundCue.AnimalProductionPop);
            }

            if (resourceHud != null)
                resourceHud.FlyFromWorld(gain.kind, gain.amount, plot.transform.position + Vector3.up,
                    0.8f, appearanceDelay: nextItem, appearanceInterval: itemInterval,
                    onEachAppearance: OnItem);
            else
                for (int i = 0; i < gain.amount; i++)
                    DOVirtual.DelayedCall(nextItem + i * itemInterval, OnItem, false).SetLink(gameObject);
            nextItem += gain.amount * itemInterval;
        }
    }
    public event Action DayChanged;
    public event Action DayClosing;
    public event Action DayClosingCancelled;

    public void RegisterWorker(WorkerAssignment worker)
    {
        if (worker == null || Array.IndexOf(workers, worker) >= 0) return;
        var updated = new List<WorkerAssignment>(workers) { worker };
        workers = updated.ToArray();
    }

    private void Awake()
    {
        GameBalance balance = GameBalance.Current;
        if (balance != null)
        {
            totalDays = balance.totalDays;
            milkPerCow = balance.milkPerCow;
            eggsPerChicken = balance.eggsPerChicken;
        }
        if (finishDayButton != null &&
            finishDayButton.TryGetComponent(out finishDayFeedback))
            finishDayFeedback.AnimatePointerClick = false;

        if (finishDayButton != null && finishDayButton.image is Image finishDayImage)
        {
            Sprite sprite = finishDayImage.overrideSprite ?? finishDayImage.sprite;
            if (sprite != null && !sprite.packed && sprite.texture.isReadable)
                finishDayImage.alphaHitTestMinimumThreshold = 0.15f;
        }

        if (hand != null) hand.SuspendEntranceForEvent();
    }

    private void OnEnable()
    {
        if (actions == null || actions.GatheringGame == null) return;
        actions.GatheringGame.BarVisibilityChanged += SetGatheringBarVisible;
        if (actions.GatheringGame.IsBarVisible) SetGatheringBarVisible(true);
    }

    private void OnDisable()
    {
        if (actions != null && actions.GatheringGame != null)
            actions.GatheringGame.BarVisibilityChanged -= SetGatheringBarVisible;
        finishDaySlide?.Kill();
        if (finishDayShifted && finishDayButton != null)
        {
            RectTransform rect = (RectTransform)finishDayButton.transform;
            rect.anchoredPosition = new Vector2(finishDayHomeX, rect.anchoredPosition.y);
            finishDayButton.interactable = finishDayWasInteractable;
            finishDayShifted = false;
        }
    }

    private void SetGatheringBarVisible(bool visible)
    {
        if (finishDayButton == null || !visible && !finishDayShifted) return;
        RectTransform rect = (RectTransform)finishDayButton.transform;
        finishDaySlide?.Kill();
        if (visible && !finishDayShifted)
        {
            finishDayHomeX = rect.anchoredPosition.x;
            finishDayWasInteractable = finishDayButton.interactable;
            finishDayButton.interactable = false;
            finishDayShifted = true;
        }
        float destination = finishDayHomeX;
        if (visible)
        {
            var viewport = (RectTransform)rect.parent;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, rect);
            destination = rect.anchoredPosition.x + Mathf.Max(0f, viewport.rect.xMax - bounds.min.x + 24f);
        }
        // O feedback de hover cancela tweens do RectTransform. Este movimento tem alvo próprio.
        finishDaySlide = DOTween.To(() => rect.anchoredPosition.x,
                x => rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y), destination, 0.34f)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                if (visible) return;
                finishDayButton.interactable = finishDayWasInteractable;
                finishDayShifted = false;
            });
    }

    internal bool RegisterTutorial(InitialTutorial guide)
    {
        if (FirstDayPreparationFinished || firstDayEventPresented || IncludesTutorialDay) return false;

        tutorial = guide;
        IncludesTutorialDay = true;
        currentDay = 0;
        UpdateDayLabel();
        return true;
    }

    internal void SkipTutorialDay()
    {
        if (currentDay == 0) skipTutorialDayPending = true;
    }

    private void Update()
    {
        if (!skipTutorialDayPending || currentDay != 0 || campaignTransitionStarted || feedingOpen ||
            !FirstDayPreparationFinished || GS2SceneFade.IsEnteringGameplay ||
            (actions != null && actions.IsBusy) || (hand != null && hand.IsDraggingCard)) return;

        if (timeline != null) timeline.ClosePanel();
        if (activeMissionsPanel != null) activeMissionsPanel.Close();
        if (merchant != null && merchant.IsOpen) merchant.HideShop();
        if (hand != null) hand.SuspendEntranceForEvent();
        BeginCampaignAfterTutorial(false);
    }

    private IEnumerator Start()
    {
        // O tutorial registra o Dia 0 no Start, antes de preparar o evento.
        yield return null;
        UpdateDayLabel();
        try
        {
            using (PrepareFirstDayMarker.Auto())
            {
                if (dayEvents != null) dayEvents.PrepareFirstDay(currentDay, seedCatalog);
                if (dayEvents != null) dayEvents.ApplyPendingPestRegression(seedCatalog);
                if (actions != null) actions.BeginNextDay();
                UpdateMarket();
            }
        }
        finally
        {
            FirstDayPreparationFinished = true;
        }
        while (GS2SceneFade.IsEnteringGameplay && !firstDayEventPresented) yield return null;
        ShowFirstDayEvent();
    }

    internal void RevealFirstDayBackdrop()
    {
        if (!firstDayEventPresented && dayEvents != null) dayEvents.RevealPreparedBackdrop();
    }

    internal void ShowFirstDayEvent()
    {
        if (firstDayEventPresented || GS2SceneFade.IsEnteringGameplay &&
            (dayEvents == null || !dayEvents.HasPreparedEvent)) return;
        firstDayEventPresented = true;
        using (ShowFirstEventMarker.Auto())
        {
            if (dayEvents != null) dayEvents.ShowPreparedEvent();
            OpenDayAfterEvent(false);
        }
    }

    public void FinishDay()
    {
        if (finishDayFeedback != null && finishDayFeedback.IsAnimating) return;
        if (!CanAcceptActions || resources == null || actions == null)
            return;
        if (actions.IsBusy)
        {
            if (finishDayFeedback != null) finishDayFeedback.ShakeRejected();
            feedback.ShowMessage(actions.GatheringNeedsPlayer ?
                "Inicie a coleta pendente pelo botão do trabalhador." :
                actions.HasPendingSelection ? "Escolha um terreno ou cancele a carta ativa." :
                "Aguarde o fim das ações.");
            return;
        }

        bool finalDay = currentDay >= totalDays;
#if UNITY_EDITOR
        finalDay |= previewFinalBalance.HasValue;
#endif
        if (!finalDay && feedingGame == null)
        {
            feedback?.ShowMessage("Minigame de alimentação não configurado.");
            return;
        }
        if (finalDay && !FindResultsView())
        {
            feedback?.ShowMessage("Tela de resultado não configurada.");
            return;
        }

#if UNITY_EDITOR
        if (previewFinalBalance.HasValue)
        {
            currentDay = totalDays;
            UpdateDayLabel();
        }
#endif
        feedingOpen = true;
        if (timeline != null) timeline.ClosePanel();
        if (activeMissionsPanel != null) activeMissionsPanel.Close();
        if (merchant != null && merchant.IsOpen) merchant.HideShop();
        if (finishDayFeedback != null) finishDayFeedback.PlayAcceptedClick();
        if (finishDayButton != null) finishDayButton.enabled = false;
        DayClosing?.Invoke();
        if (finalDay)
        {
            if (hand != null) hand.ExitForDay(null);
            FinishMatch();
            return;
        }

        void OpenFeeding() => feedingGame.Begin(workers, FinishFeeding, CancelFeeding);
        if (hand != null) hand.ExitForDay(OpenFeeding);
        else OpenFeeding();
    }

    private void CancelFeeding()
    {
        feedingOpen = false;
        if (finishDayButton != null) finishDayButton.enabled = true;
        if (hand != null) hand.ReturnAfterCancelledDay();
        DayClosingCancelled?.Invoke();
    }

    private void FinishFeeding(SaleItem?[] chosenFood)
    {
        if (!feedingOpen || gameOver || lastClosedDay == currentDay || currentDay >= totalDays) return;
        lastClosedDay = currentDay;

        for (int i = 0; i < workers.Length; i++)
        {
            WorkerAssignment worker = workers[i];
            if (worker == null || worker.IsUnavailableToday) continue;
            SaleItem? received = i < chosenFood.Length ? chosenFood[i] : null;
            if (received == SaleItem.Food) worker.ScheduleExtraAction();
            else if (!received.HasValue && currentDay > 0) missedMeals++;
        }

        if (cardChoice == null)
        {
            ContinueToNextDay();
            return;
        }

        Action onPicked = currentDay == 0 && tutorial != null
            ? tutorial.BeginDayOneFade : null;
        cardChoice.Begin(currentDay, ContinueToNextDay, onPicked);
    }

    private bool FindResultsView()
    {
        if (resultsView == null)
            resultsView = FindFirstObjectByType<SertaoMatchResultsView>(FindObjectsInactive.Include);
        if (resultsView != null) return resultsView.IsConfigured;
        var prefab = Resources.Load<GameObject>("SertaoFimDePartida");
        if (prefab == null || !prefab.TryGetComponent(out SertaoMatchResultsView template) ||
            !template.IsConfigured) return false;
        resultsView = Instantiate(template);
        return true;
    }

    private void FinishMatch()
    {
        gameOver = true;
        feedingOpen = false;
#if UNITY_EDITOR
        if (previewFinalBalance is int finalBalance)
        {
            resultsView.Show(SertaoMatchResult.CreatePreview(finalBalance, 0, GameBalance.Current), null);
            return;
        }
#endif
        int totalPlots = 0, developedPlots = 0;
        var seenPlots = new HashSet<Plot>();
        if (plots != null)
            foreach (Plot plot in plots)
            {
                if (plot == null || plot.IsWater || !seenPlots.Add(plot)) continue;
                totalPlots++;
                if (plot.IsPlowed || plot.IsFenced) developedPlots++;
            }

        int totalHouses = 0, builtHouses = 0;
        var seenHouses = new HashSet<HouseSite>();
        if (houseSites != null)
            foreach (HouseSite site in houseSites)
            {
                if (site == null || !seenHouses.Add(site)) continue;
                totalHouses++;
                if (site.IsBuilt) builtHouses++;
            }

        var result = new SertaoMatchResult(resources, missedMeals,
            missionBoard != null ? missionBoard.DeliveredCount : 0,
            missionBoard != null ? missionBoard.UndeliveredCount : 0,
            developedPlots, totalPlots, builtHouses, totalHouses, GameBalance.Current);
        resultsView.Show(result, () =>
        {
            resources.ApplyFinalBalance(result.FinalCoins);
            if (result.Won) GS2LeaderboardScore.RecordFinishedGame((int)result.FinalCoins);
        });
    }

    public void ContinueToNextDay()
    {
        if (!feedingOpen || gameOver || lastClosedDay != currentDay || currentDay >= totalDays) return;
        if (currentDay == 0)
        {
            BeginCampaignAfterTutorial(!InitialTutorial.SkipRequested);
            return;
        }
        actions.ReturnWorkersHome();
        BeginNextDay();
    }

    private void BeginCampaignAfterTutorial(bool showFarewell)
    {
        if (campaignTransitionStarted) return;
        campaignTransitionStarted = true;
        skipTutorialDayPending = false;
        feedingOpen = true;
        if (finishDayButton != null) finishDayButton.enabled = false;
        if (tutorial != null)
            StartCoroutine(tutorial.RevealDayOne(PrepareCampaignDay, StartCampaignEvent, showFarewell));
        else
        {
            PrepareCampaignDay();
            StartCampaignEvent();
        }
    }

    private bool PrepareCampaignDay()
    {
        EventCardView practiceCard = TutorialEventCard;
        if (practiceCard != null && practiceCard.IsOpen) practiceCard.CloseImmediately();
        actions?.ReturnWorkersHome();
        currentDay = 1;
        ProduceAnimalResources();
        dayEvents?.PrepareFirstDay(currentDay, seedCatalog);
        PrepareDayState(false);
        if (dayEvents == null || !dayEvents.HasPreparedEvent) return false;
        dayEvents.RevealPreparedBackdrop();
        return true;
    }

    private void StartCampaignEvent()
    {
        if (!campaignTransitionStarted) return;
        campaignTransitionStarted = false;
        dayEvents?.ShowPreparedEvent();
        OpenDayAfterEvent(true);
    }

    private void BeginNextDay()
    {
        currentDay++;
        ProduceAnimalResources();
        if (dayEvents != null) dayEvents.StartDay(currentDay, seedCatalog);
        PrepareDayState(true);
        OpenDayAfterEvent(true);
    }

    private void PrepareDayState(bool announceDay)
    {
        if (MandalaSynergy.Instance != null)
            MandalaSynergy.Instance.ApplyGrowthBonuses(currentDay, seedCatalog);
        if (dayEvents != null) dayEvents.ApplyPendingPestRegression(seedCatalog);
        actions.BeginNextDay();
        if (houseSites != null)
        {
            foreach (HouseSite site in houseSites)
            {
                if (site == null) continue;
                WorkerAssignment resident = site.CreateResident(currentDay);
                if (resident == null) continue;
                RegisterWorker(resident);
                actions.RegisterWorker(resident);
                if (dayEvents != null) dayEvents.RegisterWorker(resident);
                resident.BeginNextDay();
                resident.SetStatus("em casa");
            }
        }
        foreach (Plot plot in plots)
            if (plot != null) plot.RefreshGrowth(currentDay, seedCatalog);
        UpdateMarket();
        if (announceDay && feedback != null) feedback.ShowMessage("Dia " + currentDay + " de " + totalDays);
        if (announceDay && finishDayButton != null) finishDayButton.enabled = true;
        UpdateDayLabel();
    }

    private void OpenDayAfterEvent(bool addAnimalResources)
    {
        if (dayEvents != null && dayEvents.IsPanelOpen)
        {
            if (hand != null && !firstDayOpening) hand.SuspendEntranceForEvent();
            dayEvents.AfterEventCardClosed(() => ActivateDay(addAnimalResources));
            return;
        }
        ActivateDay(addAnimalResources);
    }

    private void ActivateDay(bool addAnimalResources)
    {
        if (currentDay == 0 && campaignTransitionStarted) return;
        firstDayOpening = false;
        feedingOpen = false;
        if (finishDayButton != null) finishDayButton.enabled = true;
        AudioManager.Play(SoundCue.DayStart);
        DayChanged?.Invoke();
        dayEvents?.ReleasePendingReward();
        if (addAnimalResources) AddDailyAnimalResources();
    }

    private void UpdateMarket()
    {
        if (merchant != null)
            merchant.SetDay(currentDay, missionBoard != null && missionBoard.HasDeliveryDueOn(currentDay));
        if (missionBoard != null)
            missionBoard.SetDay(currentDay, merchant != null && merchant.IsAvailable, CreateOfferForecast);
    }

    private DeliveryOfferForecast CreateOfferForecast() => new DeliveryOfferForecast(resources, plots,
        workers, seedCatalog, hand, dayEvents, currentDay, totalDays, todayMilk, todayEggs,
        milkPerCow, eggsPerChicken);

    private void UpdateDayLabel()
    {
        if (dayText != null)
            dayText.text = "Dia " + currentDay;
    }
}
