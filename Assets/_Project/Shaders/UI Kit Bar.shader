// Hearthline 3's bars (KitBarFill): a rounded fill with a gradient from deep to bright along it, a lighter top, slow
// diagonal stripes drifting along, a shine sweeping across now and then and a glow at its leading edge; a pulse when it
// grows. Drawn from the fill's own rect (_Size, set by KitBarFill), so its ends stay round however long it is. A UI
// shader: masks and RectMask2D clip it as they do any image.
Shader "Safe Haven/UI Kit Bar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Size ("Rect Size (px)", Vector) = (200, 10, 0, 0)
        _Glow ("Leading Edge Glow", Range(0, 2)) = 1
        _Shine ("Shine", Range(0, 1)) = 0.35
        _ShineSpeed ("Shine Speed", Float) = 0.28
        _Phase ("Shine Phase", Range(0, 1)) = 0
        _Stripes ("Stripes", Range(0, 0.3)) = 0.07
        _Pulse ("Pulse", Range(0, 1)) = 0

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
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

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
            #pragma target 2.0
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

            fixed4 _Color;
            float4 _ClipRect;
            float4 _Size;
            float _Glow, _Shine, _ShineSpeed, _Phase, _Stripes, _Pulse;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 size = max(_Size.xy, float2(1, 1));
                float2 px = uv * size;
                float r = size.y * 0.5;
                // The capsule: round at both ends, a pixel of soft edge.
                float2 centre = float2(clamp(px.x, r, max(r, size.x - r)), r);
                float d = length(px - centre) - r;
                float shape = 1 - saturate(d + 0.75);

                float3 col = i.color.rgb;
                float3 deep = col * 0.42;
                float3 bright = lerp(col, float3(1, 1, 1), 0.3);
                float3 g = lerp(deep, col, smoothstep(0.0, 0.65, uv.x));
                g = lerp(g, bright, smoothstep(0.72, 1.0, uv.x));
                // Lighter along the top, darker under: it reads as rounded.
                g *= lerp(0.78, 1.18, smoothstep(0.15, 0.95, uv.y));
                // Slow stripes drifting along it.
                float stripe = frac((px.x - px.y * 1.2) / max(size.y * 1.6, 6) - _Time.y * 0.35);
                g *= 1 - _Stripes + _Stripes * 2 * smoothstep(0.35, 0.65, stripe);
                // A shine that sweeps across now and then.
                float sweep = frac(_Time.y * _ShineSpeed + _Phase) * 2.2 - 0.6;
                float band = exp(-pow((uv.x - sweep) * 7, 2)) * _Shine;
                g += band * (0.6 + 0.4 * uv.y);
                // The leading edge glows.
                float edge = exp(-(size.x - px.x) / max(r * 1.8, 1)) * _Glow;
                g += col * edge * 0.55 + edge * 0.2;
                g += col * _Pulse * 0.7;

                fixed4 o = fixed4(g, i.color.a * shape);
                #ifdef UNITY_UI_CLIP_RECT
                o.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(o.a - 0.001);
                #endif
                return o;
            }
            ENDCG
        }
    }
}
