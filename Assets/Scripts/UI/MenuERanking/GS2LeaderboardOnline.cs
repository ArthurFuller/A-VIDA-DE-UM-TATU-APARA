using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class GS2LeaderboardOnline : MonoBehaviour
{
    [Serializable] public sealed class Entry
    {
        public string nickname;
        public string nicknameKey;
        public int coins;
    }

    [Serializable] private sealed class AuthReply
    {
        public string idToken;
        public string refreshToken;
        public string localId;
        public string id_token;
        public string refresh_token;
        public string user_id;
    }

    internal const string NicknameTaken = "Esse nome já está sendo utilizado.";

    private const string RefreshKey = "GS2.FirebaseRefreshToken";
    private static GS2LeaderboardOnline instance;
    private GS2LeaderboardSettings settings;
    private string idToken;
    private string uid;
    private DateTime tokenExpires;
    private bool authenticating;

    public static GS2LeaderboardOnline Instance
    {
        get
        {
            if (instance != null) return instance;
            var go = new GameObject("Ranking Firebase");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GS2LeaderboardOnline>();
            return instance;
        }
    }

    public bool Configured => settings != null && !string.IsNullOrWhiteSpace(settings.webApiKey);

    private bool HasSession => !string.IsNullOrEmpty(idToken) &&
        !string.IsNullOrEmpty(uid) && DateTime.UtcNow < tokenExpires;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        settings = Resources.Load<GS2LeaderboardSettings>("GS2LeaderboardSettings");
    }

    public void SaveNickname(string nickname, Action<bool, string> done) =>
        StartCoroutine(SaveName(nickname, done));

    internal void PrepareSession()
    {
        if (Configured && !authenticating && !HasSession &&
            Application.internetReachability != NetworkReachability.NotReachable)
            StartCoroutine(Authenticate(_ => { }));
    }

    public void SubmitFinishedGame(int coins)
    {
        if (GS2Profile.HasNickname) StartCoroutine(Submit(coins));
    }

    public void Load(Action<List<Entry>, string> done) => StartCoroutine(LoadEntries(done));

    private IEnumerator Authenticate(Action<bool> done)
    {
        if (!Configured) { done(false); yield break; }
        if (authenticating)
        {
            while (authenticating) yield return null;
            done(HasSession);
            yield break;
        }
        if (HasSession)
        { done(true); yield break; }

        authenticating = true;
        idToken = uid = null;
        string refresh = PlayerPrefs.GetString(RefreshKey, string.Empty);
        string url;
        string body;
        string contentType;
        if (string.IsNullOrEmpty(refresh))
        {
            url = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" +
                Uri.EscapeDataString(settings.webApiKey);
            body = "{\"returnSecureToken\":true}";
            contentType = "application/json";
        }
        else
        {
            url = "https://securetoken.googleapis.com/v1/token?key=" +
                Uri.EscapeDataString(settings.webApiKey);
            body = "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refresh);
            contentType = "application/x-www-form-urlencoded";
        }

        using (var request = UnityWebRequest.Post(url, body, contentType))
        {
            request.timeout = 6;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var reply = JsonUtility.FromJson<AuthReply>(request.downloadHandler.text);
                idToken = string.IsNullOrEmpty(reply.idToken) ? reply.id_token : reply.idToken;
                uid = string.IsNullOrEmpty(reply.localId) ? reply.user_id : reply.localId;
                refresh = string.IsNullOrEmpty(reply.refreshToken) ? reply.refresh_token : reply.refreshToken;
                if (!string.IsNullOrEmpty(idToken) && !string.IsNullOrEmpty(uid))
                {
                    PlayerPrefs.SetString(RefreshKey, refresh);
                    PlayerPrefs.Save();
                    tokenExpires = DateTime.UtcNow.AddMinutes(50);
                }
            }
            else Debug.LogWarning("Ranking: falha na identificação Firebase: " + request.error);
        }
        authenticating = false;
        done(HasSession);
    }

    private IEnumerator ClaimNickname(string nickname, Action<bool, string> done,
        bool requireUnreserved = false)
    {
        if (!GS2Profile.IsValidNickname(nickname))
        { done(false, GS2Profile.NicknameHelp); yield break; }
        bool authenticated = false;
        yield return Authenticate(ok => authenticated = ok);
        if (!authenticated)
        { done(false, Configured ? "Não foi possível conectar ao Firebase." : "Ranking Firebase ainda não configurado."); yield break; }

        string path = "nicknames/" + NameKey(nickname);
        string ownerJson = "\"" + uid + "\"";
        // A reserva condicional é atômica e já confirma a disponibilidade na mesma requisição.
        using (var put = UnityWebRequest.Put(DatabaseUrl(path), ownerJson))
        {
            put.timeout = 6;
            put.SetRequestHeader("Content-Type", "application/json");
            put.SetRequestHeader("If-Match", "null_etag");
            yield return put.SendWebRequest();
            if (put.result == UnityWebRequest.Result.Success)
            { done(true, null); yield break; }
            if (put.responseCode != 412 && put.responseCode != 401 && put.responseCode != 403)
            { done(false, "Não foi possível salvar o nome."); yield break; }

            if (put.responseCode == 412)
            {
                if (requireUnreserved)
                { done(false, NicknameTaken); yield break; }

                // Quando disponível, o corpo do 412 traz o valor atual da reserva.
                string current = put.downloadHandler.text.Trim();
                if (current.Length > 2 && current[0] == '"' && current[current.Length - 1] == '"')
                {
                    bool useExisting = current == ownerJson;
                    done(useExisting, useExisting ? null : NicknameTaken);
                    yield break;
                }
            }
        }

        // As regras do Firebase também podem rejeitar a escrita antes de devolver o valor.
        using (var get = UnityWebRequest.Get(DatabaseUrl(path)))
        {
            get.timeout = 6;
            yield return get.SendWebRequest();
            if (get.result != UnityWebRequest.Result.Success)
            { done(false, "Não foi possível verificar o nome."); yield break; }
            string current = get.downloadHandler.text.Trim();
            if (!requireUnreserved && current == ownerJson) { done(true, null); yield break; }
            done(false, current == "null" ? "Não foi possível salvar o nome." : NicknameTaken);
        }
    }

    private IEnumerator SaveName(string nickname, Action<bool, string> done)
    {
        bool claimed = false;
        string error = null;
        yield return ClaimNickname(nickname, (ok, message) => { claimed = ok; error = message; },
            requireUnreserved: true);
        if (!claimed) { done(false, error); yield break; }
        string previous = GS2Profile.Nickname;
        if (!string.IsNullOrEmpty(previous) && NameKey(previous) != NameKey(nickname))
        {
            using (var delete = UnityWebRequest.Delete(DatabaseUrl("nicknames/" + NameKey(previous))))
                yield return delete.SendWebRequest();
        }
        done(true, null);
    }

    private IEnumerator Submit(int coins, Action saved = null)
    {
        string nickname = GS2Profile.Nickname;
        bool claimed = false;
        yield return ClaimNickname(nickname, (ok, _) => claimed = ok);
        if (!claimed || nickname != GS2Profile.Nickname) yield break;
        var entry = new Entry
        {
            nickname = nickname,
            nicknameKey = NameKey(nickname),
            coins = coins
        };
        using (var put = UnityWebRequest.Put(DatabaseUrl("ranking/" + uid), JsonUtility.ToJson(entry)))
        {
            put.SetRequestHeader("Content-Type", "application/json");
            yield return put.SendWebRequest();
            if (put.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("Ranking: pontuação não enviada: " + put.error);
            else saved?.Invoke();
        }
    }

    private IEnumerator LoadEntries(Action<List<Entry>, string> done)
    {
        bool authenticated = false;
        yield return Authenticate(ok => authenticated = ok);
        if (!authenticated)
        { done(null, Configured ? "Não foi possível conectar ao Firebase." : "Ranking Firebase ainda não configurado."); yield break; }

        List<Entry> current = null;
        yield return ReadRanking(result => current = result);
        done(current, current == null ? "Não foi possível carregar o ranking." : null);

        // A lista já pode aparecer enquanto um recorde local pendente é sincronizado.
        yield return null;
        if (!GS2LeaderboardScore.HasLocalScore || !GS2Profile.HasNickname) yield break;
        string nickname = GS2Profile.Nickname;
        string nicknameKey = NameKey(nickname);
        int best = GS2LeaderboardScore.LocalBestCoins;
        if (current != null && current.Exists(entry => entry.coins >= best &&
            (entry.nicknameKey == nicknameKey ||
             string.Equals(entry.nickname, nickname, StringComparison.OrdinalIgnoreCase))))
            yield break;

        bool saved = false;
        yield return Submit(best, () => saved = true);
        if (!saved) yield break;

        yield return ReadRanking(result => { if (result != null) done(result, null); });
    }

    private IEnumerator ReadRanking(Action<List<Entry>> done)
    {
        using (var get = UnityWebRequest.Get(DatabaseUrl("ranking")))
        {
            yield return get.SendWebRequest();
            if (get.result != UnityWebRequest.Result.Success)
            { done(null); yield break; }
            var entries = ParseEntries(get.downloadHandler.text);
            entries.Sort((a, b) =>
            {
                int scoreOrder = b.coins.CompareTo(a.coins);
                return scoreOrder != 0 ? scoreOrder : string.Compare(a.nickname, b.nickname, StringComparison.OrdinalIgnoreCase);
            });
            done(entries);
        }
    }

    private string DatabaseUrl(string path) => settings.databaseUrl.TrimEnd('/') + "/" + path +
        ".json?auth=" + Uri.EscapeDataString(idToken);

    private static string NameKey(string nickname)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(nickname.Trim().ToLowerInvariant());
        var key = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes) key.Append(value.ToString("x2"));
        return key.ToString();
    }

    private static List<Entry> ParseEntries(string json)
    {
        var entries = new List<Entry>();
        if (string.IsNullOrEmpty(json) || json == "null") return entries;
        int i = 1;
        while (i < json.Length)
        {
            while (i < json.Length && json[i] != ':') i++;
            if (++i >= json.Length) break;
            while (i < json.Length && json[i] != '{') i++;
            int start = i;
            int depth = 0;
            bool quoted = false, escaped = false;
            for (; i < json.Length; i++)
            {
                char c = json[i];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0) { i++; break; }
            }
            if (depth != 0 || start >= json.Length) break;
            var entry = JsonUtility.FromJson<Entry>(json.Substring(start, i - start));
            if (entry != null && !string.IsNullOrEmpty(entry.nickname)) entries.Add(entry);
        }
        return entries;
    }
}
