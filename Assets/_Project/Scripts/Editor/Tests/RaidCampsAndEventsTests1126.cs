using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The second raid places round of 2026-11-26: camps that look like camps, resource sites marked on the minimap once seen, and events of
/// each raid's own (Troll Valley and the Moss Marsh had only the Woods' events).
/// </summary>
public sealed class RaidCampsAndEventsTests1126
{
    private static readonly string[] Generated = { "MistyWoods", "TrollValley", "MossMarsh" };

    private static RaidDefinition Raid(string id) => AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{id}.asset");

    private static List<RaidEventDefinition> Pool(RaidDefinition raid)
    {
        var so = new SerializedObject(raid);
        SerializedProperty systems = so.FindProperty("systems");
        for (int i = 0; i < systems.arraySize; i++)
        {
            SerializedProperty system = systems.GetArrayElementAtIndex(i);
            if (system.managedReferenceValue is RaidEventsSystem)
            {
                SerializedProperty pool = system.FindPropertyRelative("settings").FindPropertyRelative("pool");
                return Enumerable.Range(0, pool.arraySize).Select(k => pool.GetArrayElementAtIndex(k).objectReferenceValue as RaidEventDefinition).Where(e => e != null).ToList();
            }
        }

        return new List<RaidEventDefinition>();
    }

    [Test]
    public void EveryRaidHasEventsNoOtherRaidHas()
    {
        var pools = Generated.ToDictionary(id => id, id => Pool(Raid(id)));
        foreach (string id in Generated)
        {
            List<RaidEventDefinition> own = pools[id].Where(e => Generated.Where(other => other != id).All(other => !pools[other].Contains(e))).ToList();
            Assert.GreaterOrEqual(own.Count, id == "MistyWoods" ? 1 : 3, $"{id}: its own events ({string.Join(", ", own.Select(e => e.name))})");
            Assert.GreaterOrEqual(pools[id].Count, 8, $"{id}: enough events that a raid's four aren't the same every run");
        }
    }

    [Test]
    public void EveryEventsChoicesSayWhatTheyCost_AndItsSiteCanBeUsed()
    {
        foreach (RaidEventDefinition definition in Generated.SelectMany(id => Pool(Raid(id))).Distinct())
        {
            foreach (RaidEventChoice choice in definition.Choices)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(choice.detail), $"{definition.name} › {choice.label}: the price, plainly (GDD §9.3)");
                Assert.IsNotEmpty(choice.outcomes, $"{definition.name} › {choice.label}: something comes of it");
                Assert.IsFalse(choice.outcomes.Any(o => o == null), $"{definition.name} › {choice.label}: a missing outcome");
            }

            if (definition.SitePrefab != null)
            {
                Assert.IsNotNull(definition.SitePrefab.GetComponent<RaidEventSite>(), $"{definition.name}: its site");
                foreach (CuriousPlace place in definition.SitePrefab.GetComponentsInChildren<CuriousPlace>(true))
                {
                    // A copy of the wagon's site that kept its name; the wagon's own site, used again on purpose (the pedlar's brother), is not.
                    bool borrowed = place.InteractionName == "Overturned Wagon" && definition.SitePrefab.name != "Overturned Wagon";
                    Assert.IsFalse(borrowed, $"{definition.name}: its site is named for itself, not the wagon it was copied from");
                }
                if (definition.Choices.Count > 0)
                {
                    Assert.IsNotNull(definition.SitePrefab.GetComponent<Interactable>(), $"{definition.name}: its choices are offered at the site");
                }
            }
        }
    }

    [Test]
    public void EveryRaidsCampsAreDressed_WithSolidPiecesThatHaveTheirOwnLook()
    {
        foreach (string id in Generated)
        {
            IReadOnlyList<GameObject> dressing = Raid(id).LayoutRules.CampDressing;
            Assert.GreaterOrEqual(dressing.Count(piece => piece != null), 2, $"{id}: a camp reads as one from the camera");
            foreach (GameObject piece in dressing.Where(piece => piece != null))
            {
                Assert.IsNotNull(piece.GetComponentInChildren<Collider>(), $"{id} › {piece.name}: solid, so the navmesh goes round it");
                Assert.IsNotNull(piece.GetComponentInChildren<LODGroup>(), $"{id} › {piece.name}: a generated model with its levels");
            }

            Vector2 reach = Raid(id).LayoutRules.CampDressingReach;
            Assert.GreaterOrEqual(reach.x, 4f + 1.5f + 1.5f, $"{id}: dressing stands beyond the camp's creature points (4 m), their scatter and half its own footprint");
        }
    }

    [Test]
    public void AStashLooksUnlikeACache()
    {
        var stash = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/Caches/Camp Stash.prefab");
        var cache = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/Caches/Hunter's Cache.prefab");
        var meshes = new HashSet<Mesh>(cache.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh));
        Assert.IsFalse(stash.GetComponentsInChildren<MeshFilter>(false).Any(f => f.gameObject.activeInHierarchy && meshes.Contains(f.sharedMesh)),
            "a camp's stash is its own thing to see, not another supply crate");
    }

    [Test]
    public void EveryResourceGroupHasAMapIcon()
    {
        foreach (string id in Generated)
        {
            string text = System.IO.File.ReadAllText($"Assets/_Project/Scenes/Raids/Raid_{id}.unity");
            foreach (string group in new[] { "Ore Veins", "Timber", "Herbs and Fibres", "Life Wells" })
            {
                string icon = GroupField(text, group, "mapIcon");
                StringAssert.DoesNotContain("fileID: 0}", icon, $"{id} › {group}: marked on the minimap once seen");
            }
        }
    }

    private static string GroupField(string text, string groupName, string field)
    {
        string[] blocks = System.Text.RegularExpressions.Regex.Split(text, @"^--- !u!", System.Text.RegularExpressions.RegexOptions.Multiline);
        string objectId = blocks.Where(b => b.StartsWith("1 &") && System.Text.RegularExpressions.Regex.IsMatch(b, $@"^\s+m_Name: {System.Text.RegularExpressions.Regex.Escape(groupName)}\s*$", System.Text.RegularExpressions.RegexOptions.Multiline))
            .Select(b => b.Substring(3, b.IndexOfAny(new[] { ' ', '\r', '\n' }, 3) - 3)).FirstOrDefault();
        Assert.IsNotNull(objectId, $"no object named {groupName}");
        string group = blocks.FirstOrDefault(b => b.StartsWith("114 &") && b.Contains($"m_GameObject: {{fileID: {objectId}}}") && b.Contains("siteShare:"));
        Assert.IsNotNull(group, $"{groupName} has no Spawn Group");
        return System.Text.RegularExpressions.Regex.Match(group, $@"^\s+{field}: (.*)$", System.Text.RegularExpressions.RegexOptions.Multiline).Groups[1].Value.Trim();
    }
}
