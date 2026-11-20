using UnityEngine;
using UnityEngine.UI;

// The highlight uses the default UI material, including on renderers that fall back to tilt.
public sealed class InspectionPaperHighlight : MaskableGraphic
{
    private const int Steps = 16;
    private Vector2 pointer;
    private Vector2 pictureSize;
    private float angle;
    private float opacity;

    public void Present(Vector2 cursor, Vector2 size, float perspectiveAngle, float alpha)
    {
        if ((pointer - cursor).sqrMagnitude < 0.00000625f &&
            pictureSize == size && Mathf.Approximately(angle, perspectiveAngle) &&
            Mathf.Abs(opacity - alpha) < 0.002f && (opacity == 0f) == (alpha == 0f))
            return;

        pointer = cursor;
        pictureSize = size;
        angle = perspectiveAngle;
        opacity = alpha;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        if (opacity <= 0f || pictureSize.x <= 0f || pictureSize.y <= 0f) return;
        float yaw = pointer.x * angle;
        float pitch = -pointer.y * angle;
        float cosYaw = Mathf.Cos(yaw), sinYaw = Mathf.Sin(yaw);
        float cosPitch = Mathf.Cos(pitch), sinPitch = Mathf.Sin(pitch);
        Vector2 light = new Vector2(0.5f, 0.51f) + pointer * 0.185f;
        var gold = new Color(1f, 0.94f, 0.77f, 1f);
        for (int y = 0; y <= Steps; y++)
        {
            for (int x = 0; x <= Steps; x++)
            {
                var sample = new Vector2(x / (float)Steps, y / (float)Steps);
                var uv = new Vector2(Mathf.Lerp(0.18f, 0.82f, sample.x),
                    Mathf.Lerp(0.085f, 0.775f, sample.y));
                Vector2 offset = uv - light;
                float band = Mathf.Abs(offset.x * 0.86f - offset.y * 0.51f);
                float core = 1f - Mathf.SmoothStep(0f, 1f, (band - 0.012f) / 0.15f);
                float edge = Mathf.Min(Mathf.Min(sample.x, 1f - sample.x),
                    Mathf.Min(sample.y, 1f - sample.y));
                float mask = Mathf.SmoothStep(0f, 1f, edge / 0.16f);
                float falloff = 1f - Mathf.SmoothStep(0f, 1f, (offset.magnitude - 0.14f) / 0.36f);
                gold.a = opacity * core * mask * falloff;

                Vector2 local = Vector2.Scale(uv - Vector2.one * 0.5f, pictureSize);
                float depth = -local.x * sinYaw;
                float z = local.y * sinPitch + depth * cosPitch;
                float perspective = 3.2f / (3.2f - z / pictureSize.y);
                var position = new Vector2(local.x * cosYaw,
                    local.y * cosPitch - depth * sinPitch) * perspective;
                vertices.AddVert(position, gold, Vector2.zero);
            }
        }

        for (int y = 0; y < Steps; y++)
        {
            for (int x = 0; x < Steps; x++)
            {
                int i = y * (Steps + 1) + x;
                vertices.AddTriangle(i, i + Steps + 1, i + Steps + 2);
                vertices.AddTriangle(i, i + Steps + 2, i + 1);
            }
        }
    }
}
