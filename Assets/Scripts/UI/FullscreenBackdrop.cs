using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FullscreenBackdrop : BaseMeshEffect
{
    internal const float EdgePixels = 2f;

    internal static void Cover(Component owner)
    {
        if (owner == null || !owner.TryGetComponent(out Image image) ||
            image.overrideSprite != null || image.TryGetComponent<FullscreenBackdrop>(out _))
            return;
        image.gameObject.AddComponent<FullscreenBackdrop>();
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        Canvas canvas = graphic != null ? graphic.canvas : null;
        if (!IsActive() || canvas == null || mesh.currentVertCount != 4) return;

        Canvas rootCanvas = canvas.rootCanvas;
        if (rootCanvas.renderMode == RenderMode.WorldSpace) return;
        RectTransform viewport = (RectTransform)rootCanvas.transform;
        RectTransform rect = graphic.rectTransform;
        Vector2 padding = Vector2.one * (EdgePixels / Mathf.Max(rootCanvas.scaleFactor, 0.01f));
        Vector2 min = rect.InverseTransformPoint(viewport.TransformPoint(viewport.rect.min - padding));
        Vector2 max = rect.InverseTransformPoint(viewport.TransformPoint(viewport.rect.max + padding));
        Vector2 center = rect.rect.center;

        // Amplia só a malha do fundo. Os controles filhos continuam no mesmo RectTransform.
        UIVertex vertex = default;
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            Vector3 position = vertex.position;
            position.x = position.x < center.x ? Mathf.Min(position.x, min.x) : Mathf.Max(position.x, max.x);
            position.y = position.y < center.y ? Mathf.Min(position.y, min.y) : Mathf.Max(position.y, max.y);
            vertex.position = position;
            mesh.SetUIVertex(vertex, i);
        }
    }
}
