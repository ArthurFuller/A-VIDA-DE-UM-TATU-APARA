using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MandalaPlowedGround : MonoBehaviour
{
    private const float GridStep = 0.042f;
    private const float PatternSpan = 4.8f;
    private const float DistanceRange = 0.5f;
    private const float ShapeMargin = 0.18f;
    private const float BlendDuration = 0.42f;

    private static readonly int GroundMode = Shader.PropertyToID("_MandalaGround");
    private static readonly int MaskFrom = Shader.PropertyToID("_GroundMaskFrom");
    private static readonly int MaskTo = Shader.PropertyToID("_GroundMaskTo");
    private static readonly int MaskTransform = Shader.PropertyToID("_GroundMaskTransform");
    private static readonly int Blend = Shader.PropertyToID("_GroundBlend");

    private readonly List<Plot> plowedPlots = new List<Plot>();
    private IReadOnlyList<Plot> plots;
    private MandalaPlowedGroundSettings settings;
    private float plotFill;
    private float cornerRoundness;
    private float joinWidth;
    private MeshRenderer surface;
    private Material material;
    private Mesh mesh;
    private Texture2D oldMask;
    private Texture2D newMask;
    private Color32[] oldPixels;
    private Color32[] newPixels;
    private Color32[] nextPixels;
    private Rect domain;
    private int width;
    private int height;
    private bool dirty;
    private float elapsed = BlendDuration;
    private float blend = 1f;
    private float size;
    private float aspect;
    private float irregularity;
    private float seed;
    private Vector2 noiseSeed;

    internal static MandalaPlowedGround GetOrCreate(Plot origin, IReadOnlyList<Plot> plots)
    {
        Transform parent = origin.transform.parent;
        if (parent == null) return null;

        var ground = parent.GetComponent<MandalaPlowedGround>();
        if (ground != null) return ground;

        var settings = Resources.Load<MandalaPlowedGroundSettings>("MandalaPlowedGround");
        if (settings == null || settings.Material == null || !settings.Material.HasProperty(GroundMode) ||
            !settings.Material.shader.isSupported) return null;

        ground = parent.gameObject.AddComponent<MandalaPlowedGround>();
        ground.Initialize(origin, plots, settings);
        return ground;
    }

    internal void Invalidate() => dirty = true;

    private void Initialize(Plot origin, IReadOnlyList<Plot> allPlots, MandalaPlowedGroundSettings settings)
    {
        plots = allPlots;
        this.settings = settings;
        Bounds bounds = origin.PlowedGroundBounds;
        foreach (Plot plot in plots)
            if (plot != null && plot.transform.parent == transform && !plot.IsWater &&
                plot.PlowedGroundBounds.size.x > 0f)
                bounds.Encapsulate(plot.PlowedGroundBounds);

        // A malha dá espaço para a terra solta e para os bicos do contorno.
        domain = Rect.MinMaxRect(bounds.min.x - 0.55f, bounds.min.z - 0.55f,
            bounds.max.x + 0.55f, bounds.max.z + 0.55f);
        width = Mathf.Clamp(Mathf.CeilToInt(domain.width * 44f), 16, 640);
        height = Mathf.Clamp(Mathf.CeilToInt(domain.height * 44f), 16, 640);
        oldPixels = new Color32[width * height];
        newPixels = new Color32[width * height];
        nextPixels = new Color32[width * height];
        oldMask = MakeMask("Terra arada — máscara anterior", oldPixels);
        newMask = MakeMask("Terra arada — máscara atual", newPixels);

        material = new Material(settings.Material) { name = "Terra arada — Mandala" };
        material.SetFloat(GroundMode, 1f);
        material.SetFloat(Blend, 1f);
        material.SetTexture(MaskFrom, oldMask);
        material.SetTexture(MaskTo, newMask);
        Vector2 uvScale = new Vector2(PatternSpan / domain.width, PatternSpan / domain.height);
        material.SetVector(MaskTransform, new Vector4(uvScale.x, uvScale.y,
            0.5f - 0.5f * uvScale.x, 0.5f - 0.5f * uvScale.y));

        size = material.GetFloat("_Tamanho");
        aspect = Mathf.Max(0.1f, material.GetFloat("_Achatamento"));
        irregularity = material.GetFloat("_Irregularidade");
        seed = material.GetFloat("_Semente");
        noiseSeed = new Vector2(seed * 17.13f + 3.1f, seed * 7.71f + 1.7f);

        var child = new GameObject("Terra arada — superfície da Mandala", typeof(MeshFilter), typeof(MeshRenderer));
        child.layer = origin.gameObject.layer;
        child.transform.position = new Vector3(domain.center.x, origin.transform.position.y + settings.GroundOffset,
            domain.center.y);
        child.transform.SetParent(transform, true);
        mesh = BuildMesh(domain.size);
        child.GetComponent<MeshFilter>().sharedMesh = mesh;
        surface = child.GetComponent<MeshRenderer>();
        surface.sharedMaterial = material;
        surface.shadowCastingMode = ShadowCastingMode.Off;
        surface.receiveShadows = true;
        surface.enabled = false;
    }

    private Texture2D MakeMask(string label, Color32[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = label,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    private void LateUpdate()
    {
        if (settings == null) return;
        ReadShapeSettings();
        if (dirty)
        {
            dirty = false;
            RebuildMask();
        }

        if (elapsed >= BlendDuration) return;
        elapsed = Mathf.Min(BlendDuration, elapsed + Time.deltaTime);
        float t = elapsed / BlendDuration;
        blend = t * t * (3f - 2f * t);
        material.SetFloat(Blend, blend);
    }

    private void ReadShapeSettings()
    {
        if (plotFill == settings.PlotFill && cornerRoundness == settings.CornerRoundness &&
            joinWidth == settings.JoinWidth) return;

        plotFill = settings.PlotFill;
        cornerRoundness = settings.CornerRoundness;
        joinWidth = settings.JoinWidth;
        Invalidate();
    }

    private void RebuildMask()
    {
        plowedPlots.Clear();
        Array.Clear(nextPixels, 0, nextPixels.Length);
        foreach (Plot plot in plots)
        {
            if (plot == null || plot.transform.parent != transform || !plot.UsesPlowedGround) continue;
            plowedPlots.Add(plot);
            StampPlot(plot.PlowedGroundBounds);
        }

        foreach (Plot plot in plowedPlots)
        {
            Plot east = null, north = null;
            foreach (Plot other in plowedPlots)
            {
                if (plot == other || !plot.CanJoinPlowedGround(other)) continue;
                Vector3 offset = other.transform.position - plot.transform.position;
                if (offset.x > 0.2f && (east == null || other.transform.position.x < east.transform.position.x))
                    east = other;
                if (offset.z > 0.2f && (north == null || other.transform.position.z < north.transform.position.z))
                    north = other;
            }

            if (east != null) StampBridge(plot.PlowedGroundBounds, east.PlowedGroundBounds, true);
            if (north != null) StampBridge(plot.PlowedGroundBounds, north.PlowedGroundBounds, false);
            if (east != null && north != null) StampInnerCorner(plot, east, north);
        }

        int i = 0;
        while (i < nextPixels.Length && newPixels[i].r == nextPixels[i].r) i++;
        if (i == nextPixels.Length) return;

        // Se outra ação acontecer durante a transição, parte da máscara que já está visível.
        for (i = 0; i < oldPixels.Length; i++)
            oldPixels[i].r = (byte)Mathf.RoundToInt(Mathf.Lerp(oldPixels[i].r, newPixels[i].r, blend));

        (newPixels, nextPixels) = (nextPixels, newPixels);
        oldMask.SetPixels32(oldPixels);
        newMask.SetPixels32(newPixels);
        oldMask.Apply(false);
        newMask.Apply(false);
        surface.enabled = true;
        elapsed = blend = 0f;
        material.SetFloat(Blend, 0f);
    }

    private void StampPlot(Bounds bounds)
    {
        Vector2 center = GroundPoint(bounds.center);
        Vector2 radius = new Vector2(bounds.extents.x, bounds.extents.z) * plotFill;
        float extent = Mathf.Min(radius.x, radius.y);
        if (extent <= 0f) return;
        float round = extent * cornerRoundness;
        float edgeExpansion = Mathf.Max(0f, (size * (1f + 0.21f * irregularity) +
            0.08f * irregularity - 1f) * extent);
        Vector2 envelope = radius + Vector2.one * edgeExpansion;
        Stamp(center - envelope, center + envelope, point =>
        {
            Vector2 local = point - center;
            var p = new Vector2(local.x / radius.x, local.y / radius.y * aspect);
            float angle = Mathf.Atan2(p.y, p.x);
            float phase = seed * 1.37f;
            float edge = size * (1f + irregularity * (0.10f * Mathf.Sin(3f * angle + 1.3f + phase) +
                0.06f * Mathf.Sin(5f * angle + 0.4f + phase * 1.7f) +
                0.05f * Mathf.Sin(8f * angle + 2f + phase * 2.3f))) +
                (Fbm(p * 1.6f + noiseSeed) - 0.5f) * 0.16f * irregularity;
            Vector2 inset = new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.y)) - radius + Vector2.one * round;
            float outside = Vector2.Max(inset, Vector2.zero).magnitude;
            float inside = Mathf.Min(Mathf.Max(inset.x, inset.y), 0f);
            return round - outside - inside + (edge - 1f) * extent;
        });
    }

    private void StampBridge(Bounds first, Bounds second, bool alongX)
    {
        Vector2 a = GroundPoint(first.center), b = GroundPoint(second.center);
        Vector2 axis = b - a;
        float axisLengthSquared = axis.sqrMagnitude;
        float radius = (alongX ? Mathf.Min(first.extents.z, second.extents.z) :
            Mathf.Min(first.extents.x, second.extents.x)) * plotFill * size * joinWidth;
        Vector2 padding = Vector2.one * radius;
        Stamp(Vector2.Min(a, b) - padding, Vector2.Max(a, b) + padding, point =>
        {
            float t = Mathf.Clamp01(Vector2.Dot(point - a, axis) / axisLengthSquared);
            float widthAtPoint = radius * (1f - 0.035f * Mathf.Sin(Mathf.PI * t));
            float edgeNoise = (Fbm(point * 3.15f + noiseSeed) - 0.5f) * 0.09f;
            return widthAtPoint - (point - a - axis * t).magnitude + edgeNoise;
        }, 0.16f);
    }

    private void StampInnerCorner(Plot origin, Plot east, Plot north)
    {
        foreach (Plot candidate in plowedPlots)
        {
            if (candidate == origin || !east.CanJoinPlowedGround(candidate) ||
                !north.CanJoinPlowedGround(candidate)) continue;

            Vector2 a = GroundPoint(origin.PlowedGroundBounds.center);
            Vector2 b = GroundPoint(candidate.PlowedGroundBounds.center);
            if (b.x - a.x <= 0.2f || b.y - a.y <= 0.2f) continue;
            Vector2 center = (a + b) * 0.5f;
            Vector2 radius = (b - a) * 0.46f;
            float extent = Mathf.Min(radius.x, radius.y);
            Stamp(center - radius, center + radius, point =>
            {
                Vector2 local = point - center;
                return (1f - new Vector2(local.x / radius.x, local.y / radius.y).magnitude) * extent;
            }, 0.13f);
            return;
        }
    }

    private void Stamp(Vector2 lower, Vector2 upper, Func<Vector2, float> distance, float smooth = 0f)
    {
        lower -= Vector2.one * ShapeMargin;
        upper += Vector2.one * ShapeMargin;
        float dx = domain.width / width, dz = domain.height / height;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((lower.x - domain.xMin) / dx));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((lower.y - domain.yMin) / dz));
        int x1 = Mathf.Min(width, Mathf.CeilToInt((upper.x - domain.xMin) / dx));
        int z1 = Mathf.Min(height, Mathf.CeilToInt((upper.y - domain.yMin) / dz));
        for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                var point = new Vector2(domain.xMin + (x + 0.5f) * dx, domain.yMin + (z + 0.5f) * dz);
                float next = distance(point);
                int pixel = z * width + x;
                float current = (nextPixels[pixel].r / 255f - 0.5f) * (2f * DistanceRange);
                float field = Mathf.Max(current, next);
                if (smooth > 0f)
                {
                    float overlap = Mathf.Max(0f, smooth - Mathf.Abs(current - next)) / smooth;
                    field += overlap * overlap * smooth * 0.25f;
                }

                nextPixels[pixel].r = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(0.5f + field / (2f * DistanceRange)) * 255f);
            }
    }

    private static Vector2 GroundPoint(Vector3 point) => new Vector2(point.x, point.z);

    private static float Hash(Vector2 p)
    {
        p.x = Fraction(p.x * 123.34f);
        p.y = Fraction(p.y * 456.21f);
        float dot = Vector2.Dot(p, p + Vector2.one * 45.32f);
        return Fraction((p.x + dot) * (p.y + dot));
    }

    private static float Noise(Vector2 point)
    {
        var cell = new Vector2(Mathf.Floor(point.x), Mathf.Floor(point.y));
        Vector2 f = point - cell;
        float x = f.x * f.x * (3f - 2f * f.x), y = f.y * f.y * (3f - 2f * f.y);
        return Mathf.Lerp(Mathf.Lerp(Hash(cell), Hash(cell + Vector2.right), x),
            Mathf.Lerp(Hash(cell + Vector2.up), Hash(cell + Vector2.one), x), y);
    }

    private static float Fbm(Vector2 point) =>
        Noise(point) * 0.67f + Noise(point * 2.03f + Vector2.one * 17.1f) * 0.33f;

    private static float Fraction(float value) => value - Mathf.Floor(value);

    private static Mesh BuildMesh(Vector2 size)
    {
        int columns = Mathf.Clamp(Mathf.CeilToInt(size.x / GridStep), 1, 256);
        int rows = Mathf.Clamp(Mathf.CeilToInt(size.y / GridStep), 1, 256);
        int stride = columns + 1;
        var vertices = new Vector3[stride * (rows + 1)];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var indices = new int[columns * rows * 6];
        int triangle = 0;
        for (int z = 0; z <= rows; z++)
            for (int x = 0; x <= columns; x++)
            {
                int i = z * stride + x;
                float px = (x / (float)columns - 0.5f) * size.x;
                float pz = (z / (float)rows - 0.5f) * size.y;
                vertices[i] = new Vector3(px, 0f, pz);
                normals[i] = Vector3.up;
                uv[i] = new Vector2(0.5f + px / PatternSpan, 0.5f + pz / PatternSpan);
                if (x == columns || z == rows) continue;
                indices[triangle++] = i;
                indices[triangle++] = i + stride;
                indices[triangle++] = i + 1;
                indices[triangle++] = i + 1;
                indices[triangle++] = i + stride;
                indices[triangle++] = i + stride + 1;
            }

        return new Mesh
        {
            name = "Terra arada — malha contínua",
            indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
            vertices = vertices,
            normals = normals,
            uv = uv,
            triangles = indices,
            bounds = new Bounds(Vector3.up * 0.20f, new Vector3(size.x, 0.65f, size.y))
        };
    }

    private void OnDestroy()
    {
        if (surface != null) Destroy(surface.gameObject);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
        if (oldMask != null) Destroy(oldMask);
        if (newMask != null) Destroy(newMask);
    }
}
