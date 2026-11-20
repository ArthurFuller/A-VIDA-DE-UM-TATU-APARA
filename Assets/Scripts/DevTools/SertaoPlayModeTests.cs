#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SertaoPlayModeTests
{
    private const string MenuScene = "Assets/Scenes/MainMenu.unity";
    private const string PreviewKey = "Sertao.PlayMode.PreviewNickname";
    private const string PreviewMenu = "Sertão/Testes/Simular primeiro acesso";

    internal static bool TakeNicknamePreview()
    {
        bool requested = SessionState.GetBool(PreviewKey, false);
        SessionState.EraseBool(PreviewKey);
        return requested;
    }

    [MenuItem(PreviewMenu, false, 10)]
    private static void StartNicknamePreview()
    {
        if (!OpenMenuScene()) return;

        SessionState.SetBool(PreviewKey, true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem(PreviewMenu, true)]
    private static bool CanStartNicknamePreview() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private const string EditNicknameMenu = "Sertão/Testes/Editar tela de nickname";

    [MenuItem(EditNicknameMenu, false, 12)]
    private static void EditNicknamePage()
    {
        if (!OpenMenuScene()) return;
        Scene scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var menu = root.GetComponentInChildren<GS2FrontEnd>(true);
            if (menu == null) continue;
            GameObject page = menu.EditableNicknamePage();
            if (page == null) break;

            if (!page.activeSelf)
            {
                Undo.RecordObject(page, "Exibir tela de nickname");
                page.SetActive(true);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = page;
            EditorGUIUtility.PingObject(page);
            return;
        }

        EditorUtility.DisplayDialog("Tela de nickname",
            "Confira as referências do Canvas e do perfil em GS2FrontEnd no MainMenu.", "OK");
    }

    [MenuItem(EditNicknameMenu, true)]
    private static bool CanEditNicknamePage() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static bool OpenMenuScene()
    {
        if (SceneManager.GetActiveScene().path != MenuScene)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        }

        return true;
    }
}
#endif
