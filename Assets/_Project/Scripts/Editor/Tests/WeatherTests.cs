using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class WeatherTests
{
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
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
            {
                Object.DestroyImmediate(created[i]);
            }
        }

        created.Clear();
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

    [Test]
    public void Profile_SunByDayMoonByNight()
    {
        DayNightProfile profile = Asset<DayNightProfile>();
        DayNightState noon = profile.Evaluate(13f);
        Assert.IsFalse(noon.IsMoon);
        Assert.AreEqual(55f, noon.LightRotation.eulerAngles.x, 0.5f, "the sun is at its noon height");
        Assert.AreEqual(2.3f, noon.LightIntensity, 0.05f);
        Assert.AreEqual(1f, noon.Daylight, 1e-4f);
        Assert.AreEqual(0f, noon.Night, 1e-3f);

        DayNightState midnight = profile.Evaluate(24f);
        Assert.IsTrue(midnight.IsMoon);
        Assert.AreEqual(1f, midnight.Night, 1e-3f);
        Assert.Greater(midnight.LightIntensity, 0.3f, "moonlight keeps the night readable");
        Assert.Less(midnight.AmbientSky.grayscale, noon.AmbientSky.grayscale * 0.6f);

        Assert.AreEqual(0f, profile.Evaluate(profile.Sunrise).LightIntensity, 1e-3f, "the light fades out at the horizon");
        Assert.Greater(profile.Evaluate(19.5f).GoldenHour, 0.9f);
        Assert.Greater(profile.Evaluate(6.5f).FogDensity, noon.FogDensity, "morning mist");
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
    public void Setup_CreatesTheLooksAndWiresTheRig()
    {
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
}
