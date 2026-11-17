// Sertao / Xilo Cartoon Completo
// UM shader de material: Cartoon + Xilogravura + Line Art (contorno de verdade em volta do objeto).
// Uso: crie um material com este shader e arraste no objeto. Nada mais precisa ser configurado.
// Unity 6 / URP 17.
Shader "Sertao/XiloCartoon Completo"
{
    Properties
    {
        [Header(Cartoon)]
        [MainTexture] _BaseMap ("Textura Base", 2D) = "white" {}
        [MainColor] _CorBase ("Cor Base", Color) = (0.77, 0.39, 0.23, 1)
        _CorSombra ("Cor da Sombra (multiplica)", Color) = (0.55, 0.45, 0.55, 1)
        _LimiteSombra ("Limite da Sombra", Range(0, 1)) = 0.5
        _SuavidadeSombra ("Suavidade da Sombra", Range(0, 0.5)) = 0.03
        _ForcaSombraProjetada ("Forca da Sombra Projetada", Range(0, 1)) = 1
        _InfluenciaLuz ("Cor da Luz da Cena", Range(0, 1)) = 0.3

        [Header(Xilogravura)]
        _CorTraco ("Cor do Traco", Color) = (0.17, 0.10, 0.07, 1)
        _IntensidadeTraco ("Intensidade do Traco", Range(0, 1)) = 1
        _QuantidadeTraco ("Quantidade de Traco", Range(0, 1)) = 0.6
        _LinhasPorMetro ("Linhas por Metro", Range(1, 100)) = 12
        _EspessuraTraco ("Espessura do Traco", Range(0.05, 1)) = 0.5
        _Tremido ("Tremido (linhas tortas)", Range(0, 2)) = 0.3
        _EscalaTremido ("Tamanho do Tremido", Range(0.1, 20)) = 2
        _Curvatura ("Curvatura (linhas redondas)", Range(0, 3)) = 0.4
        _TamanhoCurva ("Tamanho da Curva", Range(0.05, 10)) = 1
        _AnguloTraco ("Angulo do Traco", Range(0, 180)) = 0
        _Falhas ("Falhas na Linha", Range(0, 1)) = 0.25
        _TracoNoObjeto ("Traco Preso ao Objeto (0 mundo, 1 objeto)", Range(0, 1)) = 0
        _CorTracoLuz ("Cor do Traco de Luz", Color) = (0.98, 0.94, 0.85, 1)
        _IntensidadeTracoLuz ("Intensidade do Traco de Luz", Range(0, 1)) = 0

        [Header(Line Art)]
        _CorLineArt ("Cor da Line Art", Color) = (0.12, 0.07, 0.05, 1)
        _EspessuraLineArt ("Espessura da Line Art (pixels)", Range(0, 12)) = 3
        _VariacaoLineArt ("Variacao a Mao", Range(0, 1)) = 0.4
        _EscalaVariacao ("Tamanho da Variacao", Range(0.05, 10)) = 1
        _FecharQuinas ("Fechar Quinas (low poly)", Range(0, 1)) = 0.5
        _ContornoInterno ("Contorno Interno (bordas viradas)", Range(0, 1)) = 0.25
        _EmpurrarLineArt ("Esconder Emendas (pisos lado a lado, metros)", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _CorBase, _CorSombra, _CorTraco, _CorTracoLuz, _CorLineArt;
            float _LimiteSombra, _SuavidadeSombra, _ForcaSombraProjetada, _InfluenciaLuz;
            float _IntensidadeTraco, _QuantidadeTraco, _LinhasPorMetro, _EspessuraTraco, _Tremido, _EscalaTremido;
            float _Curvatura, _TamanhoCurva, _AnguloTraco, _Falhas, _TracoNoObjeto, _IntensidadeTracoLuz;
            float _EspessuraLineArt, _VariacaoLineArt, _EscalaVariacao, _FecharQuinas, _ContornoInterno, _EmpurrarLineArt;
        CBUFFER_END
        ENDHLSL

        // ------------------------------------------------------------------
        // 1) Cor: cartoon + traço de xilogravura + contorno interno
        // ------------------------------------------------------------------
        Pass
        {
            Name "XiloCartoon"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                float  fog        : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float XiloEyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w > 0.5)
                    return LinearDepthToEyeDepth(rawDepth);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float XiloDepthEdge(float2 pixel, float2 delta, float raw, float2 slope, float eye)
            {
                float2 uv = GetNormalizedScreenSpaceUV(pixel + delta);
                float neighbor = XiloEyeDepth(SampleSceneDepth(uv));

                // Extrapola a profundidade da propria face para ignorar planos inclinados.
                float predicted = XiloEyeDepth(saturate(raw + dot(slope, delta)));
                float limit = max(0.08, eye * 0.024);
                predicted = clamp(predicted, eye - limit, eye + limit);

                float threshold = max(0.018, eye * 0.0018) + max(0.0, _EmpurrarLineArt);
                return smoothstep(threshold, threshold * 2.4, neighbor - predicted);
            }

            float4 XiloEdgeRing(float2 pixel, float radius, float raw, float2 slope, float eye)
            {
                return float4(
                    XiloDepthEdge(pixel, float2(-radius, 0), raw, slope, eye),
                    XiloDepthEdge(pixel, float2( radius, 0), raw, slope, eye),
                    XiloDepthEdge(pixel, float2(0, -radius), raw, slope, eye),
                    XiloDepthEdge(pixel, float2(0,  radius), raw, slope, eye));
            }

            float XiloSilhouetteInk(Varyings i)
            {
                float raw = i.positionCS.z;
                float2 slope = float2(ddx(raw), ddy(raw));
                if (_EspessuraLineArt <= 0.001) return 0.0;

                float eye = XiloEyeDepth(raw);
                float2 pixel = i.positionCS.xy;
                float sceneEye = XiloEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(pixel)));
                if (abs(eye - sceneEye) > max(0.02, eye * 0.0018)) return 0.0;

                float variation = sin(dot(i.positionWS, float3(3.1, 5.3, 4.2)) * _EscalaVariacao);
                float width = min(1.65, _EspessuraLineArt *
                    (0.72 + variation * _VariacaoLineArt * 0.13));

                float4 nearEdge = XiloEdgeRing(pixel, 1.0, raw, slope, eye);
                float edge = max(max(nearEdge.x, nearEdge.y), max(nearEdge.z, nearEdge.w));
                float thin = max(min(nearEdge.x, nearEdge.y), min(nearEdge.z, nearEdge.w));
                float ink = edge * saturate(width);

                if (width > 1.0 && edge < 0.99)
                {
                    float4 farther = XiloEdgeRing(pixel, 2.0, raw, slope, eye);
                    float farEdge = max(max(farther.x, farther.y), max(farther.z, farther.w));
                    ink = max(ink, farEdge * (width - 1.0) * 0.75);
                }

                return ink * lerp(0.96, 0.40, thin);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 N = normalize(i.normalWS);
                float3 W = i.positionWS;

                // ---------- cartoon ----------
                Light luz = GetMainLight(TransformWorldToShadowCoord(W));
                float lum = saturate((dot(N, luz.direction) + 0.6) / 1.4);
                lum *= lerp(1.0, luz.shadowAttenuation, _ForcaSombraProjetada);
                float s = max(_SuavidadeSombra, 0.001);
                float t = smoothstep(_LimiteSombra - s, _LimiteSombra + s, lum);
                float3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _CorBase.rgb;
                float3 cor = lerp(base * _CorSombra.rgb, base, t);
                cor *= lerp(float3(1, 1, 1), saturate(luz.color), _InfluenciaLuz);

                // ---------- xilogravura ----------
                float3 p = lerp(W, i.positionOS, _TracoNoObjeto);
                float3 n = abs(N);
                float2 uv; float extra = 0.0;
                if (n.y >= n.x && n.y >= n.z) { uv = p.xz; extra = 0.785398; }
                else if (n.x >= n.z)          { uv = p.zy; }
                else                          { uv = p.xy; }
                float a = radians(_AnguloTraco) + extra;
                float sa = sin(a), ca = cos(a);
                float across = uv.y * ca + uv.x * sa;
                float along  = uv.x * ca - uv.y * sa;
                float3 q = p * _EscalaTremido;
                float ruido = sin(dot(q, float3(1.7, 2.3, 1.1)) * 1.9) * 0.6 + sin(dot(q, float3(-2.9, 1.3, 2.7)) * 2.7) * 0.4;
                float onda = sin(along * (6.2831 / max(_TamanhoCurva, 0.01))) * _Curvatura;
                float x = across * _LinhasPorMetro + ruido * _Tremido + onda;
                float idx = floor(x);
                float perfil = abs(frac(x) * 2.0 - 1.0);
                float aa = max(fwidth(x) * 2.0, 0.0001);
                float h = frac(sin(idx * 91.3458) * 47453.5453);
                float g = sin(along * _LinhasPorMetro * 0.35 + h * 6.2831 + ruido);
                float falha = step(1.0 - _Falhas * 2.0, g) * step(0.001, _Falhas);
                float escuro = saturate((1.0 - lum) * _QuantidadeTraco * 2.0);
                float largura = _EspessuraTraco * escuro * (0.85 + 0.3 * h);
                float sombra = smoothstep(1.0 - largura - aa, 1.0 - largura + aa, perfil) * (1.0 - falha) * step(0.001, largura);
                float perfil2 = abs(frac(x + 0.5) * 2.0 - 1.0);
                float largura2 = _EspessuraTraco * 0.7 * saturate((lum - 0.55) * 2.2);
                float brilho = smoothstep(1.0 - largura2 - aa, 1.0 - largura2 + aa, perfil2) * (1.0 - falha) * step(0.001, largura2);

                cor = lerp(cor, _CorTraco.rgb, saturate(sombra * _IntensidadeTraco));
                cor = lerp(cor, _CorTracoLuz.rgb, saturate(brilho * _IntensidadeTracoLuz));

                // ---------- contorno interno (bordas que viram de lado) ----------
                float3 V = GetWorldSpaceNormalizeViewDir(W);
                float ndv = saturate(dot(N, V));
                float lim = _ContornoInterno * 0.5;
                float rim = (1.0 - smoothstep(lim - 0.03, lim + 0.03, ndv)) * step(0.001, _ContornoInterno);
                cor = lerp(cor, _CorLineArt.rgb * 0.08, max(rim, XiloSilhouetteInk(i)));

                cor = MixFog(cor, i.fog);
                return half4(cor, 1);
            }
            ENDHLSL
        }


        // ------------------------------------------------------------------
        // 3) Sombra projetada
        // ------------------------------------------------------------------
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

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 L = normalize(_LightPosition - posWS);
                #else
                    float3 L = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, L));
                o.positionCS = ApplyShadowClamping(cs);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // 4) Profundidade (para SSAO, depth prepass etc.)
        // ------------------------------------------------------------------
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
