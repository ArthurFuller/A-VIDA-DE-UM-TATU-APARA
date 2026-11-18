using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class EventCardView : MonoBehaviour
{
    [SerializeField] private CanvasGroup backdrop;
    [SerializeField] private RectTransform card;
    [SerializeField] private Text titleText;
    [SerializeField] private LetterJumpText descriptionText;
    [SerializeField] private CanvasGroup descriptionGroup;
    [SerializeField] private Button revealButton;
    [SerializeField] private Button skipButton;
    [Header("Animação configurável")]
    [SerializeField, Min(0)] private float entranceDuration = 0.6f;
    [SerializeField, Min(1f)] private float firstDayEntranceMultiplier = 1.30f;
    [SerializeField, Min(0)] private float flipDuration = 0.35f;
    [SerializeField, Min(0)] private float textDuration = 1.2f;
    [SerializeField] private float entranceOffset = -650f;
    [SerializeField] private float swingDegrees = 7f;
    [SerializeField, Min(0)] private float descriptionSlideOffset = 35f;
    [SerializeField, Min(0)] private float revealedGap = 0f;
    [SerializeField] private Vector2 illustratedCardSize = new Vector2(360f, 360f);
    private Vector2 homePosition;
    private Vector2 descriptionHomePosition;
    private Vector2 revealedCardPosition;
    private Vector2 originalCardSize;
    private Vector2 originalRevealPosition;
    private RectTransform descriptionRect;
    private Image backdropImage;
    private Color backdropColor;
    private Image cardImage;
    private Color frontColor;
    private Sprite originalSprite;
    private bool originalPreserveAspect;
    private string pendingDescription;
    private bool flipped;
    private bool prepared;
    private bool animateBackdropColor;
    private Sequence revealMotion;
    public bool IsOpen { get; private set; }
    internal bool BlocksCameraZoom => isActiveAndEnabled &&
        (IsOpen || (backdrop != null && backdrop.isActiveAndEnabled && backdrop.blocksRaycasts));
    internal bool TutorialRevealReady { get; private set; }
    internal RectTransform TutorialRevealTarget => revealButton != null ? revealButton.transform as RectTransform : null;
    public RectTransform CardRect => card;
    public event Action Closed;

    private void Awake()
    {
        if (backdrop != null)
        {
            backdropImage = backdrop.GetComponent<Image>();
            if (backdropImage != null) backdropColor = backdropImage.color;
        }
        if (card != null)
        {
            originalCardSize = card.sizeDelta;
            cardImage = card.GetComponent<Image>();
            if (revealButton == null) revealButton = card.GetComponentInChildren<Button>(true);
            if (revealButton != null)
                originalRevealPosition = ((RectTransform)revealButton.transform).anchoredPosition;
        }
        if (cardImage != null)
        {
            frontColor = cardImage.color;
            originalSprite = cardImage.sprite;
            originalPreserveAspect = cardImage.preserveAspect;
        }
        if (descriptionGroup != null)
            descriptionRect = descriptionGroup.GetComponent<RectTransform>();
        UpdatePositions();
        HideImmediate();
    }

    private static Vector2 CenteredPosition(RectTransform rect) => new Vector2(
        (rect.pivot.x - 0.5f) * rect.rect.width,
        (rect.pivot.y - 0.5f) * rect.rect.height);

    private void UpdatePositions()
    {
        if (card == null) return;
        homePosition = CenteredPosition(card);
        if (revealButton != null)
        {
            RectTransform button = (RectTransform)revealButton.transform;
            button.anchoredPosition = originalRevealPosition + new Vector2(
                (0.5f - button.anchorMin.x) * (card.rect.width - originalCardSize.x),
                -(1f - button.anchorMin.y) * (card.rect.height - originalCardSize.y));
        }
        if (descriptionRect == null) return;
        float left = -(card.rect.width + revealedGap + descriptionRect.rect.width) * 0.5f;
        revealedCardPosition = new Vector2(left + card.pivot.x * card.rect.width,
            homePosition.y);
        descriptionHomePosition = new Vector2(
            left + card.rect.width + revealedGap +
                descriptionRect.pivot.x * descriptionRect.rect.width,
            (descriptionRect.pivot.y - 0.5f) * descriptionRect.rect.height);
    }

    public void Show(string title, string description, Sprite art = null,
        Color? previousBackdrop = null)
    {
        Prepare(title, description, art, previousBackdrop);
        PlayPreparedEntrance();
    }

    internal void Prepare(string title, string description, Sprite art, Color? previousBackdrop)
    {
        if (card == null || backdrop == null) return;
        // The overlay can start inactive in the scene. Run Awake before setting up the card.
        if (!backdrop.gameObject.activeSelf) backdrop.gameObject.SetActive(true);
        IsOpen = false;
        backdrop.alpha = 0f;
        backdrop.blocksRaycasts = false;
        pendingDescription = description;
        flipped = false;
        TutorialRevealReady = false;
        revealMotion?.Kill();
        card.DOKill();
        backdrop.DOKill();
        if (descriptionGroup != null) descriptionGroup.DOKill();
        if (descriptionRect != null) descriptionRect.DOKill();
        card.sizeDelta = art != null ? illustratedCardSize : originalCardSize;
        UpdatePositions();
        if (CardDescriptionCatalog.Instance != null)
            descriptionHomePosition += CardDescriptionCatalog.Instance.eventOffset;
        if (cardImage != null)
        {
            cardImage.sprite = art != null ? art : originalSprite;
            cardImage.color = art != null ? Color.white : frontColor;
            cardImage.preserveAspect = art != null || originalPreserveAspect;
        }
        if (titleText != null)
        {
            titleText.text = title;
            titleText.transform.localRotation = Quaternion.identity;
            titleText.gameObject.SetActive(art == null);
        }
        if (revealButton != null)
        {
            revealButton.gameObject.SetActive(true);
            revealButton.interactable = true;
        }
        if (skipButton != null) skipButton.gameObject.SetActive(true);
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (descriptionGroup != null)
        {
            descriptionGroup.alpha = 0;
            descriptionGroup.blocksRaycasts = false;
            descriptionGroup.interactable = false;
        }
        if (descriptionRect != null)
            descriptionRect.anchoredPosition = descriptionHomePosition + Vector2.left * descriptionSlideOffset;
        card.anchoredPosition = homePosition + Vector2.down * Mathf.Abs(entranceOffset);
        card.localScale = Vector3.one;
        card.localRotation = Quaternion.identity;
        animateBackdropColor = previousBackdrop.HasValue;
        if (backdropImage != null)
        {
            backdropImage.DOKill();
            backdropImage.color = previousBackdrop ?? backdropColor;
        }
        backdrop.gameObject.SetActive(true);
        backdrop.transform.SetAsLastSibling();
        prepared = true;
    }

    internal void RevealPreparedBackdrop()
    {
        if (!prepared) return;
        if (card.parent is RectTransform cardArea)
        {
            float belowScreen = (cardArea.rect.height + card.rect.height) * 0.5f + 12f;
            card.anchoredPosition = homePosition + Vector2.down *
                Mathf.Max(Mathf.Abs(entranceOffset), belowScreen);
        }
        backdrop.alpha = 1f;
    }

    internal void PlayPreparedEntrance(bool firstDay = false)
    {
        if (!prepared) return;
        if (firstDay) RevealPreparedBackdrop();
        prepared = false;
        IsOpen = true;
        AudioManager.Play(SoundCue.UiPaper);
        backdrop.alpha = 1f;
        backdrop.blocksRaycasts = true;
        float duration = firstDay ? entranceDuration * firstDayEntranceMultiplier : entranceDuration;
        if (backdropImage != null && animateBackdropColor)
            backdropImage.DOColor(backdropColor, duration).SetEase(Ease.OutCubic);
        card.DOAnchorPos(homePosition, duration).SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                TutorialRevealReady = true;
                card.DORotate(new Vector3(0, 0, swingDegrees), 0.12f)
                    .SetLoops(2, LoopType.Yoyo).OnComplete(() => card.localRotation = Quaternion.identity);
            });
    }

    public void Flip()
    {
        if (!IsOpen || flipped || card == null || descriptionGroup == null) return;
        AudioManager.Play(SoundCue.UiPaper);
        flipped = true;
        TutorialRevealReady = false;
        card.DOKill();
        if (revealButton != null)
        {
            revealButton.interactable = false;
            revealButton.gameObject.SetActive(false);
        }
        if (skipButton != null) skipButton.gameObject.SetActive(false);
        float duration = Mathf.Max(0.15f, flipDuration);
        revealMotion = DOTween.Sequence().SetTarget(this);
        revealMotion.Append(card.DOAnchorPos(revealedCardPosition, duration)
            .SetEase(Ease.InOutCubic));
        if (descriptionRect != null)
            revealMotion.Insert(0f,
                descriptionRect.DOAnchorPos(descriptionHomePosition, duration)
                    .SetEase(Ease.OutCubic));
        revealMotion.Insert(0f, descriptionGroup.DOFade(1f, duration));
        revealMotion.InsertCallback(duration * 0.4f, () =>
        {
            if (descriptionText != null)
                descriptionText.Reveal(pendingDescription, textDuration);
            descriptionGroup.blocksRaycasts = true;
            descriptionGroup.interactable = true;
        });
    }

    public void Close()
    {
        if (!IsOpen) return;
        AudioManager.Play(SoundCue.UiPaper);
        IsOpen = false;
        TutorialRevealReady = false;
        revealMotion?.Kill();
        if (descriptionText != null) descriptionText.DOKill();
        if (descriptionGroup != null) descriptionGroup.DOKill();
        if (descriptionRect != null) descriptionRect.DOKill();
        if (card != null) card.DOKill();
        if (backdropImage != null) backdropImage.DOKill();
        if (backdrop != null) backdrop.DOFade(0, 0.2f).OnComplete(() =>
        {
            HideImmediate();
            Closed?.Invoke();
        });
    }

    internal void CloseImmediately()
    {
        if (!IsOpen && (backdrop == null || !backdrop.blocksRaycasts)) return;
        IsOpen = false;
        TutorialRevealReady = false;
        revealMotion?.Kill();
        if (card != null) card.DOKill();
        if (backdrop != null) backdrop.DOKill();
        if (backdropImage != null) backdropImage.DOKill();
        if (descriptionText != null) DOTween.Kill(descriptionText);
        if (descriptionGroup != null) descriptionGroup.DOKill();
        if (descriptionRect != null) descriptionRect.DOKill();
        HideImmediate();
        Closed?.Invoke();
    }

    private void HideImmediate()
    {
        prepared = false;
        if (descriptionGroup != null)
        {
            descriptionGroup.alpha = 0;
            descriptionGroup.blocksRaycasts = false;
            descriptionGroup.interactable = false;
        }
        if (descriptionRect != null) descriptionRect.anchoredPosition = descriptionHomePosition;
        if (backdrop != null)
        {
            backdrop.alpha = 0;
            backdrop.blocksRaycasts = false;
            backdrop.gameObject.SetActive(false);
        }
        if (card != null)
        {
            card.sizeDelta = originalCardSize;
            card.anchoredPosition = CenteredPosition(card);
            card.localScale = Vector3.one;
            card.localRotation = Quaternion.identity;
        }
        if (revealButton != null)
            ((RectTransform)revealButton.transform).anchoredPosition = originalRevealPosition;
        if (cardImage != null)
        {
            cardImage.sprite = originalSprite;
            cardImage.color = frontColor;
            cardImage.preserveAspect = originalPreserveAspect;
        }
        if (titleText != null) titleText.transform.localRotation = Quaternion.identity;
        if (revealButton != null) revealButton.gameObject.SetActive(true);
        if (skipButton != null) skipButton.gameObject.SetActive(true);
        IsOpen = false;
    }
}
