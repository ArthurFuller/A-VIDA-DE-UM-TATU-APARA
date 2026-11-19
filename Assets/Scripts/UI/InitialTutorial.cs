using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Page = TutorialPresentation.Page;

[DefaultExecutionOrder(100)]
public sealed class InitialTutorial : MonoBehaviour
{
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private Text description;

    private enum Stage
    {
        Event, Camera, Resources, Day, Timeline, Inspect, PlayCard, Action, Shop, ShopOverview,
        OrdersTab, OrdersIntro, AcceptOrder, Missions, EndDay, WaitForEndDay, Feeding, Reward, WaitForCampaign, Farewell, Done
    }

    private enum Lane { Event, Hud, Hand, Follow, ShopCard, ModalTop, Missions, EndDay, Feeding, Farewell }

    private static readonly Color TextColor = new Color(1f, 0.95f, 0.84f);
    private const int TutorialSortingOrder = 250;
    private readonly List<SpriteRenderer> workerSprites = new List<SpriteRenderer>();
    private readonly List<TutorialWorkerVisual> workerVisuals = new List<TutorialWorkerVisual>();
    private readonly List<Rect> cutouts = new List<Rect>(4);
    private readonly Vector3[] corners = new Vector3[4];

    private static InitialTutorial activeGuide;
    internal static bool IsGuiding => activeGuide != null && activeGuide.running;

    private Stage stage;
    private bool running;
    private bool canContinue;
    private bool playedCard;
    private bool sawFeeding;
    private bool sawTimelineRecord;
    private bool sawMissions;
    private bool noOrderToAccept;
    private int closingDay;
    private DeliveryMission acceptedMission;

    private DayCycle days;
    private EventCardView eventCard;
    private readonly List<RectTransform> resourceGraphics = new List<RectTransform>(18);
    private TopBarAutoHide hud;
    private DayCounterMotion dayCounter;
    private DayTimeline timeline;
    private ActivityCardAvailability hand;
    private ActionResolver actions;
    private SeedMerchant shop;
    private Button closeShop;
    private ShopTabs tabs;
    private DeliveryMissionBoard orders;
    private ActiveMissionsPanel missions;
    private Button openMissions;
    private FeedingMinigame feeding;
    private CardRewardChoice rewards;

    private RectTransform viewport;
    private RectTransform box;
    private Image boxImage;
    private Text hint;
    private LetterJumpText farewellText;
    private Image campaignCurtain;
    private Image campaignModalBlock;
    private Sprite farewellFrame;
    private Tween campaignFade;
    private Tween farewellMotion;
    private Action startCampaignEvent;
    private float blackoutStartedAt;
    private float farewellSlide;
    private Vector2 boxHome;
    private bool dayOneFading;
    private bool blackoutReached;
    private bool farewellButtonReady;

    internal const float CampaignFadeSeconds = .43f;
    private const float CampaignBlackoutSeconds = 1.5f;
    private const string FarewellMessage = "Chegou a hora de explorar o Sertão por conta própria. Boa sorte e divirta-se!";
    private Button next;
    private Button skip;
    private Text nextLabel;
    private Text skipLabel;
    private TutorialPresentation presentation;
    private Canvas handCanvas;
    private GraphicRaycaster addedHandRaycaster;
    private bool addedHandCanvas;
    private bool handOverrideSorting;
    private int handSortingOrder;
    private bool showHand;
    private WorkerAssignment highlightedWorker;
    private bool dimBackground;
    private bool needsHud;
    private ResourceHud resourceHud;
    private RectTransform highlightTarget;
    private Rect highlightViewport;
    private Rect highlightBounds;
    private Vector2 highlightScale;
    private Page visiblePage;
    private Vector2 placedOffset;
    private Vector2 placedSize;
    private bool highlightRequested;

    internal bool CanRun => isActiveAndEnabled && overlay != null && description != null;
    public bool HasVisiblePage => box != null && box.gameObject.activeSelf;
    public Page VisiblePage => visiblePage;
    public string DefaultPageText => message;
    private TutorialSpotlight spotlight;
    private Camera worldCamera;
    private string message;
    private Lane lane;
    private Lane placedLane;
    private Rect placedViewport;
    private RectTransform protectedPanel;
    private RectTransform placedPanel;
    private bool layoutValid;

    public const string SkipKey = "GS2.SkipTutorial";
    public static bool SkipRequested
    {
        get => PlayerPrefs.GetInt(SkipKey, 0) != 0;
        set { PlayerPrefs.SetInt(SkipKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private void Awake()
    {
        if (overlay == null) return;
        // The old frame includes a second sliced Image on a child.
        overlay.alpha = 0f;
        overlay.interactable = false;
        overlay.blocksRaycasts = false;
        foreach (Graphic graphic in overlay.GetComponentsInChildren<Graphic>(true))
            graphic.enabled = false;
        foreach (Transform child in overlay.transform)
            child.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (SkipRequested || !CanRun) return;
        days = FindFirstObjectByType<DayCycle>();
        if (days == null || !days.RegisterTutorial(this)) return;
        eventCard = days.TutorialEventCard;
        resourceHud = FindFirstObjectByType<ResourceHud>(FindObjectsInactive.Include);
        if (resourceHud != null) CacheResourceGraphics(resourceHud);
        hud = FindFirstObjectByType<TopBarAutoHide>(FindObjectsInactive.Include);
        if (hud != null && hud.isActiveAndEnabled) hud.SetTutorialHud(false);
        dayCounter = FindFirstObjectByType<DayCounterMotion>(FindObjectsInactive.Include);
        timeline = FindFirstObjectByType<DayTimeline>(FindObjectsInactive.Include);
        hand = FindFirstObjectByType<ActivityCardAvailability>(FindObjectsInactive.Include);
        actions = FindFirstObjectByType<ActionResolver>();
        shop = FindFirstObjectByType<SeedMerchant>(FindObjectsInactive.Include);
        closeShop = FindButton(shop, nameof(SeedMerchant.HideShop));
        tabs = FindFirstObjectByType<ShopTabs>(FindObjectsInactive.Include);
        orders = FindFirstObjectByType<DeliveryMissionBoard>(FindObjectsInactive.Include);
        missions = FindFirstObjectByType<ActiveMissionsPanel>(FindObjectsInactive.Include);
        openMissions = FindButton(missions, nameof(ActiveMissionsPanel.Toggle));
        feeding = FindFirstObjectByType<FeedingMinigame>(FindObjectsInactive.Include);
        rewards = FindFirstObjectByType<CardRewardChoice>(FindObjectsInactive.Include);
        worldCamera = Camera.main;

        presentation = Resources.Load<TutorialPresentation>(TutorialPresentation.ResourceName);
        BuildCanvas();
        days.DayClosing += OnClosingDay;
        days.DayClosingCancelled += OnClosingCancelled;
        if (actions != null) actions.CardPlayed += OnCardPlayed;
        if (orders != null) orders.MissionAccepted += OnMissionAccepted;
        running = true;
        activeGuide = this;
    }

    private void OnDisable()
    {
        if (hud != null) hud.ReleaseTutorialHud();
        HideHint();
    }

    private void OnDestroy()
    {
        if (days != null)
        {
            days.DayClosing -= OnClosingDay;
            days.DayClosingCancelled -= OnClosingCancelled;
        }
        if (actions != null) actions.CardPlayed -= OnCardPlayed;
        if (orders != null) orders.MissionAccepted -= OnMissionAccepted;
        SetHandOnTop(false);
        if (addedHandRaycaster != null) Destroy(addedHandRaycaster);
        if (addedHandCanvas && handCanvas != null) Destroy(handCanvas);
        if (activeGuide == this) activeGuide = null;
        campaignFade?.Kill();
        farewellMotion?.Kill();
        if (viewport != null) Destroy(viewport.gameObject);
    }

    public void Skip()
    {
        SkipRequested = true;
        days?.SkipTutorialDay();
        if (stage == Stage.Farewell) FinishFarewell();
        else Close();
    }

    public void Close()
    {
        running = false;
        stage = Stage.Done;
        if (hud != null) hud.ReleaseTutorialHud();
        HideHint();
    }

    private void LateUpdate()
    {
        if (!running) return;
        if (SkipRequested)
        {
            days?.SkipTutorialDay();
            if (stage == Stage.Farewell) FinishFarewell();
            else Close();
            return;
        }
        if (GS2SceneFade.IsEnteringGameplay || Time.timeScale <= 0f || SertaoCoinToss.IsOpen)
        {
            HideHint();
            return;
        }

        if (actions != null && actions.TutorialGatheringInView)
        {
            if (hud != null) hud.SetTutorialHud(false);
            HideHint();
            return;
        }

        message = null;
        needsHud = false;
        canContinue = false;
        protectedPanel = null;
        cutouts.Clear();
        highlightRequested = false;
        showHand = false;
        highlightedWorker = null;

        RectTransform visitButton = null;
        if ((stage is Stage.Action or Stage.Shop or Stage.Missions or Stage.EndDay) &&
            (shop == null || !shop.IsOpen) && (missions == null || !missions.IsOpen) &&
            actions != null && !actions.HasPendingSelection)
            visitButton = actions.TutorialGatheringVisitButton;

        if (visitButton != null)
        {
            // The entrance sequence targets the visual component, not the button's RectTransform.
            var visual = visitButton.GetComponent<GatheringVisitButtonVisual>();
            if ((visual == null || (!visual.IsExiting && !DOTween.IsTweening(visual))) &&
                Add(visitButton, 3f, fitImage: true))
                Prompt(Page.Follow, "Clique no botão para acompanhar o personagem.", Lane.Follow);
        }
        else
        {
            // Some transitions advance immediately without a page of their own.
            Stage previous;
            do
            {
                previous = stage;
                UpdateStep();
            } while (stage != previous && running && message == null);
        }

        if (!needsHud && hud != null) hud.SetTutorialHud(false);
        if (message == null)
        {
            HideHint();
            return;
        }

        if (!highlightRequested) highlightTarget = null;
        if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
        string text = presentation != null ? presentation.Text(visiblePage, message, days.TotalDays) : message;
        bool farewell = visiblePage == Page.Farewell;
        Text activeText = farewell ? farewellText : hint;
        if (hint.gameObject.activeSelf == farewell) hint.gameObject.SetActive(!farewell);
        if (farewellText.gameObject.activeSelf != farewell) farewellText.gameObject.SetActive(farewell);
        if (activeText.text != text) activeText.text = text;
        if (next.gameObject.activeSelf != canContinue) next.gameObject.SetActive(canContinue);
        if (skip.gameObject.activeSelf == farewell) skip.gameObject.SetActive(!farewell);
        UpdateAppearance();
        if (farewell)
        {
            if (farewellMotion == null && !farewellButtonReady) PlayFarewellEntrance();
            box.anchoredPosition = boxHome - Vector2.up * ((1f - farewellSlide) *
                (viewport.rect.height + box.rect.height));
        }
        SetHandOnTop(showHand);
        RefreshWorkerVisuals(highlightedWorker);
        spotlight.SetCutouts(cutouts, dimBackground);
    }

    private void UpdateStep()
    {
        switch (stage)
        {
            case Stage.Event:
                if (days.CanAcceptActions) { stage = Stage.Camera; break; }
                if (eventCard != null && eventCard.TutorialRevealReady && Add(eventCard.TutorialRevealTarget))
                    Prompt(Page.Event, "Todo dia tem um evento. Clique em Revelar para ler o que aconteceu.", Lane.Event);
                break;
            case Stage.Camera:
                Prompt(Page.Camera, "Mova a câmera com W, A, S e D.\nAjuste o zoom com a rodinha do mouse.",
                    Lane.ModalTop, true);
                break;
            case Stage.Resources:
                if (!PrepareHud()) break;
                AddResources();
                Prompt(Page.Resources, "Aqui ficam os recursos e as moedas da comunidade.", Lane.Hud, true);
                break;
            case Stage.Day:
                if (!PrepareHud()) break;
                Add(dayCounter != null ? dayCounter.TutorialHitArea : null, 1f);
                Prompt(Page.Day, "Você está no Dia 0. Depois do tutorial, a partida dura " + days.TotalDays + " dias.", Lane.Hud, true);
                break;
            case Stage.Timeline:
                UpdateTimeline();
                break;
            case Stage.Inspect:
                if (playedCard) { stage = Stage.Action; break; }
                if (timeline == null || timeline.TutorialHandInspection != null) { stage = Stage.PlayCard; break; }
                showHand = true;
                Prompt(Page.Inspect, "Algumas cartas têm bônus específicos. Clique em qualquer carta para ler a descrição.",
                    Lane.Hand, hand == null || !hand.TutorialHasCards);
                break;
            case Stage.PlayCard:
                if (playedCard) { stage = Stage.Action; break; }
                if ((timeline != null && timeline.TutorialHandInspection != null) ||
                    (actions != null && actions.HasPendingSelection)) break;
                showHand = true;
                highlightedWorker = days.TutorialWorker;
                Prompt(Page.PlayCard, "Cada ponto acima do tatu vale uma ação. Arraste uma carta até ele.",
                    Lane.Hand, actions == null || highlightedWorker == null || hand == null || !hand.TutorialHasCards);
                break;
            case Stage.Action:
                if (actions == null || !actions.IsBusy) stage = Stage.Shop;
                break;
            case Stage.Shop:
                if (shop == null || !shop.IsAvailable) { stage = Stage.Missions; break; }
                if (shop.IsOpen) { stage = Stage.ShopOverview; break; }
                if (Add(hand != null ? hand.TutorialShopCard : null))
                    Prompt(Page.Shop, "Clique na carta Mercearia para abrir a loja.", Lane.ShopCard);
                break;
            case Stage.ShopOverview:
            case Stage.OrdersTab:
            case Stage.OrdersIntro:
            case Stage.AcceptOrder:
                UpdateShop();
                break;
            case Stage.Missions:
                UpdateMissions();
                break;
            case Stage.EndDay:
                Add(ButtonRect(days.TutorialFinishButton));
                Prompt(Page.EndDay, "Quando terminar suas tarefas, clique em Finalizar dia.", Lane.EndDay, true);
                break;
            case Stage.Feeding:
                UpdateFeeding();
                break;
            case Stage.Reward:
                if (rewards != null && rewards.TutorialCardPicked) { stage = Stage.WaitForCampaign; break; }
                if (rewards != null && rewards.TutorialReady)
                    Prompt(Page.Reward, "Se houver espaço na mão, você pode escolher uma carta para o dia seguinte.",
                        Lane.ModalTop, panel: rewards.TutorialPanel);
                break;
            case Stage.Farewell:
                Prompt(Page.Farewell, FarewellMessage, Lane.Farewell, farewellButtonReady);
                break;
        }
    }

    private void UpdateTimeline()
    {
        if (timeline == null) { stage = Stage.Inspect; return; }
        const string explanation = "Na Linha do Tempo, escolha um dia para ver seus eventos e cartas usadas.";
        if (!timeline.IsOpen)
        {
            if (sawTimelineRecord) { stage = Stage.Inspect; return; }
            if (PrepareHud(timeline.TutorialTrigger) && Add(timeline.TutorialTrigger, 0f, new Vector2(.65f, .57f)))
                Prompt(Page.Timeline, explanation, Lane.Hud);
            return;
        }

        PrepareHud(timeline.TutorialTrigger);
        bool hasRecord = timeline.TutorialHistory != null;
        if (hasRecord) sawTimelineRecord = true;
        RectTransform day = timeline.TutorialDay(days.CurrentDay);
        // The panel slides in before its day button settles. Keep the shade closed between targets.
        Add(hasRecord ? timeline.TimelinePanel : day, hasRecord ? 4f : 2f);
        Prompt(hasRecord ? Page.TimelineRecord : Page.TimelineDay, explanation, Lane.Hud, hasRecord);
    }

    private void UpdateShop()
    {
        if (!shop.IsOpen)
        {
            if (!shop.TutorialPanelSettled) return;
            stage = acceptedMission != null || noOrderToAccept ? Stage.Missions : Stage.Shop;
            return;
        }

        if (!shop.TutorialPanelSettled || (tabs != null && !tabs.TutorialTabsReady)) return;
        RectTransform panel = shop.TutorialPanel;
        if (stage == Stage.ShopOverview)
        {
            Add(panel, 4f);
            Prompt(Page.ShopOverview, "Na mercearia, compre itens ou venda recursos por moedas.", Lane.ModalTop, true, panel);
            return;
        }

        if (stage == Stage.AcceptOrder && acceptedMission != null)
        {
            Add(ButtonRect(closeShop));
            Prompt(Page.CloseShop, "Encomenda aceita. Feche a mercearia para ver suas missões.", Lane.ModalTop, panel: panel);
            return;
        }

        if (tabs == null) { stage = Stage.Missions; return; }
        if (!tabs.TutorialOrdersSelected)
        {
            stage = Stage.OrdersTab;
            if (Add(tabs.TutorialOrdersTab))
                Prompt(Page.OrdersTab, "Clique na aba Encomendas para ver os pedidos.", Lane.ModalTop, panel: panel);
            return;
        }

        if (stage == Stage.OrdersTab) { stage = Stage.OrdersIntro; return; }
        if (!tabs.TutorialOrderReady) return;

        if (stage == Stage.OrdersIntro)
        {
            Add(tabs.TutorialOrderDetails);
            Prompt(Page.OrdersIntro, "Entregue os itens no prazo para ganhar moedas. As entregas também liberam novas casas para a comunidade.", Lane.ModalTop, true, panel);
            return;
        }

        if (orders == null || !orders.TutorialOfferAvailable)
        {
            noOrderToAccept = true;
            Add(ButtonRect(closeShop));
            Prompt(Page.CloseShop, "Feche a mercearia para abrir Missões.", Lane.ModalTop, panel: panel);
        }
        else
        {
            Add(orders.TutorialOfferButton);
            Prompt(Page.AcceptOrder, orders.TutorialCanAcceptSelected
                ? "Clique em Aceitar para pegar a encomenda e depois feche a mercearia."
                : "Clique em Próximo pedido para encontrar uma encomenda disponível.", Lane.ModalTop, panel: panel);
        }
    }

    private void UpdateMissions()
    {
        if (shop != null && (shop.IsOpen || !shop.TutorialPanelSettled))
        {
            if (shop.IsOpen && Add(ButtonRect(closeShop)))
                Prompt(Page.CloseShop, "Feche a mercearia para abrir Missões.", Lane.ModalTop, panel: shop.TutorialPanel);
            return;
        }
        if (missions == null || openMissions == null) { stage = Stage.EndDay; return; }
        const string explanation = "Em Missões, confira os itens e o prazo das encomendas que você aceitou.";
        if (missions.IsOpen)
        {
            sawMissions = true;
            PrepareHud(openMissions.transform as RectTransform);
            RectTransform panel = missions.TutorialList;
            if (panel != null && DOTween.IsTweening(panel)) return;
            Add(panel, 4f);
            Prompt(Page.Missions, explanation, Lane.Missions, true);
        }
        else if (sawMissions) stage = Stage.EndDay;
        else if (PrepareHud(openMissions.transform as RectTransform) && Add(ButtonRect(openMissions)))
            Prompt(Page.Missions, explanation, Lane.Missions);
    }

    private void UpdateFeeding()
    {
        if (feeding == null || days.CurrentDay > closingDay || (rewards != null && rewards.TutorialReady))
        { stage = Stage.Reward; return; }
        if (!feeding.TutorialOpen)
        {
            if (sawFeeding) stage = Stage.Reward;
            return;
        }
        sawFeeding = true;
        if (feeding.TutorialReady)
            Prompt(Page.Feeding, "Arraste comida até os tatus e confirme. Comida preparada dá uma ação extra amanhã.",
                Lane.Feeding);
    }

    private void Advance()
    {
        if (!running || !canContinue) return;
        switch (stage)
        {
            case Stage.Camera: stage = Stage.Resources; break;
            case Stage.Resources: stage = Stage.Day; break;
            case Stage.Day: stage = Stage.Timeline; break;
            case Stage.Timeline:
                timeline.ClosePanel();
                stage = Stage.Inspect;
                break;
            case Stage.Inspect: stage = Stage.PlayCard; break;
            case Stage.PlayCard: stage = Stage.Shop; break;
            case Stage.ShopOverview: stage = Stage.OrdersTab; break;
            case Stage.OrdersIntro: stage = Stage.AcceptOrder; break;
            case Stage.Farewell:
                FinishFarewell();
                break;
            case Stage.Missions:
                missions.Close();
                stage = Stage.EndDay;
                break;
            case Stage.EndDay:
                stage = Stage.WaitForEndDay;
                HideHint();
                break;
        }
        canContinue = false;
    }

    private void OnCardPlayed(int day, ActivityCardDrag card)
    {
        if (running && card != null && card.Kind != ActivityKind.BuySeeds) playedCard = true;
    }

    private void OnMissionAccepted(DeliveryMission mission)
    {
        if (running) acceptedMission = mission;
    }

    private void OnClosingDay()
    {
        if (!running) return;
        if (days.CurrentDay >= days.TotalDays) { Close(); return; }
        closingDay = days.CurrentDay;
        sawFeeding = false;
        stage = Stage.Feeding;
        if (hud != null) hud.SetTutorialHud(false);
        HideHint();
    }

    private void OnClosingCancelled()
    {
        if (running) stage = Stage.WaitForEndDay;
    }

    private bool PrepareHud(RectTransform allowedControl = null)
    {
        needsHud = true;
        if (hud == null) return true;
        hud.SetTutorialHud(true, allowedControl);
        return hud.PrepareResourceFeedback() <= .01f;
    }

    private void Prompt(Page page, string text, Lane position, bool advance = false, RectTransform panel = null)
    {
        visiblePage = page;
        dimBackground = page is not (Page.Camera or Page.Feeding or Page.Reward or Page.Farewell);
        message = text;
        lane = position;
        canContinue = advance;
        protectedPanel = panel;
    }

    private void HideHint()
    {
        if (box != null && box.gameObject.activeSelf) box.gameObject.SetActive(false);
        if (spotlight != null) spotlight.SetCutouts(null, false);
        SetHandOnTop(false);
        RefreshWorkerVisuals(null);
        highlightTarget = null;
        layoutValid = false;
    }

    internal void BeginDayOneFade()
    {
        if (dayOneFading || campaignCurtain == null) return;
        dayOneFading = true;
        stage = Stage.WaitForCampaign;
        if (hud != null) hud.SetTutorialHud(false);
        HideHint();
        campaignCurtain.gameObject.SetActive(true);
        campaignCurtain.transform.SetAsLastSibling();
        campaignFade = campaignCurtain.DOFade(1f, CampaignFadeSeconds).SetUpdate(true)
            .SetEase(Ease.InOutSine).OnComplete(() =>
            {
                blackoutReached = true;
                blackoutStartedAt = Time.unscaledTime;
            });
    }

    internal IEnumerator RevealDayOne(Func<bool> prepareDay, Action showEvent, bool withFarewell)
    {
        if (campaignCurtain == null)
        {
            prepareDay();
            showEvent();
            yield break;
        }

        BeginDayOneFade();
        while (!blackoutReached) yield return null;
        bool eventHasBackdrop = prepareDay();
        campaignModalBlock.color = eventHasBackdrop ? Color.clear : new Color(0f, 0f, 0f, .64f);
        campaignModalBlock.gameObject.SetActive(eventHasBackdrop || withFarewell);
        float remaining = CampaignBlackoutSeconds - (Time.unscaledTime - blackoutStartedAt);
        if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

        campaignFade = campaignCurtain.DOFade(0f, CampaignFadeSeconds).SetUpdate(true)
            .SetEase(Ease.InOutSine);
        yield return campaignFade.WaitForCompletion();
        campaignCurtain.gameObject.SetActive(false);
        startCampaignEvent = showEvent;
        if (withFarewell && CanRun && !SkipRequested)
        {
            stage = Stage.Farewell;
            running = true;
        }
        else FinishFarewell();
    }

    private void PlayFarewellEntrance()
    {
        float seconds = Mathf.Clamp(farewellText.text.Length * .027f, .7f, 3.3f);
        farewellSlide = 0f;
        AudioManager.Play(SoundCue.UiPaper);
        farewellMotion = DOTween.Sequence().SetUpdate(true)
            .Append(DOTween.To(() => farewellSlide, value => farewellSlide = value, 1f, .45f)
                .SetEase(Ease.OutCubic))
            .Append(farewellText.PrepareTimedReveal(farewellText.text, seconds))
            .OnComplete(() =>
            {
                farewellMotion = null;
                farewellButtonReady = true;
                next.gameObject.SetActive(true);
                if (next is TutorialButtonFeedback feedback) feedback.PlayEntrance();
            });
    }

    private void FinishFarewell()
    {
        Action showEvent = startCampaignEvent;
        startCampaignEvent = null;
        farewellMotion?.Kill();
        farewellMotion = null;
        showEvent?.Invoke();
        if (campaignModalBlock != null) campaignModalBlock.gameObject.SetActive(false);
        Close();
    }

    private void BuildCanvas()
    {
        Canvas parentCanvas = overlay.GetComponentInParent<Canvas>();
        Canvas original = parentCanvas != null ? parentCanvas.rootCanvas : null;
        var surface = new GameObject("Tutorial Guiado", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(surface, gameObject.scene);
        viewport = (RectTransform)surface.transform;
        Canvas canvas = surface.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (original != null) canvas.targetDisplay = original.targetDisplay;
        canvas.sortingOrder = TutorialSortingOrder;
        CanvasScaler source = original != null ? original.GetComponent<CanvasScaler>() : null;
        if (source != null)
        {
            CanvasScaler scaler = surface.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = source.uiScaleMode;
            scaler.referenceResolution = source.referenceResolution;
            scaler.screenMatchMode = source.screenMatchMode;
            scaler.matchWidthOrHeight = source.matchWidthOrHeight;
            scaler.scaleFactor = source.scaleFactor;
            scaler.referencePixelsPerUnit = source.referencePixelsPerUnit;
        }

        var shade = new GameObject("Sombra recortada", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TutorialSpotlight));
        Stretch((RectTransform)shade.transform, viewport, Vector2.zero, Vector2.zero);
        spotlight = shade.GetComponent<TutorialSpotlight>();
        spotlight.raycastTarget = false;

        campaignModalBlock = CreateImage("Entrada no Dia 1", viewport, Color.clear);
        campaignModalBlock.canvasRenderer.cullTransparentMesh = false;
        Stretch(campaignModalBlock.rectTransform, viewport, Vector2.zero, Vector2.zero);
        FullscreenBackdrop.Cover(campaignModalBlock);
        campaignModalBlock.gameObject.SetActive(false);

        campaignCurtain = CreateImage("Transição do Dia 1", viewport, Color.clear);
        campaignCurtain.canvasRenderer.cullTransparentMesh = false;
        Stretch(campaignCurtain.rectTransform, viewport, Vector2.zero, Vector2.zero);
        FullscreenBackdrop.Cover(campaignCurtain);
        campaignCurtain.gameObject.SetActive(false);

        Image oldFrame = description.rectTransform.parent != null
            ? description.rectTransform.parent.GetComponent<Image>() : null;
        if (oldFrame != null) farewellFrame = oldFrame.sprite;
        boxImage = CreateImage("Caixa do tutorial", viewport, new Color(0f, 0f, 0f, 0.84f));
        boxImage.raycastTarget = false;
        box = boxImage.rectTransform;
        hint = CreateText("Instrução", box, "", 13, TextColor, TextAnchor.MiddleLeft);
        hint.resizeTextForBestFit = true;
        hint.resizeTextMinSize = 11;
        hint.resizeTextMaxSize = 13;
        Stretch(hint.rectTransform, box, new Vector2(10f, 23f), new Vector2(-10f, -5f));
        var farewellLine = new GameObject("Despedida", typeof(RectTransform), typeof(CanvasRenderer), typeof(LetterJumpText));
        farewellText = farewellLine.GetComponent<LetterJumpText>();
        farewellText.font = description.font;
        farewellText.raycastTarget = false;
        Stretch(farewellText.rectTransform, box, new Vector2(24f, 38f), new Vector2(-24f, -18f));
        farewellLine.SetActive(false);
        TutorialButtonFeedback continueButton = CreateButton("Continuar", true, out nextLabel);
        continueButton.onClick.AddListener(() =>
        {
            Page page = visiblePage;
            continueButton.PlayClick(() => { if (visiblePage == page) Advance(); });
        });
        next = continueButton;
        TutorialButtonFeedback skipButton = CreateButton("Pular tutorial", false, out skipLabel);
        skipButton.onClick.AddListener(() => skipButton.PlayClick(Skip));
        skip = skipButton;
        HideHint();
    }

    private TutorialButtonFeedback CreateButton(string label, bool primary, out Text text)
    {
        Image hitArea = CreateImage(label, box, Color.clear);
        hitArea.canvasRenderer.cullTransparentMesh = false;
        RectTransform rect = hitArea.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(primary ? 1f : 0f, 0f);
        rect.anchoredPosition = new Vector2(primary ? -10f : 10f, 5f);
        rect.sizeDelta = new Vector2(primary ? 62f : 76f, 17f);

        Image image = CreateImage("Visual", rect, primary
            ? new Color(0.91f, 0.78f, 0.53f)
            : new Color(0.88f, 0.77f, 0.56f, 0.15f));
        image.raycastTarget = false;
        Stretch(image.rectTransform, rect, Vector2.zero, Vector2.zero);
        var button = hitArea.gameObject.AddComponent<TutorialButtonFeedback>();
        button.Configure(image, box);
        text = CreateText("Rótulo", image.rectTransform, label, 10, primary ? Color.black : TextColor, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, image.rectTransform, Vector2.zero, Vector2.zero);
        SertaoButtonAudio.Bind(button);
        return button;
    }

    private void UpdateAppearance()
    {
        Vector2 offset = Vector2.zero;
        Vector2 size = new Vector2(lane == Lane.Event ? 205f : 234f, 61f);
        if (presentation != null)
        {
            TutorialPresentation.BoxAppearance look = presentation.Box(visiblePage);
            offset = presentation.Offset(visiblePage);
            size = look.size;
            bool farewell = visiblePage == Page.Farewell;
            Sprite frame = look.sprite != null ? look.sprite : farewell && look.useDefaultFrame ? farewellFrame : null;
            Color tint = farewell && look.useDefaultFrame && frame == null ? new Color(.96f, .86f, .68f) : look.tint;
            ApplyImage(boxImage, frame, tint, look.spriteLayout, look.borderThickness);
            ApplyTypography(farewell ? farewellText : hint, look.typography);
            ApplyButton(skip, skipLabel, presentation.SkipSprite, presentation.ShowSkipLabel, presentation.SkipStyle);
            ApplyButton(next, nextLabel,
                farewell ? presentation.FarewellContinueSprite : presentation.ContinueSprite,
                farewell ? presentation.ShowFarewellContinueLabel : presentation.ShowContinueLabel,
                farewell ? presentation.FarewellContinueStyle : presentation.ContinueStyle);
        }

        if (!layoutValid || placedLane != lane || placedViewport != viewport.rect ||
            placedPanel != protectedPanel || placedOffset != offset || placedSize != size) PlaceBox(offset, size);
    }

    private void ApplyButton(Button button, Text label, Sprite sprite, bool showLabel,
        TutorialPresentation.ButtonAppearance look)
    {
        var image = (Image)button.targetGraphic;
        ApplyImage(image, sprite, sprite != null ? look.spriteTint : look.fillTint,
            look.spriteLayout, look.borderThickness);
        var rect = (RectTransform)button.transform;
        Vector2 anchor = Vector2.Min(Vector2.one, Vector2.Max(Vector2.zero, look.anchor));
        Vector2 size = Vector2.Max(Vector2.one, look.size);
        if (rect.anchorMin != anchor || rect.anchorMax != anchor || rect.pivot != anchor)
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        if (rect.anchoredPosition != look.offset) rect.anchoredPosition = look.offset;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        ColorBlock colors = SertaoButtonHoverPalette.WithFeedbackColors(look.colors);
        if (!button.colors.Equals(colors)) button.colors = colors;
        if (label.enabled != showLabel) label.enabled = showLabel;
        if (label.text != look.caption) label.text = look.caption;
        ApplyTypography(label, look.typography);
    }

    private static void ApplyImage(Image image, Sprite sprite, Color tint,
        TutorialPresentation.SpriteLayout layout, float borderThickness)
    {
        if (image.sprite != sprite) image.sprite = sprite;
        image.color = tint;
        image.type = sprite != null && layout == TutorialPresentation.SpriteLayout.Sliced
            ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = sprite != null && layout == TutorialPresentation.SpriteLayout.Fit;
        // uGUI divides the sprite borders by its pixels-per-unit multiplier.
        float multiplier = image.type == Image.Type.Sliced && borderThickness > 0f
            ? 1f / Mathf.Max(.05f, borderThickness) : 1f;
        if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier))
            image.pixelsPerUnitMultiplier = multiplier;
    }

    private void ApplyTypography(Text text, TutorialPresentation.TextAppearance look)
    {
        Font font = look.font != null ? look.font : description.font;
        int size = Mathf.Max(1, look.size);
        int minimumSize = Mathf.Clamp(look.minimumSize, 1, size);
        if (text.font != font) text.font = font;
        text.color = look.color;
        text.alignment = look.alignment;
        text.fontStyle = look.fontStyle;
        text.fontSize = size;
        text.lineSpacing = Mathf.Max(.1f, look.lineSpacing);
        text.resizeTextForBestFit = look.fit;
        text.resizeTextMinSize = minimumSize;
        text.resizeTextMaxSize = size;

        var insets = look.insets;
        Vector2 min = new Vector2(insets.left, insets.bottom);
        Vector2 max = new Vector2(-insets.right, -insets.top);
        RectTransform rect = text.rectTransform;
        if (rect.offsetMin != min) rect.offsetMin = min;
        if (rect.offsetMax != max) rect.offsetMax = max;
    }

    private Text CreateText(string name, RectTransform parent, string content, int size, Color tint, TextAnchor alignment)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        item.transform.SetParent(parent, false);
        Text text = item.GetComponent<Text>();
        text.font = description.font;
        text.fontSize = size;
        text.text = content;
        text.color = tint;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string name, RectTransform parent, Color tint)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.color = tint;
        return image;
    }

    private static void Stretch(RectTransform rect, RectTransform parent, Vector2 min, Vector2 max)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    private void PlaceBox(Vector2 offset, Vector2 size)
    {
        layoutValid = true;
        placedLane = lane;
        placedOffset = offset;
        placedSize = size;
        placedPanel = protectedPanel;
        Rect area = placedViewport = viewport.rect;
        const float gap = 10f;
        float width = Mathf.Clamp(size.x, 1f, Mathf.Max(1f, area.width - gap * 2f));
        float height = Mathf.Clamp(size.y, 1f, Mathf.Max(1f, area.height - gap * 2f));
        box.sizeDelta = new Vector2(width, height);
        Vector2 center = lane switch
        {
            Lane.Event => new Vector2(area.xMax - width * .5f - gap, area.center.y),
            Lane.Hud => new Vector2(area.center.x, area.yMax - area.height * .36f - height * .5f),
            Lane.Hand => new Vector2(area.xMin + width * .5f + gap, area.center.y + height * .5f),
            Lane.Feeding => new Vector2(area.xMin + width * .5f + gap, area.yMax - gap - height * .5f),
            Lane.Follow or Lane.Farewell => area.center,
            Lane.ShopCard => new Vector2(area.xMin + width * .5f + gap, area.yMin + area.height * .28f + height * .5f),
            Lane.ModalTop => ModalCenter(area, width, height, gap),
            Lane.Missions => new Vector2(area.xMin + area.width * .31f, area.yMax - area.height * .36f - height * .5f),
            _ => new Vector2(area.xMax - width * .5f - gap, area.yMin + area.height * .25f + height * .5f)
        };
        center += offset;
        boxHome = new Vector2(
            Mathf.Clamp(center.x, area.xMin + width * .5f + gap, area.xMax - width * .5f - gap),
            Mathf.Clamp(center.y, area.yMin + height * .5f + gap, area.yMax - height * .5f - gap)) - area.center;
        box.anchoredPosition = boxHome;
    }

    private Vector2 ModalCenter(Rect area, float width, float height, float gap)
    {
        float top = area.yMax - gap - height * .5f;
        if (!TryGetRect(protectedPanel, out Rect panel) || panel.yMax + height + gap * 2f <= area.yMax)
            return new Vector2(area.center.x, top);
        if (panel.xMin - area.xMin >= width + gap * 2f)
            return new Vector2(area.xMin + width * .5f + gap, top);
        if (area.xMax - panel.xMax >= width + gap * 2f)
            return new Vector2(area.xMax - width * .5f - gap, top);
        if (panel.yMin - area.yMin >= height + gap * 2f)
            return new Vector2(area.center.x, area.yMin + gap + height * .5f);
        return new Vector2(area.center.x, top);
    }

    private void CacheResourceGraphics(ResourceHud resources)
    {
        // In this HUD the counter containers have no size. Their icon and text define the bounds.
        foreach (Text counter in resources.TutorialCounters())
        {
            if (counter == null) continue;
            resourceGraphics.Add(counter.rectTransform);
            if (counter.transform.parent == null) continue;
            foreach (Transform child in counter.transform.parent)
                if (child.TryGetComponent(out Image icon)) resourceGraphics.Add(icon.rectTransform);
        }
    }

    private void SetHandOnTop(bool raised)
    {
        if (raised && handCanvas == null && hand != null)
        {
            GameObject container = hand.transform.parent.gameObject;
            handCanvas = container.GetComponent<Canvas>();
            addedHandCanvas = handCanvas == null;
            if (addedHandCanvas) handCanvas = container.AddComponent<Canvas>();
            handOverrideSorting = handCanvas.overrideSorting;
            handSortingOrder = handCanvas.sortingOrder;
            if (container.GetComponent<GraphicRaycaster>() == null)
                addedHandRaycaster = container.AddComponent<GraphicRaycaster>();
        }
        if (handCanvas == null) return;
        bool overrideSorting = raised || handOverrideSorting;
        int sortingOrder = raised ? TutorialSortingOrder + 1 : handSortingOrder;
        if (handCanvas.overrideSorting != overrideSorting) handCanvas.overrideSorting = overrideSorting;
        if (handCanvas.sortingOrder != sortingOrder) handCanvas.sortingOrder = sortingOrder;
    }

    private void RefreshWorkerVisuals(WorkerAssignment worker)
    {
        workerVisuals.RemoveAll(visual => visual == null);
        workerSprites.Clear();
        if (worker != null && worldCamera != null && viewport != null && box != null)
            worker.GetTutorialSprites(workerSprites);
        int shown = 0;
        foreach (SpriteRenderer sprite in workerSprites)
        {
            if (sprite == null || !sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite == null) continue;
            if (shown == workerVisuals.Count)
            {
                var node = new GameObject("Tatu em destaque", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TutorialWorkerVisual));
                Stretch((RectTransform)node.transform, viewport, Vector2.zero, Vector2.zero);
                node.transform.SetSiblingIndex(box.GetSiblingIndex());
                var visual = node.GetComponent<TutorialWorkerVisual>();
                visual.raycastTarget = false;
                workerVisuals.Add(visual);
            }

            TutorialWorkerVisual image = workerVisuals[shown++];
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            image.Refresh(sprite, worldCamera);
        }
        for (int i = shown; i < workerVisuals.Count; i++)
            if (workerVisuals[i].gameObject.activeSelf) workerVisuals[i].gameObject.SetActive(false);
    }

    private void AddResources()
    {
        if (resourceHud == null) return;
        RectTransform target = resourceHud.transform as RectTransform;
        if (WriteCachedHighlight(target, Vector2.one)) return;
        Rect area = default;
        foreach (RectTransform graphic in resourceGraphics)
            if (TryGetRect(graphic, out Rect item)) area = Union(area, item);
        if (area.width <= 0f || area.height <= 0f) return;

        Vector2 min = Vector2.Max(viewport.rect.min, area.min - Vector2.one * 6f);
        Vector2 max = Vector2.Min(viewport.rect.max, area.max + Vector2.one * 6f);
        StoreHighlight(target, Vector2.one, Rect.MinMaxRect(min.x + 5f, min.y + 20f, max.x - 8f, max.y - 5f));
    }

    private bool Add(RectTransform target, float padding = 5f, Vector2 scale = default, bool fitImage = false)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        if (scale == Vector2.zero) scale = Vector2.one;
        if (WriteCachedHighlight(target, scale)) return true;
        if (!TryGetRect(target, out Rect rect, fitImage)) return false;
        Vector2 halfSize = Vector2.Scale(rect.size * .5f, scale) + Vector2.one * padding;
        return StoreHighlight(target, scale, new Rect(rect.center - halfSize, halfSize * 2f));
    }

    private bool WriteCachedHighlight(RectTransform target, Vector2 scale)
    {
        if (target == null || target != highlightTarget || scale != highlightScale ||
            highlightViewport != viewport.rect) return false;
        cutouts.Add(highlightBounds);
        highlightRequested = true;
        return true;
    }

    private bool StoreHighlight(RectTransform target, Vector2 scale, Rect rect)
    {
        Vector2 min = Vector2.Max(viewport.rect.min, rect.min);
        Vector2 max = Vector2.Min(viewport.rect.max, rect.max);
        if (max.x - min.x < 2f || max.y - min.y < 2f) return false;
        highlightTarget = target;
        highlightViewport = viewport.rect;
        highlightScale = scale;
        highlightBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        cutouts.Add(highlightBounds);
        highlightRequested = true;
        return true;
    }

    private bool TryGetRect(RectTransform target, out Rect rect, bool fitImage = false)
    {
        rect = default;
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        Camera camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        if (fitImage && target.TryGetComponent(out Image image) && image.preserveAspect && image.sprite != null &&
            image.type == Image.Type.Simple)
        {
            Rect bounds = image.GetPixelAdjustedRect();
            Vector2 spriteSize = image.sprite.rect.size;
            float scale = Mathf.Min(bounds.width / spriteSize.x, bounds.height / spriteSize.y);
            Vector2 size = spriteSize * scale;
            Vector2 origin = bounds.min + Vector2.Scale(bounds.size - size, target.pivot);
            corners[0] = target.TransformPoint(origin);
            corners[1] = target.TransformPoint(origin + new Vector2(0f, size.y));
            corners[2] = target.TransformPoint(origin + size);
            corners[3] = target.TransformPoint(origin + new Vector2(size.x, 0f));
        }
        else target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screen, null, out Vector2 point))
                return false;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.width > 0f && rect.height > 0f;
    }

    private static Rect Union(Rect first, Rect second) =>
        first.width <= 0f || first.height <= 0f ? second :
            Rect.MinMaxRect(Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
                Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));

    private static RectTransform ButtonRect(Button button) =>
        button != null && button.isActiveAndEnabled && button.IsInteractable() ? button.transform as RectTransform : null;

    private static Button FindButton(Component receiver, string method)
    {
        if (receiver == null) return null;
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == receiver &&
                    button.onClick.GetPersistentMethodName(i) == method) return button;
        return null;
    }
}
