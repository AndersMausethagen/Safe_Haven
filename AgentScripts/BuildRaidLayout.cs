using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The Misty Woods' generated map (`unity command run_script --file AgentScripts/BuildRaidLayout.cs --entry BuildRaidLayout.Apply`):
/// its Layout Rules asset, the raid pointing at it, and anchors on what stands apart in Raid_MistyWoods so that it finds a
/// sensible place on any map. Safe to run again: the rules are rewritten, anchors are kept and reconfigured.
/// </summary>
public static class BuildRaidLayout
{
    private const string Rules = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string Raid = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string Scene = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";

    public static string Apply()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(Rules);
        if (rules == null)
        {
            rules = ScriptableObject.CreateInstance<RaidLayoutRules>();
            AssetDatabase.CreateAsset(rules, Rules);
        }

        rules.Configure(Shapes(), Moods(), Sites(), new[]
        {
            new RaidGroupRule { group = "Moss Trolls", site = RaidSiteKind.EnemyCamp },
            new RaidGroupRule { group = "Mistwings", site = RaidSiteKind.EnemyCamp },
        });
        EditorUtility.SetDirty(rules);

        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(Raid);
        var so = new SerializedObject(raid);
        so.FindProperty("layoutRules").objectReferenceValue = rules;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return AnchorScene();
    }

    private static RaidShapeRule[] Shapes() => new[]
    {
        new RaidShapeRule { shape = RaidLayoutShape.Hub, weight = 1f, loopChance = 0.12f, briefing = "The trails all meet at one great clearing." },
        new RaidShapeRule { shape = RaidLayoutShape.Ring, weight = 1f, loopChance = 0.08f, briefing = "The trails run in a ring around wild ground nobody crosses." },
        new RaidShapeRule { shape = RaidLayoutShape.Spine, weight = 1f, loopChance = 0.12f, briefing = "One long trail runs through it, with paths branching off." },
        new RaidShapeRule { shape = RaidLayoutShape.Thicket, weight = 1f, loopChance = 0.4f, briefing = "The trails tangle, and there is always another way round." },
    };

    private static RaidMapMood[] Moods() => new[]
    {
        new RaidMapMood
        {
            name = "Old Growth", weight = 1.2f, briefing = "The spruce stand close and the light barely reaches the ground.",
            forest = 1.15f, treeSpacing = 0.92f, rooms = 0.9f, trails = 0.85f, fogBanks = 1,
        },
        new RaidMapMood
        {
            name = "Windthrow", briefing = "A storm has been through: dead trunks and fallen logs, and long views.",
            forest = 0.7f, treeWeights = new List<float> { 1f, 1f, 0.5f, 0.5f, 4f }, rocks = 4f,
        },
        new RaidMapMood
        {
            name = "The Hollows", briefing = "The land folds into steep hollows, and the mist lies in them.",
            forest = 0.9f, ridges = 1.7f, trails = 0.9f, fogBanks = 3,
        },
        new RaidMapMood
        {
            name = "Open Glades", briefing = "Wide glades and thin woods: easy going, and little cover.",
            forest = 0.55f, rooms = 1.35f, trails = 1.3f, ridges = 0.6f,
        },
        new RaidMapMood
        {
            name = "Pinewood", briefing = "Dry pines on rising ground, with rock breaking through.",
            forest = 0.8f, treeWeights = new List<float> { 0.5f, 0.5f, 0.5f, 5f, 0.3f }, ridges = 1.25f, rocks = 2f,
        },
    };

    // In the order they choose: what should be worth the walk gets the dead ends.
    private static RaidSiteQuota[] Sites() => new[]
    {
        new RaidSiteQuota { kind = RaidSiteKind.Rumour, placement = RaidSitePlacement.DeadEnd, count = 3, separation = 45f, fromStart = 65f, offPath = new Vector2(12f, 20f), clearing = 8f },
        new RaidSiteQuota { kind = RaidSiteKind.Side, placement = RaidSitePlacement.DeadEnd, count = 5, separation = 28f, fromStart = 35f, offPath = new Vector2(11f, 18f), clearing = 6f },
        new RaidSiteQuota { kind = RaidSiteKind.EnemyCamp, placement = RaidSitePlacement.Room, count = 9, separation = 24f, fromStart = 42f, offPath = new Vector2(10f, 16f), clearing = 7f },
        new RaidSiteQuota { kind = RaidSiteKind.EventSite, placement = RaidSitePlacement.Room, count = 6, separation = 26f, fromStart = 30f, offPath = new Vector2(8f, 12f), clearing = 5f },
        new RaidSiteQuota { kind = RaidSiteKind.ResourceSite, placement = RaidSitePlacement.BesidePath, count = 14, separation = 17f, fromStart = 14f, offPath = new Vector2(9f, 15f), clearing = 10f },
        new RaidSiteQuota { kind = RaidSiteKind.SupplyCache, placement = RaidSitePlacement.BesidePath, count = 12, separation = 16f, fromStart = 20f, offPath = new Vector2(10f, 17f), clearing = 4f },
        new RaidSiteQuota { kind = RaidSiteKind.Patrol, placement = RaidSitePlacement.OnPath, count = 10, separation = 20f, fromStart = 45f, clearing = 0f },
    };

    private static string AnchorScene()
    {
        var log = new System.Text.StringBuilder();
        var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Additive);
        Transform content = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent(out RaidMap map))
            {
                content = map.Content;
            }
        }

        Transform Find(string path) => content.Find(path);
        Transform apex = Find("Apex");
        Transform round = Find("Apex/Old Moss Troll Round");
        Anchor(Find("Story/Stranded Traveler"), RaidSiteKind.Side, null, null, 0f, log);
        Anchor(Find("Story/Old Smithy Chest"), RaidSiteKind.Side, null, null, 0f, log);
        Anchor(apex, RaidSiteKind.Apex, null, round, 10f, log);
        Anchor(Find("Quest Triggers/Echo Zone (starts Echoes in the Wilds)"), RaidSiteKind.None, Find("Player Start"), null, 0f, log);
        Anchor(Find("Quest Triggers/Western Beacon Zone"), RaidSiteKind.None, Find("Extraction Points/Extraction Beacon West"), null, 0f, log);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Experience Stones")
            {
                foreach (Transform stone in root.transform)
                {
                    Anchor(stone, RaidSiteKind.Side, null, null, 0f, log);
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        return $"saved={saved}{log}";
    }

    private static void Anchor(Transform target, RaidSiteKind kind, Transform follows, Transform ring, float radius, System.Text.StringBuilder log)
    {
        if (target == null)
        {
            log.Append("\n  ! missing anchor target");
            return;
        }

        RaidMapAnchor anchor = target.GetComponent<RaidMapAnchor>();
        if (anchor == null)
        {
            anchor = target.gameObject.AddComponent<RaidMapAnchor>();
        }

        anchor.Configure(kind, follows, ring, radius);
        log.Append($"\n  {target.name}: {(follows != null ? "follows " + follows.name : kind.ToString())}");
    }
}
