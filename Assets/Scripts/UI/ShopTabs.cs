using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Apresentacao apenas. Precos, estoque, compra, venda e pedidos continuam nos sistemas existentes.
public sealed class ShopTabs : MonoBehaviour
{
    [SerializeField] private CanvasGroup buyPage;
    [SerializeField] private CanvasGroup sellPage;
    [SerializeField] private CanvasGroup ordersPage;
    [SerializeField] private Button buyTab;
    [SerializeField] private Button sellTab;
    [SerializeField] private Button ordersTab;
    [SerializeField] private ShopSellPanel sellPanel;
    [Header("Saldo e destaque da aba atual (somente visual)")]
    [SerializeField] private ResourceBank resources;
    [SerializeField] private Text coinsAvailableText;
    [SerializeField] private Image buyTabHighlight;
    [SerializeField] private Image sellTabHighlight;
    [SerializeField] private Image ordersTabHighlight;

    [Header("Area visivel interna (borda azul)")]
    [SerializeField] private RectTransform animationViewport;
    [SerializeField, Min(0f)] private float outsideMargin = 12f;

    [Header("Blocos completos")]
    [SerializeField] private RectTransform[] buyRows;
    [SerializeField] private RectTransform[] sellRows;
    [SerializeField] private RectTransform[] orderTextBlocks;
    [SerializeField] private RectTransform orderDetails;
    [SerializeField] private RectTransform[] buyFooter;
    [SerializeField] private RectTransform[] sellFooter;
    [SerializeField] private RectTransform[] ordersFooter;

    [Header("Ritmo geral da mercearia")]
    [SerializeField, Range(0.5f, 4f),
        Tooltip("Multiplica a velocidade de TODAS as animacoes internas. 1 = tempos individuais normais; 2 = duas vezes mais rapido. Nao altera os valores individuais.")]
    private float overallAnimationSpeed = 1.4f;
    [SerializeField, Min(0.05f),
        Tooltip("Tempo maximo, em segundos reais, entre o inicio da primeira e da ultima linha da lista. Evita que Vender demore demais por ter muitos itens.")]
    private float maxRowWaveSpan = 0.52f;
    [SerializeField, Range(0f, 1f),
        Tooltip("Quando iniciar o footer durante a entrada da ultima linha: 0 = junto do inicio; 1 = apos a ultima linha terminar. Atraso adicional: Footer Start Delay.")]
    private float footerAfterLastRow = 0.3f;

    [Header("Entrada dos blocos")]
    [SerializeField, Min(0.1f)] private float rowDuration = 0.64f;
    [SerializeField, Min(0f)] private float rowWaveInterval = 0.2f;
    [SerializeField, Min(0.1f)] private float footerDuration = 0.48f;
    [SerializeField, Min(0.01f)] private float headerWaveInterval = 0.14f;
    [SerializeField, Min(0.1f)] private float headerDuration = 0.55f;
    [SerializeField, Min(0f)] private float contentStartDelay = 0.14f;
    [SerializeField, Min(0f)] private float footerStartDelay = 0.1f;

    [Header("Troca lateral de encomendas")]
    [SerializeField, Min(0.1f)] private float orderSwapDuration = 0.25f;

    private enum OutsideEdge { Left, Right, Top, Bottom }
    private readonly Dictionary<RectTransform, Vector2> homePositions = new();
    private CanvasGroup selectedPage;
    private Sequence headerSequence;
    private Sequence pageSequence;
    private Sequence orderSequence;
    private bool shopOpen;

    private float Fast(float duration) =>
        Mathf.Max(0f, duration) / Mathf.Max(0.5f,
            overallAnimationSpeed > 0f ? overallAnimationSpeed : 1.4f);

    private void OnEnable()
    {
        if (resources != null) resources.Changed += RefreshCoins;
        RefreshCoins();
        UpdateTabHighlight();
    }

    private void RefreshCoins()
    {
        if (resources != null && coinsAvailableText != null)
            coinsAvailableText.text = resources.Coins.ToString();
    }

    private void UpdateTabHighlight()
    {
        if (buyTabHighlight != null) buyTabHighlight.enabled = selectedPage == buyPage;
        if (sellTabHighlight != null) sellTabHighlight.enabled = selectedPage == sellPage;
        if (ordersTabHighlight != null) ordersTabHighlight.enabled = selectedPage == ordersPage;
    }

    private void Awake()
    {
        Cache(buyTab != null ? buyTab.transform as RectTransform : null);
        Cache(sellTab != null ? sellTab.transform as RectTransform : null);
        Cache(ordersTab != null ? ordersTab.transform as RectTransform : null);
        Cache(buyRows);
        Cache(sellRows);
        Cache(orderTextBlocks);
        Cache(orderDetails);
        Cache(buyFooter);
        Cache(sellFooter);
        Cache(ordersFooter);
    }

    // O painel ainda esta descendo/subindo: todos os componentes animados ficam fora do recorte.
    public void ShowDefault(bool marketOpen)
    {
        shopOpen = false;
        StopAnimations();
        if (buyTab != null) buyTab.interactable = marketOpen;
        if (sellTab != null) sellTab.interactable = marketOpen;
        Select(marketOpen ? buyPage : ordersPage, false);
        PrepareForEntrance();
    }

    public void ShowBuy() => Select(buyPage);

    public void ShowSell()
    {
        if (sellPanel != null) sellPanel.RefreshDisplay();
        Select(sellPage);
    }

    public void ShowOrders() => Select(ordersPage);

    // Chamado APOS a janela terminar de chegar ao centro.
    public void OnShopOpened(float pageDelay = 0f)
    {
        shopOpen = true;
        AnimateHeader();
        // Respeita a ordem visual: primeiro a janela, depois as abas e entao o conteudo.
        // O tempo extra e reduzido pelo ritmo geral, sem alterar as configuracoes salvas.
        float delay = Mathf.Max(0f, pageDelay) + Fast(contentStartDelay);
        if (headerSequence != null && headerSequence.IsActive())
        {
            headerSequence.OnComplete(() =>
            {
                headerSequence = null;
                if (shopOpen) AnimatePage(selectedPage, delay);
            });
        }
        else AnimatePage(selectedPage, delay);
    }

    public void OnShopClosed()
    {
        shopOpen = false;
        StopAnimations();
    }

    public bool AnimateOrderChange(Action advance)
    {
        if (!shopOpen || selectedPage != ordersPage || orderDetails == null || advance == null)
            return false;
        if (orderSequence != null && orderSequence.IsActive()) return true;

        pageSequence?.Kill();
        pageSequence = null;
        RestorePagePositions();
        RectTransform details = orderDetails;
        Vector2 home = Home(details);
        float exitX = OffsetOutside(details, OutsideEdge.Right).x;
        float enterX = OffsetOutside(details, OutsideEdge.Left).x;
        orderSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        orderSequence.Append(details.DOAnchorPosX(home.x + exitX, Fast(orderSwapDuration))
            .SetEase(Ease.InOutSine));
        orderSequence.AppendCallback(() =>
        {
            // O conteudo e atualizado depois da saida, nunca na frente do jogador.
            details.anchoredPosition = home;
            advance();
            if (details != null)
                details.anchoredPosition = home + new Vector2(enterX, 0f);
        });
        orderSequence.Append(details.DOAnchorPos(home, Fast(orderSwapDuration * 1.4f))
            .SetEase(Ease.OutCubic));
        orderSequence.OnComplete(() =>
        {
            if (details != null) details.anchoredPosition = home;
            orderSequence = null;
        });
        return true;
    }

    private void Select(CanvasGroup page, bool playSound = true)
    {
        if (playSound) AudioManager.Play(SoundCue.PanelOpen);
        StopAnimations();
        selectedPage = page;
        SetPage(buyPage, page == buyPage);
        SetPage(sellPage, page == sellPage);
        SetPage(ordersPage, page == ordersPage);
        UpdateTabHighlight();
        if (shopOpen && playSound) AnimatePage(page, 0f);
    }

    private static void SetPage(CanvasGroup page, bool active)
    {
        if (page == null) return;
        page.alpha = active ? 1f : 0f;
        page.interactable = active;
        page.blocksRaycasts = active;
    }

    private void PrepareForEntrance()
    {
        RectTransform[] tabs = TabTransforms();
        foreach (RectTransform tab in tabs) MoveOutside(tab, OutsideEdge.Top);
        PreparePage(selectedPage);
    }

    private void PreparePage(CanvasGroup page)
    {
        if (page == buyPage)
        {
            MoveOutside(buyRows, OutsideEdge.Left);
            MoveOutside(buyFooter, OutsideEdge.Bottom);
        }
        else if (page == sellPage)
        {
            MoveOutside(sellRows, OutsideEdge.Left);
            MoveOutside(sellFooter, OutsideEdge.Bottom);
        }
        else if (page == ordersPage)
        {
            MoveOutside(orderTextBlocks, OutsideEdge.Left);
            MoveOutside(ordersFooter, OutsideEdge.Bottom);
        }
    }

    private void AnimateHeader()
    {
        headerSequence?.Kill();
        headerSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        RectTransform[] tabs = TabTransforms();
        for (int i = 0; i < tabs.Length; i++)
            Enter(tabs[i], OutsideEdge.Top, Fast(headerDuration),
                i * Fast(headerWaveInterval), headerSequence);
    }

    private void AnimatePage(CanvasGroup page, float delay)
    {
        if (page == null) return;
        pageSequence?.Kill();
        pageSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        float footerStart;
        if (page == buyPage)
        {
            footerStart = AnimateRows(buyRows, delay);
            AnimateFooter(buyFooter, footerStart + Fast(footerStartDelay));
        }
        else if (page == sellPage)
        {
            // Cada linha inteira (imagem, quantidade, preco e botoes) usa UM tween.
            footerStart = AnimateRows(sellRows, delay);
            AnimateFooter(sellFooter, footerStart + Fast(footerStartDelay));
        }
        else if (page == ordersPage)
        {
            footerStart = AnimateRows(orderTextBlocks, delay);
            AnimateFooter(ordersFooter, footerStart + Fast(footerStartDelay));
        }
    }

    // Retorna quando o footer pode COMEÇAR (nao quando a ultima linha termina).
    // A ultima linha ja deve estar entrando, mas nao precisa terminar por inteiro.
    private float AnimateRows(RectTransform[] rows, float start)
    {
        if (rows == null) return start;
        int count = 0;
        foreach (RectTransform row in rows)
            if (row != null) count++;
        if (count == 0) return start;

        float duration = Fast(rowDuration);
        float interval = count > 1
            ? Mathf.Min(Fast(rowWaveInterval),
                Mathf.Max(0.05f, maxRowWaveSpan > 0f ? maxRowWaveSpan : 0.52f) / (count - 1))
            : 0f;
        int index = 0;
        foreach (RectTransform row in rows)
        {
            if (row == null) continue;
            Enter(row, OutsideEdge.Left, duration,
                start + index * interval, pageSequence);
            index++;
        }
        float lastRowStart = start + (count - 1) * interval;
        return lastRowStart + duration * Mathf.Clamp01(footerAfterLastRow);
    }

    private void AnimateFooter(RectTransform[] items, float start)
    {
        if (items == null) return;
        // Botao(es) e total entram no MESMO momento, como um unico bloco visual.
        foreach (RectTransform item in items)
            Enter(item, OutsideEdge.Bottom, Fast(footerDuration), start, pageSequence);
    }

    private void Enter(RectTransform element, OutsideEdge edge, float duration,
        float start, Sequence sequence)
    {
        if (element == null || sequence == null) return;
        element.DOKill();
        Vector2 home = Home(element);
        element.anchoredPosition = home + OffsetOutside(element, edge);
        sequence.Insert(start, element.DOAnchorPos(home, duration)
            .SetEase(Ease.OutCubic));
    }

    private void MoveOutside(RectTransform[] elements, OutsideEdge edge)
    {
        if (elements == null) return;
        foreach (RectTransform element in elements) MoveOutside(element, edge);
    }

    private void MoveOutside(RectTransform element, OutsideEdge edge)
    {
        if (element == null) return;
        element.DOKill();
        element.anchoredPosition = Home(element);
        element.anchoredPosition += OffsetOutside(element, edge);
    }

    // Mede a distancia REAL ate o limite do viewport (borda azul), e nao um
    // deslocamento arbitrario de 20/30/70 pixels que deixa o objeto aparecendo.
    private Vector2 OffsetOutside(RectTransform element, OutsideEdge edge)
    {
        RectTransform viewport = animationViewport != null
            ? animationViewport : transform as RectTransform;
        if (viewport == null || element == null) return Vector2.zero;
        // A primeira abertura ja preparou os elementos FORA da janela.
        // Medir os Bounds nessa posicao calcula um deslocamento proximo de zero
        // e faz a entrada "pular" para dentro, sem animacao perceptivel.
        // Sempre medir os limites na posicao de repouso (home), inclusive
        // para os filhos do bloco (icone de moeda, texto, botoes, etc.).
        Vector2 current = element.anchoredPosition;
        Bounds bounds;
        try
        {
            element.anchoredPosition = Home(element);
            bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, element);
        }
        finally
        {
            element.anchoredPosition = current;
        }
        Rect clip = viewport.rect;
        float margin = outsideMargin + 2f;
        return edge switch
        {
            OutsideEdge.Left => new Vector2(clip.xMin - bounds.max.x - margin, 0f),
            OutsideEdge.Right => new Vector2(clip.xMax - bounds.min.x + margin, 0f),
            OutsideEdge.Top => new Vector2(0f, clip.yMax - bounds.min.y + margin),
            _ => new Vector2(0f, clip.yMin - bounds.max.y - margin)
        };
    }

    private RectTransform[] TabTransforms() => new[]
    {
        buyTab != null ? buyTab.transform as RectTransform : null,
        sellTab != null ? sellTab.transform as RectTransform : null,
        ordersTab != null ? ordersTab.transform as RectTransform : null
    };

    private void Cache(RectTransform[] elements)
    {
        if (elements == null) return;
        foreach (RectTransform element in elements) Cache(element);
    }

    private void Cache(RectTransform element)
    {
        if (element != null && !homePositions.ContainsKey(element))
            homePositions.Add(element, element.anchoredPosition);
    }

    private Vector2 Home(RectTransform element)
    {
        if (!homePositions.TryGetValue(element, out Vector2 home))
        {
            home = element.anchoredPosition;
            homePositions.Add(element, home);
        }
        return home;
    }

    private void RestorePagePositions()
    {
        Restore(buyRows);
        Restore(sellRows);
        Restore(orderTextBlocks);
        Restore(buyFooter);
        Restore(sellFooter);
        Restore(ordersFooter);
        Restore(orderDetails);
    }

    private void Restore(RectTransform[] elements)
    {
        if (elements == null) return;
        foreach (RectTransform element in elements) Restore(element);
    }

    private void Restore(RectTransform element)
    {
        if (element == null) return;
        element.DOKill();
        element.anchoredPosition = Home(element);
    }

    private void StopAnimations()
    {
        headerSequence?.Kill();
        headerSequence = null;
        pageSequence?.Kill();
        pageSequence = null;
        orderSequence?.Kill();
        orderSequence = null;
        Restore(TabTransforms());
        RestorePagePositions();
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= RefreshCoins;
        shopOpen = false;
        StopAnimations();
        DOTween.Kill(this);
    }
}
