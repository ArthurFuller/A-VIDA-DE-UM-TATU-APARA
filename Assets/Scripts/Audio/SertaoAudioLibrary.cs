using UnityEngine;

[CreateAssetMenu(menuName = "Sertão/Biblioteca de áudio")]
public sealed class SertaoAudioLibrary : ScriptableObject
{
    [Header("Trilha compartilhada")]
    public AudioClip musicOpening;
    public AudioClip musicLoop;
    [Range(0f, 1f)] public float musicGain = 0.62f;
    [Header("Ambiência")]
    public AudioClip wind;
    public AudioClip rain;
    [Range(0f, 1f)] public float windVolume = 0.26f;
    [Range(0f, 1f)] public float rainVolume = 0.58f;
    [Range(0f, 1f)] public float windInRain = 0.28f;
    [Min(0.05f)] public float weatherFade = 0.85f;
    [Min(0.1f)] public float reelInputHold = 0.42f;
    public AudioManager.CueEntry[] effects = System.Array.Empty<AudioManager.CueEntry>();
}
