using UnityEngine;

/// <summary>The terrain under a point, for scenes made of several terrains (Terrain.activeTerrain is just one of them).</summary>
public static class Terrains
{
    /// <summary>The terrain whose area holds <paramref name="point"/>, or the active terrain when none does.</summary>
    public static Terrain Under(Vector3 point)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (point.x >= origin.x && point.x <= origin.x + size.x && point.z >= origin.z && point.z <= origin.z + size.z)
            {
                return terrain;
            }
        }

        return Terrain.activeTerrain;
    }
}
