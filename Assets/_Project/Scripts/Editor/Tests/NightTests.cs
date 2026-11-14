using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Night is more dangerous and pays for it in Cursed Essence.</summary>
public sealed class NightTests
{
    private const string Raid = "Assets/_Project/Data/Raids/MistyWoods.asset";

    [Test]
    public void TheDark_MakesCreaturesStronger_AndDaylightChangesNothing()
    {
        NightSettings settings = NightSettings.Current;
        Assert.AreEqual(1f, settings.HealthAt(0f), 0.001f);
        Assert.AreEqual(1f, settings.PowerAt(0f), 0.001f);
        Assert.AreEqual(1f, settings.EssenceAt(0f), 0.001f);
        Assert.Greater(settings.HealthAt(1f), 1f, "night creatures take longer to bring down");
        Assert.LessOrEqual(settings.HealthAt(1f), 1.15f, "only a little: the hour is rolled by chance");
        Assert.AreEqual(1f, settings.PowerAt(1f), 0.001f, "they hit no harder: night stacked with the last stage was the unfair spike (balance pass 2026-11-06)");
        Assert.GreaterOrEqual(settings.EssenceAt(1f), 2f, "night is worth the danger");
        Assert.Greater(settings.EssenceAt(0.5f), 1f, "dusk counts for a little");
        Assert.Less(settings.EssenceAt(0.5f), settings.EssenceAt(1f));
    }

    [Test]
    public void AKillAtNight_ReapsTwiceTheEssence()
    {
        float find = NightSettings.Current.EssenceAt(1f) - 1f;
        Assert.AreEqual(4, GearPower.Reaped(2, find, 0f));
        Assert.AreEqual(2, GearPower.Reaped(1, find, 0f));
    }

    [Test]
    public void NightProwlers_GrowWithEveryBreach_UpToALimit()
    {
        NightSettings settings = NightSettings.Current;
        Assert.AreEqual(3, settings.ProwlersAlive(3, 0));
        Assert.Greater(settings.ProwlersAlive(3, 2), 3);
        Assert.AreEqual(settings.ProwlersAlive(3, 100), settings.ProwlersAlive(3, 1000), "a limit, so the dark can't fill up");
        Assert.LessOrEqual(settings.ProwlersAlive(3, 100), 8);
    }

    [Test]
    public void WorseProwlers_ComeLater_AndKeepToTheDark()
    {
        NightSettings settings = NightSettings.Current;
        Assert.IsNull(settings.DeepProwler(0, 0f), "not before any breach is beaten back");
        Assert.IsNull(settings.DeepProwler(9, 0.99f), "only some of them");
        GameObject deep = settings.DeepProwler(9, 0f);
        Assert.IsNotNull(deep);
        Assert.IsNotNull(deep.GetComponent<MonsterAI>());
    }

    [Test]
    public void ANightRaid_HoldsMoreCreaturesThanADayRaid()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(Raid);
        RaidTimeOfDay day = raid.TimesOfDay.First(hour => hour.name == "Day");
        RaidTimeOfDay night = raid.TimesOfDay.First(hour => hour.name == "Night");
        Assert.AreEqual(1f, day.enemyDensity, 0.001f);
        Assert.Greater(night.enemyDensity, day.enemyDensity);
        StringAssert.Contains("Cursed Essence", night.briefing);
    }

    // Playtest 2026-11-05: corruption from the Misty Woods; since the design review (8.5, 8.6, 2026-11-14) foretold a raid
    // ahead, by the portal's tier and the blight, never by the buildings.
    [Test]
    public void Corruption_IsRareAtFirst_GrowsWithThePortalAndTheBlight_AndStaysASmallBand()
    {
        CorruptionSettings settings = CorruptionSettings.Current;
        Assert.NotNull(settings, "Resources/CorruptionSettings");
        Assert.AreEqual(0f, settings.ChanceAt(0, 1f), "nothing before the portal wakes");
        Assert.AreEqual(0f, settings.ChanceAt(1, 1f), "nor before the ward is first raised: the first days are left to building");
        Assert.Greater(settings.ChanceAt(2, 0f), 0f, "it can come once the ward stands");
        Assert.Less(settings.ChanceAt(2, 0f), 0.34f, "rare at first");
        Assert.Greater(settings.ChanceAt(4, 0f), settings.ChanceAt(2, 0f), "more likely as the portal grows");
        Assert.Greater(settings.ChanceAt(2, 1f), settings.ChanceAt(2, 0f), "and as the blight spreads");
        Assert.LessOrEqual(settings.ChanceAt(4, 1f), 0.5f, "never most homecomings");
        Assert.AreEqual(3, settings.CountAt(2, 0f));
        Assert.LessOrEqual(settings.CountAt(4, 1f), 8, "a band, not a breach");
        Assert.Greater(settings.Creatures.Count, 0);
        foreach (UnityEngine.GameObject creature in settings.Creatures)
        {
            Assert.NotNull(creature);
            Assert.NotNull(creature.GetComponent<MonsterAI>(), creature.name + " can fight");
        }
    }
}
