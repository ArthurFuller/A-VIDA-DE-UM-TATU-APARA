using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HouseSite : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [System.Serializable]
    private sealed class WorkerConstructionRoute
    {
        public int houseNumber;
        public Transform[] waypoints;
    }

    [SerializeField] private ActionResolver actions;
    [SerializeField, Tooltip("Contorno discreto do lote enquanto ele está livre.")]
    private GameObject idleOutline;
    [Tooltip("Visual da Casa 2 deste lote. Fica visível no editor para ajustar o Transform e oculto até a construção durante o jogo.")]
    [SerializeField] private Transform houseVisualPreview;
    [Tooltip("Ponto editável da porta deste lote, usado para a saída do novo morador.")]
    [SerializeField] private Transform doorwayMarker;
    [Tooltip("Ponto externo logo depois da porta; ajuste para a direção real da entrada.")]
    [SerializeField] private Transform outsideMarker;
    [Tooltip("Origem e destino habitual do morador desta casa.")]
    [SerializeField] private Transform homeMarker;
    [Tooltip("Pontos do trajeto até a obra, na ordem da ida. A volta usa a ordem inversa.")]
    [SerializeField] private Transform[] constructionWaypoints;
    [Tooltip("Trecho exclusivo de cada casa de origem, antes dos pontos compartilhados da construção.")]
    [SerializeField, HideInInspector] private WorkerConstructionRoute[] workerConstructionRoutes;
    [Tooltip("Local onde o construtor para para trabalhar. Ajustável na cena.")]
    [SerializeField] private Transform constructionWorkPoint;
    [SerializeField, Min(0f), Tooltip("Velocidade do construtor neste trajeto. Zero usa a velocidade geral dos funcionários.")]
    private float constructionTravelSpeed;
    [SerializeField] private WorkerAssignment workerTemplate;
    [SerializeField, Min(3)] private int houseNumber = 3;
    [SerializeField] private Vector3 workerOffset = new Vector3(0.9f, 0f, -0.95f);
    [SerializeField, Min(0.1f)] private float builderClearance = 0.8f;

    public bool IsBuilt { get; private set; }
    private int builtDay;
    private bool residentCreated;
    private bool pointerOver;
    private Transform selectionOutline;
    private Mesh selectionMesh;
    private Material selectionMaterial;
    private static readonly List<HouseSite> allSites = new List<HouseSite>();

    public Vector3 DoorwayPosition => doorwayMarker != null ? doorwayMarker.position : transform.position;
    public int HouseNumber => houseNumber;
    public Transform HomeMarker => homeMarker;
    public Vector3[] ConstructionWaypoints
    {
        get
        {
            if (constructionWaypoints == null) return System.Array.Empty<Vector3>();
            var points = new List<Vector3>(constructionWaypoints.Length);
            foreach (Transform waypoint in constructionWaypoints)
                if (waypoint != null) points.Add(waypoint.position);
            return points.ToArray();
        }
    }
    public Vector3 ConstructionFacingPosition => houseVisualPreview != null ? houseVisualPreview.position : transform.position;
    public float ConstructionTravelSpeed => constructionTravelSpeed;

    public Vector3[] GetConstructionWaypoints(int originHouse)
    {
        var points = new List<Vector3>();
        if (workerConstructionRoutes != null)
            foreach (WorkerConstructionRoute route in workerConstructionRoutes)
            {
                if (route == null || route.houseNumber != originHouse || route.waypoints == null) continue;
                foreach (Transform waypoint in route.waypoints)
                    if (waypoint != null) points.Add(waypoint.position);
                break;
            }
        points.AddRange(ConstructionWaypoints);
        return points.ToArray();
    }

    public Vector3 ConstructionPosition
    {
        get
        {
            if (constructionWorkPoint != null) return constructionWorkPoint.position;
            Collider lot = GetComponent<Collider>();
            float distance = (lot != null ? lot.bounds.extents.x : 0.8f) + builderClearance;
            return transform.position + Vector3.right * distance;
        }
    }

    private void Awake()
    {
        if (Application.isPlaying && houseVisualPreview != null)
            houseVisualPreview.gameObject.SetActive(false);
        if (idleOutline != null)
        {
            Renderer surface = GetComponent<Renderer>();
            if (surface != null) surface.enabled = false;
            RefreshIdleOutline();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (actions != null) actions.TryBuildHouse(this);
    }

    private void OnEnable() => allSites.Add(this);

    private void OnDisable()
    {
        allSites.Remove(this);
        SetHighlight(false);
    }

    private void OnDestroy()
    {
        if (selectionMesh != null) Destroy(selectionMesh);
        if (selectionMaterial != null) Destroy(selectionMaterial);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!pointerOver && Time.timeScale > 0f && selectionOutline != null && selectionOutline.gameObject.activeSelf)
            AudioManager.Play(SoundCue.PlotHover);
        pointerOver = true;
        PulseOutline();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        PulseOutline();
    }

    public static void ShowAvailable()
    {
        foreach (HouseSite site in allSites)
            if (site != null) site.SetHighlight(!site.IsBuilt);
    }

    public static bool HasAvailable()
    {
        foreach (HouseSite site in allSites)
            if (site != null && !site.IsBuilt) return true;
        return false;
    }

    public static void ClearHighlights()
    {
        foreach (HouseSite site in allSites)
            if (site != null) site.SetHighlight(false);
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
        bool visible = !IsBuilt && !highlighted;
        if (idleOutline.activeSelf != visible) idleOutline.SetActive(visible);
    }

    private void PulseOutline()
    {
        if (selectionOutline == null) return;
        selectionOutline.DOKill();
        selectionOutline.localScale = Vector3.one;
        if (selectionOutline.gameObject.activeSelf && pointerOver)
            selectionOutline.DOScale(1.07f, 0.35f).SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
    }

    private void CreateOutline()
    {
        MeshFilter surface = GetComponent<MeshFilter>();
        Shader shader = Resources.Load<Shader>("PlotOutline");
        if (surface == null || surface.sharedMesh == null || shader == null) return;
        Bounds bounds = surface.sharedMesh.bounds;
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
        var outline = new GameObject("Contorno de construção", typeof(MeshFilter), typeof(MeshRenderer));
        selectionOutline = outline.transform;
        selectionOutline.SetParent(transform, false);
        outline.GetComponent<MeshFilter>().sharedMesh = selectionMesh;
        MeshRenderer renderer = outline.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = selectionMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
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

    internal void PlayConstructionSmoke(float duration)
    {
        Collider lot = GetComponent<Collider>();
        Bounds bounds = lot != null ? lot.bounds : new Bounds(transform.position, new Vector3(2f, 0.1f, 2f));
        ConstructionSmoke.Play(transform, bounds, duration);
    }

    public bool Build(int day)
    {
        if (IsBuilt || houseVisualPreview == null || doorwayMarker == null ||
            workerTemplate == null) return false;
        IsBuilt = true;
        SetHighlight(false);
        builtDay = day;
        Vector3 finalScale = houseVisualPreview.localScale;
        houseVisualPreview.localScale = finalScale * 0.08f;
        houseVisualPreview.gameObject.SetActive(true);
        DOTween.Sequence().SetLink(houseVisualPreview.gameObject)
            .Append(houseVisualPreview.DOScale(finalScale, 0.45f).SetEase(Ease.OutCubic))
            .Append(houseVisualPreview.DOPunchScale(finalScale * 0.065f, 0.25f, 3, 0.55f))
            .OnComplete(() => houseVisualPreview.localScale = finalScale);
        Renderer surface = GetComponent<Renderer>();
        if (surface != null) surface.enabled = false;
        Collider target = GetComponent<Collider>();
        if (target != null) target.enabled = false;
        return true;
    }

    public WorkerAssignment CreateResident(int day)
    {
        if (!IsBuilt || residentCreated || day <= builtDay || workerTemplate == null) return null;
        residentCreated = true;
        WorkerAssignment worker = Instantiate(workerTemplate, workerTemplate.transform.parent);
        worker.name = "Trabalhador ilustrativo " + houseNumber;
        worker.transform.position = homeMarker != null ? homeMarker.position : transform.position + workerOffset;
        worker.ConfigureNewWorker("Trabalhador " + houseNumber);
        worker.SetHouseRoute(homeMarker, outsideMarker, doorwayMarker);
        worker.SetOriginHouseNumber(houseNumber);
        worker.RestoreHomeFacing();
        return worker;
    }
}
