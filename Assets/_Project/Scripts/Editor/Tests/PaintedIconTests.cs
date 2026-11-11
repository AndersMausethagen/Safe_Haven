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
    }

    private static TestCaseData Case<T>(string wanted, string painted, System.Func<T, string> id, System.Func<T, Sprite> icon) where T : Object
    {
        System.Func<List<(string id, string name, Sprite icon)>> definitions = () =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(x => x != null).Select(x => (id(x), x.name, icon(x))).ToList();
        return new TestCaseData("Tools/LocalSpawn/" + wanted, "Tools/LocalSpawn/" + painted + "/", definitions).SetName(typeof(T).Name);
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
            Assert.IsTrue(File.ReadAllBytes(shown).SequenceEqual(File.ReadAllBytes(source)), $"{name} shows {shown}, not its painting {source}." + Fix);
        }
    }

    [TestCaseSource(nameof(Sets))]
    public void NoTwo_ShareAnIcon(string wanted, string painted, System.Func<List<(string id, string name, Sprite icon)>> definitions)
    {
        var shared = definitions().Where(d => d.icon != null).GroupBy(d => AssetDatabase.GetAssetPath(d.icon)).Where(g => g.Count() > 1).ToList();
        Assert.IsEmpty(shared, string.Join("; ", shared.Select(g => g.Key + ": " + string.Join(", ", g.Select(d => d.name)))));
    }
}
