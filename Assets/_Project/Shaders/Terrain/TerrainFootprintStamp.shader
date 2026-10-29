Shader "Hidden/Safe Haven/Terrain/Footprint Stamp"
{
    Properties
    {
        _MainTex("Previous Field", 2D) = "black" {}
        _SoleTex("Sole Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            sampler2D _MainTex, _SoleTex;
            float2 _Shift;
            float _Decay, _Stamp, _UseSoleTexture;
            float4 _Boot; // centre UV, inverse width/length in field UV
            float2 _Direction;

            float Fragment(v2f_img input) : SV_Target
            {
                float2 oldUV = input.uv + _Shift;
                float inside = all(oldUV >= 0) && all(oldUV <= 1);
                float previous = max(0, tex2D(_MainTex, saturate(oldUV)).r - _Decay) * inside;
                if (_Stamp <= 0) return previous;
                float2 offset = input.uv - _Boot.xy;
                float2 local = float2(dot(offset, float2(_Direction.y, -_Direction.x)), dot(offset, _Direction)) * _Boot.zw;
                float2 soleUV = local + 0.5;
                float sole = 0;
                if (all(soleUV >= 0) && all(soleUV <= 1))
                {
                    if (_UseSoleTexture > 0.5)
                        sole = tex2D(_SoleTex, soleUV).r;
                    else
                    {
                        // Rounded toe, narrower arch, separate heel and shallow tread grooves.
                        float width = local.y > -0.05 ? 0.48 : 0.36;
                        float body = 1 - smoothstep(0.82, 1, length(float2(local.x / width, (local.y - 0.08) / 0.42)));
                        float heel = 1 - smoothstep(0.8, 1, length(float2(local.x / 0.37, (local.y + 0.32) / 0.17)));
                        float tread = smoothstep(0.12, 0.3, abs(frac((local.y + abs(local.x) * 0.25) * 7) - 0.5));
                        sole = max(body, heel) * lerp(0.3, 1, tread);
                    }
                }
                return max(previous, saturate(sole) * _Stamp);
            }
            ENDHLSL
        }
    }
}
