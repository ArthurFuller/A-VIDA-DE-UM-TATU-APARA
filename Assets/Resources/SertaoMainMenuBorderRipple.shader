Shader "UI/Sertao Main Menu Border Ripple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _SpriteUV ("Atlas rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _SizeClock ("Width, height, seconds", Vector) = (900,506,0,0)
        [HideInInspector] _ImageOrigin ("Image origin", Vector) = (-450,-253,0,0)
        [HideInInspector] _InnerLines ("Left, right, bottom, top", Vector) = (0.037,0.046,0.068,0.060)
        [HideInInspector] _LineHalfWidth ("Band half-width", Float) = 0.018
        [HideInInspector] _Wave ("Wave count, impact, idle, speed", Vector) = (5.5,5.2,1.35,1.45)
        [HideInInspector] _ImpactA ("First impact", Vector) = (0,0,0,0)
        [HideInInspector] _ImpactB ("Second impact", Vector) = (0,0,0,0)
        [HideInInspector] _ImpactC ("Third impact", Vector) = (0,0,0,0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
            "PreviewType"="Plane" "CanUseSpriteAtlas"="True"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            half4 _Color;
            half4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _SpriteUV;
            float4 _SizeClock;
            float4 _ImageOrigin;
            float4 _InnerLines;
            float _LineHalfWidth;
            float4 _Wave;
            float4 _ImpactA, _ImpactB, _ImpactC;

            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.localPosition = input.vertex.xy;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float ImpactWave(float2 uv, float4 impact, float aspect, float elapsed)
            {
                float age = elapsed - impact.z;
                if (impact.w <= 0.0 || age <= 0.0 || age > 1.65) return 0.0;

                float2 scale = float2(aspect, 1.0);
                float2 toLine = min(abs(impact.xy - _InnerLines.xz),
                    abs((1.0 - impact.xy) - _InnerLines.yw)) * scale;
                float nearestLine = min(toLine.x, toLine.y);
                float distance = length((uv - impact.xy) * scale);
                float localAge = age - max(0.0, distance - nearestLine) / 2.45;
                float envelope = smoothstep(0.0, 0.06, localAge) *
                    (1.0 - smoothstep(0.12, 0.58, localAge));
                return sin(localAge * 34.0) * envelope * impact.w;
            }

            float2 InnerLineOffset(float2 uv, float2 size, float elapsed)
            {
                float2 toLine = min(abs(uv - _InnerLines.xz), abs((1.0 - uv) - _InnerLines.yw));
                float2 halfWidth = _LineHalfWidth * float2(size.y / size.x, 1.0);
                float2 toCorner = min(uv, 1.0 - uv).yx;
                float2 band = (1.0 - smoothstep(halfWidth * 0.35, halfWidth, toLine)) *
                    smoothstep(float2(0.085, 0.050), float2(0.19, 0.135), toCorner);
                if (max(band.x, band.y) <= 0.0) return float2(0.0, 0.0);

                float aspect = size.x / size.y;
                float pulse = ImpactWave(uv, _ImpactA, aspect, elapsed) +
                    ImpactWave(uv, _ImpactB, aspect, elapsed) +
                    ImpactWave(uv, _ImpactC, aspect, elapsed);
                float idleTime = elapsed * _Wave.w;
                float2 side = step(0.5, uv);
                float2 phase = uv.yx * _Wave.x * 6.2831853;
                float2 idle = 0.73 * sin(phase - idleTime + side * 1.8) +
                    0.27 * sin(phase * 1.45 + idleTime * 0.58 + 1.1);
                idle *= _Wave.z * smoothstep(0.0, 0.68, elapsed);
                float limit = _Wave.y + _Wave.z;
                float2 motion = clamp(_Wave.y * pulse + idle, -limit, limit);
                return (1.0 - 2.0 * side) * band * motion;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 size = max(_SizeClock.xy, 1.0);
                float2 uv = saturate((input.localPosition - _ImageOrigin.xy) / size);
                // Converte o deslocamento no Canvas para a inclinação da UV de cada fatia do sprite.
                float2 dx = ddx(input.localPosition);
                float2 dy = ddy(input.localPosition);
                float area = dx.x * dy.y - dy.x * dx.y;
                float inverseArea = abs(area) > 0.00001 ? rcp(area) : 0.0;
                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float2 gradientX = (dy.y * uvDx - dx.y * uvDy) * inverseArea;
                float2 gradientY = (dx.x * uvDy - dy.x * uvDx) * inverseArea;
                float2 offset = InnerLineOffset(uv, size, _SizeClock.z);
                float2 sampleUv = clamp(input.uv + offset.x * gradientX + offset.y * gradientY,
                    _SpriteUV.xy, _SpriteUV.zw);
                half4 color = (tex2D(_MainTex, sampleUv) + _TextureSampleAdd) * input.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
