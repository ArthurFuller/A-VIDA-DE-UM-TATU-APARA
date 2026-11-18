using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GS2FrontEnd))]
public sealed class GS2FrontEndEditor : Editor
{
    private static readonly string[] MotionFields =
    {
        "slideSeconds", "fadeSeconds", "enteringScale", "introDarkHoldSeconds",
        "introRevealSeconds", "logoDropSeconds", "buttonDropSeconds", "floatingHeight", "floatingAngle"
    };
    private static readonly string[] RippleFields =
    {
        "enabled", "rippleShader", "topLineInset", "bottomLineInset", "leftLineInset",
        "rightLineInset", "lineHalfWidth",
        "wavesPerLine", "impactAmplitude", "idleAmplitude", "idleSpeed"
    };
    private static readonly string[] ExcludeFromScene =
    {
        "m_Script", "slideSeconds", "fadeSeconds", "enteringScale", "introDarkHoldSeconds",
        "introRevealSeconds", "logoDropSeconds", "buttonDropSeconds", "floatingHeight",
        "floatingAngle", "borderRipple"
    };

    public override void OnInspectorGUI()
    {
        var menu = (GS2FrontEnd)target;
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        DrawPropertiesExcluding(serializedObject, ExcludeFromScene);
        serializedObject.ApplyModifiedProperties();

        var settings = AssetDatabase.LoadAssetAtPath<MainMenuAnimationSettings>(
            MainMenuAnimationSettings.AssetPath);
        SerializedObject tuning = settings != null ? new SerializedObject(settings) : serializedObject;
        tuning.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animação e ondulação do menu", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Os ajustes abaixo são salvos automaticamente, inclusive no Play Mode.",
            MessageType.Info);
        foreach (string path in MotionFields)
            EditorGUILayout.PropertyField(tuning.FindProperty(path));
        EditorGUILayout.PropertyField(tuning.FindProperty("borderRipple"), true);

        if (tuning.ApplyModifiedProperties())
        {
            if (settings == null) settings = CreateSettings(serializedObject);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            if (EditorApplication.isPlaying)
            {
                menu.ApplyAnimationSettings(settings);
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        using (new EditorGUI.DisabledScope(!menu.CanReplayMainEntrance || EditorApplication.isPaused))
            if (GUILayout.Button("Testar entrada do menu"))
            {
                menu.ReplayMainEntrance();
                EditorApplication.QueuePlayerLoopUpdate();
            }
    }

    private static MainMenuAnimationSettings CreateSettings(SerializedObject source)
    {
        var settings = CreateInstance<MainMenuAnimationSettings>();
        source.Update();
        var destination = new SerializedObject(settings);
        foreach (string path in MotionFields)
            destination.CopyFromSerializedProperty(source.FindProperty(path));
        foreach (string field in RippleFields)
            destination.CopyFromSerializedProperty(source.FindProperty("borderRipple." + field));
        destination.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(settings, MainMenuAnimationSettings.AssetPath);
        return settings;
    }
}
