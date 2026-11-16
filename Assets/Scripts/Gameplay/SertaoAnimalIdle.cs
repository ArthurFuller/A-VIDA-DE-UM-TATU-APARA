using UnityEngine;

public sealed class SertaoAnimalIdle : MonoBehaviour
{
    private Sprite[] frames;
    private SpriteRenderer artwork;
    private Camera view;
    private float time;

    public void Setup(AnimalKind kind, Sprite[] idleFrames)
    {
        frames = idleFrames;
        time = ((uint)GetInstanceID() % 11) * 0.073f;
        bool cow = kind == AnimalKind.Cow;
        Sprite first = frames[0];

        var child = new GameObject("Arte do animal", typeof(SpriteRenderer));
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;
        artwork = child.GetComponent<SpriteRenderer>();
        artwork.sprite = first;
        artwork.flipX = cow;

        // Medidas da arte dentro de cada quadro de 512 px, no espaço do sprite.
        float fullHeight = (cow ? 0.89f / 244f : 0.54f / 214f) * 512f;
        float fullWidth = fullHeight * first.rect.width / first.rect.height;
        child.transform.localScale = Vector3.one * (fullHeight * first.pixelsPerUnit / first.rect.height);
        child.transform.localPosition = new Vector3(
            (first.pivot.x / first.rect.width - (cow ? 0.55f : 0.52f)) * fullWidth * (cow ? -1f : 1f),
            0.03f + (first.pivot.y / first.rect.height - (cow ? 140f : 119f) / 512f) * fullHeight,
            0f);
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (artwork == null) return;
        if (view == null) view = Camera.main;
        if (view != null)
            transform.rotation = Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f);

        time += Time.deltaTime;
        int frame = Mathf.FloorToInt(time * 8f) % frames.Length;
        if (artwork.sprite != frames[frame]) artwork.sprite = frames[frame];
    }
}
