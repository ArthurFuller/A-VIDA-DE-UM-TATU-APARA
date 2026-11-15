using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveMissionsPanel : MonoBehaviour
{
    public event System.Action Closed;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private Text toggleText;
    [SerializeField] private ActiveMissionCard[] cards;
    [SerializeField] private Sprite[] itemIcons;
    [SerializeField] private DayTimeline timeline;
    [SerializeField] private SeedMerchant merchant;

    [Header("Layout da lista (editavel no Inspector)")]
    [SerializeField] private RectTransform listContent;
    [SerializeField, Min(0f), Tooltip("Espaco acima do primeiro card. Aumentar = descer todos os cards.")]
    private float firstCardTopPadding = 0f;
    [SerializeField, Min(0f), Tooltip("Distancia entre o fim de um card e o inicio do proximo.")]
    private float cardSpacing = 4f;
    [SerializeField, Min(1f), Tooltip("Altura compartilhada dos cards. A lista e a rolagem acompanham.")]
    private float cardHeight = 70f;
    [SerializeField, Min(0f)] private float listBottomPadding = 4f;

    [Header("Icones dos cards (editavel no Inspector)")]
    [SerializeField] private Vector2 itemIconSize = new Vector2(25f, 25f);
    [SerializeField, Tooltip("Posicao relativa ao canto superior esquerdo de cada card.")]
    private Vector2 itemIconPosition = new Vector2(8f, -27f);
    [SerializeField, Min(0f)] private float iconToTextGap = 6f;
    [SerializeField, Min(0f)] private float quantityRightMargin = 8f;

    [Header("Entrada da janela")]
    [SerializeField, Min(0.05f)] private float animationDuration = 0.36f;
    [SerializeField, Min(0f), Tooltip("Folga acima da area recortada antes de a janela descer.")]
    private float hiddenOffset = 24f;

    [Header("Entrada dos cards")]
    [SerializeField, Min(0.05f)] private float cardDropDuration = 0.26f;
    [SerializeField, Min(0f)] private float cardWaveInterval = 0.11f;
    [SerializeField, Min(0f)] private float cardDropDistance = 56f;

    private readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();
    private readonly List<RectTransform> cardRects = new List<RectTransform>();
    private readonly List<Vector2> cardPositions = new List<Vector2>();
    private bool isOpen;
    private Vector2 openPosition;

    public bool IsOpen => isOpen;

    private Vector2 HiddenPosition
    {
        get
        {
            if (contentRect == null) return openPosition;
            // Bring the ENTIRE window above the clipping area, rather than just 24 px.
            float distance = contentRect.rect.height + Mathf.Max(0f, -openPosition.y) + hiddenOffset;
            return openPosition + Vector2.up * distance;
        }
    }

    private void Awake()
    {
        if (contentRect != null) openPosition = contentRect.anchoredPosition;
        ApplyCardLayout();
        CacheCards();
        isOpen = false;
        if (contentRect != null) contentRect.anchoredPosition = HiddenPosition;
        if (contentGroup != null)
        {
            contentGroup.alpha = 1f; // No panel fade: visibility is controlled by the mask.
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;
        }
        UpdateToggleText();
    }

    public void Toggle()
    {
        if (!isOpen)
        {
            if (timeline != null) timeline.ClosePanel();
            if (merchant != null && merchant.IsOpen) merchant.HideShop();
        }
        isOpen = !isOpen;
        AnimateWindow();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        AnimateWindow();
    }

    private void AnimateWindow()
    {
        DOTween.Kill(this);
        if (contentRect != null) contentRect.DOKill();
        RestoreCards();
        UpdateToggleText();

        if (contentGroup != null)
        {
            contentGroup.alpha = 1f;
            contentGroup.interactable = isOpen;
            contentGroup.blocksRaycasts = false;
        }
        if (contentRect == null)
        {
            if (!isOpen) Closed?.Invoke();
            return;
        }

        if (isOpen)
        {
            PrepareCards();
            contentRect.anchoredPosition = HiddenPosition;
            contentRect.DOAnchorPos(openPosition, animationDuration)
                .SetEase(Ease.OutCubic).SetId(this)
                .OnComplete(() =>
                {
                    if (!isOpen) return;
                    if (contentGroup != null) contentGroup.blocksRaycasts = true;
                    AnimateCards();
                });
        }
        else
        {
            contentRect.DOAnchorPos(HiddenPosition, animationDuration)
                .SetEase(Ease.InCubic).SetId(this)
                .OnComplete(() => Closed?.Invoke());
        }
    }

    private void ApplyCardLayout()
    {
        if (cards == null) return;
        float height = Mathf.Max(1f, cardHeight);
        float spacing = Mathf.Max(0f, cardSpacing);
        float top = Mathf.Max(0f, firstCardTopPadding);
        int validCards = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            ActiveMissionCard card = cards[i];
            if (card == null) continue;
            RectTransform rect = card.transform as RectTransform;
            if (rect == null) continue;
            Vector2 position = rect.anchoredPosition;
            position.y = -top - validCards * (height + spacing);
            rect.anchoredPosition = position;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            card.ConfigureIconLayout(itemIconSize, itemIconPosition,
                iconToTextGap, quantityRightMargin, rect.rect.width);
            validCards++;
        }
        ResizeScrollContent(validCards);
    }

    private void ResizeScrollContent(int count)
    {
        if (listContent == null) return;
        float needed = Mathf.Max(0f, firstCardTopPadding) + Mathf.Max(0, count) *
            Mathf.Max(1f, cardHeight) + Mathf.Max(0, count - 1) *
            Mathf.Max(0f, cardSpacing) + Mathf.Max(0f, listBottomPadding);
        RectTransform viewport = listContent.parent as RectTransform;
        float viewportHeight = viewport != null ? viewport.rect.height : 0f;
        listContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(viewportHeight, needed));
    }

#if UNITY_EDITOR
    // Called by the temporary autosave after restoring values in Edit Mode.
    // Rebuilds the card positions before the scene is saved to disk.
    public void RefreshEditorLayoutAfterPlayMode()
    {
        if (Application.isPlaying) return;
        ApplyCardLayout();
    }

    private void OnValidate()
    {
        // O layout pode ser ajustado no Inspector durante o Play Mode.
        // O Editor agenda a atualização fora da desserialização de propriedades.
        UnityEditor.EditorApplication.delayCall -= PreviewLayoutInEditor;
        UnityEditor.EditorApplication.delayCall += PreviewLayoutInEditor;
    }

    private void PreviewLayoutInEditor()
    {
        if (this == null) return;

        bool runtimePreview = Application.isPlaying && cards != null &&
            cardRects.Count == cards.Length;
        if (runtimePreview)
        {
            // Só interrompe tweens dos cards enquanto o desenvolvedor está
            // ajustando o Inspector. A animação da janela permanece intacta.
            for (int i = 0; i < cardRects.Count; i++)
                if (cardRects[i] != null) cardRects[i].DOKill();
        }

        ApplyCardLayout();

        if (!runtimePreview) return;
        // Atualiza os destinos usados por RestoreCards / AnimateCards.
        // Sem isso, reabrir a aba faria os cards voltarem à posição antiga.
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] != null)
                cardPositions[i] = cardRects[i].anchoredPosition;
            if (isOpen && cards[i] != null && cards[i].gameObject.activeSelf &&
                cardGroups[i] != null)
                cardGroups[i].alpha = 1f;
        }
    }
#endif

    private void CacheCards()
    {
        if (cards == null) return;
        cardRects.Clear();
        cardGroups.Clear();
        cardPositions.Clear();
        foreach (ActiveMissionCard card in cards)
        {
            RectTransform rect = card != null ? card.transform as RectTransform : null;
            CanvasGroup group = card != null ? card.GetComponent<CanvasGroup>() : null;
            // CanvasGroup only controls visibility, never adds a fade animation.
            if (card != null && group == null) group = card.gameObject.AddComponent<CanvasGroup>();
            cardRects.Add(rect);
            cardGroups.Add(group);
            cardPositions.Add(rect != null ? rect.anchoredPosition : Vector2.zero);
        }
    }

    private void RestoreCards()
    {
        for (int i = 0; i < cardRects.Count; i++)
        {
            RectTransform rect = cardRects[i];
            if (rect != null)
            {
                rect.DOKill();
                rect.anchoredPosition = cardPositions[i];
            }
            if (cardGroups[i] != null) cardGroups[i].alpha = 1f;
        }
    }

    private void PrepareCards()
    {
        if (cards == null || cardRects.Count != cards.Length) CacheCards();
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cards[i] == null || !cards[i].gameObject.activeSelf) continue;
            if (cardGroups[i] != null) cardGroups[i].alpha = 0f;
            if (cardRects[i] != null)
                cardRects[i].anchoredPosition = cardPositions[i] + Vector2.up * cardDropDistance;
        }
    }

    private void AnimateCards()
    {
        if (cards == null) return;
        int visibleIndex = 0;
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cards[i] == null || !cards[i].gameObject.activeSelf || cardRects[i] == null)
                continue;
            RectTransform rect = cardRects[i];
            CanvasGroup group = cardGroups[i];
            Vector2 destination = cardPositions[i];
            float delay = visibleIndex++ * cardWaveInterval;
            Sequence entry = DOTween.Sequence().SetId(this);
            entry.AppendInterval(delay);
            entry.AppendCallback(() => { if (isOpen && group != null) group.alpha = 1f; });
            entry.Append(rect.DOAnchorPos(destination, cardDropDuration).SetEase(Ease.OutCubic));
        }
    }

    private void UpdateToggleText()
    {
        if (toggleText != null) toggleText.text = isOpen ? "FECHAR" : "MISSÕES";
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
        if (contentRect != null) contentRect.DOKill();
        RestoreCards();
    }

    public void Refresh(IReadOnlyList<DeliveryMission> missions, int day, ResourceBank resources)
    {
        if (cards == null || missions == null || resources == null) return;
        int shown = 0;
        for (int i = 0; i < missions.Count && shown < cards.Length; i++)
        {
            DeliveryMission mission = missions[i];
            if (!mission.accepted) continue;
            ActiveMissionCard card = cards[shown++];
            if (card == null) continue;
            int stock = mission.item switch
            {
                DeliveryItem.Wood => resources.Wood,
                DeliveryItem.Food => resources.Food,
                DeliveryItem.CornSeeds => resources.CornSeeds,
                DeliveryItem.PumpkinSeeds => resources.PumpkinSeeds,
                DeliveryItem.Fish => resources.Fish,
                DeliveryItem.Corn => resources.Corn,
                _ => resources.Pumpkin
            };
            card.gameObject.SetActive(true);
            Sprite icon = itemIcons != null && (int)mission.item < itemIcons.Length
                ? itemIcons[(int)mission.item] : null;
            card.Bind(mission, day, stock, icon);
        }
        for (int i = shown; i < cards.Length; i++)
            if (cards[i] != null) cards[i].gameObject.SetActive(false);
        // Ajusta a area rolavel a quantidade real de missoes, sem mudar as regras.
        ResizeScrollContent(shown);
    }
}
