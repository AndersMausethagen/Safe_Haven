#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Misty Woods' ravine country and relief (`unity command script run --file AgentScripts/BuildRaidGorges.cs --entry BuildRaidGorges.Apply`):
/// adds the mood "The Gorges" to the raid's Layout Rules, or rewrites it if it is there; gives the rules what ravines are drawn with
/// (river and waterfall materials, the waterfall's plunge, crag pieces); and sets the relief every run of the raid gets, whatever its mood:
/// rolled hilliness, great hills, clusters of great boulders and lookouts on the highest clearings, regions on large maps, two ways to the
/// exit, rivers with fords, landmarks, content placed by height and the Terrain Events raid system. The same relief and variety go to the
/// Troll Valley's rules. Run after BuildRaidLayout, which rewrites the whole mood list.
/// </summary>
public static class BuildRaidGorges
{
    private const string Rules = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string TrollRules = "Assets/_Project/Data/Raids/Maps/TrollValley Layout.asset";
    private static readonly string[] Raids = { "Assets/_Project/Data/Raids/MistyWoods.asset", "Assets/_Project/Data/Raids/TrollValley.asset" };
    private const string DeadCedar = "Assets/_Project/Art/Vegetation/Misty Woods/Trees/Dead Cedar.prefab";
    private const string StandingStones = "Assets/_Project/Prefabs/Raids/SetPieces/Standing Stones.prefab";
    // How often a river crosses the open land, by mood: wet hollows most, the gorges none (they have their own).
    private static readonly Dictionary<string, float> RiversByMood = new Dictionary<string, float>
    {
        { "Old Growth", 1f }, { "Windthrow", 0.7f }, { "The Hollows", 1.5f }, { "Open Glades", 1.2f }, { "Cedar Ridge", 0.6f }, { MoodName, 0f },
    };
    private const string RiverMaterial = "Assets/_Project/Art/Generated/Water/Water River (World).mat";
    private const string Crags = "Assets/_Project/Data/World/Mossy Forest/Mossy Forest Crags.asset";
    private const string Falls = "Assets/_Project/Art/Generated/Water/Water Waterfall.mat";
    private const string Plunge = "Assets/_Project/Art/Generated/Water/Waterfall Plunge.prefab";
    public const string MoodName = "The Gorges";

    public static string Apply()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(Rules);
        var river = AssetDatabase.LoadAssetAtPath<Material>(RiverMaterial);
        var crags = AssetDatabase.LoadAssetAtPath<CliffKit>(Crags);
        var falls = AssetDatabase.LoadAssetAtPath<Material>(Falls);
        var plunge = AssetDatabase.LoadAssetAtPath<GameObject>(Plunge);
        if (rules == null || river == null || crags == null || falls == null || plunge == null)
        {
            return "Missing one of: " + string.Join(", ", Rules, RiverMaterial, Crags, Falls, Plunge);
        }

        // Slender cedars on rock, as on Cedar Ridge: its tree weights follow the map's tree list, so they are copied rather than written out.
        var trees = new List<float>();
        foreach (RaidMapMood mood in rules.Moods)
        {
            if (mood != null && mood.name == "Cedar Ridge")
            {
                trees.AddRange(mood.treeWeights);
            }
        }

        rules.SetMood(new RaidMapMood
        {
            name = MoodName,
            briefing = "The land breaks into high ledges and deep gorges, with water loud far below. The ways up are few, and nobody " +
                       "climbs out of a gorge.",
            weight = 1f,
            forest = 0.75f,
            treeSpacing = 1.05f,
            treeWeights = trees,
            // Low ridges, so the ledges read as cliffs; wider trails, as their edges are a long way down.
            ridges = 0.5f,
            rooms = 1f,
            trails = 1.1f,
            rocks = 2.5f,
            fogBanks = 1,
            hills = 1.5f,
            boulders = 1.8f,
            largeByDefault = true,
            terraces = 3,
            terraceHeight = 7f,
            terraceScale = 110f,
            ravines = new Vector2Int(2, 3),
            ravineLength = new Vector2(90f, 220f),
            ravineWidth = new Vector2(9f, 13f),
            ravineDepth = new Vector2(14f, 20f),
            riverChance = 0.65f,
            ravineMist = true,
            spires = new Vector2Int(2, 4),
            spireHeight = new Vector2(8f, 14f),
            spireRadius = new Vector2(3f, 6.5f),
            ravineDescents = 1,
            rivers = 0f,
        });
        var landmarks = new[]
        {
            new RaidLandmarkRule { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeadCedar), weight = 1f, scale = new Vector2(1.6f, 2.1f), sink = 0.02f },
            new RaidLandmarkRule { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StandingStones), weight = 0.8f, scale = new Vector2(0.9f, 1.15f), sink = 0.04f },
        };
        string troll = "";
        foreach (RaidLayoutRules each in new[] { rules, AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(TrollRules) })
        {
            if (each == null)
            {
                continue;
            }

            each.ConfigureRivers(river, crags, falls, plunge);
            // Every run: some flat, some steep; a few great hills; boulders lying about; a lookout on the highest clearing.
            each.ConfigureRelief(new Vector2(0.6f, 1.9f), new Vector2Int(2, 4), new Vector2(4f, 9f), new Vector2(22f, 42f), crags,
                new Vector2Int(6, 10), new Vector2Int(1, 3), new Vector2(0.6f, 1.3f), 1, 70f);
            // Large maps in two or three regions; two ways to the exit; a river now and then; landmarks to steer by.
            each.ConfigureVariety(new Vector2Int(2, 3), 30f, true, new Vector2Int(0, 1), new Vector2(5f, 8f), new Vector2(1.3f, 1.9f), landmarks, new Vector2Int(1, 2));
            each.ConfigureSiteHeights(RaidSiteKind.SupplyCache, true, false);
            each.ConfigureSiteHeights(RaidSiteKind.Rumour, true, false);
            each.ConfigureSiteHeights(RaidSiteKind.EnemyCamp, false, true);
            foreach (RaidMapMood mood in each.Moods)
            {
                if (mood != null && RiversByMood.TryGetValue(mood.name, out float share))
                {
                    mood.rivers = share;
                }
            }

            EditorUtility.SetDirty(each);
            AssetDatabase.SaveAssetIfDirty(each);
            troll = each == rules ? troll : $" Relief and variety also on {each.name}.";
        }

        int systems = 0;
        foreach (string path in Raids)
        {
            systems += AddTerrainEvents(AssetDatabase.LoadAssetAtPath<RaidDefinition>(path)) ? 1 : 0;
        }

        return $"{MoodName} is one of {rules.Moods.Count} moods in {rules.name}; rivers use {river.name}, walls {crags.name}.{troll} Terrain Events added to {systems} raid(s).";
    }

    // Once per raid: its list keeps any settings changed by hand.
    private static bool AddTerrainEvents(RaidDefinition raid)
    {
        if (raid == null)
        {
            return false;
        }

        var data = new SerializedObject(raid);
        SerializedProperty systems = data.FindProperty("systems");
        for (int i = 0; i < systems.arraySize; i++)
        {
            if (systems.GetArrayElementAtIndex(i).managedReferenceValue is TerrainEventsSystem)
            {
                return false;
            }
        }

        systems.arraySize++;
        systems.GetArrayElementAtIndex(systems.arraySize - 1).managedReferenceValue = new TerrainEventsSystem();
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(raid);
        return true;
    }
}
