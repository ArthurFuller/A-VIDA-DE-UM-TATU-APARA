using UnityEngine;
using UnityEngine.UI;

public sealed class CardChoiceHitArea : MonoBehaviour, ICanvasRaycastFilter
{
    private RectTransform panel;
    private RectTransform card;
    private Rect area;

    public void Configure(RectTransform panelRect, Rect hitArea)
    {
        panel = panelRect;
        card = (RectTransform)transform;
        area = hitArea;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        return panel != null &&
            RectTransformUtility.RectangleContainsScreenPoint(panel, screenPoint, eventCamera) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(card, screenPoint,
                eventCamera, out Vector2 local) && area.Contains(local);
    }
}
