using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Painting the ground with any layer, tessellated (World Tools › Terrain › Paint ground): picking a layer adds it to the
/// terrain if it is missing, rebuilds the terrain's displacement data so the new ground has its relief at once, and opens
/// Unity's Paint Texture brush with that layer chosen. Also gives each look a Terrain Tools layer palette (the Paint
/// Texture tool's "Layer Palette" adds a whole look at once) and its grass and plants as terrain details.
/// </summary>
public static class GroundLayerPainter
{
    public const string OriginalSet = "Original";
    private const string OriginalFolder = WorldAssets.LayerFolder;

    public static readonly string[] Sets = WorldLook.All.Select(l => l.Name).Append(OriginalSet).ToArray();

    /// <summary>The ground layers of a set: a look's, or the original World Builder grounds.</summary>
    public static IEnumerable<TerrainLayer> Layers(string set)
    {
        WorldLook look = WorldLook.All.FirstOrDefault(l => l.Name == set);
        if (look != null)
        {
            return look.Grounds;
        }

        return AssetDatabase.FindAssets("t:TerrainLayer", new[] { OriginalFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null).OrderBy(l => l.name);
    }

    /// <summary>Adds the layer to the terrain if missing (rebuilding its displacement), selects the terrain and opens
    /// the Paint Texture brush with the layer chosen.</summary>
    public static void PaintWith(Terrain terrain, TerrainLayer layer)
    {
        if (terrain == null || layer == null)
        {
            return;
        }

        TerrainData data = terrain.terrainData;
        if (!data.terrainLayers.Contains(layer))
        {
            if (data.terrainLayers.Length >= 16)
            {
                EditorUtility.DisplayDialog("Paint ground", "This terrain already has 16 ground layers, the most the tessellated terrain can blend. Remove one first.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(data, "Add Ground Layer");
            data.terrainLayers = data.terrainLayers.Append(layer).ToArray();
            EditorUtility.SetDirty(data);
            RefreshDisplacement(terrain);
        }

        Selection.activeGameObject = terrain.gameObject;
        // The terrain's Inspector exists only once the selection has drawn: try for a few seconds.
        double until = EditorApplication.timeSinceStartup + 3.0;
        void Try()
        {
            if (ChooseInBrush(layer) || EditorApplication.timeSinceStartup > until)
            {
                EditorApplication.update -= Try;
            }
        }

        EditorApplication.update += Try;
    }

    /// <summary>Rebuilds the terrain's displacement data when it uses the tessellated world terrain.</summary>
    public static bool RefreshDisplacement(Terrain terrain)
    {
        if (terrain.materialTemplate == null || terrain.materialTemplate.shader.name != TerrainDisplacementEditor.ShaderName ||
            string.IsNullOrEmpty(AssetDatabase.GetAssetPath(terrain.terrainData)) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return false;
        }

        try
        {
            TerrainDisplacementEditor.Apply(terrain);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Paint ground: the terrain's displacement could not be rebuilt: " + exception.Message, terrain);
            return false;
        }
    }

    // Unity has no public way to pick the terrain brush, so this is best effort: if Unity changes, the terrain is
    // still selected and the layer added, and the brush is picked by hand. True once done (or impossible).
    private static bool ChooseInBrush(TerrainLayer layer)
    {
        try
        {
            Type inspectorType = TypeCache.GetTypesDerivedFrom<Editor>().FirstOrDefault(t => t.FullName == "UnityEditor.TerrainInspector");
            Type toolType = inspectorType?.Assembly.GetType("UnityEditor.TerrainTools.PaintTextureTool");
            if (inspectorType == null || toolType == null)
            {
                return true;
            }

            const BindingFlags any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object inspector = inspectorType.GetField("s_activeTerrainInspectorInstance", any)?.GetValue(null);
            if (inspector == null)
            {
                return false; // not drawn yet
            }

            inspectorType.GetMethod("SelectToolByType", any, null, new[] { typeof(Type) }, null)?.Invoke(inspector, new object[] { toolType });
            object tool = inspectorType.GetField("m_ActiveTerrainTool", any)?.GetValue(null);
            if (tool == null)
            {
                return true;
            }

            MethodInfo set = tool.GetType().GetMethod("SetSelectedTerrainLayer", any, null, new[] { typeof(TerrainLayer) }, null);
            if (set != null)
            {
                set.Invoke(tool, new object[] { layer });
            }
            else
            {
                tool.GetType().GetField("m_SelectedTerrainLayer", any)?.SetValue(tool, layer);
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        catch (Exception exception)
        {
            Debug.Log("Paint ground: pick the Paint Texture brush and the layer by hand (" + exception.Message + ").");
        }

        return true;
    }

    // ------------------------------------------------------------------ layer palettes and plants

    /// <summary>A Terrain Tools layer palette per look ("Forest Lake Ground", "Mossy Forest Ground") in its data folder,
    /// for the Paint Texture tool's Layer Palette. Returns how many were made (none without Terrain Tools).</summary>
    public static int EnsureLayerPalettes()
    {
        Type paletteType = TypeCache.GetTypesDerivedFrom<ScriptableObject>().FirstOrDefault(t => t.FullName == "UnityEngine.TerrainTools.TerrainPalette");
        FieldInfo layersField = paletteType?.GetField("PaletteLayers");
        if (layersField == null)
        {
            return 0;
        }

        int made = 0;
        foreach (WorldLook look in WorldLook.All)
        {
            string path = $"{look.DataFolder}/{look.Name} Ground.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || !AssetDatabase.IsValidFolder(look.GroundFolder))
            {
                continue;
            }

            System.IO.Directory.CreateDirectory(look.DataFolder);
            ScriptableObject palette = ScriptableObject.CreateInstance(paletteType);
            layersField.SetValue(palette, look.Grounds.ToList());
            AssetDatabase.CreateAsset(palette, path);
            made++;
        }

        return made;
    }

    /// <summary>Gives the terrain a look's grass, moss and small plants as detail kinds to paint (those it lacks).
    /// Returns how many were added.</summary>
    public static int AddDetails(Terrain terrain, WorldLook look)
    {
        TerrainData data = terrain.terrainData;
        var prototypes = data.detailPrototypes.ToList();
        int added = 0;
        foreach (GameObject prefab in look.Details)
        {
            if (prototypes.Any(p => p.prototype == prefab))
            {
                continue;
            }

            DetailPrototype prototype = Detail(prefab);
            if (prototype.Validate(out string error))
            {
                prototypes.Add(prototype);
                added++;
            }
            else
            {
                Debug.LogWarning($"Paint ground: {prefab.name} cannot be a terrain detail: {error}", prefab);
            }
        }

        if (added > 0)
        {
            Undo.RegisterCompleteObjectUndo(data, "Add Plants");
            data.detailPrototypes = prototypes.ToArray();
            GrassSetup.UseInstanceCounts(data);
            data.RefreshPrototypes();
            EditorUtility.SetDirty(data);
        }

        return added;
    }

    /// <summary>A plant as a terrain detail, sized and spread as in the converted forests.</summary>
    public static DetailPrototype Detail(GameObject prefab)
    {
        string name = prefab.name;
        bool bush = name == "Bush";
        bool low = name == "Short Grass" || name.Contains("Moss") || name.Contains("Clover") || name.Contains("Litter") || name.Contains("Mushroom");
        return new DetailPrototype
        {
            usePrototypeMesh = true,
            prototype = prefab,
            useInstancing = true,
            renderMode = DetailRenderMode.VertexLit,
            minWidth = bush ? 0.7f : low ? 0.8f : 0.75f,
            maxWidth = bush ? 1.3f : low ? 1.4f : 1.25f,
            minHeight = bush ? 0.7f : low ? 0.8f : 0.75f,
            maxHeight = bush ? 1.3f : low ? 1.3f : 1.25f,
            noiseSpread = 0.35f,
            density = bush ? 0.3f : name.StartsWith("Glow") ? 0.6f : 1f,
            targetCoverage = name == "Short Grass" ? 2f : name == "Tall Grass" ? 1.4f : 1f,
            alignToGround = low ? 0.9f : bush ? 0.2f : 0.6f,
            positionJitter = 0.8f,
            useDensityScaling = true,
        };
    }
}

/// <summary>
/// Keeps a tessellated terrain's displacement data in step with its ground layers: when a layer is added, removed or
/// reordered by any means (Unity's Paint Texture tool, a layer palette, a script), the data is rebuilt, so new ground
/// is displaced at once. Only changes made while the terrain is open count; opening a scene never rewrites it.
/// </summary>
[InitializeOnLoad]
public static class TerrainLayerWatcher
{
    private static readonly Dictionary<Terrain, int> seen = new Dictionary<Terrain, int>();
    private static double nextCheck;

    static TerrainLayerWatcher()
    {
        EditorApplication.update += Check;
    }

    private static void Check()
    {
        if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            int signature = Signature(terrain.terrainData);
            if (seen.TryGetValue(terrain, out int last) && last != signature && Uses(terrain))
            {
                if (GroundLayerPainter.RefreshDisplacement(terrain))
                {
                    Debug.Log($"{terrain.name}: ground layers changed, displacement rebuilt for {terrain.terrainData.terrainLayers.Length} layer(s).", terrain);
                }
            }

            seen[terrain] = signature;
        }

        foreach (Terrain gone in seen.Keys.Where(t => t == null).ToList())
        {
            seen.Remove(gone);
        }
    }

    private static bool Uses(Terrain terrain) =>
        terrain.materialTemplate != null && terrain.materialTemplate.shader.name == TerrainDisplacementEditor.ShaderName &&
        terrain.materialTemplate.HasProperty("_DisplacementLayerCount") && terrain.materialTemplate.GetFloat("_DisplacementLayerCount") > 0;

    private static int Signature(TerrainData data)
    {
        unchecked
        {
            int hash = 17;
            foreach (TerrainLayer layer in data.terrainLayers)
            {
                hash = hash * 31 + (layer != null ? layer.GetEntityId().GetHashCode() : 0);
            }

            return hash;
        }
    }
}
