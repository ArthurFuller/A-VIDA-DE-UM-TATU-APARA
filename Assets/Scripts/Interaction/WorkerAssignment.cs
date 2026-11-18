using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorkerAssignment : MonoBehaviour
{
    [SerializeField] private GameObject selectionMarker;
    [SerializeField] private Text statusText;
    [SerializeField] private string workerName = "Trabalhador";
    [SerializeField, Range(1, 5), Tooltip("Casa de origem usada para escolher o trajeto do funcionário.")]
    private int originHouseNumber = 1;
    [SerializeField] private ActionResolver actionResolver;
    [SerializeField] private WorkProgress workProgress;
    [SerializeField] private ParticleSystem comadreDizzyPrefab;
    [Header("Pontos de ação")]
    [SerializeField] private Sprite actionPointSprite;
    [SerializeField, Min(0.01f)] private float actionPointSize = 0.16f;
    [SerializeField, Tooltip("Posição em relação ao funcionário: X lateral, Y altura, Z profundidade.")]
    private Vector3 actionPointOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField, Min(0f)] private float actionPointSpacing = 0.2f;
    [SerializeField, Range(0, 10), InspectorName("Prévia de pontos de ação"),
        Tooltip("Mostra esta quantidade de pontos para ajustar o visual. Zero usa as ações reais; a prévia não concede ações.")]
    private int previewActionPoints;
    [Header("Visual 2D")]
    [SerializeField] private Animator spriteAnimator;
    [SerializeField, Min(0.1f)] private float testSpriteHeight = 1.9f;
    [SerializeField] private Vector3 spriteOffset = new Vector3(0f, 0.8f, 0f);
    [Header("Animação do trabalhador")]
    [SerializeField, Min(1f)] private float idleFps = 8f;
    [SerializeField, Min(1f)] private float runFps = 12f;
    [Header("Percurso da casa (ajustável em Play Mode)")]
    [SerializeField] private Transform homeMarker;
    [SerializeField] private Transform outsideMarker;
    [SerializeField] private Transform doorwayMarker;
    [SerializeField, Range(0.05f, 1f)] private float indoorScale = 0.4f;

    public static WorkerAssignment Selected { get; private set; }
    public bool UsedToday => HousePreviewActive || IsUnavailableToday || actionsRemaining <= 0;
    public bool HousePreviewActive { get; set; }
    public Transform SpriteVisual => spriteVisual;
    internal void GetTutorialSprites(List<SpriteRenderer> sprites)
    {
        if (spriteRenderer != null) sprites.Add(spriteRenderer);
        if (actionDotsRoot == null) return;
        foreach (Transform dot in actionDotsRoot)
            if (dot.TryGetComponent(out SpriteRenderer icon)) sprites.Add(icon);
    }
    public int ActionsRemaining => actionsRemaining;
    public ActivityKind? LastActivity { get; private set; }
    public bool IsUnavailableToday { get; private set; }
    public Vector3 HomePosition => homeMarker != null ? homeMarker.position : initialHomePosition;
    public Transform OutsideMarker => outsideMarker;
    public Transform DoorwayMarker => doorwayMarker;
    public float IndoorScale => indoorScale;
    public int ProductionReduction { get; private set; }
    public WorkProgress Progress => workProgress;
    public string DisplayName => workerName;
    public int OriginHouseNumber => originHouseNumber;
    public Transform HomeMarker => homeMarker;
    public int AssignmentVersion => actionResolver != null ? actionResolver.AssignmentVersion : 0;

    private int nextDayProductionReduction;
    private int absenceDaysRemaining;
    private int actionsRemaining = 1;
    private int nextDayExtraActions;
    private int coffeeActions;
    private ActivityKind? coffeeExcludedActivity;
    private readonly List<Transform> actionDots = new List<Transform>();
    private Transform actionDotsRoot;
    private ParticleSystem comadreDizzy;
    private Transform spriteVisual;
    private SpriteRenderer spriteRenderer;
    private Camera spriteCamera;
    private Vector3 initialHomePosition;
    private Vector3 previousPosition;
    private bool isWorking;
    private bool? routeFlipX;
    private Vector3? workFacingTarget;
    private float walkedSinceStep;
    private enum AnimationState { Idle, Run, Work }
    private static readonly int StateParameter = Animator.StringToHash("State");
    private static readonly int IdleSpeedParameter = Animator.StringToHash("IdleSpeed");
    private static readonly int RunSpeedParameter = Animator.StringToHash("RunSpeed");
    private const float IdleClipFps = 8f;
    private const float RunClipFps = 24f;

    private void Awake()
    {
        if (homeMarker != null)
            transform.position = homeMarker.position;
        initialHomePosition = transform.position;
        previousPosition = transform.position;
        spriteCamera = Camera.main;
        if (spriteAnimator != null && spriteAnimator.TryGetComponent(out spriteRenderer))
        {
            spriteVisual = spriteRenderer.transform;
            spriteRenderer.flipX = false;
            RefreshSpriteAppearance();
        }
        if (comadreDizzyPrefab != null)
        {
            comadreDizzy = Instantiate(comadreDizzyPrefab, transform, false);
            comadreDizzy.name = "Tontura - Comadre Fulozinha";
            comadreDizzy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var particles = comadreDizzy.main;
            particles.simulationSpace = ParticleSystemSimulationSpace.Local;
            if (spriteRenderer != null && comadreDizzy.TryGetComponent(out ParticleSystemRenderer stars))
            {
                stars.sortingLayerID = spriteRenderer.sortingLayerID;
                stars.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
        }
        actionDotsRoot = new GameObject("Ações - " + workerName).transform;
        actionDotsRoot.SetParent(transform.parent, false);
        RefreshActionDots(false);
    }

    private void LateUpdate()
    {
        if (spriteVisual != null)
        {
            if (spriteCamera == null) spriteCamera = Camera.main;
            if (spriteCamera != null)
                spriteVisual.rotation = Quaternion.Euler(0f, spriteCamera.transform.eulerAngles.y, 0f);
            UpdateSpriteAnimation();
            if (routeFlipX is bool flip && spriteRenderer != null) spriteRenderer.flipX = flip;
            if (IsUnavailableToday) PositionDizzyEffect();
        }
        if (actionDotsRoot != null)
        {
            actionDotsRoot.position = transform.position + actionPointOffset;
            if (spriteCamera == null) spriteCamera = Camera.main;
            if (spriteCamera != null) actionDotsRoot.rotation = spriteCamera.transform.rotation;
        }
    }

    private void PositionDizzyEffect()
    {
        if (comadreDizzy == null || spriteRenderer == null || spriteRenderer.sprite == null) return;
        Sprite frame = spriteRenderer.sprite;
        // As orelhas ficam à esquerda do centro no quadro de repouso.
        Vector2 ears = Vector2.Scale(frame.rect.size, new Vector2(0.28f, 0.94f));
        Vector2 offset = (ears - frame.pivot) / frame.pixelsPerUnit;
        if (spriteRenderer.flipX) offset.x = -offset.x;
        Vector3 head = spriteRenderer.transform.TransformPoint(offset) + Vector3.up * 0.08f;
        if (spriteCamera != null)
            head += Vector3.ProjectOnPlane(spriteCamera.transform.position - head,
                Vector3.up).normalized * 0.12f;
        comadreDizzy.transform.position = head;
    }

    public void RefreshSpriteAppearance()
    {
        if (spriteVisual == null) return;
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        spriteVisual.localPosition = spriteOffset;
        spriteVisual.localScale = Vector3.one * (testSpriteHeight / spriteRenderer.sprite.bounds.size.y);
        spriteAnimator.SetFloat(IdleSpeedParameter, Mathf.Max(1f, idleFps) / IdleClipFps);
        spriteAnimator.SetFloat(RunSpeedParameter, Mathf.Max(1f, runFps) / RunClipFps);
    }

    public void FaceTowards(Vector3 destination)
    {
        routeFlipX = null;
        previousPosition = transform.position;
        UpdateFacing(destination - transform.position);
    }

    internal void FaceForRoute(bool returning)
    {
        routeFlipX = returning;
        previousPosition = transform.position;
        if (spriteRenderer != null) spriteRenderer.flipX = returning;
    }

    public void SetWorking(bool value)
    {
        isWorking = value;
        workFacingTarget = null;
    }

    public void SetWorkingTowards(Vector3 target)
    {
        SetWorking(true);
        workFacingTarget = target;
        FaceTowards(target);
    }

    public void RestoreHomeFacing()
    {
        routeFlipX = null;
        previousPosition = transform.position;
        if (spriteRenderer != null)
            spriteRenderer.flipX = false; // A arte de repouso original olha para a esquerda.
    }

    private void UpdateFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (spriteRenderer == null || direction.sqrMagnitude < 0.000001f) return;
        if (spriteCamera == null) spriteCamera = Camera.main;
        float horizontal = direction.x;
        if (spriteCamera != null)
        {
            Vector3 origin = transform.position;
            Vector3 screenDirection = spriteCamera.WorldToScreenPoint(origin + direction) -
                spriteCamera.WorldToScreenPoint(origin);
            // A arte é lateral. Trechos em profundidade mantêm a última orientação.
            if (Mathf.Abs(screenDirection.x) <= Mathf.Abs(screenDirection.y) * 0.12f) return;
            horizontal = screenDirection.x;
        }
        if (Mathf.Abs(horizontal) > 0.0001f)
            spriteRenderer.flipX = horizontal > 0f; // A arte original olha para a esquerda.
    }

    private void UpdateSpriteAnimation()
    {
        Vector3 movement = transform.position - previousPosition;
        previousPosition = transform.position;
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;
        bool moving = movement.sqrMagnitude > 0.0025f * deltaTime * deltaTime;
        if (moving)
        {
            if (!routeFlipX.HasValue) UpdateFacing(movement);
            movement.y = 0f;
            float distance = movement.magnitude;
            if (distance < 0.9f)
            {
                walkedSinceStep += distance;
                if (walkedSinceStep >= 0.72f)
                {
                    walkedSinceStep -= 0.72f;
                    AudioManager.Play(SoundCue.WorkerStep);
                }
            }
            else walkedSinceStep = 0f;
        }
        else if (isWorking && workFacingTarget is Vector3 target)
            UpdateFacing(target - transform.position);
        AnimationState state = AnimationState.Idle;
        if (moving) state = AnimationState.Run;
        else if (isWorking) state = AnimationState.Work;
        spriteAnimator.SetInteger(StateParameter, (int)state);
    }

    private void OnDestroy()
    {
        if (actionDotsRoot != null) Destroy(actionDotsRoot.gameObject);
    }

    private void OnEnable()
    {
        SetHovered(false);
    }

    private void OnDisable()
    {
        if (Selected == this)
            Selected = null;
    }

    public void Select() => Selected = this;

    public void SetHovered(bool value)
    {
        // O quadrado amarelo de destino foi removido da interação com cartas.
        // Mantemos o componente e a referência serializada por compatibilidade
        // com a cena; a detecção de trabalhador continua no ActivityCardDrag.
        if (selectionMarker != null && selectionMarker.activeSelf)
            selectionMarker.SetActive(false);
    }

    public void TryAssign(ActivityKind activity, string activityName, ActivityCardDrag card = null)
    {
        if (actionResolver != null)
            actionResolver.TryAssign(this, activity, activityName, card);
    }

    public void SetStatus(string status)
    {
        if (statusText != null)
            statusText.text = workerName + ": " + status;
    }

    public bool CanAct(ActivityKind activity) => !HousePreviewActive && !IsUnavailableToday && actionsRemaining > 0 &&
        (actionsRemaining > coffeeActions || coffeeExcludedActivity != activity);

    public void MarkUsed(string activityName, ActivityKind activity)
    {
        bool spendingCoffeeAction = coffeeActions > 0 && actionsRemaining <= coffeeActions;
        actionsRemaining = Mathf.Max(0, actionsRemaining - 1);
        RefreshActionDots(true);
        if (spendingCoffeeAction)
        {
            coffeeActions--;
            if (coffeeActions > 0) coffeeExcludedActivity = activity;
        }
        else if (coffeeActions > 0) coffeeExcludedActivity = activity;
        if (coffeeActions == 0) coffeeExcludedActivity = null;
        LastActivity = activity;
        Select();
        SetStatus(activityName);
    }

    public void MarkRepeatedAction(string activityName, ActivityKind activity)
    {
        LastActivity = activity;
        Select();
        SetStatus(activityName);
    }

    public void GrantCoffeeAction()
    {
        actionsRemaining++;
        RefreshActionDots(true);
        coffeeActions++;
        if (LastActivity != ActivityKind.Coffee) coffeeExcludedActivity = LastActivity;
        LastActivity = ActivityKind.Coffee;
    }

    public void ScheduleExtraAction() => nextDayExtraActions++;

    public void ConfigureNewWorker(string name)
    {
        if (statusText != null)
        {
            float bottom = statusText.rectTransform.anchoredPosition.y;
            foreach (Text existing in statusText.transform.parent.GetComponentsInChildren<Text>(false))
                bottom = Mathf.Min(bottom, existing.rectTransform.anchoredPosition.y);
            statusText = Instantiate(statusText, statusText.transform.parent);
            statusText.name = "Status - " + name;
            RectTransform label = statusText.rectTransform;
            label.anchoredPosition = new Vector2(label.anchoredPosition.x,
                bottom - label.rect.height - 2f);
            RectTransform background = label.parent as RectTransform;
            if (background != null)
                background.sizeDelta = new Vector2(background.sizeDelta.x,
                    background.sizeDelta.y + label.rect.height + 2f);
        }
        workerName = name;
        if (actionDotsRoot != null) actionDotsRoot.name = "Ações - " + name;
        initialHomePosition = transform.position;
        SetStatus("livre");
    }

    public void SetHouseRoute(Transform home, Transform outside, Transform doorway)
    {
        homeMarker = home;
        outsideMarker = outside;
        doorwayMarker = doorway;
    }

    public void SetOriginHouseNumber(int number) => originHouseNumber = number;

    public void JoinDuringDay()
    {
        actionsRemaining = 0;
        RefreshActionDots(false);
        SetStatus("novo morador");
    }

    public void BeginNextDay()
    {
        IsUnavailableToday = absenceDaysRemaining > 0;
        if (IsUnavailableToday) absenceDaysRemaining--;
        if (comadreDizzy != null)
        {
            if (IsUnavailableToday)
            {
                PositionDizzyEffect();
                comadreDizzy.Play(true);
            }
            else comadreDizzy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        actionsRemaining = IsUnavailableToday ? 0 : 1 + nextDayExtraActions;
        RefreshActionDots(true);
        nextDayExtraActions = 0;
        coffeeActions = 0;
        coffeeExcludedActivity = null;
        LastActivity = null;
        ProductionReduction = nextDayProductionReduction;
        nextDayProductionReduction = 0;
        SetStatus(IsUnavailableToday ? "ausente" : ProductionReduction > 0 ? "produção reduzida" :
            "livre");
        SetHovered(false);
        if (Selected == this)
            Selected = null;
    }

    public void ScheduleAbsence(int days)
    {
        absenceDaysRemaining = Mathf.Max(absenceDaysRemaining, days);
    }

    public void ScheduleNextDayCondition(int productionReduction)
    {
        nextDayProductionReduction = Mathf.Max(0, productionReduction);
    }

    private void RefreshActionDots(bool animate, int? previewCount = null)
    {
        if (actionDotsRoot == null) return;
        int visibleCount = previewCount ?? actionsRemaining;
        while (actionDots.Count > visibleCount)
        {
            Transform dot = actionDots[actionDots.Count - 1];
            actionDots.RemoveAt(actionDots.Count - 1);
            dot.DOKill();
            if (animate)
                DOTween.Sequence().SetTarget(dot).SetLink(dot.gameObject)
                    .Append(dot.DOPunchScale(dot.localScale * 0.35f, 0.13f))
                    .Append(dot.DOScale(Vector3.zero, 0.13f))
                    .OnComplete(() => { if (dot != null) Destroy(dot.gameObject); });
            else Destroy(dot.gameObject);
        }
        while (actionDots.Count < visibleCount)
        {
            GameObject icon = new GameObject("Ação " + (actionDots.Count + 1),
                typeof(SpriteRenderer));
            icon.transform.SetParent(actionDotsRoot, false);
            SpriteRenderer visual = icon.GetComponent<SpriteRenderer>();
            visual.sprite = actionPointSprite;
            visual.sortingOrder = 110;
            Transform dot = icon.transform;
            actionDots.Add(dot);
            float spriteHeight = actionPointSprite != null ?
                actionPointSprite.bounds.size.y : 1f;
            Vector3 size = Vector3.one * (actionPointSize / Mathf.Max(0.01f, spriteHeight));
            dot.localScale = animate ? Vector3.zero : size;
            if (animate)
                DOTween.Sequence().SetTarget(dot).SetLink(dot.gameObject)
                    .Append(dot.DOScale(size, 0.17f).SetEase(Ease.OutBack))
                    .Append(dot.DOPunchScale(size * 0.35f, 0.2f));
        }
        for (int i = 0; i < actionDots.Count; i++)
            actionDots[i].localPosition = new Vector3(
                (i - (actionDots.Count - 1) * 0.5f) * actionPointSpacing, 0f, 0f);
    }

    public void RefreshActionPointAppearance()
    {
        if (actionDotsRoot == null) return;
        RefreshActionDots(false, previewActionPoints > 0 ? previewActionPoints : (int?)null);
        actionDotsRoot.position = transform.position + actionPointOffset;
        float spriteHeight = actionPointSprite != null ?
            actionPointSprite.bounds.size.y : 1f;
        Vector3 scale = Vector3.one * (actionPointSize / Mathf.Max(0.01f, spriteHeight));
        for (int i = 0; i < actionDots.Count; i++)
        {
            Transform dot = actionDots[i];
            if (dot == null) continue;
            dot.DOKill();
            dot.localScale = scale;
            dot.localPosition = new Vector3(
                (i - (actionDots.Count - 1) * 0.5f) * actionPointSpacing, 0f, 0f);
            SpriteRenderer renderer = dot.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.sprite = actionPointSprite;
        }
    }
}
