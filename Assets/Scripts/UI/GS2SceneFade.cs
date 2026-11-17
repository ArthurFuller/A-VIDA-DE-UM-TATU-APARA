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

    private Tween fade;
    private Tween musicFade;
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

    public void EnterGameplay(string sceneName)
    {
        if (loading || blackOverlay == null || string.IsNullOrEmpty(sceneName)) return;
        loading = true;
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
            Animator animator = screen.GetComponentInChildren<Animator>();
            if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
        StartCoroutine(LoadGameplay());
    }

    private IEnumerator LoadGameplay()
    {
        // Deixa a primeira imagem da tela de carregamento aparecer antes de iniciar a troca.
        yield return null;
        float visibleSince = Time.realtimeSinceStartup;
        gameplayLoad = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
        gameplayLoad.allowSceneActivation = false;
        int lastPercent = -1;
        while (gameplayLoad.progress < 0.9f ||
            Time.realtimeSinceStartup - visibleSince < MinimumLoadingSeconds)
        {
            int percent = Mathf.FloorToInt(Mathf.Clamp01(gameplayLoad.progress / 0.9f) * 99f);
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
        if (loadingStatus != null) loadingStatus.text = "Carregando... 100%";
        fade = blackOverlay.DOFade(0f, fadeSeconds).SetUpdate(true)
            .SetEase(Ease.InOutSine).OnComplete(() =>
            {
                AudioManager.RestartMusicAfterTransition();
                loading = false;
                Destroy(gameObject);
            });
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        fade?.Kill();
        musicFade?.Kill();
        if (gameplayLoad != null) gameplayLoad.allowSceneActivation = true;
        if (loading) AudioManager.SetMusicTransitionGain(1f);
    }
}
