using UnityEngine;

// Controle Global da Xilogravura
// Coloque este componente em UM objeto da cena (ex.: "Controle Xilogravura").
// Ele ajusta os traços de xilogravura de TODOS os materiais que usam os shaders Sertao/*
// (XiloCartoon Completo, Casa Sertaneja, Casa Detalhes, Terra Arada, Xilo Lago).
// Os ajustes de cada material continuam valendo: aqui é um multiplicador por cima de todos.
// Funciona no editor (mexeu, já aparece) e no jogo. Desligar o componente volta tudo ao normal.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Sertao/Controle Xilogravura")]
public class XiloControleGlobal : MonoBehaviour
{
    [Header("Traços da xilogravura (todos os objetos)")]
    [Tooltip("Tamanho dos traços. 1 = como está em cada material. 2 = traços com o dobro do tamanho (mais espaçados). 0.5 = traços menores e mais juntinhos.")]
    [Range(0.25f, 4f)] public float tamanhoDosTracos = 1f;

    [Tooltip("Grossura dos traços. 1 = como está em cada material.")]
    [Range(0f, 3f)] public float espessuraDosTracos = 1f;

    [Tooltip("Força dos traços. 1 = como está em cada material. 0 = sem traço.")]
    [Range(0f, 2f)] public float intensidadeDosTracos = 1f;

    static readonly int idAtivo = Shader.PropertyToID("_XiloGlobalAtivo");
    static readonly int idEscala = Shader.PropertyToID("_XiloEscalaTraco");
    static readonly int idEspessura = Shader.PropertyToID("_XiloEspessuraTraco");
    static readonly int idIntensidade = Shader.PropertyToID("_XiloIntensidadeTraco");

    void OnEnable() { Aplicar(); }
    void OnValidate() { Aplicar(); }
    void Update() { Aplicar(); }          // permite animar os valores (ex.: Timeline/Animator)
    void OnDisable() { Shader.SetGlobalFloat(idAtivo, 0f); }

    public void Aplicar()
    {
        if (!isActiveAndEnabled) return;
        Shader.SetGlobalFloat(idAtivo, 1f);
        Shader.SetGlobalFloat(idEscala, tamanhoDosTracos);
        Shader.SetGlobalFloat(idEspessura, espessuraDosTracos);
        Shader.SetGlobalFloat(idIntensidade, intensidadeDosTracos);
    }
}
