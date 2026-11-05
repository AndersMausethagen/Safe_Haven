using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reimporting a TerrainData asset (a save after painting or sculpting, a git checkout, an asset refresh) makes Unity
/// drop the terrain's runtime splat property block and reset its patch bounds, while <see cref="TerrainSurface"/> stays
/// enabled and never hears of it. The displacement then loses its control textures and the terrain renders flat until
/// the next domain reload. This re-applies the surface of every terrain whose data was just imported.
/// </summary>
internal sealed class TerrainSurfaceReimport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Length == 0) return;
        foreach (TerrainSurface surface in UnityEngine.Object.FindObjectsByType<TerrainSurface>())
        {
            TerrainData data = surface.TryGetComponent(out Terrain terrain) ? terrain.terrainData : null;
            if (data == null) continue;
            if (Array.IndexOf(imported, AssetDatabase.GetAssetPath(data)) >= 0) surface.Apply();
        }
    }
}
