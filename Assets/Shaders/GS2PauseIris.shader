Shader "UI/GS2PauseIris"
{
    Properties
    {
        _MainTex ("UI texture", 2D) = "white" {}
        _Radius ("Clear Circle Radius", Float) = 1.1
        _Feather ("Edge softness", Range(0.001, 0.15)) = 0.035
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float _Radius;
            float _Feather;
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 offset = (i.uv - 0.5) * float2(aspect, 1.0);
                float maxDistance = length(float2(aspect, 1.0) * 0.5);
                float normalizedDistance = length(offset) / maxDistance;
                float darkness = smoothstep(_Radius - _Feather, _Radius, normalizedDistance);
                return fixed4(0, 0, 0, darkness * i.color.a);
            }
            ENDCG
        }
    }
}
