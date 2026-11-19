using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GS2LeaderboardView : MonoBehaviour
{
    [SerializeField] private Text status;
    [SerializeField] private RectTransform rowTemplate;
    [SerializeField, Min(0)] private float rowGap = 8f;
    [SerializeField, Min(1)] private float visibleHeight = 165f;

    private RectTransform content;
    private ScrollRect scroll;
    private RectMask2D viewportMask;
    private Image pageFrame;
    private readonly List<(RectTransform Rect, CanvasGroup Fade, float Alpha)> pendingRows = new();
    private Sequence rowEntrance;
    private bool pageRevealed;
    private int loadVersion;

    private void Awake()
    {
        if (rowTemplate == null) return;
        var viewport = new GameObject("Recorte dos resultados", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(transform, false);
        viewportMask = viewport.GetComponent<RectMask2D>();
        float rowHeight = rowTemplate.rect.height;
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = rowTemplate.anchoredPosition -
            Vector2.up * ((visibleHeight - rowHeight) * 0.5f);
        viewport.sizeDelta = new Vector2(rowTemplate.rect.width, visibleHeight);
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        content = new GameObject("Resultados", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;

        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        rowTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject row = content.GetChild(i).gameObject;
            row.SetActive(false);
            Destroy(row);
        }
        scroll.StopMovement();
        content.sizeDelta = new Vector2(0f, visibleHeight);
        content.anchoredPosition = Vector2.zero;
        if (status != null) status.text = "Carregando ranking...";
        int request = ++loadVersion;
        GS2LeaderboardOnline.Instance.Load((entries, error) =>
        {
            if (this != null && isActiveAndEnabled && request == loadVersion)
                ShowEntries(entries, error);
        });
    }

    private void OnDisable()
    {
        loadVersion++;
        pageRevealed = false;
        pageFrame = null;
        pendingRows.Clear();
        rowEntrance?.Kill();
    }

    internal void RevealRows(Image frame)
    {
        pageFrame = frame;
        pageRevealed = true;
        AnimateRows();
    }

    private void ShowEntries(List<GS2LeaderboardOnline.Entry> entries, string error)
    {
        if (entries == null)
        {
            if (status != null) status.text = error;
            if (GS2LeaderboardScore.HasLocalScore)
                AddRow("—", GS2Profile.HasNickname ? GS2Profile.Nickname : "Jogador",
                    GS2LeaderboardScore.LocalBestCoins.ToString(), 0);
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
                AddRow((i + 1).ToString(), entries[i].nickname, entries[i].coins.ToString(), i);
            if (status != null) status.text = entries.Count == 0 ? "Nenhuma partida no ranking." : string.Empty;
        }
        int count = entries == null ? (GS2LeaderboardScore.HasLocalScore ? 1 : 0) : entries.Count;
        float rowHeight = rowTemplate.rect.height;
        content.sizeDelta = new Vector2(0, Mathf.Max(visibleHeight,
            count * rowHeight + Mathf.Max(0, count - 1) * rowGap));
        scroll.verticalNormalizedPosition = 1f;
        if (pageRevealed) AnimateRows();
    }

    private void AddRow(string position, string nickname, string coins, int index)
    {
        RectTransform row = Instantiate(rowTemplate, content);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
        row.pivot = new Vector2(0.5f, 0.5f);
        float restingY = -row.rect.height * 0.5f - index * (row.rect.height + rowGap);
        row.anchoredPosition = new Vector2(0f, restingY);
        row.GetChild(0).GetComponent<Text>().text = position;
        row.GetChild(1).GetComponent<Text>().text = nickname;
        row.GetChild(2).GetComponent<Text>().text = coins;

        CanvasGroup fade = row.GetComponent<CanvasGroup>();
        if (fade == null) fade = row.gameObject.AddComponent<CanvasGroup>();
        float restingAlpha = fade.alpha;
        fade.alpha = 0f;
        row.gameObject.SetActive(true);
        pendingRows.Add((row, fade, restingAlpha));
    }

    private void AnimateRows()
    {
        if (pendingRows.Count == 0) return;
        Canvas.ForceUpdateCanvases();

        Image frame = pageFrame;
        bool useFrame = frame != null && frame.canvas != null &&
            frame.type == Image.Type.Sliced && frame.overrideSprite != null;
        Vector4 listPadding = viewportMask.padding;
        RectTransform edge = useFrame ? frame.rectTransform : scroll.viewport;
        // The frame clips the slide horizontally; the list keeps its vertical scroll bounds.
        Rect inner = useFrame ? GS2FrontEnd.FitMaskToFrame(viewportMask, frame, fitHeight: false) : edge.rect;

        rowEntrance = DOTween.Sequence().SetUpdate(true).SetAutoKill(true);
        for (int i = 0; i < pendingRows.Count; i++)
        {
            var (row, fade, alpha) = pendingRows[i];
            float restingX = row.anchoredPosition.x;
            Vector2 offset = GS2FrontEnd.OffsetOutsideFrame(row, edge, inner, Vector2.right);
            row.anchoredPosition += offset;
            float delay = Mathf.Min(i, 4) * 0.075f;
            rowEntrance.Insert(delay, row.DOAnchorPosX(restingX, 0.44f).SetEase(Ease.OutCubic));
            rowEntrance.Insert(delay, fade.DOFade(alpha, 0.24f).SetEase(Ease.OutSine));
        }
        pendingRows.Clear();
        if (useFrame)
            rowEntrance.OnUpdate(() =>
            {
                if (frame != null) GS2FrontEnd.FitMaskToFrame(viewportMask, frame, fitHeight: false);
            });
        rowEntrance.OnKill(() =>
        {
            if (viewportMask != null) viewportMask.padding = listPadding;
            rowEntrance = null;
        });
    }
}
