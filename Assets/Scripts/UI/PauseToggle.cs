using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Controla a pausa e anima os elementos montados na cena, mantendo sua hierarquia.
public sealed class PauseToggle : MonoBehaviour
{
    [Header("Botao original da HUD")]
    [SerializeField] private Button button;
    [SerializeField] private Text label; // Referencia historica; nunca modificada.
    [SerializeField] private Image resumeArtwork; // Referencia historica; nunca modificada.
    [SerializeField] private CanvasGroup pauseOverlay;
    [SerializeField] private Button missionsButton;

    [Header("Painel editavel na Hierarchy")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup pauseGroup;
    [SerializeField] private RectTransform buttonViewport;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Shader irisShader;

    [Header("Opcoes da gameplay")]
    [SerializeField] private RectTransform optionsPage;
    [SerializeField] private CanvasGroup optionsGroup;
    [SerializeField] private Button optionsBackButton;

    [Header("Animacoes (tempo nao escalado)")]
    [SerializeField, Min(0.05f)] private float panelEnterDuration = 0.44f;
    [SerializeField, Min(0.05f)] private float panelExitDuration = 0.34f;
    [SerializeField, Min(0.05f)] private float buttonEnterDuration = 0.38f;
    [SerializeField, Min(0f)] private float buttonWaveInterval = 0.1f;
    [SerializeField, Min(0.05f)] private float optionsSlideDuration = 0.4f;

    private RectTransform[] menuButtons;
    private Button[] menuActions;
    private Vector2[] buttonHomes;
    private Vector2 panelHome;
    private RectMask2D buttonMask;
    private bool paused;
    private bool busy;
    private Image backdrop;
    private Color backdropColor;
    private TopBarAutoHide topBar;
    private DayCycle dayCycle;
    private DayTimeline timeline;
    private Sequence motion;

    // Preserva o estado exato das animacoes da gameplay durante a pausa.
    private readonly List<Tween> frozenTweens = new List<Tween>();
    private readonly List<Animator> frozenAnimators = new List<Animator>();
    private readonly List<float> animatorSpeeds = new List<float>();

    // O bloqueio da missao nao deve alterar a cor exibida pelo Button.
    private ColorBlock originalMissionColors;
    private bool missionColorsCaptured;
    private bool missionWasInteractable;

    private enum Page { Pause, Options }
    private Page current;

    private void Awake()
    {
        topBar = GetComponentInParent<TopBarAutoHide>();
        if (button != null) button.onClick.AddListener(Toggle);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenOptions);
        if (exitButton != null) exitButton.onClick.AddListener(ExitToMenu);
        if (optionsBackButton != null) optionsBackButton.onClick.AddListener(BackFromOptions);

        if (pauseOverlay == null || panel == null || pauseGroup == null || buttonViewport == null ||
            resumeButton == null || settingsButton == null || exitButton == null || optionsPage == null)
        {
            Debug.LogError("PauseToggle: referencias da interface na Hierarchy incompletas.", this);
            enabled = false;
            return;
        }

        dayCycle = FindFirstObjectByType<DayCycle>();
        timeline = FindFirstObjectByType<DayTimeline>();
        if (settingsButton.targetGraphic is Image settingsImage && settingsImage.sprite != null &&
            settingsImage.sprite.texture.isReadable)
            settingsImage.alphaHitTestMinimumThreshold = 0.1f;

        backdrop = pauseOverlay.GetComponent<Image>();
        if (backdrop != null) backdropColor = backdrop.color;
        FullscreenBackdrop.Cover(backdrop);
        panelHome = panel.anchoredPosition;
        buttonMask = buttonViewport.GetComponent<RectMask2D>();
        menuActions = new[] { resumeButton, settingsButton, exitButton };
        menuButtons = new RectTransform[menuActions.Length];
        buttonHomes = new Vector2[menuActions.Length];
        for (int i = 0; i < menuActions.Length; i++)
        {
            menuButtons[i] = menuActions[i].transform as RectTransform;
            buttonHomes[i] = menuButtons[i].anchoredPosition;
        }
        panel.gameObject.SetActive(false);
        optionsPage.gameObject.SetActive(false);
        pauseOverlay.gameObject.SetActive(false);
        UpdateGameplayButtons();
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
        if (!paused)
        {
            if (Time.timeScale <= 0f || GS2SceneFade.IsEnteringGameplay) return;
            if (timeline != null && timeline.TryCloseInspection()) return;
            if (SertaoCoinToss.IsOpen || (dayCycle != null && !dayCycle.CanAcceptActions) ||
                (button != null && (!button.isActiveAndEnabled || !button.IsInteractable()))) return;
        }
        Toggle();
    }

    private void OnDestroy()
    {
        motion?.Kill();
        if (button != null) button.onClick.RemoveListener(Toggle);
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenOptions);
        if (exitButton != null) exitButton.onClick.RemoveListener(ExitToMenu);
        if (optionsBackButton != null) optionsBackButton.onClick.RemoveListener(BackFromOptions);
        if (paused)
        {
            topBar?.SetPaused(false);
            Time.timeScale = 1f;
        }
        RestoreGameplayAnimations();
        RestoreMissionButton();
    }

    public void Toggle()
    {
        if (busy || current != Page.Pause || !enabled) return;
        if (paused) Resume();
        else Open();
    }

    private void Open()
    {
        if (paused) return;
        paused = busy = true;
        current = Page.Pause;
        AudioManager.Play(SoundCue.UiPaper);
        FreezeGameplayAnimations();
        Time.timeScale = 0f;
        topBar?.SetPaused(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        pauseOverlay.gameObject.SetActive(true);
        panel.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Image frame = panel.GetComponent<Image>();
        bool hasFrameMask = buttonMask != null && frame != null && frame.canvas != null &&
            frame.overrideSprite != null;
        Rect inner = hasFrameMask ? GS2FrontEnd.FitMaskToFrame(buttonMask, frame) : buttonViewport.rect;
        if (!hasFrameMask && buttonMask != null) inner.yMin += buttonMask.padding.y;
        pauseOverlay.alpha = 1f;
        pauseOverlay.interactable = pauseOverlay.blocksRaycasts = true;
        if (backdrop != null) backdrop.color = backdropColor;
        panel.anchoredPosition = panelHome + Vector2.down * BelowViewport();
        pauseGroup.blocksRaycasts = false;
        UpdateGameplayButtons();

        motion?.Kill();
        motion = DOTween.Sequence().SetUpdate(true);
        motion.Append(panel.DOAnchorPos(panelHome, panelEnterDuration).SetEase(Ease.OutBack, 1.12f));
        for (int i = 0; i < menuButtons.Length; i++)
        {
            RectTransform item = menuButtons[i];
            item.anchoredPosition = buttonHomes[i];
            item.anchoredPosition += GS2FrontEnd.OffsetOutsideFrame(item,
                hasFrameMask ? panel : buttonViewport, inner, Vector2.down);
            motion.Insert(panelEnterDuration + i * buttonWaveInterval,
                item.DOAnchorPos(buttonHomes[i], buttonEnterDuration).SetEase(Ease.OutBack));
        }
        motion.OnComplete(() =>
        {
            pauseGroup.blocksRaycasts = true;
            busy = false;
        });
    }

    private float BelowViewport()
    {
        RectTransform root = pauseOverlay.transform as RectTransform;
        return (root != null ? root.rect.height : 506f) * 0.5f + panel.rect.height * 0.5f + 20f;
    }

    public void Resume()
    {
        if (!paused || busy || current != Page.Pause) return;
        busy = true;
        AudioManager.Play(SoundCue.UiPaper);
        pauseGroup.blocksRaycasts = false;
        motion?.Kill();
        motion = DOTween.Sequence().SetUpdate(true)
            .Join(panel.DOAnchorPos(panelHome + Vector2.up * BelowViewport(), panelExitDuration)
                .SetEase(Ease.InCubic));
        if (backdrop != null) motion.Join(backdrop.DOFade(0f, panelExitDuration));
        motion.OnComplete(() =>
        {
            paused = busy = false;
            panel.anchoredPosition = panelHome;
            pauseOverlay.gameObject.SetActive(false);
            Time.timeScale = 1f;
            topBar?.SetPaused(false);
            RestoreGameplayAnimations();
            UpdateGameplayButtons();
        });
    }

    public void OpenOptions()
    {
        if (!paused || busy || current != Page.Pause) return;
        AudioManager.Play(SoundCue.UiPaper);
        SlidePages(panel, optionsPage, optionsGroup, Page.Options, false);
    }

    private void BackFromOptions()
    {
        if (busy || current != Page.Options) return;
        AudioManager.Play(SoundCue.UiPaper);
        SlidePages(optionsPage, panel, pauseGroup, Page.Pause, true);
    }

    private void SlidePages(RectTransform from, RectTransform to, CanvasGroup toGroup,
        Page next, bool back)
    {
        busy = true;
        motion?.Kill();
        pauseGroup.blocksRaycasts = false;
        if (optionsGroup != null) optionsGroup.blocksRaycasts = false;

        Canvas.ForceUpdateCanvases();
        RectTransform overlayRect = pauseOverlay.transform as RectTransform;
        float width = overlayRect != null ? overlayRect.rect.width : 900f;
        float distance = Mathf.Max(width + 8f,
            (panel.rect.width + optionsPage.rect.width) * 0.5f + 20f);
        float travel = back ? distance : -distance;
        float fromX = from.anchoredPosition.x;
        float toX = to.anchoredPosition.x;
        GS2FrontEnd.SetHorizontalPosition(to, toX - travel);
        if (toGroup != null) toGroup.alpha = 1f;

        motion = DOTween.Sequence().SetUpdate(UpdateType.Normal, true)
            .Append(from.DOAnchorPosX(fromX + travel, optionsSlideDuration)
                .SetEase(Ease.InOutCubic))
            .Join(to.DOAnchorPosX(toX, optionsSlideDuration)
                .SetEase(Ease.InOutCubic));
        if (next == Page.Options)
        {
            to.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            GS2FrontEnd.AnimateOptionsWave(to, to.GetComponent<Image>(),
                distance, motion, optionsSlideDuration);
        }
        else to.gameObject.SetActive(true);

        motion.InsertCallback(optionsSlideDuration, () =>
        {
            GS2FrontEnd.SetHorizontalPosition(to, toX);
            from.gameObject.SetActive(false);
            GS2FrontEnd.SetHorizontalPosition(from, fromX);
        });
        motion.OnComplete(() =>
        {
            if (toGroup != null) toGroup.blocksRaycasts = true;
            current = next;
            busy = false;
        });
    }

    private void ExitToMenu()
    {
        if (!paused || busy || current != Page.Pause) return;
        if (irisShader == null)
        {
            Debug.LogError("Pause: shader de transicao radial nao configurado.", this);
            return;
        }
        busy = true;
        pauseGroup.blocksRaycasts = false;
        AudioManager.Play(SoundCue.UiPaper);
        PauseIrisTransition.ToMenu(irisShader);
    }

    private void FreezeGameplayAnimations()
    {
        // DOTween em tempo nao escalado tambem precisa parar. Capturar apenas
        // tweens que ja estavam rodando preserva os que ja estavam pausados.
        frozenTweens.Clear();
        List<Tween> playing = DOTween.PlayingTweens();
        if (playing != null)
        {
            foreach (Tween tween in playing)
            {
                if (tween == null || !tween.IsActive() || !tween.IsPlaying()) continue;
                frozenTweens.Add(tween);
                tween.Pause();
            }
        }

        // Alguns Animators, como o lagarto da musica, usam UnscaledTime.
        // Congelar speed funciona com ambos os modos, sem mudar o sprite ou o controller.
        frozenAnimators.Clear();
        animatorSpeeds.Clear();
        Animator[] animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
        foreach (Animator animator in animators)
        {
            if (animator == null || !animator.gameObject.activeInHierarchy ||
                (pauseOverlay != null && animator.transform.IsChildOf(pauseOverlay.transform)))
                continue;
            frozenAnimators.Add(animator);
            animatorSpeeds.Add(animator.speed);
            animator.speed = 0f;
        }
    }

    private void RestoreGameplayAnimations()
    {
        for (int i = 0; i < frozenAnimators.Count; i++)
            if (frozenAnimators[i] != null)
                frozenAnimators[i].speed = animatorSpeeds[i];
        frozenAnimators.Clear();
        animatorSpeeds.Clear();

        foreach (Tween tween in frozenTweens)
            if (tween != null && tween.IsActive() && !tween.IsPlaying()) tween.Play();
        frozenTweens.Clear();
    }

    private void RestoreMissionButton()
    {
        if (!missionColorsCaptured || missionsButton == null) return;
        missionsButton.colors = originalMissionColors;
        missionsButton.interactable = missionWasInteractable;
        missionColorsCaptured = false;
    }

    private void UpdateGameplayButtons()
    {
        if (missionsButton != null)
        {
            if (paused)
            {
                if (!missionColorsCaptured)
                {
                    originalMissionColors = missionsButton.colors;
                    missionWasInteractable = missionsButton.interactable;
                    missionColorsCaptured = true;
                }
                // O estado Disabled do Button normalmente aplica alpha 0.5.
                // Mantemos a cor Normal, mas o clique continua bloqueado.
                ColorBlock colors = missionsButton.colors;
                colors.disabledColor = colors.normalColor;
                missionsButton.colors = colors;
                missionsButton.interactable = false;
            }
            else RestoreMissionButton();
        }
        if (button != null) button.interactable = !paused;
    }
}
