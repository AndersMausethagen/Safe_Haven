// A round window onto the painted map (MinimapMap): scrolled and turned with the player, ground not yet explored lost in fog,
// the ward's edge drawn on top. Everything happens here, so moving the map costs a few uniforms and no render of the world.
Shader "Safe Haven/UI/Minimap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Map", 2D) = "white" {}
        _Explored ("Explored (R)", 2D) = "white" {}
        _Fog ("Unexplored", Color) = (0.05, 0.045, 0.04, 1)
        _Rim ("Rim", Color) = (0.85, 0.65, 0.35, 1)
        _Ward ("Ward Edge", Color) = (0.85, 0.65, 0.35, 1)
        _Centre ("Centre (map UV)", Vector) = (0.5, 0.5, 0, 0)
        _Span ("Radius (map UV)", Vector) = (0.1, 0.1, 0, 0)
        _Area ("Map Size (metres)", Vector) = (400, 400, 0, 0)
        _Turn ("Turn (radians)", Float) = 0
        _RingCentre ("Ward Centre (map UV)", Vector) = (0.5, 0.5, 0, 0)
        _RingRadius ("Ward Radius (metres)", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _Explored;
            fixed4 _Fog;
            fixed4 _Rim;
            fixed4 _Ward;
            float4 _Centre;
            float4 _Span;
            float4 _Area;
            float4 _RingCentre;
            float _Turn;
            float _RingRadius;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 disc = (i.texcoord - 0.5) * 2.0;
                float radius = length(disc);
                float edge = fwidth(radius);
                float shape = 1.0 - smoothstep(1.0 - edge * 1.5, 1.0, radius);

                // Up on the screen is where the camera looks: the same turn as MinimapHud.Project, the other way round.
                float s = sin(_Turn);
                float c = cos(_Turn);
                float2 world = float2(disc.x * c + disc.y * s, disc.y * c - disc.x * s);
                float2 uv = _Centre.xy + world * _Span.xy;
                float inside = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);

                fixed4 map = tex2D(_MainTex, uv);
                float seen = smoothstep(0.25, 0.75, tex2D(_Explored, uv).r) * inside;
                fixed3 colour = lerp(_Fog.rgb, map.rgb, seen);

                if (_RingRadius > 0.0)
                {
                    float away = abs(length((uv - _RingCentre.xy) * _Area.xy) - _RingRadius);
                    colour = lerp(colour, _Ward.rgb, (1.0 - smoothstep(0.0, fwidth(away) * 1.2, away)) * 0.7);
                }

                float rim = smoothstep(1.0 - edge * 2.5, 1.0 - edge * 1.5, radius);
                colour = lerp(colour, _Rim.rgb, rim);
                return fixed4(colour, shape * i.color.a);
            }
            ENDCG
        }
    }
}
