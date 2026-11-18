using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Vinheta opcional do estúdio, exibida sobre a MainMenu sem trocar de cena.
// Sem VideoClip atribuído, o GS2FrontEnd conserva integralmente o fluxo anterior.
public sealed class GS2StudioIntro : MonoBehaviour
{
    private const float MaxPrepareSeconds = 6f;
    private const float MaxPlaybackSeconds = 45f;
    private static bool playedThisSession;

    private GameObject overlayObject;
    private GameObject videoStage;
    private CanvasGroup overlay;
    private CanvasGroup blackCurtain;
    private CanvasGroup paleCurtain;
    private Color videoBackground;
    private VideoPlayer player;
    private RenderTexture videoTexture;
    private AudioSource videoAudio;
    private VideoClip clip;
    private float initialBlackSeconds;
    private float videoRevealSeconds;
    private float videoDarkenSeconds;
    private float postVideoBlackSeconds;
    private float menuRevealSeconds;
    private bool firstFrame;
    private bool ended;
    private bool failed;
    private bool holdsListener;
    private bool previousAudioPause;
    private bool finished;

    public static bool ShouldPlay(VideoClip video) => video != null && !playedThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => playedThisSession = false;

    // Chamado no Awake do menu, ANTES do Start do AudioManager.
    public void Initialize(VideoClip video, float initialBlackHold, float videoReveal,
        float videoDarken, float postVideoBlackHold, float menuReveal, Color backgroundColor)
    {
        clip = video;
        initialBlackSeconds = Mathf.Max(0f, initialBlackHold);
        videoRevealSeconds = Mathf.Clamp(videoReveal, 0.2f, 3f);
        videoDarkenSeconds = Mathf.Clamp(videoDarken, 0.2f, 2f);
        // O Inspector de cenas antigas pode ter 0 para o campo novo.
        postVideoBlackSeconds = postVideoBlackHold <= 0f ? 1.35f :
            Mathf.Clamp(postVideoBlackHold, 1f, 2.5f);
        // Garante o clareamento lento mesmo com 0.90s serializado na cena antiga.
        menuRevealSeconds = Mathf.Clamp(menuReveal, 1.7f, 3f);
        videoBackground = backgroundColor;
        videoBackground.a = 1f;
        playedThisSession = true;
        previousAudioPause = AudioListener.pause;
        AudioListener.pause = true;
        holdsListener = true;
        CreateOverlay();
    }

    public void Begin(Action revealMenu, bool useOriginalMenuEntrance = false)
    {
        if (clip == null || overlay == null)
        {
            Finish(revealMenu);
            ReleaseAudio();
            return;
        }
        StartCoroutine(PlaySequence(revealMenu, useOriginalMenuEntrance));
    }

    private void CreateOverlay()
    {
        overlayObject = new GameObject("Abertura do estudio (temporaria)",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = overlayObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = overlayObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        overlay = overlayObject.GetComponent<CanvasGroup>();
        overlay.alpha = 1f;
        overlay.blocksRaycasts = true;
        overlay.interactable = true;

        // O objeto do vídeo é separado da cortina preta. No fim da vinheta,
        // ele é desativado ainda no escuro, sem revelar nenhum frame residual.
        videoStage = new GameObject("Video do estudio (desativado ao terminar)", typeof(RectTransform));
        var videoRect = (RectTransform)videoStage.transform;
        videoRect.SetParent(overlayObject.transform, false);
        Stretch(videoRect);

        // O MP4 e 16:9 e tem fundo branco rosado. Nas laterais, mantemos a
        // mesma cor para evitar barras pretas em resolucoes nao 16:9.
        Image background = CreateImage("Fundo compativel com o video", videoStage.transform);
        background.color = videoBackground;

        var imageObject = new GameObject("Imagem do MP4", typeof(RectTransform),
            typeof(RawImage), typeof(AspectRatioFitter));
        var imageRect = (RectTransform)imageObject.transform;
        imageRect.SetParent(videoStage.transform, false);
        Stretch(imageRect);
        var ratio = imageObject.GetComponent<AspectRatioFitter>();
        ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        ratio.aspectRatio = clip.width > 0 && clip.height > 0
            ? (float)clip.width / clip.height : 16f / 9f;
        RawImage image = imageObject.GetComponent<RawImage>();
        image.raycastTarget = false;

        int width = clip.width > 0 ? (int)clip.width : 1920;
        int height = clip.height > 0 ? (int)clip.height : 1080;
        float factor = Mathf.Min(1f, 1920f / Mathf.Max(width, height));
        width = Mathf.Max(16, Mathf.RoundToInt(width * factor));
        height = Mathf.Max(16, Mathf.RoundToInt(height * factor));
        videoTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "StudioIntro_RT",
            antiAliasing = 1
        };
        videoTexture.Create();
        image.texture = videoTexture;

        // Cortina da cor do fundo: cobre a textura antes do primeiro frame e
        // evita o flash preto de uma RenderTexture ainda nao preenchida.
        Image pale = CreateImage("Fundo claro de transicao", overlayObject.transform);
        pale.color = videoBackground;
        paleCurtain = pale.gameObject.AddComponent<CanvasGroup>();
        paleCurtain.alpha = 1f;

        Image darkness = CreateImage("Escurecimento do video", overlayObject.transform);
        darkness.color = Color.black;
        blackCurtain = darkness.gameObject.AddComponent<CanvasGroup>();
        blackCurtain.alpha = 1f;

        player = videoStage.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.isLooping = false;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;
        player.timeReference = VideoTimeReference.Freerun;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = videoTexture;
        player.sendFrameReadyEvents = true;
        player.frameReady += OnFrameReady;
        player.loopPointReached += OnVideoEnded;
        player.errorReceived += OnVideoError;
        if (clip.audioTrackCount > 0)
        {
            videoAudio = videoStage.AddComponent<AudioSource>();
            videoAudio.playOnAwake = false;
            videoAudio.spatialBlend = 0f;
            videoAudio.ignoreListenerPause = true;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, videoAudio);
        }
        else player.audioOutputMode = VideoAudioOutputMode.None;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rect = image.rectTransform;
        rect.SetParent(parent, false);
        Stretch(rect);
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private IEnumerator PlaySequence(Action revealMenu, bool useOriginalMenuEntrance)
    {
        // Segura a tela totalmente preta no início, incluindo preparação e
        // decodificação do primeiro frame. Nenhum fade do menu aparece aqui.
        float blackStartedAt = Time.realtimeSinceStartup;
        yield return null;
        try { player.Prepare(); }
        catch (Exception error)
        {
            failed = true;
            Debug.LogWarning("Abertura do estúdio: falha ao preparar MP4: " + error.Message, this);
        }
        float deadline = Time.realtimeSinceStartup + MaxPrepareSeconds;
        while (!player.isPrepared && !failed && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (failed || !player.isPrepared)
        {
            Debug.LogWarning("Abertura do estúdio: vídeo não pôde ser preparado; abrindo o menu.", this);
            yield return RevealFallback(revealMenu, useOriginalMenuEntrance);
            yield break;
        }

        // Decodifica o primeiro quadro enquanto o preto continua 100% opaco.
        // Congelá-lo evita perder a entrada da logo durante o clareamento.
        try { player.Play(); }
        catch (Exception error)
        {
            failed = true;
            Debug.LogWarning("Abertura do estúdio: falha ao reproduzir MP4: " + error.Message, this);
        }
        deadline = Time.realtimeSinceStartup + 2.5f;
        while (!firstFrame && !failed && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (failed || !firstFrame)
        {
            if (!failed) Debug.LogWarning("Abertura do estúdio: primeiro quadro não chegou.", this);
            yield return RevealFallback(revealMenu, useOriginalMenuEntrance);
            yield break;
        }
        player.Pause();

        // A cortina branca só existia para esconder o buffer sem quadro. Ela
        // sai ANTES do fade preto, nunca durante a reprodução ou o encerramento.
        if (paleCurtain != null) paleCurtain.gameObject.SetActive(false);
        while (Time.realtimeSinceStartup - blackStartedAt < initialBlackSeconds)
            yield return null;

        yield return Fade(blackCurtain, 1f, 0f, videoRevealSeconds);
        if (failed)
        {
            yield return RevealFallback(revealMenu, useOriginalMenuEntrance);
            yield break;
        }

        // A animação do vídeo começa somente depois da revelação lenta.
        player.Play();
        float playbackStartedAt = Time.realtimeSinceStartup;
        float darkenAt = Mathf.Max(0f, (float)clip.length - videoDarkenSeconds);
        deadline = Time.realtimeSinceStartup + Mathf.Min(MaxPlaybackSeconds,
            Mathf.Max(2f, (float)clip.length + 2f));
        while (!ended && !failed && player.isPlaying &&
            player.time < darkenAt && Time.realtimeSinceStartup - playbackStartedAt < darkenAt &&
            Time.realtimeSinceStartup < deadline)
            yield return null;

        // Começa a escurecer ANTES do fim do MP4, sobre a saída da logo.
        // Não esperamos o vídeo clarear sozinho para só então fechar a cortina.
        yield return Fade(blackCurtain, blackCurtain.alpha, 1f, videoDarkenSeconds);
        float fullyDarkAt = Time.realtimeSinceStartup;
        // Se ainda restarem quadros, terminam atrás do preto. Em caso de
        // atraso na reprodução, não prendemos o jogador em uma tela escura.
        float finishDeadline = Time.realtimeSinceStartup + 0.45f;
        while (!ended && !failed && player.isPlaying &&
            Time.realtimeSinceStartup < finishDeadline)
            yield return null;

        ShutdownVideo();
        if (useOriginalMenuEntrance)
        {
            // No menu, a entrada original ja tem a propria cortina preta,
            // seu tempo de espera e seu fade, seguidos das quedas dos itens.
            // Comecamos essa sequencia enquanto o video ainda esta coberto;
            // a cortina original assume a tela sem um quadro descoberto.
            Finish(revealMenu);
            ReleaseAudio();
            Cleanup();
            yield break;
        }
        // Nickname: preserva a pausa no preto e a revelacao lenta existentes.
        Finish(revealMenu);
        // Mantém o preto COMPLETO por 1,35s (ou o valor do Inspector),
        // contado desde o fim do fade, mesmo se o vídeo demorou a desligar.
        while (Time.realtimeSinceStartup - fullyDarkAt < postVideoBlackSeconds)
            yield return null;
        // É a MESMA cortina que clareia uma única vez para o próximo estado.
        yield return Fade(blackCurtain, 1f, 0f, menuRevealSeconds);
        ReleaseAudio();
        Cleanup();
    }

    private IEnumerator RevealFallback(Action revealMenu, bool useOriginalMenuEntrance)
    {
        if (blackCurtain != null && blackCurtain.alpha < 1f)
            yield return Fade(blackCurtain, blackCurtain.alpha, 1f, videoDarkenSeconds);
        float fullyDarkAt = Time.realtimeSinceStartup;
        ShutdownVideo();
        if (useOriginalMenuEntrance)
        {
            Finish(revealMenu);
            ReleaseAudio();
            Cleanup();
            yield break;
        }
        Finish(revealMenu);
        while (Time.realtimeSinceStartup - fullyDarkAt < postVideoBlackSeconds)
            yield return null;
        yield return Fade(blackCurtain, 1f, 0f, menuRevealSeconds);
        ReleaseAudio();
        Cleanup();
    }

    private void ShutdownVideo()
    {
        if (player != null)
        {
            player.Stop();
            player.targetTexture = null;
        }
        // Importante: o vídeo sai da Hierarchy ainda com a cortina preta ativa.
        if (videoStage != null) videoStage.SetActive(false);
        if (paleCurtain != null) paleCurtain.gameObject.SetActive(false);
    }

    private static IEnumerator Fade(CanvasGroup target, float from, float to, float duration)
    {
        if (target == null) yield break;
        target.alpha = from;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            target.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        if (target != null) target.alpha = to;
    }

    private void Finish(Action revealMenu)
    {
        if (finished) return;
        finished = true;
        // Prepara o nickname/menu sob o preto. A musica so e liberada
        // depois do unico fade de revelacao, em PlaySequence/RevealFallback.
        revealMenu?.Invoke();
    }

    private void ReleaseAudio()
    {
        if (!holdsListener) return;
        holdsListener = false;
        AudioListener.pause = previousAudioPause;
    }

    private void OnFrameReady(VideoPlayer video, long frame) => firstFrame = true;
    private void OnVideoEnded(VideoPlayer video) => ended = true;
    private void OnVideoError(VideoPlayer video, string message)
    {
        failed = true;
        Debug.LogWarning("Abertura do estúdio: " + message, this);
    }

    private void Cleanup()
    {
        if (player != null)
        {
            player.frameReady -= OnFrameReady;
            player.loopPointReached -= OnVideoEnded;
            player.errorReceived -= OnVideoError;
            player.Stop();
            player.targetTexture = null;
        }
        if (overlayObject != null) Destroy(overlayObject);
        overlayObject = null;
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
            videoTexture = null;
        }
        Destroy(this);
    }

    private void OnDestroy()
    {
        ReleaseAudio();
        if (player != null)
        {
            player.frameReady -= OnFrameReady;
            player.loopPointReached -= OnVideoEnded;
            player.errorReceived -= OnVideoError;
        }
        if (overlayObject != null) Destroy(overlayObject);
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }
    }
}
