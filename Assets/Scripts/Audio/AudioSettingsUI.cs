using UnityEngine;
using UnityEngine.UI;

// Controles na tela de pausa; volume 0 desliga sem apagar a preferência de mute.
public sealed class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Button musicLess;
    [SerializeField] private Button musicMore;
    [SerializeField] private Button musicMute;
    [SerializeField] private Text musicValue;
    [SerializeField] private Text musicMuteLabel;
    [SerializeField] private Button effectsLess;
    [SerializeField] private Button effectsMore;
    [SerializeField] private Button effectsMute;
    [SerializeField] private Text effectsValue;
    [SerializeField] private Text effectsMuteLabel;

    private Button weatherLess;
    private Button weatherMore;
    private Button weatherMute;
    private Text weatherValue;
    private Text weatherMuteLabel;

    private const float Step = 0.1f;

    private void Awake()
    {
        PrepareWeatherControls();
        if (musicLess != null) musicLess.onClick.AddListener(DecreaseMusic);
        if (musicMore != null) musicMore.onClick.AddListener(IncreaseMusic);
        if (musicMute != null) musicMute.onClick.AddListener(ToggleMusic);
        if (effectsLess != null) effectsLess.onClick.AddListener(DecreaseEffects);
        if (effectsMore != null) effectsMore.onClick.AddListener(IncreaseEffects);
        if (effectsMute != null) effectsMute.onClick.AddListener(ToggleEffects);
        Refresh();
    }

    // Add the row before the options transition captures the positions of its children.
    internal void PrepareWeatherControls()
    {
        if (weatherLess != null || effectsLess == null || effectsMore == null ||
            effectsMute == null || effectsValue == null || musicLess == null) return;

        Transform rowParent = effectsLess.transform.parent;
        Text effectsLabel = rowParent.Find("Efeitos")?.GetComponent<Text>();
        if (effectsLabel == null) return;

        float musicY = ((RectTransform)musicLess.transform).anchoredPosition.y;
        float effectsY = ((RectTransform)effectsLess.transform).anchoredPosition.y;
        foreach (Transform child in rowParent)
        {
            if (child is not RectTransform rect) continue;
            float y = rect.anchoredPosition.y;
            if (Mathf.Approximately(y, musicY))
                SetY(rect, 138f);
            else if (Mathf.Approximately(y, effectsY))
                SetY(rect, 80f);
            else
            {
                switch (child.name)
                {
                    case "Rotulo nickname":
                    case "Nickname":
                    case "Salvar nickname":
                        SetY(rect, -56f);
                        break;
                    case "Ignorar tutorial":
                    case "Rotulo tutorial":
                        SetY(rect, y - 22f);
                        break;
                    case "Aviso nick":
                        SetY(rect, -128f);
                        break;
                    case "Ver ranking":
                    case "Voltar":
                        SetY(rect, -176f);
                        break;
                }
            }
        }

        foreach (Button button in new[] { musicLess, musicMore, effectsLess, effectsMore })
        {
            if (button == null) continue;
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 54f);
        }

        effectsLabel.text = "Efeitos sonoros";
        AlignRowLabel(effectsLabel);
        if (rowParent.Find("Musica")?.GetComponent<Text>() is Text musicLabel)
            AlignRowLabel(musicLabel);

        Text label = CopyForWeather(effectsLabel, "Clima");
        label.text = "Clima";
        weatherValue = CopyForWeather(effectsValue, "Valor clima");
        weatherLess = CopyForWeather(effectsLess, "Clima menos");
        weatherMore = CopyForWeather(effectsMore, "Clima mais");
        weatherMute = CopyForWeather(effectsMute, "Clima silenciar");
        weatherMuteLabel = weatherMute.GetComponentInChildren<Text>(true);

        BindWeatherButton(weatherLess, DecreaseWeather);
        BindWeatherButton(weatherMore, IncreaseWeather);
        BindWeatherButton(weatherMute, ToggleWeather);
    }

    private static T CopyForWeather<T>(T template, string objectName) where T : Component
    {
        T copy = Instantiate(template, template.transform.parent, false);
        copy.gameObject.name = objectName;
        SetY((RectTransform)copy.transform, 22f);
        return copy;
    }

    private static void AlignRowLabel(Text label)
    {
        label.fontSize = 25;
        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(190f, rect.sizeDelta.y);
        rect.anchoredPosition = new Vector2(-245f, rect.anchoredPosition.y);
    }

    private static void SetY(RectTransform rect, float y)
    {
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }

    private static void BindWeatherButton(Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        SertaoButtonAudio.Bind(button);
    }

    private void OnEnable() => Refresh();
    private void Start() => Refresh();

    private void OnDestroy()
    {
        if (musicLess != null) musicLess.onClick.RemoveListener(DecreaseMusic);
        if (musicMore != null) musicMore.onClick.RemoveListener(IncreaseMusic);
        if (musicMute != null) musicMute.onClick.RemoveListener(ToggleMusic);
        if (effectsLess != null) effectsLess.onClick.RemoveListener(DecreaseEffects);
        if (effectsMore != null) effectsMore.onClick.RemoveListener(IncreaseEffects);
        if (effectsMute != null) effectsMute.onClick.RemoveListener(ToggleEffects);
    }

    private void DecreaseMusic() => ChangeMusic(-Step);
    private void IncreaseMusic() => ChangeMusic(Step);
    private void DecreaseEffects() => ChangeEffects(-Step);
    private void IncreaseEffects() => ChangeEffects(Step);
    private void DecreaseWeather() => ChangeWeather(-Step);
    private void IncreaseWeather() => ChangeWeather(Step);
    private void ChangeWeather(float delta)
    {
        AudioManager audio = AudioManager.Instance;
        if (audio != null)
            audio.SetWeatherVolume(Mathf.Round((audio.WeatherVolume + delta) * 10f) / 10f);
        Refresh();
    }

    private void ToggleWeather()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.SetWeatherMuted(!audio.WeatherMuted);
        Refresh();
    }

    private void ToggleMusic()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMuted(!AudioManager.Instance.MusicMuted);
        Refresh();
    }
    private void ToggleEffects()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetEffectsMuted(!AudioManager.Instance.EffectsMuted);
        Refresh();
    }
    private void ChangeMusic(float delta)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(Mathf.Round((AudioManager.Instance.MusicVolume + delta) * 10f) / 10f);
        Refresh();
    }
    private void ChangeEffects(float delta)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetEffectsVolume(Mathf.Round((AudioManager.Instance.EffectsVolume + delta) * 10f) / 10f);
        Refresh();
    }
    private void Refresh()
    {
        AudioManager a = AudioManager.Instance;
        if (a == null) return;
        if (musicValue != null) musicValue.text = Mathf.RoundToInt(a.MusicVolume * 100f) + "%";
        if (effectsValue != null) effectsValue.text = Mathf.RoundToInt(a.EffectsVolume * 100f) + "%";
        if (musicMuteLabel != null) musicMuteLabel.text = a.MusicMuted ? "ATIVAR" : "SILENCIAR";
        if (effectsMuteLabel != null) effectsMuteLabel.text = a.EffectsMuted ? "ATIVAR" : "SILENCIAR";
        if (weatherValue != null) weatherValue.text = Mathf.RoundToInt(a.WeatherVolume * 100f) + "%";
        if (weatherMuteLabel != null) weatherMuteLabel.text = a.WeatherMuted ? "ATIVAR" : "SILENCIAR";
    }
}
