using UnityEngine;

/// <summary>
/// Terrain data changed during play lives on a runtime copy, never the scene's asset, and every system that changes it (the
/// moss bloom, felling, harvesting) shares the one copy: each made its own under its own name and didn't know the others',
/// so the Haven's terrain (43 MB, 130k trees) was copied again mid-play on the first chop after the moss bloomed, the older copy
/// kept for the session, and a raid's generated terrain was copied on the first knock (review 2026-11-16).
/// <para>A copy made here is named with <see cref="Suffix"/>; a raid map's own copies ("(turned)", "(generated)") count too.</para>
/// </summary>
public static class RuntimeTerrain
{
    public const string Suffix = " (runtime)";

    /// <summary>True for terrain data that is already a copy made for play, which may be changed as it is.</summary>
    public static bool IsCopy(TerrainData data) =>
        data != null && (data.name.EndsWith(Suffix) || data.name.EndsWith("(turned)") || data.name.EndsWith("(generated)"));

    /// <summary>
    /// Puts <paramref name="terrain"/> (and its collider) on a runtime copy of its data unless it already draws from one. Returns
    /// the copy made now, for its maker to destroy with the scene, or null when none was needed.
    /// </summary>
    public static TerrainData Own(Terrain terrain)
    {
        TerrainData data = terrain != null ? terrain.terrainData : null;
        if (data == null || IsCopy(data))
        {
            return null;
        }

        TerrainData copy = Object.Instantiate(data);
        copy.name = data.name + Suffix;
        terrain.terrainData = copy;
        if (terrain.TryGetComponent(out TerrainCollider ground))
        {
            ground.terrainData = copy;
        }

        return copy;
    }
}
