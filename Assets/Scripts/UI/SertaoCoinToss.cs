using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SertaoCoinToss : MonoBehaviour
{
    private static readonly Rect FaceCrop = new Rect(629f / 2048f, 699f / 2048f, 932f / 2048f, 932f / 2048f);
    private static readonly Color Paper = new Color(0.99f, 0.95f, 0.79f);
    private static SertaoCoinToss active;
    public static bool IsOpen => active != null;

    private SertaoPresentationAssets art;
    private RectTransform stage, coin, turn;
    private CanvasGroup screen, choices;
    private RawImage face;
    private Button heads, tails;
    private Text title;
    private Sequence idle, toss;
    private Action began;
    private Action<bool> completed;
    private float angle, airtime;
    private int stake;
    private bool picked;
    private GameObject previousSelection;

    public static SertaoCoinToss Open(Scene scene, int stake, float airtime, Action began, Action<bool> completed)
    {
        var assets = SertaoPresentationAssets.Current;
        if (IsOpen || assets == null || assets.Heads == null || assets.Tails == null || EventSystem.current == null)
            return null;
        var root = new GameObject("Cara ou coroa", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(SertaoCoinToss));
        if (scene.IsValid() && root.scene != scene) SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 1f;
        active = root.GetComponent<SertaoCoinToss>();
        active.Setup(assets, stake, airtime, began, completed);
        return active;
    }

    private void Setup(SertaoPresentationAssets assets, int value, float duration, Action start, Action<bool> finish)
    {
        art = assets;
        stake = value;
        airtime = Mathf.Clamp(duration + 0.42f, 0.95f, 1.7f);
        began = start;
        completed = finish;
        screen = GetComponent<CanvasGroup>();
        screen.alpha = 0f;
        var scrim = MakeRect("Fundo", transform, Vector2.zero, Vector2.zero);
        scrim.anchorMin = Vector2.zero;
        scrim.anchorMax = Vector2.one;
        scrim.sizeDelta = Vector2.zero;
        scrim.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.085f, 0.048f, 0.74f);

        stage = MakeRect("Centro", transform, Vector2.zero, Vector2.zero);
        var buttons = MakeRect("Escolha", stage, Vector2.zero, Vector2.zero);
        choices = buttons.gameObject.AddComponent<CanvasGroup>();
        Label(buttons, "Aposta de " + stake + " moedas", 197f, 23);
        tails = Choice(buttons, "Coroa", 120f, false);
        heads = Choice(buttons, "Cara", -120f, true);
        tails.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = heads };
        heads.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = tails };

        coin = MakeRect("Moeda", stage, Vector2.zero, Vector2.zero);
        turn = MakeRect("Giro", coin, Vector2.zero, new Vector2(140f, 140f));
        face = turn.gameObject.AddComponent<RawImage>();
        face.uvRect = FaceCrop;
        face.raycastTarget = false;
        title = Label(stage, "", -120f, 27);
        title.enabled = false;

        DrawFace(0f);
        screen.DOFade(1f, 0.18f).SetLink(gameObject);
        idle = DOTween.Sequence().SetLink(gameObject)
            .AppendInterval(0.28f)
            .Append(DOVirtual.Float(0f, 180f, 0.33f, DrawFace).SetEase(Ease.InOutCubic))
            .AppendInterval(0.26f)
            .Append(DOVirtual.Float(180f, 360f, 0.33f, DrawFace).SetEase(Ease.InOutCubic))
            .SetLoops(-1);
        previousSelection = EventSystem.current.currentSelectedGameObject;
        tails.Select();
    }

    private void Pick(bool selectedHeads)
    {
        if (picked) return;
        picked = true;
        idle.Kill();
        choices.interactable = choices.blocksRaycasts = false;
        EventSystem.current?.SetSelectedGameObject(null);
        began?.Invoke();
        AudioManager.Play(SoundCue.GambleSpin);

        bool headsUp = UnityEngine.Random.value < 0.5f;
        bool won = headsUp == selectedHeads;
        float target = (Mathf.Floor(angle / 360f) + 4f) * 360f + (headsUp ? 0f : 180f);
        var arc = DOTween.Sequence()
            .Append(coin.DOAnchorPosY(157f, airtime * 0.44f).SetEase(Ease.OutQuad))
            .Append(coin.DOAnchorPosY(0f, airtime * 0.56f).SetEase(Ease.InQuad));
        toss = DOTween.Sequence().SetLink(gameObject)
            .Append(choices.DOFade(0f, 0.12f))
            .Join(coin.DOAnchorPosY(-34f, 0.13f).SetEase(Ease.InQuad))
            .Append(arc)
            .Join(DOVirtual.Float(angle, target, airtime, DrawFace).SetEase(Ease.OutSine))
            .AppendCallback(() => Impact(headsUp, won, target))
            .Append(coin.DOAnchorPosY(18f, 0.085f).SetEase(Ease.OutQuad))
            .Append(coin.DOAnchorPosY(0f, 0.12f).SetEase(Ease.InQuad))
            .AppendInterval(0.52f)
            .Append(screen.DOFade(0f, 0.17f))
            .OnComplete(() => Finish(won));
    }

    private void Impact(bool headsUp, bool won, float settled)
    {
        DrawFace(settled);
        title.text = (headsUp ? "Deu cara" : "Deu coroa") + "\n" + (won ? "+" : "-") + stake + " moedas";
        title.enabled = true;
        title.color = won ? Paper : new Color(1f, 0.82f, 0.67f);
        coin.DOPunchScale(Vector3.one * 0.11f, 0.19f, 4, 0.45f).SetLink(gameObject);
        stage.DOShakeAnchorPos(0.17f, new Vector2(7f, 4f), 8, 55f).SetLink(gameObject);
        SertaoCoinImpact.Play();
        AudioManager.Play(won ? SoundCue.GambleWin : SoundCue.GambleLose);
    }

    private void DrawFace(float degrees)
    {
        angle = degrees;
        float phase = angle * Mathf.Deg2Rad;
        float width = Mathf.Cos(phase);
        Texture2D nextFace = width >= 0f ? art.Heads : art.Tails;
        if (face.texture != nextFace) face.texture = nextFace;
        turn.localScale = new Vector3(Mathf.Max(0.075f, Mathf.Abs(width)), 1f, 1f);
        turn.localRotation = Quaternion.Euler(0f, 0f, -4f * Mathf.Sin(phase));
    }

    private void Finish(bool won)
    {
        var callback = completed;
        completed = null;
        if (active == this) active = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
        if (previousSelection != null && previousSelection.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        callback?.Invoke(won);
    }

    private void OnDestroy()
    {
        idle?.Kill();
        toss?.Kill();
        if (active == this) active = null;
    }

    private Button Choice(RectTransform parent, string value, float y, bool isHeads)
    {
        var target = MakeRect(value, parent, Vector2.up * y, new Vector2(218f, 50f));
        var background = target.gameObject.AddComponent<Image>();
        background.color = new Color(0.29f, 0.24f, 0.13f);
        var outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.74f, 0.63f, 0.33f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var states = button.colors;
        states.colorMultiplier = 1.32f;
        states.highlightedColor = states.selectedColor = Color.white;
        states.normalColor = new Color(0.76f, 0.76f, 0.76f, 1f);
        states.pressedColor = new Color(0.61f, 0.57f, 0.48f);
        button.colors = states;
        Label(target, value, 0f, 26);
        button.onClick.AddListener(() => Pick(isHeads));
        return button;
    }

    private Text Label(Transform parent, string value, float y, int size)
    {
        var label = MakeRect("Texto", parent, Vector2.up * y, new Vector2(330f, 72f))
            .gameObject.AddComponent<Text>();
        label.font = art.Font != null ? art.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = value;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Paper;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform MakeRect(string label, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(label, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
