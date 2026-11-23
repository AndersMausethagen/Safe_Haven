using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Berry bushes grow back at home after 3 in-game days (the user, 2026-11-23), though gathered things otherwise stay gone in the Haven
/// (Regrowth): ResourceNode.RegrowDay counts home days (SettlementState.DaysPassed), only at the player's own home.
/// </summary>
public sealed class BushRegrowthTests
{
    [Test]
    public void ABushGatheredAtHome_ComesBackOnTheDayItsDaysRunOut()
    {
        Assert.AreEqual(13, ResourceNode.RegrowDay(3, 10, true), "gathered on day 10, back on day 13");
        Assert.AreEqual(0, ResourceNode.RegrowDay(3, 10, false), "in a raid, the prologue or a friend's Haven it doesn't count days");
        Assert.AreEqual(0, ResourceNode.RegrowDay(0, 10, true), "a node without days follows the usual rule");
    }

    [Test]
    public void OnlyThingsPickedByHand_GrowBackByDays()
    {
        var nodes = AssetDatabase.FindAssets("t:GatheringNodeDefinition")
            .Select(id => AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(node => node != null && node.RegrowsAtHomeAfterDays > 0).ToList();
        Assert.IsTrue(nodes.Any(node => node.name == "Berries"), "the berry bushes come back");
        foreach (GatheringNodeDefinition node in nodes)
        {
            // Wood and stone stay gone at home: raids are the material loop (the user, 2026-11-22).
            Assert.AreEqual(ToolType.None, node.GatherTool, $"{node.name} is picked by hand");
        }
    }

    [Test]
    public void APickedBush_KeepsOrGetsItsDay_OnlyWithItsOwner()
    {
        Assert.AreEqual(14, ResourceNode.RestoredRegrowDay(14, 0, 3, 12, true), "the owner keeps the day the save holds");
        Assert.AreEqual(15, ResourceNode.RestoredRegrowDay(0, 0, 3, 12, true),
            "picked by a friend (their copy holds no day) or saved before bushes counted days: the owner counts from today");
        Assert.AreEqual(13, ResourceNode.RestoredRegrowDay(0, 13, 3, 12, true), "a day it already had isn't pushed back by a state without one");
        Assert.AreEqual(0, ResourceNode.RestoredRegrowDay(14, 0, 3, 12, false), "a friend's copy never regrows by day: the host's state grows it back");
        Assert.AreEqual(0, ResourceNode.RestoredRegrowDay(0, 0, 0, 12, true), "a node without days follows the usual rule");
    }
}
