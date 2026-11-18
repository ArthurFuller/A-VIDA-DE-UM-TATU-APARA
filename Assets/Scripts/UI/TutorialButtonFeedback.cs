using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TutorialButtonFeedback : Button
{
    private const float HoverScale = 1.027f;
    private const float PressedScale = 0.974f;
    private const float ClickScale = 1.033f;
    private const float OutlineSize = 0.8f;

    private RectTransform buttonRect;
    private RectTransform box;
    private RectTransform visual;
    private Outline outline;
    private Tween motion;
    private float scale = 1f;
    private bool hovered;
    private bool pressed;
    private bool clicking;

    internal void Configure(Image image, RectTransform container)
    {
        buttonRect = (RectTransform)transform;
        box = container;
        visual = image.rectTransform;
        targetGraphic = image;
        outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.79f, 0.37f, 0.85f);
        outline.enabled = false;
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        hovered = IsActive() && IsInteractable();
        if (hovered && !pressed && !clicking) AnimateTo(HoverScale, 0.105f);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        hovered = false;
        if (!clicking) AnimateTo(1f, 0.105f);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        if (eventData.button != PointerEventData.InputButton.Left || clicking || !IsActive() || !IsInteractable())
            return;
        pressed = true;
        AnimateTo(PressedScale, 0.045f);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        if (eventData.button != PointerEventData.InputButton.Left || !pressed) return;
        pressed = false;
        if (!clicking) AnimateTo(hovered ? HoverScale : 1f, 0.09f);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!clicking) base.OnPointerClick(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (!clicking) base.OnSubmit(eventData);
    }

    internal void PlayClick(Action afterFeedback)
    {
        if (clicking || !IsActive() || !IsInteractable()) return;
        clicking = true;
        motion?.Kill();
        motion = DOTween.Sequence().SetUpdate(true)
            .Append(ScaleTo(PressedScale, 0.035f))
            .Append(ScaleTo(ClickScale, 0.055f))
            .OnComplete(() =>
            {
                motion = null;
                clicking = false;
                AnimateTo(hovered ? HoverScale : 1f, 0.07f);
                afterFeedback?.Invoke();
            });
    }

    internal void PlayEntrance()
    {
        motion?.Kill();
        scale = .82f;
        ApplyVisualScale();
        motion = DOTween.Sequence().SetUpdate(true)
            .Append(ScaleTo(ClickScale, .13f))
            .Append(ScaleTo(hovered ? HoverScale : 1f, .09f));
    }

    private void AnimateTo(float target, float duration)
    {
        motion?.Kill();
        motion = ScaleTo(target, duration).SetUpdate(true);
    }

    private Tween ScaleTo(float target, float duration) =>
        DOTween.To(() => scale, value => scale = value, target, duration).SetEase(Ease.OutCubic);

    private void LateUpdate() => ApplyVisualScale();

    private void ApplyVisualScale()
    {
        if (visual == null || buttonRect == null || box == null || outline == null) return;
        Rect button = buttonRect.rect;
        Rect area = box.rect;
        Vector2 center = buttonRect.localPosition + (Vector3)button.center;
        Vector2 available = Vector2.Min(center - area.min, area.max - center);
        bool showOutline = (hovered || clicking) && IsInteractable();
        Vector2 half = Vector2.Max(Vector2.one, button.size) * 0.5f;
        float extra = showOutline ? OutlineSize : 0f;
        float budget = Mathf.Min(available.x / (half.x + extra), available.y / (half.y + extra));
        float boundedScale = Mathf.Min(scale, Mathf.Max(1f, budget));

        Vector3 targetScale = Vector3.one * boundedScale;
        if (visual.localScale != targetScale) visual.localScale = targetScale;

        // The contour itself must also fit when an authored button sits close to an edge.
        Vector2 remaining = available / boundedScale - half;
        float thickness = Mathf.Clamp(Mathf.Min(remaining.x, remaining.y), 0f, OutlineSize);
        outline.enabled = showOutline && thickness > 0.01f;
        if (outline.enabled) outline.effectDistance = new Vector2(thickness, -thickness);
    }

    protected override void OnDisable()
    {
        motion?.Kill();
        motion = null;
        scale = 1f;
        if (visual != null) visual.localScale = Vector3.one;
        if (outline != null) outline.enabled = false;
        hovered = pressed = clicking = false;
        base.OnDisable();
    }
}
