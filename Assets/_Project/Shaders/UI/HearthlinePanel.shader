// Safe Haven: a Hearthline window's background (UIRole > Window). The panel colour with depth: a little lighter at the top
// than the foot, a soft shadow inside the rim, and a fine grain so it reads as a surface, not a flat fill. Solid: the
// alpha is the image's own (the project blends in Linear, so a translucent dark panel would wash out). Works in masks
// and scroll views like UI/Default.
Shader "UI/Hearthline Panel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Lift ("Top Lift", Range(0, 0.5)) = 0.12
        _Edge ("Inner Shadow", Range(0, 1)) = 0.35
        _EdgeWidth ("Inner Shadow Width (px)", Range(1, 80)) = 28
        _Grain ("Grain", Range(0, 0.2)) = 0.035

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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Lift, _Edge, _EdgeWidth, _Grain;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                float2 uv = IN.texcoord;
                // Lighter at the top, as if lit from above.
                color.rgb *= 1.0 + _Lift * (uv.y - 0.5) * 2.0;
                // A soft shadow in from every edge, the same width in pixels whatever the panel's size.
                float2 pixels = 1.0 / max(fwidth(uv), 1e-5);
                float2 fromEdge = min(uv, 1.0 - uv) * pixels;
                float inside = saturate(min(fromEdge.x, fromEdge.y) / _EdgeWidth);
                color.rgb *= lerp(1.0 - _Edge, 1.0, inside * inside * (3.0 - 2.0 * inside));
                // Fine grain, fixed to the screen's pixels so it never swims as the panel fades in.
                float grain = hash(floor(IN.vertex.xy)) - 0.5;
                color.rgb *= 1.0 + grain * _Grain * 2.0;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
