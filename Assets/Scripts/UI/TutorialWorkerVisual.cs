using UnityEngine;
using UnityEngine.UI;

// Draws the live sprite on the tutorial Canvas while its original keeps all world interaction.
public sealed class TutorialWorkerVisual : Graphic
{
    private SpriteRenderer source;
    private Camera worldCamera;
    private Sprite sprite;
    private Vector2[] vertices;
    private Vector2[] uv;
    private ushort[] triangles;
    private Texture texture;

    public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;

    internal void Refresh(SpriteRenderer renderer, Camera camera)
    {
        source = renderer;
        worldCamera = camera;
        if (sprite != source.sprite)
        {
            sprite = source.sprite;
            vertices = sprite.vertices;
            uv = sprite.uv;
            triangles = sprite.triangles;
            if (texture != sprite.texture)
            {
                texture = sprite.texture;
                SetMaterialDirty();
            }
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (sprite == null || source == null || worldCamera == null) return;
        Color32 tint = source.color;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 vertex = vertices[i];
            if (source.flipX) vertex.x = -vertex.x;
            if (source.flipY) vertex.y = -vertex.y;
            Vector3 screen = worldCamera.WorldToScreenPoint(source.transform.TransformPoint(vertex));
            if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screen, null, out Vector2 local))
            {
                mesh.Clear();
                return;
            }
            mesh.AddVert(local, tint, uv[i]);
        }
        for (int i = 0; i < triangles.Length; i += 3)
            mesh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }
}
