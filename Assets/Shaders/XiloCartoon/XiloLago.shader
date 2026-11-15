// Sertao / Xilo Lago  — água de açude no estilo xilogravura (URP 17 / Unity 6)
// Uso: crie um material com este shader e aplique na malha plana da água (o espaço entre as pedras).
// Nada precisa ser configurado no projeto.
//
// Espuma e margem de lama: pinte a malha no Blender (Vertex Paint) com BRANCO na borda (perto das pedras)
// e PRETO no meio, e ligue "Usar Cor do Vertice". Sem pintura, deixe desligado.
Shader "Sertao/Xilo Lago"
{
    Properties
    {
        [Header(Cor da agua de acude)]
        _CorFunda ("Cor Funda (meio)", Color) = (0.26, 0.28, 0.19, 1)
        _CorRasa ("Cor Rasa (perto da borda)", Color) = (0.45, 0.42, 0.28, 1)
        _CorMargem ("Cor da Lama na Margem", Color) = (0.44, 0.34, 0.22, 1)
        _LarguraMargem ("Largura da Lama", Range(0, 0.6)) = 0.12
        _VariacaoCor ("Manchas na Agua", Range(0, 1)) = 0.35
        _EscalaManchas ("Tamanho das Manchas", Range(0.02, 2)) = 0.25
        [Toggle] _UsarCorVertice ("Usar Cor do Vertice (borda pintada)", Float) = 0

        [Header(Luz)]
        _CorSombraAgua ("Cor da Sombra (multiplica)", Color) = (0.55, 0.55, 0.62, 1)
        _ForcaSombra ("Forca da Sombra das Arvores", Range(0, 1)) = 0.8
        _InfluenciaLuz ("Cor da Luz da Cena", Range(0, 1)) = 0.2
        _CorBrilho ("Cor do Brilho do Sol", Color) = (1, 0.96, 0.84, 1)
        _BrilhoSol ("Brilho do Sol na Agua", Range(0, 1)) = 0.6
        _TamanhoBrilho ("Tamanho do Brilho", Range(0.05, 1)) = 0.35

        [Header(Ondas de goiva  xilogravura)]
        _CorTraco ("Cor do Traco", Color) = (0.12, 0.14, 0.10, 1)
        _IntensidadeTraco ("Intensidade do Traco", Range(0, 1)) = 0.9
        _LinhasPorMetro ("Linhas por Metro", Range(0.5, 40)) = 2.5
        _EspessuraTraco ("Espessura do Traco", Range(0.02, 1)) = 0.42
        _ComprimentoTraco ("Comprimento do Traco", Range(1, 30)) = 7
        _Afinamento ("Ponta Fina", Range(0.2, 3)) = 1
        _Tremido ("Tremido (ondas tortas)", Range(0, 2)) = 0.6
        _EscalaTremido ("Tamanho do Tremido", Range(0.02, 5)) = 0.5
        _AnguloOndas ("Angulo das Ondas", Range(0, 180)) = 0
        _VelocidadeOndas ("Velocidade das Ondas", Range(0, 2)) = 0.12
        _CorReflexo ("Cor dos Reflexos (cortes claros)", Color) = (0.93, 0.90, 0.76, 1)
        _IntensidadeReflexo ("Intensidade dos Reflexos", Range(0, 1)) = 0.45

        [Header(Espuma na borda)]
        _CorEspuma ("Cor da Espuma", Color) = (0.95, 0.92, 0.82, 1)
        _LarguraEspuma ("Largura da Espuma", Range(0, 0.6)) = 0.2
        _VelocidadeEspuma ("Velocidade da Espuma", Range(0, 3)) = 0.6

        [Header(Ondinhas circulares  peixe ou inseto)]
        _Ondinhas ("Quantidade de Ondinhas", Range(0, 1)) = 0.15
        _TamanhoOndinhas ("Tamanho das Ondinhas", Range(0.2, 10)) = 2
        _VelocidadeOndinhas ("Velocidade das Ondinhas", Range(0, 2)) = 0.35

        [Header(Movimento da superficie)]
        _AlturaOnda ("Altura da Onda", Range(0, 0.3)) = 0.015
        _TamanhoOnda ("Tamanho da Onda", Range(0.1, 20)) = 2
        _VelocidadeOnda ("Velocidade da Onda", Range(0, 3)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _CorFunda, _CorRasa, _CorMargem, _CorSombraAgua, _CorBrilho, _CorTraco, _CorReflexo, _CorEspuma;
            float _LarguraMargem, _VariacaoCor, _EscalaManchas, _UsarCorVertice;
            float _ForcaSombra, _InfluenciaLuz, _BrilhoSol, _TamanhoBrilho;
            float _IntensidadeTraco, _LinhasPorMetro, _EspessuraTraco, _ComprimentoTraco, _Afinamento, _Tremido, _EscalaTremido;
            float _AnguloOndas, _VelocidadeOndas, _IntensidadeReflexo;
            float _LarguraEspuma, _VelocidadeEspuma;
            float _Ondinhas, _TamanhoOndinhas, _VelocidadeOndinhas;
            float _AlturaOnda, _TamanhoOnda, _VelocidadeOnda;
        CBUFFER_END

        float Hash11(float n) { return frac(sin(n * 127.1) * 43758.5453); }
        float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
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

        float3 Ondular(float3 posWS)
        {
            float t = _Time.y * _VelocidadeOnda;
            float k = 6.2831 / _TamanhoOnda;
            posWS.y += (sin(posWS.x * k + t) * 0.6 + sin(posWS.z * k * 1.3 - t * 0.8) * 0.4) * _AlturaOnda;
            return posWS;
        }
        ENDHLSL

        Pass
        {
            Name "XiloLago"
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  borda      : TEXCOORD2;
                float  fog        : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = Ondular(TransformObjectToWorld(v.positionOS.xyz));
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.borda = _UsarCorVertice > 0.5 ? v.color.r : 0.0;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float2 Girar(float2 uv, float graus)
            {
                float a = radians(graus), s = sin(a), c = cos(a);
                return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
            }

            // traços de goiva: lascas separadas, grossas no meio e finas nas pontas
            float Goivas(float2 uv, float largura, float semente)
            {
                float dens = _LinhasPorMetro;
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
                return (1.0 - smoothstep(larg - aa, larg + aa, d)) * dentro * step(0.001, larg);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 W = i.positionWS;
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(W);
                float tempo = _Time.y;
                float m = saturate(i.borda);                       // 1 = borda, 0 = meio

                // ---------- cor base: funda no meio, rasa na borda, manchas de barro ----------
                float manchas = Fbm(W.xz * _EscalaManchas + float2(tempo * 0.01, 0));
                float raso = saturate(m * 1.1 + (manchas - 0.5) * _VariacaoCor * 1.6);
                float3 cor = lerp(_CorFunda.rgb, _CorRasa.rgb, raso);
                cor = lerp(cor, _CorMargem.rgb, smoothstep(1.0 - _LarguraMargem, 1.0, m) * step(0.001, _LarguraMargem));

                // ---------- luz: sombra das árvores e cor do sol ----------
                Light luz = GetMainLight(TransformWorldToShadowCoord(W));
                float sombra = (1.0 - luz.shadowAttenuation) * _ForcaSombra;
                cor *= lerp(float3(1, 1, 1), _CorSombraAgua.rgb, sombra);
                cor *= lerp(float3(1, 1, 1), saturate(luz.color), _InfluenciaLuz);

                // ---------- ondas de goiva (andando devagar) ----------
                float2 uv = Girar(W.xz, _AnguloOndas) + float2(tempo * _VelocidadeOndas, 0);
                float tracos = Goivas(uv, _EspessuraTraco * (0.8 + 0.6 * sombra), 1.0);
                cor = lerp(cor, _CorTraco.rgb, tracos * _IntensidadeTraco);

                // reflexos claros (cortes brancos), mais fortes olhando de lado e onde bate o sol
                float fresnel = pow(1.0 - saturate(dot(N, V)), 3.0);
                float3 R = reflect(-luz.direction, N);
                float sol = pow(saturate(dot(R, V)), lerp(400.0, 12.0, _TamanhoBrilho));
                float2 uv2 = Girar(W.xz, _AnguloOndas) * 1.0 + float2(-tempo * _VelocidadeOndas * 0.7, 0.37);
                float cortes = Goivas(uv2 * 0.8, _EspessuraTraco * 0.35 * saturate(0.25 + fresnel * 0.5 + sol * 3.0), 7.0);
                cor = lerp(cor, _CorReflexo.rgb, cortes * _IntensidadeReflexo * (1.0 - sombra * 0.8));
                cor = lerp(cor, _CorBrilho.rgb, saturate(sol * 2.0) * _BrilhoSol * (1.0 - sombra) * (0.4 + 0.6 * cortes));

                // ---------- ondinhas circulares ----------
                float2 cel = W.xz / _TamanhoOndinhas;
                float2 ic = floor(cel);
                float h = Hash21(ic + 3.1);
                float2 centro = float2(Hash21(ic + 7.7), Hash21(ic + 1.3)) * 0.3 + 0.35;
                float ciclo = frac(tempo * _VelocidadeOndinhas * (0.6 + 0.8 * h) + h * 9.0);
                float raio = ciclo * 0.3;
                float dist = length(frac(cel) - centro);
                float aaR = length(fwidth(cel)) * 0.75 + 1e-4;
                float anel = (1.0 - smoothstep(0.004, 0.004 + aaR, abs(dist - raio)))
                           + (1.0 - smoothstep(0.003, 0.003 + aaR, abs(dist - raio * 0.6))) * 0.6;
                anel *= (1.0 - ciclo) * step(0.08, ciclo) * step(h, _Ondinhas) * (1.0 - m);
                cor = lerp(cor, _CorReflexo.rgb, saturate(anel) * 0.7);

                // ---------- espuma batendo nas pedras (precisa da borda pintada) ----------
                float ruidoE = Fbm(W.xz * 2.5 + float2(0, tempo * 0.2));
                float linhaE = 1.0 - _LarguraEspuma + (sin(tempo * _VelocidadeEspuma + ruidoE * 6.0) * 0.5 + 0.5) * _LarguraEspuma * 0.5;
                float espuma = smoothstep(linhaE - 0.03, linhaE + 0.03, m) * step(0.001, _LarguraEspuma);
                float buracos = step(0.42, Fbm(W.xz * 6.0 + tempo * 0.15));
                espuma *= buracos;
                float contorno = espuma * (1.0 - smoothstep(linhaE + 0.03, linhaE + 0.08, m));
                cor = lerp(cor, _CorEspuma.rgb, espuma);
                cor = lerp(cor, _CorTraco.rgb, contorno * 0.6);

                cor = MixFog(cor, i.fog);
                return half4(cor, 1);
            }
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
            Varyings vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformWorldToHClip(Ondular(TransformObjectToWorld(v.positionOS.xyz))); return o; }
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
                o.positionCS = TransformWorldToHClip(Ondular(TransformObjectToWorld(v.positionOS.xyz)));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
