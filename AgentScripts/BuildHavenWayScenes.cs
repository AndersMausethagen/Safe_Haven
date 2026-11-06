using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What the Haven world's woods have along the way (`unity command script run --file AgentScripts/BuildHavenWayScenes.cs
/// --entry BuildHavenWayScenes.Apply`): small scenes beside the paths, a cairn for trail junctions, what lies about each kind of
/// glade, and the stones lining ravine lips, all composed from the Misty Woods' and the Haven's own scanned rocks and logs
/// (none of them mineable, so scenery adds no resources). Saved under Prefabs/World/Along the Way and set on the recipe; the
/// player prefab gets its character fill light. Re-runnable: it rebuilds the prefabs in place.
/// </summary>
public static class BuildHavenWayScenes
{
    private const string Folder = "Assets/_Project/Prefabs/World/Along the Way";
    private const string Woods = "Assets/_Project/Art/Vegetation/Misty Woods/";
    private const string Haven = "Assets/_Project/Art/Vegetation/Safe Haven/";
    private const string World = "Assets/_Project/Art/Vegetation/Haven World/";
    private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";

    public static string Apply()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs/World", "Along the Way");
        }

        var scenes = new List<GameObject>
        {
            Scene("Fallen Giant",
                (Woods + "Rocks and Logs/Fallen Cedar B", new Vector3(0f, 0f, 0f), new Vector3(0f, 70f, 0f), 1f),
                (Woods + "Rocks and Logs/Snag Stump", new Vector3(-3.2f, 0f, -1.4f), Vector3.zero, 1.2f),
                (Woods + "Undergrowth/Branch Pile", new Vector3(2.6f, 0f, 1.9f), new Vector3(0f, 30f, 0f), 1f),
                (Woods + "Undergrowth/Short Branches", new Vector3(0.8f, 0f, -1.6f), new Vector3(0f, 110f, 0f), 1.4f)),
            Scene("Log Stack",
                (Woods + "Rocks and Logs/Rotten Log B", new Vector3(0f, 0f, 0f), Vector3.zero, 1f),
                (Woods + "Rocks and Logs/Rotten Log B", new Vector3(0.2f, 0f, 1.1f), new Vector3(0f, 177f, 0f), 1f),
                (Woods + "Rocks and Logs/Mossy Log", new Vector3(0.1f, 0.55f, 0.55f), new Vector3(0f, 3f, 0f), 1f),
                (Woods + "Rocks and Logs/Stump A", new Vector3(2.9f, 0f, -1.3f), Vector3.zero, 1f),
                (Woods + "Undergrowth/Twig Pile", new Vector3(-2.4f, 0f, -1.1f), new Vector3(0f, 40f, 0f), 1f)),
            // A boulder stood on end: a waystone someone set there long ago, its feet sunk in moss and stones.
            Scene("Waystone",
                (World + "Rocks/Mountain Boulder", new Vector3(0f, 1.45f, 0f), new Vector3(0f, 0f, 90f), 1f),
                (Woods + "Undergrowth/Mossy Stones", new Vector3(1.3f, 0f, 0.6f), new Vector3(0f, 20f, 0f), 1.2f),
                (Woods + "Undergrowth/Mossy Stones", new Vector3(-1.1f, 0f, 0.9f), new Vector3(0f, 140f, 0f), 1f),
                (Woods + "Undergrowth/Mossy Rock Bed", new Vector3(0.2f, 0f, -1.2f), new Vector3(0f, 70f, 0f), 1f),
                (Haven + "Rocks/Lake Stones A", new Vector3(-1.4f, 0f, -0.8f), Vector3.zero, 1f)),
            Scene("Hunter's Rest",
                (World + "Props/Stone Fire Pit", Vector3.zero, Vector3.zero, 1f),
                (Woods + "Rocks and Logs/Mossy Log", new Vector3(0f, 0f, -1.9f), new Vector3(0f, 8f, 0f), 1f),
                (Woods + "Rocks and Logs/Stump B", new Vector3(1.7f, 0f, 1.0f), Vector3.zero, 0.8f),
                (Woods + "Undergrowth/Twig Pile", new Vector3(-1.6f, 0f, 1.2f), new Vector3(0f, 15f, 0f), 1f),
                (Woods + "Undergrowth/Branch Pile", new Vector3(-2.2f, 0f, -1.2f), new Vector3(0f, 80f, 0f), 0.8f)),
            Scene("Mossy Rocks",
                (Haven + "Rocks/Haven Mossy Boulder", Vector3.zero, new Vector3(0f, 25f, 0f), 1.2f),
                (Woods + "Undergrowth/Mossy Rock Bed", new Vector3(1.6f, 0f, 0.7f), new Vector3(0f, 60f, 0f), 1f),
                (Woods + "Undergrowth/Mossy Stones", new Vector3(-1.4f, 0f, 0.9f), Vector3.zero, 1.1f),
                (Woods + "Undergrowth/Mossy Stones", new Vector3(0.6f, 0f, -1.4f), new Vector3(0f, 200f, 0f), 0.9f),
                (Woods + "Undergrowth/Forest Floor Patch A", new Vector3(-0.4f, 0f, -0.2f), new Vector3(0f, 90f, 0f), 1.2f)),
        };

        // Flat slabs stacked smaller and smaller, each a little turned: read at a glance from above as made by hand.
        GameObject cairn = Scene("Cairn",
            (World + "Rocks/Mountain Slab", new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), 0.5f),
            (World + "Rocks/Mountain Slab", new Vector3(0.05f, 0.27f, 0f), new Vector3(0f, 35f, 0f), 0.4f),
            (World + "Rocks/Mountain Slab", new Vector3(-0.03f, 0.49f, 0.02f), new Vector3(0f, 80f, 0f), 0.31f),
            (World + "Rocks/Mountain Slab", new Vector3(0.02f, 0.66f, -0.02f), new Vector3(0f, 125f, 0f), 0.23f),
            (Woods + "Undergrowth/Mossy Stones", new Vector3(0.7f, 0f, 0.3f), Vector3.zero, 0.8f));

        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        recipe.trailScenes = scenes;
        recipe.junctionMarker = cairn;
        recipe.rimStones = Prefabs(Woods + "Undergrowth/Mossy Stones", Haven + "Rocks/Lake Stones B", Haven + "Rocks/Haven Mossy Boulder",
                                   Woods + "Undergrowth/Scree", Woods + "Undergrowth/Mossy Rock Bed");
        recipe.lairDressing = Prefabs(Woods + "Undergrowth/Rotten Branch", Woods + "Undergrowth/Twig Pile", Woods + "Undergrowth/Short Branches",
                                      Woods + "Undergrowth/Forest Floor Patch A", Woods + "Undergrowth/Forest Floor Patch B", Woods + "Undergrowth/Forest Floor Patch C",
                                      Woods + "Rocks and Logs/Snag Stump", Woods + "Undergrowth/Lichen Branch", Woods + "Undergrowth/Branch Pile",
                                      Woods + "Undergrowth/Mossy Branch Log");
        recipe.rockyDressing = Prefabs(Woods + "Undergrowth/Ground Slab", Woods + "Undergrowth/Mossy Rock Bed", Woods + "Undergrowth/Scree",
                                       Haven + "Rocks/Haven Mossy Boulder", Woods + "Rocks and Logs/Cliff Boulder C", World + "Rocks/Mountain Slab",
                                       Woods + "Undergrowth/Mossy Stones");
        recipe.meadowDressing = Prefabs(Haven + "Plants/Haven Fern", Haven + "Plants/Lake Fern A", Haven + "Plants/Lake Fern B",
                                        Woods + "Rocks and Logs/Stump C", Woods + "Rocks and Logs/Mossy Log", Haven + "Rocks/Haven Mossy Stone",
                                        Haven + "Rocks/Haven Fallen Branch");
        recipe.wetDressing = Prefabs(Haven + "Plants/Lake Fern A", Haven + "Plants/Lake Fern B", Haven + "Plants/Lake Fern C",
                                     Haven + "Plants/Lake Grass Clump", Haven + "Rocks/Lake Stones A", Haven + "Rocks/Lake Stones B",
                                     Woods + "Rocks and Logs/Rotten Log C", Woods + "Rocks and Logs/Mossy Rotten Log");
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssetIfDirty(recipe);

        string light = AddFillLight();
        return $"{scenes.Count} trail scenes and a cairn in {Folder}; recipe: {recipe.rimStones.Count} rim stones, glade dressing " +
               $"{recipe.lairDressing.Count}/{recipe.rockyDressing.Count}/{recipe.meadowDressing.Count}/{recipe.wetDressing.Count}; {light}";
    }

    private static GameObject Scene(string name, params (string prefab, Vector3 at, Vector3 euler, float scale)[] parts)
    {
        var root = new GameObject(name);
        try
        {
            foreach ((string prefab, Vector3 at, Vector3 euler, float scale) in parts)
            {
                GameObject source = Load(prefab);
                var made = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                made.transform.localPosition = at;
                made.transform.localRotation = Quaternion.Euler(euler);
                made.transform.localScale = Vector3.one * scale;
            }

            return PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static List<GameObject> Prefabs(params string[] paths)
    {
        var list = new List<GameObject>();
        foreach (string path in paths)
        {
            list.Add(Load(path));
        }

        return list;
    }

    private static GameObject Load(string path) =>
        AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab") ?? throw new FileNotFoundException("No prefab at " + path);

    private static string AddFillLight()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            if (root.GetComponent<CharacterFillLight>() != null)
            {
                return "player already has its fill light";
            }

            root.AddComponent<CharacterFillLight>();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            return "fill light added to the player";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
