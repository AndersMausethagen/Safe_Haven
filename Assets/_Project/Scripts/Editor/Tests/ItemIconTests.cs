using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

/// <summary>The items' icons are LocalSpawn's paintings, not the placeholders (Tools/LocalSpawn/generate_item_icons.py).</summary>
public class ItemIconTests
{
    private const string Wanted = "Tools/LocalSpawn/item_icons.json";

    [Test]
    public void EveryPaintedItem_ShowsItsPainting_NotAPlaceholder()
    {
        var ids = Regex.Matches(File.ReadAllText(Wanted), "^\\s*\"([^\"_][^\"]*)\"\\s*:", RegexOptions.Multiline)
            .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        var items = AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>).Where(i => i != null).GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Assert.Greater(ids.Count, 150);
        foreach (string id in ids)
        {
            Assert.IsTrue(items.TryGetValue(id, out ItemDefinition item), "No item with the id " + id);
            Assert.IsNotNull(item.Icon, id);
            // The placeholders are painted at 128 px (64 for the fish); the paintings are imported at 256.
            Assert.AreEqual(256, item.Icon.texture.width,
                $"{item.DisplayName}: its icon is {AssetDatabase.GetAssetPath(item.Icon)} at {item.Icon.texture.width} px. A content builder may have put the placeholder back: run AgentScripts/BuildItemIcons.cs (Apply).");
        }
    }

    [Test]
    public void NoTwoPaintedItems_ShareAnIcon()
    {
        var ids = Regex.Matches(File.ReadAllText(Wanted), "^\\s*\"([^\"_][^\"]*)\"\\s*:", RegexOptions.Multiline)
            .Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet();
        var shared = AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>).Where(i => i != null && ids.Contains(i.Id) && i.Icon != null)
            .GroupBy(i => AssetDatabase.GetAssetPath(i.Icon)).Where(g => g.Count() > 1).ToList();
        Assert.IsEmpty(shared, string.Join("; ", shared.Select(g => g.Key + ": " + string.Join(", ", g.Select(i => i.DisplayName)))));
    }
}
