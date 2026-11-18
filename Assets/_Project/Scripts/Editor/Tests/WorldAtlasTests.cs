using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The Haven's regions and named places on the world map (WorldAtlas, PlaceDiscovery, AgentScripts/BuildHavenAtlas.cs).</summary>
public sealed class WorldAtlasTests
{
    private static WorldAtlas Haven => WorldAtlas.For("SafeHaven");

    [Test]
    public void TheHaven_HasNamedRegions_AroundIt()
    {
        WorldAtlas atlas = Haven;
        Assert.IsNotNull(atlas, "Bake it: AgentScripts/BuildHavenAtlas.cs, Apply.");
        Assert.GreaterOrEqual(atlas.Regions.Count, 8);
        CollectionAssert.AllItemsAreUnique(atlas.Regions.Select(r => r.name));
        Assert.AreEqual("Haven Meadows", atlas.RegionAt(Vector3.zero).name, "The portal stands in the Haven's own meadows.");
        foreach (WorldAtlas.Region region in atlas.Regions)
        {
            Assert.Greater(region.share, 0f, region.name);
            Assert.AreEqual(region.name, atlas.RegionAt(new Vector3(region.label.x, 0f, region.label.y)).name, $"{region.name}'s name is written inside it.");
        }

        Assert.IsNull(WorldAtlas.For("Raid_MistyWoods"), "Raids are laid out anew each time: they have no atlas.");
    }

    [Test]
    public void EveryPlace_HasItsOwnName_AndStandsInARegion()
    {
        WorldAtlas atlas = Haven;
        Assert.IsNotEmpty(atlas.Places, "how many is the recipe's; that the atlas holds them all is TheAtlas_IsTheWorldThatWasGenerated");
        CollectionAssert.AllItemsAreUnique(atlas.Places.Select(p => p.id));
        CollectionAssert.AllItemsAreUnique(atlas.Places.Select(p => p.name));
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            Assert.IsNotNull(atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y)), place.name);
            Assert.Greater(place.radius, 10f, place.name);
        }
    }

    [Test]
    public void TheAtlas_IsTheWorldThatWasGenerated()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>("Assets/_Project/Data/World/Safe Haven/Haven World Recipe.asset");
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe);
        WorldAtlas atlas = Haven;
        Assert.AreEqual(plan.Pois.Count, atlas.Places.Count);
        foreach (HavenWorldPlan.Poi poi in plan.Pois)
        {
            Assert.IsTrue(atlas.Places.Any(p => (p.at - poi.At).sqrMagnitude < 1f), $"A {poi.Rule.name} at {poi.At} is not in the atlas: bake it again.");
        }
    }

    // That the journal keeps it through a save is SaveCoverageTests' (every saved field of CharacterProgress, through SaveMapper).
    [Test]
    public void AFoundPlace_IsFoundOnce()
    {
        var progress = new CharacterProgress();
        string id = Haven.Places[0].id;
        Assert.IsFalse(progress.Journal.HasDiscovered(id));
        Assert.IsTrue(progress.Journal.NoteDiscovered(id));
        Assert.IsFalse(progress.Journal.NoteDiscovered(id), "Found once.");
        Assert.IsTrue(progress.Journal.HasDiscovered(id));
    }
}
