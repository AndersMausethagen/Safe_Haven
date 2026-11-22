using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// The user's list of 2026-11-22: every progress bar is the one kit bar (KitBarFill through KitBar: the bar shader, its glide and
/// pulse), and the Smithy's Charcoal Clamp makes charcoal for less wood than the campfire.
/// </summary>
public sealed class SharedBarsAndCharcoalTests
{
    // A bar's fill moved by hand: "fill.anchorMax = new Vector2(share, 1f)" with anything but a fixed 0 or 1 for its share.
    private static readonly Regex HandMovedFill = new Regex(@"[Ff]ill\w*(\.rectTransform)?\.anchorMax\s*=\s*new Vector2\((?!\s*[01]f\s*,)", RegexOptions.Compiled);

    [Test]
    public void EveryProgressBar_MovesThroughTheKitBar()
    {
        string root = "Assets/_Project/Scripts";
        var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            // The floating bars over every creature stay plain: one material each, over dozens of creatures, isn't worth it.
            .Where(path => !path.Replace('\\', '/').Contains("/Editor/") && !path.EndsWith("KitBarFill.cs") && !path.EndsWith("KitParts.cs")
                           && !path.EndsWith("WorldHealthBar.cs"))
            .SelectMany(path => File.ReadAllLines(path).Select((line, i) => (path, line, i)))
            .Where(entry => HandMovedFill.IsMatch(entry.line))
            .Select(entry => $"{Path.GetFileName(entry.path)}:{entry.i + 1}")
            .ToList();
        Assert.IsTrue(HandMovedFill.IsMatch("fill.anchorMax = new Vector2(share, 1f);"), "the canary: the pattern finds a hand-moved fill");
        Assert.IsFalse(HandMovedFill.IsMatch("fill.anchorMax = new Vector2(0f, 1f);"), "a fill laid out at zero is no bar moving");
        Assert.IsEmpty(offenders, "move a bar with KitBar.Drive or KitBar.Set: " + string.Join(", ", offenders));
    }

    [Test]
    public void TheSmithysClamp_MakesCharcoal_ForLessThanTheFire()
    {
        var fire = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/CharcoalAtTheFire.asset");
        var clamp = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/CharcoalAtTheSmithy.asset");
        Assert.IsNotNull(clamp);
        Assert.AreSame(fire.Output, clamp.Output);
        Assert.AreEqual(CraftingStationType.Anvil, clamp.Station, "made at the Smithy");
        Assert.IsNotNull(clamp.NeedsModule, "with its module fitted");
        Assert.AreEqual("module.charcoalclamp", clamp.NeedsModule.Id);
        Assert.Less(clamp.ItemCost.Sum(cost => cost.Amount), fire.ItemCost.Sum(cost => cost.Amount), "less wood than the fire");
        var content = UnityEngine.Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        Assert.IsTrue(content.Recipes.Contains(clamp), "the save knows it");
        Assert.AreSame(clamp.NeedsModule, content.FindModule("module.charcoalclamp"));
    }
}
