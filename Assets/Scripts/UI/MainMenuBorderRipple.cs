using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Sertão/Menu/Ondulação da moldura")]
public sealed class MainMenuBorderRipple : MonoBehaviour
{
    [SerializeField] private Shader rippleShader;
    [SerializeField, Range(0.03f, 0.20f),
        Tooltip("Faixa da arte, medida da borda em direção ao centro.")]
    private float edgeBand = 0.105f;
    [SerializeField, Range(0f, 12f), Tooltip("Intensidade máxima da resposta aos impactos, em unidades da UI.")]
    private float impactAmplitude = 5.2f;
    [SerializeField, Range(0f, 4f), Tooltip("Intensidade da ondulação contínua após o primeiro impacto.")]
    private float idleAmplitude = 1.35f;
    [SerializeField, Range(0.4f, 3.5f)]
    private float idleSpeed = 1.45f;

    private static readonly int SpriteUvId = Shader.PropertyToID("_SpriteUV");
    private static readonly int WaveId = Shader.PropertyToID("_Wave");
    private static readonly int SizeClockId = Shader.PropertyToID("_SizeClock");
    private static readonly int[] ImpactIds =
    {
        Shader.PropertyToID("_ImpactA"),
        Shader.PropertyToID("_ImpactB"),
        Shader.PropertyToID("_ImpactC")
    };

    private Image background;
    private Material originalMaterial;
    private Material waveMaterial;
    private Sprite boundSprite;
    private float elapsed;
    private int nextImpact;

    internal void Bind(Image image)
    {
        if (background == image) return;
        ResetRipple();
        background = image;
    }

    internal void Impact(RectTransform source, float strength)
    {
        if (!isActiveAndEnabled || background == null || !background.isActiveAndEnabled ||
            !ReadyMaterial()) return;

        Rect drawing = BackgroundRect();
        Vector2 position = drawing.center;
        if (source != null)
            position = (Vector2)background.rectTransform.InverseTransformPoint(
                source.TransformPoint(source.rect.center));
        Vector2 origin = new Vector2(
            Mathf.InverseLerp(drawing.xMin, drawing.xMax, position.x),
            Mathf.InverseLerp(drawing.yMin, drawing.yMax, position.y));
        waveMaterial.SetVector(ImpactIds[nextImpact], new Vector4(origin.x, origin.y, elapsed, strength));
        nextImpact = (nextImpact + 1) % ImpactIds.Length;
        ApplySettings(drawing);
    }

    internal void ResetRipple()
    {
        if (waveMaterial != null)
        {
            if (background != null && background.material == waveMaterial)
                background.material = originalMaterial;
            Destroy(waveMaterial);
        }
        waveMaterial = null;
        originalMaterial = null;
        boundSprite = null;
        elapsed = 0f;
        nextImpact = 0;
    }

    private bool ReadyMaterial()
    {
        Sprite sprite = background.overrideSprite;
        if (sprite == null || (sprite.packed && sprite.packingRotation != SpritePackingRotation.None))
            return false;
        if (waveMaterial == null)
        {
            if (rippleShader == null) rippleShader = Resources.Load<Shader>("SertaoMainMenuBorderRipple");
            if (rippleShader == null || !rippleShader.isSupported) return false;
            originalMaterial = background.material;
            waveMaterial = new Material(rippleShader)
            {
                name = "Sertão - onda da moldura do menu",
                hideFlags = HideFlags.DontSave
            };
            background.material = waveMaterial;
        }
        if (boundSprite != sprite)
        {
            waveMaterial.SetVector(SpriteUvId, DataUtility.GetOuterUV(sprite));
            boundSprite = sprite;
        }
        return true;
    }

    private Rect BackgroundRect()
    {
        Rect rect = background.GetPixelAdjustedRect();
        if (background.type == Image.Type.Simple && background.preserveAspect && boundSprite != null)
        {
            Vector2 pixels = boundSprite.rect.size;
            Vector2 pivot = background.rectTransform.pivot;
            float scale = Mathf.Min(rect.width / pixels.x, rect.height / pixels.y);
            Vector2 size = pixels * scale;
            rect.position += Vector2.Scale(rect.size - size, pivot);
            rect.size = size;
        }
        return rect;
    }

    private void ApplySettings(Rect rect)
    {
        waveMaterial.SetVector(WaveId, new Vector4(edgeBand, impactAmplitude, idleAmplitude, idleSpeed));
        waveMaterial.SetVector(SizeClockId, new Vector4(
            Mathf.Max(1f, rect.width), Mathf.Max(1f, rect.height), elapsed, 0f));
    }

    private void Update()
    {
        if (waveMaterial == null || background == null || !background.isActiveAndEnabled) return;
        if (!ReadyMaterial())
        {
            ResetRipple();
            return;
        }
        // O menu roda com timeScale zero; usa a mesma proteção contra saltos da entrada.
        elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        ApplySettings(BackgroundRect());
    }

    private void OnDisable() => ResetRipple();

    [ContextMenu("Testar ondulação em Play Mode")]
    private void PreviewImpact()
    {
        if (Application.isPlaying) Impact(null, 1f);
    }
}
