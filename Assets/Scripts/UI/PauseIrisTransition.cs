using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Persiste somente durante a troca para o menu; nao depende de timeScale.
public sealed class PauseIrisTransition : MonoBehaviour
{
    public static bool ReturningToMenu { get; private set; }
    private Material irisMaterial;
    private Tween irisTween;

    public static void ToMenu(Shader shader)
    {
        if (shader == null || ReturningToMenu) return;
        ReturningToMenu = true;
        var root = new GameObject("Transicao radial para menu", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(PauseIrisTransition));
        DontDestroyOnLoad(root);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(900f, 506.25f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var surface = new GameObject("Preto de fora para dentro", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)surface.transform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = surface.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        var transition = root.GetComponent<PauseIrisTransition>();
        transition.irisMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        image.material = transition.irisMaterial;
        FullscreenBackdrop.Cover(image);
        transition.SetRadius(1.1f);
        transition.AnimateRadius(-0.05f, 0.44f, () => SceneManager.LoadScene("MainMenu"));
        SceneManager.sceneLoaded += transition.SceneOpened;
    }

    private void SceneOpened(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu") return;
        SceneManager.sceneLoaded -= SceneOpened;
        // O Main Menu usa seus tweens em tempo nao escalado.
        Time.timeScale = 0f;
        // Permite que a propria interface do MainMenu termine de aparecer antes da abertura radial.
        irisTween = DOVirtual.DelayedCall(0.36f,
            () => AnimateRadius(1.1f, 0.44f, () => Destroy(gameObject)), true)
            .SetUpdate(true).SetTarget(this);
    }

    private void SetRadius(float value)
    {
        if (irisMaterial != null) irisMaterial.SetFloat("_Radius", value);
    }

    private void AnimateRadius(float end, float duration, System.Action completed)
    {
        float begin = irisMaterial.GetFloat("_Radius");
        irisTween?.Kill();
        irisTween = DOTween.To(() => begin, v => { begin = v; SetRadius(v); }, end, duration)
            .SetEase(Ease.InOutSine).SetUpdate(true).SetTarget(this).OnComplete(() => completed?.Invoke());
    }

    private void OnDestroy()
    {
        irisTween?.Kill();
        SceneManager.sceneLoaded -= SceneOpened;
        if (irisMaterial != null) Destroy(irisMaterial);
        ReturningToMenu = false;
    }
}
