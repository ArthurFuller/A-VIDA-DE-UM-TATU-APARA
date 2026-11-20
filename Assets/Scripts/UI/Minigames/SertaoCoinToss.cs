using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SertaoCoinToss : MonoBehaviour
{
    [SerializeField] private CanvasGroup screen;
    [SerializeField] private CanvasGroup choices;
    [SerializeField] private RectTransform stage, coin, turn;
    [SerializeField] private RawImage face;
    [SerializeField] private Button heads, tails;
    [SerializeField] private Text stakeLabel, title;
    [SerializeField] private Texture2D headsArt, tailsArt;
    [SerializeField] private Color winColor = new Color(0.99f, 0.95f, 0.79f);
    [SerializeField] private Color lossColor = new Color(1f, 0.82f, 0.67f);

    private static SertaoCoinToss active;
    public static bool IsOpen => active != null;

    private Texture2D headsTexture, tailsTexture;
    private TopBarAutoHide topBar;
    private Tween intro;
    private Sequence idle, toss;
    private Action began;
    private Action<bool> completed;
    private Vector2 stageHome, coinHome;
    private Vector3 coinHomeScale, turnHomeScale;
    private Quaternion turnHomeRotation;
    private float angle, airtime, verticalFlip;
    private int stake;
    private bool picked, temporaryInstance;
    private GameObject previousSelection;
    private HoverPunchButton headsFeedback, tailsFeedback;

    private bool IsConfigured => screen != null && choices != null && stage != null &&
        coin != null && turn != null && face != null && heads != null && tails != null &&
        stakeLabel != null && title != null;

    protected void Awake()
    {
        if (!IsConfigured) return;
        FullscreenBackdrop.Cover(screen.GetComponentInChildren<Image>(true));
        stageHome = stage.anchoredPosition;
        coinHome = coin.anchoredPosition;
        coinHomeScale = coin.localScale;
        turnHomeScale = turn.localScale;
        turnHomeRotation = turn.localRotation;
        screen.alpha = 0f;
        screen.interactable = screen.blocksRaycasts = false;
        heads.onClick.AddListener(PickHeads);
        tails.onClick.AddListener(PickTails);
        SertaoButtonAudio.Bind(heads);
        SertaoButtonAudio.Bind(tails);
        headsFeedback = HoverPunchButton.Bind(heads);
        tailsFeedback = HoverPunchButton.Bind(tails);
        headsFeedback.AnimatePointerClick = tailsFeedback.AnimatePointerClick = false;
    }

    public static SertaoCoinToss Open(Scene scene, int stake, float airtime, Action began, Action<bool> completed)
    {
        if (IsOpen || EventSystem.current == null) return null;

        SertaoCoinToss view = null;
        foreach (var candidate in FindObjectsByType<SertaoCoinToss>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.temporaryInstance || candidate.gameObject.scene != scene || !candidate.enabled ||
                (candidate.transform.parent != null && !candidate.transform.parent.gameObject.activeInHierarchy) ||
                !candidate.IsConfigured) continue;
            view = candidate;
            break;
        }

        if (view == null)
        {
            var prefab = Resources.Load<GameObject>("SertaoCaraOuCoroa");
            if (prefab == null || !prefab.TryGetComponent(out SertaoCoinToss template) ||
                !template.IsConfigured) return null;
            view = Instantiate(template);
            view.temporaryInstance = true;
            if (scene.IsValid() && view.gameObject.scene != scene)
                SceneManager.MoveGameObjectToScene(view.gameObject, scene);
        }

        var assets = SertaoPresentationAssets.Current;
        view.headsTexture = view.headsArt != null ? view.headsArt : assets != null ? assets.Heads : null;
        view.tailsTexture = view.tailsArt != null ? view.tailsArt : assets != null ? assets.Tails : null;
        if (view.headsTexture == null || view.tailsTexture == null)
        {
            if (view.temporaryInstance) Destroy(view.gameObject);
            return null;
        }

        active = view;
        view.Setup(stake, airtime, began, completed);
        return view;
    }

    private void Setup(int value, float duration, Action start, Action<bool> finish)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        foreach (var candidate in FindObjectsByType<TopBarAutoHide>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != gameObject.scene) continue;
            topBar = candidate;
            break;
        }
        topBar?.SetCoinTossOpen(true);
        stake = value;
        airtime = Mathf.Clamp(duration + 0.42f, 0.95f, 1.7f);
        began = start;
        completed = finish;
        picked = false;
        verticalFlip = 0f;
        stage.anchoredPosition = stageHome;
        coin.anchoredPosition = coinHome;
        coin.localScale = coinHomeScale;
        choices.alpha = 1f;
        choices.interactable = choices.blocksRaycasts = true;
        screen.alpha = 0f;
        screen.interactable = screen.blocksRaycasts = true;
        stakeLabel.text = "Aposta de " + stake + " moedas";
        title.enabled = false;
        face.enabled = true;

        DrawFace(0f);
        intro = screen.DOFade(1f, 0.18f).SetLink(gameObject);
        idle = DOTween.Sequence().SetLink(gameObject)
            .AppendInterval(0.28f)
            .Append(DOVirtual.Float(0f, 180f, 0.33f, DrawFace).SetEase(Ease.InOutCubic))
            .AppendInterval(0.26f)
            .Append(DOVirtual.Float(180f, 360f, 0.33f, DrawFace).SetEase(Ease.InOutCubic))
            .SetLoops(-1);
        previousSelection = EventSystem.current.currentSelectedGameObject;
        tails.Select();
    }

    private void PickHeads() => Pick(true);
    private void PickTails() => Pick(false);

    private void Pick(bool selectedHeads)
    {
        if (picked) return;
        picked = true;
        (selectedHeads ? headsFeedback : tailsFeedback)?.PlayAcceptedClick();
        idle.Kill();
        choices.interactable = choices.blocksRaycasts = false;
        EventSystem.current?.SetSelectedGameObject(null);
        began?.Invoke();

        bool headsUp = UnityEngine.Random.value < 0.5f;
        bool won = headsUp == selectedHeads;
        float target = (Mathf.Floor(angle / 360f) + 4f) * 360f + (headsUp ? 0f : 180f);
        var arc = DOTween.Sequence()
            .Append(coin.DOAnchorPosY(coinHome.y + 157f, airtime * 0.44f).SetEase(Ease.OutQuad))
            .Append(coin.DOAnchorPosY(coinHome.y, airtime * 0.56f).SetEase(Ease.InQuad));
        toss = DOTween.Sequence().SetLink(gameObject)
            .Append(choices.DOFade(0f, 0.12f))
            .Join(coin.DOAnchorPosY(coinHome.y - 34f, 0.13f).SetEase(Ease.InQuad))
            .Join(DOVirtual.Float(0f, 1f, 0.13f, FlipVertically).SetEase(Ease.InOutSine))
            .AppendCallback(() => AudioManager.Play(SoundCue.GambleSpin))
            .Append(arc)
            .Join(DOVirtual.Float(angle, target, airtime, DrawFace).SetEase(Ease.OutSine))
            .AppendCallback(() => Impact(headsUp, won, target))
            .Append(coin.DOAnchorPosY(coinHome.y + 18f, 0.085f).SetEase(Ease.OutQuad))
            .AppendCallback(() => AudioManager.Play(won ? SoundCue.GambleWin : SoundCue.GambleLose))
            .Append(coin.DOAnchorPosY(coinHome.y, 0.12f).SetEase(Ease.InQuad))
            .AppendInterval(0.52f)
            .Append(screen.DOFade(0f, 0.17f))
            .OnComplete(() => Finish(won));
    }

    private void Impact(bool headsUp, bool won, float settled)
    {
        DrawFace(settled);
        title.text = (headsUp ? "Deu cara" : "Deu coroa") + "\n" + (won ? "+" : "-") + stake + " moedas";
        title.enabled = true;
        title.color = won ? winColor : lossColor;
        coin.DOPunchScale(Vector3.one * 0.11f, 0.19f, 4, 0.45f).SetLink(gameObject);
        stage.DOShakeAnchorPos(0.17f, new Vector2(7f, 4f), 8, 55f).SetLink(gameObject);
        SertaoCoinImpact.Play();
        AudioManager.Play(SoundCue.GambleLand);
    }

    private void FlipVertically(float amount)
    {
        verticalFlip = amount;
        DrawFace(angle);
    }

    private void DrawFace(float degrees)
    {
        angle = degrees;
        float phase = angle * Mathf.Deg2Rad;
        float width = Mathf.Cos(phase);
        Texture2D nextFace = width >= 0f ? headsTexture : tailsTexture;
        if (face.texture != nextFace) face.texture = nextFace;
        float thickness = Mathf.Max(0.075f, Mathf.Abs(width));
        turn.localScale = Vector3.Scale(turnHomeScale, new Vector3(Mathf.Lerp(thickness, 1f, verticalFlip),
            Mathf.Lerp(1f, thickness, verticalFlip), 1f));
        turn.localRotation = turnHomeRotation * Quaternion.Euler(0f, 0f, -4f * Mathf.Sin(phase));
    }

    private void Finish(bool won)
    {
        var callback = completed;
        completed = null;
        toss = null;
        gameObject.SetActive(false);
        if (temporaryInstance) Destroy(gameObject);
        callback?.Invoke(won);
    }

    protected void OnDisable()
    {
        intro?.Kill();
        idle?.Kill();
        toss?.Kill();
        if (active != this) return;

        stage.DOKill();
        coin.DOKill();
        active = null;
        topBar?.SetCoinTossOpen(false);
        topBar = null;
        if (previousSelection != null && previousSelection.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
        began = null;
        completed = null;
    }

    protected void OnDestroy()
    {
        if (heads != null) heads.onClick.RemoveListener(PickHeads);
        if (tails != null) tails.onClick.RemoveListener(PickTails);
    }
}
