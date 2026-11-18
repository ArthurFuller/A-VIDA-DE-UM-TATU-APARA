using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameFeedback : MonoBehaviour
{
    [SerializeField] private Text messageText;
    [SerializeField] private CanvasGroup summaryGroup;
    [SerializeField] private RectTransform summaryPanel;
    [SerializeField] private Text summaryText;
    [SerializeField, Min(0)] private float summaryFadeDuration = 0.25f;
    [SerializeField, Min(0.05f)] private float summaryTransitionDuration = 0.62f;
    [SerializeField, Min(0f)] private float summarySlideDistance = 80f;
    [SerializeField, Min(0.01f)] private float summaryLineInterval = 0.18f;
    [SerializeField, Min(0)] private float feedbackPunchDuration = 0.2f;
    [SerializeField, Min(0)] private float feedbackPunchScale = 0.08f;

    private Sequence summaryRevealSequence;
    private Sequence summaryExitSequence;
    private Vector2 summaryHome;
    private Image summaryBackdrop;
    private Color summaryBackdropColor;
    public bool IsSummaryTransitioning { get; private set; }

    private void Awake()
    {
        if (summaryPanel != null) summaryHome = summaryPanel.anchoredPosition;
        if (summaryGroup != null)
        {
            summaryBackdrop = summaryGroup.GetComponent<Image>();
            if (summaryBackdrop != null) summaryBackdropColor = summaryBackdrop.color;
            FullscreenBackdrop.Cover(summaryBackdrop);
            summaryGroup.alpha = 0;
            summaryGroup.blocksRaycasts = false;
            summaryGroup.interactable = false;
        }
    }

    private void OnDestroy()
    {
        summaryRevealSequence?.Kill();
        summaryExitSequence?.Kill();
    }

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

    public void ShowSummary(string summary)
    {
        if (summaryGroup == null || summaryText == null)
            return;
        AudioManager.Play(SoundCue.UiPaper);
        if (!summaryGroup.gameObject.activeSelf) summaryGroup.gameObject.SetActive(true);
        summaryExitSequence?.Kill();
        summaryRevealSequence?.Kill();
        summaryText.text = string.Empty;
        summaryGroup.DOKill();
        IsSummaryTransitioning = true;
        if (summaryBackdrop != null)
        {
            summaryBackdrop.DOKill();
            summaryBackdrop.color = new Color(summaryBackdropColor.r, summaryBackdropColor.g,
                summaryBackdropColor.b, 0f);
            summaryBackdrop.DOFade(summaryBackdropColor.a, summaryTransitionDuration)
                .SetEase(Ease.OutSine);
        }
        summaryGroup.alpha = 1f;
        summaryGroup.blocksRaycasts = true;
        summaryGroup.interactable = true;
        if (summaryPanel != null)
        {
            summaryPanel.DOKill();
            RectTransform root = summaryGroup.transform as RectTransform;
            float below = root != null ? -root.rect.height - summarySlideDistance :
                summaryHome.y - summaryPanel.rect.height - summarySlideDistance;
            summaryPanel.anchoredPosition = new Vector2(summaryHome.x, below);
            summaryPanel.DOAnchorPos(summaryHome, summaryTransitionDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => IsSummaryTransitioning = false);
        }
        else IsSummaryTransitioning = false;

        summaryRevealSequence = DOTween.Sequence().AppendInterval(summaryTransitionDuration);
        string revealed = string.Empty;
        foreach (string line in summary.Replace("\r", string.Empty).Split('\n'))
        {
            if (line.Length == 0) continue;
            string next = revealed.Length == 0 ? line : revealed + "\n" + line;
            summaryRevealSequence.AppendCallback(() => summaryText.text = next);
            summaryRevealSequence.AppendInterval(summaryLineInterval);
            revealed = next;
        }
    }

    public void HideSummary(Func<Color?, bool> openNextScreen)
    {
        if (summaryGroup == null || !summaryGroup.gameObject.activeInHierarchy)
        {
            openNextScreen(null);
            return;
        }
        if (summaryExitSequence != null && summaryExitSequence.IsActive()) return;
        AudioManager.Play(SoundCue.UiPaper);

        IsSummaryTransitioning = true;
        summaryRevealSequence?.Kill();
        summaryGroup.DOKill();
        summaryGroup.interactable = false;
        if (summaryBackdrop != null) summaryBackdrop.DOKill();

        summaryExitSequence = DOTween.Sequence().SetTarget(this);
        if (summaryPanel != null)
        {
            summaryPanel.DOKill();
            RectTransform viewport = summaryPanel.parent as RectTransform;
            float distance = summaryPanel.rect.width + summarySlideDistance;
            if (viewport != null)
            {
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    viewport, summaryPanel);
                distance = Mathf.Max(0f, bounds.max.x - viewport.rect.xMin) +
                    summarySlideDistance;
            }
            summaryExitSequence.Append(summaryPanel.DOAnchorPosX(
                summaryPanel.anchoredPosition.x - distance, summaryTransitionDuration)
                .SetEase(Ease.InOutSine));
        }
        if (summaryPanel == null)
            summaryExitSequence.Append(summaryGroup.DOFade(0f, summaryFadeDuration));

        summaryExitSequence.OnComplete(() =>
        {
            summaryExitSequence = null;
            Color? backdropAtExit = null;
            if (summaryBackdrop != null && summaryGroup.alpha > 0f)
            {
                Color color = summaryBackdrop.color;
                color.a *= summaryGroup.alpha;
                backdropAtExit = color;
            }
            bool hasNextScreen = openNextScreen(backdropAtExit);
            summaryGroup.blocksRaycasts = false;
            if (hasNextScreen) summaryGroup.alpha = 0f;
            else if (summaryGroup.alpha > 0f) summaryGroup.DOFade(0f, summaryFadeDuration);
            IsSummaryTransitioning = false;
        });
    }
}
