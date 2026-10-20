// The raid map painter's zone overlay: the painted zones drawn as a see-through colour layer hugging the terrain.
// Editor only (drawn with Graphics.DrawMeshNow from the Scene view), so it lives in an Editor folder.
Shader "Hidden/Safe Haven/Raid Map Overlay"
{
    Properties
    {
        _MainTex ("Zones", 2D) = "black" {}
        _Opacity ("Opacity", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Opacity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.a *= _Opacity;
                return c;
            }
            ENDCG
        }
    }
}
