using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public enum AnimalKind { None, Cow, Chicken }

public sealed class Plot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    // Na grade de PrototypeLayout, a areia desce até cerca de 0,084 m abaixo da origem dos slots.
    private const float FenceFootOffset = -0.09f;
    private const float FenceOutlinePadding = 0.13f;
    private static readonly System.Random chickenFacingRandom = new System.Random();

    [SerializeField] private ActionResolver actionResolver;
    [Tooltip("Pontos opcionais do trajeto, na ordem da ida. A volta usa a ordem inversa.")]
    [SerializeField] private Transform[] workWaypoints;
    [Tooltip("Opcional: posição do funcionário durante a atividade neste terreno.")]
    [SerializeField] private Transform workPoint;
    [SerializeField, Min(0f), Tooltip("Velocidade neste terreno. Zero usa a velocidade geral dos funcionários.")]
    private float workTravelSpeed;
    [SerializeField] private Renderer surfaceRenderer;
    [SerializeField, Tooltip("Contorno discreto do terreno enquanto ele está livre.")]
    private GameObject idleOutline;
    [SerializeField] private Material plowedMaterial;
    [SerializeField] private GameObject plowedVisual; // Visual opcional: mantém o solo lógico e collider.
    [SerializeField, Tooltip("Slot de água: bloqueia ações de cultivo, cercas e animais.")]
    private bool isWater;
    [SerializeField] private GameObject cornVisual;
    [SerializeField] private GameObject pumpkinVisual;
    [SerializeField] private Color jerimumColor = new Color(0.92f, 0.42f, 0.08f);
    [SerializeField, Min(0.1f)] private float adjacentDistance = 2.5f;
    [SerializeField] private GameObject growthBar;
    [SerializeField] private GameObject[] fenceParts;
    [SerializeField] private GameObject joinedFenceModel; // Modelo FBX para cercas unificadas.
    [SerializeField, Min(0f)] private float fencePieceDelay = 0.06f;
    [SerializeField, Min(0.01f)] private float fencePieceDuration = 0.28f;
    [SerializeField, Tooltip("Direção do bico na arte original da galinha, antes de aplicar o espelhamento.")]
    private bool chickenSpriteFacesRight = true;
    [SerializeField] private Transform growthFill;
    [SerializeField, Min(0.01f)] private float growthFillWidth = 0.84f;
    [SerializeField] private Color readyGrowthColor = new Color(0.16f, 0.75f, 0.2f);
    [SerializeField] private Color wiltingGrowthColor = new Color(0.90f, 0.20f, 0.16f);

    public bool IsPlowed { get; private set; }
    public bool IsFenced { get; private set; }
    public bool IsWater => isWater;
    public CropType Crop { get; private set; }
    public AnimalKind Animal { get; private set; }
    public int PlantedDay { get; private set; }
    internal bool IsWilting => willWilt;
    private int growthAdjustment;
    private int mandalaGrowthAdjustment;
    private int lastMandalaGrowthDay = -1;
    private bool fertilized;
    private Bounds surfaceBounds;
    private float workClearance = 1.3f;
    private Vector3[] fenceBasePositions;
    private Vector3[] fenceBaseScales;
    private JoinedVisual joinedVisual;
    private bool fenceInitialized;
    private Transform selectionOutline;
    private Mesh selectionMesh;
    private Material selectionMaterial;
    private bool pointerOver;
    private Transform animalVisual;
    private Transform cropModel;
    private Renderer growthFillRenderer;
    private Color growingFillColor;
    private MaterialPropertyBlock growthFillBlock;
    private Quaternion growthBarRotation;
    private Tween wiltingMotion;
    private bool willWilt;
    private Vector3 plowedVisualHome;
    private MandalaPlowedGround mandalaGround;
    internal Bounds PlowedGroundBounds => surfaceBounds;
    internal bool UsesPlowedGround => mandalaGround != null && IsPlowed && !isWater && !IsFenced;
    private static readonly List<Plot> allPlots = new List<Plot>();
    private static readonly Dictionary<string, JoinedVisual> joinedVisuals =
        new Dictionary<string, JoinedVisual>();
    public bool IsEmpty => !isWater && Crop == CropType.None;
    public Vector3 WorkPosition => workPoint != null ? workPoint.position :
        transform.position + Vector3.back * (surfaceRenderer != null ?
            surfaceRenderer.bounds.extents.z + 0.5f : workClearance);
    public float WorkTravelSpeed => workTravelSpeed;
    public Vector3[] WorkWaypoints
    {
        get
        {
            if (workWaypoints == null) return Array.Empty<Vector3>();
            var points = new List<Vector3>(workWaypoints.Length);
            foreach (Transform waypoint in workWaypoints)
                if (waypoint != null) points.Add(waypoint.position);
            return points.ToArray();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3? from = null;
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        if (workWaypoints != null)
            foreach (Transform waypoint in workWaypoints)
            {
                if (waypoint == null) continue;
                if (from.HasValue) Gizmos.DrawLine(from.Value, waypoint.position);
                Gizmos.DrawSphere(waypoint.position, 0.08f);
                from = waypoint.position;
            }
        if (workPoint == null) return;
        if (from.HasValue) Gizmos.DrawLine(from.Value, workPoint.position);
        Gizmos.DrawSphere(workPoint.position, 0.12f);
        Gizmos.DrawLine(workPoint.position, transform.position);
    }
    // Mesma regra de vizinhança ortogonal usada pelos cercados.
    public bool IsAdjacentTo(Plot other) => IsAdjacentTo(other, false);

    internal bool IsAdjacentTo(Plot other, bool includeDiagonals)
    {
        if (other == null || other == this) return false;
        Vector3 d = other.transform.position - transform.position;
        if (Mathf.Abs(d.y) >= 0.2f) return false;
        if (includeDiagonals)
        {
            float x = Mathf.Abs(d.x);
            float z = Mathf.Abs(d.z);
            return x < adjacentDistance && z < adjacentDistance && (x > 0.2f || z > 0.2f);
        }
        return (Mathf.Abs(d.x) < 0.2f && Mathf.Abs(d.z) < adjacentDistance && Mathf.Abs(d.z) > 0.2f) ||
               (Mathf.Abs(d.z) < 0.2f && Mathf.Abs(d.x) < adjacentDistance && Mathf.Abs(d.x) > 0.2f);
    }

    public event Action StateChanged;

    private void NotifyStateChanged()
    {
        if (actionResolver != null) actionResolver.InvalidatePlotHighlights();
        StateChanged?.Invoke();
    }

    private void Awake()
    {
        if (growthBar != null) growthBarRotation = growthBar.transform.localRotation;
        if (growthFill != null)
        {
            growthFillRenderer = growthFill.GetComponent<Renderer>();
            if (growthFillRenderer != null && growthFillRenderer.sharedMaterial != null)
                growingFillColor = growthFillRenderer.sharedMaterial.color;
        }
        if (surfaceRenderer != null)
        {
            surfaceBounds = surfaceRenderer.bounds;
            workClearance = surfaceBounds.extents.z + 0.5f;
        }

        if (growthBar != null && surfaceRenderer != null)
        {
            Transform bar = growthBar.transform;
            Vector3 position = bar.localPosition;
            // Deixa a barra baixa, na areia em frente à borda do canteiro.
            float halfDepth = surfaceBounds.extents.z / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.z));
            position.z = Mathf.Min(position.z, -halfDepth - 0.14f);
            position.y = Mathf.Min(position.y, 0.10f);
            bar.localPosition = position;
        }
        if (fenceParts != null)
        {
            fenceBasePositions = new Vector3[fenceParts.Length];
            fenceBaseScales = new Vector3[fenceParts.Length];
            Vector3 fenceGround = transform.position + Vector3.up * FenceFootOffset;
            for (int i = 0; i < fenceParts.Length; i++)
            {
                if (fenceParts[i] == null) continue;
                Transform piece = fenceParts[i].transform;
                Vector3 position = piece.localPosition;
                // As peças têm pivô central; o pé fica ligeiramente dentro da areia.
                position.y = piece.parent.InverseTransformPoint(fenceGround).y +
                    Mathf.Abs(piece.localScale.y) * 0.5f;
                fenceBasePositions[i] = position;
                fenceBaseScales[i] = piece.localScale;
            }
        }
        if (plowedVisual != null)
        {
            plowedVisualHome = plowedVisual.transform.localPosition;
            plowedVisual.SetActive(false);
        }
        if (idleOutline != null) RefreshSurfaceVisual();
    }

    private void OnEnable()
    {
        allPlots.Add(this);
        if (mandalaGround != null) mandalaGround.Invalidate();
        RefreshWiltingMotion();
    }
    private void OnDisable()
    {
        StopWiltingMotion();
        allPlots.Remove(this);
        if (mandalaGround != null) mandalaGround.Invalidate();
        if (allPlots.Count == 0)
        {
            foreach (JoinedVisual joined in joinedVisuals.Values) joined.Dispose();
            joinedVisuals.Clear();
        }
        SetHighlight(false);
    }

    private void OnDestroy()
    {
        if (selectionMesh != null) Destroy(selectionMesh);
        if (selectionMaterial != null) Destroy(selectionMaterial);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isWater) return;
        if (!pointerOver && Time.timeScale > 0f && selectionOutline != null && selectionOutline.gameObject.activeSelf)
            AudioManager.Play(SoundCue.PlotHover);
        pointerOver = true;
        PulseOutline();
        joinedVisual?.SetHovered(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        PulseOutline();
        joinedVisual?.SetHovered(false);
    }

    public bool CanUse(ActivityKind activity, int currentDay, SeedCatalog catalog,
        WorkerAssignment reservationOwner = null)
    {
        if (isWater || (actionResolver != null && actionResolver.IsTargetReserved(this, reservationOwner)))
            return false;
        return activity switch
        {
            ActivityKind.Plow or ActivityKind.BuildFence =>
                !IsPlowed && !IsFenced && IsEmpty && Animal == AnimalKind.None,
            ActivityKind.PlantCorn or ActivityKind.PlantPumpkin or ActivityKind.PlantJerimum =>
                IsPlowed && !IsFenced && IsEmpty && Animal == AnimalKind.None,
            ActivityKind.PlaceCow or ActivityKind.PlaceChicken =>
                IsFenced && IsEmpty && Animal == AnimalKind.None,
            ActivityKind.FeedAnimals => IsFenced && Animal != AnimalKind.None,
            ActivityKind.Harvest => !IsFenced && IsReady(currentDay, catalog),
            ActivityKind.WateringCan => !IsFenced && !IsEmpty &&
                !IsReady(currentDay, catalog) && catalog != null,
            ActivityKind.Fertilizer => !IsFenced && !IsEmpty && !fertilized,
            _ => false
        };
    }

    public static void ShowAvailable(ActivityKind activity, int currentDay, SeedCatalog catalog)
    {
        foreach (JoinedVisual joined in joinedVisuals.Values)
            joined.ShowAvailable(activity, currentDay, catalog);
        foreach (Plot plot in allPlots)
        {
            if (plot == null) continue;
            bool groupAvailable = plot.joinedVisual != null && plot.joinedVisual.HasOutline;
            plot.SetHighlight(!groupAvailable &&
                plot.CanUse(activity, currentDay, catalog));
        }
    }

    public static bool HasAvailable(ActivityKind activity, int currentDay, SeedCatalog catalog)
    {
        foreach (Plot plot in allPlots)
            if (plot != null && plot.CanUse(activity, currentDay, catalog)) return true;
        return false;
    }

    public static void ClearHighlights()
    {
        foreach (Plot plot in allPlots)
            if (plot != null) plot.SetHighlight(false);
        foreach (JoinedVisual joined in joinedVisuals.Values) joined.SetHighlight(false);
    }

    private void SetHighlight(bool visible)
    {
        if (visible && selectionOutline == null) CreateOutline();
        if (selectionOutline != null)
        {
            selectionOutline.gameObject.SetActive(visible);
            PulseOutline();
        }
        RefreshIdleOutline();
    }

    private void RefreshIdleOutline()
    {
        if (idleOutline == null) return;
        bool highlighted = selectionOutline != null && selectionOutline.gameObject.activeSelf;
        bool visible = !isWater && !IsPlowed && !IsFenced && !highlighted;
        if (idleOutline.activeSelf != visible) idleOutline.SetActive(visible);
    }

    private void PulseOutline()
    {
        if (selectionOutline == null) return;
        selectionOutline.DOKill();
        Vector3 scale = Vector3.one;
        if (IsFenced && selectionMesh != null)
        {
            Vector3 size = Vector3.Scale(selectionMesh.bounds.size, selectionOutline.parent.lossyScale);
            scale.x = Mathf.Max(1f, 2f * (FenceHalfX * Mathf.Abs(transform.lossyScale.x) +
                FenceOutlinePadding) / Mathf.Max(0.001f, Mathf.Abs(size.x)));
            scale.z = Mathf.Max(1f, 2f * (FenceHalfZ * Mathf.Abs(transform.lossyScale.z) +
                FenceOutlinePadding) / Mathf.Max(0.001f, Mathf.Abs(size.z)));
        }
        selectionOutline.localScale = scale;
        if (selectionOutline.gameObject.activeSelf && pointerOver)
            selectionOutline.DOScale(scale * 1.07f, 0.35f).SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
    }

    private void CreateOutline()
    {
        MeshFilter surfaceMesh = surfaceRenderer != null ? surfaceRenderer.GetComponent<MeshFilter>() : null;
        Shader shader = Resources.Load<Shader>("PlotOutline");
        if (surfaceMesh == null || surfaceMesh.sharedMesh == null || shader == null) return;
        Bounds bounds = surfaceMesh.sharedMesh.bounds;
        float left = bounds.min.x, right = bounds.max.x;
        float bottom = bounds.min.z, top = bounds.max.z;
        float width = Mathf.Min(right - left, top - bottom) * 0.045f;
        float y = bounds.max.y + 0.015f;
        var vertices = new Vector3[16];
        var triangles = new int[24];
        AddEdge(vertices, triangles, 0, left, left + width, bottom, top, y);
        AddEdge(vertices, triangles, 1, right - width, right, bottom, top, y);
        AddEdge(vertices, triangles, 2, left + width, right - width, bottom, bottom + width, y);
        AddEdge(vertices, triangles, 3, left + width, right - width, top - width, top, y);
        selectionMesh = new Mesh { vertices = vertices, triangles = triangles };
        selectionMesh.RecalculateBounds();
        selectionMaterial = new Material(shader);
        var outline = new GameObject("Contorno de seleção", typeof(MeshFilter), typeof(MeshRenderer));
        selectionOutline = outline.transform;
        selectionOutline.SetParent(surfaceRenderer.transform, false);
        outline.GetComponent<MeshFilter>().sharedMesh = selectionMesh;
        MeshRenderer outlineRenderer = outline.GetComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = selectionMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
    }

    private static void AddEdge(Vector3[] vertices, int[] triangles, int edge,
        float minX, float maxX, float minZ, float maxZ, float y)
    {
        int v = edge * 4, t = edge * 6;
        vertices[v] = new Vector3(minX, y, minZ);
        vertices[v + 1] = new Vector3(maxX, y, minZ);
        vertices[v + 2] = new Vector3(maxX, y, maxZ);
        vertices[v + 3] = new Vector3(minX, y, maxZ);
        triangles[t] = v;
        triangles[t + 1] = v + 2;
        triangles[t + 2] = v + 1;
        triangles[t + 3] = v;
        triangles[t + 4] = v + 3;
        triangles[t + 5] = v + 2;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isWater) return;
        TutorialHints.Once("plots", "Use os terrenos destacados para arar, plantar, cercar ou cuidar das culturas.");
        if (actionResolver != null)
            actionResolver.TryUsePlot(this);
    }

    public void Plow()
    {
        if (isWater || IsPlowed || IsFenced || Animal != AnimalKind.None)
            return;
        IsPlowed = true;
        if (surfaceRenderer != null)
        {
            mandalaGround = MandalaPlowedGround.GetOrCreate(this, allPlots);
            if (mandalaGround != null) mandalaGround.Invalidate();
            else if (plowedMaterial != null) surfaceRenderer.sharedMaterial = plowedMaterial;
        }
        RefreshSurfaceVisual();
        if (plowedVisual != null && plowedVisual.activeSelf)
        {
            Transform visual = plowedVisual.transform;
            visual.DOKill();
            visual.localPosition = plowedVisualHome + Vector3.down * 0.18f;
            visual.DOLocalMove(plowedVisualHome, 0.42f).SetEase(Ease.OutCubic);
        }
        NotifyStateChanged();
    }

    public void Plant(CropType crop, int currentDay, SeedCatalog catalog)
    {
        if (isWater || !IsPlowed || IsFenced || !IsEmpty || Animal != AnimalKind.None || crop == CropType.None)
            return;
        Crop = crop;
        PlantedDay = currentDay;
        growthAdjustment = 0;
        mandalaGrowthAdjustment = 0;
        lastMandalaGrowthDay = -1;
        fertilized = false;
        var art = SertaoPresentationAssets.Current;
        cropModel = art != null ? art.CreateCrop(crop, transform, surfaceBounds) : null;
        if (cornVisual != null) cornVisual.SetActive(cropModel == null && crop == CropType.Corn);
        if (pumpkinVisual != null) pumpkinVisual.SetActive(cropModel == null && crop != CropType.Corn);
        if (cropModel == null) TintRootVisual(crop);
        if (growthBar != null) growthBar.SetActive(true);
        RefreshGrowth(currentDay, catalog);
        RefreshJoinedPlots();
        if (mandalaGround != null) mandalaGround.Invalidate();
        NotifyStateChanged();
        if (MandalaSynergy.Instance != null)
            MandalaSynergy.Instance.ApplyGrowthBonuses(currentDay, catalog);
    }

    public bool IsReady(int currentDay, SeedCatalog catalog)
    {
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        int age = GrowthAge(currentDay);
        return settings != null && age >= settings.growthDays &&
            age < settings.growthDays + settings.witherDaysAfterMaturity;
    }

    public int GrowthAge(int day) => day + growthAdjustment - PlantedDay;

    public List<Plot> ReadyConnectedPlots(int currentDay, SeedCatalog catalog) =>
        ConnectedPlotsFor(ActivityKind.Harvest, currentDay, catalog);

    public IReadOnlyList<Plot> JoinedPlots => joinedVisual?.Members;

    private static bool AffectsJoinedPlots(ActivityKind activity) =>
        activity is ActivityKind.Harvest or ActivityKind.WateringCan or
            ActivityKind.Fertilizer or ActivityKind.FeedAnimals;

    public List<Plot> ConnectedPlotsFor(ActivityKind activity, int currentDay, SeedCatalog catalog)
    {
        var ready = new List<Plot>();
        if (!CanUse(activity, currentDay, catalog)) return ready;
        ready.Add(this);
        if (!AffectsJoinedPlots(activity) || joinedVisual == null) return ready;

        List<Plot> remaining = joinedVisual.EligibleMembers(activity, currentDay, catalog);
        remaining.Remove(this);
        Plot previous = this;
        while (remaining.Count > 0)
        {
            int nearest = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < remaining.Count; i++)
            {
                float candidate = (remaining[i].transform.position - previous.transform.position).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate;
                nearest = i;
            }
            previous = remaining[nearest];
            ready.Add(previous);
            remaining.RemoveAt(nearest);
        }
        return ready;
    }

    public int HarvestYield(int currentDay, SeedCatalog catalog)
    {
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        if (settings == null) return 0;
        int extraDays = Mathf.Max(0, currentDay + growthAdjustment - mandalaGrowthAdjustment -
            PlantedDay - settings.growthDays);
        int baseYield = settings.foodYield + extraDays * settings.bonusYieldPerExtraDay;
        GameBalance balance = GameBalance.Current;
        int fertilizerBonus = fertilized ? (balance != null ? balance.fertilizerHarvestBonus : baseYield) : 0;
        int synergy = MandalaSynergy.Instance != null ? MandalaSynergy.Instance.ExtraCropYield(this) : 0;
        return baseYield + fertilizerBonus + synergy;
    }

    public void RefreshGrowth(int currentDay, SeedCatalog catalog)
    {
        GameObject visual = Crop == CropType.Corn ? cornVisual : pumpkinVisual;
        if (Crop == CropType.None)
            return;
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        int growthDays = settings != null ? settings.growthDays : 2;
        int age = GrowthAge(currentDay);
        int witherAge = settings != null ? growthDays + settings.witherDaysAfterMaturity : int.MaxValue;
        if (settings != null && age >= witherAge)
        {
            Harvest();
            return;
        }
        bool ready = settings != null && age >= growthDays;
        willWilt = ready && age == witherAge - 1;
        RefreshWiltingMotion();
        float progress = Mathf.Clamp01((float)age / Mathf.Max(1, growthDays));
        if (cropModel != null) SertaoPresentationAssets.AnimateCropGrowth(cropModel, progress, Crop);
        else if (visual != null)
        {
            visual.transform.DOKill();
            visual.transform.DOScale(new Vector3(1f, Mathf.Lerp(0.45f, 1f, progress), 1f), 0.35f);
        }
        if (growthFill != null)
        {
            if (growthFillRenderer != null)
            {
                growthFillBlock ??= new MaterialPropertyBlock();
                growthFillRenderer.GetPropertyBlock(growthFillBlock);
                Color fillColor = willWilt ? wiltingGrowthColor :
                    ready ? readyGrowthColor : growingFillColor;
                growthFillBlock.SetColor("_Color", fillColor);
                growthFillBlock.SetColor("_BaseColor", fillColor);
                growthFillRenderer.SetPropertyBlock(growthFillBlock);
            }
            float width = Mathf.Max(0.001f, growthFillWidth * progress);
            Vector3 size = growthFill.localScale;
            growthFill.localScale = new Vector3(width, size.y, size.z);
            Vector3 position = growthFill.localPosition;
            growthFill.localPosition = new Vector3(-growthFillWidth * 0.5f + width * 0.5f,
                position.y, position.z);
        }
    }

    private void RefreshWiltingMotion()
    {
        if (!willWilt || !isActiveAndEnabled)
        {
            StopWiltingMotion();
            return;
        }
        if (growthBar == null || wiltingMotion != null) return;
        Transform bar = growthBar.transform;
        wiltingMotion = DOVirtual.Float(0f, 2f * Mathf.PI, 1.3f, phase =>
            bar.localRotation = growthBarRotation *
                Quaternion.AngleAxis(Mathf.Sin(phase) * 3f, Vector3.forward))
            .SetEase(Ease.Linear).SetLoops(-1).SetLink(growthBar);
    }

    private void StopWiltingMotion()
    {
        if (wiltingMotion == null) return;
        wiltingMotion.Kill();
        wiltingMotion = null;
        if (growthBar != null) growthBar.transform.localRotation = growthBarRotation;
    }

    public CropType Harvest()
    {
        if (isWater) return CropType.None;
        CropType harvested = Crop;
        Crop = CropType.None;
        willWilt = false;
        StopWiltingMotion();
        fertilized = false;
        mandalaGrowthAdjustment = 0;
        lastMandalaGrowthDay = -1;
        if (cropModel != null)
        {
            cropModel.DOKill();
            cropModel.gameObject.SetActive(false);
            Destroy(cropModel.gameObject);
            cropModel = null;
        }
        if (cornVisual != null) cornVisual.SetActive(false);
        if (pumpkinVisual != null) pumpkinVisual.SetActive(false);
        if (growthBar != null) growthBar.SetActive(false);
        RefreshJoinedPlots();
        if (mandalaGround != null) mandalaGround.Invalidate();
        NotifyStateChanged();
        return harvested;
    }

    public void AdvanceGrowth(int days)
    {
        if (Crop == CropType.None || days == 0) return;
        growthAdjustment += days;
        NotifyStateChanged();
    }

    public void RegressGrowthFromPests(int stages, int currentDay, SeedCatalog catalog)
    {
        if (isWater || Crop == CropType.None || stages <= 0 || catalog == null) return;
        CropSettings settings = catalog.Get(Crop);
        if (settings == null) return;
        int age = currentDay + growthAdjustment - PlantedDay;
        int regression = IsReady(currentDay, catalog)
            ? Mathf.Max(stages, age - settings.growthDays + 1) : stages;
        AdvanceGrowth(-regression);
        RefreshGrowth(currentDay, catalog);
    }

    public void AdvanceGrowthTowardMaturity(int days, int currentDay, SeedCatalog catalog)
    {
        if (isWater || Crop == CropType.None || days <= 0 || catalog == null ||
            lastMandalaGrowthDay == currentDay) return;
        CropSettings settings = catalog.Get(Crop);
        if (settings == null) return;
        int remaining = settings.growthDays - (currentDay + growthAdjustment - PlantedDay);
        if (remaining > 0)
        {
            int advance = Mathf.Min(days, remaining);
            mandalaGrowthAdjustment += advance;
            lastMandalaGrowthDay = currentDay;
            AdvanceGrowth(advance);
            RefreshGrowth(currentDay, catalog);
        }
    }

    public void Fertilize()
    {
        if (isWater || Crop == CropType.None || fertilized) return;
        fertilized = true;
        NotifyStateChanged();
    }

    public void BuildFence()
    {
        if (isWater || IsPlowed || IsFenced || !IsEmpty || Animal != AnimalKind.None) return;
        IsFenced = true;
        if (fenceParts != null && fenceBasePositions != null) FormFencePerimeter(true);
        else AudioManager.Play(SoundCue.FenceBuilt);
        RefreshJoinedPlots();
        NotifyStateChanged();
    }

    private Plot[] FencedNeighbors()
    {
        var neighbors = new Plot[4]; // norte, leste, sul, oeste
        foreach (Plot other in allPlots)
        {
            if (other == this || other == null || Animal == AnimalKind.None ||
                other.Animal != Animal || !other.IsFenced) continue;
            Vector3 offset = other.transform.position - transform.position;
            if (Mathf.Abs(offset.y) >= 0.2f) continue;
            if (Mathf.Abs(offset.x) < 0.2f && Mathf.Abs(offset.z) < adjacentDistance)
                neighbors[offset.z > 0f ? 0 : 2] = other;
            else if (Mathf.Abs(offset.z) < 0.2f && Mathf.Abs(offset.x) < adjacentDistance)
                neighbors[offset.x > 0f ? 1 : 3] = other;
        }
        return neighbors;
    }

    private float FenceHalfX => fenceBasePositions != null && fenceBasePositions.Length > 3 ?
        Mathf.Abs(fenceBasePositions[3].x) + fenceBaseScales[3].x * 0.5f : 0.74f;
    private float FenceHalfZ => fenceBasePositions != null && fenceBasePositions.Length > 0 ?
        Mathf.Abs(fenceBasePositions[0].z) + fenceBaseScales[0].z * 0.5f : 0.785f;

    private bool FenceTarget(int index, Plot[] neighbors, out Vector3 position, out Vector3 scale)
    {
        position = fenceBasePositions[index];
        scale = fenceBaseScales[index];
        int side = index / 3;
        if (neighbors[side] != null) return false;

        // Os 12 segmentos existentes já formam o cercado. Ao juntar terrenos,
        // retiramos somente os lados internos: nenhuma peça é esticada ou substituída.
        return true;
    }

    private static void RefreshFencedPlots() => RefreshJoinedPlots();

    private void HideOriginalFenceParts()
    {
        if (fenceParts == null || fenceBasePositions == null) return;
        fenceInitialized = false;
        for (int i = 0; i < fenceParts.Length; i++)
        {
            GameObject part = fenceParts[i];
            if (part == null) continue;
            Transform piece = part.transform;
            piece.DOKill();
            if (!part.activeSelf) continue;
            int index = i;
            piece.DOLocalMoveY(fenceBasePositions[i].y - 0.45f,
                fencePieceDuration).SetDelay(i * fencePieceDelay * 0.5f)
                .SetEase(Ease.InSine).OnComplete(() =>
                {
                    part.SetActive(false);
                    piece.localPosition = fenceBasePositions[index];
                    piece.localScale = fenceBaseScales[index];
                });
        }
    }

    private void FormFencePerimeter(bool playBuiltSound = false)
    {
        fenceInitialized = true;
        Plot[] neighbors = FencedNeighbors();
        Tween lastArrival = null;
        for (int i = 0; i < fenceParts.Length; i++)
        {
            GameObject part = fenceParts[i];
            if (part == null) continue;
            Transform piece = part.transform;
            bool visible = FenceTarget(i, neighbors, out Vector3 position, out Vector3 scale);
            if (!visible)
            {
                part.SetActive(false);
                piece.localPosition = fenceBasePositions[i];
                piece.localScale = fenceBaseScales[i];
                continue;
            }
            bool arriving = !part.activeSelf;
            if (arriving)
            {
                piece.localPosition = position - Vector3.up * 0.45f;
                piece.localScale = scale;
                lastArrival = DOTween.Sequence().SetTarget(piece)
                    .AppendInterval(i * fencePieceDelay)
                    .AppendCallback(() => part.SetActive(true))
                    .Append(piece.DOLocalMove(position, fencePieceDuration + 0.1f)
                        .SetEase(Ease.OutBack));
                continue;
            }
            if ((piece.localPosition - position).sqrMagnitude < 0.0001f &&
                (piece.localScale - scale).sqrMagnitude < 0.0001f) continue;
            piece.DOLocalMove(position, fencePieceDuration + 0.1f).SetDelay(0.12f)
                .SetEase(Ease.OutSine);
            piece.DOScale(scale, fencePieceDuration + 0.1f).SetDelay(0.12f)
                .SetEase(Ease.OutSine);
        }
        if (!playBuiltSound) return;
        if (lastArrival == null) AudioManager.Play(SoundCue.FenceBuilt);
        else lastArrival.OnComplete(() =>
        {
            if (this != null && isActiveAndEnabled) AudioManager.Play(SoundCue.FenceBuilt);
        });
    }

    public bool PlaceAnimal(AnimalKind kind)
    {
        if (isWater || !IsFenced || !IsEmpty || Animal != AnimalKind.None || kind == AnimalKind.None)
            return false;
        Animal = kind;
        var root = new GameObject(kind == AnimalKind.Cow ? "Vaca" : "Galinha");
        root.transform.SetParent(transform, false);
        root.layer = gameObject.layer;
        animalVisual = root.transform;
        var art = SertaoPresentationAssets.Current;
        Sprite[] frames = art != null ? art.AnimalFrames(kind) : null;
        if (frames != null && frames.Length > 0 && frames[0] != null)
            SertaoAnimalIdle.CreateGroup(root.transform, kind, frames);
        else if (kind == AnimalKind.Cow)
        {
            AddAnimalPart(root.transform, "Corpo", new Vector3(0f, 0.43f, 0f),
                new Vector3(0.62f, 0.38f, 0.46f), new Color(0.92f, 0.9f, 0.8f));
            AddAnimalPart(root.transform, "Cabeça", new Vector3(0.33f, 0.47f, 0f),
                new Vector3(0.29f, 0.29f, 0.28f), new Color(0.3f, 0.24f, 0.19f));
            AddAnimalPart(root.transform, "Mancha", new Vector3(-0.09f, 0.64f, 0.07f),
                new Vector3(0.21f, 0.04f, 0.19f), new Color(0.29f, 0.23f, 0.18f));
        }
        else
        {
            AddAnimalPart(root.transform, "Corpo", new Vector3(0f, 0.38f, 0f),
                new Vector3(0.36f, 0.32f, 0.35f), new Color(0.98f, 0.83f, 0.37f));
            AddAnimalPart(root.transform, "Cabeça", new Vector3(0.18f, 0.53f, 0f),
                new Vector3(0.2f, 0.2f, 0.2f), new Color(1f, 0.9f, 0.52f));
            AddAnimalPart(root.transform, "Bico", new Vector3(0.31f, 0.51f, 0f),
                new Vector3(0.12f, 0.07f, 0.12f), new Color(0.95f, 0.39f, 0.12f));
        }
        PositionAnimals(root.transform, kind);
        root.transform.localScale = Vector3.zero;
        root.transform.DOScale(1f, 0.32f).SetEase(Ease.OutBack)
            .OnComplete(() => root.transform.DOPunchScale(Vector3.one * 0.08f, 0.2f));
        RefreshFencedPlots();
        NotifyStateChanged();
        return true;
    }

    private void PositionAnimals(Transform group, AnimalKind kind)
    {
        SpriteRenderer[] animals = group.GetComponentsInChildren<SpriteRenderer>();
        if (kind == AnimalKind.Chicken && animals.Length == 3)
        {
            int facingLeft = chickenFacingRandom.Next(animals.Length);
            for (int i = 0; i < animals.Length; i++)
            {
                bool facesRight = i != facingLeft;
                animals[i].flipX = facesRight != chickenSpriteFacesRight;
            }
        }

        // As animações usam o pé desenhado como origem, sem contar a margem transparente.
        float groundY = transform.position.y + FenceFootOffset + 0.03f;
        float feet = group.position.y;
        if (animals.Length == 0)
        {
            Renderer[] parts = group.GetComponentsInChildren<Renderer>();
            if (parts.Length > 0) feet = parts[0].bounds.min.y;
            foreach (Renderer part in parts)
                feet = Mathf.Min(feet, part.bounds.min.y);
        }
        group.position += Vector3.up * (groundY - feet);
    }

    public void PulseAnimal()
    {
        if (animalVisual == null) return;
        animalVisual.DOKill();
        animalVisual.localScale = Vector3.one;
        animalVisual.DOPunchScale(Vector3.one * 0.09f, 0.19f, 2, 0.45f)
            .SetLink(animalVisual.gameObject);
    }

    private static void AddAnimalPart(Transform parent, string name, Vector3 position,
        Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Collider hitbox = part.GetComponent<Collider>();
        if (hitbox != null) Destroy(hitbox);
        Renderer renderer = part.GetComponent<Renderer>();
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", color);
        tint.SetColor("_Color", color);
        renderer.SetPropertyBlock(tint);
    }

    private void TintRootVisual(CropType crop)
    {
        if (pumpkinVisual == null) return;
        foreach (Renderer part in pumpkinVisual.GetComponentsInChildren<Renderer>(true))
        {
            if (part.sharedMaterial == null || !part.sharedMaterial.HasProperty("_BaseColor")) continue;
            Color original = part.sharedMaterial.GetColor("_BaseColor");
            var properties = new MaterialPropertyBlock();
            part.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", crop == CropType.Jerimum ? jerimumColor : original);
            part.SetPropertyBlock(properties);
        }
    }

    // O modelo detalhado é somente visual; toda lógica continua no Solo original.
    private void RefreshSurfaceVisual()
    {
        bool showPlowed = plowedVisual != null && IsPlowed && !UsesPlowedGround &&
            (joinedVisual == null || joinedVisual.UsesDetailedPlowed);
        if (plowedVisual != null && plowedVisual.activeSelf != showPlowed)
            plowedVisual.SetActive(showPlowed);
        if (surfaceRenderer != null)
            surfaceRenderer.enabled = !IsFenced && joinedVisual == null && !showPlowed &&
                !UsesPlowedGround && (idleOutline == null || IsPlowed);
        RefreshIdleOutline();
    }

    private static void RefreshJoinedPlots()
    {
        var components = new Dictionary<string, List<Plot>>();
        var visited = new HashSet<Plot>();
        foreach (Plot plot in allPlots)
        {
            if (plot == null || plot.surfaceRenderer == null || visited.Contains(plot) ||
                plot.Crop == CropType.None && plot.Animal == AnimalKind.None) continue;
            var members = new List<Plot>();
            var queue = new Queue<Plot>();
            queue.Enqueue(plot);
            visited.Add(plot);
            while (queue.Count > 0)
            {
                Plot current = queue.Dequeue();
                members.Add(current);
                foreach (Plot other in allPlots)
                    if (other != null && !visited.Contains(other) && SameResource(current, other) &&
                        Adjacent(current, other))
                    {
                        visited.Add(other);
                        queue.Enqueue(other);
                    }
            }
            if (members.Count < 2) continue;
            var ids = new List<int>();
            foreach (Plot member in members) ids.Add(member.GetInstanceID());
            ids.Sort();
            components[string.Join(",", ids)] = members;
        }

        var obsolete = new List<string>();
        foreach (string key in joinedVisuals.Keys)
            if (!components.ContainsKey(key)) obsolete.Add(key);
        foreach (string key in obsolete)
        {
            joinedVisuals[key].Dispose(true);
            joinedVisuals.Remove(key);
        }
        foreach (KeyValuePair<string, List<Plot>> entry in components)
        {
            if (!joinedVisuals.TryGetValue(entry.Key, out JoinedVisual joined))
            {
                joined = new JoinedVisual(entry.Value);
                joinedVisuals.Add(entry.Key, joined);
            }
            foreach (Plot member in entry.Value)
                member.joinedVisual = joined;
        }
        foreach (Plot plot in allPlots)
            if (plot != null) plot.RefreshSurfaceVisual();
        foreach (Plot plot in allPlots)
            if (plot != null && plot.IsFenced && plot.joinedVisual == null &&
                !plot.fenceInitialized && plot.fenceParts != null)
                plot.FormFencePerimeter();
    }

    internal bool CanJoinPlowedGround(Plot other) => UsesPlowedGround && other.UsesPlowedGround &&
        mandalaGround == other.mandalaGround && SameResource(this, other) && Adjacent(this, other);

    private static bool SameResource(Plot a, Plot b) =>
        (!a.IsFenced && !b.IsFenced && a.Crop != CropType.None && a.Crop == b.Crop) ||
        (a.IsFenced && b.IsFenced && a.Animal != AnimalKind.None && a.Animal == b.Animal);

    private static bool Adjacent(Plot a, Plot b)
    {
        Vector3 distance = b.transform.position - a.transform.position;
        if (Mathf.Abs(distance.y) >= 0.2f) return false;
        float x = Mathf.Abs(distance.x), z = Mathf.Abs(distance.z);
        float reach = Mathf.Min(a.adjacentDistance, b.adjacentDistance);
        return (x > 0.2f && x < reach && z < 0.2f) ||
            (z > 0.2f && z < reach && x < 0.2f);
    }

    private struct BorderEdge
    {
        public bool Horizontal;
        public float Line;
        public float Start;
        public float End;
        public int Normal;
        public BorderEdge(bool horizontal, float line, float start, float end, int normal)
        {
            Horizontal = horizontal;
            Line = line;
            Start = start;
            End = end;
            Normal = normal;
        }
    }

    private sealed class JoinedVisual
    {
        private readonly List<Plot> members;
        public IReadOnlyList<Plot> Members => members;
        private readonly GameObject root;
        private readonly Mesh surfaceMesh;
        private readonly Mesh bridgeMesh;
        private readonly Mesh outlineMesh;
        private Mesh eligibleOutline;
        private readonly Material outlineMaterial;
        private readonly Transform outline;
        private readonly Vector2 outlineCenter;
        private readonly float outlineHeight;
        private readonly List<Transform> fencePieces = new List<Transform>();
        private bool hovered;
        public bool IsAnimalGroup { get; }
        public bool UsesDetailedPlowed { get; }
        public bool HasOutline => outline != null && outline.gameObject.activeSelf;

        public List<Plot> EligibleMembers(ActivityKind activity, int currentDay, SeedCatalog catalog) =>
            members.FindAll(plot => plot != null && plot.CanUse(activity, currentDay, catalog));

        public void ShowAvailable(ActivityKind activity, int currentDay, SeedCatalog catalog)
        {
            if (!AffectsJoinedPlots(activity) || outlineMaterial == null)
            {
                SetHighlight(false);
                return;
            }

            ClearEligibleOutline();
            List<Plot> eligible = EligibleMembers(activity, currentDay, catalog);
            if (eligible.Count < 2 || eligible.Count == members.Count)
            {
                SetHighlight(eligible.Count >= 2 && outlineMesh.vertexCount > 0);
                return;
            }

            List<Rect> regions = BuildRegions(eligible);
            bool[,] filled = FillRegionGrid(regions, out List<float> xs, out List<float> zs);
            var center = new Vector2((xs[0] + xs[xs.Count - 1]) * 0.5f,
                (zs[0] + zs[zs.Count - 1]) * 0.5f);
            eligibleOutline = BuildOutline(CollectBorder(xs, zs, filled), center, outlineHeight,
                IsAnimalGroup ? FenceOutlinePadding : 0f);
            outline.localPosition = new Vector3(center.x - outlineCenter.x, 0f, center.y - outlineCenter.y);
            outline.GetComponent<MeshFilter>().sharedMesh = eligibleOutline;
            SetHighlight(eligibleOutline.vertexCount > 0);
        }

        public JoinedVisual(List<Plot> connected)
        {
            members = connected;
            IsAnimalGroup = connected[0].Animal != AnimalKind.None;
            UsesDetailedPlowed = !IsAnimalGroup;
            foreach (Plot plot in connected)
                if (!plot.IsPlowed || plot.plowedVisual == null)
                    UsesDetailedPlowed = false;
            var regions = BuildRegions(connected);
            float left = float.MaxValue, right = float.MinValue;
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (Rect region in regions)
            {
                left = Mathf.Min(left, region.xMin);
                right = Mathf.Max(right, region.xMax);
                bottom = Mathf.Min(bottom, region.yMin);
                top = Mathf.Max(top, region.yMax);
            }
            Vector2 center = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
            root = new GameObject("Área unificada - " + (IsAnimalGroup ? connected[0].Animal.ToString() :
                connected[0].Crop.ToString()), typeof(MeshFilter), typeof(MeshRenderer),
                typeof(MeshCollider), typeof(PlotJoinedSurface));
            root.transform.SetParent(connected[0].transform.parent, true);
            root.transform.position = new Vector3(center.x, 0f, center.y);
            root.layer = connected[0].gameObject.layer;
            float surfaceTop = connected[0].surfaceBounds.max.y + 0.002f;
            float surfaceBottom = connected[0].surfaceBounds.min.y;
            surfaceMesh = BuildSurface(regions, center, surfaceTop, surfaceBottom,
                out List<BorderEdge> border);
            root.GetComponent<MeshFilter>().sharedMesh = surfaceMesh;
            MeshRenderer surface = root.GetComponent<MeshRenderer>();
            surface.sharedMaterial = connected[0].surfaceRenderer.sharedMaterial;
            root.GetComponent<MeshCollider>().sharedMesh = surfaceMesh;
            bool usesMandalaGround = !IsAnimalGroup && connected.TrueForAll(plot => plot.UsesPlowedGround);
            // O cercado unificado usa a mesma malha para seleção, com o visual do chão do mapa.
            if (usesMandalaGround || IsAnimalGroup) surface.enabled = false;
            else if (UsesDetailedPlowed)
            {
                // Os modelos individuais mantêm o relevo. Só as faixas entre slots
                // recebem uma superfície visível, animada junto da área unificada.
                surface.enabled = false;
                var bridges = regions.GetRange(connected.Count, regions.Count - connected.Count);
                if (bridges.Count > 0)
                {
                    bridgeMesh = BuildSurface(bridges, center, surfaceTop + 0.003f,
                        surfaceBottom, out _);
                    var bridge = new GameObject("Terra entre os terrenos",
                        typeof(MeshFilter), typeof(MeshRenderer));
                    bridge.transform.SetParent(root.transform, false);
                    bridge.GetComponent<MeshFilter>().sharedMesh = bridgeMesh;
                    bridge.GetComponent<MeshRenderer>().sharedMaterial = surface.sharedMaterial;
                }
            }
            root.GetComponent<PlotJoinedSurface>().Configure(connected, SetHovered);

            outlineCenter = center;
            outlineHeight = surfaceTop + 0.015f;
            outlineMesh = BuildOutline(border, outlineCenter, outlineHeight,
                IsAnimalGroup ? FenceOutlinePadding : 0f);
            Shader shader = Resources.Load<Shader>("PlotOutline");
            outlineMaterial = shader != null ? new Material(shader) : null;
            var outlineObject = new GameObject("Contorno da área", typeof(MeshFilter),
                typeof(MeshRenderer));
            outline = outlineObject.transform;
            outline.SetParent(root.transform, false);
            outlineObject.GetComponent<MeshFilter>().sharedMesh = outlineMesh;
            MeshRenderer outlineRenderer = outlineObject.GetComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineObject.SetActive(false);

            if (IsAnimalGroup)
            {
                // Preserva a animação original: as cercas antigas afundam
                // e o novo perímetro é reconstruído, peça por peça, de baixo para cima.
                foreach (Plot plot in members) plot.HideOriginalFenceParts();
                BuildFence(border, center);
            }
            if (!usesMandalaGround)
            {
                root.transform.localScale = new Vector3(0.975f, 1f, 0.975f);
                DOTween.Sequence().SetTarget(root.transform)
                    .Append(root.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutCubic))
                    .Append(root.transform.DOPunchScale(new Vector3(0.055f, 0f, 0.055f),
                        0.26f, 4));
                if (UsesDetailedPlowed)
                    foreach (Plot plot in members)
                        plot.plowedVisual.transform.DOPunchScale(
                            new Vector3(0.045f, 0f, 0.045f), 0.42f, 3);
            }
        }

        public void SetHighlight(bool visible)
        {
            if (!visible) ClearEligibleOutline();
            if (outline == null || outlineMaterial == null) return;
            outline.gameObject.SetActive(visible);
            PulseOutline();
        }

        private void ClearEligibleOutline()
        {
            if (eligibleOutline == null) return;
            if (outline != null)
            {
                outline.localPosition = Vector3.zero;
                outline.GetComponent<MeshFilter>().sharedMesh = outlineMesh;
            }
            UnityEngine.Object.Destroy(eligibleOutline);
            eligibleOutline = null;
        }

        public void SetHovered(bool value)
        {
            if (value && !hovered && Time.timeScale > 0f && outline != null && outline.gameObject.activeSelf)
                AudioManager.Play(SoundCue.PlotHover);
            hovered = value;
            PulseOutline();
        }

        private void PulseOutline()
        {
            if (outline == null) return;
            outline.DOKill();
            outline.localScale = Vector3.one;
            if (hovered && outline.gameObject.activeSelf)
                outline.DOScale(1.045f, 0.35f).SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
        }

        public void Dispose(bool animateFence = false)
        {
            foreach (Plot plot in members)
                if (plot != null)
                {
                    plot.joinedVisual = null;
                    if (!plot.IsFenced && plot.surfaceRenderer != null) plot.surfaceRenderer.enabled = true;
                }
            if (outline != null) outline.DOKill();
            if (root != null)
            {
                root.transform.DOKill();
                root.transform.localScale = Vector3.one;
            }
            if (animateFence && IsAnimalGroup && fencePieces.Count > 0 && root != null)
            {
                root.GetComponent<MeshRenderer>().enabled = false;
                root.GetComponent<MeshCollider>().enabled = false;
                if (outline != null) outline.gameObject.SetActive(false);
                float finish = 0f;
                for (int i = 0; i < fencePieces.Count; i++)
                {
                    Transform piece = fencePieces[i];
                    if (piece == null) continue;
                    piece.DOKill();
                    float delay = i * 0.012f;
                    finish = delay + 0.2f;
                    piece.DOLocalMoveY(piece.localPosition.y - 0.45f, 0.2f)
                        .SetDelay(delay).SetEase(Ease.InSine);
                }
                DOVirtual.DelayedCall(finish, DestroyAssets);
                return;
            }
            foreach (Transform piece in fencePieces) if (piece != null) piece.DOKill();
            DestroyAssets();
        }

        private void DestroyAssets()
        {
            ClearEligibleOutline();
            if (root != null) UnityEngine.Object.Destroy(root);
            if (surfaceMesh != null) UnityEngine.Object.Destroy(surfaceMesh);
            if (bridgeMesh != null) UnityEngine.Object.Destroy(bridgeMesh);
            if (outlineMesh != null) UnityEngine.Object.Destroy(outlineMesh);
            if (outlineMaterial != null) UnityEngine.Object.Destroy(outlineMaterial);
        }

        private static Plot Neighbor(List<Plot> plots, Plot origin, int x, int z)
        {
            if (origin == null) return null;
            foreach (Plot plot in plots)
            {
                if (plot == origin || !Adjacent(origin, plot)) continue;
                Vector3 offset = plot.transform.position - origin.transform.position;
                if ((x != 0 && Mathf.Sign(offset.x) == x && Mathf.Abs(offset.z) < 0.2f) ||
                    (z != 0 && Mathf.Sign(offset.z) == z && Mathf.Abs(offset.x) < 0.2f))
                    return plot;
            }
            return null;
        }

        private static List<Rect> BuildRegions(List<Plot> plots)
        {
            const float overlap = 0.01f;
            var regions = new List<Rect>();
            foreach (Plot plot in plots)
            {
                Bounds bounds = plot.surfaceBounds;
                regions.Add(new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
            }
            foreach (Plot plot in plots)
            {
                Plot east = Neighbor(plots, plot, 1, 0);
                Plot north = Neighbor(plots, plot, 0, 1);
                Bounds own = plot.surfaceBounds;
                if (east != null)
                {
                    Bounds next = east.surfaceBounds;
                    float z0 = Mathf.Max(own.min.z, next.min.z);
                    float z1 = Mathf.Min(own.max.z, next.max.z);
                    if (next.min.x > own.max.x && z1 > z0)
                        regions.Add(Rect.MinMaxRect(own.max.x - overlap, z0,
                            next.min.x + overlap, z1));
                }
                if (north != null)
                {
                    Bounds next = north.surfaceBounds;
                    float x0 = Mathf.Max(own.min.x, next.min.x);
                    float x1 = Mathf.Min(own.max.x, next.max.x);
                    if (next.min.z > own.max.z && x1 > x0)
                        regions.Add(Rect.MinMaxRect(x0, own.max.z - overlap,
                            x1, next.min.z + overlap));
                }
                if (east == null || north == null || Neighbor(plots, east, 0, 1) == null)
                    continue;
                Bounds right = east.surfaceBounds;
                Bounds above = north.surfaceBounds;
                if (right.min.x > own.max.x && above.min.z > own.max.z)
                    regions.Add(Rect.MinMaxRect(own.max.x - overlap,
                        own.max.z - overlap, right.min.x + overlap,
                        above.min.z + overlap));
            }
            return regions;
        }

        private static List<float> Coordinates(List<Rect> regions, bool xAxis)
        {
            var values = new List<float>();
            foreach (Rect region in regions)
            {
                values.Add(xAxis ? region.xMin : region.yMin);
                values.Add(xAxis ? region.xMax : region.yMax);
            }
            values.Sort();
            var unique = new List<float>();
            foreach (float value in values)
                if (unique.Count == 0 || value - unique[unique.Count - 1] > 0.001f)
                    unique.Add(value);
            return unique;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        private static bool[,] FillRegionGrid(List<Rect> regions, out List<float> xs, out List<float> zs)
        {
            xs = Coordinates(regions, true);
            zs = Coordinates(regions, false);
            var filled = new bool[xs.Count - 1, zs.Count - 1];
            for (int x = 0; x < xs.Count - 1; x++)
                for (int z = 0; z < zs.Count - 1; z++)
                {
                    Vector2 point = new Vector2((xs[x] + xs[x + 1]) * 0.5f,
                        (zs[z] + zs[z + 1]) * 0.5f);
                    foreach (Rect region in regions)
                        if (region.Contains(point)) { filled[x, z] = true; break; }
                }
            return filled;
        }

        private static List<BorderEdge> CollectBorder(List<float> xs, List<float> zs, bool[,] filled)
        {
            var border = new List<BorderEdge>();
            for (int x = 0; x < xs.Count - 1; x++)
                for (int z = 0; z < zs.Count - 1; z++)
                {
                    if (!filled[x, z]) continue;
                    if (z == 0 || !filled[x, z - 1])
                        border.Add(new BorderEdge(true, zs[z], xs[x], xs[x + 1], -1));
                    if (z == zs.Count - 2 || !filled[x, z + 1])
                        border.Add(new BorderEdge(true, zs[z + 1], xs[x], xs[x + 1], 1));
                    if (x == 0 || !filled[x - 1, z])
                        border.Add(new BorderEdge(false, xs[x], zs[z], zs[z + 1], -1));
                    if (x == xs.Count - 2 || !filled[x + 1, z])
                        border.Add(new BorderEdge(false, xs[x + 1], zs[z], zs[z + 1], 1));
                }
            return border;
        }

        private static void BorderEndpoints(BorderEdge edge, out Vector2 start, out Vector2 end)
        {
            // Sentido: a área ocupada fica sempre à esquerda do segmento.
            Vector2 a = edge.Horizontal ? new Vector2(edge.Start, edge.Line) :
                new Vector2(edge.Line, edge.Start);
            Vector2 b = edge.Horizontal ? new Vector2(edge.End, edge.Line) :
                new Vector2(edge.Line, edge.End);
            bool reverse = edge.Horizontal ? edge.Normal > 0 : edge.Normal < 0;
            start = reverse ? b : a;
            end = reverse ? a : b;
        }

        private static Mesh BuildSurface(List<Rect> regions, Vector2 center, float top,
            float bottom, out List<BorderEdge> border)
        {
            bool[,] filled = FillRegionGrid(regions, out List<float> xs, out List<float> zs);
            border = CollectBorder(xs, zs, filled);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int x = 0; x < xs.Count - 1; x++)
                for (int z = 0; z < zs.Count - 1; z++)
                {
                    if (!filled[x, z]) continue;
                    float x0 = xs[x] - center.x, x1 = xs[x + 1] - center.x;
                    float z0 = zs[z] - center.y, z1 = zs[z + 1] - center.y;
                    AddQuad(vertices, triangles, new Vector3(x0, top, z0),
                        new Vector3(x0, top, z1), new Vector3(x1, top, z1),
                        new Vector3(x1, top, z0));
                }
            foreach (BorderEdge edge in border)
            {
                BorderEndpoints(edge, out Vector2 start, out Vector2 end);
                start -= center;
                end -= center;
                AddQuad(vertices, triangles, new Vector3(start.x, bottom, start.y),
                    new Vector3(start.x, top, start.y), new Vector3(end.x, top, end.y),
                    new Vector3(end.x, bottom, end.y));
            }
            var mesh = new Mesh { name = "Solo unificado" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Recria o contorno como anéis fechados; em cada quina o mesmo vértice
        // é compartilhado pelas duas faixas. Evita lacunas em L, U e outras formas.
        private static Mesh BuildOutline(List<BorderEdge> border, Vector2 center, float y, float padding)
        {
            const float halfWidth = 0.035f;
            var starts = new List<Vector2>(border.Count);
            var ends = new List<Vector2>(border.Count);
            foreach (BorderEdge edge in border)
            {
                BorderEndpoints(edge, out Vector2 start, out Vector2 end);
                starts.Add(start);
                ends.Add(end);
            }
            var used = new bool[border.Count];
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int first = 0; first < border.Count; first++)
            {
                if (used[first]) continue;
                var ring = new List<Vector2>();
                int current = first;
                bool closed = false;
                for (int step = 0; step <= border.Count; step++)
                {
                    if (used[current]) break;
                    used[current] = true;
                    ring.Add(starts[current]);
                    Vector2 tip = ends[current];
                    if ((tip - starts[first]).sqrMagnitude < 0.000001f)
                    {
                        closed = true;
                        break;
                    }
                    int next = -1;
                    for (int k = 0; k < starts.Count; k++)
                        if (!used[k] && (starts[k] - tip).sqrMagnitude < 0.000001f)
                        {
                            next = k;
                            break;
                        }
                    if (next < 0) break;
                    current = next;
                }
                if (!closed || ring.Count < 4) continue;
                int count = ring.Count;
                // Para cada canto calculamos a interseção das duas retas
                // paralelas ao perímetro. Assim o padding também fecha nas reentrâncias.
                var inner = new Vector2[count];
                var outer = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    Vector2 here = ring[i];
                    Vector2 incoming = (here - ring[(i + count - 1) % count]).normalized;
                    Vector2 outgoing = (ring[(i + 1) % count] - here).normalized;
                    Vector2 normalBefore = new Vector2(incoming.y, -incoming.x);
                    Vector2 normalAfter = new Vector2(outgoing.y, -outgoing.x);
                    float denominator = 1f + Vector2.Dot(normalBefore, normalAfter);
                    Vector2 miter = denominator > 0.01f ?
                        (normalBefore + normalAfter) / denominator : normalAfter;
                    inner[i] = here + miter * (padding - halfWidth) - center;
                    outer[i] = here + miter * (padding + halfWidth) - center;
                }
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    AddQuad(vertices, triangles,
                        new Vector3(outer[i].x, y, outer[i].y),
                        new Vector3(inner[i].x, y, inner[i].y),
                        new Vector3(inner[next].x, y, inner[next].y),
                        new Vector3(outer[next].x, y, outer[next].y));
                }
            }
            var mesh = new Mesh { name = "Contorno unificado" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // A geometria do perímetro continua vindo dos mesmos terrenos. Apenas
        // a distribuição das peças muda: reutiliza a cerquinha original, em tamanho
        // constante, e completa os intervalos entre os slots sem deformar o FBX.
        private void BuildFence(List<BorderEdge> border, Vector2 center)
        {
            Plot source = members[0];
            if (source.fenceParts == null || source.fenceParts.Length < 12 ||
                source.fenceBasePositions == null || source.fenceBaseScales == null)
                return;

            // Junta arestas colineares: cada lateral passa a ser uma sequência
            // contínua de módulos, inclusive através do espaço entre slots.
            border.Sort((a, b) =>
            {
                int orientation = a.Horizontal.CompareTo(b.Horizontal);
                if (orientation != 0) return orientation;
                int line = a.Line.CompareTo(b.Line);
                if (line != 0) return line;
                int normal = a.Normal.CompareTo(b.Normal);
                return normal != 0 ? normal : a.Start.CompareTo(b.Start);
            });
            var merged = new List<BorderEdge>();
            foreach (BorderEdge edge in border)
            {
                int last = merged.Count - 1;
                if (last >= 0 && merged[last].Horizontal == edge.Horizontal &&
                    Mathf.Abs(merged[last].Line - edge.Line) < 0.001f &&
                    merged[last].Normal == edge.Normal &&
                    edge.Start <= merged[last].End + 0.001f)
                {
                    BorderEdge extended = merged[last];
                    extended.End = Mathf.Max(extended.End, edge.End);
                    merged[last] = extended;
                }
                else merged.Add(edge);
            }

            float startDelay = source.fencePieceDuration +
                (source.fenceParts.Length - 1) * source.fencePieceDelay * 0.5f + 0.05f;
            int pieceIndex = 0;
            foreach (BorderEdge edge in merged)
            {
                // Escolhe o mesmo módulo que já existe na lateral correspondente.
                // Lados opostos usam suas próprias versões, preservando a arte.
                int templateIndex = edge.Horizontal ?
                    (edge.Normal > 0 ? 1 : 7) : (edge.Normal > 0 ? 4 : 10);
                GameObject template = source.fenceParts[templateIndex];
                if (template == null) continue;
                Transform originalPiece = template.transform;
                Transform originalParent = originalPiece.parent;
                if (originalParent == null) continue;
                Vector3 originalScale = originalPiece.lossyScale;
                float moduleLength = edge.Horizontal ?
                    Mathf.Abs(originalScale.x) : Mathf.Abs(originalScale.z);
                if (moduleLength < 0.01f) continue;

                float span = edge.End - edge.Start;
                if (span <= 0f) continue;
                int count = Mathf.Max(1, Mathf.RoundToInt(span / moduleLength));
                // Nunca deixa aberturas grandes entre módulos. Se não couberem
                // exatamente, coloca mais um e sobrepõe ligeiramente, sem esticar.
                while (count > 1 && (span - moduleLength) / (count - 1) > moduleLength + 0.015f)
                    count++;
                float spacing = count > 1 ? (span - moduleLength) / (count - 1) : 0f;

                Vector3 originalCenter = originalParent.TransformPoint(
                    source.fenceBasePositions[templateIndex]);
                float surfaceLine = edge.Horizontal ?
                    (edge.Normal > 0 ? source.surfaceBounds.max.z : source.surfaceBounds.min.z) :
                    (edge.Normal > 0 ? source.surfaceBounds.max.x : source.surfaceBounds.min.x);
                float offset = (edge.Horizontal ? originalCenter.z : originalCenter.x) - surfaceLine;
                float boundary = edge.Line + offset;
                Vector3 rootScale = root.transform.lossyScale;

                for (int i = 0; i < count; i++)
                {
                    float middle = count == 1 ? (edge.Start + edge.End) * 0.5f :
                        edge.Start + moduleLength * 0.5f + i * spacing;
                    Vector3 worldPosition = edge.Horizontal ?
                        new Vector3(middle, originalCenter.y, boundary) :
                        new Vector3(boundary, originalCenter.y, middle);

                    // Clona o objeto real da cerca, com seus filhos, mesh, material e
                    // proporções originais. Nunca usa cubos substitutos ou GS2VisualFit.
                    GameObject part = UnityEngine.Object.Instantiate(template, root.transform, false);
                    part.name = "Cerquinha unificada " + ++pieceIndex;
                    Transform piece = part.transform;
                    piece.rotation = originalPiece.rotation;
                    piece.localScale = new Vector3(
                        originalScale.x / Mathf.Max(0.001f, Mathf.Abs(rootScale.x)),
                        originalScale.y / Mathf.Max(0.001f, Mathf.Abs(rootScale.y)),
                        originalScale.z / Mathf.Max(0.001f, Mathf.Abs(rootScale.z)));
                    Vector3 position = root.transform.InverseTransformPoint(worldPosition);
                    piece.localPosition = position + Vector3.down * 0.45f;
                    // A superfície unificada já recebe o clique; cópias visuais
                    // não devem interceptar o apontamento do jogador.
                    foreach (Collider collider in part.GetComponentsInChildren<Collider>(true))
                        UnityEngine.Object.Destroy(collider);
                    part.SetActive(true);
                    fencePieces.Add(piece);
                    piece.DOLocalMove(position, source.fencePieceDuration + 0.1f)
                        .SetDelay(startDelay + pieceIndex * source.fencePieceDelay * 0.45f)
                        .SetEase(Ease.OutBack);
                }
            }
        }


    }
}
