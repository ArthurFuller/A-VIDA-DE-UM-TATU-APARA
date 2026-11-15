using UnityEngine;

[CreateAssetMenu(menuName = "Tapira/Ranking Firebase")]
public sealed class GS2LeaderboardSettings : ScriptableObject
{
    public string databaseUrl = "https://jogo-tapira-leaderboard-default-rtdb.firebaseio.com";
    public string webApiKey;
}
