using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GS2Profile : MonoBehaviour
{
    private const string NicknameKey = "GS2.LeaderboardNickname";
    internal const string NicknameHelp = "Use até 18 caracteres, com pelo menos uma letra ou número.";
    public static string Nickname => PlayerPrefs.GetString(NicknameKey, string.Empty);
    public static bool HasNickname => IsValidNickname(Nickname);

    [Serializable]
    internal sealed class NicknamePage
    {
        [SerializeField] internal GameObject Root;
        [SerializeField] internal InputField Input;
        [SerializeField] internal Button Proceed;
        [SerializeField] internal Text Message;

        internal bool IsReady => Root != null && Input != null && Proceed != null && Message != null;
    }

    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Toggle skipTutorial;
    [SerializeField] private Text notice;
    [Header("Mensagens de nome")]
    [SerializeField] private string checkingMessage = "Verificando nome...";
    [SerializeField] private string takenMessage = "Esse nome já está sendo utilizado.";
    [SerializeField] private string savedMessage = "Nome salvo com sucesso.";
    [SerializeField] private Color errorColor = new Color32(247, 104, 98, 255);
    [SerializeField] private Color successColor = new Color32(128, 224, 153, 255);
    [SerializeField, Min(0.1f)] private float noticeSeconds = 3.5f;

    private Color optionsNoticeColor;
    private Tween optionsNoticeTimer;
    private bool savingInOptions;

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
        if (notice != null) optionsNoticeColor = notice.color;
    }

    private void OnEnable()
    {
        if (nicknameInput != null) nicknameInput.SetTextWithoutNotify(Nickname);
        if (savingInOptions) ShowNotice(notice, checkingMessage, optionsNoticeColor);
        else ClearOptionsNotice();
    }

    private void OnDisable()
    {
        ClearOptionsNotice();
    }

    private void OnDestroy()
    {
        if (skipTutorial != null) skipTutorial.onValueChanged.RemoveListener(SetSkipTutorial);
        if (saveButton != null) saveButton.onClick.RemoveListener(SaveNickname);
    }

    private void SetSkipTutorial(bool value) => InitialTutorial.SkipRequested = value;

    internal NicknamePage CreateFirstRunPage(Transform parent)
    {
        if (nicknameInput == null || saveButton == null || notice == null) return null;
        var screen = new GameObject("Primeiro acesso", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)screen.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        screen.GetComponent<Image>().color = new Color32(35, 26, 20, 255);

        Text heading = Instantiate(notice, rect, false);
        heading.name = "Título";
        Place(heading.rectTransform, 110f, 480f, 55f);
        heading.gameObject.SetActive(true);
        heading.text = "Qual é o seu nome?";
        heading.color = new Color32(248, 228, 193, 255);
        heading.fontSize = 29;
        heading.alignment = TextAnchor.MiddleCenter;

        Text message = Instantiate(notice, rect, false);
        message.name = "Mensagem";
        Place(message.rectTransform, -91f, 540f, 46f);
        message.gameObject.SetActive(true);
        message.text = string.Empty;
        message.color = heading.color;
        message.alignment = TextAnchor.MiddleCenter;

        InputField input = Instantiate(nicknameInput, rect, false);
        input.name = "Campo de nome";
        Place((RectTransform)input.transform, 39f, 300f, 45f);
        input.characterLimit = 18;
        input.onValueChanged = new InputField.OnChangeEvent();
        input.onEndEdit = new InputField.EndEditEvent();
        input.SetTextWithoutNotify(string.Empty);
        input.gameObject.SetActive(true);

        Button proceed = Instantiate(saveButton, rect, false);
        proceed.name = "Botão Prosseguir";
        Place((RectTransform)proceed.transform, -34f, 176f, 72f);
        proceed.onClick = new Button.ButtonClickedEvent();
        Text buttonText = proceed.GetComponentInChildren<Text>(true);
        if (buttonText != null) buttonText.text = "Prosseguir";
        proceed.gameObject.SetActive(true);
        return new NicknamePage { Root = screen, Input = input, Proceed = proceed, Message = message };
    }

    internal bool ShowFirstRun(NicknamePage page, Transform parent, Action<Action> done, bool preview)
    {
        bool temporary = page == null || page.Root == null;
        if (temporary) page = CreateFirstRunPage(parent);
        if (page == null || !page.IsReady) return false;
        FullscreenBackdrop.Cover(page.Root.transform);

        InputField input = page.Input;
        Button proceed = page.Proceed;
        Text message = page.Message;
        HoverPunchButton feedback = HoverPunchButton.Bind(proceed);
        feedback.AnimatePointerClick = false;
        bool submitting = false;
        bool sessionRequested = false;
        Color noticeColor = message.color;
        RectTransform inputRect = (RectTransform)input.transform;
        RectTransform buttonRect = (RectTransform)proceed.transform;
        Tween noticeTimer = null;
        Tween shake = null;
        input.SetTextWithoutNotify(string.Empty);
        message.text = string.Empty;
        message.enabled = true;
        message.gameObject.SetActive(true);
        proceed.gameObject.SetActive(false);
        page.Root.SetActive(true);

        void Refresh(string value)
        {
            bool valid = IsValidNickname(value.Trim());
            proceed.gameObject.SetActive(valid);
            if (valid && !preview && !sessionRequested)
            {
                sessionRequested = true;
                GS2LeaderboardOnline.Instance.PrepareSession();
            }
        }

        void ClearNotice()
        {
            noticeTimer?.Kill();
            noticeTimer = null;
            message.text = string.Empty;
            message.color = noticeColor;
        }

        void ShowFirstRunError(string error)
        {
            if (page.Root == null || !page.Root.activeInHierarchy) return;
            submitting = false;
            bool taken = error == GS2LeaderboardOnline.NicknameTaken;
            ShowNotice(message, ErrorMessage(error), errorColor);
            noticeTimer = DOVirtual.DelayedCall(noticeSeconds, () =>
            {
                noticeTimer = null;
                ClearNotice();
            }).SetLink(page.Root);

            if (!taken) return;
            shake?.Kill();
            Vector2 inputHome = inputRect.anchoredPosition;
            Vector2 buttonHome = buttonRect.anchoredPosition;
            shake = DOVirtual.Float(0f, 1f, 0.27f, progress =>
            {
                float offset = Mathf.Sin(progress * Mathf.PI * 6f) * (1f - progress) * 5f;
                Vector2 movement = Vector2.right * offset;
                inputRect.anchoredPosition = inputHome + movement;
                buttonRect.anchoredPosition = buttonHome + movement;
            }).SetEase(Ease.Linear).SetUpdate(true).SetLink(page.Root).OnKill(() =>
            {
                shake = null;
                if (inputRect != null) inputRect.anchoredPosition = inputHome;
                if (buttonRect != null) buttonRect.anchoredPosition = buttonHome;
            });
        }

        void Complete()
        {
            ClearNotice();
            shake?.Kill();
            proceed.interactable = false;
            input.onValueChanged.RemoveListener(Refresh);
            proceed.onClick.RemoveListener(Submit);
            done(() =>
            {
                page.Root.SetActive(false);
                if (temporary) Destroy(page.Root);
            });
        }

        void Submit()
        {
            if (submitting || !IsValidNickname(input.text.Trim())) return;
            submitting = true;
            ClearNotice();
            shake?.Kill();
            feedback.PlayAcceptedClick();
            if (preview)
                Complete();
            else
                Save(input, proceed, message, Complete, ShowFirstRunError);
        }

        input.onValueChanged.AddListener(Refresh);
        proceed.onClick.AddListener(Submit);
        SertaoButtonAudio.Bind(proceed);
        return true;
    }

    private static void Place(RectTransform rect, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    public void SaveNickname()
    {
        if (nicknameInput == null || savingInOptions) return;
        ClearOptionsNotice();
        savingInOptions = true;
        Save(nicknameInput, saveButton, notice,
            () => ShowOptionsResult(savedMessage, successColor),
            error =>
            {
                if (error == NicknameHelp || error == GS2LeaderboardOnline.NicknameTaken)
                    nicknameInput.SetTextWithoutNotify(Nickname);
                ShowOptionsResult(ErrorMessage(error), errorColor);
            });
    }

    private void ShowOptionsResult(string message, Color color)
    {
        savingInOptions = false;
        if (!isActiveAndEnabled || notice == null) return;
        ShowNotice(notice, message, color);
        optionsNoticeTimer = DOVirtual.DelayedCall(noticeSeconds, () =>
        {
            optionsNoticeTimer = null;
            ClearOptionsNotice();
        }).SetLink(gameObject);
    }

    private void ClearOptionsNotice()
    {
        optionsNoticeTimer?.Kill();
        optionsNoticeTimer = null;
        if (notice == null) return;
        notice.text = string.Empty;
        notice.color = optionsNoticeColor;
        notice.gameObject.SetActive(false);
    }

    private string ErrorMessage(string error) => error == GS2LeaderboardOnline.NicknameTaken
        ? takenMessage : error ?? "Não foi possível verificar o nome. Tente de novo.";

    private static void ShowNotice(Text label, string message, Color color)
    {
        if (label == null) return;
        label.text = message;
        label.color = color;
        label.enabled = true;
        label.gameObject.SetActive(true);
    }

    private void Save(InputField input, Button button, Text message, Action onSaved,
        Action<string> onRejected)
    {
        if (input == null) return;
        string value = input.text.Trim();
        if (!IsValidNickname(value))
        {
            onRejected(NicknameHelp);
            return;
        }

        var leaderboard = GS2LeaderboardOnline.Instance;
        if (!leaderboard.Configured || Application.internetReachability == NetworkReachability.NotReachable)
        {
            onRejected("Não foi possível verificar o nome. Tente de novo.");
            return;
        }

        input.interactable = false;
        if (button != null) button.interactable = false;
        if (message != null) ShowNotice(message, checkingMessage, message.color);
        leaderboard.SaveNickname(value, (saved, error) =>
        {
            if (input == null) return;
            input.interactable = true;
            if (button != null) button.interactable = true;
            if (!saved)
            {
                onRejected(error);
                return;
            }
            PlayerPrefs.SetString(NicknameKey, value);
            PlayerPrefs.Save();
            input.SetTextWithoutNotify(value);
            onSaved();
            if (GS2LeaderboardScore.HasLocalScore)
                leaderboard.SubmitFinishedGame(GS2LeaderboardScore.LocalBestCoins);
        });
    }
}
