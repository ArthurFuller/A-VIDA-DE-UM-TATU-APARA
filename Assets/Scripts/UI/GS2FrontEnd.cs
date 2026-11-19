using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;
using UnityEngine.Video;

// Navegação da cena de entrada; a transição preta permanece até a cena do jogo abrir.
public sealed class GS2FrontEnd : MonoBehaviour
{
    [SerializeField] private CanvasGroup root;
    [SerializeField] private CanvasGroup blackFade;
    [SerializeField] private GS2SceneFade sceneFade;
    [SerializeField] private RectTransform mainPage;
    [SerializeField] private RectTransform optionsPage;
    [SerializeField] private GS2Profile.NicknamePage nicknamePage;
    [SerializeField] private RectTransform leaderboardPage;
    [SerializeField] private CanvasGroup mainGroup;
    [SerializeField] private CanvasGroup optionsGroup;
    [SerializeField] private CanvasGroup leaderboardGroup;
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button mainLeaderboardButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button leaderboardButton;
    [SerializeField] private Button leaderboardBackButton;
    [SerializeField] private Text statusText;
    [SerializeField, Min(0.1f)] private float slideSeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 0.36f;
    [SerializeField, Range(0.8f, 1f)] private float enteringScale = 0.975f;
    [Header("Arte e movimento do menu inicial")]
    [SerializeField] private RectTransform mainLogo;
    [SerializeField] private Image mainBackground;
    [SerializeField] private Image optionsBackground;
    [SerializeField] private Sprite optionsFrameSprite;
    [SerializeField, Min(0.1f)] private float introDarkHoldSeconds = 0.18f;
    [SerializeField, Min(0.15f)] private float introRevealSeconds = 0.65f;
    [SerializeField, Min(0.1f)] private float logoDropSeconds = 0.50f;
    [SerializeField, Min(0.1f)] private float buttonDropSeconds = 0.27f;
    [Header("Vinheta de abertura da desenvolvedora")]
    [SerializeField, Tooltip("Arraste aqui o MP4 quando estiver pronto. Sem vídeo, a abertura atual não muda.")]
    private VideoClip studioOpeningVideo;
    [SerializeField, Min(0f), Tooltip("Tempo em preto antes de aparecer a vinheta. Padrão: 2,5 segundos.")]
    private float studioBlackHoldSeconds = 2.5f;
    [SerializeField, Range(0.2f, 3f), Tooltip("Clareamento lento antes de começar a animação do MP4.")]
    private float studioRevealSeconds = 1.05f;
    [SerializeField, Range(0.2f, 2f), Tooltip("Escurecimento sobre a saída da logo, antes do vídeo acabar.")]
    private float studioDarkenSeconds = 0.68f;
    [SerializeField, Range(1f, 2.5f), Tooltip("Tempo completamente preto após o vídeo terminar, antes de mostrar nickname/menu.")]
    private float studioPostVideoBlackSeconds = 1.35f;
    [SerializeField, Range(1.7f, 3f), Tooltip("Clareamento suave e único para revelar nickname ou menu.")]
    private float studioMenuRevealSeconds = 1.75f;
    [SerializeField, Tooltip("Cor do fundo do MP4 (Old Welder: branco rosado #FFF6F9).")]
    private Color studioVideoBackground = new Color32(255, 246, 249, 255);
    private GS2StudioIntro studioIntro;
    private bool nicknameAfterStudioIntro;

    [Header("Sons da entrada inicial")]
    [SerializeField] private SoundCue introImpactCue = SoundCue.CardPlaced;
    [SerializeField, Range(0f, 1f), Tooltip("Volume do impacto ao pousar o logo. Zero desliga.")]
    private float introLogoImpactVolume = 0.34f;
    [SerializeField, Range(0f, 1f), Tooltip("Volume do impacto de cada botão. Zero desliga.")]
    private float introButtonImpactVolume = 0.20f;
    private AudioSource introImpactSource;
    [SerializeField, Min(0f)] private float floatingHeight = 3.5f;
    [SerializeField, Min(0f)] private float floatingAngle = 1.1f;
    [SerializeField, InspectorName("Ondulação da moldura")] private BorderRipple borderRipple = new();
    private Sequence motion;
    private PageItems mainItems;
    private readonly List<(RectTransform Source, RectTransform Page, RectTransform Image)>
        backgroundLayouts = new();
    private Image mainPageBackground;
    private Image leaderboardBackground;
    private bool resizeBackgrounds;
    private bool slidingPages;
    private bool separateLogo;
    private bool floating;
    private bool startMenuEntrance;
    private bool waitingForPauseReveal;
    private bool advancingIntro;
    private CanvasGroup introCurtain;
    private float floatStarted;
    private bool locked;
    private Image inputShield;
    private EventSystem blockedEventSystem;
    private bool previousNavigation;
    private enum Page { Main, Options, Leaderboard }
    private Page current;
    private Page leaderboardBackPage = Page.Options;
    private bool returnToRanking;

    internal sealed class PageItems
    {
        public RectTransform[] rects;
        public Vector2[] positions;
        public Vector3[] scales;
        public Quaternion[] rotations;
    }

    private void Awake()
    {
        Time.timeScale = 0f;
        ApplyAnimationSettings(Resources.Load<MainMenuAnimationSettings>(MainMenuAnimationSettings.ResourceName));
        bool returningFromMatch = SertaoMatchResultsView.BindReturningMenu(this, out returnToRanking);
        bool returningFromPause = PauseIrisTransition.ReturningToMenu;
        if (!returningFromPause && !returningFromMatch && GS2StudioIntro.ShouldPlay(studioOpeningVideo))
        {
            studioIntro = gameObject.AddComponent<GS2StudioIntro>();
            studioIntro.Initialize(studioOpeningVideo, studioBlackHoldSeconds,
                studioRevealSeconds, studioDarkenSeconds, studioPostVideoBlackSeconds,
                studioMenuRevealSeconds, studioVideoBackground);
        }
        if (nicknamePage?.Root != null) nicknamePage.Root.SetActive(false);
        if (root != null) { root.alpha = 0f; root.interactable = true; root.blocksRaycasts = true; }
        SetInitialPage(mainPage, mainGroup, true);
        SetInitialPage(optionsPage, optionsGroup, false);
        SetInitialPage(leaderboardPage, leaderboardGroup, false);
        ConfigureMainArt();
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (mainLeaderboardButton != null) mainLeaderboardButton.onClick.AddListener(OpenLeaderboard);
        if (exitButton != null) exitButton.onClick.AddListener(Exit);
        if (optionsBackButton != null) optionsBackButton.onClick.AddListener(Back);
        if (leaderboardButton != null) leaderboardButton.onClick.AddListener(OpenLeaderboard);
        if (leaderboardBackButton != null) leaderboardBackButton.onClick.AddListener(Back);
        current = Page.Main;
        SetLocked(true);
        bool previewNickname = false;
#if UNITY_EDITOR
        previewNickname = SertaoPlayModeTests.TakeNicknamePreview();
#endif
        if (!returningFromPause && (previewNickname || !GS2Profile.HasNickname) &&
            root != null && optionsPage != null &&
            optionsPage.TryGetComponent(out GS2Profile profile) &&
            profile.ShowFirstRun(nicknamePage, root.transform, CompleteFirstRun, previewNickname))
        {
            SetInitialPage(mainPage, mainGroup, false);
            SetSeparateLogoVisible(false);
            SetLocked(true);
            nicknameAfterStudioIntro = studioIntro != null;
            if (!returningFromMatch && studioIntro == null)
                motion = DOTween.Sequence().SetUpdate(true)
                    .Append(root.DOFade(1f, fadeSeconds))
                    .OnComplete(() => SetLocked(false));
            return;
        }
        SetInitialDestination();
        startMenuEntrance = !returningFromMatch;
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        LayoutPageBackgrounds();
        if (locked) BlockNavigation();
        // Os botões precisam guardar a posição e a escala no Awake do próprio hover.
        if (studioIntro != null)
            studioIntro.Begin(() =>
            {
                studioIntro = null;
                // A unica cortina da vinheta permanece preta durante esta troca.
                // A transicao inicial da cena nao deve competir com ela.
                sceneFade?.ReleaseInitialMenuOverlay();
                if (nicknameAfterStudioIntro)
                {
                    nicknameAfterStudioIntro = false;
                    if (root != null) root.alpha = 1f;
                    SetLocked(false);
                }
                else if (startMenuEntrance)
                    // A cortina ORIGINAL do menu faz a propria revelacao antes
                    // dos elementos cairem; nao animar atras do fade longo do video.
                    AnimateMenuEntrance();
            }, !nicknameAfterStudioIntro);
        else if (startMenuEntrance) AnimateMenuEntrance();
    }

    public void ApplyAnimationSettings(MainMenuAnimationSettings settings)
    {
        if (settings == null) return;
        slideSeconds = settings.slideSeconds;
        fadeSeconds = settings.fadeSeconds;
        enteringScale = settings.enteringScale;
        introDarkHoldSeconds = settings.introDarkHoldSeconds;
        introRevealSeconds = settings.introRevealSeconds;
        logoDropSeconds = settings.logoDropSeconds;
        buttonDropSeconds = settings.buttonDropSeconds;
        floatingHeight = settings.floatingHeight;
        floatingAngle = settings.floatingAngle;
        borderRipple ??= new BorderRipple();
        if (settings.borderRipple != null) borderRipple.CopyFrom(settings.borderRipple);
    }

#if UNITY_EDITOR
    public bool CanReplayMainEntrance => Application.isPlaying && isActiveAndEnabled &&
        current == Page.Main && mainPage != null && mainPage.gameObject.activeInHierarchy &&
        !slidingPages && !waitingForPauseReveal && (!locked || advancingIntro);

    public void ReplayMainEntrance()
    {
        if (CanReplayMainEntrance) AnimateMenuEntrance();
    }
#endif

    private void ConfigureMainArt()
    {
        if (mainBackground == null && root != null)
        {
            mainBackground = FindAuthoredPageBackground(mainPage);
            if (mainBackground == null) mainBackground = FindNamedBackground(root.transform);
            if (mainBackground == null) mainBackground = FindNamedBackground(root.transform.parent);
        }
        if (mainLogo == null) mainLogo = FindMainLogo();
        CreatePageBackgrounds();
        borderRipple ??= new BorderRipple();
        borderRipple.Bind(mainPageBackground);
        separateLogo = mainLogo != null && mainPage != null && !mainLogo.IsChildOf(mainPage);
        var foreground = new List<RectTransform>(5);
        if (mainLogo != null) foreground.Add(mainLogo);
        if (playButton != null) foreground.Add((RectTransform)playButton.transform);
        if (optionsButton != null) foreground.Add((RectTransform)optionsButton.transform);
        if (mainLeaderboardButton != null) foreground.Add((RectTransform)mainLeaderboardButton.transform);
        if (exitButton != null) foreground.Add((RectTransform)exitButton.transform);
        mainItems = CaptureItems(foreground.ToArray());
    }

    private Sprite FindOptionsFrame(Sprite sharedSprite)
    {
        if (IsOptionsFrame(sharedSprite)) return sharedSprite;
        if (optionsPage != null)
            foreach (Image image in optionsPage.GetComponentsInChildren<Image>(true))
                if (IsOptionsFrame(image.sprite)) return image.sprite;
        return null;
    }

    private void CreatePageBackgrounds()
    {
        Image shared = root != null ? root.GetComponent<Image>() : null;
        Sprite frame = optionsFrameSprite != null ? optionsFrameSprite :
            FindOptionsFrame(shared != null ? shared.sprite : null);
        Image mainSource = mainBackground != null ? mainBackground : shared;
        Image optionsSource = optionsBackground != null ? optionsBackground : FindAuthoredPageBackground(optionsPage);
        mainPageBackground = CreatePageBackground(mainPage, mainSource);
        Image optionsImage = optionsSource != null ? CreatePageBackground(optionsPage, optionsSource) :
            CreatePageBackground(optionsPage, shared, frame);
        leaderboardBackground = CreatePageBackground(leaderboardPage, shared, frame);
        if (shared != null) shared.enabled = false;
        if (mainSource != null && mainSource != mainPageBackground) mainSource.enabled = false;
        if (optionsSource != null && optionsSource != optionsImage) optionsSource.enabled = false;
        if (optionsImage != null) optionsImage.maskable = false;
        optionsBackground = optionsImage;
    }

    private Image CreatePageBackground(RectTransform page, Image template, Sprite frame = null)
    {
        if (page == null || (template == null && frame == null)) return null;
        if (template != null && template.rectTransform.parent == page)
        {
            template.rectTransform.SetAsFirstSibling();
            template.raycastTarget = false;
            template.enabled = true;
            return template;
        }

        var image = new GameObject("Fundo da página", typeof(RectTransform), typeof(Image))
            .GetComponent<Image>();
        RectTransform rect = image.rectTransform;
        rect.SetParent(page, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = rect.anchorMax = page.pivot;
        rect.pivot = new Vector2(0.5f, 0.5f);
        bool restoreFrame = frame != null &&
            (template == null || template.sprite != frame || template.type != Image.Type.Sliced);
        if (template != null)
        {
            image.sprite = template.overrideSprite;
            image.material = template.material;
            image.type = template.type;
            image.color = template.color;
            image.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
            image.preserveAspect = template.preserveAspect;
            image.fillCenter = template.fillCenter;
        }
        if (restoreFrame)
        {
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.98f);
            image.pixelsPerUnitMultiplier = 6.05f;
            image.preserveAspect = false;
            image.fillCenter = true;
        }
        image.raycastTarget = false;
        image.maskable = false;
        RectTransform layoutSource = template != null ? template.rectTransform :
            root != null ? root.transform as RectTransform : page;
        backgroundLayouts.Add((layoutSource, page, rect));
        return image;
    }

    private void OnRectTransformDimensionsChange() => resizeBackgrounds = true;

    private void LayoutPageBackgrounds()
    {
        resizeBackgrounds = false;
        foreach (var (source, page, rect) in backgroundLayouts)
        {
            if (source == null || page == null || rect == null) continue;
            Rect sourceRect = source.rect;
            rect.localPosition = page.InverseTransformPoint(source.TransformPoint(sourceRect.center));
            rect.localRotation = Quaternion.Inverse(page.rotation) * source.rotation;
            Vector3 width = page.InverseTransformVector(source.TransformVector(Vector3.right * sourceRect.width));
            Vector3 height = page.InverseTransformVector(source.TransformVector(Vector3.up * sourceRect.height));
            rect.sizeDelta = new Vector2(width.magnitude, height.magnitude);
        }
    }

    private static bool IsOptionsFrame(Sprite sprite) => sprite != null &&
        sprite.border.sqrMagnitude > 0f &&
        sprite.name.Replace("-", "").Replace(" ", "").IndexOf("9SlicedBox",
            System.StringComparison.OrdinalIgnoreCase) >= 0;

    private static Image FindAuthoredPageBackground(RectTransform page)
    {
        Image child = FindNamedBackground(page);
        if (child != null) return child;
        return page != null && page.TryGetComponent(out Image image) && image.sprite != null ?
            image : null;
    }

    private static Image FindNamedBackground(Transform parent)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            string label = child.name.Trim().ToLowerInvariant();
            if ((label == "bg" || label == "fundo" || label.StartsWith("fundo do menu") ||
                label.StartsWith("background")) && child.TryGetComponent(out Image image))
                return image;
        }
        return null;
    }

    private RectTransform FindMainLogo()
    {
        if (mainPage == null) return null;
        RectTransform fallback = null;
        foreach (RectTransform rect in mainPage.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect == mainPage) continue;
            if (IsLogo(rect)) return rect;
            if (rect.name.Equals("Nome do jogo", System.StringComparison.OrdinalIgnoreCase)) fallback = rect;
        }
        if (root != null)
        {
            foreach (Transform child in root.transform)
                if (child is RectTransform rect && IsLogo(rect)) return rect;
            if (root.transform.parent != null)
                foreach (Transform child in root.transform.parent)
                    if (child != root.transform && child is RectTransform sibling && IsLogo(sibling))
                        return sibling;
        }
        return fallback;
    }

    private bool IsLogo(RectTransform rect)
    {
        if (mainBackground != null && rect == mainBackground.rectTransform) return false;
        if (rect.name.IndexOf("logo", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return rect.TryGetComponent(out Image image) && image.sprite != null &&
            image.sprite.name.IndexOf("logo", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetSeparateLogoVisible(bool visible)
    {
        if (separateLogo && mainLogo != null) mainLogo.gameObject.SetActive(visible);
    }

    private static Vector2 DropOrigin(Button button, RectTransform source)
    {
        if (button == null) return Vector2.zero;
        RectTransform rect = (RectTransform)button.transform;
        if (source == null) return rect.anchoredPosition + Vector2.up * 68f;
        Vector3 center = rect.parent.InverseTransformPoint(source.TransformPoint(source.rect.center));
        return rect.anchoredPosition + (Vector2)(center - rect.localPosition);
    }

    private void PrepareMainDrop()
    {
        Canvas.ForceUpdateCanvases();
        RestoreMainItems();
        for (int i = 0; i < mainItems.rects.Length; i++)
            if (mainItems.rects[i] != null) mainItems.rects[i].localScale = mainItems.scales[i];

        Vector2 playDropOrigin = DropOrigin(playButton, mainLogo);
        RectTransform playSource = playButton != null ? (RectTransform)playButton.transform : mainLogo;
        Vector2 optionsDropOrigin = DropOrigin(optionsButton, playSource);
        Vector2 rankingDropOrigin = DropOrigin(mainLeaderboardButton, optionsButton != null ?
            (RectTransform)optionsButton.transform : playSource);
        for (int i = 0; i < mainItems.rects.Length; i++)
        {
            RectTransform rect = mainItems.rects[i];
            if (rect == null) continue;
            if (rect == mainLogo)
            {
                if (root != null && root.transform is RectTransform viewport)
                {
                    Bounds logo = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, rect);
                    float rise = Mathf.Max(0f, viewport.rect.yMax - logo.min.y + 14f);
                    rect.anchoredPosition += (Vector2)rect.parent.InverseTransformVector(
                        viewport.TransformVector(Vector3.up * rise));
                }
                else rect.anchoredPosition += Vector2.up * 500f;
                continue;
            }
            rect.localScale = Vector3.zero;
            if (playButton != null && rect == playButton.transform)
                rect.anchoredPosition = playDropOrigin;
            if (optionsButton != null && rect == optionsButton.transform)
                rect.anchoredPosition = optionsDropOrigin;
            if (mainLeaderboardButton != null && rect == mainLeaderboardButton.transform)
                rect.anchoredPosition = rankingDropOrigin;
        }
    }

    private void AddMainDrop(Sequence sequence, float start)
    {
        PrepareMainDrop();
        if (borderRipple != null) borderRipple.ResetRipple();
        float impactAt = start;
        if (mainLogo != null)
        {
            int index = System.Array.IndexOf(mainItems.rects, mainLogo);
            sequence.Insert(start, mainLogo.DOAnchorPos(mainItems.positions[index], logoDropSeconds)
                .SetEase(Ease.InQuad));
            impactAt += logoDropSeconds;
            sequence.Insert(impactAt, mainLogo.DOPunchRotation(Vector3.forward * 6f,
                0.28f, 6, 0.45f));
            sequence.Insert(impactAt, mainLogo.DOPunchScale(mainItems.scales[index] * 0.065f,
                0.25f, 5, 0.35f));
            AddBorderImpact(sequence, impactAt, mainLogo, 1f);
        }
        if (playButton != null) impactAt = AddButtonDrop(sequence, playButton, impactAt + 0.025f, 0.62f);
        if (optionsButton != null) impactAt = AddButtonDrop(sequence, optionsButton, impactAt + 0.025f, 0.46f);
        if (mainLeaderboardButton != null)
            impactAt = AddButtonDrop(sequence, mainLeaderboardButton, impactAt + 0.025f, 0.46f);
        if (exitButton != null)
        {
            var rect = (RectTransform)exitButton.transform;
            Vector3 restingScale = mainItems.scales[System.Array.IndexOf(mainItems.rects, rect)];
            sequence.Insert(impactAt + 0.10f, rect.DOScale(restingScale * 1.10f, 0.22f)
                .SetEase(Ease.OutCubic));
            sequence.Insert(impactAt + 0.32f, rect.DOScale(restingScale, 0.13f)
                .SetEase(Ease.InOutSine));
        }
    }

    private float AddButtonDrop(Sequence sequence, Button button, float at, float rippleStrength)
    {
        RectTransform rect = (RectTransform)button.transform;
        int index = System.Array.IndexOf(mainItems.rects, rect);
        sequence.Insert(at, rect.DOAnchorPos(mainItems.positions[index], buttonDropSeconds)
            .SetEase(Ease.InQuad));
        sequence.Insert(at, rect.DOScale(mainItems.scales[index], buttonDropSeconds * 0.65f)
            .SetEase(Ease.OutCubic));
        float impact = at + buttonDropSeconds;
        sequence.Insert(impact, rect.DOPunchRotation(Vector3.forward * 3.4f, 0.22f, 5, 0.4f));
        sequence.Insert(impact, rect.DOPunchScale(mainItems.scales[index] * 0.055f, 0.20f, 4, 0.35f));
        AddBorderImpact(sequence, impact, rect, rippleStrength);
        return impact;
    }

    private void AddBorderImpact(Sequence sequence, float impact, RectTransform source, float strength)
    {
        sequence.InsertCallback(impact, () =>
        {
            if (borderRipple != null) borderRipple.Impact(source, strength);
            PlayIntroImpact(source == mainLogo ? introLogoImpactVolume : introButtonImpactVolume);
        });
    }

    private void PlayIntroImpact(float volume)
    {
        AudioManager manager = AudioManager.Instance;
        if (volume <= 0f || manager == null || manager.EffectsMuted || manager.EffectsVolume <= 0f)
            return;
        SertaoAudioLibrary library = Resources.Load<SertaoAudioLibrary>("SertaoAudioLibrary");
        if (library == null || library.effects == null) return;
        foreach (AudioManager.CueEntry entry in library.effects)
        {
            if (entry == null || entry.cue != introImpactCue || entry.clips == null || entry.clips.Length == 0)
                continue;
            AudioClip clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
            if (clip == null) return;
            if (introImpactSource == null)
            {
                introImpactSource = gameObject.AddComponent<AudioSource>();
                introImpactSource.playOnAwake = false;
                introImpactSource.ignoreListenerPause = true;
                introImpactSource.spatialBlend = 0f;
            }
            introImpactSource.PlayOneShot(clip, Mathf.Clamp01(entry.volume * volume * manager.EffectsVolume));
            return;
        }
    }

    private void Update() => borderRipple?.Tick();

    [ContextMenu("Testar ondulação em Play Mode")]
    private void PreviewBorderImpact()
    {
        if (Application.isPlaying) borderRipple?.Impact(mainLogo, 1f);
    }

    private void LateUpdate()
    {
        if (resizeBackgrounds && !slidingPages) LayoutPageBackgrounds();
        if (waitingForPauseReveal)
        {
            if (!PauseIrisTransition.ReturningToMenu)
            {
                waitingForPauseReveal = false;
                AnimateMenuEntrance(true);
            }
            return;
        }
        if (advancingIntro)
        {
            // Um quadro demorado de carregamento não pode pular o fade e as batidas.
            float step = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            motion.ManualUpdate(step, step);
            return;
        }
        if (!floating || mainItems == null) return;
        float time = Time.unscaledTime - floatStarted;
        float strength = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.9f));
        for (int i = 0; i < mainItems.rects.Length; i++)
        {
            RectTransform rect = mainItems.rects[i];
            if (rect == null) continue;
            float phase = i * 1.37f;
            float noise = Mathf.PerlinNoise(time * 0.37f + i, 0.65f) - 0.5f;
            float height = (Mathf.Sin(time * (1.32f + i * 0.11f) + phase) * 0.7f + noise * 0.6f);
            float angle = (Mathf.Sin(time * (1.08f + i * 0.17f) - phase) * 0.7f + noise * 0.6f);
            rect.anchoredPosition = mainItems.positions[i] + Vector2.up * (height * floatingHeight * strength);
            rect.localRotation = mainItems.rotations[i] *
                Quaternion.AngleAxis(angle * floatingAngle * strength, Vector3.forward);
        }
    }

    private void RestoreMainItems()
    {
        if (mainItems == null) return;
        for (int i = 0; i < mainItems.rects.Length; i++)
        {
            RectTransform rect = mainItems.rects[i];
            if (rect == null) continue;
            rect.anchoredPosition = mainItems.positions[i];
            rect.localRotation = mainItems.rotations[i];
        }
    }

    private void SetLocked(bool value)
    {
        if (floating && value) RestoreMainItems();
        locked = value;
        floating = !value && current == Page.Main &&
            mainPage != null && mainPage.gameObject.activeInHierarchy;
        if (floating) floatStarted = Time.unscaledTime;
        if (value)
        {
            if (inputShield == null && root != null)
            {
                var shield = new GameObject("Bloqueio de entrada", typeof(RectTransform), typeof(Image));
                inputShield = shield.GetComponent<Image>();
                RectTransform rect = inputShield.rectTransform;
                rect.SetParent(root.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                inputShield.color = Color.clear;
                inputShield.canvasRenderer.cullTransparentMesh = false;
                inputShield.raycastTarget = true;
            }

            if (inputShield != null)
            {
                inputShield.transform.SetAsLastSibling();
                inputShield.gameObject.SetActive(true);
            }
            BlockNavigation();
        }
        else
        {
            if (inputShield != null) inputShield.gameObject.SetActive(false);
            RestoreNavigation();
            if (current == Page.Leaderboard && leaderboardPage != null &&
                leaderboardPage.TryGetComponent(out GS2LeaderboardView ranking))
                ranking.RevealRows();
        }
    }

    private void BlockNavigation()
    {
        EventSystem system = EventSystem.current;
        if (system == null || system == blockedEventSystem) return;
        RestoreNavigation();
        blockedEventSystem = system;
        previousNavigation = system.sendNavigationEvents;
        system.sendNavigationEvents = false;
        system.SetSelectedGameObject(null);
    }

    private void RestoreNavigation()
    {
        if (blockedEventSystem != null)
            blockedEventSystem.sendNavigationEvents = previousNavigation;
        blockedEventSystem = null;
    }

#if UNITY_EDITOR
    internal GameObject EditableNicknamePage()
    {
        if (nicknamePage?.Root != null) return nicknamePage.Root;
        if (root == null || optionsPage == null ||
            !optionsPage.TryGetComponent(out GS2Profile profile))
            return null;
        UnityEditor.Undo.RecordObject(this, "Criar tela de nickname");
        nicknamePage = profile.CreateFirstRunPage(root.transform);
        if (nicknamePage != null)
            UnityEditor.Undo.RegisterCreatedObjectUndo(nicknamePage.Root, "Criar tela de nickname");
        return nicknamePage?.Root;
    }
#endif

    private void CompleteFirstRun(System.Action hideNickname)
    {
        SetLocked(true);
        void ShowMenu()
        {
            hideNickname();
            SetInitialDestination();
            if (current == Page.Main) PrepareMainDrop();
        }

        if (sceneFade != null)
            sceneFade.TransitionFromNickname(ShowMenu, () => AnimateMenuEntrance(true));
        else
        {
            ShowMenu();
            AnimateMenuEntrance();
        }
    }

    private void SetInitialDestination()
    {
        bool showRanking = returnToRanking && leaderboardPage != null && leaderboardGroup != null;
        returnToRanking = false;
        current = showRanking ? Page.Leaderboard : Page.Main;
        if (showRanking) leaderboardBackPage = Page.Main;
        SetInitialPage(mainPage, mainGroup, !showRanking);
        SetInitialPage(leaderboardPage, leaderboardGroup, showRanking);
        SetSeparateLogoVisible(!showRanking);
    }

    private void AnimateMenuEntrance(bool alreadyRevealed = false)
    {
        SetLocked(true);
        motion?.Kill();
        if (current != Page.Main)
        {
            motion = DOTween.Sequence().SetUpdate(true);
            AddPageReveal(motion);
            motion.OnComplete(() => SetLocked(false));
            return;
        }
        if (root != null) root.alpha = 1f;
        if (!alreadyRevealed && PauseIrisTransition.ReturningToMenu)
        {
            PrepareMainDrop();
            waitingForPauseReveal = true;
            return;
        }

        motion = DOTween.Sequence().SetUpdate(UpdateType.Manual, true).Pause();
        float dropBegins = 0f;
        if (!alreadyRevealed)
        {
            ShowIntroCurtain();
            introCurtain.alpha = 1f;
            if (sceneFade != null) sceneFade.ReleaseInitialMenuOverlay();
            dropBegins = introDarkHoldSeconds + introRevealSeconds;
            motion.Insert(introDarkHoldSeconds,
                introCurtain.DOFade(0f, introRevealSeconds).SetEase(Ease.InOutSine));
        }
        AddMainDrop(motion, dropBegins);
        motion.OnComplete(() =>
        {
            advancingIntro = false;
            RemoveIntroCurtain();
            SetLocked(false);
        });
        advancingIntro = true;
        motion.Play();
    }

    internal void AddEntranceTweens(Sequence sequence)
    {
        if (current == Page.Main && mainPage != null && mainPage.gameObject.activeSelf)
        {
            if (root != null) root.alpha = 1f;
            PrepareMainDrop();
            return;
        }
        AddPageReveal(sequence);
    }

    private void AddPageReveal(Sequence sequence)
    {
        RectTransform page = PageRect(current);
        if (page != null) page.localScale = Vector3.one * enteringScale;
        if (root != null) sequence.Insert(0f, root.DOFade(1f, fadeSeconds));
        if (page != null)
            sequence.Insert(0f, page.DOScale(1f, fadeSeconds).SetEase(Ease.OutCubic));
    }

    internal void CompleteMatchReturn()
    {
        if (current == Page.Main && mainPage != null && mainPage.gameObject.activeSelf)
            AnimateMenuEntrance(true);
        else
            SetLocked(false);
    }

    private void ShowIntroCurtain()
    {
        if (introCurtain != null) return;
        var canvasObject = new GameObject("Preto da entrada do menu", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        Canvas contentCanvas = root != null ? root.GetComponentInParent<Canvas>() : null;
        if (contentCanvas != null) canvas.targetDisplay = contentCanvas.rootCanvas.targetDisplay;
        introCurtain = canvasObject.GetComponent<CanvasGroup>();
        introCurtain.alpha = 1f;

        var fill = new GameObject("Preto", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransform rect = fill.rectTransform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fill.color = Color.black;
        fill.raycastTarget = false;
        FullscreenBackdrop.Cover(fill);
    }

    private void RemoveIntroCurtain()
    {
        if (introCurtain == null) return;
        Destroy(introCurtain.gameObject);
        introCurtain = null;
    }

    private void OnDisable() => borderRipple?.ResetRipple();

    private void OnDestroy()
    {
        motion?.Kill();
        RemoveIntroCurtain();
        RestoreNavigation();
        if (playButton != null) playButton.onClick.RemoveListener(Play);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
        if (mainLeaderboardButton != null) mainLeaderboardButton.onClick.RemoveListener(OpenLeaderboard);
        if (exitButton != null) exitButton.onClick.RemoveListener(Exit);
        if (optionsBackButton != null) optionsBackButton.onClick.RemoveListener(Back);
        if (leaderboardButton != null) leaderboardButton.onClick.RemoveListener(OpenLeaderboard);
        if (leaderboardBackButton != null) leaderboardBackButton.onClick.RemoveListener(Back);
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    public void Play()
    {
        if (locked || root == null || sceneFade == null ||
            !Application.CanStreamedLevelBeLoaded("PrototypeLayout")) return;
        SetLocked(true);
        motion?.Kill();
        sceneFade.EnterGameplay("PrototypeLayout");
    }

    public void OpenOptions() => SwitchTo(Page.Options);
    public void OpenLeaderboard()
    {
        if (locked || current == Page.Leaderboard) return;
        leaderboardBackPage = current;
        SwitchTo(Page.Leaderboard);
    }

    public void Back() => SwitchTo(current == Page.Leaderboard ? leaderboardBackPage : Page.Main);

    private void SwitchTo(Page destination)
    {
        if (locked || current == destination) return;
        RectTransform from = PageRect(current);
        RectTransform to = PageRect(destination);
        CanvasGroup fromGroup = PageGroup(current);
        CanvasGroup toGroup = PageGroup(destination);
        if (from == null || to == null || fromGroup == null || toGroup == null) return;
        AudioManager.Play(SoundCue.UiPaper);
        SetLocked(true);
        Canvas.ForceUpdateCanvases();
        LayoutPageBackgrounds();
        float distance = SlideDistance(from, to);
        bool back = destination == Page.Main ||
            (current == Page.Leaderboard && destination == Page.Options);
        float entryX = back ? -distance : distance;
        to.gameObject.SetActive(true);
        int mainSibling = -1;
        int logoSibling = -1;
        if (destination == Page.Main)
        {
            SetSeparateLogoVisible(true);
            mainSibling = RaiseAbove(to, from);
            if (separateLogo) logoSibling = RaiseAbove(mainLogo, to);
        }
        bool slideBackgrounds = destination == Page.Main || destination == Page.Options;
        Image outgoingImage = slideBackgrounds ? BackgroundFor(current) : null;
        RectTransform outgoingBackground = outgoingImage != null ? outgoingImage.rectTransform : null;
        float outgoingBackgroundX = outgoingBackground != null ? outgoingBackground.anchoredPosition.x : 0f;
        if (destination != Page.Options)
        {
            to.anchoredPosition = slideBackgrounds ? Vector2.zero : new Vector2(entryX, 0f);
            to.localScale = slideBackgrounds ? Vector3.one : Vector3.one * enteringScale;
        }
        toGroup.alpha = slideBackgrounds ? 1f : 0f;
        toGroup.interactable = true;
        toGroup.blocksRaycasts = false;
        fromGroup.blocksRaycasts = false;
        motion?.Kill();
        slidingPages = true;
        motion = DOTween.Sequence().SetUpdate(true);
        bool leavingOptions = current == Page.Options;
        float fromX = leavingOptions ? from.anchoredPosition.x : 0f;
        motion.Join(from.DOAnchorPosX(fromX - entryX, slideSeconds).SetEase(Ease.InOutCubic));
        if (current == Page.Main && separateLogo && mainLogo != null)
            motion.Insert(0f, mainLogo.DOAnchorPosX(mainLogo.anchoredPosition.x - entryX,
                slideSeconds).SetEase(Ease.InOutCubic));
        RectMask2D optionsMask = null;
        if (destination == Page.Options)
        {
            Canvas.ForceUpdateCanvases();
            optionsMask = AnimateOptionsWave(to, optionsBackground, distance, motion, slideSeconds,
                optionsBackground != null ? optionsBackground.rectTransform : null);
        }
        else if (destination == Page.Leaderboard)
        {
            motion.Join(to.DOAnchorPosX(0f, slideSeconds).SetEase(Ease.OutCubic));
            motion.Join(to.DOScale(1f, slideSeconds).SetEase(Ease.OutCubic));
            motion.Join(toGroup.DOFade(1f, slideSeconds * 0.85f));
        }
        if (leavingOptions) optionsMask = EnableFrameMask(from, optionsBackground, motion);
        if (slideBackgrounds)
            AnimateBackgrounds(from, outgoingBackground, outgoingBackgroundX,
                BackgroundFor(destination), entryX, destination == Page.Main ? to : null);
        if (optionsMask != null)
        {
            FitMaskToFrame(optionsMask, optionsBackground);
            motion.OnUpdate(() => FitMaskToFrame(optionsMask, optionsBackground));
        }
        motion.OnComplete(() =>
            {
                from.gameObject.SetActive(false);
                SetHorizontalPosition(from, fromX);
                if (outgoingBackground != null) SetHorizontalPosition(outgoingBackground, outgoingBackgroundX);
                if (logoSibling >= 0 && mainLogo != null) mainLogo.SetSiblingIndex(logoSibling);
                if (mainSibling >= 0) to.SetSiblingIndex(mainSibling);
                slidingPages = false;
                if (destination != Page.Main)
                {
                    SetSeparateLogoVisible(false);
                    RestoreMainItems();
                }
                toGroup.blocksRaycasts = true;
                current = destination;
                SetLocked(false);
            });
    }

    private static int RaiseAbove(Transform visual, Transform reference)
    {
        if (visual == null || visual.parent == null) return -1;
        while (reference != null && reference.parent != visual.parent)
            reference = reference.parent;
        if (reference == null) return -1;
        int position = visual.GetSiblingIndex();
        int referencePosition = reference.GetSiblingIndex();
        if (position >= referencePosition) return -1;
        visual.SetSiblingIndex(referencePosition);
        return position;
    }

    private Image BackgroundFor(Page page) => page == Page.Main ? mainPageBackground :
        page == Page.Options ? optionsBackground : leaderboardBackground;

    private void AnimateBackgrounds(RectTransform from, RectTransform outgoing, float outgoingX,
        Image incomingImage, float entryX, RectTransform incomingPage)
    {
        RectTransform incoming = incomingImage != null ? incomingImage.rectTransform : null;
        Vector3 pageTravel = from.parent.TransformVector(Vector3.right * entryX);
        Vector3 backgroundTravel = pageTravel;
        if (outgoing != null && incoming != null && root != null && root.transform is RectTransform viewport)
        {
            Bounds oldBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, outgoing);
            Bounds newBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, incoming);
            float direction = Mathf.Sign(entryX);
            // As bordas se encontram mesmo quando a onda viaja mais longe que os fundos.
            float edgeToEdge = direction > 0f ? oldBounds.max.x - newBounds.min.x :
                newBounds.max.x - oldBounds.min.x;
            if (edgeToEdge > 1f)
                backgroundTravel = viewport.TransformVector(Vector3.right * direction * (edgeToEdge - 1f));
            float correction = outgoing.parent.InverseTransformVector(pageTravel - backgroundTravel).x;
            motion.Insert(0f, outgoing.DOAnchorPosX(outgoingX + correction, slideSeconds).SetEase(Ease.InOutCubic));
        }

        SlideIn(incomingPage != null ? incomingPage : incoming, backgroundTravel);
        if (incomingPage != null && separateLogo) SlideIn(mainLogo, backgroundTravel);
    }

    private void SlideIn(RectTransform item, Vector3 travel)
    {
        if (item == null || item.parent == null) return;
        float homeX = item.anchoredPosition.x;
        SetHorizontalPosition(item, homeX + item.parent.InverseTransformVector(travel).x);
        motion.Insert(0f, item.DOAnchorPosX(homeX, slideSeconds).SetEase(Ease.InOutCubic));
    }

    private RectTransform PageRect(Page page) => page == Page.Main ? mainPage :
        page == Page.Options ? optionsPage : leaderboardPage;

    private CanvasGroup PageGroup(Page page) => page == Page.Main ? mainGroup :
        page == Page.Options ? optionsGroup : leaderboardGroup;

    private void SetInitialPage(RectTransform page, CanvasGroup group, bool active)
    {
        if (page == null) return;
        if (page != optionsPage)
        {
            page.anchoredPosition = Vector2.zero;
            page.localScale = Vector3.one;
        }
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = active;
        }
        page.gameObject.SetActive(active);
    }

    private float SlideDistance(RectTransform from, RectTransform to)
    {
        RectTransform viewport = root.transform as RectTransform;
        float viewportWidth = viewport != null ? viewport.rect.width : 0f;
        return Mathf.Max(1100f, (viewportWidth + Mathf.Max(from.rect.width, to.rect.width)) * 0.5f + 32f);
    }

    internal static PageItems CaptureItems(RectTransform page, RectTransform background = null)
    {
        if (page == null) return null;
        var rects = new System.Collections.Generic.List<RectTransform>(page.childCount);
        foreach (Transform child in page)
            if (child is RectTransform rect && rect != background) rects.Add(rect);
        rects.Sort((a, b) =>
            b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        return CaptureItems(rects.ToArray());
    }

    private static PageItems CaptureItems(RectTransform[] rects)
    {
        var items = new PageItems
        {
            rects = rects,
            positions = new Vector2[rects.Length],
            scales = new Vector3[rects.Length],
            rotations = new Quaternion[rects.Length]
        };
        for (int i = 0; i < rects.Length; i++)
        {
            items.positions[i] = rects[i].anchoredPosition;
            items.scales[i] = rects[i].localScale;
            items.rotations[i] = rects[i].localRotation;
        }
        return items;
    }

    internal static RectMask2D AnimateOptionsWave(RectTransform page, Image frame,
        float distance, Sequence sequence, float duration, RectTransform background = null)
    {
        PageItems items = CaptureItems(page, background);
        if (items == null || items.rects.Length == 0) return null;
        RectMask2D mask = EnableFrameMask(page, frame, sequence);
        Rect inner = mask != null ? FitMaskToFrame(mask, frame) : default;
        // No menu principal a moldura desliza separadamente dos controles.
        RectTransform viewport = background != null && mask != null
            ? frame.canvas.rootCanvas.transform as RectTransform : null;
        var visibility = new CanvasGroup[items.rects.Length];
        var restingAlpha = new float[items.rects.Length];

        // A página já começou a deslizar quando os primeiros controles aparecem.
        float waveStart = duration * 0.5f;
        for (int i = 0; i < items.rects.Length; i++)
        {
            RectTransform item = items.rects[i];
            if (!item.TryGetComponent(out CanvasGroup group))
                group = item.gameObject.AddComponent<CanvasGroup>();
            visibility[i] = group;
            float alpha = restingAlpha[i] = group.alpha;
            group.alpha = 0f;
            float homeX = items.positions[i].x;
            float offset = mask != null ? OffsetOutsideFrame(item, frame.rectTransform, inner, Vector2.right).x :
                distance;
            if (viewport != null)
                offset = Mathf.Max(offset, OffsetOutsideFrame(item, viewport, viewport.rect, Vector2.right).x);
            SetHorizontalPosition(item, homeX + offset);
            float entryTime = waveStart + i * 0.035f;
            sequence.InsertCallback(entryTime, () => { if (group != null) group.alpha = alpha; });
            sequence.Insert(entryTime,
                item.DOAnchorPosX(homeX, duration).SetEase(Ease.OutCubic));
        }
        sequence.onKill += () =>
        {
            for (int i = 0; i < visibility.Length; i++)
            {
                if (visibility[i] != null) visibility[i].alpha = restingAlpha[i];
                if (items.rects[i] != null) SetHorizontalPosition(items.rects[i], items.positions[i].x);
            }
        };

        return mask;
    }

    private static RectMask2D EnableFrameMask(RectTransform page, Image frame, Sequence sequence)
    {
        if (frame == null || frame.canvas == null || frame.overrideSprite == null) return null;
        bool existed = page.TryGetComponent(out RectMask2D mask);
        if (!existed) mask = page.gameObject.AddComponent<RectMask2D>();
        bool maskWasEnabled = existed && mask.enabled;
        Vector4 previousPadding = mask.padding;
        mask.enabled = true;
        sequence.SetAutoKill(true).OnKill(() =>
        {
            if (mask == null) return;
            mask.enabled = maskWasEnabled;
            mask.padding = previousPadding;
        });
        return mask;
    }

    internal static void SetHorizontalPosition(RectTransform rect, float x)
    {
        Vector2 position = rect.anchoredPosition;
        position.x = x;
        rect.anchoredPosition = position;
    }

    internal static Rect FitMaskToFrame(RectMask2D mask, Image frame)
    {
        RectTransform frameRect = frame.rectTransform;
        Rect inner = frame.GetPixelAdjustedRect();
        // Distâncias até o papel no sprite 9SlicedBox: esquerda, baixo, direita e cima.
        Vector4 paperInset = new Vector4(112f, 116f, 99f, 97f);
        float unitsPerPixel = 1f / (frame.pixelsPerUnit * frame.pixelsPerUnitMultiplier);
        Vector4 slice = frame.overrideSprite.border;
        float xUnits = Mathf.Min(unitsPerPixel, inner.width / Mathf.Max(1f, slice.x + slice.z));
        float yUnits = Mathf.Min(unitsPerPixel, inner.height / Mathf.Max(1f, slice.y + slice.w));
        inner.xMin += paperInset.x * xUnits;
        inner.yMin += paperInset.y * yUnits;
        inner.xMax -= paperInset.z * xUnits;
        inner.yMax -= paperInset.w * yUnits;

        Transform canvas = frame.canvas.rootCanvas.transform;
        Vector3 min = canvas.InverseTransformPoint(frameRect.TransformPoint(inner.min));
        Vector3 max = canvas.InverseTransformPoint(frameRect.TransformPoint(inner.max));
        Rect outer = mask.canvasRect;
        mask.padding = new Vector4(min.x - outer.xMin, min.y - outer.yMin,
            outer.xMax - max.x, outer.yMax - max.y);
        return inner;
    }

    internal static Vector2 OffsetOutsideFrame(RectTransform item, RectTransform frame,
        Rect inner, Vector2 direction)
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(frame, item);
        float distance = direction.y < 0f ? bounds.max.y - inner.yMin :
            direction.x < 0f ? bounds.max.x - inner.xMin : inner.xMax - bounds.min.x;
        return item.parent.InverseTransformVector(frame.TransformVector(direction * Mathf.Max(0f, distance + 2f)));
    }

    public void Exit()
    {
        if (locked || root == null || blackFade == null) return;
        SetLocked(true);
        motion?.Kill();
        blackFade.blocksRaycasts = true;
        blackFade.DOKill();
        motion = DOTween.Sequence().SetUpdate(true)
            .Append(blackFade.DOFade(1f, fadeSeconds))
            .OnComplete(() =>
            {
#if UNITY_EDITOR || UNITY_WEBGL
#if UNITY_WEBGL && !UNITY_EDITOR
                if (statusText != null) statusText.text = "Para sair, feche a aba do navegador.";
#else
                if (statusText != null) statusText.text = "SAIR funciona na versão publicada.";
#endif
                blackFade.DOFade(0f, fadeSeconds).SetUpdate(true).OnComplete(() =>
                {
                    blackFade.blocksRaycasts = false;
                    SetLocked(false);
                });
#else
                Application.Quit();
#endif
            });
    }

    [System.Serializable]
    private sealed class BorderRipple : MainMenuAnimationSettings.RippleSettings
    {
        private static readonly int SpriteUvId = Shader.PropertyToID("_SpriteUV");
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int SizeClockId = Shader.PropertyToID("_SizeClock");
        private static readonly int ImageOriginId = Shader.PropertyToID("_ImageOrigin");
        private static readonly int InnerLinesId = Shader.PropertyToID("_InnerLines");
        private static readonly int LineHalfWidthId = Shader.PropertyToID("_LineHalfWidth");
        private static readonly int[] ImpactIds =
        {
            Shader.PropertyToID("_ImpactA"),
            Shader.PropertyToID("_ImpactB"),
            Shader.PropertyToID("_ImpactC")
        };

        private Image background;
        private Material originalMaterial;
        private Material waveMaterial;
        private Shader defaultShader;
        private Sprite boundSprite;
        private float elapsed;
        private int nextImpact;
        private bool shaderWarningShown;

        internal void Bind(Image image)
        {
            if (background == image) return;
            ResetRipple();
            background = image;
        }

        internal void Impact(RectTransform source, float strength)
        {
            if (!enabled || background == null || !background.isActiveAndEnabled ||
                !ReadyMaterial()) return;

            Rect drawing = BackgroundRect();
            Vector2 position = drawing.center;
            if (source != null)
                position = (Vector2)background.rectTransform.InverseTransformPoint(
                    source.TransformPoint(source.rect.center));
            Vector2 origin = new Vector2(
                Mathf.InverseLerp(drawing.xMin, drawing.xMax, position.x),
                Mathf.InverseLerp(drawing.yMin, drawing.yMax, position.y));
            waveMaterial.SetVector(ImpactIds[nextImpact], new Vector4(origin.x, origin.y, elapsed, strength));
            nextImpact = (nextImpact + 1) % ImpactIds.Length;
            ApplySettings(drawing);
        }

        internal void ResetRipple()
        {
            if (waveMaterial != null)
            {
                if (background != null && background.material == waveMaterial)
                    background.material = originalMaterial;
                UnityEngine.Object.Destroy(waveMaterial);
            }
            waveMaterial = null;
            originalMaterial = null;
            boundSprite = null;
            elapsed = 0f;
            nextImpact = 0;
        }

        private bool ReadyMaterial()
        {
            Sprite sprite = background.overrideSprite;
            if (sprite == null || (sprite.packed && sprite.packingRotation != SpritePackingRotation.None))
                return false;
            if (waveMaterial == null)
            {
                if (rippleShader == null && defaultShader == null)
                    defaultShader = Resources.Load<Shader>("SertaoMainMenuBorderRipple");
                Shader activeShader = rippleShader != null ? rippleShader : defaultShader;
                if (activeShader == null || !activeShader.isSupported)
                {
                    if (!shaderWarningShown)
                    {
                        shaderWarningShown = true;
                        Debug.LogWarning("Confira a importação do shader SertaoMainMenuBorderRipple.", background);
                    }
                    return false;
                }
                originalMaterial = background.material;
                waveMaterial = new Material(activeShader)
                {
                    name = "Sertão - onda da moldura do menu",
                    hideFlags = HideFlags.DontSave
                };
                background.material = waveMaterial;
            }
            if (boundSprite != sprite)
            {
                waveMaterial.SetVector(SpriteUvId, DataUtility.GetOuterUV(sprite));
                boundSprite = sprite;
            }
            return true;
        }

        private Rect BackgroundRect()
        {
            Rect rect = background.GetPixelAdjustedRect();
            if (background.type == Image.Type.Simple && background.preserveAspect && boundSprite != null)
            {
                Vector2 pixels = boundSprite.rect.size;
                Vector2 pivot = background.rectTransform.pivot;
                float scale = Mathf.Min(rect.width / pixels.x, rect.height / pixels.y);
                Vector2 size = pixels * scale;
                rect.position += Vector2.Scale(rect.size - size, pivot);
                rect.size = size;
            }
            return rect;
        }

        private void ApplySettings(Rect rect)
        {
            waveMaterial.SetVector(WaveId, new Vector4(wavesPerLine, impactAmplitude, idleAmplitude, idleSpeed));
            float left = leftLineInset > 0f ? leftLineInset : DefaultLeftLineInset;
            float right = rightLineInset > 0f ? rightLineInset : DefaultRightLineInset;
            waveMaterial.SetVector(InnerLinesId, new Vector4(left, right, bottomLineInset, topLineInset));
            waveMaterial.SetFloat(LineHalfWidthId, lineHalfWidth);
            waveMaterial.SetVector(ImageOriginId, new Vector4(rect.xMin, rect.yMin, 0f, 0f));
            waveMaterial.SetVector(SizeClockId, new Vector4(
                Mathf.Max(1f, rect.width), Mathf.Max(1f, rect.height), elapsed, 0f));
        }

        internal void Tick()
        {
            if (waveMaterial == null) return;
            if (!enabled)
            {
                ResetRipple();
                return;
            }
            if (background == null || !background.isActiveAndEnabled) return;
            if (!ReadyMaterial())
            {
                ResetRipple();
                return;
            }
            // O menu roda com timeScale zero; usa a mesma proteção contra saltos da entrada.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            ApplySettings(BackgroundRect());
        }
    }
}
