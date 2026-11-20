using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameFeedback : MonoBehaviour
{
    [SerializeField] private Text messageText;
    [SerializeField, Min(0)] private float feedbackPunchDuration = 0.2f;
    [SerializeField, Min(0)] private float feedbackPunchScale = 0.08f;

    public void ShowMessage(string message)
    {
        if (messageText == null)
            return;
        messageText.text = message;
        var rect = messageText.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOPunchScale(Vector3.one * feedbackPunchScale, feedbackPunchDuration);
    }
}
