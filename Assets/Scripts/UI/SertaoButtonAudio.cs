using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class SertaoButtonAudio : MonoBehaviour, IPointerEnterHandler
{
    private Button button;
    private bool cardFeedback;

    internal static void Bind(Button target, bool useCardFeedback = false)
    {
        if (!Application.isPlaying || target == null) return;
        if (!target.TryGetComponent(out SertaoButtonAudio feedback))
            feedback = target.gameObject.AddComponent<SertaoButtonAudio>();
        if (useCardFeedback) feedback.cardFeedback = true;
        feedback.button = target;
        target.onClick.RemoveListener(feedback.PlayClick);
        target.onClick.AddListener(feedback.PlayClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(PlayClick);
    }

    private void PlayClick()
    {
        if (!cardFeedback) AudioManager.Play(SoundCue.UiClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!cardFeedback && !eventData.dragging && button != null &&
            button.isActiveAndEnabled && button.IsInteractable())
            AudioManager.Play(SoundCue.UiHover);
    }
}
