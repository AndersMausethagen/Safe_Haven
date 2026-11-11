using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Stands the boundary stones in the Haven when the scene starts: one for each claimable region, where the atlas says (beside a
/// path near the region's middle), dropped onto the walkable ground. Runtime objects, so the scene file never changes for them.
/// Made for every scene with a player and the Haven's atlas (ExperienceTracker), which also tells the player what happened to
/// the land while they were away.
/// </summary>
public static class TerritorySpawner
{
    public static void Begin(string sceneName)
    {
        if (sceneName != Territory.HomeScene || Territory.Atlas == null || GameObject.Find("Boundary Stones") != null)
        {
            return;
        }

        GameObject prefab = TerritorySettings.Current.StonePrefab;
        if (prefab == null)
        {
            Debug.LogWarning("TerritorySettings has no boundary stone prefab: run AgentScripts/BuildTerritory.cs.");
            return;
        }

        var holder = new GameObject("Boundary Stones");
        foreach (WorldAtlas.Region region in Territory.Claimable)
        {
            if (region.stoneAt == Vector2.zero)
            {
                continue;
            }

            Vector3 wanted = new Vector3(region.stoneAt.x, 0f, region.stoneAt.y);
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                wanted.y = terrain.SampleHeight(wanted) + terrain.transform.position.y;
            }

            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                wanted = hit.position;
            }

            // Facing home, so its carved face greets the way back.
            Vector3 toHome = -new Vector3(wanted.x, 0f, wanted.z);
            Quaternion facing = toHome.sqrMagnitude > 1f ? Quaternion.LookRotation(toHome.normalized, Vector3.up) : Quaternion.identity;
            GameObject stone = Object.Instantiate(prefab, wanted, facing, holder.transform);
            stone.name = $"Boundary Stone ({region.name})";
            stone.GetComponent<BoundaryStone>().Configure(region.name);
        }

        Territory.PostWaiting();
    }
}
