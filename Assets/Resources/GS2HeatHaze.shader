Shader "GS2/Heat Haze"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [Enum(Classic, 0, RisingAir, 1)] _HeatPattern ("Heat Pattern", Float) = 1
        _Distortion ("Distortion", Range(0,0.015)) = 0.005
        _Height ("Vertical Reach", Range(0.2,0.95)) = 0.71
        _Speed ("Wave Speed", Range(0,5)) = 1.4
        _Opacity ("Opacity", Range(0,1)) = 0.55
        _Warmth ("Warmth", Range(0,0.3)) = 0.09
        _WarmColor ("Warm Color", Color) = (1,0.77,0.49,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _HeatPattern;
                float _Distortion;
                float _Height;
                float _Speed;
                float _Opacity;
                float _Warmth;
                half4 _WarmColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float2 HeatOffset(float2 uv)
            {
                float time = _Time.y * _Speed;
                if (_HeatPattern < 0.5)
                {
                    float wave = sin(uv.y * 130.0 - time * 3.0 +
                        sin(uv.x * 23.0 + time) * 2.0);
                    return float2(wave, wave * 0.25);
                }

                float slow = sin(uv.x * 19.0 + uv.y * 11.0 - time * 0.65);
                float pockets = lerp(0.50, 1.0, smoothstep(-0.8, 0.85, slow));
                float coarse = sin(uv.y * 58.0 - time * 2.1 + slow * 2.4);
                float detail = sin(uv.y * 101.0 + uv.x * 31.0 - time * 3.0);
                float lift = sin(uv.y * 39.0 - uv.x * 16.0 - time * 1.65 + slow);
                return float2((coarse * 0.72 + detail * 0.28) * 1.3, lift * 0.24) * pockets;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float mask = 1.0 - smoothstep(0.12, _Height, input.uv.y);
                if (mask <= 0.0) return half4(0, 0, 0, 0);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 offset = HeatOffset(input.uv) * _Distortion * mask;
                half3 sceneColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture,
                    sampler_CameraOpaqueTexture, saturate(screenUV + offset)).rgb;
                sceneColor = lerp(sceneColor, sceneColor * _WarmColor.rgb, _Warmth);
                return half4(sceneColor, _Opacity * mask * input.color.a);
            }
            ENDHLSL
        }
    }
}
