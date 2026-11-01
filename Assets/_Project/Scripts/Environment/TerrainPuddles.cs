using UnityEngine;

/// <summary>Foot contacts with the rain puddles drawn by WorldWetness in GenerateWorldTerrainShader.py.</summary>
public static class TerrainPuddles
{
    private static readonly int WetnessId = Shader.PropertyToID("_SafeHavenWetness");
    public const float ContactCoverage = 0.15f;

    public static bool Contains(Terrain terrain, Vector3 point)
    {
        if (terrain == null || terrain.terrainData == null || terrain.materialTemplate == null ||
            terrain.materialTemplate.shader == null || terrain.materialTemplate.shader.name != "Safe Haven/Terrain (World)")
        {
            return false;
        }

        Vector3 local = point - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
        {
            return false;
        }

        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z);
        return Coverage(point, terrain.transform.TransformDirection(normal).y,
            Shader.GetGlobalFloat(WetnessId)) >= ContactCoverage;
    }

    public static bool TryFind(Vector3 point, out float height)
    {
        height = 0f;
        if (!Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit,
            1.5f, ~0, QueryTriggerInteraction.Ignore) ||
            !(hit.collider is TerrainCollider) || !hit.collider.TryGetComponent(out Terrain terrain) ||
            !Contains(terrain, hit.point))
        {
            return false;
        }

        height = Mathf.Max(hit.point.y, point.y) + 0.03f;
        return true;
    }

    public static float Coverage(Vector3 point, float normalY, float wetness)
    {
        wetness = Mathf.Clamp01(wetness);
        if (wetness <= 0.001f)
        {
            return 0f;
        }

        Vector2 p = new Vector2(point.x, point.z);
        float lows = Noise(p * 0.75f) * 0.55f + Noise(p * 2.1f + Vector2.one * 17.3f) * 0.3f +
            Noise(p * 5.3f + Vector2.one * 41.7f) * 0.15f;
        float shore = Mathf.Lerp(0.76f, 0.63f, wetness);
        return SmoothStep(shore, shore + 0.05f, lows) * SmoothStep(0.62f, 0.8f, normalY) *
            Mathf.Clamp01(wetness * 3f);
    }

    private static float SmoothStep(float low, float high, float value)
    {
        float t = Mathf.Clamp01((value - low) / (high - low));
        return t * t * (3f - 2f * t);
    }

    private static float Fraction(float value) => value - Mathf.Floor(value);

    private static float Hash(Vector2 p)
    {
        p = new Vector2(Fraction(p.x * 123.34f), Fraction(p.y * 456.21f));
        float dot = Vector2.Dot(p, p + Vector2.one * 45.32f);
        p += Vector2.one * dot;
        return Fraction(p.x * p.y);
    }

    private static float Noise(Vector2 p)
    {
        Vector2 cell = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
        float x = Fraction(p.x), y = Fraction(p.y);
        x = x * x * (3f - 2f * x);
        y = y * y * (3f - 2f * y);
        return Mathf.Lerp(Mathf.Lerp(Hash(cell), Hash(cell + Vector2.right), x),
            Mathf.Lerp(Hash(cell + Vector2.up), Hash(cell + Vector2.one), x), y);
    }
}
