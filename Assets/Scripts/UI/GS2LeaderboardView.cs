using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GS2LeaderboardView : MonoBehaviour
{
    [SerializeField] private Text status;
    [SerializeField] private RectTransform rowTemplate;
    [SerializeField] private Image resultsPanel;
    [SerializeField, Min(0)] private float rowGap = 8f;
    [SerializeField, Min(1)] private float visibleHeight = 165f;

    private sealed class Row
    {
        public RectTransform Rect;
        public Text Position;
        public Text Nickname;
        public Text Coins;
        public CanvasGroup Fade;
        public float Alpha;
    }

    private readonly List<Row> rows = new();
    private List<GS2LeaderboardOnline.Entry> entries;
    private RectTransform content;
    private ScrollRect scroll;
    private CanvasGroup[] pageGroups;
    private Sequence rowEntrance;
    private float restingX;
    private int firstBoundIndex = -1;
    private bool layoutDirty;
    private bool needsEntrance;
    private bool localFallback;
    private int loadVersion;

    private float RowHeight => rowTemplate.rect.height;
    private float RowStride => Mathf.Max(1f, RowHeight + rowGap);

    private void Awake()
    {
        if (rowTemplate == null) return;
        pageGroups = GetComponentsInParent<CanvasGroup>(true);
        if (resultsPanel == null)
        {
            // A cena existente nomeia o painel costurado de "Image".
            Transform panel = transform.Find("Image");
            if (panel != null) resultsPanel = panel.GetComponent<Image>();
        }

        var viewport = new GameObject("Recorte dos resultados", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(transform, false);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        Image input = viewport.GetComponent<Image>();
        input.color = Color.clear;
        input.raycastTarget = true;
        input.canvasRenderer.cullTransparentMesh = false;

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
        scroll.onValueChanged.AddListener(OnScrolled);
        rowTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (content == null) return;
        entries = null;
        needsEntrance = true;
        localFallback = false;
        UpdateViewport();
        scroll.StopMovement();
        content.anchoredPosition = Vector2.zero;
        RefreshRows(true);
        SetStatus("Carregando ranking...");
        StartCoroutine(LoadAfterFirstFrame(++loadVersion));
    }

    private IEnumerator LoadAfterFirstFrame(int request)
    {
        // Deixa o menu iniciar a transição antes de preparar os resultados.
        yield return null;
        if (!isActiveAndEnabled || request != loadVersion) yield break;

        if (Debug.isDebugBuild)
        {
            const int previewCount = 30;
            var preview = new List<GS2LeaderboardOnline.Entry>(previewCount);
            for (int i = 0; i < previewCount; i++)
                preview.Add(new GS2LeaderboardOnline.Entry
                {
                    nickname = $"Teste {i + 1:00}",
                    coins = 350 - i * 10
                });
            ShowEntries(preview, null);
            yield break;
        }

        GS2LeaderboardOnline.Instance.Load((result, error) =>
        {
            if (this != null && isActiveAndEnabled && request == loadVersion)
                ShowEntries(result, error);
        });
    }

    private void OnDisable()
    {
        loadVersion++;
        rowEntrance?.Kill();
    }

    private void OnRectTransformDimensionsChange() => layoutDirty = true;

    private void LateUpdate()
    {
        if (content == null) return;
        if (layoutDirty)
        {
            rowEntrance?.Complete();
            UpdateViewport();
            RefreshRows(true);
        }

        RevealRows();
    }

    internal void RevealRows()
    {
        if (!isActiveAndEnabled || !needsEntrance || entries == null || entries.Count == 0) return;
        foreach (CanvasGroup group in pageGroups)
            if (group != null && group.isActiveAndEnabled && (!group.blocksRaycasts || group.alpha < 0.99f))
                return;
        AnimateRows();
    }

    private void ShowEntries(List<GS2LeaderboardOnline.Entry> result, string error)
    {
        rowEntrance?.Complete();
        bool firstResult = entries == null;
        localFallback = result == null && GS2LeaderboardScore.HasLocalScore;
        entries = result;
        if (localFallback)
            entries = new List<GS2LeaderboardOnline.Entry>
            {
                new()
                {
                    nickname = GS2Profile.HasNickname ? GS2Profile.Nickname : "Jogador",
                    coins = GS2LeaderboardScore.LocalBestCoins
                }
            };

        SetStatus(result == null ? error : result.Count == 0 ? "Nenhuma partida no ranking." : "");
        if (firstResult)
        {
            scroll.StopMovement();
            content.anchoredPosition = Vector2.zero;
        }
        UpdateViewport();
        RefreshRows(true);
    }

    private void SetStatus(string message)
    {
        if (status == null) return;
        status.text = message;
        status.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void UpdateViewport()
    {
        layoutDirty = false;
        var parent = (RectTransform)transform;
        Vector2 center = parent.InverseTransformPoint(rowTemplate.TransformPoint(rowTemplate.rect.center));
        float top = center.y + RowHeight * 0.5f;
        var bounds = new Rect(center.x - rowTemplate.rect.width * 0.5f,
            top - visibleHeight, rowTemplate.rect.width, visibleHeight);

        if (resultsPanel != null)
        {
            Rect inner = resultsPanel.GetPixelAdjustedRect();
            float width = inner.width;
            float height = inner.height;
            // Margens do painel costurado, medidas até a borda interna.
            inner.xMin += width * 0.027f;
            inner.xMax -= width * 0.030f;
            inner.yMin += height * 0.057f;
            inner.yMax -= height * 0.070f;
            RectTransform panel = resultsPanel.rectTransform;
            Vector2 min = parent.InverseTransformPoint(panel.TransformPoint(inner.min));
            Vector2 max = parent.InverseTransformPoint(panel.TransformPoint(inner.max));
            // O primeiro resultado começa abaixo dos cabeçalhos fixos.
            float listTop = Mathf.Min(top, max.y);
            if (listTop > min.y && max.x > min.x)
                bounds = Rect.MinMaxRect(min.x, min.y, max.x, listTop);
        }

        scroll.viewport.sizeDelta = bounds.size;
        scroll.viewport.anchoredPosition = bounds.center - parent.rect.center;
        restingX = center.x - bounds.center.x;
        UpdateContentHeight();
        Vector2 offset = content.anchoredPosition;
        offset.y = Mathf.Clamp(offset.y, 0f, Mathf.Max(0f, content.rect.height - bounds.height));
        content.anchoredPosition = offset;
    }

    private void UpdateContentHeight()
    {
        int total = entries?.Count ?? 0;
        float height = total > 0 ? total * RowStride - rowGap : 0f;
        content.sizeDelta = new Vector2(0f, Mathf.Max(scroll.viewport.rect.height, height));
    }

    private void OnScrolled(Vector2 _)
    {
        if (rowEntrance != null && content.anchoredPosition.y > 0.01f)
            rowEntrance.Complete();
        RefreshRows();
    }

    private void RefreshRows(bool force = false)
    {
        int total = entries?.Count ?? 0;
        int first = Mathf.Max(0, Mathf.FloorToInt(content.anchoredPosition.y / RowStride));
        if (!force && firstBoundIndex == first) return;
        firstBoundIndex = first;
        int poolSize = Mathf.CeilToInt(scroll.viewport.rect.height / RowStride) + 1;
        int visible = Mathf.Clamp(total - first, 0, poolSize);
        while (rows.Count < visible) rows.Add(CreateRow());
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (i >= visible)
            {
                row.Rect.gameObject.SetActive(false);
                continue;
            }

            int index = first + i;
            GS2LeaderboardOnline.Entry entry = entries[index];
            row.Rect.anchoredPosition = new Vector2(restingX, -RowHeight * 0.5f - index * RowStride);
            row.Position.text = localFallback ? "—" : (index + 1).ToString();
            row.Nickname.text = entry.nickname;
            row.Coins.text = entry.coins.ToString();
            row.Fade.alpha = needsEntrance ? 0f : row.Alpha;
            row.Rect.gameObject.SetActive(true);
        }
    }

    private Row CreateRow()
    {
        RectTransform rect = Instantiate(rowTemplate, content);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        CanvasGroup fade = rect.GetComponent<CanvasGroup>();
        if (fade == null) fade = rect.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = false;
        return new Row
        {
            Rect = rect,
            Position = rect.GetChild(0).GetComponent<Text>(),
            Nickname = rect.GetChild(1).GetComponent<Text>(),
            Coins = rect.GetChild(2).GetComponent<Text>(),
            Fade = fade,
            Alpha = fade.alpha
        };
    }

    private void AnimateRows()
    {
        needsEntrance = false;
        RectTransform viewport = scroll.viewport;
        rowEntrance = DOTween.Sequence().SetUpdate(true).SetAutoKill(true);
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (!row.Rect.gameObject.activeSelf) continue;
            row.Rect.anchoredPosition += GS2FrontEnd.OffsetOutsideFrame(
                row.Rect, viewport, viewport.rect, Vector2.right);
            float delay = Mathf.Min(i, 4) * 0.075f;
            rowEntrance.Insert(delay, row.Rect.DOAnchorPosX(restingX, 0.44f).SetEase(Ease.OutCubic));
            rowEntrance.Insert(delay, row.Fade.DOFade(row.Alpha, 0.24f).SetEase(Ease.OutSine));
        }
        rowEntrance.OnKill(() => rowEntrance = null);
    }
}
