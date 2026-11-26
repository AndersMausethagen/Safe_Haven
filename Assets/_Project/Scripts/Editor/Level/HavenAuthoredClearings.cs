using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Open ground for the places written by hand into the Haven's atlas (WorldAtlas.Authored, Docs/HavenWorldStory_2026-11-26.md step 4):
/// the plan clears trees round its own places, and these stand on its glades where trees still grow. Clears the terrain's painted
/// trees inside each one's Clearing. Run by the world generator's Finish (so a regenerated world keeps them) and once by hand when a
/// place is written. Felled-tree saves go back to all standing when the tree count changes (TerrainTreeMemory), as on any regeneration.
/// </summary>
public static class HavenAuthoredClearings
{
    /// <summary>Removes the terrain trees inside the authored places' clearings; returns how many.</summary>
    public static int Clear(Terrain terrain, WorldAtlas atlas)
    {
        if (terrain == null || terrain.terrainData == null || atlas == null)
        {
            return 0;
        }

        var circles = new List<Vector3>();
        foreach (WorldAtlas.Place place in atlas.Authored)
        {
            if (place != null && place.clearing > 0f)
            {
                circles.Add(new Vector3(place.at.x, place.at.y, place.clearing));
            }
        }

        if (circles.Count == 0)
        {
            return 0;
        }

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        TreeInstance[] trees = data.treeInstances;
        var kept = new List<TreeInstance>(trees.Length);
        foreach (TreeInstance tree in trees)
        {
            float x = origin.x + tree.position.x * size.x, z = origin.z + tree.position.z * size.z;
            bool inside = false;
            foreach (Vector3 circle in circles)
            {
                if ((x - circle.x) * (x - circle.x) + (z - circle.y) * (z - circle.y) < circle.z * circle.z)
                {
                    inside = true;
                    break;
                }
            }

            if (!inside)
            {
                kept.Add(tree);
            }
        }

        if (kept.Count != trees.Length)
        {
            data.SetTreeInstances(kept.ToArray(), true);
            UnityEditor.EditorUtility.SetDirty(data);
        }

        return trees.Length - kept.Count;
    }
}
