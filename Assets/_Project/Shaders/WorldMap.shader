Shader "Safe Haven/UI/World Map"
{
    Properties
    {
        [PerRendererData] _MainTex ("Map", 2D) = "white" {}
        _Explored ("Explored (R)", 2D) = "white" {}
        _Fog ("Unexplored", Color) = (0.05, 0.045, 0.04, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _Explored;
            fixed4 _Fog;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 map = tex2D(_MainTex, input.texcoord);
                float explored = smoothstep(0.25, 0.75, tex2D(_Explored, input.texcoord).r);
                return fixed4(lerp(_Fog.rgb, map.rgb, explored), 1.0);
            }
            ENDCG
        }
    }
}
