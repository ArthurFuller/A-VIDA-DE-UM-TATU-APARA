using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SertaoMouseCursor : MonoBehaviour
{
    private const float ClickPreviewDuration = 0.10f;
    // The palm stays aligned when the claw closes, keeping the same click point.
    private static readonly Vector2 Hotspot = new Vector2(2f, 2f);

    private Texture2D normalTexture;
    private Texture2D pressedTexture;
    private float pressedUntil;
    private bool showingPressed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Texture2D normal = Resources.Load<Texture2D>("SertaoCursors/Normal");
        Texture2D pressed = Resources.Load<Texture2D>("SertaoCursors/Pressed");
        if (normal == null || pressed == null) return;

        var component = new GameObject("Cursor do Sertão").AddComponent<SertaoMouseCursor>();
        component.normalTexture = normal;
        component.pressedTexture = pressed;
        DontDestroyOnLoad(component.gameObject);
        component.ShowCursor(false);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (normalTexture == null || !Application.isFocused || mouse == null) return;

        float now = Time.unscaledTime;
        if (mouse.leftButton.wasPressedThisFrame)
            pressedUntil = now + ClickPreviewDuration;

        bool pressed = mouse.leftButton.isPressed || now < pressedUntil;
        if (pressed != showingPressed) ShowCursor(pressed);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        pressedUntil = 0f;
        if (normalTexture != null && (hasFocus || showingPressed)) ShowCursor(false);
    }

    private void ShowCursor(bool pressed)
    {
        showingPressed = pressed;
        Cursor.SetCursor(pressed ? pressedTexture : normalTexture, Hotspot, CursorMode.Auto);
    }

    private void OnDestroy()
    {
        if (normalTexture != null) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
