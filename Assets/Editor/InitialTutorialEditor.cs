using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InitialTutorial))]
public sealed class InitialTutorialEditor : Editor
{
    private bool followPage = true;
    private bool showBox = true;
    private bool showSkip;
    private bool showContinue;
    private TutorialPresentation.Page selectedPage;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var guide = (InitialTutorial)target;
        var settings = AssetDatabase.LoadAssetAtPath<TutorialPresentation>(TutorialPresentation.AssetPath);
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Os ajustes da apresentação ainda não foram criados.", MessageType.Info);
            if (GUILayout.Button("Criar ajustes do tutorial"))
            {
                settings = CreateInstance<TutorialPresentation>();
                AssetDatabase.CreateAsset(settings, TutorialPresentation.AssetPath);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
            return;
        }

        var presentation = new SerializedObject(settings);
        presentation.Update();
        bool active = EditorApplication.isPlaying && guide.HasVisiblePage;
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!active))
            followPage = EditorGUILayout.Toggle("Acompanhar etapa em jogo", followPage);
        if (active && followPage) selectedPage = guide.VisiblePage;
        using (new EditorGUI.DisabledScope(active && followPage))
            selectedPage = (TutorialPresentation.Page)EditorGUILayout.EnumPopup("Etapa", selectedPage);

        showBox = EditorGUILayout.Foldout(showBox, "Caixa e texto da etapa", true);
        if (showBox)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                SerializedProperty tip = presentation.FindProperty("tips").GetArrayElementAtIndex((int)selectedPage);
                Field(tip, "offset", "Posição (X / Y)", "Deslocamento em unidades do Canvas. X positivo vai para a direita e Y positivo sobe.");
                SerializedProperty box = tip.FindPropertyRelative("box");
                Field(box, "size", "Largura / altura", "Dimensões da caixa em unidades do Canvas.");
                Field(box, "sprite", "Sprite do fundo");
                if (selectedPage == TutorialPresentation.Page.Farewell)
                    Field(box, "useDefaultFrame", "Usar moldura da cena",
                        "Usada quando nenhum sprite de fundo personalizado foi escolhido.");
                DrawSpriteLayout(box);
                Field(box, "tint", "Cor e opacidade do fundo");
                EditorGUILayout.Space();
                Field(tip, "text", "Texto da etapa", "Vazio usa o texto padrão. Use {dias} para o total da partida.");
                if (active && selectedPage == guide.VisiblePage && string.IsNullOrEmpty(tip.FindPropertyRelative("text").stringValue))
                    EditorGUILayout.HelpBox(guide.DefaultPageText, MessageType.None);
                DrawTypography(box.FindPropertyRelative("typography"));
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Botões · aplicados a todas as etapas", EditorStyles.boldLabel);
        showSkip = EditorGUILayout.Foldout(showSkip, "Pular tutorial", true);
        if (showSkip) DrawButton(presentation, "skipStyle", "skipSprite", "showSkipLabel");
        showContinue = EditorGUILayout.Foldout(showContinue, "Continuar", true);
        if (showContinue) DrawButton(presentation, "continueStyle", "continueSprite", "showContinueLabel");

        if (presentation.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(settings);
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        }
        if (GUILayout.Button("Salvar ajustes"))
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }
    }

    public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

    private static void DrawButton(SerializedProperty style, SerializedProperty sprite, SerializedProperty showLabel)
    {
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(sprite, new GUIContent("Sprite"));
            DrawSpriteLayout(style);
            Field(style, "spriteTint", "Cor e opacidade do sprite");
            Field(style, "fillTint", "Cor sem sprite");
            Field(style, "size", "Largura / altura", "Dimensões do botão e da área clicável, em unidades do Canvas.");
            Field(style, "anchor", "Âncora (X / Y)", "De 0 a 1. Zero corresponde à esquerda ou embaixo, e um à direita ou em cima.");
            Field(style, "offset", "Posição (X / Y)", "Deslocamento a partir da âncora, em unidades do Canvas.");
            Field(style, "colors", "Cores de interação", "Tom aplicado ao fundo do botão para cada estado.", true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(showLabel, new GUIContent("Mostrar rótulo"));
            if (showLabel.boolValue)
            {
                Field(style, "caption", "Rótulo");
                DrawTypography(style.FindPropertyRelative("typography"));
            }
        }
    }

    private static void DrawButton(SerializedObject presentation, string style, string sprite, string showLabel) =>
        DrawButton(presentation.FindProperty(style), presentation.FindProperty(sprite), presentation.FindProperty(showLabel));

    private static void DrawSpriteLayout(SerializedProperty style)
    {
        SerializedProperty layout = style.FindPropertyRelative("spriteLayout");
        EditorGUILayout.PropertyField(layout, new GUIContent("Ajuste do sprite"));
        if (layout.intValue != (int)TutorialPresentation.SpriteLayout.Sliced) return;
        SerializedProperty border = style.FindPropertyRelative("borderThickness");
        var label = new GUIContent("Espessura da borda",
            "1 = espessura original, 0,5 = metade, 2 = dobro. O sprite precisa ter bordas no Sprite Editor.");
        float value = border.floatValue > 0f ? border.floatValue : 1f;
        EditorGUI.BeginChangeCheck();
        float edited = EditorGUILayout.FloatField(label, value);
        if (EditorGUI.EndChangeCheck()) border.floatValue = Mathf.Max(.05f, edited);
    }

    private static void DrawTypography(SerializedProperty typography)
    {
        Field(typography, "color", "Cor do texto");
        Field(typography, "font", "Fonte", "Vazio usa a fonte da cena.");
        Field(typography, "fontStyle", "Estilo da fonte");
        Field(typography, "size", "Tamanho da fonte");
        Field(typography, "alignment", "Alinhamento");
        Field(typography, "lineSpacing", "Espaçamento entre linhas");
        Field(typography, "fit", "Reduzir texto para caber");
        if (typography.FindPropertyRelative("fit").boolValue)
            Field(typography, "minimumSize", "Tamanho mínimo");
        SerializedProperty insets = typography.FindPropertyRelative("insets");
        EditorGUILayout.PropertyField(insets, new GUIContent("Margens do texto"), false);
        if (!insets.isExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            Field(insets, "left", "Esquerda");
            Field(insets, "right", "Direita");
            Field(insets, "top", "Topo");
            Field(insets, "bottom", "Base");
        }
    }

    private static void Field(SerializedProperty parent, string name, string label,
        string tooltip = null, bool children = false) =>
        EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label, tooltip), children);
}
