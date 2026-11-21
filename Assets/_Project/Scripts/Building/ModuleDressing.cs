using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a fitted module looks like outside its building (the systems review, 2026-11-21: modules changed numbers and nothing to see;
/// Valheim's station extensions stand beside the bench, so an upgrade is read from across the yard). A module with a dressing
/// (BuildingModuleDefinition.Dressing: a look prefab such as the Sawhorse for the Frame Saw) shows it by the building's door wall,
/// beside the door and clear of it, one after another outwards, standing on the ground. Only a standing building shows them, and
/// the pieces are looks only: no colliders, nothing saved (they follow Construction.Fitted, which is).
/// </summary>
public static class ModuleDressing
{
    private const string Prefix = "Module Dressing: ";
    // Metres from the door's middle to the first piece, between pieces, and out from the wall.
    private const float FirstAside = 2.3f;
    private const float Between = 1.5f;
    private const float Out = 0.35f;

    /// <summary>Puts up the dressings of the modules fitted to a standing building, and takes down any others.</summary>
    public static void Show(Construction construction)
    {
        if (construction == null || !Application.isPlaying)
        {
            return;
        }

        var wanted = new List<BuildingModuleDefinition>();
        if (construction.IsStanding)
        {
            foreach (BuildingModuleDefinition module in construction.Fitted)
            {
                if (module != null && module.Dressing != null && !wanted.Contains(module))
                {
                    wanted.Add(module);
                }
            }
        }

        Transform root = construction.transform;
        var shown = new Dictionary<string, Transform>();
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (!child.name.StartsWith(Prefix))
            {
                continue;
            }

            string id = child.name.Substring(Prefix.Length);
            if (wanted.Exists(module => module.Id == id) && !shown.ContainsKey(id))
            {
                shown[id] = child;
            }
            else
            {
                Object.Destroy(child.gameObject);
            }
        }

        if (wanted.Count == 0 || !construction.TryGetComponent(out BuildingObject building))
        {
            return;
        }

        for (int i = 0; i < wanted.Count; i++)
        {
            BuildingModuleDefinition module = wanted[i];
            if (!shown.TryGetValue(module.Id, out Transform piece))
            {
                piece = Make(module, root);
            }

            Place(piece, building, i);
        }
    }

    // A copy of the look with nothing that acts or blocks: made under a switched-off holder so nothing in it wakes first.
    private static Transform Make(BuildingModuleDefinition module, Transform root)
    {
        var holder = new GameObject("Dressing Holder");
        holder.SetActive(false);
        GameObject piece = Object.Instantiate(module.Dressing, holder.transform);
        piece.name = Prefix + module.Id;
        foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true))
        {
            Object.Destroy(collider);
        }

        foreach (MonoBehaviour behaviour in piece.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.Destroy(behaviour);
        }

        piece.transform.SetParent(root, false);
        Object.Destroy(holder);
        piece.layer = root.gameObject.layer;
        return piece.transform;
    }

    // Beside the door along its wall, alternating right and left, outward from the door; turned to face out like the door.
    private static void Place(Transform piece, BuildingObject building, int index)
    {
        Vector3 door = building.Door(out Quaternion outward);
        Vector3 along = outward * Vector3.right;
        Vector3 forward = outward * Vector3.forward;
        float side = index % 2 == 0 ? 1f : -1f;
        float aside = FirstAside + (index / 2) * Between;
        // The door sits 0.55 m outside the wall: back to the wall, then out a little.
        Vector3 at = door + along * (side * aside) - forward * (0.55f - Out);
        at.y = GroundAt(at, building.transform.position.y);
        piece.SetPositionAndRotation(at, outward);
    }

    private static float GroundAt(Vector3 point, float fallback)
    {
        Terrain terrain = Terrain.activeTerrain;
        return terrain != null ? terrain.SampleHeight(point) + terrain.transform.position.y : fallback;
    }
}
