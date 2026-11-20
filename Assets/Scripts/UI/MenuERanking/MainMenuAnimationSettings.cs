using UnityEngine;

public sealed class MainMenuAnimationSettings : ScriptableObject
{
    public const string ResourceName = "SertaoMainMenuAnimationSettings";
    public const string AssetPath = "Assets/Resources/" + ResourceName + ".asset";

    [Min(0.1f)] public float slideSeconds = 0.4f;
    [Min(0.1f)] public float fadeSeconds = 0.36f;
    [Range(0.8f, 1f)] public float enteringScale = 0.975f;
    [Min(0.1f)] public float introDarkHoldSeconds = 0.18f;
    [Min(0.15f)] public float introRevealSeconds = 0.65f;
    [Min(0.1f)] public float logoDropSeconds = 0.50f;
    [Min(0.1f)] public float buttonDropSeconds = 0.27f;
    [Min(0f)] public float floatingHeight = 3.5f;
    [Min(0f)] public float floatingAngle = 1.1f;
    [InspectorName("Ondulação da moldura")] public RippleSettings borderRipple = new();

    [System.Serializable]
    public class RippleSettings
    {
        internal const float DefaultLeftLineInset = 0.037f;
        internal const float DefaultRightLineInset = 0.046f;

        [InspectorName("Ativar teste")] public bool enabled = true;
        public Shader rippleShader;
        [Range(0.025f, 0.18f), InspectorName("Filete superior"),
            Tooltip("Distância do filete superior à borda de cima, em fração da altura.")]
        public float topLineInset = 0.060f;
        [Range(0.025f, 0.18f), InspectorName("Filete inferior"),
            Tooltip("Distância do filete inferior à borda de baixo, em fração da altura.")]
        public float bottomLineInset = 0.068f;
        [Range(0.015f, 0.18f), InspectorName("Filete esquerdo"),
            Tooltip("Distância do filete à borda esquerda, em fração da largura.")]
        public float leftLineInset = DefaultLeftLineInset;
        [Range(0.015f, 0.18f), InspectorName("Filete direito"),
            Tooltip("Distância do filete à borda direita, em fração da largura.")]
        public float rightLineInset = DefaultRightLineInset;
        [Range(0.008f, 0.028f), InspectorName("Largura da faixa"),
            Tooltip("Meia largura da faixa animada, em fração da altura.")]
        public float lineHalfWidth = 0.018f;
        [Range(2f, 12f), InspectorName("Ondas por filete")] public float wavesPerLine = 5.5f;
        [Range(0f, 12f)] public float impactAmplitude = 5.2f;
        [Range(0f, 4f)] public float idleAmplitude = 1.35f;
        [Range(0.4f, 3.5f)] public float idleSpeed = 1.45f;

        internal void CopyFrom(RippleSettings other)
        {
            enabled = other.enabled;
            rippleShader = other.rippleShader;
            topLineInset = other.topLineInset;
            bottomLineInset = other.bottomLineInset;
            leftLineInset = other.leftLineInset;
            rightLineInset = other.rightLineInset;
            lineHalfWidth = other.lineHalfWidth;
            wavesPerLine = other.wavesPerLine;
            impactAmplitude = other.impactAmplitude;
            idleAmplitude = other.idleAmplitude;
            idleSpeed = other.idleSpeed;
        }
    }
}
