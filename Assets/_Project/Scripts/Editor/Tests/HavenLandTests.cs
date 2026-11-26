using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Docs/HavenWorldStory_2026-11-26.md steps 1-2: every region beyond the meadows has its own look and a ring that opens with the
/// story; the rings open in order; a region's look fades into the next at the border; the stones of the outer ring say "not yet".
/// </summary>
public sealed class HavenLandTests
{
    private static WorldAtlas Atlas => AssetDatabase.LoadAssetAtPath<WorldAtlas>("Assets/_Project/Resources/Atlases/SafeHaven.asset");
    private static HavenRegionSettings Settings => AssetDatabase.LoadAssetAtPath<HavenRegionSettings>("Assets/_Project/Resources/HavenRegionSettings.asset");

    [Test]
    public void EveryRegionBeyondTheMeadows_HasItsOwnLook_AndARing()
    {
        HavenRegionSettings settings = Settings;
        Assert.IsNotNull(settings, "Resources/HavenRegionSettings");
        foreach (WorldAtlas.Region region in Atlas.Regions.Where(r => r.name != "Haven Meadows"))
        {
            HavenRegionSettings.RegionLook look = settings.LookOf(region.name);
            Assert.IsNotNull(look, $"{region.name}: a look");
            Assert.That(look.ring, Is.InRange(1, 3), $"{region.name}: a ring the story opens");
            Assert.IsFalse(string.IsNullOrWhiteSpace(look.rememberedLine), $"{region.name}: a line for the moment it is remembered");
            Assert.Greater(look.forgottenFloor, 0f, $"{region.name}: forgotten land is never clear");
        }

        Assert.IsNull(settings.LookOf("Haven Meadows"), "home is never forgotten");
        var filters = Atlas.Regions.Select(r => settings.LookOf(r.name)).Where(l => l != null).Select(l => l.filter).ToList();
        Assert.AreEqual(filters.Count, filters.Distinct().Count(), "each region its own colour, not one shared look");
    }

    [Test]
    public void TheOuterRingsOpen_WithTheStory()
    {
        HavenRegionSettings settings = Settings;
        Assert.IsTrue(settings.Openings.Any(o => o.ring == 2 && o.questStarted != null), "the outer ring: after the Heartroot");
        Assert.IsTrue(settings.Openings.Any(o => o.ring == 3 && (o.questStarted != null || o.residentHome != null)), "the last: Act II's end");
        Assert.IsTrue(HavenRings.IsOpen(0), "home is always open");
        Assert.AreNotEqual(settings.NotYet(2), settings.NotYet(3), "each ring says why it is not yet in its own words");
        int near = Atlas.Regions.Count(r => settings.RingOf(r.name) == 1);
        Assert.GreaterOrEqual(near, 3, "a near ring to remember first");
    }

    [Test]
    public void ARegionsLook_FadesIntoTheNext_AtTheBorder()
    {
        const int size = 16;
        var values = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = size / 2; x < size; x++)
            {
                values[y * size + x] = 1f;
            }
        }

        HavenLand.Blur(values, new float[size * size], size, 2);
        float left = values[8 * size + 2], border = values[8 * size + size / 2], right = values[8 * size + size - 3];
        Assert.AreEqual(0f, left, 1e-4f, "well inside the other region: none of it");
        Assert.AreEqual(1f, right, 1e-4f, "well inside: all of it");
        Assert.That(border, Is.InRange(0.4f, 0.8f), "at the border: a share of each");
        float total = 0f;
        foreach (float v in values) total += v;
        Assert.AreEqual(size * size / 2f, total, 1f, "blurring moves the look, it doesn't add or lose any");
    }
}
