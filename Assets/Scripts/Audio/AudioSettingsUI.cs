using UnityEngine;
using UnityEngine.UI;

// Controles de áudio das duas telas de Opções.
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

    [SerializeField] private Button weatherLess;
    [SerializeField] private Button weatherMore;
    [SerializeField] private Button weatherMute;
    [SerializeField] private Text weatherValue;
    [SerializeField] private Text weatherMuteLabel;

    private const float Step = 0.1f;

    private void Awake()
    {
        if (musicLess != null) musicLess.onClick.AddListener(DecreaseMusic);
        if (musicMore != null) musicMore.onClick.AddListener(IncreaseMusic);
        if (musicMute != null) musicMute.onClick.AddListener(ToggleMusic);
        if (effectsLess != null) effectsLess.onClick.AddListener(DecreaseEffects);
        if (effectsMore != null) effectsMore.onClick.AddListener(IncreaseEffects);
        if (effectsMute != null) effectsMute.onClick.AddListener(ToggleEffects);
        if (weatherLess != null) weatherLess.onClick.AddListener(DecreaseWeather);
        if (weatherMore != null) weatherMore.onClick.AddListener(IncreaseWeather);
        if (weatherMute != null) weatherMute.onClick.AddListener(ToggleWeather);
        Refresh();
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
        if (weatherLess != null) weatherLess.onClick.RemoveListener(DecreaseWeather);
        if (weatherMore != null) weatherMore.onClick.RemoveListener(IncreaseWeather);
        if (weatherMute != null) weatherMute.onClick.RemoveListener(ToggleWeather);
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
