using UnityEngine;

public sealed class MandalaPlowedGroundSettings : ScriptableObject
{
    [SerializeField] private Material material;
    [SerializeField, Tooltip("Altura da superfície arada em relação ao pivô dos terrenos.")]
    private float groundOffset = 0.02f;

    [Header("Canteiro e junção")]
    [SerializeField, Range(0.8f, 1f), Tooltip("Quanto do espaço do slot o canteiro ocupa.")]
    private float tamanhoCanteiro = 0.96f;
    [SerializeField, Range(0.35f, 1f), Tooltip("Aumente para ter cantos mais redondos.")]
    private float arredondamentoCantos = 0.68f;
    [SerializeField, Range(0.7f, 1f), Tooltip("Largura da emenda em relação à largura do canteiro.")]
    private float larguraJuncao = 0.96f;

    internal Material Material => material;
    internal float GroundOffset => groundOffset;
    internal float PlotFill => tamanhoCanteiro;
    internal float CornerRoundness => arredondamentoCantos;
    internal float JoinWidth => larguraJuncao;
}
