using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    [SerializeField] private Button exitButton;
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button leaderboardButton;
    [SerializeField] private Button leaderboardBackButton;
    [SerializeField] private Text statusText;
    [SerializeField, Min(0.1f)] private float slideSeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 0.36f;
    [SerializeField, Range(0.8f, 1f)] private float enteringScale = 0.975f;
    private Sequence motion;
    private PageItems mainItems;
    private PageItems optionsItems;
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
    }

    private void Awake()
    {
        Time.timeScale = 0f;
        bool returningFromMatch = SertaoMatchResultsView.BindReturningMenu(this, out returnToRanking);
        if (nicknamePage?.Root != null) nicknamePage.Root.SetActive(false);
        if (root != null) { root.alpha = 0f; root.interactable = true; root.blocksRaycasts = true; }
        SetInitialPage(mainPage, mainGroup, true);
        SetInitialPage(optionsPage, optionsGroup, false);
        SetInitialPage(leaderboardPage, leaderboardGroup, false);
        mainItems = CaptureItems(mainPage);
        if (optionsPage != null && optionsPage.TryGetComponent(out AudioSettingsUI audioSettings))
            audioSettings.PrepareWeatherControls();
        optionsItems = CaptureItems(optionsPage);
        if (mainPage != null) mainPage.localScale = Vector3.one * enteringScale;
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
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
        if ((previewNickname || !GS2Profile.HasNickname) && root != null && optionsPage != null &&
            optionsPage.TryGetComponent(out GS2Profile profile) &&
            profile.ShowFirstRun(nicknamePage, root.transform, CompleteFirstRun, previewNickname))
        {
            SetInitialPage(mainPage, mainGroup, false);
            SetLocked(true);
            if (!returningFromMatch)
                motion = DOTween.Sequence().SetUpdate(true)
                    .Append(root.DOFade(1f, fadeSeconds))
                    .OnComplete(() => SetLocked(false));
            return;
        }
        SetInitialDestination();
        if (!returningFromMatch) AnimateMainEntrance();
    }

    private void Start()
    {
        if (locked) BlockNavigation();
    }

    private void SetLocked(bool value)
    {
        locked = value;
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
        }

        if (sceneFade != null)
            sceneFade.TransitionFromNickname(ShowMenu, () => SetLocked(false));
        else
        {
            ShowMenu();
            AnimateMainEntrance();
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
    }

    private void AnimateMainEntrance()
    {
        SetLocked(true);
        motion = DOTween.Sequence().SetUpdate(true);
        AddEntranceTweens(motion);
        motion.OnComplete(() => SetLocked(false));
    }

    internal void AddEntranceTweens(Sequence sequence)
    {
        RectTransform page = PageRect(current);
        if (page != null) page.localScale = Vector3.one * enteringScale;
        if (root != null) sequence.Insert(0f, root.DOFade(1f, fadeSeconds));
        if (page != null)
            sequence.Insert(0f, page.DOScale(1f, fadeSeconds).SetEase(Ease.OutCubic));
    }

    internal void CompleteMatchReturn() => SetLocked(false);

    private void OnDestroy()
    {
        motion?.Kill();
        RestoreNavigation();
        if (playButton != null) playButton.onClick.RemoveListener(Play);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
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
        float distance = SlideDistance(from, to);
        bool back = destination == Page.Main ||
            (current == Page.Leaderboard && destination == Page.Options);
        float entryX = back ? -distance : distance;
        to.gameObject.SetActive(true);
        bool wave = destination == Page.Main || destination == Page.Options;
        to.anchoredPosition = wave ? Vector2.zero : new Vector2(entryX, 0f);
        to.localScale = wave ? Vector3.one : Vector3.one * enteringScale;
        toGroup.alpha = wave ? 1f : 0f;
        toGroup.interactable = true;
        toGroup.blocksRaycasts = false;
        fromGroup.blocksRaycasts = false;
        motion?.Kill();
        motion = DOTween.Sequence().SetUpdate(true);
        motion.Join(from.DOAnchorPosX(-entryX, slideSeconds).SetEase(Ease.InOutCubic));
        if (wave)
            AnimateWave(destination == Page.Main ? mainItems : optionsItems, entryX, motion, slideSeconds);
        else
        {
            motion.Join(to.DOAnchorPosX(0f, slideSeconds).SetEase(Ease.OutCubic));
            motion.Join(to.DOScale(1f, slideSeconds).SetEase(Ease.OutCubic));
            motion.Join(toGroup.DOFade(1f, slideSeconds * 0.85f));
        }
        motion.OnComplete(() =>
            {
                from.gameObject.SetActive(false);
                toGroup.blocksRaycasts = true;
                current = destination;
                SetLocked(false);
            });
    }

    private RectTransform PageRect(Page page) => page == Page.Main ? mainPage :
        page == Page.Options ? optionsPage : leaderboardPage;

    private CanvasGroup PageGroup(Page page) => page == Page.Main ? mainGroup :
        page == Page.Options ? optionsGroup : leaderboardGroup;

    private void SetInitialPage(RectTransform page, CanvasGroup group, bool active)
    {
        if (page == null) return;
        page.anchoredPosition = Vector2.zero;
        page.localScale = Vector3.one;
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

    internal static PageItems CaptureItems(RectTransform page)
    {
        if (page == null) return null;
        var items = new PageItems
        {
            rects = new RectTransform[page.childCount],
            positions = new Vector2[page.childCount],
            scales = new Vector3[page.childCount]
        };
        for (int i = 0; i < items.rects.Length; i++)
            items.rects[i] = page.GetChild(i) as RectTransform;
        System.Array.Sort(items.rects, (a, b) =>
            b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        for (int i = 0; i < items.rects.Length; i++)
        {
            items.positions[i] = items.rects[i].anchoredPosition;
            items.scales[i] = items.rects[i].localScale;
        }
        return items;
    }

    internal static void AnimateWave(PageItems items, float entryX, Sequence sequence, float duration)
    {
        if (items == null) return;
        for (int i = 0; i < items.rects.Length; i++)
        {
            RectTransform item = items.rects[i];
            item.anchoredPosition = items.positions[i] + Vector2.right * entryX;
            item.localScale = items.scales[i];
            float delay = i * 0.035f;
            sequence.Insert(delay, item.DOAnchorPos(items.positions[i], duration)
                .SetEase(Ease.OutCubic));
        }
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
}
