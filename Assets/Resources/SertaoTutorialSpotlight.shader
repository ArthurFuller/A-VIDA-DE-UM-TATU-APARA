Shader "Sertao/UI/Tutorial Spotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _ShadeAlpha ("Shade Opacity", Range(0, 1)) = 0.64
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Cutouts[4];
            float _CutoutCount;
            float _ShadeAlpha;

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float clear = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float4 bounds = _Cutouts[i];
                    float2 inside = step(bounds.xy, input.uv) * step(input.uv, bounds.zw);
                    clear = max(clear, inside.x * inside.y * step(i + 1, _CutoutCount));
                }
                return fixed4(0, 0, 0, _ShadeAlpha * (1 - clear));
            }
            ENDCG
        }
    }
}
