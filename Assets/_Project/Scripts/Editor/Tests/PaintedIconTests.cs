using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Buildings, combat skills, skills and perks show LocalSpawn's paintings (Tools/LocalSpawn/generate_item_icons.py
/// --set NAME), not placeholders or renders, and every one of them has a line saying what its icon shows.
/// </summary>
public class PaintedIconTests
{
    private const string Fix = " Paint it (Tools/LocalSpawn/generate_item_icons.py) and run AgentScripts/BuildIcons.cs; a content builder may have put its own icon back.";

    private static IEnumerable<TestCaseData> Sets()
    {
        yield return Case<BuildingDefinition>("building_icons.json", "BuildingIcons", b => b.Id, b => b.Icon);
        yield return Case<SkillDefinition>("skill_icons.json", "SkillIcons", s => s.Id, s => s.Icon);
        yield return Case<ProficiencyDefinition>("proficiency_icons.json", "ProficiencyIcons", p => p.Id, p => p.Icon);
        yield return Case<PerkDefinition>("perk_icons.json", "PerkIcons", p => p.Id, p => p.Icon);
        yield return Case<StatusEffectDefinition>("condition_icons.json", "ConditionIcons", s => s.Id, s => s.Icon);
    }

    private static TestCaseData Case<T>(string wanted, string painted, System.Func<T, string> id, System.Func<T, Sprite> icon) where T : Object
    {
        System.Func<List<(string id, string name, Sprite icon)>> definitions = () =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(x => x != null).Select(x => (id(x), x.name, icon(x))).ToList();
        return new TestCaseData("Tools/LocalSpawn/" + wanted, "Tools/LocalSpawn/" + painted + "/", definitions).SetName(typeof(T).Name);
    }

    // BuildIcons writes the painting at half size and marks the importer with the painting's hash; and icons are mipmapped,
    // as they are drawn small.
    private static bool IsPaintingOf(string icon, string painting)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(icon);
        Assert.IsTrue(importer.mipmapEnabled, icon + " has no mipmaps: small icons turn grainy.");
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            string hash = System.BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(painting))).Replace("-", "").ToLowerInvariant();
            return importer.userData == "painted:" + hash;
        }
    }

    private static HashSet<string> Ids(string wanted) =>
        Regex.Matches(File.ReadAllText(wanted), "^\\s*\"([^\"_][^\"]*)\"\\s*:", RegexOptions.Multiline)
            .Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet();

    [TestCaseSource(nameof(Sets))]
    public void EveryOne_ShowsItsPainting(string wanted, string painted, System.Func<List<(string id, string name, Sprite icon)>> definitions)
    {
        HashSet<string> ids = Ids(wanted);
        var all = definitions();
        Assert.IsNotEmpty(all);
        foreach ((string id, string name, Sprite icon) in all)
        {
            Assert.IsTrue(ids.Contains(id), $"{name} ({id}) has no line in {wanted} saying what its icon shows.");
            string source = painted + Regex.Replace(id.ToLowerInvariant(), "[^a-z0-9_]", "_") + ".png";
            Assert.IsTrue(File.Exists(source), $"{name}: {source} has not been painted." + Fix);
            Assert.IsNotNull(icon, $"{name} has no icon." + Fix);
            string shown = AssetDatabase.GetAssetPath(icon);
            Assert.IsTrue(IsPaintingOf(shown, source), $"{name} shows {shown}, not its painting {source}." + Fix);
        }
    }

    // Only the essences and portal lights were placeholders; the other resources were painted before.
    [Test]
    public void EveryListedEssence_ShowsItsPainting()
    {
        HashSet<string> ids = Ids("Tools/LocalSpawn/resource_icons.json");
        Assert.IsNotEmpty(ids, "the essences listed to paint");
        var resources = AssetDatabase.FindAssets("t:ResourceDefinition").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResourceDefinition>).Where(r => r != null && ids.Contains(r.Id)).ToList();
        Assert.AreEqual(ids.Count, resources.Count, "Every listed id is a resource.");
        foreach (ResourceDefinition resource in resources)
        {
            string source = "Tools/LocalSpawn/ResourceIcons/" + resource.Id + ".png";
            Assert.IsTrue(IsPaintingOf(AssetDatabase.GetAssetPath(resource.Icon), source), resource.DisplayName + Fix);
        }
    }

    [Test]
    public void ABoon_ShowsItsItemsIcon_OrOneForItsKind()
    {
        ConditionIconSettings icons = ConditionIconSettings.Current;
        Assert.IsNotNull(icons.ForBoon(HavenReadiness.RestedId), "Well Rested has its own.");
        Assert.IsNotNull(icons.ForBoon("something_new"), "Anything else has the fallback.");
        var meal = AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
            .First(i => i != null && i.HasBoon && i.Icon != null);
        Assert.AreEqual(meal.Icon, icons.ForBoon(meal.Id), "A meal or tonic shows its own item.");
    }

    [TestCaseSource(nameof(Sets))]
    public void NoTwo_ShareAnIcon(string wanted, string painted, System.Func<List<(string id, string name, Sprite icon)>> definitions)
    {
        var shared = definitions().Where(d => d.icon != null).GroupBy(d => AssetDatabase.GetAssetPath(d.icon)).Where(g => g.Count() > 1).ToList();
        Assert.IsEmpty(shared, string.Join("; ", shared.Select(g => g.Key + ": " + string.Join(", ", g.Select(d => d.name)))));
    }
}
