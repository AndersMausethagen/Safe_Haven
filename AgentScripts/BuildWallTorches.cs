using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wall torches on the Haven's buildings (`unity command script run --file AgentScripts/BuildWallTorches.cs --entry
/// BuildWallTorches.Apply`; the user 2026-11-14: "more buildings should have torches as part of their mesh, lighting up
/// when it gets dark"). Each building in the list gets one or two Wall Torch parts (BuildStructureModels.Decor makes the
/// part) on its front wall, beside the door: a ray against the building's own model finds the wall, and the torch is set
/// on it facing out, low enough that its flame shows under the eaves from the game's camera (the first try, at two fifths
/// of the height, hid every flame behind the roof's edge: only the glow on the eaves showed). Lit after dusk by
/// NightFlame. Run after the models or their sizes change (BuildStructureModels, BuildBuildingScale); re-runnable, it
/// takes the old torches down first.
/// </summary>
public static class BuildWallTorches
{
    private const string TorchPrefab = "Assets/_Project/Prefabs/Structures/Parts/Wall Torch.prefab";
    private const string TorchName = "Wall Torch";

    // The buildings and how many torches: two on the workplaces and the big buildings, one beside a home's door.
    private static readonly (string building, int count)[] Lit =
    {
        ("Forge", 2), ("ConstructionTable", 2), ("Sawbench", 2), ("TanningRack", 2), ("Loom", 2), ("TradingPost", 2),
        ("Stable", 2), ("WainwrightsYard", 2), ("Cottage", 1), ("Hut", 1), ("Sauna", 1),
    };

    // Across the front, as shares of the width from the centre: the first pair that finds wall on both sides is used.
    private static readonly float[] Offsets = { 0.3f, 0.22f, 0.38f, 0.15f };
    // Heights to try, as shares of the model's height: under the eaves of a low turf roof.
    private static readonly float[] Heights = { 0.4f, 0.34f, 0.28f };
    // The gameplay camera's pitch (PlayerCameraController's top-down profile, measured in Play: 47°): how steeply the
    // view looks down past a roof's edge at the wall below it.
    private const float CameraPitch = 47f;
    // The Wall Torch part's flame centre in its own space: up from the mount and out from the wall. The centre is kept in
    // view (so at least the flame's upper half shows), not the whole flame: that asked the low-roofed homes for a torch
    // at their feet.
    private const float FlameUp = 0.43f, FlameOut = 0.27f, FlameHalf = 0f;
    // The lowest a torch is mounted, above the model's foot.
    private const float LowestMount = 0.45f;

    public static string Apply()
    {
        var torch = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefab);
        if (torch == null)
        {
            return "no Wall Torch part: run BuildStructureModels.Decor first";
        }

        var report = new List<string>();
        foreach ((string name, int count) in Lit)
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/" + name + ".asset");
            if (definition == null || definition.Prefab == null)
            {
                report.Add($"{name}: no building");
                continue;
            }

            string placed = "";
            PrefabBuildUtility.EditPrefab(definition.Prefab, root => placed = Place(root, torch, count, definition));
            report.Add($"{name}: {placed}");
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    private static string Place(GameObject root, GameObject torch, int count, BuildingDefinition definition)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = root.transform.GetChild(i);
            if (child.name.StartsWith(TorchName))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        (Vector3[] points, int[] triangles) = Body(root);
        if (points == null)
        {
            return "no model mesh";
        }

        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (Vector3 point in points) bounds.Encapsulate(point);

        // Halved buildings get torches a little smaller, not half: still a torch a person could carry.
        float scale = Mathf.Lerp(1f, definition.ModelScale, 0.5f);
        var found = new List<(Vector3 at, Vector3 normal)>();
        foreach (float height in Heights)
        {
            float y = Mathf.Clamp(bounds.min.y + bounds.size.y * height, 0.9f, 2.2f);
            foreach (float offset in Offsets)
            {
                found.Clear();
                float[] sides = count == 1 ? new[] { offset } : new[] { -offset, offset };
                foreach (float side in sides)
                {
                    var from = new Vector3(bounds.center.x + side * bounds.size.x, y, bounds.max.z + 1f);
                    if (!Wall(points, triangles, from, Vector3.back, bounds, out Vector3 at, out Vector3 normal))
                    {
                        continue;
                    }

                    // Low enough to be seen past the eaves: the flame's top under the line from the roof's edge to the
                    // camera. Then the wall again at that height (a plinth or a porch may stand further out).
                    float mount = InView(points, triangles, at, bounds, scale);
                    if (mount < bounds.min.y + LowestMount)
                    {
                        continue;
                    }

                    if (mount < at.y)
                    {
                        from.y = mount;
                        if (!Wall(points, triangles, from, Vector3.back, bounds, out at, out normal))
                        {
                            continue;
                        }
                    }

                    found.Add((at, normal));
                }

                if (found.Count == sides.Length)
                {
                    break;
                }
            }

            if (found.Count == (count == 1 ? 1 : 2))
            {
                break;
            }
        }

        if (found.Count == 0)
        {
            return "no front wall found";
        }

        int n = 0;
        foreach ((Vector3 at, Vector3 normal) in found)
        {
            var part = (GameObject)PrefabUtility.InstantiatePrefab(torch, root.transform);
            part.name = found.Count > 1 ? $"{TorchName} {++n}" : TorchName;
            Vector3 facing = new Vector3(normal.x, 0f, normal.z).normalized;
            part.transform.localPosition = at + facing * 0.005f;
            part.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            part.transform.localScale = Vector3.one * scale;
        }

        return string.Join(", ", found.Select(f => $"({f.at.x:0.00}, {f.at.y:0.00}, {f.at.z:0.00})")) + $" x{scale:0.00}";
    }

    // The highest mount at this place on the wall from which the flame shows under the roof: rays down from above step out
    // from the wall until they meet no more roof, giving the eave's height and how far it reaches past the wall.
    private static float InView(Vector3[] points, int[] triangles, Vector3 wall, Bounds bounds, float scale)
    {
        float eave = float.MaxValue, reach = 0f;
        for (float out_ = 0.02f; out_ < 2f; out_ += 0.05f)
        {
            var from = new Vector3(wall.x, bounds.max.y + 1f, wall.z + out_);
            if (!Nearest(points, triangles, from, Vector3.down, out Vector3 at, out Vector3 _) || at.y < wall.y)
            {
                break;
            }

            eave = Mathf.Min(eave, at.y);
            reach = out_;
        }

        if (eave == float.MaxValue)
        {
            return wall.y; // no roof over this wall
        }

        float covered = Mathf.Max(0f, reach - FlameOut * scale);
        float roofUnder = eave - 0.12f; // the eave's own thickness
        float flameTop = roofUnder - covered * Mathf.Tan(CameraPitch * Mathf.Deg2Rad) - 0.04f;
        return Mathf.Min(wall.y, flameTop - (FlameUp + FlameHalf) * scale);
    }

    private static bool Nearest(Vector3[] points, int[] triangles, Vector3 from, Vector3 direction, out Vector3 at, out Vector3 normal)
    {
        at = normal = Vector3.zero;
        float nearest = float.MaxValue;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            if (Hit(from, direction, a, b, c, out float distance) && distance < nearest)
            {
                nearest = distance;
                at = from + direction * distance;
                normal = Vector3.Cross(b - a, c - a).normalized;
            }
        }

        return nearest < float.MaxValue;
    }

    // The building's model, its biggest LOD0, in the root's space (its scale and the model's ModelSize applied).
    private static (Vector3[] points, int[] triangles) Body(GameObject root)
    {
        MeshFilter best = root.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null && filter.name == "LOD0" && filter.GetComponentInParent<NightFlame>(true) == null)
            .OrderByDescending(filter => filter.sharedMesh.triangles.Length)
            .FirstOrDefault();
        if (best == null)
        {
            return (null, null);
        }

        Matrix4x4 toRoot = root.transform.worldToLocalMatrix * best.transform.localToWorldMatrix;
        Vector3[] points = best.sharedMesh.vertices.Select(v => toRoot.MultiplyPoint3x4(v)).ToArray();
        return (points, best.sharedMesh.triangles);
    }

    // The nearest face the ray meets that is a wall turned towards it (not a roof, not a floor), and in the front part of
    // the building: a ray through a door or an open front that meets the back wall finds nothing.
    private static bool Wall(Vector3[] points, int[] triangles, Vector3 from, Vector3 direction, Bounds bounds, out Vector3 at, out Vector3 normal)
    {
        at = normal = Vector3.zero;
        float nearest = float.MaxValue;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            if (!Hit(from, direction, a, b, c, out float distance) || distance >= nearest)
            {
                continue;
            }

            Vector3 face = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(face, direction) > 0f)
            {
                face = -face; // the side the ray comes from
            }

            nearest = distance;
            at = from + direction * distance;
            normal = face;
        }

        return nearest < float.MaxValue && Mathf.Abs(normal.y) < 0.35f && Vector3.Dot(normal, -direction) > 0.6f
            && at.z > bounds.max.z - bounds.size.z * 0.4f;
    }

    // Möller–Trumbore, both sides.
    private static bool Hit(Vector3 from, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0f;
        Vector3 ab = b - a, ac = c - a;
        Vector3 p = Vector3.Cross(direction, ac);
        float det = Vector3.Dot(ab, p);
        if (Mathf.Abs(det) < 1e-7f)
        {
            return false;
        }

        float inverse = 1f / det;
        Vector3 t = from - a;
        float u = Vector3.Dot(t, p) * inverse;
        if (u < 0f || u > 1f)
        {
            return false;
        }

        Vector3 q = Vector3.Cross(t, ab);
        float v = Vector3.Dot(direction, q) * inverse;
        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        distance = Vector3.Dot(ac, q) * inverse;
        return distance > 0f;
    }
}
