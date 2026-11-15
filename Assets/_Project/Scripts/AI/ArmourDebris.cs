using UnityEngine;

/// <summary>
/// Pieces breaking off a creature (Docs/EnemyCombatResearch.md §4.9): a piece of armour chipped by a heavy blow (CreatureWounds, Monster
/// Definition › Armour Parts) or a splintered shield (MonsterShieldPose). The piece on the body is hidden, and a loose copy of what it
/// looked like at that moment drops from where it was, thrown along the blow, and is gone after a while. A look only: what the chip costs
/// the creature is CreatureWounds' armour.
/// </summary>
public static class ArmourDebris
{
    private const float Lifetime = 6f;
    private const float Throw = 2.2f;
    private const float Lift = 1.6f;

    /// <summary>Breaks off each named child of <paramref name="body"/> (names separated by commas, the first match of each). Returns how many came away.</summary>
    public static int BreakOff(Transform body, string names, Vector3 blowDirection)
    {
        int broken = 0;
        foreach (string raw in names.Split(','))
        {
            string name = raw.Trim();
            Transform part = name.Length > 0 ? Find(body, name) : null;
            if (part != null && part.gameObject.activeInHierarchy)
            {
                BreakOff(part, blowDirection);
                broken++;
            }
        }

        return broken;
    }

    /// <summary>Breaks <paramref name="part"/> off: hidden on the body, a loose copy thrown along the blow. A splintered one also sounds.</summary>
    public static void BreakOff(Transform part, Vector3 blowDirection, bool splinters = false)
    {
        Vector3 along = blowDirection;
        along.y = 0f;
        along = along.sqrMagnitude > 1e-4f ? along.normalized : -part.forward;
        foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>())
        {
            // The see-through outline's copies (EnemyVisibility) hide with the piece; they aren't pieces of it.
            if (!OutlineCopies.IsCopy(renderer))
            {
                Loose(renderer, along);
            }
        }

        part.gameObject.SetActive(false);
        if (splinters)
        {
            WoundSettings settings = WoundSettings.Current;
            AudioManager.Play(settings.ArmourChipSound, part.position);
            if (settings.ArmourShards != null)
            {
                PrefabPool.Spawn(settings.ArmourShards, part.position, Quaternion.LookRotation(along));
            }
        }
    }

    private static void Loose(Renderer renderer, Vector3 along)
    {
        Mesh mesh = null;
        bool baked = false;
        if (renderer is SkinnedMeshRenderer skinned)
        {
            mesh = new Mesh { name = skinned.name + " (broken off)" };
            skinned.BakeMesh(mesh, true);
            baked = true;
        }
        else if (renderer.TryGetComponent(out MeshFilter filter))
        {
            mesh = filter.sharedMesh;
        }

        if (mesh == null || mesh.vertexCount == 0)
        {
            return;
        }

        Transform from = renderer.transform;
        var piece = new GameObject(renderer.name + " (broken off)");
        piece.transform.SetPositionAndRotation(from.position, from.rotation);
        // A baked skinned mesh is already at world scale; a plain mesh keeps its object's scale.
        piece.transform.localScale = baked ? Vector3.one : from.lossyScale;
        piece.AddComponent<MeshFilter>().sharedMesh = mesh;
        piece.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        BoxCollider box = piece.AddComponent<BoxCollider>();
        box.center = mesh.bounds.center;
        box.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.02f);
        Rigidbody body = piece.AddComponent<Rigidbody>();
        body.mass = 2f;
        body.linearVelocity = along * Throw * Random.Range(0.7f, 1.2f) + Vector3.up * Lift * Random.Range(0.7f, 1.2f);
        body.angularVelocity = Random.insideUnitSphere * 8f;
        Object.Destroy(piece, Lifetime);
        if (baked)
        {
            Object.Destroy(mesh, Lifetime + 0.1f);
        }
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }
}
