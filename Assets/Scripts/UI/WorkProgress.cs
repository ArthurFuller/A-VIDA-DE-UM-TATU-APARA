using UnityEngine;

public sealed class WorkProgress : MonoBehaviour
{
    [SerializeField] private GameObject barRoot;
    [SerializeField] private Transform fill;
    [SerializeField] private ParticleSystem workDust;
    [SerializeField, Min(0.01f)] private float fillWidth = 0.84f;
    [SerializeField, Tooltip("Deslocamento da poeira para o lado da mão, para cima e em direção à câmera.")]
    private Vector3 workDustOffset = new Vector3(0.24f, 0.52f, 0.32f);

    private SpriteRenderer workerSprite;
    private Camera gameplayCamera;

    private void Awake()
    {
        if (workDust == null)
            workDust = transform.Find("Terra - poeira de trabalho")?.GetComponent<ParticleSystem>();
        if (workDust == null) return;
        workerSprite = transform.Find("Visual 2D")?.GetComponent<SpriteRenderer>();
        workDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = workDust.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = Mathf.Max(12, main.maxParticles);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.36f, 0.60f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.19f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.67f, 0.48f, 0.30f, 0.78f), new Color(0.90f, 0.73f, 0.49f, 0.93f));

        var shape = workDust.shape;
        shape.enabled = true;
        shape.position = Vector3.zero;
        shape.scale = new Vector3(0.17f, 0.12f, 0.14f);
        var emission = workDust.emission;
        emission.enabled = true;
        emission.rateOverTime = 11f;
        var size = workDust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.72f, 1f, 1.18f));

        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.42f, 0f), new GradientAlphaKey(1f, 0.14f),
                new GradientAlphaKey(0.64f, 0.52f), new GradientAlphaKey(0f, 1f) });
        var color = workDust.colorOverLifetime;
        color.enabled = true;
        color.color = fade;

        if (workDust.TryGetComponent(out ParticleSystemRenderer renderer))
        {
            var dustMaterial = Resources.Load<Material>("GS2_Poeira");
            if (dustMaterial != null) renderer.sharedMaterial = dustMaterial;
            renderer.enabled = true;
            if (workerSprite != null)
            {
                renderer.sortingLayerID = workerSprite.sortingLayerID;
                renderer.sortingOrder = workerSprite.sortingOrder + 1;
            }
        }
    }

    public void Show(bool soilWork = false)
    {
        if (barRoot != null) barRoot.SetActive(true);
        if (soilWork) StartWorkDust();
        else StopWorkDust();
        SetProgress(0);
    }

    private void StartWorkDust()
    {
        if (workDust == null || workDust.isEmitting) return;
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        Vector3 right = gameplayCamera != null ? gameplayCamera.transform.right : transform.right;
        Vector3 toCamera = gameplayCamera != null ? -gameplayCamera.transform.forward : -transform.forward;
        float facing = workerSprite != null && workerSprite.flipX ? 1f : -1f;
        if (workDust.transform != transform)
            workDust.transform.position = transform.position + Vector3.up * workDustOffset.y +
                right * (workDustOffset.x * facing) +
                Vector3.ProjectOnPlane(toCamera, Vector3.up).normalized * workDustOffset.z;
        if (!workDust.gameObject.activeSelf) workDust.gameObject.SetActive(true);
        workDust.Play(false);
        workDust.Emit(4);
    }

    private void StopWorkDust()
    {
        if (workDust != null && workDust.isEmitting)
            workDust.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    public void SetProgress(float progress)
    {
        if (fill == null)
            return;
        float width = fillWidth * Mathf.Clamp01(progress);
        Vector3 size = fill.localScale;
        size.x = Mathf.Max(0.001f, width);
        fill.localScale = size;
        Vector3 position = fill.localPosition;
        position.x = -fillWidth * 0.5f + width * 0.5f;
        fill.localPosition = position;
    }

    public void Hide()
    {
        if (barRoot != null) barRoot.SetActive(false);
        StopWorkDust();
    }

    private void OnDisable()
    {
        if (workDust != null)
            workDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
