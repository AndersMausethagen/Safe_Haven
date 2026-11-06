using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Haven world's recipe (`unity command script run --file AgentScripts/BuildHavenWorldRecipe.cs --entry BuildHavenWorldRecipe.Apply`): rings
/// from the portal out into the Misty Woods, a lake with islands to the east, a mountain to the north-west, the raids' forest for the
/// woods, points of interest from the raids' set pieces, creatures by distance (no bosses) and resources by distance, rising in tier.
/// Rewrites the recipe asset; Safe Haven › World › Generate Haven World (HavenWorldGenerator) turns it into the world.
/// </summary>
public static class BuildHavenWorldRecipe
{
    private const string Woods = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const string SetPieces = "Assets/_Project/Prefabs/Raids/SetPieces/";

    public static string Apply()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<HavenWorldRecipe>();
            AssetDatabase.CreateAsset(recipe, HavenWorldGenerator.RecipePath);
        }

        recipe.woods = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(Woods);
        recipe.pointsOfInterest = new List<HavenPointOfInterest>
        {
            Poi("Ruined Watchtower", "Ruined Watchtower", 240f, 430f, 2, 12f, high: true),
            Poi("Standing Stones", "Standing Stones", 200f, 380f, 1, 12f, high: true),
            Poi("Island Stones", "Standing Stones", 0f, 0f, 1, 8f, island: true),
            Poi("Old Barrow", "Old Barrow", 300f, 450f, 1, 12f),
            Poi("Burnt Farmstead", "Burnt Farmstead", 210f, 340f, 1, 14f),
            Poi("Wolf Den", "Wolf Den", 330f, 460f, 1, 12f),
            // Built by BuildHavenWorldProps from a CC0 fire pit and the woods' logs.
            Poi("Woodland Camp", "Woodland Camp", 170f, 330f, 2, 9f, folder: "Assets/_Project/Prefabs/World/"),
        };
        // Lone big trees in some glades, so each reads as its own place from above; the maple's colour stands out among the cedars.
        recipe.gladeLandmarks = new List<GameObject>();
        foreach (string tree in new[] { "Misty Woods/Trees/Cedar Giant A", "Misty Woods/Trees/Cedar Giant B", "Misty Woods/Trees/Dead Cedar", "Safe Haven/Trees/Sugar Maple B" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Art/Vegetation/{tree}.prefab");
            if (prefab != null)
            {
                recipe.gladeLandmarks.Add(prefab);
            }
        }

        recipe.creatures = new List<HavenCreatureBand>
        {
            Band("Edge of the woods", 200f, 300f, 5, new Vector2Int(1, 2), ("ForestBittenWolf", 2f), ("BlightBoar", 1f), ("Rootling", 1f)),
            Band("Misty Woods", 300f, 400f, 7, new Vector2Int(2, 3), ("ForestBittenWolf", 2f), ("BlightBoar", 1.5f), ("BogSpitter", 1f), ("MossShaman", 0.6f), ("Mistwing", 0.8f)),
            Band("Deep woods", 390f, 480f, 8, new Vector2Int(2, 4), ("SmallMossTroll", 0.8f), ("Draug", 1f), ("BarrowShieldbearer", 0.8f), ("MarshLight", 0.6f), ("Huldra", 0.4f), ("BogSpitter", 1f)),
        };
        recipe.resources = new List<HavenResourceBand>
        {
            Resource("Loose stone", 80f, 260f, ResourceNodeKind.Rock, 1, 1, 10, 2, 3),
            Resource("Forage", 80f, 280f, ResourceNodeKind.Forage, 0, 1, 12, 2, 4),
            Resource("Fibre plants", 150f, 400f, ResourceNodeKind.Plant, 1, 2, 12, 2, 4),
            Resource("Stone and copper", 250f, 400f, ResourceNodeKind.Rock, 1, 2, 16, 2, 4),
            Resource("Hard wood", 300f, 480f, ResourceNodeKind.Wood, 2, 3, 14, 1, 3),
            Resource("Deep forage", 300f, 480f, ResourceNodeKind.Forage, 1, 3, 12, 2, 4),
            Resource("Iron", 380f, 480f, ResourceNodeKind.Rock, 2, 3, 18, 2, 4),
            Resource("Mountain ore", 260f, 480f, ResourceNodeKind.Rock, 2, 4, 16, 3, 5, mountain: true),
        };
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssetIfDirty(recipe);
        return $"Haven World Recipe: {recipe.pointsOfInterest.Count} kinds of place, {recipe.creatures.Count} creature bands, {recipe.resources.Count} resource bands.";
    }

    private static HavenPointOfInterest Poi(string name, string prefab, float from, float to, int count, float clearing, bool high = false, bool island = false,
                                            string folder = SetPieces) =>
        new HavenPointOfInterest
        {
            name = name,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + prefab + ".prefab"),
            distance = new Vector2(from, to),
            count = count,
            clearing = clearing,
            high = high,
            onIsland = island,
            path = !island,
        };

    private static HavenCreatureBand Band(string name, float from, float to, int zones, Vector2Int group, params (string creature, float weight)[] creatures)
    {
        var band = new HavenCreatureBand { name = name, distance = new Vector2(from, to), zones = zones, group = group, respawnSeconds = 600f };
        foreach ((string creature, float weight) in creatures)
        {
            MonsterDefinition definition = null;
            foreach (MonsterDefinition one in CheatSettings.Current.Creatures)
            {
                if (one != null && one.name == creature)
                {
                    definition = one;
                }
            }

            if (definition != null && definition.Prefab != null)
            {
                band.creatures.Add(new SpawnTable.Entry { prefab = definition.Prefab, weight = weight });
            }
        }

        return band;
    }

    private static HavenResourceBand Resource(string name, float from, float to, ResourceNodeKind kind, int lowTier, int highTier, int clusters, int least, int most, bool mountain = false) =>
        new HavenResourceBand
        {
            name = name,
            distance = new Vector2(from, to),
            kind = kind,
            tiers = new Vector2Int(lowTier, highTier),
            clusters = clusters,
            perCluster = new Vector2Int(least, most),
            mountainOnly = mountain,
        };
}
