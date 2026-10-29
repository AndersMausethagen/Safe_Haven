using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AtmosphereTests
{
    [Test]
    public void SkyController_MistCombinationsPreserveBaseWeatherAndBlendWind()
    {
        var set = WeatherSetup.EnsureAssets();
        foreach (var pair in new[] { (set.Breezy, set.BreezyMist), (set.Storm, set.StormMist) })
        {
            WeatherState expected = pair.Item1.State;
            expected.Mist = set.Mist.State.Mist;
            Assert.AreEqual(expected, pair.Item2.State, "Only Mist should change, not the wind, rain, fog or lightning.");
        }
        var go = new GameObject("Mist combination test");
        float oldWind = AtmosphereConditions.WeatherWind, oldMist = AtmosphereConditions.WeatherMist;
        float oldFog = AtmosphereConditions.WeatherFog;
        bool oldMode = AtmosphereConditions.WeatherOnlyAtmosphere;
        try
        {
            var weather = go.AddComponent<WeatherSystem>();
            weather.ChangesByItself = false;
            weather.SetWeather(set.BreezyMist, 0f);
            Assert.AreEqual(set.Breezy.State.Wind, AtmosphereConditions.WeatherWind);
            Assert.AreEqual(set.Mist.State.Mist, AtmosphereConditions.WeatherMist);
            weather.SetWeather(set.StormMist, 10f);
            weather.Tick(5f);
            Assert.AreEqual(Mathf.Lerp(set.Breezy.State.Wind, set.Storm.State.Wind, 0.5f),
                AtmosphereConditions.WeatherWind, 0.0001f);
            Assert.AreEqual(set.Mist.State.Mist, AtmosphereConditions.WeatherMist);
            weather.Tick(5f);
            Assert.AreEqual(set.StormMist.State, weather.Current);
            var method = typeof(SkyControllerWindow).GetMethod("AllWeathers",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            object[] args = { weather, null };
            var choices = (System.Collections.Generic.List<WeatherDefinition>)method.Invoke(null, args);
            Assert.Contains(set.BreezyMist, choices);
            Assert.Contains(set.StormMist, choices);
            Assert.IsFalse(set.Climate.Weathers.Any(e => e.weather == set.BreezyMist || e.weather == set.StormMist),
                "Preview combinations must not change random weather probabilities.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            AtmosphereConditions.WeatherWind = oldWind;
            AtmosphereConditions.WeatherMist = oldMist;
            AtmosphereConditions.WeatherFog = oldFog;
            AtmosphereConditions.WeatherOnlyAtmosphere = oldMode;
        }
    }

    [Test]
    public void MistHabitat_BakesHollowsAndActualWaterSurfaceAboveLakeBed()
    {
        Scene original = SceneManager.GetActiveScene();
        FogGround previousGround = FogGround.Current;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(40f, 10f, 40f) };
        try
        {
            SceneManager.SetActiveScene(scene);
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++)
                for (int x = 0; x < 33; x++)
                {
                    float dx = x * 40f / 32f - 20f, dz = z * 40f / 32f - 20f;
                    heights[z, x] = (dx * dx + dz * dz) * 0.001f;
                }
            data.SetHeights(0, 0, heights);
            Terrain.CreateTerrainGameObject(data);
            var waterObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waterObject.transform.position = new Vector3(20f, 1.95f, 20f);
            waterObject.transform.localScale = new Vector3(8f, 0.1f, 8f);
            var water = waterObject.AddComponent<WaterSurface>();
            var ground = new GameObject("Habitat").AddComponent<FogGround>();
            ground.Bake();
            Color centre = ground.Texture.GetPixel(9, 9);
            Assert.AreEqual(TextureFormat.RGBAHalf, ground.Texture.format);
            Assert.Less(centre.r * ground.HeightRange.y, 0.3f, "Lake bed remains the real fog ground.");
            Assert.AreEqual(2f, centre.g * ground.HeightRange.y + ground.HeightRange.x, 0.01f);
            Assert.AreEqual(1f, centre.b, "Mist must identify the actual water footprint.");
            Assert.Greater(centre.a, 1f, "The hollow must accumulate more mist than its surroundings.");
            Color dry = ground.Texture.GetPixel(0, 0);
            Assert.AreEqual(0f, dry.b);
            Assert.AreEqual(dry.r, dry.g, 0.001f, "Dry mist still follows terrain, not a global water plane.");
            water.enabled = false;
            ground.Bake();
            Assert.AreEqual(0f, ground.Texture.GetPixel(9, 9).b);
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(scene, true);
            Object.DestroyImmediate(data);
            if (previousGround != null) previousGround.SendMessage("OnEnable");
        }
    }

    [Test]
    public void MistVeils_DefaultToVisibleDriftAndCoexistWithStorm()
    {
        float oldWind = AtmosphereConditions.WeatherWind, oldMist = AtmosphereConditions.WeatherMist;
        float oldFog = AtmosphereConditions.WeatherFog;
        bool oldMode = AtmosphereConditions.WeatherOnlyAtmosphere;
        var go = new GameObject("Storm veil test");
        var terrainObject = new GameObject("Veil terrain");
        try
        {
            var mist = terrainObject.AddComponent<TerrainMist>();
            var system = go.AddComponent<WeatherSystem>();
            system.ChangesByItself = false;
            var mistWeather = AssetDatabase.LoadAssetAtPath<WeatherDefinition>(WeatherSetup.WeatherFolder + "/Mist.asset");
            var storm = AssetDatabase.LoadAssetAtPath<WeatherDefinition>(WeatherSetup.WeatherFolder + "/Storm.asset");
            Assert.IsNotNull(mistWeather);
            Assert.IsNotNull(storm);
            Assert.AreEqual(new Vector2(1.75f, 0.625f), mist.Wind);
            Assert.AreEqual(3.5f, mist.FlowStretch);
            system.SetWeather(mistWeather, 0f);
            float calmSpeed = mist.Wind.magnitude * AtmosphereConditions.WeatherWind;
            Assert.Greater(calmSpeed, 0.7f);
            system.SetWeather(storm, 0f);
            Assert.Greater(mist.EffectiveDensity, 0f, "Storm and mist must coexist.");
            Assert.Greater(AtmosphereConditions.WeatherFog, 0f);
            Assert.Greater(mist.Wind.magnitude * AtmosphereConditions.WeatherWind, calmSpeed * 5f);
            var settings = new SerializedObject(mist);
            settings.FindProperty("driftSpeed").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(Vector2.zero, mist.Wind, "Designers can stop drift and internal billowing.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(terrainObject);
            AtmosphereConditions.WeatherWind = oldWind;
            AtmosphereConditions.WeatherMist = oldMist;
            AtmosphereConditions.WeatherFog = oldFog;
            AtmosphereConditions.WeatherOnlyAtmosphere = oldMode;
        }
    }

    [Test]
    public void WeatherOnlyAtmosphere_ClearRemovesFogAndGoldenMistWhileTransitionsStaySmooth()
    {
        float oldFog = AtmosphereConditions.WeatherFog, oldMist = AtmosphereConditions.WeatherMist;
        float oldGolden = AtmosphereConditions.GoldenHour, oldWind = AtmosphereConditions.WeatherWind;
        bool oldMode = AtmosphereConditions.WeatherOnlyAtmosphere;
        var clear = ScriptableObject.CreateInstance<WeatherDefinition>();
        var mistWeather = ScriptableObject.CreateInstance<WeatherDefinition>();
        var fogWeather = ScriptableObject.CreateInstance<WeatherDefinition>();
        var go = new GameObject("Weather-only test");
        var terrainObject = new GameObject("Weather-only terrain");
        try
        {
            clear.Configure("Clear", 1f, 0f, 0f, 0f, 0f, Color.white, 0f, 1f, 1f);
            mistWeather.Configure("Mist", 1f, 0f, 0f, 0f, 0f, Color.white, 0f, 1f, 1f, 1f);
            fogWeather.Configure("Fog", 1f, 0f, 0f, 0f, 2.2f, Color.white, 0f, 1f, 1f);
            var system = go.AddComponent<WeatherSystem>();
            system.ChangesByItself = false;
            var mist = terrainObject.AddComponent<TerrainMist>();
            var settings = new SerializedObject(mist);
            settings.FindProperty("baseAmount").floatValue = 1f;
            settings.FindProperty("goldenHourAmount").floatValue = 1f;
            settings.ApplyModifiedProperties();
            AtmosphereConditions.GoldenHour = 1f;
            system.SetWeather(clear, 0f);
            Assert.IsTrue(AtmosphereConditions.WeatherOnlyAtmosphere);
            Assert.AreEqual(0f, mist.EffectiveDensity, "Even dawn and legacy always-on mist must clear.");
            Assert.AreEqual(0f, AtmosphereConditions.FogMultiplier(0.5f), "Partial legacy influence must not leave a fog floor.");
            system.SetWeather(mistWeather, 10f);
            system.Tick(5f);
            Assert.AreEqual(mist.Density * 0.5f, mist.EffectiveDensity, 0.0001f);
            Assert.AreEqual(0f, AtmosphereConditions.FogMultiplier(0.5f), "Mist can arrive without global fog.");
            system.Tick(5f);
            Assert.AreEqual(mist.Density, mist.EffectiveDensity);
            system.SetWeather(fogWeather, 10f);
            system.Tick(10f);
            Assert.AreEqual(0f, mist.EffectiveDensity, "Fog can arrive without ground mist.");
            Assert.AreEqual(2.2f, AtmosphereConditions.FogMultiplier(0.5f), 0.0001f);
            system.SetWeather(clear, 10f);
            system.Tick(10f);
            Assert.AreEqual(0f, AtmosphereConditions.FogMultiplier(0.5f));
            Assert.AreEqual(0f, mist.EffectiveDensity);
            system.enabled = false;
            Assert.IsFalse(AtmosphereConditions.WeatherOnlyAtmosphere);
            Assert.AreEqual(mist.Density, mist.EffectiveDensity, "Authored mist remains available without weather control.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(terrainObject);
            Object.DestroyImmediate(clear);
            Object.DestroyImmediate(mistWeather);
            Object.DestroyImmediate(fogWeather);
            AtmosphereConditions.WeatherFog = oldFog;
            AtmosphereConditions.WeatherMist = oldMist;
            AtmosphereConditions.GoldenHour = oldGolden;
            AtmosphereConditions.WeatherWind = oldWind;
            AtmosphereConditions.WeatherOnlyAtmosphere = oldMode;
        }
    }

    [Test]
    public void WeatherWind_IntegratesSpeedWithoutPhaseJumpsOrMultipleCameraDoubleTicks()
    {
        float oldWind = AtmosphereConditions.WeatherWind, oldDensity = AtmosphereConditions.FogDensity;
        var material = new Material(Shader.Find("Hidden/Safe Haven/Atmosphere"));
        try
        {
            var type = typeof(AtmosphereFeature).GetNestedType("AtmospherePass", System.Reflection.BindingFlags.NonPublic);
            object pass = System.Activator.CreateInstance(type, new object[] { material });
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var lastTime = type.GetField("lastWindTime", flags);
            var travel = type.GetField("extraWindTime", flags);
            var upload = type.GetMethod("UploadParameters", flags);
            AtmosphereConditions.WeatherWind = 2.8f;
            lastTime.SetValue(pass, Time.time - 2f);
            upload.Invoke(pass, new object[] { Vector3.zero });
            float stormTravel = (float)travel.GetValue(pass);
            Assert.AreEqual(3.6f, stormTravel, 0.002f, "Two seconds at 2.8x adds 3.6 extra seconds of drift.");
            upload.Invoke(pass, new object[] { Vector3.zero });
            Assert.AreEqual(stormTravel, (float)travel.GetValue(pass), 0.002f, "Second camera at same game time must not accelerate drift.");
            AtmosphereConditions.WeatherWind = 0.4f;
            upload.Invoke(pass, new object[] { Vector3.zero });
            Assert.AreEqual(stormTravel, (float)travel.GetValue(pass), 0.002f, "A new target wind cannot teleport the cloud phase.");
            lastTime.SetValue(pass, Time.time - 1f);
            upload.Invoke(pass, new object[] { Vector3.zero });
            Assert.AreEqual(stormTravel - 0.6f, (float)travel.GetValue(pass), 0.002f);
        }
        finally
        {
            Object.DestroyImmediate(material);
            AtmosphereConditions.WeatherWind = oldWind;
            AtmosphereConditions.FogDensity = oldDensity;
        }
    }

    [Test]
    public void MistAmount_CanBeWeatherOnlyGoldenOnlyOrAlongsideFog()
    {
        Assert.AreEqual(0f, TerrainMist.EvaluateAmount(0f, 1f, 1f, 0f, 0f));
        Assert.AreEqual(1f, TerrainMist.EvaluateAmount(0f, 1f, 0f, 1f, 0f));
        Assert.AreEqual(0.5f, TerrainMist.EvaluateAmount(0f, 0f, 1f, 0f, 0.5f));
        Assert.AreEqual(1f, TerrainMist.EvaluateAmount(1f, 0f, 0f, 0f, 0f), "Always-on authored layers remain supported.");
        Assert.AreEqual(0.7f, TerrainMist.EvaluateAmount(0.2f, 1f, 1f, 0.7f, 0.5f), 0.0001f);
        var profile = ScriptableObject.CreateInstance<DayNightProfile>();
        try
        {
            Assert.Greater(TerrainMist.EvaluateAmount(0f, 0f, 1f, 0f, profile.Evaluate(6.5f).GoldenHour), 0.9f);
            Assert.AreEqual(0f, TerrainMist.EvaluateAmount(0f, 0f, 1f, 0f, profile.Evaluate(13f).GoldenHour));
        }
        finally { Object.DestroyImmediate(profile); }
    }

    [Test]
    public void Weather_PublishesIndependentMistAndWindWithoutDayNightRig()
    {
        float oldFog = AtmosphereConditions.WeatherFog, oldWind = AtmosphereConditions.WeatherWind;
        float oldMist = AtmosphereConditions.WeatherMist;
        bool oldMode = AtmosphereConditions.WeatherOnlyAtmosphere;
        var go = new GameObject("Mist weather test") { hideFlags = HideFlags.HideAndDontSave };
        var storm = ScriptableObject.CreateInstance<WeatherDefinition>();
        var mistOnly = ScriptableObject.CreateInstance<WeatherDefinition>();
        try
        {
            storm.Configure("Storm", 2.8f, 0.5f, 1f, 1f, 2.2f, Color.gray, 0f, 2f, 4f, 0.4f);
            mistOnly.Configure("Ground Mist", 0.4f, 0f, 0f, 0f, 0f, Color.white, 0f, 2f, 4f, 1f);
            var system = go.AddComponent<WeatherSystem>();
            system.ChangesByItself = false;
            system.SetWeather(storm, 0f);
            Assert.AreEqual(2.8f, AtmosphereConditions.WeatherWind, 0.0001f);
            Assert.AreEqual(0.4f, AtmosphereConditions.WeatherMist, 0.0001f);
            system.SetWeather(mistOnly, 10f);
            Assert.AreEqual(2.8f, AtmosphereConditions.WeatherWind, 0.0001f, "Changing target must not jump the wind.");
            system.Tick(5f);
            Assert.AreEqual(1.6f, AtmosphereConditions.WeatherWind, 0.0001f);
            Assert.AreEqual(0.7f, AtmosphereConditions.WeatherMist, 0.0001f);
            system.Tick(5f);
            Assert.AreEqual(0f, AtmosphereConditions.WeatherFog);
            Assert.AreEqual(1f, AtmosphereConditions.WeatherMist);
            Assert.AreEqual(0f, mistOnly.State.Fog, "Authored shared data stays unchanged.");
            system.enabled = false;
            Assert.AreEqual(1f, AtmosphereConditions.WeatherWind);
            Assert.AreEqual(0f, AtmosphereConditions.WeatherMist);
            Assert.AreEqual(1f, AtmosphereConditions.WeatherFog);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(storm);
            Object.DestroyImmediate(mistOnly);
            AtmosphereConditions.WeatherFog = oldFog;
            AtmosphereConditions.WeatherWind = oldWind;
            AtmosphereConditions.WeatherMist = oldMist;
            AtmosphereConditions.WeatherOnlyAtmosphere = oldMode;
        }
    }

    [Test]
    public void TorchClearing_RemovesRenderedExtinctionAndScattering()
    {
        var material = new Material(Shader.Find("Hidden/Safe Haven/Atmosphere"));
        var target = new RenderTexture(16, 16, 0, RenderTextureFormat.ARGBFloat);
        var pixels = new Texture2D(16, 16, TextureFormat.RGBAFloat, false, true);
        RenderTexture previousTarget = RenderTexture.active;
        Texture previousDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
        Vector4 previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
        Matrix4x4 previousInverse = Shader.GetGlobalMatrix("unity_MatrixInvVP");
        try
        {
            Shader.SetGlobalTexture("_CameraDepthTexture", SystemInfo.usesReversedZBuffer ? Texture2D.blackTexture : Texture2D.whiteTexture);
            Shader.SetGlobalVector("_WorldSpaceCameraPos", Vector4.zero);
            Shader.SetGlobalMatrix("unity_MatrixInvVP", Matrix4x4.identity);
            material.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));
            material.SetVector("_FogParams0", new Vector4(0.1f, 100f, 0f, 0f));
            material.SetVector("_FogParams1", Vector4.zero);
            material.SetVector("_FogParams2", new Vector4(10f, 32f, 0f, 0f));
            material.SetColor("_FogAmbient", Color.white);
            material.SetColor("_FogAlbedo", Color.white);
            material.SetFloat("_FogLightCount", 1f);
            material.SetVectorArray("_FogLightPositions", new Vector4[AtmosphereFeature.MaxLights]);
            material.SetVectorArray("_FogLightColors", new Vector4[AtmosphereFeature.MaxLights]);
            var clearings = new Vector4[AtmosphereFeature.MaxLights];
            material.SetVectorArray("_FogLightClearings", clearings);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Color fog = pixels.GetPixel(8, 8);
            Assert.AreEqual(Mathf.Exp(-1f), fog.a, 0.015f, "10 metres of extinction 0.1/m.");
            Assert.Greater(fog.r, 0.5f, "Fog must scatter before clearing.");
            clearings[0] = new Vector4(100f, 1f, 0.6f, 0f);
            material.SetVectorArray("_FogLightClearings", clearings);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Color cleared = pixels.GetPixel(8, 8);
            Assert.AreEqual(1f, cleared.a, 0.002f, "Clearing removes medium, not just its lighting.");
            Assert.AreEqual(0f, cleared.r, 0.002f);
            Assert.AreEqual(0f, cleared.g, 0.002f);
            Assert.AreEqual(0f, cleared.b, 0.002f);

            clearings[0].y = 0.5f;
            material.SetVectorArray("_FogLightClearings", clearings);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Assert.AreEqual(Mathf.Exp(-0.5f), pixels.GetPixel(8, 8).a, 0.015f);

            clearings[0].y = 0f;
            material.SetVectorArray("_FogLightClearings", clearings);
            material.SetVector("_FogParams1", new Vector4(0f, 0f, 0.1f, 0.7f));
            material.SetVector("_FogParams3", new Vector4(0f, 0.05f, 0.5f, 0.5f));
            material.SetFloat("_FogDriftSpeed", 0f);
            material.SetVector("_FogWind", new Vector4(1f, 0f, 0.5f, 0f));
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Color[] stationaryFog = pixels.GetPixels();
            material.SetVector("_FogWind", new Vector4(1f, 0f, 0.5f, 1000f));
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Color[] laterFog = pixels.GetPixels();
            for (int i = 0; i < stationaryFog.Length; i++)
                Assert.AreEqual(stationaryFog[i].a, laterFog[i].a, 0.00001f, "Fog clumps and banks must stay stationary even when weather advances.");
            material.SetVector("_FogParams1", Vector4.zero);
            material.SetVector("_FogParams3", Vector4.zero);
            material.SetVector("_FogParams0", Vector4.zero);
            material.SetInt("_TerrainMistCount", 1);
            material.SetTexture("_GroundTex", Texture2D.blackTexture);
            material.SetVector("_GroundRect", new Vector4(-100f, -100f, 0.005f, 0.005f));
            material.SetVector("_GroundRange", Vector4.zero);
            var rects = new Vector4[AtmosphereFeature.MaxMists];
            var shapes = new Vector4[AtmosphereFeature.MaxMists];
            var clouds = new Vector4[AtmosphereFeature.MaxMists];
            var tints = new Vector4[AtmosphereFeature.MaxMists];
            rects[0] = new Vector4(-100f, -100f, 200f, 200f);
            shapes[0] = new Vector4(0.2f, -100f, 200f, 1f);
            clouds[0] = new Vector4(0.05f, 1f, 0f, 0f);
            tints[0] = Vector4.one;
            material.SetVectorArray("_TerrainMistRects", rects);
            material.SetVectorArray("_TerrainMistShapes", shapes);
            material.SetVectorArray("_TerrainMistClouds", clouds);
            material.SetVectorArray("_TerrainMistTints", tints);
            material.SetVectorArray("_TerrainMistWinds", new Vector4[AtmosphereFeature.MaxMists]);
            clearings[0].y = 0f;
            material.SetVectorArray("_FogLightClearings", clearings);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Assert.Less(pixels.GetPixel(8, 8).a, 0.98f, "Independent terrain mist is present before clearing.");
            clearings[0].y = 1f;
            material.SetVectorArray("_FogLightClearings", clearings);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Assert.AreEqual(1f, pixels.GetPixel(8, 8).a, 0.002f, "Torch also removes the independent terrain mist.");
            Assert.AreEqual(0f, pixels.GetPixel(8, 8).r, 0.002f, "Cleared mist must not leave emissive scattering.");

            clearings[0].y = 0f;
            material.SetVectorArray("_FogLightClearings", clearings);
            clouds[0] = new Vector4(0.1f, 0.65f, 0.35f, 3.5f);
            material.SetVectorArray("_TerrainMistClouds", clouds);
            material.SetVector("_FogParams2", new Vector4(40f, 48f, 0f, 0f));
            Shader.SetGlobalMatrix("unity_MatrixInvVP", Matrix4x4.Scale(new Vector3(32f, 32f, 1f)));
            var winds = new Vector4[AtmosphereFeature.MaxMists];
            winds[0] = new Vector4(1.75f, 0.625f, 0f, 0f);
            material.SetVectorArray("_TerrainMistWinds", winds);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Color[] before = pixels.GetPixels();
            winds[0].z = 8f;
            material.SetVectorArray("_TerrainMistWinds", winds);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            Color[] after = pixels.GetPixels();
            float change = 0f;
            for (int i = 0; i < before.Length; i++)
                change += Mathf.Abs(before[i].a - after[i].a);
            Assert.Greater(change / before.Length, 0.01f, "Veil drift must visibly change rendered extinction over eight seconds.");

            shapes[0] = new Vector4(0.12f, 0.03f, 2f, 1f);
            material.SetVectorArray("_TerrainMistShapes", shapes);
            float[] extinction = new float[3];
            float[] heights = { 0.25f, 1.8f, 2.1f };
            for (int h = 0; h < heights.Length; h++)
            {
                Shader.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(0f, heights[h], 0f, 0f));
                Shader.SetGlobalMatrix("unity_MatrixInvVP", Matrix4x4.TRS(new Vector3(0f, heights[h], 0f),
                    Quaternion.identity, new Vector3(32f, 0f, 1f)));
                Graphics.Blit(Texture2D.blackTexture, target, material, 0);
                pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                extinction[h] = pixels.GetPixels().Average(c => 1f - c.a);
            }
            Assert.Greater(extinction[0], 0.05f, "Mist must have a dense ground core.");
            Assert.Greater(extinction[0], extinction[1] * 10f, "Mist must be much denser at ankle height than near the top.");
            Assert.AreEqual(0f, extinction[2], 0.0001f, "Mist must not fill the air above its finite ground layer.");

            var waves = new Vector4[AtmosphereFeature.MaxMists];
            waves[0] = new Vector4(0.5f, 12f, 0.6f, 0f);
            material.SetVectorArray("_TerrainMistWaves", waves);
            material.SetVector("_FogParams2", new Vector4(10f, 48f, 0f, 0f));
            winds[0] = Vector4.zero;
            material.SetVectorArray("_TerrainMistWinds", winds);
            Shader.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(0f, 0.25f, 0f, 0f));
            Shader.SetGlobalMatrix("unity_MatrixInvVP", Matrix4x4.TRS(new Vector3(0f, 0.25f, 0f),
                Quaternion.identity, new Vector3(10f, 0f, 1f)));
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            before = pixels.GetPixels();
            winds[0].z = 6f;
            material.SetVectorArray("_TerrainMistWinds", winds);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            after = pixels.GetPixels();
            change = 0f;
            for (int i = 0; i < before.Length; i++)
                change += Mathf.Abs(before[i].a - after[i].a);
            Assert.Greater(change / before.Length, 0.005f, "Calm waves must be visible independently of cloud translation.");

            winds[0].z = 20f;
            material.SetVectorArray("_TerrainMistWinds", winds);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            after = pixels.GetPixels();
            change = 0f;
            for (int i = 0; i < before.Length; i++)
                change += Mathf.Abs(before[i].a - after[i].a);
            Assert.Greater(change / before.Length, 0.005f, "Mist must not repeat after the former 12m / 0.6m/s wave period.");
            winds[0] = new Vector4(1.75f, 0.625f, 6f, 0f);
            material.SetVectorArray("_TerrainMistWinds", winds);
            Graphics.Blit(Texture2D.blackTexture, target, material, 0);
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            before = pixels.GetPixels();
            foreach (float agitation in new[] { 1.2f, 2.4f })
            {
                winds[0].w = agitation;
                material.SetVectorArray("_TerrainMistWinds", winds);
                Graphics.Blit(Texture2D.blackTexture, target, material, 0);
                pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                after = pixels.GetPixels();
                change = 0f;
                for (int i = 0; i < before.Length; i++)
                    change += Mathf.Abs(before[i].a - after[i].a);
                Assert.Greater(change / before.Length, 0.005f, "Breezy/Storm must change mist shape, not just translate it faster.");
            }

            var habitat = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true);
            try
            {
                material.SetVectorArray("_TerrainMistWaves", new Vector4[AtmosphereFeature.MaxMists]);
                clouds[0] = new Vector4(0.1f, 1f, 0f, 3.5f);
                material.SetVectorArray("_TerrainMistClouds", clouds);
                habitat.SetPixel(0, 0, new Color(0f, 0.5f, 1f, 2f));
                habitat.Apply();
                material.SetTexture("_GroundTex", habitat);
                material.SetVector("_GroundRange", new Vector4(0f, 10f, 0f, 0f));
                material.SetFloat("_HasMistHabitat", 1f);
                Shader.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(0f, 5.25f, 0f, 0f));
                Shader.SetGlobalMatrix("unity_MatrixInvVP", Matrix4x4.TRS(new Vector3(0f, 5.25f, 0f),
                    Quaternion.identity, new Vector3(32f, 0f, 1f)));
                Graphics.Blit(Texture2D.blackTexture, target, material, 0);
                pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                float unpooled = pixels.GetPixels().Average(c => 1f - c.a);
                var pooling = new Vector4[AtmosphereFeature.MaxMists];
                pooling[0] = new Vector4(1.5f, 2f, 2f, 0f);
                material.SetVectorArray("_TerrainMistPooling", pooling);
                Graphics.Blit(Texture2D.blackTexture, target, material, 0);
                pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                float pooled = pixels.GetPixels().Average(c => 1f - c.a);
                Assert.Greater(unpooled, 0.05f, "Mist must render above the water surface, not the lake bed five metres below.");
                Assert.Greater(pooled, unpooled + 0.03f, "Water and hollows must visibly accumulate denser mist.");
            }
            finally { Object.DestroyImmediate(habitat); }
        }
        finally
        {
            Shader.SetGlobalTexture("_CameraDepthTexture", previousDepth);
            Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
            Shader.SetGlobalMatrix("unity_MatrixInvVP", previousInverse);
            RenderTexture.active = previousTarget;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void ExpeditionTorch_AddsAtmosphereToItsPointLights()
    {
        var go = new GameObject("Expedition torch test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            var torch = go.AddComponent<ExpeditionTorch>();
            torch.SendMessage("Awake");
            torch.SendMessage("Awake");
            Assert.AreEqual(1, go.GetComponents<AtmosphereLight>().Length);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TorchClearing_FollowsLightStateAndRangeIndependentlyOfFogGlow()
    {
        var go = new GameObject("Atmosphere light test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 2f;
            var atmosphere = go.AddComponent<AtmosphereLight>();
            Assert.AreEqual(4f, atmosphere.ClearingRadius);
            Assert.AreEqual(1f, atmosphere.ClearingStrength);
            light.range = 2f;
            Assert.AreEqual(2f, atmosphere.ClearingRadius);
            light.intensity = 0.25f;
            Assert.AreEqual(0.25f, atmosphere.ClearingStrength);
            light.enabled = false;
            Assert.AreEqual(0f, atmosphere.ClearingStrength);
            light.enabled = true;
            light.intensity = 0f;
            Assert.AreEqual(0f, atmosphere.ClearingStrength);
            light.intensity = 2f;
            light.color = Color.black;
            Assert.AreEqual(0f, atmosphere.ClearingStrength);
            light.color = Color.white;
            light.type = LightType.Spot;
            Assert.AreEqual(0f, atmosphere.ClearingStrength, "Do not clear outside a spotlight cone.");
            light.type = LightType.Point;
            var settings = new SerializedObject(atmosphere);
            settings.FindProperty("fogStrength").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(1f, atmosphere.ClearingStrength);
            settings.FindProperty("clearingStrength").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(0f, atmosphere.ClearingStrength);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Shafts_CanRunWithoutChangingBaseFog()
    {
        var fog = ScriptableObject.CreateInstance<AtmosphericFog>();
        try
        {
            Assert.IsFalse(fog.IsActive());
            fog.shaftDensity.value = 0.004f;
            Assert.IsTrue(fog.IsActive());
            Assert.AreEqual(0f, fog.density.value);
            fog.active = false;
            Assert.IsFalse(fog.IsActive());
        }
        finally
        {
            Object.DestroyImmediate(fog);
        }
    }

    [Test]
    public void TerrainMist_RegistersAndMeasuresDistanceToTileNotOrigin()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(100f, 20f, 80f) };
        var go = Terrain.CreateTerrainGameObject(data);
        try
        {
            go.transform.position = new Vector3(10f, 0f, -20f);
            var mist = go.AddComponent<TerrainMist>();
            Assert.Contains(mist, TerrainMist.Active.ToArray());
            Assert.AreEqual(0f, mist.DistanceSquared(new Vector3(90f, 0f, 50f)));
            Assert.AreEqual(25f, mist.DistanceSquared(new Vector3(115f, 0f, 50f)));
            Assert.AreEqual(200f, mist.DistanceSquared(new Vector3(120f, 0f, 70f)));
            mist.enabled = false;
            Assert.IsFalse(TerrainMist.Active.Contains(mist));
            mist.enabled = true;
            Assert.AreEqual(1, TerrainMist.Active.Count(m => m == mist));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(testScene, true);
        }
    }

    [Test]
    public void AtmosphereShader_CompilesAllPassesAndShadowVariants()
    {
        var shader = Shader.Find("Hidden/Safe Haven/Atmosphere");
        var material = new Material(shader);
        try
        {
            foreach (string keyword in new[] { "", "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN" })
            {
                material.shaderKeywords = string.IsNullOrEmpty(keyword) ? new string[0] : new[] { keyword };
                Assert.IsTrue(material.SetPass(0), keyword);
                Assert.IsTrue(material.SetPass(1), keyword);
            }

            Assert.IsEmpty(ShaderUtil.GetShaderMessages(shader)
                .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                .Select(m => m.message).ToArray());
        }
        finally
        {
            Object.DestroyImmediate(material);
        }
    }
}
