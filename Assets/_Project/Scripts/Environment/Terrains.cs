using System.Collections.Generic;
using UnityEngine;

/// <summary>The terrain under a point, for scenes made of several terrains (Terrain.activeTerrain is just one of them).</summary>
public static class Terrains
{
    private static readonly List<Terrain> active = new List<Terrain>();
    private static int activeFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        active.Clear();
        activeFrame = -1;
    }

    /// <summary>The loaded terrains, as Terrain.activeTerrains, without its new array on every call (birds, hooves and
    /// water ask many times a frame). Gathered once a frame; an entry can be a terrain destroyed since, so check for null.</summary>
    public static IReadOnlyList<Terrain> Active
    {
        get
        {
            if (activeFrame != Time.frameCount)
            {
                activeFrame = Time.frameCount;
                Terrain.GetActiveTerrains(active);
            }

            return active;
        }
    }

    /// <summary>The terrain whose area holds <paramref name="point"/>, or the active terrain when none does.</summary>
    public static Terrain Under(Vector3 point)
    {
        IReadOnlyList<Terrain> terrains = Active;
        for (int i = 0; i < terrains.Count; i++)
        {
            Terrain terrain = terrains[i];
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
