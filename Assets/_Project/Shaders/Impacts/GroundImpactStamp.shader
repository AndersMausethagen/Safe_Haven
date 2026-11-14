// The ground-impact field (GroundImpactField): R = how far the ground is pressed down in metres (negative: a raised lip),
// G = how fast R heals (metres per second), B = how far the grass is pressed down or cut (0-1), A = how fast B heals.
// Pass 0 heals the field by _Delta seconds and moves its window by _Shift; pass 1 presses one mark into it.
Shader "Hidden/Safe Haven/Ground Impact Stamp"
{
    Properties
    {
        _MainTex("Previous Field", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        HLSLINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float2 _Shift;
        float _Delta;
        float2 _Centre;     // field UV
        float _Area;        // metres across the field
        float2 _Direction;  // unit, on the ground
        float _Shape;       // 1 crater, 2 furrow, 3 mow
        float _Radius, _Depth, _Rim, _Length, _Flatten, _Seconds, _ArcCos, _Seed;

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float Noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
        }

        // A pit with a raised lip, its edge broken up a little: depth at the middle, the lip just outside the edge.
        float2 Pit(float distance, float radius, float depth, float rim, float wobble)
        {
            float d = distance / max(radius * (0.85 + 0.3 * wobble), 1e-3);
            float bowl = saturate(1 - d * d);
            float lip = exp(-((d - 1.12) * (d - 1.12)) / 0.06);
            return float2(depth * bowl - rim * lip, saturate(1.45 - d));
        }
        ENDHLSL

        Pass
        {
            Name "Heal"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Heal
            float4 Heal(v2f_img input) : SV_Target
            {
                float2 oldUV = input.uv + _Shift;
                float inside = all(oldUV >= 0) && all(oldUV <= 1);
                float4 v = tex2D(_MainTex, saturate(oldUV)) * inside;
                float r = sign(v.r) * max(0, abs(v.r) - v.g * _Delta);
                float b = max(0, v.b - v.a * _Delta);
                return float4(r, abs(r) > 1e-5 ? v.g : 0, b, b > 1e-5 ? v.a : 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Stamp"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Stamp
            float4 Stamp(v2f_img input) : SV_Target
            {
                float4 v = tex2D(_MainTex, input.uv);
                float2 d = (input.uv - _Centre) * _Area; // metres from the middle
                float2 side = float2(_Direction.y, -_Direction.x);
                float along = dot(d, _Direction);
                float wobble = Noise(d * 1.7 + _Seed);
                float height = 0, lowered = 0;
                if (_Shape < 1.5)
                {
                    float2 pit = Pit(length(d), _Radius, _Depth, _Rim, wobble);
                    height = pit.x;
                    lowered = pit.y * _Flatten;
                }
                else if (_Shape < 2.5)
                {
                    // Ploughed from behind to here: deepest where it stopped.
                    float t = clamp(along, -_Length, 0);
                    float2 pit = Pit(length(d - _Direction * t), _Radius, _Depth * lerp(0.45, 1, (t + _Length) / max(_Length, 1e-3)), _Rim, wobble);
                    height = pit.x;
                    lowered = pit.y * _Flatten;
                }
                else
                {
                    // Mown: an arc of grass cut short ahead of the swing, ragged at its edge, a scuff in the ground.
                    float distance = length(d);
                    float facing = distance > 1e-3 ? dot(d / distance, _Direction) : 1;
                    float arc = smoothstep(_ArcCos - 0.06, _ArcCos + 0.06, facing);
                    float reach = (1 - smoothstep(_Radius * (0.8 + 0.2 * wobble), _Radius * 1.05, distance)) * smoothstep(0.25, 0.6, distance);
                    lowered = _Flatten * arc * reach * (0.75 + 0.25 * wobble);
                    height = 0.008 * arc * reach;
                }

                float4 o = v;
                if (abs(height) > abs(v.r))
                {
                    o.r = height;
                    o.g = abs(height) / max(_Seconds, 1);
                }
                if (lowered > v.b)
                {
                    o.b = lowered;
                    o.a = lowered / max(_Seconds, 1);
                }
                return o;
            }
            ENDHLSL
        }

        Pass
        {
            // Into a terrain's footprint field (its _MainTex here): R, the prints, kept; G = this field's ground offset at
            // the same spot, so the terrain shader reads both from the one texture it already has.
            Name "Mirror"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Mirror
            sampler2D _ImpactSource;
            float4 _FootprintWindow; // origin x, z, metres across
            float4 _ImpactWindow;    // origin x, z, 1 / metres
            float2 Mirror(v2f_img input) : SV_Target
            {
                float prints = tex2D(_MainTex, input.uv).r;
                float2 world = _FootprintWindow.xy + input.uv * _FootprintWindow.z;
                float2 uv = (world - _ImpactWindow.xy) * _ImpactWindow.z;
                float mark = all(uv >= 0) && all(uv <= 1) ? tex2D(_ImpactSource, uv).r : 0;
                return float2(prints, mark);
            }
            ENDHLSL
        }
    }
}
