using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Sprites;
using UnityEngine.UI;

public sealed class TimelineCardInspection : MonoBehaviour
{
    private static readonly int Cursor = Shader.PropertyToID("_Cursor");
    private static readonly int ArtDepth = Shader.PropertyToID("_ArtDepth");
    private static readonly int CardSize = Shader.PropertyToID("_CardSize");
    private static readonly int SpriteUv = Shader.PropertyToID("_SpriteUV");
    private static readonly int VisibleUv = Shader.PropertyToID("_VisibleUV");

    private float maxTilt;
    private float maxShift;
    private float followSpeed;
    private float artDepth;
    private float paperSheen;
    private RectTransform rect;
    private RectTransform parent;
    private Canvas canvas;
    private Image image;
    private InspectionPaperHighlight highlight;
    private Material depthMaterial;
    private Material previousMaterial;
    private Tween releaseTween;
    private bool active;
    private Vector2 center;
    private Vector2 cursor;
    private float sheenFocus;
    private Vector2 drawnSize;

    public void Configure(float tilt, float shift, float speed, float depth, float sheen)
    {
        maxTilt = tilt;
        maxShift = shift;
        followSpeed = speed;
        artDepth = depth;
        paperSheen = sheen;
        if (depthMaterial != null) depthMaterial.SetFloat(ArtDepth, artDepth);
        ApplyHighlight(1f);
    }

    public void Begin(Canvas owner)
    {
        rect = (RectTransform)transform;
        parent = rect.parent as RectTransform;
        canvas = owner;
        center = rect.anchoredPosition;
        image = GetComponent<Image>();
        if (image != null && image.sprite != null &&
            !image.sprite.name.Contains("9-SlicedBox"))
        {
            UpdateDrawingSize();
            PrepareCardMaterial();
            var layer = new GameObject("Reflexo do papel", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(InspectionPaperHighlight));
            var layerRect = (RectTransform)layer.transform;
            layerRect.SetParent(rect, false);
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;
            highlight = layer.GetComponent<InspectionPaperHighlight>();
            highlight.raycastTarget = false;
            ApplyHighlight(1f);
        }
        active = true;
    }

    public void Stop()
    {
        active = false;
        if (depthMaterial == null && highlight == null) return;
        Vector2 initial = cursor;
        releaseTween = DOVirtual.Float(1f, 0f, 0.16f, value =>
        {
            cursor = initial * value;
            if (depthMaterial != null)
                depthMaterial.SetVector(Cursor, new Vector4(cursor.x, cursor.y, PerspectiveAngle, 0f));
            ApplyHighlight(value);
        }).SetEase(Ease.OutCubic).OnComplete(ReleaseMaterial);
    }

    private float PerspectiveAngle => Mathf.Min(maxTilt, 16f) * Mathf.Deg2Rad;

    private void ApplyHighlight(float fade)
    {
        if (highlight == null) return;
        float opacity = paperSheen * Mathf.Lerp(0.54f, 1f, sheenFocus) * fade;
        highlight.Present(cursor, drawnSize, depthMaterial != null ? PerspectiveAngle : 0f, opacity);
    }

    private void PrepareCardMaterial()
    {
        Sprite sprite = image.sprite;
        if (sprite == null || sprite.name.Contains("9-SlicedBox") ||
            (sprite.packed && sprite.packingRotation != SpritePackingRotation.None))
            return;

        Shader shader = Resources.Load<Shader>("InspectionCardDepth");
        if (shader == null || !shader.isSupported) return;

        Vector2 spriteSize = sprite.rect.size;
        Vector4 padding = DataUtility.GetPadding(sprite);
        Vector4 outerUv = DataUtility.GetOuterUV(sprite);
        if (spriteSize.x <= 0f || spriteSize.y <= 0f ||
            outerUv.z <= outerUv.x || outerUv.w <= outerUv.y)
            return;

        previousMaterial = image.material;
        depthMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        depthMaterial.SetVector(SpriteUv, outerUv);
        depthMaterial.SetVector(VisibleUv, new Vector4(
            padding.x / spriteSize.x, padding.y / spriteSize.y,
            1f - padding.z / spriteSize.x, 1f - padding.w / spriteSize.y));
        depthMaterial.SetFloat(ArtDepth, artDepth);
        UpdateDrawingSize();
        image.material = depthMaterial;
    }

    private void UpdateDrawingSize()
    {
        Vector2 picture = image.sprite.rect.size;
        Vector2 available = image.GetPixelAdjustedRect().size;
        drawnSize = picture * Mathf.Min(available.x / picture.x, available.y / picture.y);
        if (depthMaterial != null)
            depthMaterial.SetVector(CardSize, new Vector4(drawnSize.x, drawnSize.y, 0f, 0f));
    }

    private void OnRectTransformDimensionsChange()
    {
        if (image != null && image.sprite != null)
        {
            UpdateDrawingSize();
            ApplyHighlight(1f);
        }
    }

    private void Update()
    {
        if (!active || parent == null || canvas == null) return;
        Vector2 target = ReadPointer(out float focus);
        float follow = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        cursor = Vector2.Lerp(cursor, target, follow);
        sheenFocus = Mathf.Lerp(sheenFocus, focus, follow);

        if (depthMaterial != null)
        {
            depthMaterial.SetVector(Cursor, new Vector4(cursor.x, cursor.y, PerspectiveAngle, 0f));
            rect.localRotation = Quaternion.Euler(0f, 0f, -cursor.x * maxTilt * 0.1f);
        }
        else
        {
            rect.localRotation = Quaternion.Euler(-cursor.y * maxTilt, cursor.x * maxTilt,
                -cursor.x * maxTilt * 0.2f);
        }

        ApplyHighlight(1f);
        float shift = depthMaterial != null ? maxShift * 0.35f : maxShift;
        rect.anchoredPosition = center + cursor * shift;
    }

    private Vector2 ReadPointer(out float focus)
    {
        focus = 0f;
        Touchscreen touch = Touchscreen.current;
        Mouse mouse = Mouse.current;
        Vector2 screen;
        if (touch != null && touch.primaryTouch.press.isPressed)
            screen = touch.primaryTouch.position.ReadValue();
        else if (mouse != null)
            screen = mouse.position.ReadValue();
        else
            return Vector2.zero;

        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera,
                out Vector2 local))
            return Vector2.zero;

        // The input plane stays still while the rendered card follows the pointer.
        Vector2 relative = local - parent.rect.center - center;
        Vector2 size = depthMaterial != null ? drawnSize : rect.rect.size;
        Vector2 half = Vector2.Scale(size * 0.5f, (Vector2)rect.localScale);
        if (depthMaterial != null)
            half = Vector2.Scale(half, new Vector2(0.72f, 0.93f));
        if (half.x <= 0f || half.y <= 0f) return Vector2.zero;
        Vector2 normalized = new Vector2(relative.x / half.x, relative.y / half.y);
        float edge = Mathf.Max(Mathf.Abs(normalized.x), Mathf.Abs(normalized.y));
        focus = 1f - Mathf.SmoothStep(0f, 1f, (edge - 1f) / 0.2f);
        return Vector2.ClampMagnitude(normalized, 1f) * focus;
    }

    private void OnDestroy()
    {
        releaseTween?.Kill();
        ReleaseMaterial();
    }

    private void ReleaseMaterial()
    {
        if (depthMaterial == null) return;
        if (image != null) image.material = previousMaterial;
        Destroy(depthMaterial);
        depthMaterial = null;
    }
}
