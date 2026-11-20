using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SertaoCoinChoiceButton : Button
{
    private bool pointerInside;
    private bool pointerFocus;

    public override void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        base.OnPointerEnter(eventData);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        base.OnPointerExit(eventData);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && IsInteractable())
        {
            pointerFocus = true;
            pointerInside = true;
        }
        base.OnPointerDown(eventData);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && pointerFocus)
        {
            var hit = eventData.pointerCurrentRaycast.gameObject;
            if (hit == null || (hit != gameObject && !hit.transform.IsChildOf(transform)))
                OnPointerExit(eventData);
        }
        base.OnPointerUp(eventData);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        pointerFocus = eventData is PointerEventData;
        base.OnSelect(eventData);
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        pointerFocus = false;
        base.OnDeselect(eventData);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        if (pointerFocus && !pointerInside &&
            (state == SelectionState.Pressed || state == SelectionState.Selected))
            state = SelectionState.Normal;
        base.DoStateTransition(state, instant);
    }

    protected override void OnDisable()
    {
        pointerInside = false;
        pointerFocus = false;
        base.OnDisable();
    }
}
