using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Haven world's recipe (`unity command script run --file AgentScripts/BuildHavenWorldRecipe.cs --entry BuildHavenWorldRecipe.Apply`):
/// the Haven's meadows in the middle and terraces climbing away from them, a lake with islands to the east, a mountain to the
/// north-west, a bog to the south, a tarn and a mere, the raids' forest for the woods, places (the raids' set pieces and
/// BuildHavenWorldPlaces' own), creatures by distance (no bosses), game to hunt, and resources by distance, rising in tier.
/// Rewrites the recipe asset's numbers and lists (run BuildHavenWorldProps, BuildHavenWayScenes and BuildHavenWorldPlaces
/// first: they make the prefabs it points at). Safe Haven › World › Haven World Planner shows what it plans; Generate Haven
/// World (HavenWorldGenerator) turns it into the world.
/// </summary>
public static class BuildHavenWorldRecipe
{
    private const string Woods = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const string SetPieces = "Assets/_Project/Prefabs/Raids/SetPieces/";
    private const string World = "Assets/_Project/Prefabs/World/";
    private const string Places = "Assets/_Project/Prefabs/World/Places/";
    private const string Way = "Assets/_Project/Prefabs/World/Along the Way/";

    public static string Apply()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<HavenWorldRecipe>();
            AssetDatabase.CreateAsset(recipe, HavenWorldGenerator.RecipePath);
        }

        recipe.woods = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(Woods);

        // The land: flat benches with cliffs between, read from above. About three cliffs on a walk straight out, wandering into
        // bays and table rocks; the roads run in valleys, the benches above them are rooms a trail climbs to.
        recipe.terraceStep = 7f;
        recipe.terraceRise = 21f;
        recipe.terraceWander = 10f;
        recipe.terraceScale = 180f;
        recipe.benchRoll = 0.3f;
        recipe.riserWidth = 3.5f;
        recipe.rampLength = 30f;
        recipe.roadValley = 0.9f;
        recipe.roadValleyWidth = 40f;
        recipe.scrambleSpacing = new Vector2(90f, 150f);
        recipe.scrambleRadius = 8f;
        recipe.cliffMinLength = 24f;
        recipe.cliffRockNear = 30f;

        // Water: two rivers of different widths, streams out of the meres, the bog and springs, a tarn in the northern hills and a
        // mere in the south-western woods, each with islands, and the bog to the south.
        recipe.rivers = new Vector2Int(2, 2);
        recipe.riverWidth = new Vector2(4.5f, 6f);
        recipe.streams = new Vector2Int(5, 6);
        recipe.streamWidth = new Vector2(1.6f, 2.6f);
        recipe.ponds = new Vector2Int(4, 6);
        recipe.meres = new List<HavenMere>
        {
            new HavenMere { name = "Tarn", at = new Vector2(25f, 335f), size = new Vector2(36f, 25f), depth = 2.5f, islands = new Vector2Int(1, 1) },
            new HavenMere { name = "Mere", at = new Vector2(248f, 310f), size = new Vector2(54f, 37f), depth = 3.5f, islands = new Vector2Int(2, 3) },
        };
        recipe.bogAt = new Vector2(195f, 345f);
        recipe.bogRadius = 95f;
        recipe.bogWater = 0.38f;
        recipe.bogWoods = 0.22f;

        // Fewer ravines than before: the cliffs do their work now.
        recipe.outcrops = new Vector2Int(7, 10);
        recipe.ravines = new Vector2Int(4, 6);

        // Rooms and the ways between them: more glades, nearly all with a second way out, and places that lead on.
        recipe.glades = new Vector2Int(30, 36);
        recipe.gladeRadius = new Vector2(13f, 28f);
        recipe.gladeLinks = 0.8f;
        recipe.placeLinks = 0.65f;
        recipe.groves = new Vector2Int(6, 8);
        recipe.trailSceneSpacing = new Vector2(45f, 70f);
        recipe.longestDullWalk = 90f;

        recipe.woodsMist = 0.55f;
        recipe.bogMist = 1f;
        recipe.gladeMist = 0.35f;

        recipe.pointsOfInterest = new List<HavenPointOfInterest>
        {
            // No road ends at nothing: the first rule to ask takes each road's end.
            Poi("Wayfarer's Stones", "Wayfarer's Stones", 0f, 0f, 4, 10f, folder: Places, roadEnd: true, rewards: 3, reward: ResourceNodeKind.Forage, tiers: new Vector2Int(1, 3)),
            Poi("Island Stones", "Standing Stones", 0f, 0f, 1, 8f, island: true),
            Poi("Drowned Stones", "Drowned Stones", 0f, 0f, 1, 10f, folder: Places, bog: true, rewards: 4, reward: ResourceNodeKind.Plant, tiers: new Vector2Int(2, 3)),
            Poi("Fisher's Rest", "Fisher's Rest", 0f, 0f, 2, 8f, folder: Places, shore: true, rewards: 2, reward: ResourceNodeKind.Forage, tiers: new Vector2Int(0, 1)),
            // In the Haven's own meadows, a short walk from home.
            Poi("Mother Maple", "Mother Maple", 105f, 175f, 1, 10f, folder: Places, meadows: true, rewards: 3, reward: ResourceNodeKind.Forage, tiers: new Vector2Int(0, 1)),
            Poi("Shepherd's Fold", "Shepherd's Fold", 110f, 190f, 2, 8f, folder: Places, meadows: true, rewards: 2, reward: ResourceNodeKind.Plant, tiers: new Vector2Int(1, 1)),
            Poi("Ruined Watchtower", "Ruined Watchtower", 240f, 430f, 2, 12f, high: true),
            Poi("Standing Stones", "Standing Stones", 200f, 380f, 1, 12f, high: true),
            Poi("Lookout Cairn", "Lookout Cairn", 230f, 480f, 3, 7f, folder: Places, high: true),
            Poi("Old Barrow", "Old Barrow", 300f, 480f, 1, 12f),
            Poi("Burnt Farmstead", "Burnt Farmstead", 210f, 340f, 1, 14f),
            Poi("Wolf Den", "Wolf Den", 330f, 480f, 1, 12f),
            Poi("Old Quarry", "Old Quarry", 250f, 480f, 2, 10f, folder: Places, rewards: 4, reward: ResourceNodeKind.Rock, tiers: new Vector2Int(2, 3)),
            Poi("Elder Cedar", "Elder Cedar", 280f, 480f, 2, 10f, folder: Places, rewards: 3, reward: ResourceNodeKind.Forage, tiers: new Vector2Int(2, 3)),
            // Built by BuildHavenWorldProps from a CC0 fire pit and the woods' logs.
            Poi("Woodland Camp", "Woodland Camp", 170f, 480f, 3, 9f, folder: World),
        };

        recipe.meadowScenes = new List<GameObject>();
        foreach (string scene in new[] { "Boundary Stone", "Maple Rest", "Field Stones", "Old Stump and Saplings" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Way + scene + ".prefab");
            if (prefab != null)
            {
                recipe.meadowScenes.Add(prefab);
            }
        }

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

        // Most glades get one: tall silhouettes are how a top-down wood is found again (giant cedars, a dead cedar, a maple).
        recipe.gladeLandmarkChance = 0.7f;

        recipe.creatures = new List<HavenCreatureBand>
        {
            // Game: to hunt (and one day tame). In the Haven's meadows and the open glades, back by day as well as by night.
            // (Made by BuildGame: run it first.)
            Band("Meadow game", 105f, 200f, 5, new Vector2Int(2, 3), true, false, 420f, ("Deer", 3f), ("Stag", 0.6f), ("Fox", 0.8f)),
            Band("Woodland game", 200f, 340f, 7, new Vector2Int(2, 4), true, false, 480f, ("Deer", 2f), ("Stag", 1f), ("Fox", 0.8f), ("WildBoar", 1.2f)),
            Band("Deep game", 340f, 480f, 6, new Vector2Int(2, 4), true, false, 540f, ("Stag", 1.5f), ("WildBoar", 1.5f), ("Deer", 1f)),
            Band("Edge of the woods", 200f, 300f, 5, new Vector2Int(1, 2), false, false, 600f, ("ForestBittenWolf", 2f), ("BlightBoar", 1f), ("Rootling", 1f)),
            Band("Misty Woods", 300f, 400f, 8, new Vector2Int(2, 3), false, false, 600f, ("ForestBittenWolf", 2f), ("BlightBoar", 1.5f), ("BogSpitter", 1f), ("MossShaman", 0.6f), ("Mistwing", 0.8f)),
            Band("Deep woods", 390f, 480f, 12, new Vector2Int(2, 4), false, false, 600f, ("SmallMossTroll", 0.8f), ("Draug", 1f), ("BarrowShieldbearer", 0.8f), ("MarshLight", 0.6f), ("Huldra", 0.4f), ("BogSpitter", 1f)),
            Band("Bog", 0f, 0f, 4, new Vector2Int(2, 3), false, true, 600f, ("BogSpitter", 2f), ("MarshLight", 1.5f), ("Draug", 0.6f)),
        };
        recipe.resources = new List<HavenResourceBand>
        {
            Resource("Loose stone", 80f, 260f, ResourceNodeKind.Rock, 1, 1, 10, 2, 3),
            Resource("Forage", 80f, 280f, ResourceNodeKind.Forage, 0, 1, 14, 2, 4),
            Resource("Fibre plants", 150f, 400f, ResourceNodeKind.Plant, 1, 2, 12, 2, 4),
            Resource("Stone and copper", 250f, 400f, ResourceNodeKind.Rock, 1, 2, 16, 2, 4),
            Resource("Hard wood", 300f, 480f, ResourceNodeKind.Wood, 2, 3, 18, 1, 3),
            Resource("Deep forage", 300f, 480f, ResourceNodeKind.Forage, 1, 3, 16, 2, 4),
            Resource("Iron", 380f, 480f, ResourceNodeKind.Rock, 2, 3, 22, 2, 4),
            Resource("Mountain ore", 260f, 480f, ResourceNodeKind.Rock, 2, 4, 16, 3, 5, mountain: true),
            Resource("Bog plants", 0f, 0f, ResourceNodeKind.Plant, 1, 3, 7, 2, 4, bog: true),
            Resource("Bog forage", 0f, 0f, ResourceNodeKind.Forage, 1, 3, 6, 2, 4, bog: true),
        };
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssetIfDirty(recipe);
        int missing = recipe.pointsOfInterest.FindAll(poi => poi.prefab == null).Count;
        int empty = recipe.creatures.FindAll(band => band.creatures.Count == 0).Count;
        return $"Haven World Recipe: {recipe.pointsOfInterest.Count} kinds of place ({missing} without a prefab), {recipe.creatures.Count} creature bands " +
               $"({empty} with no creature found), {recipe.resources.Count} resource bands, {recipe.meres.Count} meres, {recipe.meadowScenes.Count} meadow scenes.";
    }

    private static HavenPointOfInterest Poi(string name, string prefab, float from, float to, int count, float clearing, bool high = false, bool island = false,
                                            string folder = SetPieces, bool roadEnd = false, bool bog = false, bool shore = false, bool meadows = false,
                                            int rewards = 0, ResourceNodeKind reward = ResourceNodeKind.Forage, Vector2Int tiers = default) =>
        new HavenPointOfInterest
        {
            name = name,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + prefab + ".prefab"),
            distance = new Vector2(from, to),
            count = count,
            clearing = clearing,
            high = high,
            onIsland = island,
            atRoadEnd = roadEnd,
            inBog = bog,
            onShore = shore,
            inMeadows = meadows,
            path = !island && !roadEnd,
            rewards = rewards,
            rewardKind = reward,
            rewardTiers = tiers,
        };

    private static HavenCreatureBand Band(string name, float from, float to, int zones, Vector2Int group, bool peaceful, bool bog, float respawn,
                                          params (string creature, float weight)[] creatures)
    {
        var band = new HavenCreatureBand
        {
            name = name, distance = new Vector2(from, to), zones = zones, group = group, respawnSeconds = respawn, peaceful = peaceful, bogOnly = bog,
        };
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

            // Not on the cheat menu's list: by its file.
            if (definition == null)
            {
                definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"Assets/_Project/Data/Monsters/{creature}.asset");
            }

            if (definition != null && definition.Prefab != null)
            {
                band.creatures.Add(new SpawnTable.Entry { prefab = definition.Prefab, weight = weight });
            }
        }

        return band;
    }

    private static HavenResourceBand Resource(string name, float from, float to, ResourceNodeKind kind, int lowTier, int highTier, int clusters, int least, int most,
                                              bool mountain = false, bool bog = false) =>
        new HavenResourceBand
        {
            name = name,
            distance = new Vector2(from, to),
            kind = kind,
            tiers = new Vector2Int(lowTier, highTier),
            clusters = clusters,
            perCluster = new Vector2Int(least, most),
            mountainOnly = mountain,
            bogOnly = bog,
        };
}
