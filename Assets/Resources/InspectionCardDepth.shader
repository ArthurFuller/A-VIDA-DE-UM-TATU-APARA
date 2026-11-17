Shader "UI/Sertao Inspection Card"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _Cursor ("Cursor and angle", Vector) = (0,0,0,0)
        [HideInInspector] _CardSize ("Card dimensions", Vector) = (100,100,0,0)
        [HideInInspector] _SpriteUV ("Atlas rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _VisibleUV ("Trimmed sprite rectangle", Vector) = (0,0,1,1)
        _ArtDepth ("Profundidade da ilustração", Range(0,0.05)) = 0.033

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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float3 projectiveUv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            half4 _TextureSampleAdd;
            half4 _Color;
            float4 _ClipRect;
            float4 _Cursor;
            float4 _CardSize;
            float4 _SpriteUV;
            float4 _VisibleUV;
            float _ArtDepth;

            Varyings vert(AppData input)
            {
                Varyings output;
                float2 atlasUv = (input.uv - _SpriteUV.xy) / max(_SpriteUV.zw - _SpriteUV.xy, 0.0001);
                float2 uv = lerp(_VisibleUV.xy, _VisibleUV.zw, atlasUv);
                float height = max(_CardSize.y, 1.0);
                float aspect = _CardSize.x / height;
                float2 p = (uv - 0.5) * float2(aspect, 1.0);

                float pitch = -_Cursor.y * _Cursor.z;
                float yaw = _Cursor.x * _Cursor.z;
                float3 tilted = float3(p.x * cos(yaw), p.y, -p.x * sin(yaw));
                tilted.yz = float2(tilted.y * cos(pitch) - tilted.z * sin(pitch),
                                  tilted.y * sin(pitch) + tilted.z * cos(pitch));
                float perspective = 3.2 / (3.2 - tilted.z);
                input.vertex.xy += (tilted.xy * perspective - p) * height;

                output.position = UnityObjectToClipPos(input.vertex);
                output.localPosition = input.vertex.xy;
                output.projectiveUv = float3(uv * perspective, perspective);
                output.color = input.color * _Color;
                return output;
            }

            half4 CardSample(float2 uv)
            {
                float2 normalized = saturate((uv - _VisibleUV.xy) /
                    max(_VisibleUV.zw - _VisibleUV.xy, 0.0001));
                return tex2D(_MainTex, lerp(_SpriteUV.xy, _SpriteUV.zw, normalized))
                    + _TextureSampleAdd;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.projectiveUv.xy / input.projectiveUv.z;
                // All card families share the same printed frame and title band.
                float2 toEdge = min(uv - float2(0.174, 0.079), float2(0.826, 0.785) - uv);
                float artWindow = smoothstep(0.0, 0.086, min(toEdge.x, toEdge.y));
                // A broad depth curve keeps the carved outlines continuous in motion.
                float2 fromCenter = (uv - float2(0.50, 0.44)) / float2(0.34, 0.39);
                float relief = 1.0 - smoothstep(0.18, 1.0, length(fromCenter));
                float2 artUv = uv + _Cursor.xy * _ArtDepth * artWindow * lerp(0.48, 1.12, relief);

                half4 color = CardSample(artUv) * input.color;
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
