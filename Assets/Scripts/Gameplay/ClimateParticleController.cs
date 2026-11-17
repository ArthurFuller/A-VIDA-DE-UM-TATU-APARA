using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// VFX apenas: o evento continua sendo a fonte de verdade da gameplay.
public sealed class ClimateParticleController : MonoBehaviour
{
    [System.Serializable]
    private sealed class TumbleweedRoute
    {
        public Vector3 start;
        public Vector3 end;
    }

    [SerializeField] private DayEventController events;
    [SerializeField] private ParticleSystem rain;
    [SerializeField] private ParticleSystem droughtDust;
    [SerializeField] private CanvasGroup droughtTint;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.65f;
    [Header("Bola do deserto")]
    [SerializeField] private GameObject tumbleweedPrefab;
    [SerializeField, Min(0.1f), Tooltip("Diâmetro da bola em unidades da cena.")] private float tumbleweedDiameter = 0.85f;
    [SerializeField, Min(0.1f), Tooltip("Unidades da cena percorridas por segundo.")] private float tumbleweedMoveSpeed = 3.5f;
    [SerializeField, Range(0.1f, 1f)] private float tumbleweedSpeedMultiplier = 0.58f;
    [SerializeField, Min(1f)] private float tumbleweedIntervalMin = 18f;
    [SerializeField, Min(1f)] private float tumbleweedIntervalMax = 32f;
    [SerializeField, Tooltip("Cada caminho tem um ponto inicial e um final em coordenadas da cena.")] private TumbleweedRoute[] tumbleweedRoutes;

    private Tween tintFade;
    private bool raining;
    private bool drought;
    private UniversalAdditionalCameraData cameraData;
    private CameraOverrideOption previousColorOption;
    private GameObject tumbleweed;
    private Transform tumbleweedShadow;
    private Sprite tumbleweedShadowSprite;
    private Sequence tumbleweedTravel;
    private float nextTumbleweedTime;

    private void Awake()
    {
#if UNITY_EDITOR
        Transform preview = transform.Find("Prévia - Bola do deserto");
        if (preview != null) preview.gameObject.SetActive(false);
#endif
        if (gameplayCamera != null)
            gameplayCamera.TryGetComponent(out cameraData);
        if (cameraData != null)
            previousColorOption = cameraData.requiresColorOption;
    }

    private void Update()
    {
        if (events == null) return;
        if (events.IsRaining != raining || events.IsDrought != drought)
            Refresh();
        if (raining && rain != null && !rain.isPlaying)
            rain.Play(true);
        if (drought && droughtDust != null && !droughtDust.isPlaying)
            droughtDust.Play(true);
        if (drought && tumbleweed == null && Time.time >= nextTumbleweedTime)
        {
            SpawnTumbleweed();
            ScheduleTumbleweed();
        }
    }

    private void Start() => AudioManager.SetRain(raining);

    private void OnEnable()
    {
        rain?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        droughtDust?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (droughtTint != null) droughtTint.alpha = 0f;
        RemoveTumbleweed();
        raining = drought = false;
        if (events != null) events.WeatherChanged += Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        if (events != null) events.WeatherChanged -= Refresh;
        AudioManager.SetRain(false);
        tintFade?.Kill();
        rain?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        droughtDust?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (droughtTint != null) droughtTint.alpha = 0f;
        RemoveTumbleweed();
        if (cameraData != null) cameraData.requiresColorOption = previousColorOption;
    }

    private void OnDestroy()
    {
        if (tumbleweedShadowSprite != null) Destroy(tumbleweedShadowSprite);
    }

    private void Refresh()
    {
        bool nextRain = events != null && events.IsRaining;
        bool nextDrought = events != null && events.IsDrought;
        AudioManager.SetRain(nextRain);
        if (nextRain != raining)
        {
            raining = nextRain;
            ApplyEmission(rain, nextRain);
        }
        if (nextDrought != drought)
        {
            drought = nextDrought;
            if (cameraData != null)
                cameraData.requiresColorOption = nextDrought
                    ? CameraOverrideOption.On : previousColorOption;
            ApplyEmission(droughtDust, nextDrought);
            if (nextDrought)
            {
                ScheduleTumbleweed();
            }
            else RemoveTumbleweed();
            if (droughtTint != null)
            {
                tintFade?.Kill();
                tintFade = droughtTint.DOFade(nextDrought ? 1f : 0f, transitionSeconds)
                    .SetEase(Ease.InOutSine).SetUpdate(true);
            }
        }
    }

    private static void ApplyEmission(ParticleSystem system, bool active)
    {
        if (system == null) return;
        if (active) system.Play(true);
        else system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void ScheduleTumbleweed()
    {
        nextTumbleweedTime = Time.time + Random.Range(tumbleweedIntervalMin,
            Mathf.Max(tumbleweedIntervalMin, tumbleweedIntervalMax));
    }

    private void SpawnTumbleweed()
    {
        if (tumbleweedPrefab == null ||
            tumbleweedRoutes == null || tumbleweedRoutes.Length == 0) return;
        TumbleweedRoute route = tumbleweedRoutes[Random.Range(0, tumbleweedRoutes.Length)];
        if (route == null) return;
        Vector3 start = route.start;
        Vector3 end = route.end;
        Vector3 travel = end - start;
        Vector3 horizontalTravel = Vector3.ProjectOnPlane(travel, Vector3.up);
        if (horizontalTravel.sqrMagnitude < 0.01f) return;

        tumbleweed = Instantiate(tumbleweedPrefab, start, Quaternion.identity);
        tumbleweed.name = "Bola do deserto - seca";
        Renderer[] renderers = tumbleweed.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { RemoveTumbleweed(); return; }
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        tumbleweed.transform.localScale *= tumbleweedDiameter /
            Mathf.Max(0.01f, bounds.size.x, bounds.size.y, bounds.size.z);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        foreach (Collider collider in tumbleweed.GetComponentsInChildren<Collider>())
            collider.enabled = false;

        Transform ball = tumbleweed.transform;
        Vector3 pivotToCenter = bounds.center - ball.position;
        Vector3 centerStart = bounds.center + Vector3.up * (start.y - bounds.min.y);
        Quaternion initialRotation = ball.rotation;
        ball.position = centerStart - pivotToCenter;
        Vector3 rollAxis = Vector3.Cross(Vector3.up, horizontalTravel).normalized;
        float radius = Mathf.Max(0.05f, bounds.extents.y);
        float totalRoll = horizontalTravel.magnitude / radius * Mathf.Rad2Deg;
        float travelSeconds = travel.magnitude / Mathf.Max(0.1f, tumbleweedMoveSpeed * tumbleweedSpeedMultiplier);
        Vector3 shadowOffset = Vector3.ProjectOnPlane(bounds.center - start, Vector3.up) + Vector3.up * 0.035f;
        tumbleweedShadow = CreateTumbleweedShadow(route.start + shadowOffset);
        tumbleweedTravel = DOTween.Sequence().SetTarget(this)
            .Append(DOVirtual.Float(0f, 1f, travelSeconds, progress =>
            {
                Quaternion roll = Quaternion.AngleAxis(totalRoll * progress, rollAxis);
                Vector3 center = centerStart + travel * progress;
                ball.SetPositionAndRotation(center - roll * pivotToCenter, roll * initialRotation);
            }).SetEase(Ease.Linear));
        if (tumbleweedShadow != null)
            tumbleweedTravel.Join(tumbleweedShadow.DOMove(route.end + shadowOffset, travelSeconds)
                .SetEase(Ease.Linear));
        tumbleweedTravel.OnComplete(RemoveTumbleweed);
    }

    private Transform CreateTumbleweedShadow(Vector3 position)
    {
        Material material = Resources.Load<Material>("GS2_SombraBolaDeserto");
        if (material == null) return null;
        if (tumbleweedShadowSprite == null)
        {
            Texture2D white = Texture2D.whiteTexture;
            tumbleweedShadowSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height),
                new Vector2(0.5f, 0.5f), white.width, 0, SpriteMeshType.FullRect);
        }
        var shadow = new GameObject("Sombra da bola do deserto", typeof(SpriteRenderer));
        shadow.layer = tumbleweed.layer;
        Transform shadowTransform = shadow.transform;
        shadowTransform.SetParent(transform, false);
        shadowTransform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
        shadowTransform.localScale = new Vector3(1.22f, 0.88f, 1f) * tumbleweedDiameter;
        SpriteRenderer renderer = shadow.GetComponent<SpriteRenderer>();
        renderer.sharedMaterial = material;
        renderer.sprite = tumbleweedShadowSprite;
        return shadowTransform;
    }

    private void RemoveTumbleweed()
    {
        tumbleweedTravel?.Kill();
        tumbleweedTravel = null;
        if (tumbleweed != null) Destroy(tumbleweed);
        tumbleweed = null;
        if (tumbleweedShadow != null) Destroy(tumbleweedShadow.gameObject);
        tumbleweedShadow = null;
    }
}
