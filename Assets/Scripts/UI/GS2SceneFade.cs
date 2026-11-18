using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Canvas da cena inicial que cobre a troca de cena e clareia a gameplay depois do carregamento.
public sealed class GS2SceneFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 0.42f;

    [Header("Transições")]
    [SerializeField, Min(0.1f)] private float nicknameDarkenSeconds = 0.68f;
    [SerializeField, Min(0.1f)] private float nicknameRevealSeconds = 0.85f;
    [SerializeField, Min(0.1f)] private float loadingExitSeconds = 0.62f;
    [SerializeField, Min(0.1f)] private float gameplayRevealSeconds = 0.95f;
    [SerializeField, Range(0f, 1f)] private float firstEventAtRevealProgress = 0.60f;

    private Tween fade;
    private Tween musicFade;
    private static GS2SceneFade gameplayTransition;
    internal static bool IsEnteringGameplay => gameplayTransition != null;

    private CanvasGroup loadingPresentation;
    private ThreadPriority priorLoadingPriority;
    private bool adjustedLoadingPriority;
    private const float MinimumLoadingSeconds = 3f;
    private Text loadingStatus;
    private AsyncOperation gameplayLoad;
    private string destination;
    private bool loading;

    private void Awake()
    {
        if (blackOverlay == null) blackOverlay = GetComponent<CanvasGroup>();
        if (blackOverlay == null) return;
        // Na volta radial do pause, a transicao persistente ja cobre a tela.
        if (PauseIrisTransition.ReturningToMenu)
        {
            blackOverlay.alpha = 0f;
            blackOverlay.interactable = blackOverlay.blocksRaycasts = false;
            return;
        }
        blackOverlay.alpha = 1f;
        blackOverlay.interactable = false;
        blackOverlay.blocksRaycasts = true;
        fade = blackOverlay.DOFade(0f, fadeSeconds).SetUpdate(true)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => blackOverlay.blocksRaycasts = false);
    }

    internal void ReleaseInitialMenuOverlay()
    {
        if (loading || blackOverlay == null) return;
        fade?.Kill();
        fade = null;
        blackOverlay.alpha = 0f;
        blackOverlay.blocksRaycasts = false;
    }

    public void TransitionFromNickname(System.Action showMenu, System.Action finished = null)
    {
        if (blackOverlay == null || loading)
        {
            showMenu();
            finished?.Invoke();
            return;
        }

        fade?.Kill();
        blackOverlay.blocksRaycasts = true;
        fade = DOTween.Sequence().SetUpdate(true)
            .Append(blackOverlay.DOFade(1f, nicknameDarkenSeconds).SetEase(Ease.InOutSine))
            .AppendCallback(() => showMenu())
            .AppendInterval(1f)
            .Append(blackOverlay.DOFade(0f, nicknameRevealSeconds).SetEase(Ease.InOutSine))
            .OnComplete(() =>
            {
                blackOverlay.blocksRaycasts = false;
                finished?.Invoke();
            });
    }

    public void EnterGameplay(string sceneName)
    {
        if (loading || blackOverlay == null || string.IsNullOrEmpty(sceneName)) return;
        loading = true;
        gameplayTransition = this;
        destination = sceneName;
        blackOverlay.blocksRaycasts = true;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        fade?.Kill();
        musicFade = DOTween.To(() => AudioManager.MusicTransitionGain,
                AudioManager.SetMusicTransitionGain, 0f, fadeSeconds)
            .SetEase(Ease.InOutSine).SetUpdate(true);
        fade = blackOverlay.DOFade(1f, fadeSeconds).SetUpdate(true)
            .SetEase(Ease.InOutSine).OnComplete(BeginLoad);
    }

    private void BeginLoad()
    {
        AudioManager.SetMusicTransitionGain(0f);
        GameObject prefab = Resources.Load<GameObject>("TatuCarregamento");
        if (prefab != null)
        {
            GameObject screen = Instantiate(prefab, blackOverlay.transform, false);
            loadingStatus = screen.GetComponentInChildren<Text>();
            loadingPresentation = screen.AddComponent<CanvasGroup>();
            loadingPresentation.interactable = loadingPresentation.blocksRaycasts = false;
        }
        StartCoroutine(LoadGameplay());
    }

    private IEnumerator LoadGameplay()
    {
        // Renderiza o primeiro quadro antes de pedir a cena.
        yield return null;
        priorLoadingPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        adjustedLoadingPriority = true;
        gameplayLoad = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
        gameplayLoad.allowSceneActivation = false;

        float visibleSeconds = 0f;
        float shownProgress = 0f;
        int lastPercent = -1;
        while (gameplayLoad.progress < 0.9f ||
            visibleSeconds < MinimumLoadingSeconds || shownProgress < 99f)
        {
            // Um quadro demorado durante a integração de ativos não pula a animação.
            float seenFrame = Mathf.Min(Time.unscaledDeltaTime, 0.10f);
            visibleSeconds += seenFrame;
            float loadedPercent = Mathf.Clamp01(gameplayLoad.progress / 0.9f) * 99f;
            shownProgress = Mathf.MoveTowards(shownProgress, loadedPercent, 58f * seenFrame);
            int percent = Mathf.FloorToInt(shownProgress);
            if (loadingStatus != null && percent != lastPercent)
            {
                loadingStatus.text = "Carregando... " + percent + "%";
                lastPercent = percent;
            }
            yield return null;
        }
        gameplayLoad.allowSceneActivation = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!loading || scene.name != destination) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        fade?.Kill();
        gameplayLoad = null;
        RestoreLoadingPriority();
        if (loadingStatus != null) loadingStatus.text = "Carregando... 100%";
        StartCoroutine(RevealGameplay(scene));
    }

    private IEnumerator RevealGameplay(Scene scene)
    {
        DayCycle dayCycle = FindFirstObjectByType<DayCycle>();
        while (dayCycle != null && dayCycle.isActiveAndEnabled &&
            dayCycle.gameObject.scene == scene && !dayCycle.FirstDayPreparationFinished)
            yield return null;

        var reveal = DOTween.Sequence().SetUpdate(true);
        if (loadingPresentation != null)
            reveal.Append(loadingPresentation.DOFade(0f, loadingExitSeconds).SetEase(Ease.InOutSine));
        float revealStart = reveal.Duration();
        bool hasDayCycle = dayCycle != null && dayCycle.isActiveAndEnabled && dayCycle.gameObject.scene == scene;
        if (hasDayCycle)
            reveal.AppendCallback(() =>
            {
                if (dayCycle != null) dayCycle.RevealFirstDayBackdrop();
            });
        reveal.Append(blackOverlay.DOFade(0f, gameplayRevealSeconds).SetEase(Ease.InOutSine));
        if (hasDayCycle)
            reveal.InsertCallback(revealStart + gameplayRevealSeconds * firstEventAtRevealProgress, () =>
            {
                if (dayCycle != null) dayCycle.ShowFirstDayEvent();
            });
        fade = reveal.OnComplete(() =>
        {
            AudioManager.RestartMusicAfterTransition();
            gameplayTransition = null;
            loading = false;
            Destroy(gameObject);
        });
    }

    private void RestoreLoadingPriority()
    {
        if (!adjustedLoadingPriority) return;
        Application.backgroundLoadingPriority = priorLoadingPriority;
        adjustedLoadingPriority = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        fade?.Kill();
        musicFade?.Kill();
        if (gameplayLoad != null) gameplayLoad.allowSceneActivation = true;
        RestoreLoadingPriority();
        if (gameplayTransition == this) gameplayTransition = null;
        if (loading) AudioManager.SetMusicTransitionGain(1f);
    }
}
