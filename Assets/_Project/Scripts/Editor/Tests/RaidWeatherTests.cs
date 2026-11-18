using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A run's weather foretold in the briefing (Docs/GameDesignResearch_2026-11-17.md W3): the run opens in a weather picked from the raid's
/// climate by its seed, the same every time the seed is read (briefing, raid, a friend's game), by the climate's chances; the briefing's
/// line reads the rules it names; and each raid's climate is the one its scene uses.
/// </summary>
public sealed class RaidWeatherTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in made)
        {
            Object.DestroyImmediate(thing);
        }

        made.Clear();
    }

    private WeatherDefinition Weather(string name, float rain, float fog = 0f, float mist = 0f, float lightning = 0f)
    {
        var weather = ScriptableObject.CreateInstance<WeatherDefinition>();
        weather.Configure(name, 1f, 0.2f, rain, 0.5f, fog, Color.gray, lightning, 3f, 6f, mist);
        made.Add(weather);
        return weather;
    }

    [Test]
    public void ARunOpensInTheSameWeather_EveryTimeItsSeedIsRead_ByTheClimatesChances()
    {
        WeatherDefinition clear = Weather("Clear", 0f), rain = Weather("Rain", 0.7f);
        var climate = ScriptableObject.CreateInstance<ClimateDefinition>();
        made.Add(climate);
        climate.Configure(clear, 45f, new ClimateDefinition.Entry { weather = clear, chance = 3f }, new ClimateDefinition.Entry { weather = rain, chance = 1f });

        int rainy = 0;
        for (int seed = 0; seed < 2000; seed++)
        {
            WeatherDefinition opening = RaidWeather.Opening(climate, seed);
            Assert.AreSame(opening, RaidWeather.Opening(climate, seed), "the briefing and the raid read the same seed alike");
            rainy += opening == rain ? 1 : 0;
        }

        Assert.AreEqual(0.25f, rainy / 2000f, 0.04f, "a quarter of runs open in rain, as its chance says");
        Assert.IsNull(RaidWeather.Opening((ClimateDefinition)null, 3), "a raid without a climate of its own foretells nothing");
    }

    [Test]
    public void TheBriefingLine_ReadsTheRulesItNames()
    {
        PerceptionSettings perception = PerceptionSettings.Current;
        WeatherDefinition rain = Weather("Rain", 0.7f, 1.8f);
        string line = RaidWeather.BriefingLine(rain, false, perception);
        string carry = PerceptionRules.RainNoiseScale(0.7f, perception).ToString("P0");
        StringAssert.Contains("Rain", line);
        StringAssert.Contains($"carry {carry} as far", line, "the share is the noise rule's own");
        StringAssert.Contains("fire won't take", line, "heavy rain soaks the ground past what fire needs");

        string snow = RaidWeather.BriefingLine(rain, true, perception);
        StringAssert.Contains("Snow", snow, "rain in a snowing season falls as snow");
        StringAssert.DoesNotContain("carry", snow, "snow muffles nothing (PlayerNoise reads rain alone)");

        StringAssert.Contains("Little can be seen", RaidWeather.BriefingLine(Weather("Fog", 0f, 2.2f), false, perception));
        StringAssert.Contains("Lightning", RaidWeather.BriefingLine(Weather("Storm", 1f, 2.2f, 0.4f, 4f), false, perception));
        string clear = RaidWeather.BriefingLine(Weather("Clear", 0f), false, perception);
        StringAssert.DoesNotContain("carry", clear);
        StringAssert.DoesNotContain("seen", clear);
        Assert.IsNull(RaidWeather.BriefingLine(null, false, perception));
    }

    [Test]
    public void EachExpedition_ForetellsTheClimateItsSceneUses_AndBossArenasNone()
    {
        foreach (string id in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(id));
            if (raid == null || string.IsNullOrEmpty(raid.SceneName))
            {
                continue;
            }

            if (raid.IsEncounter)
            {
                Assert.IsNull(raid.Climate, $"{raid.name}: an arena keeps its one look (the user: one fixed hour each)");
                continue;
            }

            Assert.IsNotNull(raid.Climate, $"{raid.name} has a climate to foretell");
            string scene = File.ReadAllText($"Assets/_Project/Scenes/Raids/{raid.SceneName}.unity");
            string climate = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(raid.Climate));
            StringAssert.Contains(climate, scene, $"{raid.name}'s scene weather uses the climate its briefing foretells");
        }
    }
}
