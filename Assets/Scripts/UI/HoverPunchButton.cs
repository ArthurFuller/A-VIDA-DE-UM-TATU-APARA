using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class HoverPunchButton : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    [SerializeField, Min(1f)] private float hoverScale = 1.08f;
    [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.9f;
    [SerializeField, Min(0.05f)] private float duration = 0.16f;

    private Button button;
    private Image image;
    private RectTransform rect;
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private Quaternion originalRotation;
    private Color originalColor;
    private bool hovered;
    private bool pressedEligible;
    private bool animating;
    public bool IsAnimating => animating;
    public bool AnimatePointerClick { get; set; } = true;

    internal static HoverPunchButton Bind(Button target)
    {
        if (target.transition == Selectable.Transition.ColorTint)
        {
            ColorBlock colors = SertaoButtonHoverPalette.WithFeedbackColors(target.colors);
            Color normal = colors.normalColor;
            if (Mathf.Min(normal.r, normal.g, normal.b) > 0.95f)
                colors.normalColor = new Color(0.92f, 0.92f, 0.92f, normal.a);
            if (!target.colors.Equals(colors)) target.colors = colors;
        }
        return target.TryGetComponent(out HoverPunchButton effect)
            ? effect : target.gameObject.AddComponent<HoverPunchButton>();
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        SertaoButtonHoverPalette.Apply(button);
        image = button.image != null ? button.image : GetComponent<Image>();
        rect = (RectTransform)transform;
        originalScale = rect.localScale;
        originalPosition = rect.anchoredPosition;
        originalRotation = rect.localRotation;
        if (image != null)
        {
            originalColor = image.color;
            if (image.TryGetComponent(out Outline oldOutline)) oldOutline.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (rect != null)
        {
            rect.DOKill();
            rect.localScale = originalScale;
            rect.anchoredPosition = originalPosition;
            rect.localRotation = originalRotation;
        }
        if (image != null)
        {
            image.DOKill();
            image.color = originalColor;
        }
        hovered = false;
        pressedEligible = false;
        animating = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) return;
        hovered = true;
        if (animating) return;
        rect.DOKill();
        rect.DOScale(originalScale * hoverScale, duration).SetEase(Ease.OutBack).SetUpdate(true);
        TweenTint(SertaoButtonHoverPalette.Lighten(originalColor));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        if (animating) return;
        rect.DOKill();
        rect.DOScale(originalScale, duration).SetEase(Ease.OutCubic).SetUpdate(true);
        TweenTint(originalColor);
    }

    private void TweenTint(Color target)
    {
        if (image == null) return;
        image.DOKill();
        image.DOColor(target, duration).SetUpdate(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!AnimatePointerClick || !pressedEligible || animating || button == null || !button.isActiveAndEnabled ||
            !button.IsInteractable()) return;
        PlayAcceptedClick();
    }

    public void PlayAcceptedClick()
    {
        if (rect == null || animating) return;
        pressedEligible = false;
        rect.DOKill();
        animating = true;
        // Botões com transição própria, como Finalizar Dia, não usam o ColorBlock.
        if (image != null && button.transition != Selectable.Transition.ColorTint)
        {
            image.DOKill();
            image.color = SertaoButtonHoverPalette.Pressed(originalColor.a);
        }
        DOTween.Sequence().SetTarget(rect).SetUpdate(true)
            .Append(rect.DOScale(originalScale * pressedScale, 0.08f))
            .Append(rect.DOScale(originalScale * (hovered ? hoverScale : 1f), duration)
                .SetEase(Ease.OutBack))
            .OnComplete(() =>
            {
                rect.localScale = originalScale * (hovered ? hoverScale : 1f);
                TweenTint(hovered && button.IsInteractable()
                    ? SertaoButtonHoverPalette.Lighten(originalColor) : originalColor);
                animating = false;
            })
            .OnKill(() => animating = false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressedEligible = eventData.button == PointerEventData.InputButton.Left &&
            AnimatePointerClick && !animating && button != null &&
            button.isActiveAndEnabled && button.IsInteractable();
    }

    public void ShakeRejected()
    {
        if (rect == null || animating) return;
        AudioManager.Play(SoundCue.UiRejected);
        pressedEligible = false;
        rect.DOKill();
        animating = true;
        rect.localScale = originalScale * (hovered ? hoverScale : 1f);
        rect.anchoredPosition = originalPosition;
        rect.localRotation = originalRotation;
        rect.DOShakeRotation(0.32f, new Vector3(0f, 0f, 8f), 12, 90f).SetUpdate(true)
            .OnComplete(() =>
            {
                rect.localRotation = originalRotation;
                rect.localScale = originalScale * (hovered ? hoverScale : 1f);
                TweenTint(hovered && button.IsInteractable()
                    ? SertaoButtonHoverPalette.Lighten(originalColor) : originalColor);
                animating = false;
            })
            .OnKill(() =>
            {
                rect.localRotation = originalRotation;
                animating = false;
            });
    }
}
