using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class ActivityCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private string activityName;
    [SerializeField, TextArea(2, 3)] private string description;
    [SerializeField] private ActivityKind activity;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private DragCursorPreview preview;
    [SerializeField] private SeedMerchant shop;
    [Header("Dissolucao descartavel")]
    [SerializeField, Min(0.05f)] private float dissolveMoveDuration = 0.55f;
    [SerializeField, Min(0f)] private float dissolvePause = 0.2f;
    [SerializeField, Min(0.05f)] private float dissolveDuration = 1.4f;
    [SerializeField, Min(1f)] private float dissolveCenterScale = 1.7f;
    [Header("Transformação do Replay")]
    [SerializeField, Min(1f)] private float replayCenterScale = 1.62f;
    [SerializeField, Min(0.05f)] private float replayCenterDuration = 0.64f;
    [SerializeField, Min(0.05f)] private float replayDeliveryDuration = 0.54f;
    [SerializeField, Min(0.05f)] private float replaySelectionDuration = 0.58f;

    private WorkerAssignment hoveredWorker;
    private bool dragging;
    private bool cancelled;
    private int originalSibling;
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private bool hovered;
    private bool returning;
    private bool shopClicking;
    private Tween shopClickTween;
    private Tween shopHoverTween;
    private Vector3 dragWorldOffset;
    private RectTransform handBoundary;
    private float dragShrinkTopInset;
    private Camera dragEventCamera;
    private Vector2 dragPointerScreen;
    private float dragCenterBlend;
    private bool miniature;
    private Tween dragCenterTween;
    private Quaternion homeRotation;
    private Quaternion dragRotation;
    private Quaternion hoverHomeRotation;
    private int dragSibling;
    private Graphic[] fadeGraphics;
    private Color[] fadeColors;
    private bool replayAnimating;
    private bool replayAlreadyDelivered;
    private RectTransform replaySelectionStart;
    private WorkerAssignment deliveryWorker;
    private Tween pendingFloat;
    private Vector2 homePivot;
    private float dragAngle;
    private float angleVelocity;
    private float horizontalSpeed;
    private float lastPointerMove;
    private Vector2 previousPointer;
    private bool waitingForTarget;
    private const float ReturnDropDuration = 0.18f;
    private CanvasGroup pendingFoodGroup;
    private bool pendingFoodRaycasts;
    private ActivityKind replaySelectionActivity;
    private Tween selectionTurn;
    private Image replaySelectionImage;
    private Sprite replaySelectionOriginal;
    private SeedCatalog seedCatalog;
    private InventoryTrading trading;
    internal ActivityCardAvailability Hand => transform.parent != null ?
        transform.parent.GetComponentInChildren<ActivityCardAvailability>(true) : null;

    public ActivityKind Kind => activity;
    public string DisplayName => activityName;
    public string Description
    {
        get
        {
            CardDescriptionCatalog.Entry entry = CardDescriptionCatalog.Instance != null ?
                CardDescriptionCatalog.Instance.Find(CardDescriptionCatalog.ActivityKey(activity)) : null;
            if (entry != null) return entry.text;
            CropType crop = activity switch
            {
                ActivityKind.PlantCorn => CropType.Corn,
                ActivityKind.PlantPumpkin => CropType.Pumpkin,
                ActivityKind.PlantJerimum => CropType.Jerimum,
                _ => CropType.None
            };
            if (crop == CropType.None)
                return description;
            if (seedCatalog == null) seedCatalog = FindFirstObjectByType<SeedCatalog>();
            if (trading == null) trading = FindFirstObjectByType<InventoryTrading>();
            CropSettings settings = seedCatalog != null ? seedCatalog.Get(crop) : null;
            if (settings == null || trading == null) return description;
            SaleItem item = crop == CropType.Corn ? SaleItem.Corn :
                crop == CropType.Pumpkin ? SaleItem.Pumpkin : SaleItem.Jerimum;
            string synergy = crop == CropType.Corn ?
                " Vizinhança: quando maduro, acelera macaxeira e jerimum em 1 etapa por dia e dá +2 à colheita; animais produzem +1." :
                " Vizinhança: milho maduro acelera o crescimento e dá +2 à colheita; galinha protege de pragas e poço protege da seca.";
            return "Plante em terreno arado. Cresce em " + settings.growthDays +
                (settings.growthDays == 1 ? " dia" : " dias") + ", rende " +
                settings.foodYield + " por colheita e vende por " +
                trading.GetPrice(item) + " moedas/un." + synergy;
        }
    }
    public bool HasEffect => true;
    public bool IsReturning => returning;
    public bool IsDragging => dragging;
    public void EndHoverForInspection() => RestoreHover(true);
    internal bool IsReplaySelectionInTransit => activity == ActivityKind.Replay &&
        selectionTurn != null && selectionTurn.IsActive();
    public void ResetHoverForSelection() => RestoreHover(true);

    public void PlayRejectedReplay()
    {
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        returning = true;
        float center = rect.anchoredPosition.x;
        DOTween.Sequence()
            .Append(rect.DOAnchorPosX(center + 18f, 0.045f))
            .Append(rect.DOAnchorPosX(center - 18f, 0.06f))
            .Append(rect.DOAnchorPosX(center + 15f, 0.055f))
            .Append(rect.DOAnchorPosX(center - 12f, 0.055f))
            .Append(rect.DOAnchorPosX(center, 0.05f))
            .SetTarget(rect)
            .OnComplete(() => AnimateReturn())
            .OnKill(() => { if (!waitingForTarget) returning = false; });
    }

    public void PlayReplay(WorkerAssignment worker, ActivityKind repeated,
        bool deliverDuringReplay, System.Action onFinished)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        RectTransform rect = (RectTransform)transform;
        if (root == null || worker == null)
        {
            onFinished?.Invoke();
            return;
        }
        replayAnimating = true;
        returning = true;
        Image source = GetComponent<Image>();
        ActivityCardDrag templateCard = Hand?.TemplateFor(repeated);
        Image template = templateCard != null ? templateCard.GetComponent<Image>() : null;
        Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, startScreen,
            canvas.worldCamera, out Vector2 start);
        var shadeObject = new GameObject("Foco do Replay", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform shade = (RectTransform)shadeObject.transform;
        shade.SetParent(root, false);
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        Image shadeImage = shadeObject.GetComponent<Image>();
        shadeImage.color = new Color(0f, 0f, 0f, 0f);
        shadeImage.raycastTarget = false;
        FullscreenBackdrop.Cover(shadeImage);
        var ghostObject = new GameObject("Carta Replay", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        RectTransform ghost = (RectTransform)ghostObject.transform;
        ghost.SetParent(root, false);
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.sizeDelta = rect.rect.size;
        ghost.localPosition = start;
        ghost.localScale = rect.localScale;
        Image image = ghostObject.GetComponent<Image>();
        image.sprite = source != null ? source.overrideSprite ?? source.sprite : null;
        image.color = source != null ? source.color : Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        CanvasGroup originalGroup = GetComponent<CanvasGroup>();
        if (originalGroup == null) originalGroup = gameObject.AddComponent<CanvasGroup>();
        originalGroup.alpha = 0f;
        rect.anchoredPosition = originalPosition;
        rect.localScale = originalScale;
        rect.localRotation = homeRotation;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(shadeImage.DOFade(0.27f, 0.52f));
        sequence.Append(ghost.DOAnchorPos(Vector2.zero, replayCenterDuration).SetEase(Ease.OutCubic));
        sequence.Join(ghost.DOScale(replayCenterScale, replayCenterDuration).SetEase(Ease.OutBack));
        sequence.Append(ghost.DOPunchRotation(new Vector3(0f, 0f, 10f), 0.88f, 12));
        sequence.AppendCallback(() =>
        {
            Sprite copiedArt = template != null ? template.overrideSprite ?? template.sprite : null;
            bool hasFinishedArt = copiedArt != null && !copiedArt.name.Contains("9-SlicedBox");
            if (hasFinishedArt) image.sprite = copiedArt;
            else
            {
                if (copiedArt != null) image.sprite = copiedArt;
                var label = new GameObject("Nome da ação", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Text));
                RectTransform labelRect = (RectTransform)label.transform;
                labelRect.SetParent(ghost, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 8f);
                labelRect.offsetMax = new Vector2(-8f, -8f);
                Text text = label.GetComponent<Text>();
                Text reference = templateCard != null ?
                    templateCard.GetComponentInChildren<Text>(true) : null;
                if (reference == null) reference = GetComponentInChildren<Text>(true);
                if (reference != null) text.font = reference.font;
                text.text = templateCard != null ? templateCard.DisplayName : repeated.ToString();
                text.color = new Color(0.75f, 0.1f, 0.1f);
                text.alignment = TextAnchor.MiddleCenter;
                text.resizeTextForBestFit = true;
                text.raycastTarget = false;
            }
        });
        sequence.Append(ghost.DOPunchScale(Vector3.one * 0.19f, 0.25f, 5));
        sequence.OnComplete(() =>
        {
            shadeImage.DOFade(0f, 0.28f).OnComplete(() => Destroy(shadeObject));
            if (deliverDuringReplay)
                AnimateDelivery(ghost, worker, FinishReplay, fromReplay: true);
            else
                FinishReplay();
        });

        void FinishReplay()
        {
            replayAnimating = false;
            returning = deliverDuringReplay;
            replayAlreadyDelivered = deliverDuringReplay;
            if (!deliverDuringReplay) replaySelectionStart = ghost;
            ghostObject.SetActive(false);
            try
            {
                onFinished?.Invoke();
            }
            finally
            {
                replaySelectionStart = null;
                if (!deliverDuringReplay && pendingFoodGroup == null && originalGroup != null)
                    originalGroup.alpha = 1f;
                Destroy(ghostObject);
            }
        }
    }

    internal void RestoreUnspentReplay()
    {
        if (!replayAlreadyDelivered) return;
        replayAlreadyDelivered = false;
        returning = false;
        RestoreAppearance();
    }

    public void FadeOut(System.Action onFinished)
    {
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        pendingFloat?.Kill();
        if (ReleaseFoodCard() || replayAlreadyDelivered)
        {
            replayAlreadyDelivered = false;
            deliveryWorker = null;
            waitingForTarget = false;
            returning = false;
            onFinished?.Invoke();
            return;
        }
        returning = true;
        bool replayingDisposable = activity == ActivityKind.Replay &&
            replaySelectionActivity is ActivityKind.BuildHouse or ActivityKind.BuildFence;
        if (activity == ActivityKind.BuildHouse || activity == ActivityKind.BuildFence || replayingDisposable)
        {
            DissolveDisposable(onFinished, replayingDisposable);
            return;
        }
        if (deliveryWorker != null && !replayAnimating)
        {
            AnimateDelivery(rect, deliveryWorker, onFinished);
            deliveryWorker = null;
            waitingForTarget = false;
            return;
        }
        deliveryWorker = null;
        fadeGraphics = GetComponentsInChildren<Graphic>(true);
        fadeColors = new Color[fadeGraphics.Length];
        for (int i = 0; i < fadeGraphics.Length; i++)
        {
            fadeColors[i] = fadeGraphics[i].color;
            fadeGraphics[i].DOFade(0f, 0.2f);
        }
        rect.DOScale(rect.localScale * 0.88f, 0.2f).SetEase(Ease.InSine)
            .OnComplete(() =>
            {
                returning = false;
                onFinished?.Invoke();
            });
    }

    private void DissolveDisposable(System.Action onFinished, bool replayingDisposable)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRoot = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        Image source = GetComponent<Image>();
        Material preset = Resources.Load<Material>("CardDissolveMaterial");
        if (canvasRoot == null || source == null || preset == null)
        {
            returning = false;
            onFinished?.Invoke();
            return;
        }

        RectTransform card = (RectTransform)transform;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.rootCanvas.worldCamera,
            card.TransformPoint(card.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screen,
            canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null :
                canvas.rootCanvas.worldCamera, out Vector2 start);
        var visual = new GameObject("Carta descartada - dissolucao", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform ghost = (RectTransform)visual.transform;
        ghost.SetParent(canvasRoot, false);
        ghost.anchorMin = ghost.anchorMax = Vector2.one * 0.5f;
        ghost.pivot = Vector2.one * 0.5f;
        ghost.sizeDelta = card.rect.size;
        ghost.localPosition = start;
        ghost.localScale = card.lossyScale / canvasRoot.lossyScale.x;
        Image art = visual.GetComponent<Image>();
        art.sprite = replayingDisposable ? source.overrideSprite : source.sprite;
        art.type = source.type;
        art.color = source.color;
        art.raycastTarget = false;
        if (replayingDisposable)
        {
            ghost.rotation = card.rotation;
            art.preserveAspect = source.preserveAspect;
        }
        Material ghostMaterial = new Material(preset);
        ghostMaterial.SetFloat("_Cutoff", 0f);
        ghostMaterial.SetVector("_RectSize", new Vector4(ghost.rect.width, ghost.rect.height, 0f, 0f));
        art.material = ghostMaterial;

        CanvasGroup original = GetComponent<CanvasGroup>();
        if (original == null) original = gameObject.AddComponent<CanvasGroup>();
        bool wasInteractable = original.interactable;
        bool blockedRaycasts = original.blocksRaycasts;
        original.alpha = 0f;
        original.interactable = false;
        original.blocksRaycasts = false;
        bool cleaned = false;
        void Clean()
        {
            if (cleaned) return;
            cleaned = true;
            if (replayingDisposable && original != null)
            {
                original.interactable = wasInteractable;
                original.blocksRaycasts = blockedRaycasts;
            }
            Destroy(ghostMaterial);
            Destroy(visual);
        }
        DOTween.Sequence().SetTarget(ghost)
            .Append(ghost.DOAnchorPos(Vector2.zero, dissolveMoveDuration).SetEase(Ease.OutCubic))
            .Join(ghost.DOScale(dissolveCenterScale, dissolveMoveDuration).SetEase(Ease.OutCubic))
            .AppendInterval(dissolvePause)
            .AppendCallback(() => AudioManager.Play(SoundCue.CardDissolve))
            .Append(DOTween.To(() => ghostMaterial.GetFloat("_Cutoff"),
                value => ghostMaterial.SetFloat("_Cutoff", value), 1f, dissolveDuration))
            .OnComplete(() =>
            {
                returning = false;
                Clean();
                onFinished?.Invoke();
            })
            .OnKill(Clean);
    }

    public void PrepareDelivery(WorkerAssignment worker)
    {
        deliveryWorker = worker;
        pendingFloat?.Kill();
    }

    internal void FlyToFoodPreparation(WorkerAssignment worker, ActivityKind selectedActivity,
        System.Action onArrived)
    {
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        returning = true;
        if (replaySelectionStart != null)
        {
            ShowReplaySelectionArt(selectedActivity);
            MatchReplayPose(rect);
        }
        pendingFoodGroup = GetComponent<CanvasGroup>();
        if (pendingFoodGroup == null) pendingFoodGroup = gameObject.AddComponent<CanvasGroup>();
        pendingFoodRaycasts = pendingFoodGroup.blocksRaycasts;
        pendingFoodGroup.blocksRaycasts = false;
        pendingFoodGroup.alpha = 1f;
        AnimateDelivery(rect, worker, () =>
        {
            if (pendingFoodGroup == null) return;
            pendingFoodGroup.alpha = 0f;
            returning = true;
            onArrived?.Invoke();
        });
    }

    internal void ReturnFromFoodPreparation()
    {
        CanvasGroup group = pendingFoodGroup;
        if (!ReleaseFoodCard()) return;
        AnimateReturn(revealBelowScreen: true);
        group.alpha = 1f;
    }

    private bool ReleaseFoodCard()
    {
        if (pendingFoodGroup == null) return false;
        pendingFoodGroup.blocksRaycasts = pendingFoodRaycasts;
        pendingFoodGroup = null;
        return true;
    }

    internal void LeaveHandForReplay(Vector2 handPosition, float handScale, Quaternion handRotation)
    {
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        originalPosition = handPosition;
        originalScale = Vector3.one * handScale;
        homeRotation = handRotation;
        homePivot = rect.pivot;
        dragSibling = rect.GetSiblingIndex();
        returning = true;
        rect.DOAnchorPosY(BelowHandPosition(rect).y, ReturnDropDuration).SetEase(Ease.InCubic);
    }

    internal void ReturnFromReplayReservation() => AnimateReturn(playSound: false);

    public void FlyToSelection(ActivityKind selectedActivity)
    {
        RectTransform rect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || rect.parent == null) return;
        rect.DOKill();
        pendingFloat?.Kill();
        waitingForTarget = true;
        returning = true;
        if (activity == ActivityKind.Replay) ShowReplaySelectionArt(selectedActivity);
        RectTransform parent = (RectTransform)rect.parent;
        float duration = replaySelectionStart != null ? replaySelectionDuration : 0.42f;
        if (replaySelectionStart != null) MatchReplayPose(rect);
        else rect.localRotation = homeRotation;
        Vector2 screen = new Vector2(Screen.width * 0.17f, Screen.height * 0.82f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen,
                canvas.worldCamera, out Vector2 destination)) return;
        destination += rect.anchoredPosition - (Vector2)rect.localPosition;
        rect.DOAnchorPos(destination, duration).SetEase(Ease.OutCubic);
        rect.DOScale(originalScale * 0.90f, duration).SetEase(Ease.OutBack);
        selectionTurn = rect.DOLocalRotate(new Vector3(0f, 0f, -8f), duration)
            .OnComplete(() =>
            {
                if (!waitingForTarget) return;
                pendingFloat = rect.DOAnchorPosY(destination.y + 7f, 1.3f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
            });
    }

    private void MatchReplayPose(RectTransform rect)
    {
        RectTransform ghost = replaySelectionStart;
        rect.localScale = ghost.lossyScale / rect.parent.lossyScale.x;
        rect.rotation = ghost.rotation;
        rect.position += ghost.TransformPoint(ghost.rect.center) - rect.TransformPoint(rect.rect.center);
    }

    private void ShowReplaySelectionArt(ActivityKind selectedActivity)
    {
        replaySelectionActivity = selectedActivity;
        ActivityCardDrag template = Hand?.TemplateFor(selectedActivity);
        if (template == null || !template.TryGetComponent(out Image art) ||
            !TryGetComponent(out Image image)) return;
        Sprite selectedSprite = art.overrideSprite != null ? art.overrideSprite : art.sprite;
        if (selectedSprite == null) return;
        replaySelectionImage = image;
        replaySelectionOriginal = image.overrideSprite;
        image.overrideSprite = selectedSprite;
    }

    private void RestoreReplaySelectionArt()
    {
        replaySelectionActivity = default;
        if (replaySelectionImage == null) return;
        replaySelectionImage.overrideSprite = replaySelectionOriginal == replaySelectionImage.sprite
            ? null : replaySelectionOriginal;
        replaySelectionImage = null;
        replaySelectionOriginal = null;
    }

    public void ReturnFromSelection()
    {
        if (!waitingForTarget) return;
        waitingForTarget = false;
        pendingFloat?.Kill();
        AnimateReturn();
    }

    private void AnimateDelivery(RectTransform rect, WorkerAssignment worker,
        System.Action onFinished, bool fromReplay = false)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        RectTransform originalParent = rect.parent as RectTransform;
        Camera destinationCamera = worldCamera != null ? worldCamera : Camera.main;
        if (canvas == null || originalParent == null || destinationCamera == null || worker == null)
        {
            onFinished?.Invoke();
            return;
        }
        bool fromSelection = !fromReplay && waitingForTarget;
        Vector3 visualCenter = rect.TransformPoint(rect.rect.center);
        Vector2 originalAnchorMin = rect.anchorMin;
        Vector2 originalAnchorMax = rect.anchorMax;
        RectTransform flightParent = originalParent;
        if (fromSelection && canvas.rootCanvas.transform is RectTransform root)
        {
            if (root != originalParent)
            {
                rect.SetParent(root, true);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                flightParent = root;
            }
            rect.SetAsLastSibling();
        }
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.position += visualCenter - rect.TransformPoint(rect.rect.center);
        float liftScale = Mathf.Max(rect.localScale.x, 0.1f) * 1.16f;
        float flightDuration = fromReplay ? replayDeliveryDuration : 0.47f;
        Vector2 anchorOffset = rect.anchoredPosition - (Vector2)rect.localPosition;
        Vector2 flightStart = rect.anchoredPosition;
        float flightProgress = 0f;
        Sequence delivery = DOTween.Sequence().SetTarget(rect);
        delivery.Append(rect.DOAnchorPosY(rect.anchoredPosition.y + 28f, 0.18f)
            .SetEase(Ease.OutCubic));
        delivery.Join(rect.DOScale(liftScale, 0.18f)
            .SetEase(Ease.OutCubic));
        delivery.AppendCallback(() => flightStart = rect.anchoredPosition);
        delivery.Append(DOTween.To(() => flightProgress, progress =>
        {
            flightProgress = progress;
            if (worker == null || rect == null) return;
            Vector2 screen = destinationCamera.WorldToScreenPoint(
                worker.transform.position + Vector3.up);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(flightParent,
                    screen, canvas.worldCamera, out Vector2 destination))
                rect.anchoredPosition = Vector2.LerpUnclamped(flightStart, destination + anchorOffset, progress);
        }, 1f, flightDuration).SetEase(Ease.InCubic));
        delivery.Join(rect.DOLocalRotate(new Vector3(0f, 0f,
            rect.localEulerAngles.z + 360f), flightDuration, RotateMode.FastBeyond360));
        delivery.Join(rect.DOScale(originalScale * 0.04f, flightDuration).SetEase(Ease.InCubic));
        CanvasGroup fadeGroup = rect.GetComponent<CanvasGroup>();
        if (fadeGroup == null) fadeGroup = rect.gameObject.AddComponent<CanvasGroup>();
        delivery.Append(rect.DOScale(originalScale * 0.14f, 0.08f)
            .SetEase(Ease.OutBack));
        delivery.Join(fadeGroup.DOFade(0f, 0.08f));
        delivery.OnComplete(() =>
        {
            if (flightParent != originalParent && rect != null && originalParent != null)
            {
                rect.SetParent(originalParent, false);
                rect.anchorMin = originalAnchorMin;
                rect.anchorMax = originalAnchorMax;
            }
            if (!fromReplay) rect.pivot = homePivot;
            returning = false;
            onFinished?.Invoke();
        });
    }

    public void RestoreAppearance()
    {
        RestoreReplaySelectionArt();
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
        if (fadeGraphics == null) return;
        for (int i = 0; i < fadeGraphics.Length; i++)
            if (fadeGraphics[i] != null) fadeGraphics[i].color = fadeColors[i];
    }

    public void ConfigureRuntime(Camera camera, DragCursorPreview cursorPreview)
    {
        worldCamera = camera;
        preview = cursorPreview;
    }

    private void OnEnable()
    {
        returning = false;
        waitingForTarget = false;
        if (preview != null) preview.CancelRequested += CancelDrag;
        originalScale = transform.localScale;
        if (activity == ActivityKind.BuySeeds)
        {
            shopClicking = false;
            transform.localScale = Vector3.one;
        }
        homePivot = ((RectTransform)transform).pivot;
    }

    private void OnDisable()
    {
        ReleaseFoodCard();
        RestoreReplaySelectionArt();
        if (preview != null) preview.CancelRequested -= CancelDrag;
        shopClickTween?.Kill();
        shopHoverTween?.Kill();
        pendingFloat?.Kill();
        if (activity == ActivityKind.BuySeeds)
        {
            shopClicking = false;
            hovered = false;
            transform.localScale = Vector3.one;
        }
        CancelDrag();
        RestoreHover(true);
        ((RectTransform)transform).DOKill();
        returning = false;
        waitingForTarget = false;
    }

    private void Update()
    {
        if (!dragging) return;
        FollowDragPointer();
        float idle = Time.unscaledTime - lastPointerMove;
        float targetAngle = Mathf.Clamp(-horizontalSpeed * 0.018f, -16f, 16f);
        if (idle > 0.07f)
        {
            float elapsed = idle - 0.07f;
            targetAngle = Mathf.Sin(elapsed * 10f) * Mathf.Exp(-elapsed * 3.5f) *
                Mathf.Clamp(-horizontalSpeed * 0.018f, -9f, 9f);
        }
        if (hoveredWorker != null)
            targetAngle = Mathf.Sin(Time.unscaledTime * 48f) * 5f;
        dragAngle = Mathf.SmoothDampAngle(dragAngle, targetAngle, ref angleVelocity,
            hoveredWorker != null ? 0.035f : 0.11f, Mathf.Infinity, Time.unscaledDeltaTime);
        transform.localRotation = dragRotation * Quaternion.Euler(0f, 0f, dragAngle);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dragging || hovered || returning || Hand != null && Hand.IsDraggingCard) return;
        if (activity != ActivityKind.BuySeeds && Hand != null && Hand.ContainsCard(this)) return;
        if (activity != ActivityKind.BuySeeds && Hand != null && Hand.IsSelectionLocked) return;
        hovered = true;
        if (Time.timeScale > 0f) AudioManager.Play(SoundCue.CardHover);
        if (activity == ActivityKind.BuySeeds)
        {
            if (!shopClicking)
            {
                shopHoverTween?.Kill();
                shopHoverTween = transform.DOScale(Vector3.one * 1.1f, 0.16f)
                    .SetEase(Ease.OutSine);
            }
            return;
        }
        originalScale = transform.localScale;
        originalSibling = transform.GetSiblingIndex();
        originalPosition = ((RectTransform)transform).anchoredPosition;
        hoverHomeRotation = transform.localRotation;
        transform.SetAsLastSibling();
        transform.DOKill();
        transform.DOScale(originalScale * 1.18f, 0.18f).SetEase(Ease.OutCubic);
        ((RectTransform)transform).DOAnchorPosY(originalPosition.y + 30f, 0.18f).SetEase(Ease.OutCubic);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (returning || dragging)
        {
            hovered = false;
            return;
        }
        if (activity != ActivityKind.BuySeeds && Hand != null && Hand.ContainsCard(this))
        {
            hovered = false;
            return;
        }
        if (activity == ActivityKind.BuySeeds)
        {
            hovered = false;
            if (shopClicking) return;
            shopHoverTween?.Kill();
            shopHoverTween = transform.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutSine);
            return;
        }
        RestoreHover(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (activity == ActivityKind.BuySeeds)
        {
            if (shop == null || Hand != null && Hand.IsGatheringBarVisible) return;
            shopClickTween?.Kill();
            shopClicking = true;
            shopHoverTween?.Kill();
            shopClickTween = DOTween.Sequence()
                .Append(transform.DOScale(Vector3.one * 0.93f, 0.08f).SetEase(Ease.InSine))
                .Append(transform.DOScale(Vector3.one * 1.12f, 0.12f).SetEase(Ease.OutBack))
                .Append(transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutSine))
                .OnComplete(() => shopClicking = false);
            shop.ToggleShop();
            return;
        }
        if (!eventData.dragging && !dragging && !returning)
            Hand?.InspectCard(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            !HasEffect || activity == ActivityKind.BuySeeds || returning) return;
        if (Hand != null && Hand.IsSelectionLocked) return;
        GetComponent<CardHoverOutline>()?.SetHighlighted(false);
        ActivityCardAvailability hand = Hand;
        hand?.ClosePopupsForDrag();
        Vector2 restingPosition = default;
        float restingScale = 1f;
        Quaternion restingRotation = Quaternion.identity;
        bool hasRestingPose = hand != null && hand.ClearHoverForDrag(this,
            out restingPosition, out restingScale, out restingRotation);
        RestoreHover(true);
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        dragRotation = rect.localRotation;
        originalPosition = hasRestingPose ? restingPosition : rect.anchoredPosition;
        originalScale = hasRestingPose ? Vector3.one * restingScale : rect.localScale;
        homeRotation = hasRestingPose ? restingRotation : rect.localRotation;
        homePivot = rect.pivot;
        Vector3 cardCenter = rect.TransformPoint(rect.rect.center);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.position += cardCenter - rect.TransformPoint(rect.rect.center);
        dragAngle = angleVelocity = horizontalSpeed = 0f;
        previousPointer = eventData.position;
        lastPointerMove = Time.unscaledTime;
        dragSibling = rect.GetSiblingIndex();
        handBoundary = hand != null ? hand.DragBoundary : null;
        dragShrinkTopInset = hand != null ? hand.DragShrinkTopInset : 0f;
        dragEventCamera = eventData.pressEventCamera;
        dragPointerScreen = eventData.position;
        float pointerDelta = eventData.position.x - previousPointer.x;
        if (Mathf.Abs(pointerDelta) > 0.5f)
        {
            horizontalSpeed = Mathf.Lerp(horizontalSpeed,
                pointerDelta / Mathf.Max(Time.unscaledDeltaTime, 0.01f), 0.4f);
            lastPointerMove = Time.unscaledTime;
        }
        previousPointer = eventData.position;
        dragCenterBlend = 0f;
        miniature = false;
        dragCenterTween?.Kill();
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)rect.parent, eventData.position, eventData.pressEventCamera,
                out Vector3 cursorWorld))
            dragWorldOffset = rect.position - cursorWorld;
        else dragWorldOffset = Vector3.zero;
        rect.SetAsLastSibling();
        dragging = true;
        TutorialHints.Once("cards", "Arraste as cartas até um trabalhador para atribuir ações. Algumas também exigem escolher um terreno.");
        AudioManager.Play(SoundCue.CardPick);
        cancelled = false;
        OnDrag(eventData);
        UpdateHover(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragPointerScreen = eventData.position;
        bool outside = handBoundary == null || !RectTransformUtility.RectangleContainsScreenPoint(
            handBoundary, eventData.position, dragEventCamera);
        if (!outside && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                handBoundary, eventData.position, dragEventCamera, out Vector2 handPoint))
            outside = handPoint.y >= handBoundary.rect.yMax - dragShrinkTopInset;
        if (outside != miniature)
        {
            miniature = outside;
            RectTransform rect = (RectTransform)transform;
            rect.DOKill();
            rect.DOScale(originalScale * (outside ? 0.42f : 1f), 0.16f)
                .SetEase(Ease.OutCubic);
            dragCenterTween?.Kill();
            dragCenterTween = DOTween.To(() => dragCenterBlend,
                value => dragCenterBlend = value, outside ? 1f : 0f, 0.16f)
                .SetEase(Ease.OutCubic);
        }
        FollowDragPointer();
        UpdateHover(eventData.position);
        transform.parent.GetComponentInChildren<ActivityCardAvailability>(true)?
            .UpdateDucked(eventData.position, dragEventCamera);
    }

    private void FollowDragPointer()
    {
        RectTransform rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)rect.parent, dragPointerScreen, dragEventCamera,
                out Vector3 cursorWorld)) return;
        Vector3 centerOffset = rect.position - rect.TransformPoint(rect.rect.center);
        rect.position = cursorWorld + Vector3.Lerp(dragWorldOffset, centerOffset,
            dragCenterBlend);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || cancelled)
        {
            ClearDrag();
            return;
        }
        OnDrag(eventData);
        dragging = false;
        transform.localRotation = dragRotation * Quaternion.Euler(0f, 0f, dragAngle);
        dragCenterTween?.Kill();
        UpdateHover(eventData.position);
        WorkerAssignment target = hoveredWorker;
        ClearDrag();
        if (target != null)
        {
            int assignmentVersion = target.AssignmentVersion;
            target.TryAssign(activity, activityName, this);
            if (target.AssignmentVersion != assignmentVersion)
                AudioManager.Play(SoundCue.CardPlaced);
        }
        ActivityCardAvailability hand = transform.parent != null ?
            transform.parent.GetComponentInChildren<ActivityCardAvailability>(true) : null;
        if (!replayAnimating && !returning && gameObject.activeInHierarchy &&
            (hand == null || hand.ContainsCard(this)))
            AnimateReturn();
        hand?.EndExternalDrag();
    }

    public void CancelDrag()
    {
        if (!dragging) return;
        dragging = false;
        cancelled = true;
        dragCenterTween?.Kill();
        ClearDrag();
        if (gameObject.activeInHierarchy) AnimateReturn();
        transform.parent.GetComponentInChildren<ActivityCardAvailability>(true)?.EndExternalDrag();
    }

    private void ClearDrag()
    {
        if (hoveredWorker != null) hoveredWorker.SetHovered(false);
        hoveredWorker = null;
    }

    private void AnimateReturn(bool revealBelowScreen = false, bool playSound = true)
    {
        waitingForTarget = false;
        pendingFloat?.Kill();
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        returning = true;
        if (playSound) AudioManager.Play(SoundCue.CardReturn);
        Vector3 cardCenter = rect.TransformPoint(rect.rect.center);
        rect.pivot = homePivot;
        rect.position += cardCenter - rect.TransformPoint(rect.rect.center);
        Vector2 belowScreen = BelowHandPosition(rect);
        Sequence sequence = DOTween.Sequence();
        if (revealBelowScreen) rect.anchoredPosition = belowScreen;
        else sequence.Append(rect.DOAnchorPos(belowScreen, ReturnDropDuration).SetEase(Ease.InCubic));
        sequence.AppendCallback(() =>
            {
                RestoreReplaySelectionArt();
                rect.localRotation = homeRotation;
            })
            .Append(rect.DOAnchorPos(originalPosition, 0.36f).SetEase(Ease.OutBack))
            .Join(rect.DOScale(originalScale, 0.36f).SetEase(Ease.OutBack))
            .SetTarget(rect)
            .OnComplete(() => RestoreDragSibling(rect))
            .OnKill(() => { if (!waitingForTarget) returning = false; });
    }

    private Vector2 BelowHandPosition(RectTransform rect) =>
        new Vector2(originalPosition.x, -rect.rect.height * Mathf.Max(0.4f, originalScale.y) - 90f);

    private void RestoreDragSibling(RectTransform rect)
    {
        if (rect != null && rect.parent != null)
            rect.SetSiblingIndex(Mathf.Min(dragSibling, rect.parent.childCount - 1));
        returning = false;
    }

    private void RestoreHover(bool immediate)
    {
        if (!hovered) return;
        hovered = false;
        transform.DOKill();
        RectTransform rect = (RectTransform)transform;
        if (immediate)
        {
            transform.localScale = originalScale;
            rect.anchoredPosition = originalPosition;
            rect.localRotation = hoverHomeRotation;
        }
        else
        {
            transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutCubic);
            rect.DOAnchorPos(originalPosition, 0.15f).SetEase(Ease.OutCubic);
            rect.DOLocalRotateQuaternion(hoverHomeRotation, 0.15f).SetEase(Ease.OutCubic);
        }
        transform.SetSiblingIndex(originalSibling);
    }

    private void UpdateHover(Vector2 screenPosition)
    {
        WorkerAssignment target = null;
        if (worldCamera != null)
        {
            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 200f))
            {
                target = hit.collider.GetComponentInParent<WorkerAssignment>();
                if (target != null)
                    break;
            }
        }

        if (target == hoveredWorker)
            return;
        if (hoveredWorker != null)
            hoveredWorker.SetHovered(false);
        hoveredWorker = target;
        if (hoveredWorker != null)
            hoveredWorker.SetHovered(true);
    }
}
