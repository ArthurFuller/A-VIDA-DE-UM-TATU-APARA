using UnityEngine;
using UnityEngine.UI;

public sealed class LoadingTatuFlipbook : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Sprite[] frames;
    private float clipTime;
    private int shownFrame;

    private void Update()
    {
        if (image == null || frames == null || frames.Length < 2) return;

        const float framesPerSecond = 24f;
        clipTime = Mathf.Repeat(clipTime + Mathf.Min(Time.unscaledDeltaTime, 0.10f),
            frames.Length / framesPerSecond);
        int frame = Mathf.Min((int)(clipTime * framesPerSecond), frames.Length - 1);
        if (shownFrame == frame) return;
        shownFrame = frame;
        image.sprite = frames[frame];
    }
}
