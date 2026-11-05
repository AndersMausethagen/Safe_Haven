using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rock on a terrain's steep ground as paint: the look's rocky ground on steep slopes and Cliff Rock on the steepest
/// drops, so a brush paints over them like any ground (World Tools › Terrain › Rock on Steep Ground). The terrain shader
/// draws both from three sides, with their relief, wherever they are painted (Rock From Painted Layers). Terrains that
/// are not painted by hand (generated raid maps) keep rock by slope, with the same two rocks.
/// </summary>
public static class TerrainRock
{
    public const string CliffRockPath = WorldAssets.LayerFolder + "/Cliff Rock/Cliff Rock.terrainlayer";

    public static TerrainLayer CliffRock => AssetDatabase.LoadAssetAtPath<TerrainLayer>(CliffRockPath);

    /// <summary>Gives every world terrain material the Cliff Rock textures for its steepest ground where it has none
    /// (otherwise the steepest ground would be plain grey). Returns how many changed.</summary>
    public static int EnsureSteepTextures()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material"))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null || material.shader.name != TerrainDisplacementEditor.ShaderName || material.GetTexture("_SteepAlbedo") != null)
            {
                continue;
            }

            UseLayerTextures(material, CliffRock, "_Steep");
            material.SetFloat("_SteepHeightMidpoint", TerrainDisplacementEditor.MeanHeight(material.GetTexture("_SteepMask")));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            changed++;
        }

        return changed;
    }

    /// <summary>Paints the look's rocky ground on the terrain's steep slopes and Cliff Rock on the steepest (blended over
    /// what is painted there, by the material's slope settings), marks both as rock and switches the material to rock from
    /// painted layers. Undoable paint; returns a summary.</summary>
    public static string PaintSteepGround(Terrain terrain, WorldLook look)
    {
        Material material = terrain.materialTemplate;
        if (material == null || material.shader.name != TerrainDisplacementEditor.ShaderName)
        {
            return "not a world terrain: use a Look first";
        }

        TerrainLayer rocky = look.RockGround;
        TerrainLayer cliff = CliffRock;
        if (rocky == null || cliff == null)
        {
            return "the rock grounds are missing";
        }

        int adding = new[] { rocky, cliff }.Count(l => !terrain.terrainData.terrainLayers.Contains(l));
        if (terrain.terrainData.terrainLayers.Length + adding > 16)
        {
            return "no room for two more ground layers (16 at most)";
        }

        Undo.RecordObject(material, "Rock on Steep Ground");
        UseLayerTextures(material, rocky, "_Cliff");
        UseLayerTextures(material, cliff, "_Steep");
        var rules = ScriptableObject.CreateInstance<TerrainPaintRules>();
        rules.Rules.Add(Rule(rocky, material.GetFloat("_CliffSlopeStart"), 0.25f, 6f));
        rules.Rules.Add(Rule(cliff, material.GetFloat("_SteepSlopeStart"), 0.2f, 5f));
        WorldPaint.Apply(terrain, rules);
        Object.DestroyImmediate(rules);

        TerrainSurface surface = terrain.GetComponent<TerrainSurface>();
        if (surface != null && surface.Profile != null)
        {
            MarkRock(surface.Profile);
        }

        material.SetFloat("_RockFromLayers", 1f);
        EditorUtility.SetDirty(material);
        TerrainDisplacementEditor.Apply(terrain); // layer data with the rock kinds, and the rocks' relief midpoints
        if (surface != null && surface.Profile != null)
        {
            MarkRock(surface.Profile); // new entries Apply just made
            TerrainDisplacementEditor.Apply(terrain);
        }

        return $"{rocky.name} on slopes past {material.GetFloat("_CliffSlopeStart"):0}°, {cliff.name} past {material.GetFloat("_SteepSlopeStart"):0}°; paint over them to change them";
    }

    private static void MarkRock(TerrainSurfaceProfile profile)
    {
        foreach (TerrainSurfaceProfile.Layer entry in profile.layers.Where(e => e != null && e.rock == TerrainSurfaceProfile.RockKind.None))
        {
            entry.rock = TerrainSurfaceProfile.RockOf(entry.terrainLayer);
        }

        EditorUtility.SetDirty(profile);
    }

    private static TerrainPaintRules.Rule Rule(TerrainLayer layer, float fromSlope, float patchiness, float softness) =>
        new TerrainPaintRules.Rule
        {
            layer = layer, strength = 1f, slope = new Vector2(fromSlope, 90f), softness = softness, patchiness = patchiness, patchSize = 6f,
        };

    // The material's rock textures are the painted layer's, so what is painted is what shows.
    private static void UseLayerTextures(Material material, TerrainLayer layer, string prefix)
    {
        material.SetTexture(prefix + "Albedo", layer.diffuseTexture);
        material.SetTexture(prefix + "Normal", layer.normalMapTexture);
        material.SetTexture(prefix + "Mask", layer.maskMapTexture);
        material.SetFloat(prefix + "Tiling", Mathf.Max(layer.tileSize.x, 1f));
    }
}
