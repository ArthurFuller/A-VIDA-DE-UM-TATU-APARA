using UnityEngine;
using UnityEngine.UI;

// Botão da barra superior: a arte acompanha a pausa da trilha.
public sealed class AudioLizardButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private AudioLizardVisual visual;
    private AudioManager audioManager;

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(ToggleMusic);
    }

    private void Start()
    {
        audioManager = AudioManager.Instance;
        if (audioManager == null) return;
        audioManager.MusicMuteChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(ToggleMusic);
        if (audioManager != null) audioManager.MusicMuteChanged -= Refresh;
    }

    private void ToggleMusic()
    {
        if (audioManager != null) audioManager.SetMusicMuted(!audioManager.MusicMuted);
    }

    private void Refresh()
    {
        if (audioManager != null && visual != null)
            visual.SetMuted(audioManager.MusicMuted);
    }
}
