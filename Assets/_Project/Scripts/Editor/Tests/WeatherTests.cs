using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class WeatherTests
{
    [Test]
    public void TheGround_SoaksWithTheRainAndDriesSlowly()
    {
        Assert.AreEqual(0.5f, WeatherSystem.AdvanceWetness(0f, 1f, 30f, 60f, 240f), 0.001f, "half way through a minute's soak");
        Assert.AreEqual(1f, WeatherSystem.AdvanceWetness(0.9f, 1f, 30f, 60f, 240f), 0.001f, "and no further");
        Assert.AreEqual(0.1f, WeatherSystem.AdvanceWetness(0f, 0.25f, 24f, 60f, 240f), 0.001f, "light rain soaks slower");
        Assert.AreEqual(0.25f, WeatherSystem.AdvanceWetness(0.25f, 0.25f, 24f, 60f, 240f), 0.001f, "and only so far");
        Assert.AreEqual(0.75f, WeatherSystem.AdvanceWetness(1f, 0f, 60f, 60f, 240f), 0.001f, "drying has its own pace");
        Assert.AreEqual(0f, WeatherSystem.AdvanceWetness(0.01f, 0f, 60f, 60f, 240f), 0.001f);
    }

    [Test]
    public void Fireflies_ComeOutAtNightAndShelterFromTheRain()
    {
        var profile = ScriptableObject.CreateInstance<FireflyProfile>();

        // How many is the profile's to tune; far more by night than by day, and rain sends most of them in.
        Assert.Greater(profile.CountFor(1f, 0f), profile.CountFor(0f, 0f) * 4);
        Assert.Less(profile.CountFor(1f, 1f), profile.CountFor(1f, 0f) / 4);
        Object.DestroyImmediate(profile);
    }

    private readonly List<Object> created = new List<Object>();
    private float fogDensity;
    private Color fogColour;
    private Light renderSun;

    [SetUp]
    public void SetUp()
    {
        fogDensity = RenderSettings.fogDensity;
        fogColour = RenderSettings.fogColor;
        renderSun = RenderSettings.sun;
    }

    [TearDown]
    public void TearDown()
    {
        TestCleanup.Destroy(created);
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogColor = fogColour;
        RenderSettings.sun = renderSun;
        Shader.SetGlobalFloat("_SafeHavenRain", 0f);
    }

    private T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        created.Add(go);
        return go.AddComponent<T>();
    }

    private T Asset<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        created.Add(asset);
        return asset;
    }

    private WeatherDefinition Weather(string name, float wind, float rain, float clouds, float lightning = 0f, float minutes = 5f)
    {
        WeatherDefinition weather = Asset<WeatherDefinition>();
        weather.Configure(name, wind, 0.2f, rain, clouds, 2f, new Color(0.5f, 0.5f, 0.6f, 0.5f), lightning, minutes, minutes);
        return weather;
    }

    private static void Wire(Object target, params (string Field, Object Value)[] fields)
    {
        var serialized = new SerializedObject(target);
        foreach (var (field, value) in fields)
        {
            serialized.FindProperty(field).objectReferenceValue = value;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Skies whose day is made dark on purpose, and why: the day and night rules below don't hold for them.
    private static readonly Dictionary<string, string> MadeDarkByDay = new Dictionary<string, string>
    {
        ["Prologue Forest Night"] = "the prologue's night at noon: the day dies hours early, so its midday is darker than its night",
    };

    // Over the skies the game plays (every Day Night profile asset), not a fresh profile's defaults: the sun by day, a readable moon
    // by night, light gone at the horizon, and an evening sun high enough not to graze flat ground. Their tuning is each sky's own.
    [Test]
    public void Profile_SunByDayMoonByNight()
    {
        int skies = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:DayNightProfile", new[] { "Assets/_Project/Data" }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<DayNightProfile>(AssetDatabase.GUIDToAssetPath(guid));
            skies++;
            if (MadeDarkByDay.ContainsKey(profile.name))
            {
                continue;
            }

            float middayHour = (profile.Sunrise + profile.Sunset) * 0.5f;
            float midnightHour = (profile.Sunrise + profile.Sunset + 24f) * 0.5f % 24f;
            DayNightState noon = profile.Evaluate(middayHour);
            Assert.IsFalse(noon.IsMoon, profile.name);
            Assert.AreEqual(1f, noon.Daylight, 1e-4f, profile.name);
            Assert.AreEqual(0f, noon.Night, 1e-3f, profile.name);

            DayNightState midnight = profile.Evaluate(midnightHour);
            Assert.IsTrue(midnight.IsMoon, profile.name);
            Assert.AreEqual(1f, midnight.Night, 1e-3f, profile.name);
            Assert.Greater(midnight.LightIntensity, 0.3f, $"{profile.name}: moonlight keeps the night readable");
            Assert.Less(midnight.AmbientSky.grayscale, noon.AmbientSky.grayscale * 0.6f, profile.name);

            Assert.AreEqual(0f, profile.Evaluate(profile.Sunrise).LightIntensity, 1e-3f, $"{profile.name}: the light fades out at the horizon");
            Assert.GreaterOrEqual(profile.Evaluate(profile.Sunset - 0.5f).LightRotation.eulerAngles.x, 12f, $"{profile.name}: the evening light never grazes flat ground");
        }

        Assert.Greater(skies, 3, "the game's skies were found");
    }

    [Test]
    public void Cycle_LightsTheSceneAndWeightsTheVolumes()
    {
        Light sun = Make<Light>("sun");
        sun.type = LightType.Directional;
        Volume night = Make<Volume>("night");
        Volume golden = Make<Volume>("golden");
        DayNightCycle cycle = Make<DayNightCycle>("cycle");
        Wire(cycle, ("profile", Asset<DayNightProfile>()), ("sun", sun), ("nightVolume", night), ("goldenHourVolume", golden));

        cycle.Hour = 13f;
        Assert.AreEqual(2.3f, sun.intensity, 0.05f);
        Assert.AreEqual(0f, night.weight, 1e-3f);
        Assert.AreEqual(0.006f, RenderSettings.fogDensity, 1e-4f);
        Assert.AreSame(sun, RenderSettings.sun);

        cycle.Hour = 26f; // wraps to 2:00
        Assert.AreEqual(2f, cycle.Hour, 1e-4f);
        Assert.IsTrue(cycle.IsNight);
        Assert.Greater(sun.color.b, sun.color.r, "bluish moonlight");
        Assert.AreEqual(1f, night.weight, 1e-3f);

        cycle.Hour = 19.5f;
        Assert.Greater(golden.weight, 0.9f);
    }

    [Test]
    public void Trilight_ProbeMatchesTheGradient()
    {
        Color sky = new Color(0.6f, 0.7f, 0.9f), equator = new Color(0.4f, 0.4f, 0.4f), ground = new Color(0.1f, 0.08f, 0.05f);
        SphericalHarmonicsL2 probe = DayNightCycle.Trilight(sky, equator, ground);
        var colours = new Color[2];
        probe.Evaluate(new[] { Vector3.up, Vector3.down }, colours);
        Assert.Greater(colours[0].b, colours[1].b * 3f, "brighter from above");
        Assert.AreEqual(0.5f * (sky.b + equator.b), colours[0].b, 0.15f, "an upward face sees about the sky-to-horizon average");

        SphericalHarmonicsL2 flat = DayNightCycle.Trilight(Color.gray, Color.gray, Color.gray);
        colours = new Color[1];
        flat.Evaluate(new[] { Vector3.up }, colours);
        Assert.AreEqual(Color.gray.r, colours[0].r, 0.03f, "a uniform sky gives its own colour");
    }

    [Test]
    public void Weather_BlendsIntoWindRainAndTheOvercastLook()
    {
        MossWind wind = Make<MossWind>("wind");
        RainEffect rain = Make<RainEffect>("rain");
        Volume overcast = Make<Volume>("overcast");
        WeatherSystem system = Make<WeatherSystem>("weather");
        Wire(system, ("wind", wind), ("rain", rain), ("overcastVolume", overcast));
        float calmTrees = Shader.GetGlobalFloat("_SafeHavenTreeWind");

        WeatherDefinition storm = Weather("Storm", 3f, 1f, 1f);
        system.SetWeather(storm, 10f);
        system.Tick(5f);
        Assert.AreEqual(0.5f, system.Current.Rain, 0.01f, "half way");
        system.Tick(5f);
        Assert.AreEqual(1f, system.Current.Rain, 1e-4f);
        Assert.AreEqual(1f, rain.Intensity, 1e-4f);
        Assert.AreEqual(1f, Shader.GetGlobalFloat("_SafeHavenRain"), 1e-4f);
        Assert.AreEqual(1f, overcast.weight, 1e-4f);
        Assert.AreEqual(calmTrees * 3f, Shader.GetGlobalFloat("_SafeHavenTreeWind"), 1e-3f, "the storm bends trees harder");

        Object.DestroyImmediate(system.gameObject);
        Assert.AreEqual(calmTrees, Shader.GetGlobalFloat("_SafeHavenTreeWind"), 1e-3f, "wind back to normal without weather");
    }

    [Test]
    public void Weather_StormsFlashLightning()
    {
        WeatherSystem system = Make<WeatherSystem>("weather");
        system.SetRandomSeed(7);
        system.SetWeather(Weather("Storm", 2f, 1f, 1f, lightning: 6f), 0f);
        float brightest = 0f;
        for (int i = 0; i < 600; i++)
        {
            system.Tick(0.1f);
            brightest = Mathf.Max(brightest, system.LightningFlash);
        }

        Assert.Greater(brightest, 0.5f);
    }

    [Test]
    public void Climate_PicksByChanceAndChangesWhenTimeIsUp()
    {
        WeatherDefinition clear = Weather("Clear", 1f, 0f, 0f, minutes: 0.1f);
        WeatherDefinition rainy = Weather("Rain", 1.5f, 0.7f, 0.8f);
        ClimateDefinition climate = Asset<ClimateDefinition>();
        climate.Configure(clear, 0f, new ClimateDefinition.Entry { weather = clear, chance = 1f },
                          new ClimateDefinition.Entry { weather = rainy, chance = 3f });
        Assert.AreSame(clear, climate.Pick(0.1f));
        Assert.AreSame(rainy, climate.Pick(0.5f));
        Assert.AreSame(rainy, climate.Pick(1f));

        ClimateDefinition onlyRain = Asset<ClimateDefinition>();
        onlyRain.Configure(clear, 0f, new ClimateDefinition.Entry { weather = rainy, chance = 1f });
        WeatherSystem system = Make<WeatherSystem>("weather");
        Wire(system, ("climate", onlyRain));
        system.SetWeather(clear, 0f); // lasts 6 seconds
        system.Tick(3f);
        Assert.AreSame(clear, system.Weather);
        system.Tick(4f);
        Assert.AreSame(rainy, system.Weather);
    }

    [Test]
    public void Climate_SameSeedGivesTheSameWeatherWhateverTheFrameRate()
    {
        // Co-op: every game on one raid seeds its weather from the run; frame rates differ, and storms flash every frame.
        WeatherDefinition clear = Asset<WeatherDefinition>();
        clear.Configure("Clear", 1f, 0.2f, 0f, 0f, 2f, Color.gray, 0f, 0.05f, 0.2f);
        WeatherDefinition rainy = Asset<WeatherDefinition>();
        rainy.Configure("Rain", 1.5f, 0.2f, 0.7f, 0.8f, 2f, Color.gray, 0f, 0.05f, 0.2f);
        WeatherDefinition storm = Asset<WeatherDefinition>();
        storm.Configure("Storm", 2f, 0.2f, 1f, 1f, 2f, Color.gray, 6f, 0.05f, 0.2f);
        ClimateDefinition climate = Asset<ClimateDefinition>();
        climate.Configure(clear, 0f, new ClimateDefinition.Entry { weather = clear, chance = 1f },
                          new ClimateDefinition.Entry { weather = rainy, chance = 1f },
                          new ClimateDefinition.Entry { weather = storm, chance = 1f });

        List<WeatherDefinition> Run(string name, float frame)
        {
            var seen = new List<WeatherDefinition>();
            WeatherSystem system = Make<WeatherSystem>(name);
            Wire(system, ("climate", climate));
            system.SetWeather(clear, 0f);
            system.SetRandomSeed(42); // as the raid's sky rig does once the weather has started
            system.Changed += seen.Add;
            for (float time = 0f; time < 90f; time += frame)
            {
                system.Tick(frame);
            }

            return seen;
        }

        List<WeatherDefinition> fast = Run("fast", 1f / 144f), slow = Run("slow", 1f / 25f);
        Assert.GreaterOrEqual(fast.Count, 6, "the climate changed the weather several times");
        CollectionAssert.AreEqual(fast, slow);
    }

    [Test]
    public void FindWeather_ByNameFromTheClimateOrWhatIsLoaded()
    {
        // Co-op: a visit follows the host's weather by name.
        WeatherDefinition clear = Weather("Clear", 1f, 0f, 0f);
        clear.name = "Probe Clear";
        WeatherDefinition rainy = Weather("Rain", 1.5f, 0.7f, 0.8f);
        rainy.name = "Probe Rain";
        WeatherDefinition corruption = Weather("Corruption", 2f, 0.2f, 1f);
        corruption.name = "Probe Corruption Haze";
        ClimateDefinition climate = Asset<ClimateDefinition>();
        climate.Configure(clear, 0f, new ClimateDefinition.Entry { weather = rainy, chance = 1f });
        WeatherSystem system = Make<WeatherSystem>("weather");
        Wire(system, ("climate", climate));

        Assert.AreSame(clear, system.FindWeather("Probe Clear"));
        Assert.AreSame(rainy, system.FindWeather("Probe Rain"));
        Assert.AreSame(corruption, system.FindWeather("Probe Corruption Haze"), "not the climate's, but loaded");
        Assert.IsNull(system.FindWeather("No Such Sky"));
        Assert.IsNull(system.FindWeather(""));
    }

    [Test]
    public void Setup_CreatesTheLooksAndWiresTheRig()
    {
        // First: a new scene unloads assets only scripts hold, such as the set below.
        TestScenes.StartClean();
        WeatherSetup.Set set = WeatherSetup.EnsureAssets();
        Assert.IsTrue(set.Night.TryGet(out ColorAdjustments night));
        Assert.IsTrue(night.saturation.overrideState);
        Assert.Less(night.saturation.value, 0f, "nights are less colourful");
        Assert.IsFalse(night.hueShift.overrideState, "only the chosen values override the scene's look");
        Assert.IsTrue(set.GoldenHour.TryGet(out WhiteBalance warm));
        Assert.Greater(warm.temperature.value, 0f);
        Assert.IsTrue(set.Overcast.Has<ColorAdjustments>());
        Assert.AreSame(set.Clear, set.Climate.Start);
        Assert.Greater(set.Storm.State.LightningPerMinute, 0f);
        Assert.IsNotNull(set.RainPrefab.GetComponent<RainEffect>());

        Light sun = Make<Light>("sun");
        sun.type = LightType.Directional;
        RenderSettings.sun = sun;
        GameObject rig = WeatherSetup.AddToScene(set);
        created.Add(rig);
        Assert.AreSame(rig, WeatherSetup.AddToScene(set), "running it again completes the same rig");
        var cycle = new SerializedObject(rig.GetComponentInChildren<DayNightCycle>());
        Assert.AreSame(sun, cycle.FindProperty("sun").objectReferenceValue);
        Assert.AreSame(set.DayNight, cycle.FindProperty("profile").objectReferenceValue);
        var weather = new SerializedObject(rig.GetComponentInChildren<WeatherSystem>());
        Assert.IsNotNull(weather.FindProperty("rain").objectReferenceValue);
        Assert.IsNotNull(weather.FindProperty("overcastVolume").objectReferenceValue);
        Assert.AreEqual(3, rig.GetComponentsInChildren<Volume>().Length);
    }

    // Docs/LookAndFeelResearch_2026-11-18.md V3: from 20 m up the streaks barely read, so a share of the splashes lands round the player.
    [Test]
    public void ShareOfTheSplashesLandsRoundThePlayer()
    {
        var player = new Vector2(3f, -2f);
        for (int i = 0; i < 200; i++)
        {
            Assert.LessOrEqual(Vector2.Distance(RainEffect.SplashOffset(player, 1f, 6f, 18f), player), 6.001f, "all of them near: within Near Reach");
            Assert.LessOrEqual(RainEffect.SplashOffset(player, 0f, 6f, 18f).magnitude, 18.001f, "none near: anywhere in the view's reach");
        }

        int near = 0;
        for (int i = 0; i < 2000; i++)
        {
            near += Vector2.Distance(RainEffect.SplashOffset(player, 0.5f, 6f, 18f), player) <= 6f ? 1 : 0;
        }

        Assert.That(near / 2000f, Is.InRange(0.5f, 0.62f), "half go near, and a few of the rest land there too");
    }
}
