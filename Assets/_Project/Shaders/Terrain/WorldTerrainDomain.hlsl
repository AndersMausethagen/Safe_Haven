[domain("tri")]
WORLD_VARYINGS WorldDomain(WorldTessFactors factors, const OutputPatch<WorldControlPoint, 3> patch, float3 bary : SV_DomainLocation)
{
    WORLD_ATTRIBUTES v = (WORLD_ATTRIBUTES)0;
    UNITY_TRANSFER_INSTANCE_ID(patch[0], v);
    UNITY_SETUP_INSTANCE_ID(v);
    float4 position = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
    v.normalOS = normalize(patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z);
    v.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
    WorldDisplace(position, v.normalOS, v.texcoord);
    v.WORLD_POSITION = position;
    return WORLD_VERTEX(v);
}
