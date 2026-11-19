Shader "UI/Sertao Card Reward Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Ink Opacity", Range(0, 1)) = 0.94
        _Speed ("Pulses Per Second", Range(0, 1.2)) = 0.53
        _WaveAmount ("Outward Travel", Range(0, 1.25)) = 1
        _Ink ("Ink Color", Color) = (0.63, 0.48, 0.24, 1)
        _Gold ("Golden Stroke", Color) = (0.96, 0.68, 0.23, 1)
        _Light ("Highlight", Color) = (1, 0.968, 0.75, 1)
        _Glow ("Glow", Range(0, 1)) = 0.95
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            half4 _Color, _Ink, _Gold, _Light;
            float _Intensity, _Speed, _WaveAmount, _Glow;
            float4 _ClipRect;

            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                output.localPosition = input.vertex.xy;
                return output;
            }

            // Distance along the rounded rectangle, starting at the top centre.
            float PerimeterPosition(float2 fromCenter, float2 edge, float2 straightHalf, float radius)
            {
                float quarter = straightHalf.x + straightHalf.y + radius * 1.5707963;
                float arc = straightHalf.x + radius * atan2(max(edge.x, 0.00001), max(edge.y, 0.00001));
                if (edge.y <= 0.0) arc = quarter - abs(fromCenter.y);
                if (edge.x <= 0.0) arc = abs(fromCenter.x);
                if (fromCenter.y < 0.0) arc = 2.0 * quarter - arc;
                if (fromCenter.x < 0.0) arc = 4.0 * quarter - arc;
                return arc / (4.0 * quarter);
            }

            float Band(float distance, float core, float fade)
            {
                return 1.0 - smoothstep(core, fade, abs(distance));
            }

            float3 PrintedWave(float across, float edgeDistance, float age, float seed)
            {
                float progress = saturate(age / 0.74);
                float width = lerp(0.18, 0.32, progress);
                float taper = across / width;
                float ends = 1.0 - smoothstep(0.57, 0.99, abs(taper));
                if (ends <= 0.0 || progress >= 1.0) return float3(0, 0, 0);

                // Each broken stroke expands away from the card at its own fixed place.
                float roughEdge = sin(across * 39.0 + seed * 13.0) * 0.0011 +
                    sin(across * 74.0 - seed * 21.0) * 0.00065;
                float arc = saturate(1.0 - taper * taper);
                float lift = lerp(0.14, 0.55, smoothstep(0.18, 0.77, progress));
                float travel = _WaveAmount * progress * 0.047 *
                    lerp(lift, 1.0, arc) * lerp(0.82, 1.13, seed);
                float widthInk = lerp(0.0060, 0.0029, progress) * lerp(0.49, 1.0, ends);
                float cut = 0.62 + 0.24 * sin(across * 106.0 + seed * 18.0) +
                    0.18 * sin(across * 173.0 - seed * 25.0);
                float grain = lerp(0.43, 1.0, smoothstep(0.21, 0.63, cut));
                float life = smoothstep(0.0, 0.10, progress) * (1.0 - smoothstep(0.49, 1.0, progress));
                float fromStroke = edgeDistance - travel - roughEdge;
                float stroke = Band(fromStroke, widthInk * 0.32, widthInk * 1.12) *
                    ends * grain * life;
                // A short cut connects the youngest part of the pulse to the edge.
                float notch = Band(across - edgeDistance * (seed - 0.5) * 3.3, 0.007, 0.029) *
                    (1.0 - smoothstep(0.001, max(0.002, travel), edgeDistance));
                float ink = max(stroke, 0.48 * notch * life * (1.0 - smoothstep(0.16, 0.46, progress)));

                // The gleam crosses only this short stroke while the pulse travels outwards.
                float gleamCenter = lerp(-0.42, 0.47, progress) * (seed < 0.5 ? -1.0 : 1.0);
                float gleam = 1.0 - smoothstep(0.15, 0.66, abs(taper - gleamCenter));
                float light = Band(fromStroke + widthInk * 0.12, 0.0017, 0.0041) *
                    gleam * ends * grain * life;
                float fleckCell = across * 9.0 + seed;
                float fleckDepth = lerp(0.0085, 0.016, frac(floor(fleckCell) * 0.618034 + seed));
                float fleck = Band(frac(fleckCell) - 0.5, 0.025, 0.12) *
                    Band(fromStroke - fleckDepth, 0.0008, 0.0037) * ends * gleam * life *
                    smoothstep(0.12, 0.32, progress);

                float halo = Band(fromStroke, 0.002, 0.024) * ends * life * (0.64 + 0.36 * gleam);
                float atEdge = Band(edgeDistance, 0.002, 0.012) * ends * life *
                    (1.0 - smoothstep(0.13, 0.51, progress));
                return float3(ink, max(light, 0.81 * fleck), max(halo, atEdge));
            }

            void Over(inout half4 layer, half4 color, half opacity)
            {
                half alpha = color.a * opacity;
                layer = half4(color.rgb * alpha, alpha) + layer * (1.0 - alpha);
            }

            half4 frag(Varyings input) : SV_Target
            {
                const float2 aspect = float2(0.77, 1.0);
                const float cornerRadius = 0.038;
                float2 fromCenter = (input.uv - 0.5) * aspect;
                // The graphic extends to 1.32 times the visible card bounds.
                float2 straightHalf = aspect * (0.5 / 1.32) - cornerRadius;
                float2 edge = abs(fromCenter) - straightHalf;
                float edgeDistance = length(max(edge, 0.0)) + min(max(edge.x, edge.y), 0.0) - cornerRadius;
                if (edgeDistance < -0.003 || edgeDistance > 0.086) return 0;

                float perimeter = PerimeterPosition(fromCenter, edge, straightHalf, cornerRadius);
                float blocks = frac(perimeter) * 11.0;
                float seed = frac((floor(blocks) + 1.0) * 0.618034);
                float across = frac(blocks) - 0.5 + (seed - 0.5) * 0.16;
                float clock = _Time.y * _Speed + seed;
                float3 wave = PrintedWave(across, edgeDistance, frac(clock), seed);
                float3 echo = PrintedWave(across * 1.08 - 0.04, edgeDistance, frac(clock + 0.28), seed);
                float3 marks = max(wave, echo * float3(0.59, 0.64, 0.48));

                half4 result = 0;
                Over(result, _Gold, marks.z * _Glow * 0.26);
                Over(result, _Light, marks.z * _Glow * 0.56);
                Over(result, lerp(_Ink, _Gold, 0.40), marks.x);
                Over(result, _Light, marks.y * _Glow);
                half opacity = smoothstep(-0.003, 0.001, edgeDistance) * _Intensity * input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                opacity *= UnityGet2DClipping(input.localPosition, _ClipRect);
                #endif
                result.rgb *= input.color.rgb;
                return result * opacity;
            }
            ENDCG
        }
    }
}
