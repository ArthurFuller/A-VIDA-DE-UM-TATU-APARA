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
#if UNITY_EDITOR
    [Header("Prévia dos recursos no primeiro card")]
    [SerializeField] private SaleItem previewFirst = SaleItem.Fish;
    [SerializeField] private SaleItem previewSecond = SaleItem.Wood;
    [SerializeField] private SaleItem previewThird = SaleItem.Corn;
#endif
    [Header("Sprite, tamanho e posição de cada recurso")]
    [SerializeField] private List<ActiveMissionIconProfile> iconProfiles = new();
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

    [Header("Entrada da janela")]
    [SerializeField, Min(0.05f)] private float animationDuration = 0.36f;
    [SerializeField, Min(0f), Tooltip("Folga acima da area recortada antes de a janela descer.")]
    private float hiddenOffset = 24f;

    [Header("Entrada dos cards")]
    [SerializeField, Min(0.05f)] private float cardDropDuration = 0.26f;
    [SerializeField, Min(0f)] private float cardWaveInterval = 0.11f;
    [SerializeField, Min(0f)] private float cardDropDistance = 56f;

    [Header("Entrega dos cards")]
    [SerializeField, Min(0.05f)] private float deliveryExitDuration = 0.27f;
    [SerializeField, Min(0.05f)] private float deliveryReflowDuration = 0.31f;

    private ActiveMissionCard deliveredCard;
    private int deliveryVersion;
    private bool rebindingAfterDelivery;
    private Sequence deliveryMotion;
    private System.Action refreshAfterDelivery;

    private readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();
    private readonly List<RectTransform> cardRects = new List<RectTransform>();
    private readonly List<Vector2> cardPositions = new List<Vector2>();
    private bool isOpen;
    private Vector2 openPosition;
    private ActivityCardAvailability hand;
    private CommunityCamera communityCamera;

    public bool IsOpen => isOpen;
    internal RectTransform TutorialList => contentRect;

#if UNITY_EDITOR
    public ActiveMissionCard EditorPreviewCard =>
        cards != null && cards.Length > 0 ? cards[0] : null;
    private SaleItem PreviewItem(int index) => index switch
    {
        0 => previewFirst,
        1 => previewSecond,
        _ => previewThird
    };

    public void RefreshEditorIconPreview(int index)
    {
        if (Application.isPlaying || cards == null || cards.Length == 0 || cards[0] == null) return;
        ActiveMissionIconProfile profile = ProfileFor(PreviewItem(index));
        profile?.Apply(cards[0].IconAt(index), index);
    }

    public void RefreshMatchingEditorSprites(int changedIndex)
    {
        if (Application.isPlaying || EditorPreviewCard == null) return;
        SaleItem item = PreviewItem(changedIndex);
        ActiveMissionIconProfile profile = ProfileFor(item);
        if (profile == null) return;
        for (int i = 0; i < 3; i++)
        {
            if (i == changedIndex || PreviewItem(i) != item) continue;
            Image image = EditorPreviewCard.IconAt(i);
            if (image == null) continue;
            image.sprite = profile.sprite;
            image.enabled = profile.sprite != null;
        }
    }

    public void RefreshEditorCardFormatting()
    {
        if (EditorPreviewCard == null) return;
        EditorPreviewCard.RefreshEditorFormatting(iconProfiles);
        ApplySharedCardFormatting();
        if (Application.isPlaying)
            foreach (ActiveMissionCard card in cards)
                if (card != null && card != EditorPreviewCard)
                    card.RefreshIconProfiles(iconProfiles);
    }

    public void RefreshEditorPreview()
    {
        if (Application.isPlaying || EditorPreviewCard == null) return;
        EditorPreviewCard.ShowEditorThirdItem();
        for (int i = 0; i < 3; i++) RefreshEditorIconPreview(i);
        ApplyCardLayout();
        RefreshEditorCardFormatting();
    }

    public void CaptureEditorIcon(int index)
    {
        if (EditorPreviewCard == null) return;
        if (index < 0 || index > 2) return;
        ActiveMissionIconProfile profile = ProfileFor(EditorPreviewCard.PreviewedItem(index, PreviewItem(index)));
        Image image = EditorPreviewCard.IconAt(index);
        if (profile == null || image == null) return;
        profile.sprite = image.sprite;
        if (index == 0)
        {
            profile.firstSize = image.rectTransform.sizeDelta;
            profile.firstPosition = image.rectTransform.anchoredPosition;
        }
        else if (index == 1)
        {
            profile.secondSize = image.rectTransform.sizeDelta;
            profile.secondPosition = image.rectTransform.anchoredPosition;
        }
        else
        {
            profile.thirdSize = image.rectTransform.sizeDelta;
            profile.thirdPosition = image.rectTransform.anchoredPosition;
        }
    }
#endif

    private ActiveMissionIconProfile ProfileFor(SaleItem item)
    {
        foreach (ActiveMissionIconProfile profile in iconProfiles)
            if (profile != null && profile.item == item) return profile;
        return null;
    }

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
        hand = FindFirstObjectByType<ActivityCardAvailability>(FindObjectsInactive.Include);
        communityCamera = FindFirstObjectByType<CommunityCamera>(FindObjectsInactive.Include);
        if (contentRect != null) openPosition = contentRect.anchoredPosition;
        ApplySharedCardFormatting();
        ApplyCardLayout();
        CacheCards();
        isOpen = false;
        if (contentRect != null) contentRect.anchoredPosition = HiddenPosition;
        if (contentGroup != null)
        {
            contentGroup.alpha = 1f; // No panel fade: visibility is controlled by the mask.
            contentGroup.blocksRaycasts = false;
        }
        UpdateToggleText();
    }

    private bool HasCompetingInteraction =>
        Time.timeScale == 0f || SertaoCoinToss.IsOpen ||
        (hand != null && (hand.IsDraggingCard || hand.IsSelectionLocked)) ||
        (communityCamera != null && (communityCamera.HasGameplayModal || communityCamera.IsFollowingWorker));

    private void Update()
    {
        if (isOpen && HasCompetingInteraction) Close();
    }

    public void Toggle()
    {
        if (!isOpen && HasCompetingInteraction) return;
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
        if (!isOpen) FinishDelivery();
        DOTween.Kill(this);
        if (contentRect != null) contentRect.DOKill();
        RestoreCards();
        UpdateToggleText();

        if (contentGroup != null)
        {
            contentGroup.alpha = 1f;
            // Bloqueia o ponteiro sem aplicar a cor de botão desativado.
            contentGroup.blocksRaycasts = false;
        }
        if (contentRect == null)
        {
            if (!isOpen) Closed?.Invoke();
            return;
        }

        AudioManager.Play(SoundCue.UiPaper);
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
                .SetEase(Ease.InCubic).SetId(this).SetUpdate(true)
                .OnComplete(() => Closed?.Invoke());
        }
    }

    private void ApplySharedCardFormatting()
    {
        if (cards == null || cards.Length == 0 || cards[0] == null) return;
        for (int i = 1; i < cards.Length; i++)
            if (cards[i] != null) cards[i].CopyFormattingFrom(cards[0]);
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
            if (card == null || (Application.isPlaying && !card.gameObject.activeSelf)) continue;
            RectTransform rect = card.transform as RectTransform;
            if (rect == null) continue;
            Vector2 position = rect.anchoredPosition;
            position.y = -top;
            rect.anchoredPosition = position;
            float actualHeight = Mathf.Max(height, card.RequiredHeight);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, actualHeight);
            top += actualHeight + spacing;
            validCards++;
        }
        ResizeScrollContentHeight(top - (validCards > 0 ? spacing : 0f) +
            Mathf.Max(0f, listBottomPadding));
    }

    private void ResizeScrollContentHeight(float needed)
    {
        if (listContent == null) return;
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
        RefreshEditorPreview();
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
        if (runtimePreview && deliveredCard != null) return;
        if (runtimePreview)
        {
            // Só interrompe tweens dos cards enquanto o desenvolvedor está
            // ajustando o Inspector. A animação da janela permanece intacta.
            for (int i = 0; i < cardRects.Count; i++)
                if (cardRects[i] != null) cardRects[i].DOKill();
        }

        ApplyCardLayout();

        if (runtimePreview)
            foreach (ActiveMissionCard card in cards)
                if (card != null) card.RefreshIconProfiles(iconProfiles);

        if (!runtimePreview) return;
        // Atualiza os destinos usados por RestoreCards / AnimateCards.
        // Sem isso, reabrir a aba faria os cards voltarem à posição antiga.
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] != null)
                cardPositions[i] = cardRects[i].anchoredPosition;
            if (isOpen && cards[i] != null && cards[i].gameObject.activeSelf &&
                cardGroups[i] != null)
            {
                cardGroups[i].alpha = 1f;
                cardGroups[i].blocksRaycasts = true;
            }
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
            if (cardGroups[i] != null)
            {
                cardGroups[i].alpha = 1f;
                cardGroups[i].blocksRaycasts = true;
            }
        }
    }

    private void PrepareCards()
    {
        if (cards == null || cardRects.Count != cards.Length) CacheCards();
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cards[i] == null || !cards[i].gameObject.activeSelf) continue;
            if (cardGroups[i] != null)
            {
                cardGroups[i].alpha = 0f;
                cardGroups[i].blocksRaycasts = false;
            }
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
            Sequence entry = DOTween.Sequence().SetId(this).SetTarget(rect);
            entry.AppendInterval(delay);
            entry.AppendCallback(() => { if (isOpen && group != null) group.alpha = 1f; });
            entry.Append(rect.DOAnchorPos(destination, cardDropDuration).SetEase(Ease.OutCubic));
            entry.OnComplete(() => { if (isOpen && group != null) group.blocksRaycasts = true; });
        }
    }

    private void UpdateToggleText()
    {
        if (toggleText != null) toggleText.text = isOpen ? "FECHAR" : "MISSÕES";
    }

    private void OnDisable()
    {
        FinishDelivery();
        DOTween.Kill(this);
        if (contentRect != null) contentRect.DOKill();
        RestoreCards();
    }

    public bool BeginDelivery(ActiveMissionCard card, System.Action refresh, out System.Action onLastDeparture)
    {
        onLastDeparture = null;
        if (!isActiveAndEnabled || !isOpen || deliveredCard != null || card == null ||
            !card.gameObject.activeInHierarchy) return false;
        deliveredCard = card;
        refreshAfterDelivery = refresh;
        card.SetDelivering();

        int version = ++deliveryVersion;
        onLastDeparture = () =>
        {
            // As moedas de uma entrega anterior podem continuar em voo após reabrir a aba.
            if (this != null && deliveryVersion == version && deliveredCard == card)
                AnimateDeliveredCardExit(card);
        };
        return true;
    }

    private void AnimateDeliveredCardExit(ActiveMissionCard card)
    {
        if (!isOpen || deliveryMotion != null) return;
        int index = System.Array.IndexOf(cards, card);
        if (index < 0 || index >= cardRects.Count || cardRects[index] == null)
        {
            FinishDelivery();
            return;
        }

        RectTransform departing = cardRects[index];
        RectTransform viewport = listContent != null ? listContent.parent as RectTransform : null;
        Transform parent = departing.parent;
        float right = viewport != null
            ? parent.InverseTransformPoint(viewport.TransformPoint(new Vector3(viewport.rect.xMax, 0f))).x
            : parent.InverseTransformPoint(departing.TransformPoint(Vector3.right * departing.rect.width)).x;
        float left = parent.InverseTransformPoint(departing.TransformPoint(Vector3.right * departing.rect.xMin)).x;
        float exitX = departing.anchoredPosition.x + Mathf.Max(0f, right - left) + 4f;

        Sequence motion = DOTween.Sequence().SetId(this);
        deliveryMotion = motion;
        motion.Append(departing.DOAnchorPosX(exitX, deliveryExitDuration).SetEase(Ease.InCubic));
        float rise = departing.rect.height + Mathf.Max(0f, cardSpacing);
        float riseStart = deliveryExitDuration;
        for (int i = index + 1; i < cardRects.Count; i++)
        {
            RectTransform rect = cardRects[i];
            if (rect == null || !rect.gameObject.activeSelf) continue;
            rect.DOKill();
            CanvasGroup group = cardGroups[i];
            if (group != null)
            {
                // O botão mantém seu estado visual até terminar de subir.
                group.blocksRaycasts = false;
                if (group.alpha < 1f)
                    motion.InsertCallback(riseStart, () => group.alpha = 1f);
            }
            motion.Insert(riseStart, rect.DOAnchorPosY(cardPositions[i].y + rise, deliveryReflowDuration)
                .SetEase(Ease.OutCubic));
        }

        if (viewport != null)
        {
            float bottom = Mathf.Max(0f, listContent.rect.height - viewport.rect.height - rise);
            if (listContent.anchoredPosition.y > bottom)
            {
                contentRect?.GetComponent<ScrollRect>()?.StopMovement();
                motion.Insert(riseStart, listContent.DOAnchorPosY(bottom, deliveryReflowDuration)
                    .SetEase(Ease.OutCubic));
            }
        }

        motion.OnComplete(() =>
        {
            deliveryMotion = null;
            FinishDelivery();
        });
    }

    public void FinishDelivery()
    {
        if (refreshAfterDelivery == null) return;
        deliveryVersion++;
        deliveryMotion?.Kill();
        deliveryMotion = null;
        deliveredCard = null;
        System.Action refresh = refreshAfterDelivery;
        refreshAfterDelivery = null;
        RestoreCards();
        rebindingAfterDelivery = true;
        try
        {
            refresh.Invoke();
        }
        finally
        {
            rebindingAfterDelivery = false;
        }
    }

    public void Refresh(IReadOnlyList<DeliveryMission> missions, DeliveryMissionBoard board)
    {
        if (cards == null || missions == null || board == null || refreshAfterDelivery != null) return;
        ApplySharedCardFormatting();
        int shown = 0;
        float usedHeight = Mathf.Max(0f, firstCardTopPadding);
        for (int i = 0; i < missions.Count && shown < cards.Length; i++)
        {
            DeliveryMission mission = missions[i];
            if (!mission.accepted) continue;
            int cardIndex = shown++;
            ActiveMissionCard card = cards[cardIndex];
            if (card == null) continue;
            card.gameObject.SetActive(true);
            float height = Mathf.Max(cardHeight,
                card.Bind(mission, iconProfiles, board, rebindingAfterDelivery));
            RectTransform rect = card.transform as RectTransform;
            if (rect == null) continue;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Vector2 position = rect.anchoredPosition;
            position.y = -usedHeight;
            rect.anchoredPosition = position;
            if (cardIndex < cardPositions.Count) cardPositions[cardIndex] = position;
            usedHeight += height + Mathf.Max(0f, cardSpacing);
        }
        for (int i = shown; i < cards.Length; i++)
            if (cards[i] != null) cards[i].gameObject.SetActive(false);
        // Ajusta a area rolavel a quantidade real de missoes, sem mudar as regras.
        ResizeScrollContentHeight(usedHeight - (shown > 0 ? Mathf.Max(0f, cardSpacing) : 0f) +
            Mathf.Max(0f, listBottomPadding));
    }

}
