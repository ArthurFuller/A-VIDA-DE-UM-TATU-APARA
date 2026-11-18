using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialSpotlight : Graphic
{
    private const int MaxCutouts = 4;
    private const float ShadeAlpha = 0.64f;
    private static readonly int CutoutsId = Shader.PropertyToID("_Cutouts");
    private static readonly int CountId = Shader.PropertyToID("_CutoutCount");
    private static readonly int AlphaId = Shader.PropertyToID("_ShadeAlpha");

    private readonly Vector4[] shaderCutouts = new Vector4[MaxCutouts];
    private readonly List<Rect> cutouts = new List<Rect>(MaxCutouts);
    private readonly List<Rect> remaining = new List<Rect>();
    private readonly List<Rect> split = new List<Rect>();
    private Material shadeMaterial;
    private Rect lastArea;

    protected override void Awake()
    {
        base.Awake();
        Shader shader = Resources.Load<Shader>("SertaoTutorialSpotlight");
        if (shader == null || !shader.isSupported) return;
        shadeMaterial = new Material(shader);
        shadeMaterial.SetFloat(AlphaId, ShadeAlpha);
        material = shadeMaterial;
    }

    protected override void OnDestroy()
    {
        if (shadeMaterial != null) Destroy(shadeMaterial);
        base.OnDestroy();
    }

    internal void SetCutouts(IReadOnlyList<Rect> targets, bool shade)
    {
        int count = targets == null ? 0 : Mathf.Min(targets.Count, MaxCutouts);
        if (enabled != shade) enabled = shade;
        if (!shade)
        {
            cutouts.Clear();
            lastArea = default;
            return;
        }

        Rect area = rectTransform.rect;
        if (area.width <= 0f || area.height <= 0f) return;
        bool changed = area != lastArea || cutouts.Count != count;
        for (int i = 0; i < count && !changed; i++)
            changed = cutouts[i] != targets[i];
        if (!changed) return;

        lastArea = area;
        cutouts.Clear();
        for (int i = 0; i < count; i++)
        {
            Rect target = targets[i];
            cutouts.Add(target);
            shaderCutouts[i] = new Vector4(
                (target.xMin - area.xMin) / area.width, (target.yMin - area.yMin) / area.height,
                (target.xMax - area.xMin) / area.width, (target.yMax - area.yMin) / area.height);
        }

        if (shadeMaterial != null)
        {
            shadeMaterial.SetVectorArray(CutoutsId, shaderCutouts);
            shadeMaterial.SetFloat(CountId, count);
        }
        else SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect area = rectTransform.rect;
        if (shadeMaterial != null)
        {
            Quad(mesh, area, Color.white);
            return;
        }

        // The same cutouts remain usable on renderers that cannot load the shader.
        remaining.Clear();
        remaining.Add(area);
        foreach (Rect hole in cutouts)
        {
            split.Clear();
            foreach (Rect piece in remaining)
            {
                float left = Mathf.Max(piece.xMin, hole.xMin);
                float right = Mathf.Min(piece.xMax, hole.xMax);
                float bottom = Mathf.Max(piece.yMin, hole.yMin);
                float top = Mathf.Min(piece.yMax, hole.yMax);
                if (left >= right || bottom >= top)
                {
                    split.Add(piece);
                    continue;
                }
                AddPiece(piece.xMin, piece.yMin, left, piece.yMax);
                AddPiece(right, piece.yMin, piece.xMax, piece.yMax);
                AddPiece(left, piece.yMin, right, bottom);
                AddPiece(left, top, right, piece.yMax);
            }
            remaining.Clear();
            remaining.AddRange(split);
        }

        Color32 tint = new Color(0f, 0f, 0f, ShadeAlpha);
        foreach (Rect piece in remaining) Quad(mesh, piece, tint);
    }

    private void AddPiece(float left, float bottom, float right, float top)
    {
        if (right > left && top > bottom) split.Add(Rect.MinMaxRect(left, bottom, right, top));
    }

    private static void Quad(VertexHelper mesh, Rect rect, Color32 tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, Vector2.up);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, Vector2.one);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, Vector2.right);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }
}
