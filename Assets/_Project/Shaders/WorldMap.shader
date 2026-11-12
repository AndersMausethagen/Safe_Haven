Shader "Safe Haven/UI/World Map"
{
    // The M map as a cartographer's sheet: tan parchment with the land sketched in charcoal (the regions' borders, the big
    // shapes of the ground), and where the player has been, the painted map's colours show through like inks laid on the
    // paper, with a finer charcoal line over them. The explored edge is ragged, with a darker tide line where the colour
    // stops; the sheet itself has a deckled edge and darkens with age towards it. Paper and grain: Tools/UI/map_parchment.py.
    Properties
    {
        [PerRendererData] _MainTex ("Painted Map", 2D) = "white" {}
        _Explored ("Explored (R)", 2D) = "white" {}
        _Paper ("Parchment", 2D) = "white" {}
        _Grain ("Grain (R tooth, G smudge, B slow, A fibres)", 2D) = "gray" {}
        _Borders ("Region Borders (A)", 2D) = "black" {}
        _BordersRect ("Borders UV (offset xy, scale zw)", Vector) = (0, 0, 1, 1)
        _Aspect ("Map Width / Height", Float) = 1
        _PaperTiles ("Paper Tiles (per map height)", Float) = 1.3
        _GrainTiles ("Grain Tiles (per map height)", Float) = 5
        _Ink ("Charcoal", Color) = (0.16, 0.13, 0.105, 0.92)
        _Reveal ("Colour Where Explored", Range(0, 1)) = 0.9
        _Wash ("Watercolour (lift and saturation)", Range(0, 1)) = 0.75
        _SketchUnexplored ("Sketch Where Unexplored", Range(0, 1)) = 0.42
        _SketchExplored ("Sketch Where Explored", Range(0, 1)) = 0.5
        _BorderStrength ("Region Borders", Range(0, 1)) = 0.85
        _Smudge ("Charcoal Shading Where Unexplored", Range(0, 1)) = 0.22
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _Explored;
            sampler2D _Paper;
            sampler2D _Grain;
            sampler2D _Borders;
            float4 _BordersRect;
            float _Aspect;
            float _PaperTiles;
            float _GrainTiles;
            fixed4 _Ink;
            float _Reveal;
            float _Wash;
            float _SketchUnexplored;
            float _SketchExplored;
            float _BorderStrength;
            float _Smudge;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                return output;
            }

            float Luma(float2 uv)
            {
                return dot(tex2D(_MainTex, uv).rgb, float3(0.3, 0.59, 0.11));
            }

            // How sharply the painted ground changes here (a Sobel filter), looked at `step` texels apart: one texel finds
            // paths, shores and tree lines; a few find only the big shapes, as a sketch from hearsay would.
            float Edges(float2 uv, float step)
            {
                float2 t = _MainTex_TexelSize.xy * step;
                float tl = Luma(uv + float2(-t.x, t.y)), tc = Luma(uv + float2(0, t.y)), tr = Luma(uv + t);
                float ml = Luma(uv - float2(t.x, 0)), mr = Luma(uv + float2(t.x, 0));
                float bl = Luma(uv - t), bc = Luma(uv - float2(0, t.y)), br = Luma(uv + float2(t.x, -t.y));
                float gx = (tr + 2 * mr + br) - (tl + 2 * ml + bl);
                float gy = (tl + 2 * tc + tr) - (bl + 2 * bc + br);
                return sqrt(gx * gx + gy * gy);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 sheet = uv * float2(_Aspect, 1);
                float4 grain = tex2D(_Grain, sheet * _GrainTiles);
                float4 slow = tex2D(_Grain, sheet * _GrainTiles * 0.21 + 0.37);
                fixed3 paper = tex2D(_Paper, sheet * _PaperTiles).rgb;

                // The sheet: a deckled edge, and age gathering towards it.
                float2 toEdge = min(uv, 1 - uv) * float2(_Aspect, 1);
                float edge = min(toEdge.x, toEdge.y) + (slow.b - 0.5) * 0.018 + (grain.a - 0.5) * 0.004;
                float alpha = smoothstep(0.0, 0.004, edge);
                float age = smoothstep(0.14, 0.0, edge);
                paper = lerp(paper, paper * fixed3(0.70, 0.56, 0.40), age * age);

                // A charcoal hand wavers: every line is drawn a little off true.
                float2 waver = (float2(slow.g, slow.b) - 0.5) * _MainTex_TexelSize.xy * 3.0;

                // Where the player has been, with a ragged edge.
                float seen = tex2D(_Explored, uv).r;
                float reach = seen + (grain.g - 0.5) * 0.14 + (slow.b - 0.5) * 0.34;
                float explored = smoothstep(0.44, 0.56, reach);
                float tideLine = saturate(1 - abs(reach - 0.5) * 10) * step(0.02, seen);

                // The colours as watercolour washes on the paper: the ground's dark painted colours lifted and made richer,
                // then laid on the paper so its tone and grain carry into them.
                fixed3 painted = tex2D(_MainTex, uv).rgb;
                fixed3 lifted = pow(saturate(painted), lerp(1.0, 0.55, _Wash));
                float grey = dot(lifted, float3(0.3, 0.59, 0.11));
                lifted = saturate(lerp(grey.xxx, lifted, 1.0 + 0.8 * _Wash));
                fixed3 inked = lerp(lerp(lifted, lifted * (paper / 0.74), 0.6), paper, 0.1);
                fixed3 colour = lerp(paper, inked, explored * _Reveal);
                colour = lerp(colour, colour * fixed3(0.74, 0.64, 0.52), tideLine * 0.55);

                // Charcoal: the regions' borders, the land's lines, and soft shading where it is dark, all broken by the
                // paper's tooth.
                float2 borderUv = _BordersRect.xy + (uv + waver) * _BordersRect.zw;
                float2 spread = _MainTex_TexelSize.xy * 0.8 * _BordersRect.zw;
                float border = max(max(tex2D(_Borders, borderUv).a, tex2D(_Borders, borderUv + spread).a),
                                   max(tex2D(_Borders, borderUv - spread).a, tex2D(_Borders, borderUv + float2(spread.x, -spread.y)).a));
                float fine = saturate((Edges(uv + waver, 1.0) - 0.08) * 5.0);
                float coarse = saturate((Edges(uv + waver * 2.0, 3.5) - 0.07) * 3.5);
                float sketch = lerp(coarse * _SketchUnexplored, fine * _SketchExplored, explored);
                float tooth = lerp(0.45, 1.0, grain.r);
                float charcoal = saturate(max(sketch, saturate(border * 1.6) * _BorderStrength)) * tooth;

                float2 far = _MainTex_TexelSize.xy * 7.0;
                float dark = 1 - (Luma(uv + far) + Luma(uv - far) + Luma(uv + float2(far.x, -far.y)) + Luma(uv + float2(-far.x, far.y))) * 0.25;
                colour *= 1 - saturate(dark - 0.35) * _Smudge * (1 - explored) * lerp(0.5, 1.0, grain.g);

                colour = lerp(colour, _Ink.rgb, charcoal * _Ink.a);
                return fixed4(colour, alpha * input.color.a);
            }
            ENDCG
        }
    }
}
