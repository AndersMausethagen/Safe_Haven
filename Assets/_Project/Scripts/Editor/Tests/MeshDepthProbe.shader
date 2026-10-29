Shader "Hidden/Safe Haven/Mesh Depth Probe"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest NotEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 Vertex(uint id : SV_VertexID) : SV_POSITION
            {
                float4 position = GetFullScreenTriangleVertexPosition(id);
                #if UNITY_REVERSED_Z
                    position.z = 0;
                #else
                    position.z = 1;
                #endif
                return position;
            }
            half4 Fragment() : SV_Target { return half4(1,1,1,1); }
            ENDHLSL
        }
    }
}
