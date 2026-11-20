using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class SertaoButtonHoverPalette
{
    internal static Color Ivory(float alpha) => new Color(1f, 253f / 255f, 245f / 255f, alpha);

    internal static Color Pressed(float alpha) => new Color(241f / 255f, 240f / 255f, 236f / 255f, alpha);

    internal static Color Lighten(Color color) => Color.Lerp(color, Ivory(color.a), 0.25f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= ApplyToScene;
        SceneManager.sceneLoaded += ApplyToScene;
    }

    private static void ApplyToScene(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                Apply(button);
    }

    internal static void Apply(Button button)
    {
        if (button.transition != Selectable.Transition.ColorTint) return;
        ColorBlock colors = WithFeedbackColors(button.colors);
        if (!button.colors.Equals(colors)) button.colors = colors;
    }

    internal static ColorBlock WithFeedbackColors(ColorBlock colors)
    {
        colors.highlightedColor = ReplaceYellow(colors.highlightedColor);
        colors.pressedColor = Pressed(colors.pressedColor.a);
        colors.selectedColor = ReplaceYellow(colors.selectedColor);
        return colors;
    }

    private static Color ReplaceYellow(Color color)
    {
        return color.r - color.b >= 0.10f && color.g - color.b >= 0.07f &&
            Mathf.Abs(color.r - color.g) <= 0.26f ? Ivory(color.a) : color;
    }
}
