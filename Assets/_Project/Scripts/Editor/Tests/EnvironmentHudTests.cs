using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnvironmentHudTests
{
    [TestCase(0f, "Night")]
    [TestCase(5.999f, "Night")]
    [TestCase(6f, "Dawn")]
    [TestCase(7.999f, "Dawn")]
    [TestCase(8f, "Day")]
    [TestCase(17.999f, "Day")]
    [TestCase(18f, "Dusk")]
    [TestCase(19.999f, "Dusk")]
    [TestCase(20f, "Night")]
    [TestCase(24f, "Night")]
    [TestCase(-1f, "Night")]
    public void Phase_ChangesAtProfileBoundaries(float hour, string expected)
    {
        Assert.AreEqual(expected, EnvironmentHudView.PhaseAt(hour, 6f, 20f));
    }

    [Test]
    public void Phase_FollowsAuthoredSunriseAndSunset()
    {
        Assert.AreEqual("Night", EnvironmentHudView.PhaseAt(7f, 8f, 16f));
        Assert.AreEqual("Dawn", EnvironmentHudView.PhaseAt(9f, 8f, 16f));
        Assert.AreEqual("Day", EnvironmentHudView.PhaseAt(10f, 8f, 16f));
        Assert.AreEqual("Dusk", EnvironmentHudView.PhaseAt(14f, 8f, 16f));
        Assert.AreEqual("Night", EnvironmentHudView.PhaseAt(16f, 8f, 16f));
        Assert.AreEqual(9f, EnvironmentHudView.Boundary(2, 8f, 12f), "short days retain a daytime segment");
    }

    [TestCase(0f, "00:00")]
    [TestCase(9.5f, "09:30")]
    [TestCase(23.999f, "23:59")]
    [TestCase(24f, "00:00")]
    [TestCase(25f, "01:00")]
    [TestCase(-0.5f, "23:30")]
    public void Clock_FormatsAndWraps(float hour, string expected)
    {
        Assert.AreEqual(expected, EnvironmentHudView.FormatHour(hour));
    }

    [TestCase("Clear", WeatherIconGraphic.Symbol.Sun)]
    [TestCase("Breezy", WeatherIconGraphic.Symbol.Wind)]
    [TestCase("Mist", WeatherIconGraphic.Symbol.Mist)]
    [TestCase("Fog", WeatherIconGraphic.Symbol.Mist)]
    [TestCase("Rain", WeatherIconGraphic.Symbol.Rain)]
    [TestCase("Storm", WeatherIconGraphic.Symbol.Storm)]
    public void Weather_UsesActualAuthoredConditions(string name, WeatherIconGraphic.Symbol expected)
    {
        WeatherDefinition weather = AssetDatabase.LoadAssetAtPath<WeatherDefinition>(
            WeatherSetup.WeatherFolder + "/" + name + ".asset");
        Assert.IsNotNull(weather);
        Assert.AreEqual(expected, WeatherIconGraphic.Classify(weather.State, false));
    }

    [Test]
    public void Weather_ClearNightUsesMoonAndBlendsUseVisibleConditions()
    {
        Assert.AreEqual(WeatherIconGraphic.Symbol.Moon, WeatherIconGraphic.Classify(WeatherState.Clear, true));
        WeatherState rain = WeatherState.Clear;
        rain.Rain = 1f;
        Assert.AreEqual(WeatherIconGraphic.Symbol.Sun,
            WeatherIconGraphic.Classify(WeatherState.Lerp(WeatherState.Clear, rain, 0f), false));
        Assert.AreEqual(WeatherIconGraphic.Symbol.Rain,
            WeatherIconGraphic.Classify(WeatherState.Lerp(WeatherState.Clear, rain, 0.5f), true));
        WeatherState clouds = WeatherState.Clear;
        clouds.Clouds = 0.8f;
        Assert.AreEqual(WeatherIconGraphic.Symbol.Cloud, WeatherIconGraphic.Classify(clouds, false));
    }

    [Test]
    public void Timeline_WrapsDotAtMidnightAndProducesGeometry()
    {
        var go = new GameObject("test timeline", typeof(RectTransform));
        var mesh = new Mesh();
        try
        {
            var track = go.AddComponent<EnvironmentTimelineGraphic>();
            ((RectTransform)go.transform).sizeDelta = new Vector2(480f, 10f);
            track.SetTime(12f, 6f, 20f);
            Assert.AreEqual(0.5f, track.NormalizedHour);
            track.SetTime(24f, 6f, 20f);
            Assert.AreEqual(0f, track.NormalizedHour);
            using (var vertices = new VertexHelper())
            {
                typeof(EnvironmentTimelineGraphic).GetMethod("OnPopulateMesh",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.DeclaredOnly)
                    .Invoke(track, new object[] { vertices });
                vertices.FillMesh(mesh);
                Assert.Greater(mesh.vertexCount, 80);
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mesh);
        }
    }
}
