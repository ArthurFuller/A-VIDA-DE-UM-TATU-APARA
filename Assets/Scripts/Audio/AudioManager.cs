using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A biblioteca compartilhada se sobrepõe às entradas antigas nas duas cenas.
public sealed class AudioManager : MonoBehaviour
{
    [Serializable]
    public sealed class CueEntry
    {
        public SoundCue cue;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0f)] public float minInterval = 0.06f;
    }

    [Header("Fontes na Hierarchy")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource fishingPullSource;
    [Header("Puxar linha (pesca)")]
    [SerializeField, Min(0.1f)] private float fishingIdleDelay = 1f;
    [SerializeField, Range(0.02f, 0.4f)] private float fishingFadeDuration = 0.12f;
    [Header("Biblioteca de efeitos (atribuir clips aqui)")]
    [SerializeField] private CueEntry[] effects = Array.Empty<CueEntry>();
    [SerializeField] private SertaoAudioLibrary library;

    private const string MusicKey = "Sertao_MusicVolume_v1";
    private const string EffectsKey = "Sertao_EffectsVolume_v1";
    private const string WeatherKey = "Sertao_WeatherVolume_v1";
    private const string WeatherMuteKey = "Sertao_WeatherMute_v1";
    private const string MusicMuteKey = "Sertao_MusicMute_v1";
    private const string EffectsMuteKey = "Sertao_EffectsMute_v1";
    private readonly Dictionary<SoundCue, CueEntry> entries = new();
    private readonly Dictionary<SoundCue, float> lastPlay = new();
    private readonly Dictionary<SoundCue, int> lastVariant = new();
    private float musicVolume;
    private float effectsVolume;
    private float weatherVolume;
    private bool weatherMuted;
    private AudioSource weatherEffectsSource;
    private bool musicMuted;
    private bool sceneMusicPaused;
    private bool effectsMuted;
    private float fishingPullUntil;
    private float fishingPullFade;
    private float fishingPullCueVolume = 1f;
    private AudioSource rainSource;
    private bool rainWanted;
    private float rainBlend;
    private static AudioSource uiSource;
    private static float musicTransitionGain = 1f;
    private static AudioSource sharedOpening;
    private static AudioSource sharedLoop;
    private static double soundtrackStartDsp = double.NaN;
    private static double soundtrackPausedAt;
    private bool UsesSoundtrack => library != null && library.musicLoop != null;

    public static AudioManager Instance { get; private set; }
    public float MusicVolume => musicVolume;
    public float EffectsVolume => effectsVolume;
    public float WeatherVolume => weatherVolume;
    public bool WeatherMuted => weatherMuted;
    public bool MusicMuted => musicMuted;
    public bool EffectsMuted => effectsMuted;
    public event Action MusicMuteChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("AudioManager duplicado: mantendo o primeiro", this);
            enabled = false;
            return;
        }
        Instance = this;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.7f));
        effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, 0.85f));
        weatherVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(WeatherKey, 0.4f));
        weatherMuted = PlayerPrefs.GetInt(WeatherMuteKey, 0) != 0;
        musicMuted = PlayerPrefs.GetInt(MusicMuteKey, 0) != 0;
        effectsMuted = PlayerPrefs.GetInt(EffectsMuteKey, 0) != 0;
        foreach (CueEntry entry in effects)
            if (entry != null && !entries.ContainsKey(entry.cue))
                entries.Add(entry.cue, entry);
        if (library == null) library = Resources.Load<SertaoAudioLibrary>("SertaoAudioLibrary");
        if (library != null && library.effects != null)
        {
            foreach (CueEntry entry in library.effects)
                if (entry != null) entries[entry.cue] = entry;
        }
        if (musicSource != null)
        {
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            if (UsesSoundtrack || musicMuted)
            {
                musicSource.playOnAwake = false;
                musicSource.Stop();
            }
        }
        if (effectsSource != null) { effectsSource.loop = false; effectsSource.spatialBlend = 0f; }
        if (ambienceSource != null)
        {
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            if (library != null)
            {
                ambienceSource.playOnAwake = false;
                ambienceSource.Stop();
                ambienceSource.clip = library.wind;
            }
        }
        weatherEffectsSource = gameObject.AddComponent<AudioSource>();
        weatherEffectsSource.playOnAwake = false;
        weatherEffectsSource.spatialBlend = 0f;
        if (ambienceSource != null)
            weatherEffectsSource.outputAudioMixerGroup = ambienceSource.outputAudioMixerGroup;

        if (library != null && library.rain != null)
        {
            rainSource = gameObject.AddComponent<AudioSource>();
            rainSource.playOnAwake = false;
            rainSource.loop = true;
            rainSource.spatialBlend = 0f;
            rainSource.clip = library.rain;
            if (ambienceSource != null)
                rainSource.outputAudioMixerGroup = ambienceSource.outputAudioMixerGroup;
        }
        if (fishingPullSource != null)
        {
            fishingPullSource.loop = true;
            fishingPullSource.playOnAwake = false;
            fishingPullSource.spatialBlend = 0f;
            if (entries.TryGetValue(SoundCue.FishingPull, out CueEntry pull))
            {
                fishingPullCueVolume = pull.volume;
                if ((library != null || fishingPullSource.clip == null) && pull.clips != null)
                    foreach (AudioClip clip in pull.clips)
                        if (clip != null) { fishingPullSource.clip = clip; break; }
            }
            fishingPullSource.Stop();
        }
        PrepareUiAudio();
        ApplyVolumes();
    }

    private void PrepareUiAudio()
    {
        if (uiSource == null)
        {
            var audio = new GameObject("Áudio da interface");
            DontDestroyOnLoad(audio);
            uiSource = audio.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.ignoreListenerPause = true;
            uiSource.spatialBlend = 0f;
            uiSource.priority = 64;
        }
        uiSource.outputAudioMixerGroup = effectsSource != null ? effectsSource.outputAudioMixerGroup : null;
    }

    private void Start()
    {
        if (Instance != this) return;
        StartMusic();
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SertaoButtonAudio.Bind(button);
        if (ambienceSource != null && ambienceSource.clip != null && !ambienceSource.isPlaying)
            ambienceSource.Play();
    }

    private void StartMusic()
    {
        AudioClip loop = UsesSoundtrack ? library.musicLoop : null;
        if (loop != null && sharedLoop != null && sharedLoop.clip == loop)
        {
            ApplySoundtrackPause();
            return;
        }

        if (sharedLoop != null)
        {
            sharedLoop.Stop();
            if (sharedOpening != null) sharedOpening.Stop();
            Destroy(sharedLoop.gameObject);
            sharedOpening = sharedLoop = null;
        }
        if (loop == null)
        {
            if (!musicMuted && musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
                musicSource.Play();
            return;
        }

        var track = new GameObject("Trilha do Sertão");
        DontDestroyOnLoad(track);
        sharedLoop = AddMusicSource(track, loop, true);
        if (library.musicOpening != null)
            sharedOpening = AddMusicSource(track, library.musicOpening, false);
        ApplyMusicVolume();

        soundtrackStartDsp = double.NaN;
        soundtrackPausedAt = 0d;
        if (!musicMuted) ScheduleSoundtrack(0.18);
    }

    private static void ScheduleSoundtrack(double leadTime)
    {
        double begins = AudioSettings.dspTime + leadTime;
        soundtrackStartDsp = begins - soundtrackPausedAt;
        AudioClip opening = sharedOpening != null ? sharedOpening.clip : null;
        double openingDuration = opening != null ? (double)opening.samples / opening.frequency : 0d;
        if (soundtrackPausedAt < openingDuration)
        {
            int startSample = Math.Min(opening.samples - 1,
                (int)(soundtrackPausedAt * opening.frequency));
            sharedOpening.timeSamples = startSample;
            sharedOpening.PlayScheduled(begins);
            begins += (double)(opening.samples - startSample) / opening.frequency;
            sharedLoop.timeSamples = 0;
        }
        else
        {
            AudioClip loop = sharedLoop.clip;
            double loopTime = soundtrackPausedAt - openingDuration;
            sharedLoop.timeSamples = (int)((loopTime * loop.frequency) % loop.samples);
        }
        sharedLoop.PlayScheduled(begins);
    }

    private void ApplySoundtrackPause()
    {
        if (sharedLoop == null) return;
        if (!musicMuted)
        {
            if (double.IsNaN(soundtrackStartDsp)) ScheduleSoundtrack(0.05);
            return;
        }

        if (double.IsNaN(soundtrackStartDsp)) return;
        // Cancela também a entrada já agendada do loop enquanto a introdução toca.
        soundtrackPausedAt = Math.Max(0d, AudioSettings.dspTime - soundtrackStartDsp);
        soundtrackStartDsp = double.NaN;
        sharedOpening?.Stop();
        sharedLoop.Stop();
    }

    private AudioSource AddMusicSource(GameObject root, AudioClip clip, bool loop)
    {
        var source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = loop;
        source.clip = clip;
        if (musicSource != null)
            source.outputAudioMixerGroup = musicSource.outputAudioMixerGroup;
        return source;
    }

    public static void SetRain(bool active)
    {
        if (Instance != null) Instance.rainWanted = active;
    }

    // A pesca mantém um único som em loop enquanto o jogador continua puxando.
    public static void KeepFishingPullAlive()
    {
        if (Instance == null || !Instance.isActiveAndEnabled) return;
        Instance.RefreshFishingPull();
    }

    public static void StopFishingPull()
    {
        if (Instance != null) Instance.fishingPullUntil = 0f;
    }

    private void RefreshFishingPull()
    {
        if (fishingPullSource == null || fishingPullSource.clip == null ||
            effectsMuted || effectsVolume <= 0f || Time.timeScale <= 0f) return;
        fishingPullUntil = Time.unscaledTime + (library != null ? library.reelInputHold : fishingIdleDelay);
        if (!fishingPullSource.isPlaying) fishingPullSource.Play();
    }

    private void Update()
    {
        if (rainSource != null)
        {
            if (rainWanted && !rainSource.isPlaying) rainSource.Play();
            rainBlend = Mathf.MoveTowards(rainBlend, rainWanted ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.05f, library.weatherFade));
            ApplyWeatherVolumes();
            if (!rainWanted && rainBlend <= 0f && rainSource.isPlaying) rainSource.Stop();
        }
        UpdateFishingPull();
    }

    private void UpdateFishingPull()
    {
        if (fishingPullSource == null) return;
        if (Time.timeScale <= 0f) fishingPullUntil = 0f;
        bool active = Time.unscaledTime < fishingPullUntil && !effectsMuted && effectsVolume > 0f;
        fishingPullFade = Mathf.MoveTowards(fishingPullFade, active ? 1f : 0f,
            Time.unscaledDeltaTime / Mathf.Max(0.02f, fishingFadeDuration));
        fishingPullSource.volume = fishingPullFade * fishingPullCueVolume *
            (effectsMuted ? 0f : effectsVolume);
        if (!active && fishingPullFade <= 0f && fishingPullSource.isPlaying)
            fishingPullSource.Stop();
    }

    public static void Play(SoundCue cue)
    {
        if (Instance != null && Instance.isActiveAndEnabled)
            Instance.PlayEffect(cue);
    }

    private void PlayEffect(SoundCue cue)
    {
        if (cue == SoundCue.FishingPull) return;
        if (cue is SoundCue.PanelOpen or SoundCue.PanelClose or
            SoundCue.CardInspectOpen or SoundCue.CardInspectClose)
            cue = SoundCue.UiPaper;
        bool weatherCue = cue is SoundCue.EventRain or SoundCue.EventDrought;
        AudioSource source = cue switch
        {
            SoundCue.UiClick or SoundCue.UiHover or SoundCue.UiPaper or SoundCue.UiRejected or
            SoundCue.CardHover or SoundCue.CardPick or SoundCue.CardReturn or SoundCue.CardDrag or
            SoundCue.CardReward => uiSource,
            SoundCue.EventRain or SoundCue.EventDrought => weatherEffectsSource,
            _ => effectsSource
        };
        float level = weatherCue ? WeatherLevel : effectsMuted ? 0f : effectsVolume;
        if (source == null || !source.isActiveAndEnabled || level <= 0f ||
            !entries.TryGetValue(cue, out CueEntry entry) || entry.clips == null || entry.clips.Length == 0)
            return;
        float now = Time.unscaledTime;
        if (lastPlay.TryGetValue(cue, out float last) && now - last < entry.minInterval)
            return;
        int first = UnityEngine.Random.Range(0, entry.clips.Length);
        int pick = first;
        // Evita repetição imediata de um mesmo sample quando há variações.
        if (entry.clips.Length > 1 && lastVariant.TryGetValue(cue, out int previous) && first == previous)
            pick = (first + 1) % entry.clips.Length;
        AudioClip clip = entry.clips[pick];
        if (clip == null) return;
        lastPlay[cue] = now;
        lastVariant[cue] = pick;
        source.PlayOneShot(clip, Mathf.Clamp01(entry.volume));
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, musicVolume);
        ApplyVolumes();
    }
    public void SetEffectsVolume(float value)
    {
        effectsVolume = Mathf.Clamp01(value);
        if (effectsVolume <= 0f) StopFishingPull();
        PlayerPrefs.SetFloat(EffectsKey, effectsVolume);
        ApplyVolumes();
    }
    public void SetMusicMuted(bool muted)
    {
        if (musicMuted == muted) return;
        musicMuted = muted;
        PlayerPrefs.SetInt(MusicMuteKey, muted ? 1 : 0);
        ApplyMusicVolume();
        if (UsesSoundtrack)
        {
            ApplySoundtrackPause();
        }
        else if (musicSource != null && musicSource.clip != null)
        {
            if (muted && musicSource.isPlaying)
            {
                musicSource.Pause();
                sceneMusicPaused = true;
            }
            else if (!muted)
            {
                if (sceneMusicPaused) musicSource.UnPause();
                else if (!musicSource.isPlaying) musicSource.Play();
                sceneMusicPaused = false;
            }
        }
        MusicMuteChanged?.Invoke();
    }
    public void SetEffectsMuted(bool muted)
    {
        effectsMuted = muted;
        if (muted) StopFishingPull();
        PlayerPrefs.SetInt(EffectsMuteKey, muted ? 1 : 0);
        ApplyVolumes();
    }
    public void SetWeatherVolume(float value)
    {
        weatherVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(WeatherKey, weatherVolume);
        ApplyWeatherVolumes();
    }

    public void SetWeatherMuted(bool muted)
    {
        weatherMuted = muted;
        PlayerPrefs.SetInt(WeatherMuteKey, muted ? 1 : 0);
        ApplyWeatherVolumes();
    }

    private float WeatherLevel => weatherMuted ? 0f : weatherVolume;

    private void ApplyWeatherVolumes()
    {
        float master = WeatherLevel;
        if (weatherEffectsSource != null) weatherEffectsSource.volume = master;
        if (ambienceSource != null)
            ambienceSource.volume = master * (library != null
                ? library.windVolume * Mathf.Lerp(1f, library.windInRain, rainBlend) : 1f);
        if (rainSource != null)
            rainSource.volume = master * library.rainVolume * rainBlend;
    }

    internal static float MusicTransitionGain => musicTransitionGain;

    internal static void SetMusicTransitionGain(float gain)
    {
        musicTransitionGain = Mathf.Clamp01(gain);
        if (Instance != null) Instance.ApplyMusicVolume();
    }

    internal static void RestartMusicAfterTransition()
    {
        var audio = Instance;
        if (audio == null)
        {
            musicTransitionGain = 1f;
            return;
        }

        if (audio.UsesSoundtrack && sharedLoop != null)
        {
            sharedLoop.Stop();
            if (sharedOpening != null) sharedOpening.Stop();
            soundtrackStartDsp = double.NaN;
            soundtrackPausedAt = 0d;
            if (!audio.musicMuted) ScheduleSoundtrack(0.05);
        }
        else if (audio.musicSource != null && audio.musicSource.clip != null)
        {
            audio.musicSource.Stop();
            audio.sceneMusicPaused = false;
            if (!audio.musicMuted) audio.musicSource.Play();
        }
        SetMusicTransitionGain(1f);
    }

    private void ApplyMusicVolume()
    {
        float volume = musicMuted ? 0f : musicVolume * musicTransitionGain;
        if (musicSource != null)
            musicSource.volume = UsesSoundtrack ? 0f : volume;
        if (!UsesSoundtrack) return;
        float gain = volume * library.musicGain;
        if (sharedOpening != null) sharedOpening.volume = gain;
        if (sharedLoop != null) sharedLoop.volume = gain;
    }

    private void ApplyVolumes()
    {
        ApplyMusicVolume();
        float level = effectsMuted ? 0f : effectsVolume;
        if (effectsSource != null) effectsSource.volume = level;
        if (uiSource != null) uiSource.volume = level;
        ApplyWeatherVolumes();
        if (fishingPullSource != null)
            fishingPullSource.volume = fishingPullFade * fishingPullCueVolume *
                (effectsMuted ? 0f : effectsVolume);
    }
    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        StopFishingPull();
        if (fishingPullSource != null) fishingPullSource.Stop();
        fishingPullFade = 0f;
        PlayerPrefs.Save();
    }
    private void OnDisable()
    {
        if (Instance != this) return;
        if (fishingPullSource != null) fishingPullSource.Stop();
        fishingPullUntil = fishingPullFade = 0f;
        rainBlend = 0f;
        if (rainSource != null) rainSource.Stop();
        if (weatherEffectsSource != null) weatherEffectsSource.Stop();
        ApplyVolumes();
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
