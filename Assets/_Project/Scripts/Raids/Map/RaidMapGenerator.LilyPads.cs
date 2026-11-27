using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lily pads on the made map's still water (RaidMapDefinition › Lily Pads): laid out by RaidLilyPads over the shaped terrain, on the ponds'
/// and the fen's water, kept off trails, clearings, rivers and ravines. Part of RaidMapGenerator, in its own file.
/// </summary>
public static partial class RaidMapGenerator
{
    public const string LilyPadsName = "Lily Pads";

    // Metres pads keep from a river's water and a ravine's edge: running water carries none, and a fen's sheet runs on over both.
    private const float LilyRiverClearance = 3f;
    private const float LilyRavineClearance = 2f;

    /// <summary>
    /// Lays the map definition's lily pads, in place of an earlier attempt's, after the terrain is shaped and the water placed. Kept out of
    /// the map's Content, which is snapped to the ground. <paramref name="meshes"/> holds the meshes made, for whoever owns the map to
    /// destroy. Returns a few words for the generation log.
    /// </summary>
    public static string PlaceLilyPads(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutField field, List<Mesh> meshes)
    {
        RemoveOld(map.transform, LilyPadsName);

        RaidLilyPadRule rule = map.Definition != null ? map.Definition.LilyPads : null;
        if (rule == null || !rule.Enabled || field == null)
        {
            return "no lily pads";
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var padMeshes = new Mesh[rule.pads.Count];
        var radii = new float[rule.pads.Count];
        Material material = rule.material;
        for (int i = 0; i < padMeshes.Length; i++)
        {
            GameObject prefab = rule.pads[i]?.prefab;
            padMeshes[i] = MeshOf(prefab);
            radii[i] = RaidLilyPads.Radius(padMeshes[i]);
            if (material == null && prefab != null && prefab.GetComponentInChildren<MeshRenderer>() is MeshRenderer shown)
            {
                material = shown.sharedMaterial;
            }
        }

        if (material == null)
        {
            Debug.LogWarning($"{map.Definition.name} wants lily pads but neither its Lily Pads › Material nor its first pad has a material.", map.Definition);
            return "no lily pads (no material)";
        }

        TerrainData data = map.Terrain.terrainData;
        int resolution = data.heightmapResolution;
        float[,] bed = data.GetHeights(0, 0, resolution, resolution);
        float tall = data.size.y;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                bed[y, x] *= tall;
            }
        }

        float fen = rules.Fen.enabled ? field.FenWater : float.NaN;
        float StillWater(Vector2 point)
        {
            foreach (RaidLayoutPond pond in plan.Ponds)
            {
                if ((point - pond.Position).sqrMagnitude < pond.Reach * pond.Reach && pond.Edge(point) < 0f)
                {
                    return pond.Level;
                }
            }

            return fen;
        }

        bool KeepClear(Vector2 point) => field.WalkedDistance(point.x / plan.Size, point.y / plan.Size) < rule.trailClearance
            || plan.InRiver(point, LilyRiverClearance) || plan.OverRavine(point, out _, out _, LilyRavineClearance);

        List<RaidLilyPads.Piece> pieces = RaidLilyPads.Place(rule, RaidLayout.SeedFor(plan.Seed, "Lily Pads"), plan.Size, bed, StillWater,
            KeepClear, radii);
        if (pieces.Count == 0)
        {
            return $"no lily pads (no water suits), {clock.ElapsedMilliseconds} ms";
        }

        var holder = new GameObject(LilyPadsName).transform;
        holder.SetParent(map.transform, false);
        holder.SetPositionAndRotation(map.Terrain.transform.position, Quaternion.identity);
        int before = meshes.Count;
        int vertices = RaidLilyPads.Build(holder, pieces, padMeshes, MeshOf(rule.flower), MeshOf(rule.bud), material, meshes);
        int flowers = 0, buds = 0;
        foreach (RaidLilyPads.Piece piece in pieces)
        {
            flowers += piece.Kind == RaidLilyPads.Flower ? 1 : 0;
            buds += piece.Kind == RaidLilyPads.Bud ? 1 : 0;
        }

        return $"{pieces.Count - flowers - buds} lily pads, {flowers} flowers, {buds} buds in {meshes.Count - before} meshes of " +
               $"{vertices} vertices, {clock.ElapsedMilliseconds} ms";
    }

    private static Mesh MeshOf(GameObject prefab) =>
        prefab != null && prefab.GetComponentInChildren<MeshFilter>() is MeshFilter filter ? filter.sharedMesh : null;
}
