using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class LetterJumpText : Text
{
    [SerializeField, Min(0)] private float jumpHeight = 14f;
    private float revealProgress;
    private bool normalizeToMesh;

    public void Reveal(string message, float duration) => CreateReveal(message, duration, false);

    internal Tween PrepareTimedReveal(string message, float duration) =>
        CreateReveal(message, duration, true);

    private Tween CreateReveal(string message, float duration, bool useMeshCount)
    {
        DOTween.Kill(this);
        text = message;
        normalizeToMesh = useMeshCount;
        revealProgress = 0;
        SetVerticesDirty();
        float end = normalizeToMesh ? 1f : Mathf.Max(1, message.Length);
        return DOTween.To(() => revealProgress, value =>
        {
            revealProgress = value;
            SetVerticesDirty();
        }, end, duration).SetEase(Ease.Linear).SetTarget(this);
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        base.OnPopulateMesh(vertexHelper);
        int count = vertexHelper.currentVertCount / 4;
        float progress = normalizeToMesh ? revealProgress * count : revealProgress;
        int shown = Mathf.CeilToInt(progress);
        float fraction = progress - Mathf.Floor(progress);
        for (int i = 0; i < count; i++)
        {
            bool hidden = i >= shown;
            float offset = i == shown - 1 && fraction > 0.001f ? jumpHeight * (1f - fraction) : 0f;
            for (int corner = 0; corner < 4; corner++)
            {
                int index = i * 4 + corner;
                UIVertex vertex = new UIVertex();
                vertexHelper.PopulateUIVertex(ref vertex, index);
                if (hidden) vertex.color.a = 0;
                vertex.position.y += offset;
                vertexHelper.SetUIVertex(vertex, index);
            }
        }
    }
}
