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
        Assert.Greater(settings.HealthAt(1f), 1f);
        Assert.Greater(settings.PowerAt(1f), 1f);
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
}
