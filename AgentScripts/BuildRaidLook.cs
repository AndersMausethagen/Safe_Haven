using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Brings the raid up to the world's graphical standard (`unity command script run --file AgentScripts/BuildRaidLook.cs --entry BuildRaidLook.Apply`):
/// the tessellated forest material, wind-animated trees, grass and plants grown on every generated map, terrain mist, moss, footprints
/// and the wind that moves it all. Takes what the prologue forest already tuned (same three ground layers, same size) and Haven's
/// moss and wind. Run from SafeHaven with nothing unsaved; it stays open. Safe to run again.
/// </summary>
public static class BuildRaidLook
{
    private const string RaidScene = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";
    private const string PrologueScene = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    private const string Vegetation = "Assets/_Project/Data/Prologue/Vegetation/";
    private const string ForestMaterial = "Assets/_Project/Art/Terrain/SafeHaven/MistyWoods Terrain Displacement.mat";

    public static string Apply()
    {
        Scene home = SceneManager.GetActiveScene();
        if (home.isDirty)
        {
            return "the open scene has unsaved changes";
        }

        var log = new List<string>();
        var definition = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:RaidMapDefinition")[0]));
        Material material = Material_();
        log.Add(Definition(definition, material));
        log.Add(MapTerrain());
        AssetDatabase.SaveAssets();
        log.Add(SceneDressing(home, definition, material));
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    // A copy, so the raid can be tuned apart from the prologue; the same layers, so the same displacement arrays fit. Haven's reach.
    private static Material Material_()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(ForestMaterial) == null)
        {
            AssetDatabase.CopyAsset(Vegetation + "Prologue Forest Terrain Displacement.mat", ForestMaterial);
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(ForestMaterial);
        var haven = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Terrain/SafeHaven/Haven World Terrain Displacement.mat");
        foreach (string property in new[] { "_DisplacementStrength", "_DisplacementNear", "_DisplacementFar", "_TessellationFactor", "_TessellationPixels", "_TessellationEdgeLength" })
        {
            material.SetFloat(property, haven.GetFloat(property));
        }

        // The prologue's ground has no cliffs to speak of; generated ridges reach 50 degrees, so they need Haven's cliff rock.
        Shader shader = material.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string property = shader.GetPropertyName(i);
            if (!property.StartsWith("_Cliff"))
            {
                continue;
            }

            switch (shader.GetPropertyType(i))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    material.SetTexture(property, haven.GetTexture(property));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    material.SetColor(property, haven.GetColor(property));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    material.SetVector(property, haven.GetVector(property));
                    break;
                default:
                    material.SetFloat(property, haven.GetFloat(property));
                    break;
            }
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static string Definition(RaidMapDefinition definition, Material material)
    {
        Color Average(TerrainLayer layer)
        {
            MethodInfo method = typeof(GrassSetup).GetMethod("AverageColour", BindingFlags.NonPublic | BindingFlags.Static);
            return (Color)method.Invoke(null, new object[] { layer });
        }

        var so = new SerializedObject(definition);
        so.FindProperty("terrainMaterial").objectReferenceValue = material;
        so.FindProperty("rocksInForest").floatValue = 0.12f;
        SerializedProperty trees = so.FindProperty("trees");
        int windy = 0;
        for (int i = 0; i < trees.arraySize; i++)
        {
            SerializedProperty prefab = trees.GetArrayElementAtIndex(i).FindPropertyRelative("prefab");
            if (prefab.objectReferenceValue == null || prefab.objectReferenceValue.name.EndsWith(" Wind"))
            {
                windy++;
                continue;
            }

            var wind = AssetDatabase.LoadAssetAtPath<GameObject>(Vegetation + prefab.objectReferenceValue.name + " Wind.prefab");
            if (wind != null)
            {
                prefab.objectReferenceValue = wind;
                windy++;
            }
        }

        // Forest floor: a thick lawn of moss-loving growth. Brown paths and rock stay bare: nothing grows once they show.
        (TerrainLayer layer, float shortGrass, float tall, float bushes, float plants, float bareAt)[] growth =
        {
            (definition.ForestLayer, 0.8f, 0.4f, 0.35f, 0.85f, 0f),
            (definition.PathLayer, 0f, 0f, 0f, 0f, 0.8f),
            (definition.CliffLayer, 0f, 0f, 0f, 0f, 0.45f),
        };
        SerializedProperty list = so.FindProperty("growth");
        list.arraySize = growth.Length;
        for (int i = 0; i < growth.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("layer").objectReferenceValue = growth[i].layer;
            entry.FindPropertyRelative("shortGrass").floatValue = growth[i].shortGrass;
            entry.FindPropertyRelative("tallGrass").floatValue = growth[i].tall;
            entry.FindPropertyRelative("bushes").floatValue = growth[i].bushes;
            entry.FindPropertyRelative("plants").floatValue = growth[i].plants;
            entry.FindPropertyRelative("bareAt").floatValue = growth[i].bareAt;
            entry.FindPropertyRelative("colour").colorValue = Average(growth[i].layer);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return $"map: {windy} of {trees.arraySize} trees with wind, tessellated material, growth by ground";
    }

    // The map's TerrainData is copied for every run, so what it holds is what each generated map starts from.
    private static string MapTerrain()
    {
        var map = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset");
        var forest = AssetDatabase.LoadAssetAtPath<TerrainData>(Vegetation + "Prologue Forest Terrain.asset");
        map.SetDetailResolution(forest.detailResolution, forest.detailResolutionPerPatch);
        map.detailPrototypes = forest.detailPrototypes;
        map.SetDetailScatterMode(forest.detailScatterMode);
        map.wavingGrassStrength = forest.wavingGrassStrength;
        map.wavingGrassAmount = forest.wavingGrassAmount;
        map.wavingGrassSpeed = forest.wavingGrassSpeed;
        map.wavingGrassTint = forest.wavingGrassTint;
        map.RefreshPrototypes();
        EditorUtility.SetDirty(map);
        return $"terrain: {map.detailPrototypes.Length} kinds of grass and plants, {map.detailScatterMode}";
    }

    private static string SceneDressing(Scene home, RaidMapDefinition definition, Material material)
    {
        Terrain havenTerrain = TerrainIn(home);
        GameObject havenWind = Root(home, "Forest Wind");
        Scene prologue = EditorSceneManager.OpenScene(PrologueScene, OpenSceneMode.Additive);
        Scene raid = EditorSceneManager.OpenScene(RaidScene, OpenSceneMode.Additive);
        try
        {
            Terrain from = TerrainIn(prologue);
            Terrain terrain = TerrainIn(raid);
            terrain.materialTemplate = material;
            terrain.drawInstanced = from.drawInstanced;

            Replace<GrassTerrainColour>(from, terrain);
            Replace<TerrainSurface>(from, terrain);
            Replace<TerrainFootprints>(from, terrain);
            Replace<TerrainMist>(from, terrain);
            TerrainMoss moss = Replace<TerrainMoss>(havenTerrain, terrain);
            var mossData = new SerializedObject(moss);
            mossData.FindProperty("mossLayer").objectReferenceValue = definition.ForestLayer;
            mossData.ApplyModifiedPropertiesWithoutUndo();
            terrain.GetComponent<GrassTerrainColour>().ColourMap = null;

            GameObject old = Root(raid, "Forest Wind");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            GameObject wind = Object.Instantiate(havenWind);
            wind.name = "Forest Wind";
            SceneManager.MoveGameObjectToScene(wind, raid);
            EditorSceneManager.MarkSceneDirty(raid);
            EditorSceneManager.SaveScene(raid);
        }
        finally
        {
            EditorSceneManager.CloseScene(raid, true);
            EditorSceneManager.CloseScene(prologue, true);
        }

        return "raid scene: mist, moss, surface, footprints, ground colour and wind";
    }

    private static T Replace<T>(Terrain from, Terrain to) where T : Component
    {
        T old = to.GetComponent<T>();
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        ComponentUtility.CopyComponent(from.GetComponent<T>());
        ComponentUtility.PasteComponentAsNew(to.gameObject);
        return to.GetComponent<T>();
    }

    private static Terrain TerrainIn(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Terrain terrain = root.GetComponentInChildren<Terrain>(true);
            if (terrain != null)
            {
                return terrain;
            }
        }

        throw new InvalidOperationException("No terrain in " + scene.name);
    }

    private static GameObject Root(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }
}
