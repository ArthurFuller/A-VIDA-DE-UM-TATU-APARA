#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SertaoFinalDayPreview
{
    private const string PreviewMenu = "Sertão/Testes/Simular fim da partida/";
    private const string WinMenu = PreviewMenu + "Vitória (+24 moedas)";
    private const string ZeroMenu = PreviewMenu + "Sem reservas (0 moedas)";
    private const string LossMenu = PreviewMenu + "Dívida (-12 moedas)";

    [MenuItem(WinMenu, false, 20)]
    private static void PreviewWin() => StartPreview(24);

    [MenuItem(ZeroMenu, false, 21)]
    private static void PreviewZero() => StartPreview(0);

    [MenuItem(LossMenu, false, 22)]
    private static void PreviewLoss() => StartPreview(-12);

    [MenuItem(WinMenu, true)]
    [MenuItem(ZeroMenu, true)]
    [MenuItem(LossMenu, true)]
    private static bool CanPreview() => EditorApplication.isPlaying && !EditorApplication.isPaused &&
        SceneManager.GetActiveScene().name == "PrototypeLayout";

    private static void StartPreview(int finalBalance)
    {
        if (!CanPreview()) return;
        Scene scene = SceneManager.GetActiveScene();
        foreach (DayCycle cycle in Object.FindObjectsByType<DayCycle>(FindObjectsSortMode.None))
        {
            if (cycle.gameObject.scene != scene || !cycle.isActiveAndEnabled) continue;
            if (cycle.TryPreviewFinalDay(finalBalance)) return;
            Debug.LogWarning("Para simular o fim da partida, feche o evento inicial, saia da pausa " +
                "e aguarde as ações pendentes. O botão Finalizar dia precisa estar disponível.", cycle);
            return;
        }
        Debug.LogWarning("Não foi possível encontrar o controlador de dias na cena de gameplay.");
    }
}
#endif
