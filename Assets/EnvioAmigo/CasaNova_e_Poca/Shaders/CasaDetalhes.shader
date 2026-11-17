// Sertao / Casa Detalhes — usa a SUA textura (Base Map) e só acrescenta detalhes por cima,
// clareando/escurecendo a cor que você escolheu (não troca a cor). Cartoon + xilogravura + line art.
// Escolha o tipo de detalhe no material:
//   Parede : manchas suaves, camadas de taipa, sujeira escurecendo a base, rachaduras finas
//   Madeira: veios na direção mais comprida da peça e rachas
//   Telha  : fileiras de telha (capa e canal) com leve variação de tom
//   Chao   : chão batido com manchas e partes mais gastas
//   Tijolo : cada tijolo num tom levemente diferente
//   Tecido : trama leve
//   Liso   : sem detalhe (só cartoon + xilogravura)
Shader "Sertao/Casa Detalhes"
{
    Properties
    {
        [Header(Cor  sua textura)]
        _BaseMap ("Textura (Base Map)", 2D) = "white" {}
        _CorBase ("Cor (multiplica)", Color) = (1, 1, 1, 1)

        [Header(Detalhes)]
        [Enum(Parede,0,Madeira,1,Telha,2,Chao,3,Tijolo,4,Tecido,5,Liso,6)] _Tipo ("Tipo de Detalhe", Float) = 0
        _IntensidadeDetalhes ("Intensidade dos Detalhes", Range(0, 1)) = 0.5
        _Escala ("Escala dos Detalhes", Range(0.25, 4)) = 1
        _ContornoDetalhes ("Linhas dos Detalhes", Range(0, 1)) = 0.5

        [Header(Parede e Chao)]
        _SujeiraBase ("Sujeira na Base", Range(0, 1)) = 0.5
        _AlturaSujeira ("Altura da Sujeira (m)", Range(0, 1.5)) = 0.35
        _Camadas ("Camadas de Taipa", Range(0, 1)) = 0.35
        _Rachaduras ("Rachaduras", Range(0, 1)) = 0.3

        [Header(Madeira)]
        _VeiosPorMetro ("Veios por Metro", Range(2, 80)) = 26
        _Rachas ("Rachas", Range(0, 1)) = 0.4

        [Header(Telha)]
        _LarguraTelha ("Largura da Telha (m)", Range(0.05, 0.5)) = 0.17
        _ComprimentoTelha ("Comprimento da Telha (m)", Range(0.1, 1)) = 0.3
        _CurvaTelha ("Curva da Telha", Range(0, 1.5)) = 0.45

        [Header(Cartoon)]
        _CorSombra ("Cor da Sombra (multiplica)", Color) = (0.6, 0.5, 0.6, 1)
        _LimiteSombra ("Limite da Sombra", Range(0, 1)) = 0.45
        _SuavidadeSombra ("Suavidade da Sombra", Range(0, 0.5)) = 0.03
        _ForcaSombraProjetada ("Forca da Sombra Projetada", Range(0, 1)) = 0.8
        _InfluenciaLuz ("Cor da Luz da Cena", Range(0, 1)) = 0.25

        [Header(Xilogravura)]
        _CorTraco ("Cor do Traco", Color) = (0.12, 0.08, 0.05, 1)
        _IntensidadeTraco ("Intensidade do Traco na Sombra", Range(0, 1)) = 0.6
        _LinhasPorMetro ("Linhas por Metro", Range(2, 80)) = 24
        _EspessuraTraco ("Espessura do Traco", Range(0, 1)) = 0.32

        [Header(Line Art)]
        _CorLineArt ("Cor da Line Art", Color) = (0.1, 0.06, 0.04, 1)
        _EspessuraLineArt ("Espessura da Line Art (pixels)", Range(0, 12)) = 2.5
        _VariacaoLineArt ("Variacao a Mao", Range(0, 1)) = 0.4
        _EscalaVariacao ("Tamanho da Variacao", Range(0.05, 10)) = 1
        _FecharQuinas ("Fechar Quinas (low poly)", Range(0, 1)) = 0.5
        _ContornoInterno ("Contorno Interno (bordas viradas)", Range(0, 1)) = 0.2
        _EmpurrarLineArt ("Esconder Emendas (metros)", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _CorBase, _CorSombra, _CorTraco, _CorLineArt;
            float _Tipo, _IntensidadeDetalhes, _Escala, _ContornoDetalhes;
            float _SujeiraBase, _AlturaSujeira, _Camadas, _Rachaduras;
            float _VeiosPorMetro, _Rachas;
            float _LarguraTelha, _ComprimentoTelha, _CurvaTelha;
            float _LimiteSombra, _SuavidadeSombra, _ForcaSombraProjetada, _InfluenciaLuz;
            float _IntensidadeTraco, _LinhasPorMetro, _EspessuraTraco;
            float _EspessuraLineArt, _VariacaoLineArt, _EscalaVariacao, _FecharQuinas, _ContornoInterno, _EmpurrarLineArt;
        CBUFFER_END

        // ---- controle global da xilogravura (objeto "Controle Xilogravura" na cena) ----
        float _XiloGlobalAtivo, _XiloEscalaTraco, _XiloEspessuraTraco, _XiloIntensidadeTraco;
        float XiloEscala() { return _XiloGlobalAtivo > 0.5 ? max(_XiloEscalaTraco, 0.05) : 1.0; }
        float XiloEsp()    { return _XiloGlobalAtivo > 0.5 ? max(_XiloEspessuraTraco, 0.0) : 1.0; }
        float XiloInt()    { return _XiloGlobalAtivo > 0.5 ? max(_XiloIntensidadeTraco, 0.0) : 1.0; }
        ENDHLSL

        Pass
        {
            Name "CasaDetalhes"
            Tags { "LightMode" = "UniversalForward" }
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
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float H11(float n) { return frac(sin(n * 127.1) * 43758.5453); }
            float H21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float2 H22(float2 p) { float3 a = frac(p.xyx * float3(123.34, 234.34, 345.65)); a += dot(a, a + 34.45); return frac(float2(a.x * a.y, a.y * a.z)); }
            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(H21(i), H21(i + float2(1, 0)), u.x), lerp(H21(i + float2(0, 1)), H21(i + 1), u.x), u.y);
            }
            float Fbm(float2 p) { return Ruido(p) * 0.57 + Ruido(p * 2.03 + 17.1) * 0.29 + Ruido(p * 4.07 + 3.7) * 0.14; }
            float2 Voronoi(float2 p)
            {
                float2 i = floor(p), f = frac(p); float d1 = 8, d2 = 8;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 o = float2(x, y); float2 r = o + H22(i + o) - f; float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
                return float2(sqrt(d1), sqrt(d2) - sqrt(d1));
            }
            float Linha(float d, float larg, float aa) { return 1.0 - smoothstep(larg, larg + aa, d); }

            // Detalhe = fator de brilho (1 = igual à sua cor), linhas e normal falsa.
            void Detalhe(float3 W, float3 N, out float fator, out float linhas, out float3 Nn)
            {
                fator = 1; linhas = 0; Nn = N;
                int tipo = (int)round(_Tipo);
                if (tipo == 6) return;
                float3 origem = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float3 P = (W - origem) / max(_Escala, 0.01);
                float3 bmin = unity_RendererBounds_Min.xyz, tam = unity_RendererBounds_Max.xyz - bmin;
                float hb = W.y - bmin.y;
                float semente = H21(origem.xz * 1.73 + origem.y * 3.1);
                bool ladoX = abs(N.x) > abs(N.z);
                float ao = ladoX ? P.z : P.x;
                float2 uv = abs(N.y) > 0.7 ? P.xz : float2(ao, P.y);

                if (tipo == 0) // PAREDE
                {
                    float m = Fbm(uv * 1.6 + semente * 7.0);
                    fator = 0.9 + 0.2 * m;
                    float cam = sin((P.y + (Fbm(float2(ao * 0.8, 3.0)) - 0.5) * 0.12) / 0.33 * 6.2832);
                    fator *= 1.0 + _Camadas * 0.06 * cam;
                    float sujo = 1.0 - smoothstep(0.0, _AlturaSujeira, hb - (Fbm(float2((P.x + P.z) * 2.0, 1.0)) - 0.5) * _AlturaSujeira * 0.7);
                    fator *= 1.0 - _SujeiraBase * 0.35 * sujo;
                    float2 vr = Voronoi(uv * 2.4 + semente * 5.0);
                    float racha = Linha(vr.y, 0.01, fwidth(vr.y) + 1e-4) * smoothstep(0.6, 0.72, Fbm(uv * 0.9 + 11.0)) * step(0.001, _Rachaduras) * step(1.0 - _Rachaduras, Fbm(uv * 3.0 + 1.0) + 0.3);
                    linhas = racha;
                    if (N.y > 0.7 && hb < tam.y * 0.45) { fator = 0.9 + 0.2 * Fbm(P.xz * 1.3); linhas = 0; }   // piso dentro da peça
                }
                else if (tipo == 1) // MADEIRA
                {
                    // a casa é uma malha só: o veio segue a face (de pé = veio em pé; deitada = veio no comprido)
                    float s, al;
                    if (abs(N.y) < 0.7) { s = P.x + P.z; al = P.y; }
                    else                { s = P.x * 0.2 + P.z; al = P.x; }
                    s += semente * 3.1;
                    float torto = Fbm(float2(al * 1.5, s * 0.7)) * 0.08;
                    float k = (s + torto) * _VeiosPorMetro;
                    float veio = pow(abs(sin(k * 3.14159)), 5.0);
                    fator = 1.08 - 0.22 * veio - 0.1 * Fbm(float2(s * 6.0, al * 0.8));
                    fator *= 0.94 + 0.12 * semente;
                    float aa = fwidth(s * 4.1) + 1e-4;
                    linhas = Linha(abs(frac(s * 4.1 + torto * 3.0) - 0.5), 0.02, aa) * step(1.0 - _Rachas * 0.6, Fbm(float2(al * 3.0, floor(s * 4.1) * 7.3)));
                }
                else if (tipo == 2) // TELHA
                {
                    if (N.y < -0.2) { fator = 0.85; return; }
                    float3 Ng = normalize(cross(ddy(W), ddx(W))); if (dot(Ng, N) < 0) Ng = -Ng;
                    bool porZ = abs(Ng.z) >= abs(Ng.x);
                    float2 d = porZ ? float2(0, Ng.z >= 0 ? 1 : -1) : float2(Ng.x >= 0 ? 1 : -1, 0);
                    float across = (porZ ? P.x : P.z) / _LarguraTelha;
                    float along = dot(P.xz, d) / _ComprimentoTelha;
                    float ci = floor(across), f = frac(across);
                    float capa = step(0.5, frac(ci * 0.5));
                    float a2 = along + capa * 0.5 + H11(ci) * 0.2; float rj = floor(a2), fr = frac(a2);
                    float curva = sin(f * 3.14159);
                    fator = capa > 0.5 ? (0.9 + 0.18 * curva) : (1.02 - 0.14 * curva);
                    fator *= 0.92 + 0.16 * H21(float2(ci, rj) + semente * 11.0);
                    fator *= lerp(0.75, 1.0, smoothstep(0.0, 0.2, fr));
                    Nn = normalize(N + float3(porZ ? 1 : 0, 0, porZ ? 0 : 1) * cos(f * 3.14159) * _CurvaTelha * (capa > 0.5 ? 1.0 : -1.0));
                    linhas = max(Linha(min(f, 1 - f), 0.025, fwidth(across) + 1e-4), Linha(fr, 0.03, fwidth(a2) + 1e-4));
                }
                else if (tipo == 3) // CHAO
                {
                    float2 q = abs(N.y) > 0.7 ? P.xz : uv;
                    fator = 0.88 + 0.24 * Fbm(q * 1.2 + semente * 3.0);
                    float gasto = smoothstep(0.55, 0.8, Fbm(q * 0.5 + 4.0));
                    fator *= 1.0 + gasto * 0.15;
                    float2 vr = Voronoi(q * 2.0 + 7.0);
                    linhas = Linha(vr.y, 0.008, fwidth(vr.y) + 1e-4) * step(1.0 - _Rachaduras, Fbm(q * 1.5)) * step(0.001, _Rachaduras);
                }
                else if (tipo == 4) // TIJOLO
                {
                    fator = 0.82 + 0.36 * semente;
                    fator *= 0.94 + 0.12 * Fbm(uv * 14.0);
                }
                else // TECIDO
                {
                    fator = 0.94 + 0.06 * sin(dot(P, float3(260, 240, 280))) * sin(dot(P, float3(-250, 270, 230)));
                }
                float ki = _IntensidadeDetalhes * 2.0;              // 0.5 = normal, 1 = dobro, 0 = sem detalhe
                fator = 1.0 + (fator - 1.0) * ki;
                linhas *= saturate(ki);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 W = i.positionWS;
                float3 N = normalize(i.normalWS);
                float3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _CorBase.rgb;

                float fator, linhas; float3 Nn;
                Detalhe(W, N, fator, linhas, Nn);
                float3 cor0 = base * max(fator, 0.0);
                cor0 = lerp(cor0, base * 0.45, saturate(linhas) * _ContornoDetalhes);   // linha = a sua cor bem mais escura

                Light luz = GetMainLight(TransformWorldToShadowCoord(W));
                float lum = saturate((dot(Nn, luz.direction) + 0.6) / 1.4);
                lum *= lerp(1.0, luz.shadowAttenuation, _ForcaSombraProjetada);
                float sv = max(_SuavidadeSombra, 0.001);
                float t = smoothstep(_LimiteSombra - sv, _LimiteSombra + sv, lum);
                float3 cor = lerp(cor0 * _CorSombra.rgb, cor0, t);
                cor *= lerp(float3(1, 1, 1), saturate(luz.color), _InfluenciaLuz);

                // traço de xilogravura na sombra
                float3 n = abs(N);
                float2 uvx = n.y >= max(n.x, n.z) ? W.xz : (n.x >= n.z ? W.zy : W.xy);
                float a = n.y >= max(n.x, n.z) ? 0.785 : 0.35;
                float across = (uvx.y * cos(a) + uvx.x * sin(a)) * (_LinhasPorMetro / XiloEscala());
                across += (sin(dot(W, float3(1.7, 2.3, 1.1)) * 3.0) * 0.6 + sin(dot(W, float3(-2.9, 1.3, 2.7)) * 5.0) * 0.4) * 0.35;
                float perfil = abs(frac(across) * 2.0 - 1.0);
                float aa = max(fwidth(across) * 2.0, 1e-4);
                float larg = saturate(_EspessuraTraco * XiloEsp()) * saturate((1.0 - t) * 1.2);
                float falha = step(0.55, sin((uvx.x * cos(a) - uvx.y * sin(a)) * (_LinhasPorMetro / XiloEscala()) * 0.33 + floor(across) * 2.1));
                float hach = lerp(smoothstep(1.0 - larg - aa, 1.0 - larg + aa, perfil), larg, smoothstep(0.3, 1.0, aa)) * (1.0 - falha) * step(0.001, larg);
                cor = lerp(cor, _CorTraco.rgb, saturate(hach * _IntensidadeTraco * XiloInt()));

                float3 V = GetWorldSpaceNormalizeViewDir(W);
                float ndv = saturate(dot(N, V));
                float lim = _ContornoInterno * 0.5;
                float rim = (1.0 - smoothstep(lim - 0.03, lim + 0.03, ndv)) * step(0.001, _ContornoInterno);
                cor = lerp(cor, _CorLineArt.rgb, rim);

                cor = MixFog(cor, i.fog);
                return half4(cor, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "LineArt"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 centro = v.positionOS.xyz;
                float3 dirOS = normalize(lerp(v.normalOS, normalize(centro + 1e-5), _FecharQuinas));
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                posWS -= GetWorldSpaceNormalizeViewDir(posWS) * _EmpurrarLineArt;
                float3 dirWS = TransformObjectToWorldNormal(dirOS);
                float4 cs = TransformWorldToHClip(posWS);
                float3 dirCS = mul((float3x3)GetWorldToHClipMatrix(), dirWS);
                float var = sin(dot(posWS, float3(3.1, 5.3, 4.2)) * _EscalaVariacao) * 0.5 + sin(dot(posWS, float3(-7.7, 2.9, 6.1)) * _EscalaVariacao) * 0.5;
                float px = _EspessuraLineArt * max(1.0 + _VariacaoLineArt * var, 0.0);
                float2 d = dirCS.xy; float len = length(d); d = len > 1e-5 ? d / len : float2(0, 0);
                cs.xy += d * px * 2.0 / _ScreenParams.xy * cs.w;
                o.positionCS = cs; o.fog = ComputeFogFactor(cs.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { clip(_EspessuraLineArt - 0.001); return half4(MixFog(_CorLineArt.rgb, i.fog), 1); }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 L = normalize(_LightPosition - posWS);
                #else
                    float3 L = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(posWS, nWS, L)));
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
