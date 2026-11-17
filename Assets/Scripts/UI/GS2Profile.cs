using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class GS2Profile : MonoBehaviour
{
    private const string NicknameKey = "GS2.LeaderboardNickname";
    internal const string NicknameHelp = "Use até 18 caracteres, com pelo menos uma letra ou número.";
    public static string Nickname => PlayerPrefs.GetString(NicknameKey, string.Empty);
    public static bool HasNickname => IsValidNickname(Nickname);

    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Toggle skipTutorial;
    [SerializeField] private Text notice;

    internal static bool IsValidNickname(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 18) return false;
        for (int i = 0; i < value.Length; i++)
            if (char.IsLetterOrDigit(value, i)) return true;
        return false;
    }

    private void Awake()
    {
        if (skipTutorial != null)
        {
            skipTutorial.isOn = InitialTutorial.SkipRequested;
            skipTutorial.onValueChanged.AddListener(SetSkipTutorial);
        }
        if (saveButton != null) saveButton.onClick.AddListener(SaveNickname);
    }

    private void OnEnable()
    {
        if (nicknameInput != null) nicknameInput.SetTextWithoutNotify(Nickname);
    }

    private void OnDestroy()
    {
        if (skipTutorial != null) skipTutorial.onValueChanged.RemoveListener(SetSkipTutorial);
        if (saveButton != null) saveButton.onClick.RemoveListener(SaveNickname);
    }

    private void SetSkipTutorial(bool value) => InitialTutorial.SkipRequested = value;

    internal bool ShowFirstRun(Transform parent, Action done)
    {
        if (nicknameInput == null || saveButton == null || notice == null) return false;
        var screen = new GameObject("Primeiro acesso", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)screen.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        screen.GetComponent<Image>().color = new Color32(35, 26, 20, 255);

        Text heading = Instantiate(notice, rect, false);
        Place(heading.rectTransform, 110f, 480f, 55f);
        heading.gameObject.SetActive(true);
        heading.text = "Qual é o seu nickname?";
        heading.color = new Color32(248, 228, 193, 255);
        heading.fontSize = 29;
        heading.alignment = TextAnchor.MiddleCenter;

        Text message = Instantiate(notice, rect, false);
        Place(message.rectTransform, -91f, 540f, 46f);
        message.gameObject.SetActive(true);
        message.text = string.Empty;
        message.color = heading.color;
        message.alignment = TextAnchor.MiddleCenter;

        InputField input = Instantiate(nicknameInput, rect, false);
        Place((RectTransform)input.transform, 39f, 300f, 45f);
        input.characterLimit = 18;
        input.onValueChanged = new InputField.OnChangeEvent();
        input.onEndEdit = new InputField.EndEditEvent();
        input.SetTextWithoutNotify(string.Empty);
        input.gameObject.SetActive(true);

        Button proceed = Instantiate(saveButton, rect, false);
        Place((RectTransform)proceed.transform, -34f, 176f, 72f);
        proceed.onClick = new Button.ButtonClickedEvent();
        Text buttonText = proceed.GetComponentInChildren<Text>(true);
        if (buttonText != null) buttonText.text = "Prosseguir";
        proceed.gameObject.SetActive(false);
        input.onValueChanged.AddListener(value =>
        {
            proceed.gameObject.SetActive(IsValidNickname(value.Trim()));
            message.text = string.Empty;
        });
        proceed.onClick.AddListener(() => Save(input, proceed, message, () =>
        {
            screen.SetActive(false);
            Destroy(screen);
            done();
        }));
        SertaoButtonAudio.Bind(proceed);
        return true;
    }

    private static void Place(RectTransform rect, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    public void SaveNickname() => Save(nicknameInput, saveButton, notice);

    private static void Save(InputField input, Button button, Text message, Action firstRunDone = null)
    {
        if (input == null) return;
        string value = input.text.Trim();
        if (!IsValidNickname(value))
        {
            if (message != null) message.text = NicknameHelp;
            return;
        }

        var leaderboard = GS2LeaderboardOnline.Instance;
        void Store()
        {
            PlayerPrefs.SetString(NicknameKey, value);
            PlayerPrefs.Save();
            if (input != null) input.SetTextWithoutNotify(value);
            if (message != null) message.text = "Nickname salvo.";
            firstRunDone?.Invoke();
        }

        if (!leaderboard.Configured || (firstRunDone != null &&
            Application.internetReachability == NetworkReachability.NotReachable))
        {
            Store();
            return;
        }

        input.interactable = false;
        if (button != null) button.interactable = false;
        if (message != null) message.text = "Verificando nickname...";
        leaderboard.SaveNickname(value, (saved, error) =>
        {
            if (input == null) return;
            input.interactable = true;
            if (button != null) button.interactable = true;
            if (!saved && (firstRunDone == null || error == GS2LeaderboardOnline.NicknameTaken))
            {
                if (message != null) message.text = error;
                return;
            }
            Store();
            if (saved && GS2LeaderboardScore.HasLocalScore)
                leaderboard.SubmitFinishedGame(GS2LeaderboardScore.LocalBestCoins);
        });
    }
}
