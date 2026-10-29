#ifndef SAFE_HAVEN_MESH_TESSELLATION
#define SAFE_HAVEN_MESH_TESSELLATION
struct MeshControlPoint
{
    float4 positionOS : INTERNALTESSPOS;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MeshTessFactors
{
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

struct MeshAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

MeshControlPoint MeshVertex(MeshAttributes input)
{
    MeshControlPoint result = (MeshControlPoint)0;
    UNITY_TRANSFER_INSTANCE_ID(input, result);
    result.positionOS = input.positionOS;
    result.normalOS = input.normalOS;
    result.tangentOS = input.tangentOS;
    result.texcoord = input.texcoord;
    result.staticLightmapUV = input.staticLightmapUV;
    result.dynamicLightmapUV = input.dynamicLightmapUV;
    return result;
}

float MeshDistanceFade(float3 positionWS)
{
    return 1 - saturate((distance(positionWS, _WorldSpaceCameraPos) - _TessellationNear)
        / max(_TessellationFar - _TessellationNear, 0.01));
}

float MeshEdgeFactor(float3 a, float3 b)
{
    float fade = MeshDistanceFade((a + b) * 0.5);
    float density = clamp(distance(a, b) / max(_TessellationEdgeLength, 0.005), 1, _TessellationFactor);
    return lerp(1, density, fade);
}

MeshTessFactors MeshPatch(InputPatch<MeshControlPoint, 3> patch)
{
    UNITY_SETUP_INSTANCE_ID(patch[0]);
    float3 a = TransformObjectToWorld(patch[0].positionOS.xyz);
    float3 b = TransformObjectToWorld(patch[1].positionOS.xyz);
    float3 c = TransformObjectToWorld(patch[2].positionOS.xyz);
    MeshTessFactors result;
    result.edge[0] = MeshEdgeFactor(b, c);
    result.edge[1] = MeshEdgeFactor(c, a);
    result.edge[2] = MeshEdgeFactor(a, b);
    result.inside = (result.edge[0] + result.edge[1] + result.edge[2]) / 3;
    return result;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("MeshPatch")]
[maxtessfactor(64.0)]
MeshControlPoint MeshHull(InputPatch<MeshControlPoint, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

float MeshHeight(float2 uv)
{
    float4 channel = max(_HeightChannel, 0);
    channel /= max(dot(channel, float4(1, 1, 1, 1)), 0.0001);
    return (dot(SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, uv, _HeightMip), channel)
        - _HeightMidpoint) * _HeightStrength;
}

[domain("tri")]
Varyings MeshDomain(MeshTessFactors factors, const OutputPatch<MeshControlPoint, 3> patch,
    float3 bary : SV_DomainLocation)
{
    UNITY_SETUP_INSTANCE_ID(patch[0]);
    float3 positionOS = patch[0].positionOS.xyz * bary.x + patch[1].positionOS.xyz * bary.y + patch[2].positionOS.xyz * bary.z;
    float3 normalOS = normalize(patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z);
    float4 tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
    float2 uv = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
    float fade = MeshDistanceFade(positionWS);
    float2 heightUV = TRANSFORM_TEX(uv, _BaseMap);
    float height = MeshHeight(heightUV);
    positionWS += normalWS * height * fade;

    // UV derivatives in world metres keep height-derived normals independent of mesh scale.
    float3 e1 = TransformObjectToWorld(patch[1].positionOS.xyz) - TransformObjectToWorld(patch[0].positionOS.xyz);
    float3 e2 = TransformObjectToWorld(patch[2].positionOS.xyz) - TransformObjectToWorld(patch[0].positionOS.xyz);
    float2 d1 = (patch[1].texcoord - patch[0].texcoord) * _BaseMap_ST.xy;
    float2 d2 = (patch[2].texcoord - patch[0].texcoord) * _BaseMap_ST.xy;
    float determinant = d1.x * d2.y - d1.y * d2.x;
    if (abs(determinant) > 0.000001 && _HeightNormalStrength > 0)
    {
        float3 dpdu = (e1 * d2.y - e2 * d1.y) / determinant;
        float3 dpdv = (e2 * d1.x - e1 * d2.x) / determinant;
        float2 stepUV = max(_HeightMap_TexelSize.xy * exp2(_HeightMip), 0.00001);
        float dhdu = (MeshHeight(heightUV + float2(stepUV.x, 0)) - MeshHeight(heightUV - float2(stepUV.x, 0))) / (2 * stepUV.x);
        float dhdv = (MeshHeight(heightUV + float2(0, stepUV.y)) - MeshHeight(heightUV - float2(0, stepUV.y))) / (2 * stepUV.y);
        dpdu -= normalWS * dot(normalWS, dpdu);
        dpdv -= normalWS * dot(normalWS, dpdv);
        float uu = dot(dpdu, dpdu), uvDot = dot(dpdu, dpdv), vv = dot(dpdv, dpdv);
        float metric = uu * vv - uvDot * uvDot;
        float3 gradient = ((dhdu * vv - dhdv * uvDot) * dpdu + (dhdv * uu - dhdu * uvDot) * dpdv)
            / max(metric, 0.000001);
        normalWS = normalize(normalWS - gradient * fade * _HeightNormalStrength);
        normalOS = TransformWorldToObjectNormal(normalWS);
    }

    Attributes v = (Attributes)0;
    UNITY_TRANSFER_INSTANCE_ID(patch[0], v);
#if defined(MESH_DEPTH_ONLY)
    v.position = float4(TransformWorldToObject(positionWS), 1);
#else
    v.positionOS = float4(TransformWorldToObject(positionWS), 1);
    #if defined(MESH_DEPTH_NORMALS)
        v.normal = normalOS;
    #else
        v.normalOS = normalOS;
    #endif
    #if !defined(MESH_SHADOW)
        float3 tangentWS = TransformObjectToWorldDir(tangentOS.xyz);
        tangentWS = SafeNormalize(tangentWS - normalWS * dot(normalWS, tangentWS));
        v.tangentOS = float4(TransformWorldToObjectDir(tangentWS), tangentOS.w);
    #endif
#endif
    v.texcoord = uv;
#if defined(MESH_FORWARD)
    v.staticLightmapUV = patch[0].staticLightmapUV * bary.x + patch[1].staticLightmapUV * bary.y + patch[2].staticLightmapUV * bary.z;
    v.dynamicLightmapUV = patch[0].dynamicLightmapUV * bary.x + patch[1].dynamicLightmapUV * bary.y + patch[2].dynamicLightmapUV * bary.z;
#endif
    return MESH_PASS_VERTEX(v);
}
#endif
