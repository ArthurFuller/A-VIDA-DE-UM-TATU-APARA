// Sertao / Terra Arada — canteiro de terra preparada para plantio (URP 17 / Unity 6)
// Uso: crie um material com este shader e jogue num PLANO deitado em cima do chão de areia.
// O shader desenha o canteiro com contorno torto, leiras curvas e a borda some (fica transparente),
// então a areia de baixo aparece em volta. Não precisa configurar nada no projeto.
//
// Dicas:
// - Suba o plano 1 ou 2 cm acima do chão.
// - "Altura das leiras" é em metros e já funciona num plano simples (a luz faz o relevo).
// - Para o relevo de verdade (leira saindo do chão), ligue "Deslocar Vertices" e use um plano
//   com bastante subdivisão (ex.: o Plano_TerraArada que vem junto, ou subdivida no Blender).
// - Mude a "Semente" para cada canteiro ter um formato diferente.
Shader "Sertao/Terra Arada"
{
    Properties
    {
        [Header(Forma do canteiro)]
        _Tamanho ("Tamanho", Range(0.2, 1)) = 0.84
        _Achatamento ("Achatamento (oval)", Range(0.4, 1.5)) = 0.78
        _Irregularidade ("Contorno Torto", Range(0, 2)) = 0.8
        _Suavidade ("Borda que Some na Areia", Range(0.01, 0.4)) = 0.09
        _FaixaSolta ("Faixa de Terra Solta em Volta", Range(0, 0.3)) = 0.09
        _Semente ("Semente (muda o formato)", Float) = 0

        [Header(Leiras)]
        _QuantidadeLeiras ("Quantidade de Leiras", Range(2, 60)) = 24
        _AnguloLeiras ("Angulo das Leiras", Range(-90, 90)) = 14
        _Curvatura ("Curvatura das Leiras", Range(-2, 2)) = 0.35
        _Tortura ("Leiras Tortinhas", Range(0, 1)) = 0.35
        _AlturaLeiras ("Altura das Leiras (metros)", Range(0, 0.5)) = 0.08
        [Toggle] _Deslocar ("Deslocar Vertices (plano subdividido)", Float) = 0
        _Torroes ("Torroes", Range(0, 1)) = 0.5
        _EscalaTorroes ("Tamanho dos Torroes", Range(5, 120)) = 40

        [Header(Cores)]
        _CorSulco ("Cor do Fundo do Sulco", Color) = (0.337, 0.212, 0.133, 1)
        _CorTopo ("Cor do Topo da Leira", Color) = (0.573, 0.392, 0.243, 1)
        _CorTerraSolta ("Cor da Terra Solta", Color) = (0.675, 0.502, 0.329, 1)
        _TexturaTerra ("Textura da Terra (opcional, multiplica)", 2D) = "white" {}

        [Header(Cartoon)]
        _CorSombra ("Cor da Sombra (multiplica)", Color) = (0.62, 0.52, 0.50, 1)
        _LimiteSombra ("Limite da Sombra", Range(-1, 1)) = 0.25
        _SuavidadeSombra ("Suavidade da Sombra", Range(0, 0.5)) = 0.04
        _ForcaSombraProjetada ("Forca da Sombra Projetada", Range(0, 1)) = 0.8
        _InfluenciaLuz ("Cor da Luz da Cena", Range(0, 1)) = 0.4

        [Header(Xilogravura)]
        _CorTraco ("Cor do Traco", Color) = (0.13, 0.08, 0.05, 1)
        _TracoSulco ("Traco no Fundo do Sulco", Range(0, 1)) = 0.75
        _EspessuraSulco ("Espessura do Traco do Sulco", Range(0.01, 0.4)) = 0.09
        _Falhas ("Falhas no Traco", Range(0, 1)) = 0.35
        _Hachura ("Hachura na Sombra", Range(0, 1)) = 0.35
        _HachurasPorMetro ("Hachuras por Metro", Range(2, 60)) = 28
        _EspessuraHachura ("Espessura da Hachura", Range(0.05, 0.9)) = 0.22
        _AnguloHachura ("Angulo da Hachura", Range(0, 180)) = 35

        [Header(Borda)]
        [Toggle] _Pontilhado ("Borda Pontilhada (em vez de transparente)", Float) = 0
        _TamanhoPonto ("Tamanho do Pontilhado", Range(5, 120)) = 45

        [HideInInspector] _MandalaGround ("Solo da Mandala", Float) = 0
        [HideInInspector] _GroundMaskFrom ("Máscara anterior", 2D) = "black" {}
        [HideInInspector] _GroundMaskTo ("Máscara atual", 2D) = "black" {}
        [HideInInspector] _GroundMaskTransform ("UV da máscara", Vector) = (1, 1, 0, 0)
        [HideInInspector] _GroundBlend ("Transição", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TexturaTerra_ST;
            half4 _CorSulco, _CorTopo, _CorTerraSolta, _CorSombra, _CorTraco;
            float _Tamanho, _Achatamento, _Irregularidade, _Suavidade, _FaixaSolta, _Semente;
            float _QuantidadeLeiras, _AnguloLeiras, _Curvatura, _Tortura, _AlturaLeiras, _Deslocar, _Torroes, _EscalaTorroes;
            float _LimiteSombra, _SuavidadeSombra, _ForcaSombraProjetada, _InfluenciaLuz;
            float _TracoSulco, _EspessuraSulco, _Falhas, _Hachura, _HachurasPorMetro, _EspessuraHachura, _AnguloHachura;
            float _Pontilhado, _TamanhoPonto;
            float4 _GroundMaskTransform;
            float _MandalaGround, _GroundBlend;
        CBUFFER_END

        TEXTURE2D(_GroundMaskFrom); SAMPLER(sampler_GroundMaskFrom);
        TEXTURE2D(_GroundMaskTo); SAMPLER(sampler_GroundMaskTo);

        float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
        float Ruido(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + 1), u.x), u.y);
        }
        float Fbm(float2 p) { return Ruido(p) * 0.67 + Ruido(p * 2.03 + 17.1) * 0.33; }

        float2 Sem() { return float2(_Semente * 17.13 + 3.1, _Semente * 7.71 + 1.7); }

        // m = 1 dentro do canteiro (cai suave na borda); solta = faixa de terra solta em volta
        void Mascara(float2 uv, out float m, out float solta)
        {
            m = 0.0;
            solta = 0.0;
            if (_MandalaGround > 0.5)
            {
                float2 maskUV = uv * _GroundMaskTransform.xy + _GroundMaskTransform.zw;
                float after = SAMPLE_TEXTURE2D_LOD(_GroundMaskTo, sampler_GroundMaskTo, maskUV, 0).r;
                float field = after;
                if (_GroundBlend < 1.0)
                {
                    float before = SAMPLE_TEXTURE2D_LOD(_GroundMaskFrom, sampler_GroundMaskFrom, maskUV, 0).r;
                    field = lerp(before, after, _GroundBlend);
                }
                // Vermelho codifica distância com sinal no intervalo de -0.5 m a 0.5 m.
                float d = field - 0.5;
                m = smoothstep(0.0, max(0.005, _Suavidade * 0.72), d);
                solta = smoothstep(-max(0.001, _FaixaSolta * 0.72), 0.0, d) * (1.0 - m);
                solta *= step(0.0001, _FaixaSolta) * smoothstep(0.25, 0.65, Fbm(uv * 18.0 + Sem() * 2.0));
                return;
            }

            float2 P = uv * 2.0 - 1.0;
            float2 s = Sem();
            float ang = atan2(P.y, P.x);
            float r = length(float2(P.x, P.y / _Achatamento));
            float ph = _Semente * 1.37;
            float borda = _Tamanho * (1.0 + _Irregularidade * (0.10 * sin(3.0 * ang + 1.3 + ph) + 0.06 * sin(5.0 * ang + 0.4 + ph * 1.7) + 0.05 * sin(8.0 * ang + 2.0 + ph * 2.3)))
                        + (Fbm(P * 1.6 + s) - 0.5) * 0.16 * _Irregularidade;
            float d = borda - r;
            m = smoothstep(0.0, _Suavidade, d);
            solta = smoothstep(-_FaixaSolta, 0.0, d) * (1.0 - m) * step(0.0001, _FaixaSolta);
            solta *= smoothstep(0.25, 0.65, Fbm(P * 9.0 + s * 2.0));
        }

        // x = coordenada contínua através das leiras (cada inteiro = um sulco), v = ao longo da leira
        void Leiras(float2 uv, out float x, out float v)
        {
            float2 P = uv * 2.0 - 1.0;
            float a = radians(_AnguloLeiras), c = cos(a), sn = sin(a);
            float u = P.x * c + P.y * sn;
            v = -P.x * sn + P.y * c;
            float curva = _MandalaGround > 0.5 ? 1.0 - cos(v * 1.7) : v * v;
            u += _Curvatura * 0.5 * curva + (Fbm(P * 4.0 + Sem() * 3.0) - 0.5) * 0.15 * _Tortura;
            x = u * _QuantidadeLeiras * 0.5;
        }

        // altura 0..1 (topo da leira = 1), já sumindo na borda
        float Altura(float2 uv, out float leira, out float m, out float solta)
        {
            Mascara(uv, m, solta);
            float x, v; Leiras(uv, x, v);
            leira = pow(saturate(cos((frac(x) - 0.5) * 6.28318) * 0.5 + 0.5), 1.5);
            float torrao = (Fbm(uv * _EscalaTorroes + Sem()) - 0.5) * _Torroes * 0.7;
            return (leira + torrao) * pow(saturate(m), 1.6);
        }
        float AlturaSo(float2 uv) { float l, m, s; return Altura(uv, l, m, s); }
        ENDHLSL

        Pass
        {
            Name "TerraArada"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TexturaTerra); SAMPLER(sampler_TexturaTerra);

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
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                if (_Deslocar > 0.5) p += n * AlturaSo(v.uv) * _AlturaLeiras;
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = n;
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float3 W = i.positionWS;
                float3 N = normalize(i.normalWS);

                float leira, m, solta;
                float h = Altura(uv, leira, m, solta);

                // ---------- relevo: gradiente da altura levado para o mundo (funciona em qualquer tamanho de plano) ----------
                float e = 0.0015;
                float dhdu = (AlturaSo(uv + float2(e, 0)) - AlturaSo(uv - float2(e, 0))) / (2.0 * e);
                float dhdv = (AlturaSo(uv + float2(0, e)) - AlturaSo(uv - float2(0, e))) / (2.0 * e);
                float3 dp1 = ddx(W), dp2 = ddy(W);
                float2 duv1 = ddx(uv), duv2 = ddy(uv);
                float3 dp2perp = cross(dp2, N), dp1perp = cross(N, dp1);
                float det = dot(dp1, dp2perp);
                det = abs(det) < 1e-12 ? 1e-12 : det;
                float3 gradU = (dp2perp * duv1.x + dp1perp * duv2.x) / det;
                float3 gradV = (dp2perp * duv1.y + dp1perp * duv2.y) / det;
                float3 gradH = (dhdu * gradU + dhdv * gradV) * _AlturaLeiras;
                float3 Np = normalize(N - gradH);

                // ---------- cor da terra ----------
                float torr = Fbm(uv * _EscalaTorroes * 0.5 + Sem());
                float3 terra = lerp(_CorSulco.rgb, _CorTopo.rgb, leira) * (0.88 + 0.24 * torr);
                terra *= SAMPLE_TEXTURE2D(_TexturaTerra, sampler_TexturaTerra, uv * _TexturaTerra_ST.xy + _TexturaTerra_ST.zw).rgb;
                float3 cor = lerp(_CorTerraSolta.rgb * (0.9 + 0.2 * torr), terra, m);

                // ---------- luz cartoon ----------
                Light luz = GetMainLight(TransformWorldToShadowCoord(W));
                float ndl = dot(Np, luz.direction);
                float lit = smoothstep(_LimiteSombra - _SuavidadeSombra, _LimiteSombra + _SuavidadeSombra, ndl);
                lit *= lerp(1.0, luz.shadowAttenuation, _ForcaSombraProjetada);
                cor *= lerp(_CorSombra.rgb, float3(1, 1, 1), lit);
                cor *= lerp(float3(1, 1, 1), saturate(luz.color), _InfluenciaLuz);

                // ---------- xilogravura: traço de goiva no fundo de cada sulco, com falhas ----------
                float x, v; Leiras(uv, x, v);
                float dist = abs(frac(x + 0.5) - 0.5);                       // 0 no fundo do sulco
                float aa = fwidth(x) * 1.2 + 1e-4;
                float fila = floor(x + 0.5);
                float larg = _EspessuraSulco * lerp(0.6, 1.4, Ruido(float2(v * _QuantidadeLeiras * 0.4, fila * 3.7)));
                float falha = step(_Falhas, Ruido(float2(v * _QuantidadeLeiras * 0.9 + 5.0, fila * 7.3 + _Semente)));
                float sulco = (1.0 - smoothstep(larg - aa, larg + aa, dist)) * falha * smoothstep(0.35, 0.9, m);
                cor = lerp(cor, _CorTraco.rgb, sulco * _TracoSulco);

                // hachura diagonal só na sombra (lado da leira que não pega sol)
                float ah = radians(_AnguloHachura);
                float t = dot(W.xz, float2(cos(ah), sin(ah))) * _HachurasPorMetro;
                float aah = fwidth(t) * 1.2 + 1e-4;
                float dh = abs(frac(t) - 0.5) * 2.0;
                float hw = _EspessuraHachura * lerp(0.5, 1.2, Ruido(W.xz * 3.0));
                float hach = (1.0 - smoothstep(hw - aah, hw + aah, 1.0 - dh)) * (1.0 - lit) * m;
                hach *= step(_Falhas * 0.8, Ruido(float2(t * 0.25, dot(W.xz, float2(-sin(ah), cos(ah))) * 2.0)));
                cor = lerp(cor, _CorTraco.rgb, hach * _Hachura);

                // ---------- transparência da borda ----------
                float alfa = max(m, solta * 0.85);
                float2 P = uv * 2.0 - 1.0;
                if (_MandalaGround < 0.5)
                    alfa *= 1.0 - smoothstep(0.92, 1.0, max(abs(P.x), abs(P.y)));
                if (_Pontilhado > 0.5)
                {
                    float pts = Ruido(W.xz * _TamanhoPonto);
                    alfa = step(pts, alfa * 1.05);
                }
                clip(alfa - 0.003);

                cor = MixFog(cor, i.fog);
                return half4(cor, alfa);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
