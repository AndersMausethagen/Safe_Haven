using UnityEngine;

/// <summary>
/// What a horse stands on, read from the terrain under it: the name of the terrain layer painted most strongly there
/// (Haven Path, Muddy Bank, Cliff Rock...), which HorseSettings › Grounds turns into a speed, and how steeply the ground
/// rises ahead. Off any terrain (a building's floor, a bridge) there is no layer and nothing changes.
/// </summary>
public static class HorseGround
{
    /// <summary>What a hoof strikes: earth and grass, stone, wood (a floor, a bridge, a dock) or shallow water.</summary>
    public enum Surface { Earth, Stone, Wood, Water }

    private static readonly string[] Stony = { "Rock", "Gravel", "Cliff", "Stone", "Cobble" };

    /// <summary>The surface under a point: water over it, a built floor under it, or the terrain's layer.</summary>
    public static Surface SurfaceAt(Vector3 point)
    {
        if (WaterSurface.TryFind(point + Vector3.up * 0.3f, 0.6f, out float water) && water > point.y + 0.04f)
        {
            return Surface.Water;
        }

        if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.1f, ~0, QueryTriggerInteraction.Ignore)
            && !(hit.collider is TerrainCollider) && hit.collider.GetComponentInParent<Horse>() == null)
        {
            // Built ground: a stone floor or path by its look, wood otherwise (planks, bridges, docks).
            Renderer drawn = hit.collider.GetComponentInParent<Renderer>();
            string look = drawn != null && drawn.sharedMaterial != null ? drawn.sharedMaterial.name : hit.collider.name;
            return IsStony(look) ? Surface.Stone : Surface.Wood;
        }

        return IsStony(LayerAt(point)) ? Surface.Stone : Surface.Earth;
    }

    private static bool IsStony(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (string word in Stony)
        {
            if (name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The name of the strongest terrain layer at a point, or empty.</summary>
    public static string LayerAt(Vector3 point)
    {
        Terrain terrain = TerrainAt(point);
        if (terrain == null || terrain.terrainData.alphamapLayers == 0)
        {
            return "";
        }

        TerrainData data = terrain.terrainData;
        Vector3 local = point - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
        float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
        int best = 0;
        for (int i = 1; i < weights.GetLength(2); i++)
        {
            if (weights[0, 0, i] > weights[0, 0, best])
            {
                best = i;
            }
        }

        TerrainLayer layer = best < data.terrainLayers.Length ? data.terrainLayers[best] : null;
        return layer != null ? layer.name : "";
    }

    /// <summary>How much the ground rises over <paramref name="ahead"/> metres the way it goes (rise over run; negative downhill).</summary>
    public static float GradeAhead(Vector3 point, Vector3 forward, float ahead)
    {
        Terrain terrain = TerrainAt(point);
        if (terrain == null)
        {
            return 0f;
        }

        forward.y = 0f;
        Vector3 next = point + forward.normalized * ahead;
        return (terrain.SampleHeight(next) - terrain.SampleHeight(point)) / ahead;
    }

    private static Terrain TerrainAt(Vector3 point)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 corner = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (point.x >= corner.x && point.x <= corner.x + size.x && point.z >= corner.z && point.z <= corner.z + size.z)
            {
                return terrain;
            }
        }

        return null;
    }
}
