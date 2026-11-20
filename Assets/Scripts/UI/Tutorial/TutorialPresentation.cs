using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialPresentation : ScriptableObject
{
    public enum Page
    {
        [InspectorName("Evento")] Event,
        [InspectorName("Recursos")] Resources,
        [InspectorName("Contador de dias")] Day,
        [InspectorName("Linha do Tempo · abrir")] Timeline,
        [InspectorName("Linha do Tempo · dia atual")] TimelineDay,
        [InspectorName("Linha do Tempo · registro")] TimelineRecord,
        [InspectorName("Cartas · inspecionar")] Inspect,
        [InspectorName("Cartas · jogar")] PlayCard,
        [InspectorName("Acompanhar o tatu")] Follow,
        [InspectorName("Mercearia · abrir")] Shop,
        [InspectorName("Mercearia · comprar e vender")] ShopOverview,
        [InspectorName("Mercearia · aba Encomendas")] OrdersTab,
        [InspectorName("Encomendas · recompensas")] OrdersIntro,
        [InspectorName("Encomendas · aceitar")] AcceptOrder,
        [InspectorName("Encomendas · fechar")] CloseShop,
        [InspectorName("Missões")] Missions,
        [InspectorName("Finalizar dia")] EndDay,
        [InspectorName("Alimentação")] Feeding,
        [InspectorName("Carta do dia seguinte")] Reward,
        [InspectorName("Despedida do tutorial")] Farewell,
        [InspectorName("Câmera")] Camera
    }

    public enum SpriteLayout
    {
        [InspectorName("Manter proporção")] Fit,
        [InspectorName("Esticar")] Stretch,
        [InspectorName("Nove fatias")] Sliced
    }

    [Serializable]
    internal struct Insets
    {
        [Min(0f)] public float left, right, top, bottom;

        public Insets(float left, float right, float top, float bottom)
        {
            this.left = left;
            this.right = right;
            this.top = top;
            this.bottom = bottom;
        }
    }

    [Serializable]
    internal sealed class TextAppearance
    {
        public Color color;
        public Font font;
        [Min(1)] public int size;
        public FontStyle fontStyle;
        public TextAnchor alignment;
        [Min(0.1f)] public float lineSpacing = 1f;
        public bool fit;
        [Min(1)] public int minimumSize;
        public Insets insets;

        public TextAppearance(Color color, int size, TextAnchor alignment)
        {
            this.color = color;
            this.size = size;
            this.alignment = alignment;
            minimumSize = size;
        }
    }

    [Serializable]
    internal sealed class BoxAppearance
    {
        public Vector2 size;
        public Color tint = new Color(0f, 0f, 0f, .84f);
        public Sprite sprite;
        public bool useDefaultFrame;
        public SpriteLayout spriteLayout = SpriteLayout.Stretch;
        [Min(.05f)] public float borderThickness = 1f;
        public TextAppearance typography = new TextAppearance(TextColor, 13, TextAnchor.MiddleLeft)
        {
            fit = true,
            minimumSize = 11,
            insets = new Insets(10f, 10f, 5f, 23f)
        };

        public BoxAppearance(float width) => size = new Vector2(width, 61f);
    }

    [Serializable]
    internal sealed class ButtonAppearance
    {
        public Vector2 size;
        public Vector2 anchor;
        public Vector2 offset;
        public SpriteLayout spriteLayout = SpriteLayout.Fit;
        [Min(.05f)] public float borderThickness = 1f;
        public Color spriteTint = Color.white;
        public Color fillTint;
        public ColorBlock colors = ColorBlock.defaultColorBlock;
        public string caption;
        public TextAppearance typography;

        public ButtonAppearance(bool primary)
        {
            size = new Vector2(primary ? 62f : 76f, 17f);
            anchor = new Vector2(primary ? 1f : 0f, 0f);
            offset = new Vector2(primary ? -10f : 10f, 5f);
            fillTint = primary ? new Color(.91f, .78f, .53f) : new Color(.88f, .77f, .56f, .15f);
            caption = primary ? "Continuar" : "Pular tutorial";
            typography = new TextAppearance(primary ? Color.black : TextColor, 10, TextAnchor.MiddleCenter);
        }
    }

    [Serializable]
    private struct Tip
    {
        public Vector2 offset;
        [TextArea(2, 5)] public string text;
        public BoxAppearance box;
    }

    public const string ResourceName = "SertaoTutorialPresentation";
    public const string AssetPath = "Assets/Resources/" + ResourceName + ".asset";
    private static readonly Color TextColor = new Color(1f, .95f, .84f);

    [SerializeField] private Tip[] tips = new Tip[(int)Page.Camera + 1];
    [SerializeField] private Sprite skipSprite;
    [SerializeField] private Sprite continueSprite;
    [SerializeField] private bool showSkipLabel = true;
    [SerializeField] private bool showContinueLabel = true;
    [SerializeField] private ButtonAppearance skipStyle;
    [SerializeField] private ButtonAppearance continueStyle;
    [SerializeField] private Sprite farewellContinueSprite;
    [SerializeField] private bool showFarewellContinueLabel = true;
    [SerializeField] private ButtonAppearance farewellContinueStyle;
    [SerializeField, HideInInspector] private bool hasAppearance;
    [SerializeField, HideInInspector] private bool hasSeparateFarewellButton;

    internal Sprite SkipSprite => skipSprite;
    internal Sprite ContinueSprite => continueSprite;
    internal bool ShowSkipLabel => showSkipLabel;
    internal bool ShowContinueLabel => showContinueLabel;
    internal ButtonAppearance SkipStyle => skipStyle;
    internal ButtonAppearance ContinueStyle => continueStyle;
    internal Sprite FarewellContinueSprite => farewellContinueSprite;
    internal bool ShowFarewellContinueLabel => showFarewellContinueLabel;
    internal ButtonAppearance FarewellContinueStyle => farewellContinueStyle;

    private void OnEnable()
    {
        // Preserve the existing pages when upgrading the appearance asset from the V5.
        int previousCount = tips?.Length ?? 0;
        int count = (int)Page.Camera + 1;
        if (previousCount < count) Array.Resize(ref tips, count);
        int firstNew = hasAppearance ? previousCount : 0;
        for (int i = firstNew; i < count; i++)
            tips[i].box = (Page)i switch
            {
                Page.Farewell => CreateFarewellBox(),
                Page.Camera => new BoxAppearance(270f),
                Page.Event => new BoxAppearance(205f),
                _ => new BoxAppearance(234f)
            };
        if (!hasAppearance)
        {
            skipStyle = new ButtonAppearance(false);
            continueStyle = new ButtonAppearance(true);
            hasAppearance = true;
        }
        if (!hasSeparateFarewellButton)
        {
            // Existing shared adjustments belong to the farewell card.
            // Regular tutorial cards return to their own default appearance.
            farewellContinueSprite = continueSprite;
            showFarewellContinueLabel = showContinueLabel;
            farewellContinueStyle = continueStyle;
            continueSprite = null;
            continueStyle = new ButtonAppearance(true);
            hasSeparateFarewellButton = true;
        }
    }

    private static BoxAppearance CreateFarewellBox()
    {
        return new BoxAppearance(308f)
        {
            size = new Vector2(308f, 127f),
            tint = Color.white,
            useDefaultFrame = true,
            spriteLayout = SpriteLayout.Sliced,
            typography = new TextAppearance(new Color(.16f, .12f, .075f), 18, TextAnchor.MiddleCenter)
            {
                fit = true,
                minimumSize = 13,
                insets = new Insets(24f, 24f, 18f, 38f)
            }
        };
    }

    internal BoxAppearance Box(Page page) => tips[(int)page].box;
    internal Vector2 Offset(Page page) => tips[(int)page].offset;

    internal string Text(Page page, string fallback, int days)
    {
        string edited = tips[(int)page].text;
        return string.IsNullOrWhiteSpace(edited) ? fallback : edited.Replace("{dias}", days.ToString());
    }
}
