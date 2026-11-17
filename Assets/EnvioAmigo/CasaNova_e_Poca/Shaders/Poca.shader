// Sertao / Poca — pocinha / lagoinha de açude para espalhar pelo mapa (URP 17 / Unity 6)
// Igual à Terra Arada: crie um material com este shader e jogue num PLANO deitado em cima do chão.
// O shader desenha a poça (contorno torto e redondinho), a margem de lama molhada que vai secando,
// e o resto do plano fica transparente. Mude a "Semente" para cada poça ter um formato diferente.
// Dica: suba o plano 1 a 2 cm acima do chão.
Shader "Sertao/Poca"
{
    Properties
    {
        [Header(Forma da poca)]
        _Tamanho ("Tamanho", Range(0.1, 0.9)) = 0.6
        _Achatamento ("Achatamento (oval)", Range(0.4, 1.5)) = 0.8
        _Irregularidade ("Contorno Torto", Range(0, 2)) = 0.8
        _Semente ("Semente (muda o formato)", Float) = 0
        _LarguraLama ("Largura da Margem de Lama", Range(0, 0.5)) = 0.14
        _LamaSome ("Lama Some na Areia", Range(0.05, 1)) = 0.45

        [Header(Agua)]
        _CorFunda ("Cor Funda (meio)", Color) = (0.24, 0.27, 0.18, 1)
        _CorRasa ("Cor Rasa (beirada)", Color) = (0.45, 0.42, 0.28, 1)
        _Profundidade ("Fundura (meio mais escuro)", Range(0, 1)) = 0.6
        _VariacaoCor ("Manchas na Agua", Range(0, 1)) = 0.35
        _EscalaManchas ("Tamanho das Manchas", Range(0.1, 5)) = 1
        _Espuma ("Espuminha na Beirada", Range(0, 1)) = 0.5
        _CorEspuma ("Cor da Espuma", Color) = (0.93, 0.9, 0.8, 1)
        _LinhaBeirada ("Linha da Beirada (desenho)", Range(0, 1)) = 0.7

        [Header(Lama)]
        _CorLama ("Lama Molhada", Color) = (0.36, 0.27, 0.18, 1)
        _CorLamaSeca ("Lama Seca", Color) = (0.6, 0.47, 0.32, 1)
        _Rachaduras ("Rachaduras na Lama Seca", Range(0, 1)) = 0.5

        [Header(Luz)]
        _CorSombraAgua ("Cor da Sombra (multiplica)", Color) = (0.58, 0.55, 0.62, 1)
        _ForcaSombra ("Forca da Sombra Projetada", Range(0, 1)) = 0.8
        _InfluenciaLuz ("Cor da Luz da Cena", Range(0, 1)) = 0.2
        _CorBrilho ("Cor do Brilho do Sol", Color) = (1, 0.96, 0.84, 1)
        _BrilhoSol ("Brilho do Sol na Agua", Range(0, 1)) = 0.5
        _TamanhoBrilho ("Tamanho do Brilho", Range(0.05, 1)) = 0.35

        [Header(Ondas de goiva  xilogravura)]
        _CorTraco ("Cor do Traco", Color) = (0.12, 0.14, 0.1, 1)
        _IntensidadeTraco ("Intensidade do Traco", Range(0, 1)) = 0.85
        _LinhasPorMetro ("Linhas por Metro", Range(0.5, 40)) = 5
        _EspessuraTraco ("Espessura do Traco", Range(0.02, 1)) = 0.4
        _ComprimentoTraco ("Comprimento do Traco", Range(1, 30)) = 5
        _Afinamento ("Ponta Fina", Range(0.2, 3)) = 1
        _Tremido ("Tremido (ondas tortas)", Range(0, 2)) = 0.5
        _EscalaTremido ("Tamanho do Tremido", Range(0.02, 5)) = 1
        _AnguloOndas ("Angulo das Ondas", Range(0, 180)) = 0
        _VelocidadeOndas ("Velocidade das Ondas", Range(0, 2)) = 0.08
        _CorReflexo ("Cor dos Reflexos (cortes claros)", Color) = (0.93, 0.9, 0.76, 1)
        _IntensidadeReflexo ("Intensidade dos Reflexos", Range(0, 1)) = 0.4

        [Header(Ondinhas circulares)]
        _Ondinhas ("Quantidade de Ondinhas", Range(0, 1)) = 0.3
        _TamanhoOndinhas ("Tamanho das Ondinhas", Range(0.2, 5)) = 0.8
        _VelocidadeOndinhas ("Velocidade das Ondinhas", Range(0, 2)) = 0.35

        [Header(Borda)]
        [Toggle] _Pontilhado ("Borda da Lama Pontilhada (em vez de transparente)", Float) = 0
        _TamanhoPonto ("Tamanho do Pontilhado", Range(5, 120)) = 45
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Tamanho, _Achatamento, _Irregularidade, _Semente, _LarguraLama, _LamaSome;
            half4 _CorFunda, _CorRasa, _CorEspuma, _CorLama, _CorLamaSeca, _CorSombraAgua, _CorBrilho, _CorTraco, _CorReflexo;
            float _Profundidade, _VariacaoCor, _EscalaManchas, _Espuma, _LinhaBeirada, _Rachaduras;
            float _ForcaSombra, _InfluenciaLuz, _BrilhoSol, _TamanhoBrilho;
            float _IntensidadeTraco, _LinhasPorMetro, _EspessuraTraco, _ComprimentoTraco, _Afinamento, _Tremido, _EscalaTremido;
            float _AnguloOndas, _VelocidadeOndas, _IntensidadeReflexo;
            float _Ondinhas, _TamanhoOndinhas, _VelocidadeOndinhas;
            float _Pontilhado, _TamanhoPonto;
        CBUFFER_END

        // ---- controle global da xilogravura (objeto "Controle Xilogravura" na cena) ----
        float _XiloGlobalAtivo, _XiloEscalaTraco, _XiloEspessuraTraco, _XiloIntensidadeTraco;
        float XiloEscala() { return _XiloGlobalAtivo > 0.5 ? max(_XiloEscalaTraco, 0.05) : 1.0; }
        float XiloEsp()    { return _XiloGlobalAtivo > 0.5 ? max(_XiloEspessuraTraco, 0.0) : 1.0; }
        float XiloInt()    { return _XiloGlobalAtivo > 0.5 ? max(_XiloIntensidadeTraco, 0.0) : 1.0; }
        ENDHLSL

        Pass
        {
            Name "Poca"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fog        : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash11(float n) { return frac(sin(n * 127.1) * 43758.5453); }
            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float2 Hash22(float2 p) { float3 a = frac(p.xyx * float3(123.34, 234.34, 345.65)); a += dot(a, a + 34.45); return frac(float2(a.x * a.y, a.y * a.z)); }
            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + 1), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float s = 0, a = 0.5;
                [unroll] for (int k = 0; k < 3; k++) { s += a * Ruido(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s / 0.875;
            }
            float2 Voronoi(float2 p)
            {
                float2 i = floor(p), f = frac(p); float d1 = 8, d2 = 8;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 o = float2(x, y); float2 r = o + Hash22(i + o) - f; float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
                return float2(sqrt(d1), sqrt(d2) - sqrt(d1));
            }
            float2 Girar(float2 uv, float graus)
            {
                float a = radians(graus), s = sin(a), c = cos(a);
                return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
            }

            // traços de goiva: lascas separadas, grossas no meio e finas nas pontas (igual ao lago)
            float Goivas(float2 uv, float largura, float semente)
            {
                float dens = _LinhasPorMetro / XiloEscala();
                float w = Fbm(uv * _EscalaTremido + semente) - 0.5;
                float x = uv.y * dens + w * _Tremido * 2.0;
                float fila = floor(x);
                float ao = uv.x * dens / _ComprimentoTraco + Hash11(fila + semente) * 7.0;
                float seg = floor(ao);
                float t = frac(ao);
                float rs = Hash21(float2(fila, seg) + semente);
                float tt = t / lerp(0.55, 1.0, rs);
                float dentro = step(tt, 1.0) * step(0.25, rs);
                float ponta = pow(saturate(sin(3.14159 * saturate(tt))), _Afinamento);
                float larg = largura * ponta * lerp(0.7, 1.3, Hash11(rs * 91.7));
                float d = abs(frac(x) - 0.5) * 2.0;
                float aa = fwidth(x) * 1.5 + 1e-4;
                return (1.0 - smoothstep(larg - aa, larg + aa, d)) * dentro * step(0.001, larg) * (1.0 - smoothstep(0.45, 1.05, aa));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 W = i.positionWS;
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(W);
                float tempo = _Time.y;

                // ---------- forma: d > 0 é água, d < 0 é fora ----------
                float2 P = i.uv * 2.0 - 1.0;
                float2 s = float2(_Semente * 17.13 + 3.1, _Semente * 7.71 + 1.7);
                float ang = atan2(P.y, P.x);
                float r = length(float2(P.x, P.y / _Achatamento));
                float ph = _Semente * 1.37;
                float borda = _Tamanho * (1.0 + _Irregularidade * (0.09 * sin(3.0 * ang + 1.3 + ph) + 0.05 * sin(5.0 * ang + 0.4 + ph * 1.7) + 0.035 * sin(8.0 * ang + 2.0 + ph * 2.3)))
                            + (Fbm(P * 1.6 + s) - 0.5) * 0.14 * _Irregularidade;
                float d = borda - r;
                float aaD = fwidth(d) + 1e-5;
                float agua = smoothstep(-aaD, aaD, d);

                // ---------- luz ----------
                Light luz = GetMainLight(TransformWorldToShadowCoord(W));
                float sombra = (1.0 - luz.shadowAttenuation) * _ForcaSombra;

                // ---------- água ----------
                float manchas = Fbm(W.xz * _EscalaManchas + float2(tempo * 0.01, 0));
                float prof = saturate(d / max(_Tamanho * 0.6, 0.01)) * _Profundidade;
                float3 corAgua = lerp(_CorRasa.rgb, _CorFunda.rgb, saturate(prof + (manchas - 0.5) * _VariacaoCor));
                corAgua *= lerp(float3(1, 1, 1), _CorSombraAgua.rgb, sombra);
                corAgua *= lerp(float3(1, 1, 1), saturate(luz.color), _InfluenciaLuz);

                float2 uvO = Girar(W.xz, _AnguloOndas) + float2(tempo * _VelocidadeOndas, 0);
                float tracos = Goivas(uvO, saturate(_EspessuraTraco * XiloEsp()) * (0.8 + 0.6 * sombra), 1.0);
                corAgua = lerp(corAgua, _CorTraco.rgb, saturate(tracos * _IntensidadeTraco * XiloInt()) * smoothstep(0.0, 0.06, d));

                float fresnel = pow(1.0 - saturate(dot(N, V)), 3.0);
                float3 R = reflect(-luz.direction, N);
                float sol = pow(saturate(dot(R, V)), lerp(400.0, 12.0, _TamanhoBrilho));
                float2 uv2 = Girar(W.xz, _AnguloOndas) + float2(-tempo * _VelocidadeOndas * 0.7, 0.37);
                float cortes = Goivas(uv2 * 0.8, saturate(_EspessuraTraco * XiloEsp()) * 0.35 * saturate(0.25 + fresnel * 0.5 + sol * 3.0), 7.0);
                corAgua = lerp(corAgua, _CorReflexo.rgb, saturate(cortes * _IntensidadeReflexo * XiloInt()) * (1.0 - sombra * 0.8));
                corAgua = lerp(corAgua, _CorBrilho.rgb, saturate(sol * 2.0) * _BrilhoSol * (1.0 - sombra) * (0.4 + 0.6 * cortes));

                // ondinhas circulares (peixe, inseto, pingo)
                float2 cel = W.xz / _TamanhoOndinhas;
                float2 ic = floor(cel);
                float h = Hash21(ic + 3.1);
                float2 centro = float2(Hash21(ic + 7.7), Hash21(ic + 1.3)) * 0.3 + 0.35;
                float ciclo = frac(tempo * _VelocidadeOndinhas * (0.6 + 0.8 * h) + h * 9.0);
                float raio = ciclo * 0.3;
                float dist = length(frac(cel) - centro);
                float aaR = length(fwidth(cel)) * 0.75 + 1e-4;
                float anel = (1.0 - smoothstep(0.004, 0.004 + aaR, abs(dist - raio))) + (1.0 - smoothstep(0.003, 0.003 + aaR, abs(dist - raio * 0.6))) * 0.6;
                anel *= (1.0 - ciclo) * step(0.08, ciclo) * step(h, _Ondinhas) * smoothstep(0.03, 0.1, d);
                corAgua = lerp(corAgua, _CorReflexo.rgb, saturate(anel) * 0.7);

                // espuminha batendo na beirada
                float larguraEsp = 0.035 * _Espuma;
                float ondaEsp = (sin(tempo * 0.8 + Fbm(W.xz * 3.0) * 6.0) * 0.5 + 0.5) * larguraEsp * 0.6;
                float esp = (1.0 - smoothstep(larguraEsp * 0.4 + ondaEsp, larguraEsp + ondaEsp, d)) * step(0.45, Fbm(W.xz * 7.0 + tempo * 0.1)) * step(0.001, _Espuma);
                corAgua = lerp(corAgua, _CorEspuma.rgb, esp * 0.85);

                // ---------- lama em volta ----------
                float t = saturate(-d / max(_LarguraLama, 1e-3));                    // 0 na beirada da água, 1 no fim da lama
                float nl = Fbm(W.xz * 2.0 + s);
                float3 corLama = lerp(_CorLama.rgb, _CorLamaSeca.rgb, smoothstep(0.0, 0.8, t + (nl - 0.5) * 0.3));
                corLama *= 0.9 + 0.2 * Fbm(W.xz * 6.0);
                float2 vr = Voronoi(W.xz * 4.0 + s);
                float racha = (1.0 - smoothstep(0.0, 0.02 + fwidth(vr.y), vr.y)) * smoothstep(0.35, 0.7, t) * step(0.001, _Rachaduras) * step(1.0 - _Rachaduras, Fbm(W.xz * 1.5 + 4.0) + 0.25);
                corLama = lerp(corLama, corLama * 0.55, racha);
                float ndl = dot(N, luz.direction);
                float litL = smoothstep(0.42, 0.48, saturate((ndl + 0.6) / 1.4) * lerp(1.0, luz.shadowAttenuation, _ForcaSombra));
                corLama *= lerp(_CorSombraAgua.rgb, float3(1, 1, 1), litL);
                corLama *= lerp(float3(1, 1, 1), saturate(luz.color), _InfluenciaLuz);
                float alfaLama = 1.0 - smoothstep(1.0 - _LamaSome, 1.0, t + (nl - 0.5) * 0.25);
                alfaLama *= step(0.001, _LarguraLama);

                // ---------- junta ----------
                float3 cor = lerp(corLama, corAgua, agua);
                float linha = (1.0 - smoothstep(0.0, aaD * 2.0, abs(d) - aaD * 0.5)) * _LinhaBeirada;
                cor = lerp(cor, _CorTraco.rgb, linha * 0.85);
                float alfa = max(agua, alfaLama);
                alfa *= smoothstep(1.0, 0.92, max(abs(P.x), abs(P.y)));                // nunca corta reto na borda do plano
                if (_Pontilhado > 0.5 && agua < 0.5) alfa = step(Ruido(W.xz * _TamanhoPonto), alfa * 1.05);
                clip(alfa - 0.003);

                cor = MixFog(cor, i.fog);
                return half4(cor, alfa);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
