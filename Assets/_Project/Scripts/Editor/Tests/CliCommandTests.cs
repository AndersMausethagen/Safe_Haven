using System;
using NUnit.Framework;
using UnityEngine;

public class CliCommandTests
{
    [Test]
    public void ResolveTypeAcceptsAliasesAndClassNames()
    {
        Assert.AreEqual(typeof(ItemDefinition), CliLookup.ResolveType("item", out string fields));
        StringAssert.Contains("weight", fields);
        Assert.AreEqual(typeof(RecipeDefinition), CliLookup.ResolveType("RecipeDefinition", out _));
        Assert.Throws<ArgumentException>(() => CliLookup.ResolveType("not-a-type", out _));
    }

    [Test]
    public void FindMatchesIdDisplayNameAndAssetName()
    {
        ItemDefinition byName = CliLookup.Find<ItemDefinition>("IronOre");
        Assert.IsNotNull(byName);
        Assert.AreSame(byName, CliLookup.Find<ItemDefinition>(byName.Id));
        Assert.AreSame(byName, CliLookup.Find<ItemDefinition>(byName.DisplayName.ToUpperInvariant()));
        Assert.IsNull(CliLookup.Find<ItemDefinition>("zz-no-such-item"));
    }

    [Test]
    public void AmbiguousPartialKeyListsTheCandidates()
    {
        var error = Assert.Throws<ArgumentException>(() => CliLookup.Find<ItemDefinition>("Ore"));
        StringAssert.Contains("IronOre", error.Message);
    }

    [Test]
    public void ContentListingIsCompact()
    {
        string listing = ContentCliCommands.Content("item", "IronOre", "weight,maximumStack");
        StringAssert.Contains("IronOre", listing);
        StringAssert.Contains("weight=2", listing);
        StringAssert.Contains("maximumStack=", listing);
    }

    [Test]
    public void StructsPrintReferenceFirstAndSkipBlanks()
    {
        string drops = ContentCliCommands.Content("monster", "ForestBittenWolf", "drops");
        StringAssert.Contains("{RoughHide ", drops);
        StringAssert.DoesNotContain("resource=-", drops);
    }

    [Test]
    public void FindReferencesCoversEveryAssetSharingTheName()
    {
        string refs = ContentCliCommands.FindReferences("IronOre", "Assets/_Project");
        StringAssert.Contains("Data/Items/Materials/IronOre.asset", refs);
        StringAssert.Contains("Data/Gathering/IronOre.asset", refs);
    }

    [Test]
    public void HealthRunsEveryPage()
    {
        string report = ContentCliCommands.Health(limit: 0);
        StringAssert.DoesNotContain("check failed", report);
        StringAssert.Contains("assets", report);
    }

    [Test]
    public void PlaytestCommandsRefuseOutsidePlayMode()
    {
        var error = Assert.Throws<InvalidOperationException>(() => PlaytestCliCommands.Give("IronOre"));
        StringAssert.Contains("play mode", error.Message);
    }

    [Test]
    public void JoinLimitedReportsWhatWasCut()
    {
        string joined = CliLookup.JoinLimited(new[] { "a", "b", "c" }, 2, "head");
        Assert.AreEqual("head\na\nb\n(+1 more; raise --limit)", joined.Replace("\r", ""));
        Assert.IsFalse(Application.isPlaying);
    }
}
