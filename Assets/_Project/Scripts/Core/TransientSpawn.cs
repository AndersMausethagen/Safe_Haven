using UnityEngine;

/// <summary>
/// Spawns a copy of a prefab that is not part of the saved scene (ward-breach attackers, night prowlers at home): its
/// SaveableEntities are removed before it wakes, so a save never records it, and optionally its loot too.
/// </summary>
public static class TransientSpawn
{
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool keepLoot)
    {
        var holder = new GameObject("Spawning");
        holder.SetActive(false);
        GameObject copy = Object.Instantiate(prefab, position, rotation, holder.transform);
        foreach (SaveableEntity saveable in copy.GetComponentsInChildren<SaveableEntity>(true))
        {
            Object.DestroyImmediate(saveable);
        }

        if (!keepLoot)
        {
            foreach (LootOnDeath loot in copy.GetComponentsInChildren<LootOnDeath>(true))
            {
                Object.DestroyImmediate(loot);
            }
        }

        copy.transform.SetParent(null, true);
        Object.Destroy(holder);
        return copy;
    }
}
