using UnityEngine;

// Guarda somente a melhor partida deste dispositivo até o ranking online ser conectado.
public static class GS2LeaderboardScore
{
    private const string HasScoreKey = "GS2.Leaderboard.HasLocalScore";
    private const string ScoreKey = "GS2.Leaderboard.LocalBestCoins";

    public static bool HasLocalScore => PlayerPrefs.GetInt(HasScoreKey, 0) != 0;
    public static int LocalBestCoins => PlayerPrefs.GetInt(ScoreKey);

    public static void RecordFinishedGame(int coins)
    {
        if (HasLocalScore && coins <= LocalBestCoins) return;
        PlayerPrefs.SetInt(ScoreKey, coins);
        PlayerPrefs.SetInt(HasScoreKey, 1);
        PlayerPrefs.Save();
        GS2LeaderboardOnline.Instance.SubmitFinishedGame(coins);
    }
}
