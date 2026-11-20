using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class TopBarAutoHide : MonoBehaviour
{
    [SerializeField] private RectTransform bar;
    [SerializeField] private RectTransform background;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private DayTimeline timeline;
    [SerializeField] private ActiveMissionsPanel missionsPanel;
    [SerializeField] private ActivityCardAvailability hand;
    [SerializeField] private Vector2 openPosition = Vector2.zero;
    [SerializeField] private Vector2 closedPosition = new Vector2(0f, 178f);
    [SerializeField, InspectorName("Posição ao visualizar um dia")]
    private Vector2 timelineDayPosition = new Vector2(0f, 90f);
    [SerializeField, Min(0.1f)] private float idleSeconds = 10f;
    [SerializeField, Min(0.01f)] private float slideDuration = 0.36f;
    [SerializeField, Min(0f)] private float waveDelay = 0.055f;
    [SerializeField, Min(0f)] private float widgetOffset = 24f;

    private RectTransform[] widgets;
    private Vector2[] widgetPositions;
    private Camera uiCamera;
    private ResourceHud resourceHud;
    private Sequence motion;
    private bool open;
    private bool dayActive;
    private bool wavePendingForDay = true;
    private bool waitForPointerExit;
    private bool timelineDayOpen;
    private bool closingAfterTimeline;
    private bool paused;
    private bool coinTossOpen;
    private bool MotionLocked => paused || coinTossOpen;
    private bool followingWorker;
    private float lastHoverTime;
    private bool tutorialRestricted;
    private bool tutorialVisible;
    private RectTransform tutorialControl;
    private TutorialControlState[] tutorialControls;

    private struct TutorialControlState
    {
        public CanvasGroup group;
        public bool interactable;
        public bool blocksRaycasts;
    }

    internal bool TutorialRestricted => tutorialRestricted;
    private Vector2 ClosedDestination => tutorialRestricted && bar != null
        ? closedPosition + Vector2.up * bar.rect.height : closedPosition;

    // Paused transitions still have a temporary layout and should not be captured by the editor.
    public bool IsMoving => motion != null && motion.IsActive() && !motion.IsComplete();

    private void Awake()
    {
        if (bar == null) bar = (RectTransform)transform;
        resourceHud = GetComponent<ResourceHud>();
        var ordered = new List<RectTransform>(bar.childCount);
        for (int i = 0; i < bar.childCount; i++)
        {
            RectTransform child = bar.GetChild(i) as RectTransform;
            if (child != null && child != background && child.gameObject.activeSelf)
                ordered.Add(child);
        }
        ordered.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
        widgets = ordered.ToArray();
        widgetPositions = new Vector2[widgets.Length];
        for (int i = 0; i < widgets.Length; i++)
            widgetPositions[i] = widgets[i].anchoredPosition;
        Canvas canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
    }

    private void OnEnable()
    {
        if (dayCycle != null)
        {
            dayCycle.DayChanged += OpenForNewDay;
            dayCycle.DayClosing += CloseForDayEnd;
            dayCycle.DayClosingCancelled += OpenForNewDay;
        }
        wavePendingForDay = true;
        SetOpen(false, true);
    }

    private void OnDisable()
    {
        if (dayCycle != null)
        {
            dayCycle.DayChanged -= OpenForNewDay;
            dayCycle.DayClosing -= CloseForDayEnd;
            dayCycle.DayClosingCancelled -= OpenForNewDay;
        }
        motion?.Kill();
        RestoreTutorialControls();
        tutorialRestricted = false;
        tutorialControl = null;
    }

    internal void SetTutorialHud(bool visible, RectTransform allowedControl = null)
    {
        if (!isActiveAndEnabled || bar == null) return;
        bool entering = !tutorialRestricted;
        bool visibilityChanged = entering || tutorialVisible != visible;
        if (!visibilityChanged && tutorialControl == allowedControl) return;

        tutorialRestricted = true;
        tutorialVisible = visible;
        tutorialControl = allowedControl;
        if (entering) CaptureTutorialControls();
        foreach (TutorialControlState state in tutorialControls)
        {
            if (state.group == null) continue;
            bool allowed = visible && allowedControl != null && state.group.transform == allowedControl;
            state.group.interactable = allowed && state.interactable;
            state.group.blocksRaycasts = allowed && state.blocksRaycasts;
        }

        if (visibilityChanged && !visible) SetOpen(false, entering, force: true);
        else if (visibilityChanged && dayActive && !followingWorker && !IsHandBusy())
            SetOpen(true, false);
    }

    internal void ReleaseTutorialHud()
    {
        if (!tutorialRestricted) return;
        tutorialRestricted = false;
        tutorialControl = null;
        RestoreTutorialControls();
        if (isActiveAndEnabled && !open && bar != null && bar.anchoredPosition != closedPosition)
            SetOpen(false, false, force: true);
    }

    private void CaptureTutorialControls()
    {
        Selectable[] controls = bar.GetComponentsInChildren<Selectable>(true);
        tutorialControls = new TutorialControlState[controls.Length];
        for (int i = 0; i < controls.Length; i++)
        {
            CanvasGroup group = controls[i].GetComponent<CanvasGroup>();
            if (group == null) group = controls[i].gameObject.AddComponent<CanvasGroup>();
            tutorialControls[i] = new TutorialControlState
            {
                group = group,
                interactable = group.interactable,
                blocksRaycasts = group.blocksRaycasts
            };
        }
    }

    private void RestoreTutorialControls()
    {
        if (tutorialControls == null) return;
        foreach (TutorialControlState state in tutorialControls)
        {
            if (state.group == null) continue;
            state.group.interactable = state.interactable;
            state.group.blocksRaycasts = state.blocksRaycasts;
        }
        tutorialControls = null;
    }

    private bool IsHandBusy() => hand != null && (hand.IsDraggingCard || hand.IsSelectionLocked);

    private void Update()
    {
        if (MotionLocked) return;
        if (tutorialRestricted)
        {
            if (tutorialVisible && dayActive && !followingWorker && !IsHandBusy() && !closingAfterTimeline)
            {
                if (!open) SetOpen(true, false);
            }
            else if (open || (!IsMoving && bar != null && bar.anchoredPosition != ClosedDestination))
                SetOpen(false, false, force: true);
            return;
        }
        if (!dayActive) return;
        if (followingWorker && (resourceHud == null || !resourceHud.HasFlyingResources))
        {
            if (open) SetOpen(false, false);
            return;
        }

        // Dragging takes precedence over hover, open panels and resource flights.
        if (hand != null && hand.IsDraggingCard)
        {
            lastHoverTime = Time.unscaledTime;
            if (open) SetOpen(false, false);
            return;
        }
        if (closingAfterTimeline) return;

        // Target selection has priority over the HUD, including incoming resources.
        if (hand != null && hand.IsSelectionLocked)
        {
            if (open)
            {
                missionsPanel?.Close();
                CloseAfterTimeline();
            }
            return;
        }

        // Keep the destination visible for the entire flight, including while paused.
        if (resourceHud != null && resourceHud.HasFlyingResources)
        {
            RevealForResources();
            return;
        }

        if (Mouse.current == null) return;
        Vector2 pointer = Mouse.current.position.ReadValue();
        bool overBar = open ? RectTransformUtility.RectangleContainsScreenPoint(bar, pointer, uiCamera) :
            IsOverRevealTab(pointer);
        if (waitForPointerExit)
        {
            if (IsMoving || overBar) return;
            waitForPointerExit = false;
        }
        if ((timeline != null && timeline.IsOpen) ||
            (missionsPanel != null && missionsPanel.IsOpen))
        {
            lastHoverTime = Time.unscaledTime;
            if (!open) SetOpen(true, false);
            return;
        }
        if (overBar)
        {
            lastHoverTime = Time.unscaledTime;
            if (!open) SetOpen(true, false);
        }
        else if (open && Time.unscaledTime - lastHoverTime >= idleSeconds)
            SetOpen(false, false);
    }

    private bool IsOverRevealTab(Vector2 pointer)
    {
        if (background == null || !background.gameObject.activeInHierarchy ||
            pointer.x < 0f || pointer.y < 0f || pointer.x >= Screen.width || pointer.y >= Screen.height ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(background, pointer, uiCamera,
                out Vector2 point)) return false;

        Rect rect = background.rect;
        if (rect.width <= 0f || rect.height <= 0f) return false;
        // Contorno da lingueta na arte do HUD, medido a partir do topo da imagem.
        const float tabStart = 280f / 1080f;
        const float tabShoulder = 408f / 1080f;
        const float tabEnd = 409f / 1080f;
        float y = (rect.yMax - point.y) / rect.height;
        if (y < tabStart || y > tabEnd) return false;
        float halfWidth = y <= tabShoulder
            ? Mathf.Lerp(0.142f, 0.080f, Mathf.InverseLerp(tabStart, tabShoulder, y))
            : Mathf.Lerp(0.080f, 0.028f, Mathf.InverseLerp(tabShoulder, tabEnd, y));
        float x = (point.x - rect.center.x) / rect.width;
        return Mathf.Abs(x) <= halfWidth;
    }

    public void RevealForResources() => PrepareResourceFeedback();

    public void SetFollowingWorker(bool following)
    {
        followingWorker = following;
        if (following) SetOpen(false, false);
        else if (dayActive)
        {
            lastHoverTime = Time.unscaledTime;
            SetOpen(true, false);
        }
    }

    // The day counter is the only manual close target for the top bar.
    public void CloseFromDayCounter()
    {
        if (tutorialRestricted || MotionLocked || !dayActive || !open || closingAfterTimeline ||
            (resourceHud != null && resourceHud.HasFlyingResources) ||
            (hand != null && (hand.IsDraggingCard || hand.IsSelectionLocked))) return;

        AudioManager.Play(SoundCue.UiPaper);
        missionsPanel?.Close();
        waitForPointerExit = true;
        CloseAfterTimeline();
    }

    // Returns the time remaining before the counter area is fully visible.
    public float PrepareResourceFeedback()
    {
        if ((tutorialRestricted && !tutorialVisible) || MotionLocked || !dayActive || closingAfterTimeline || timelineDayOpen ||
            (timeline != null && timeline.IsOpen) ||
            (hand != null && (hand.IsSelectionLocked || hand.IsDraggingCard))) return 0f;

        lastHoverTime = Time.unscaledTime;
        if (!open) SetOpen(true, false);
        if (bar == null) return 0f;
        return motion != null && motion.IsActive()
            ? Mathf.Max(0f, motion.Duration(false) - motion.Elapsed()) : 0f;
    }

    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        UpdateMotionLock();
    }

    internal void SetCoinTossOpen(bool value)
    {
        if (coinTossOpen == value) return;
        coinTossOpen = value;
        UpdateMotionLock();
    }

    private void UpdateMotionLock()
    {
        if (MotionLocked) motion?.Pause();
        else
        {
            motion?.Play();
            lastHoverTime = Time.unscaledTime;
        }
    }

    private void OpenForNewDay()
    {
        wavePendingForDay = true;
        dayActive = true;
        waitForPointerExit = false;
        lastHoverTime = Time.unscaledTime;
        SetOpen(true, false);
    }

    private void CloseForDayEnd()
    {
        dayActive = false;
        CloseAfterTimeline();
    }

    public void PreviewCurrentPosition()
    {
        if (!Application.isPlaying || coinTossOpen) return;
        motion?.Complete();
        MoveBar(timelineDayOpen ? timelineDayPosition :
            open ? openPosition : ClosedDestination, true, null);
    }

    public void MoveToTimelineDay(System.Action onComplete)
    {
        if (coinTossOpen)
        {
            onComplete?.Invoke();
            return;
        }
        timelineDayOpen = true;
        open = true;
        lastHoverTime = Time.unscaledTime;
        MoveBar(timelineDayPosition, false, onComplete);
    }

    public void ReturnFromTimelineDay(System.Action onComplete)
    {
        if (coinTossOpen)
        {
            onComplete?.Invoke();
            return;
        }
        timelineDayOpen = false;
        lastHoverTime = Time.unscaledTime;
        MoveBar(openPosition, false, onComplete);
    }

    private void CloseAfterTimeline()
    {
        if (timeline != null && timeline.IsOpen)
        {
            closingAfterTimeline = true;
            timeline.ClosePanel(() =>
            {
                closingAfterTimeline = false;
                SetOpen(false, false);
            });
            return;
        }
        SetOpen(false, false);
    }

    private void SetOpen(bool show, bool immediate, bool force = false)
    {
        if (MotionLocked || (show && tutorialRestricted &&
            (!tutorialVisible || !dayActive || followingWorker || IsHandBusy()))) return;
        bool playWave = show && wavePendingForDay && !immediate;
        if (open == show && !immediate && !playWave && !force) return;
        open = show;
        timelineDayOpen = false;
        lastHoverTime = Time.unscaledTime;
        motion?.Kill();
        if (bar == null) return;

        Vector2 destination = show ? openPosition : ClosedDestination;
        float exitDistance = bar.rect.height +
            Mathf.Abs(closedPosition.y - openPosition.y) + widgetOffset;

        // During ordinary opening/closing, widgets remain fixed to their parent HUD.
        // Only the first opening of each day receives staggered entrance tweens.
        for (int i = 0; i < widgets.Length; i++)
            widgets[i].anchoredPosition = widgetPositions[i] +
                ((playWave || (immediate && !show && wavePendingForDay))
                    ? Vector2.up * exitDistance : Vector2.zero);

        MoveBar(destination, immediate, null);
        if (!playWave) return;

        wavePendingForDay = false;
        for (int i = 0; i < widgets.Length; i++)
        {
            if (!widgets[i].gameObject.activeSelf) continue;
            motion.Insert(slideDuration + i * waveDelay,
                widgets[i].DOAnchorPos(widgetPositions[i], slideDuration * 0.65f)
                    .SetEase(Ease.OutCubic));
        }
    }

    private void MoveBar(Vector2 destination, bool immediate, System.Action onComplete)
    {
        motion?.Kill();
        if (bar == null) return;
        bool separateArt = background != null && background.parent != bar;
        float artScale = separateArt && bar.rect.width > 0f
            ? background.rect.width / bar.rect.width : 1f;
        if (immediate)
        {
            bar.anchoredPosition = destination;
            if (separateArt) background.anchoredPosition = destination * artScale;
            timeline?.FollowHud();
            onComplete?.Invoke();
            return;
        }
        motion = DOTween.Sequence().SetUpdate(true);
        motion.Insert(0f, bar.DOAnchorPos(destination, slideDuration).SetEase(Ease.InOutCubic));
        if (separateArt)
            motion.Insert(0f, background.DOAnchorPos(destination * artScale, slideDuration)
                .SetEase(Ease.InOutCubic));
        motion.OnUpdate(() => timeline?.FollowHud());
        motion.OnComplete(() => onComplete?.Invoke());
    }
}
