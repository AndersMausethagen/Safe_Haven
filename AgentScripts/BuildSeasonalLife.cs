using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Seasons that change how the Haven is played (`unity command script run --file AgentScripts/BuildSeasonalLife.cs
/// --entry BuildSeasonalLife.Apply`): which forage and plants grow in which seasons (berries in summer, mushrooms in
/// autumn, little but sticks and stones in winter), and the festivals of the year (the Harvest Feast at the end of
/// autumn, the Midwinter Bonfire), with their bonfire, music (Tools/Audio/festival.py), lines and boons; fireflies by
/// season; geese going over in autumn and spring (Tools/Audio/seasons.py); the spirits that remember (SpiritBonds); and
/// the blight's hollowed grazers among the herds. Re-runnable.
/// The scene part saves the scene only if it was clean when this began.
/// </summary>
public static class BuildSeasonalLife
{
    private const string FestivalData = "Assets/_Project/Data/World/Festivals";
    private const string Music = "Assets/_Project/Audio/Music/Safe Haven Festivals/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Living";
    private const string Prefabs = "Assets/_Project/Prefabs/World/Living";

    // Season Cycle names: Summer, Autumn, Late Autumn, Winter, Spring.
    private static readonly Dictionary<string, string[]> Growing = new Dictionary<string, string[]>
    {
        { "Berries", new[] { "Summer", "Autumn" } },
        { "CloudberryPatch", new[] { "Summer" } },
        { "Chanterelles", new[] { "Autumn", "Late Autumn" } },
        { "WildBarley", new[] { "Summer", "Autumn" } },
        { "WildTurnips", new[] { "Autumn", "Late Autumn" } },
        { "HealingHerb", new[] { "Spring", "Summer", "Autumn" } },
        { "Flax", new[] { "Spring", "Summer", "Autumn" } },
        { "NettleFiber", new[] { "Spring", "Summer", "Autumn" } },
        { "BrambleThicket", new[] { "Spring", "Summer", "Autumn", "Late Autumn" } },
    };

    public static string Apply()
    {
        var report = new List<string> { NodeSeasons() };
        List<FestivalDefinition> festivals = FestivalAssets();
        report.Add($"{festivals.Count} festivals");
        GameObject bonfire = Bonfire();
        report.Add(bonfire != null ? "bonfire" : "no bonfire (Campfire prefab missing)");
        report.Add(Fireflies());
        report.Add(Spirits());
        report.Add(Scene(festivals, bonfire));
        report.Add(Raids());
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static string NodeSeasons()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        int set = 0;
        foreach (GatheringNodeDefinition node in content.GatheringNodes)
        {
            if (node == null)
            {
                continue;
            }

            var seasons = Growing.TryGetValue(node.name, out string[] names) ? new List<string>(names) : new List<string>();
            node.ConfigureSeasons(seasons);
            EditorUtility.SetDirty(node);
            set += seasons.Count > 0 ? 1 : 0;
        }

        return $"{set} nodes grow in their seasons";
    }

    // ----- Fireflies, spirits and the blight -----

    private static string Fireflies()
    {
        var cycle = AssetDatabase.LoadAssetAtPath<SeasonCycle>("Assets/_Project/Data/World/Season Cycle.asset");
        if (cycle == null)
        {
            return "no Season Cycle";
        }

        var share = new Dictionary<string, float> { { "Summer", 1f }, { "Autumn", 0.35f }, { "Late Autumn", 0f }, { "Winter", 0f }, { "Spring", 0.2f } };
        foreach (SeasonCycle.Season season in cycle.Seasons)
        {
            season.fireflies = share.TryGetValue(season.name, out float f) ? f : 1f;
            season.riverRise = season.name == "Spring" ? 0.45f : 0f; // the thaw: fords are swum
        }

        EditorUtility.SetDirty(cycle);
        return "fireflies by season";
    }

    private static string Spirits()
    {
        var huldra = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Huldra Glimpse.prefab");
        if (huldra != null && huldra.TryGetComponent(out Glimpse glimpse) && glimpse.Spirit != SpiritBonds.Huldra)
        {
            glimpse.SetSpirit(SpiritBonds.Huldra);
            EditorUtility.SetDirty(huldra);
        }

        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>("Assets/_Project/Data/World/Safe Haven/Haven World Recipe.asset");
        if (recipe != null)
        {
            recipe.hollowedGrazer = AssetDatabase.LoadAssetAtPath<Glimpse>($"{Prefabs}/Hollowed Deer.prefab");
            EditorUtility.SetDirty(recipe);
        }

        // Winter coats where snow lies: the fox goes white, deer and stags greyer.
        var coats = new Dictionary<string, Color>
        {
            { "Fox", new Color(0.93f, 0.93f, 0.95f, 0.9f) },
            { "Deer", new Color(0.5f, 0.46f, 0.42f, 0.5f) },
            { "Stag", new Color(0.45f, 0.4f, 0.36f, 0.5f) },
        };
        int coated = 0;
        foreach (KeyValuePair<string, Color> coat in coats)
        {
            var animal = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{coat.Key}.prefab");
            if (animal != null && animal.TryGetComponent(out Wildlife wild))
            {
                wild.SetWinterCoat(coat.Value);
                EditorUtility.SetDirty(animal);
                coated++;
            }
        }

        return $"the Huldra remembers; hollowed grazers for the recipe; {coated} winter coats";
    }

    private static SoundDefinition Call(string name, string files, int count, float volume, float full, float audible)
    {
        string path = $"{Sounds}/{name}.asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var data = new SerializedObject(sound);
        data.FindProperty("displayName").stringValue = name;
        SerializedProperty clips = data.FindProperty("clips");
        clips.arraySize = 0;
        for (int i = 1; i <= count; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/SFX/Safe Haven Living World/{files} {i}.wav");
            if (clip != null)
            {
                clips.arraySize++;
                clips.GetArrayElementAtIndex(clips.arraySize - 1).objectReferenceValue = clip;
            }
        }

        data.FindProperty("channel").enumValueIndex = (int)AudioChannel.Ambience;
        data.FindProperty("volume").floatValue = volume;
        data.FindProperty("fullVolumeDistance").floatValue = full;
        data.FindProperty("audibleDistance").floatValue = audible;
        data.FindProperty("maxAtOnce").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    // ----- Raids follow the year -----

    // The raid scene gets the Haven's seasons (the same year, the same day), with its ice, tracks and spring melt: a
    // winter raid is snowy, its marsh pools frozen into ways across; a spring raid's fords run deep.
    private static string Raids()
    {
        const string raidPath = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";
        Seasons home = Object.FindFirstObjectByType<Seasons>();
        if (home == null)
        {
            return "raids skipped (open SafeHaven: its Seasons are copied)";
        }

        var homeData = new SerializedObject(home);
        var cycle = (SeasonCycle)homeData.FindProperty("cycle").objectReferenceValue;
        float startDay = homeData.FindProperty("startDay").floatValue;
        WinterIce homeIce = Object.FindFirstObjectByType<WinterIce>();
        if (homeIce != null)
        {
            Ensure<SpringMelt>(homeIce.gameObject);
            EditorUtility.SetDirty(homeIce.gameObject);
            // Saved with the rest of this run's changes to the Haven (Scene() runs just before, from a clean scene).
            EditorSceneManager.SaveScene(homeIce.gameObject.scene);
        }

        var raid = EditorSceneManager.OpenScene(raidPath, OpenSceneMode.Additive);
        try
        {
            GameObject holder = raid.GetRootGameObjects().FirstOrDefault(g => g.name == "Seasons");
            if (holder == null)
            {
                holder = new GameObject("Seasons");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, raid);
            }

            Ensure<Seasons>(holder).Configure(cycle, startDay);
            var ice = Ensure<WinterIce>(holder);
            if (homeIce != null)
            {
                var from = new SerializedObject(homeIce);
                ice.Configure((SoundDefinition)from.FindProperty("crack").objectReferenceValue,
                    (SoundDefinition)from.FindProperty("creak").objectReferenceValue);
            }

            Ensure<WinterTracks>(holder);
            Ensure<SpringMelt>(holder);
            bool weather = raid.GetRootGameObjects().Any(g => g.GetComponentInChildren<WeatherSystem>(true) != null);
            EditorUtility.SetDirty(holder);
            EditorSceneManager.MarkSceneDirty(raid);
            EditorSceneManager.SaveScene(raid);
            return $"raid scene: seasons, ice, tracks, spring melt (saved){(weather ? "" : "; it has no Weather System, so no snowfall there")}";
        }
        finally
        {
            EditorSceneManager.CloseScene(raid, true);
        }
    }

    // ----- Festivals -----

    private static SoundDefinition Tune(string name)
    {
        Folder(Sounds);
        string path = $"{Sounds}/{name}.asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var data = new SerializedObject(sound);
        data.FindProperty("displayName").stringValue = name;
        SerializedProperty clips = data.FindProperty("clips");
        clips.arraySize = 1;
        clips.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Music}{name}.wav");
        data.FindProperty("channel").enumValueIndex = (int)AudioChannel.Music;
        data.FindProperty("volume").floatValue = 0.55f;
        data.FindProperty("fullVolumeDistance").floatValue = 10f;
        data.FindProperty("audibleDistance").floatValue = 60f;
        data.FindProperty("maxAtOnce").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    private static List<FestivalDefinition> FestivalAssets()
    {
        Folder(FestivalData);
        // The year: Summer days 0-9, Autumn 10-15, Late Autumn 16-19, Winter 20-25, Spring 26-31.
        var harvest = Festival("Harvest Feast", 15, new Vector2(17f, 23.5f),
            "Tonight is the Harvest Feast. The Haven gathers at the bonfire.",
            new[]
            {
                "Eat, eat! There's more than we can carry to the cellar.",
                "A good year, all things told. The woods gave more than they took.",
                "Dance with me? No? The fiddle won't wait.",
                "Leave a bowl at the waystone tonight. The forest likes to be thanked.",
                "Next year we'll clear the east field. Next year.",
            },
            Tune("Harvest Polska"), "Harvest Cheer",
            new List<StatBonus>
            {
                new StatBonus(StatType.MaxStamina, 0.1f, StatModifierOp.Percent),
                new StatBonus(StatType.GatherYield, 0.1f, StatModifierOp.Percent),
            }, 1500f);
        var midwinter = Festival("Midwinter Bonfire", 23, new Vector2(16.5f, 24f),
            "It's Midwinter. Tonight the Haven keeps the bonfire burning against the long dark.",
            new[]
            {
                "The longest night. From tomorrow the light comes back, a little at a time.",
                "Stand close to the fire. The cold can't have us tonight.",
                "My grandmother said the dead come to warm themselves at Midwinter. Leave them room.",
                "Burn a sprig of juniper for luck. Go on.",
                "Listen. Even the woods are quiet tonight.",
            },
            Tune("Midwinter Air"), "Midwinter Warmth",
            new List<StatBonus>
            {
                new StatBonus(StatType.FrostResistance, 0.6f, StatModifierOp.Flat),
                new StatBonus(StatType.MoveSpeed, 0.05f, StatModifierOp.Percent),
            }, 1800f);
        return new List<FestivalDefinition> { harvest, midwinter };
    }

    private static FestivalDefinition Festival(string name, int day, Vector2 hours, string said, string[] lines, SoundDefinition music,
        string boonName, List<StatBonus> boon, float seconds)
    {
        string path = $"{FestivalData}/{name}.asset";
        var festival = AssetDatabase.LoadAssetAtPath<FestivalDefinition>(path);
        if (festival == null)
        {
            festival = ScriptableObject.CreateInstance<FestivalDefinition>();
            AssetDatabase.CreateAsset(festival, path);
        }

        festival.Configure(name, day, hours, said, lines, music, boonName, boon, seconds);
        EditorUtility.SetDirty(festival);
        return festival;
    }

    // The Campfire's stones, logs and light, half again as big, with tall flames and a column of smoke. Only the look:
    // no building, saving or crafting.
    private static GameObject Bonfire()
    {
        var campfire = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Structures/Campfire.prefab");
        if (campfire == null)
        {
            return null;
        }

        Folder(Prefabs);
        var root = new GameObject("Festival Bonfire");
        try
        {
            foreach (string part in new[] { "Model", "Light" })
            {
                Transform source = campfire.transform.Find(part);
                if (source == null)
                {
                    continue;
                }

                GameObject copy = Object.Instantiate(source.gameObject, root.transform);
                copy.name = part;
                copy.transform.localPosition = source.localPosition * 1.8f;
                copy.transform.localRotation = source.localRotation;
                copy.transform.localScale = Vector3.Scale(source.localScale, Vector3.one * 1.8f);
                if (part == "Light" && copy.TryGetComponent(out Light light))
                {
                    light.range *= 1.8f;
                    light.intensity *= 1.4f;
                }
            }

            Flames(root, FlameMaterial());
            var smoke = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Effects/Hearth Smoke.mat");
            root.AddComponent<HearthSmoke>().Configure(new Vector3(0f, 2.6f, 0f), Vector3.zero, 0f, smoke);
            return PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/Festival Bonfire.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Material FlameMaterial()
    {
        const string path = "Assets/_Project/Art/Effects/Bonfire Flame.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Effect Particle"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Effects/Smoke Puff.png"));
        material.SetColor("_TintColor", new Color(1.6f, 0.75f, 0.25f, 1f));
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Flames(GameObject root, Material material)
    {
        var go = new GameObject("Flames");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startRotation = 0f; // upright flames (the Fire shader draws each as a flame)
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.3f, 0.8f), new Color(1f, 0.45f, 0.15f, 0.7f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 45f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.55f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.08f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        BuildFire.Upright(system); // low and dense, drawn by the Fire shader (run BuildFire for the material)
    }

    private static string Scene(List<FestivalDefinition> festivals, GameObject bonfire)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SafeHaven")
        {
            return "scene skipped (open SafeHaven)";
        }

        bool wasClean = !scene.isDirty;
        Transform living = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "07 Ambience")?.transform.Find("Living World");
        if (living == null)
        {
            return "no 07 Ambience/Living World (run BuildLivingWorld first)";
        }

        Transform holder = living.Find("Festivals");
        if (holder == null)
        {
            holder = new GameObject("Festivals").transform;
            holder.SetParent(living, false);
        }

        Ensure<Festivals>(holder.gameObject).Configure(festivals, bonfire);
        EditorUtility.SetDirty(holder.gameObject);

        // Geese going over, drawn with the scene's birds' picture sheet.
        Transform geese = living.Find("Geese");
        if (geese == null)
        {
            geese = new GameObject("Geese").transform;
            geese.SetParent(living, false);
        }

        Ensure<ParticleSystem>(geese.gameObject);
        Ensure<MigratingGeese>(geese.gameObject).Configure(Call("Geese Call", "Geese Call", 3, 0.5f, 25f, 120f));
        Birds birds = Object.FindFirstObjectByType<Birds>();
        if (birds != null && birds.TryGetComponent(out ParticleSystemRenderer birdDrawn))
        {
            var drawn = geese.GetComponent<ParticleSystemRenderer>();
            drawn.sharedMaterial = birdDrawn.sharedMaterial;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        EditorUtility.SetDirty(geese.gameObject);

        // The spirits, and the blight's hollowed among the herds.
        Transform spirits = living.Find("Spirits");
        if (spirits == null)
        {
            spirits = new GameObject("Spirits").transform;
            spirits.SetParent(living, false);
        }

        Ensure<SpiritBonds>(spirits.gameObject).Configure(
            AssetDatabase.LoadAssetAtPath<SoundDefinition>($"{Sounds}/Nokk Song.asset"),
            AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/HuldraSilk.asset"),
            AssetDatabase.LoadAssetAtPath<SoundDefinition>($"{Sounds}/Offering Taken.asset"));
        EditorUtility.SetDirty(spirits.gameObject);
        var hollow = AssetDatabase.LoadAssetAtPath<Glimpse>($"{Prefabs}/Hollowed Deer.prefab");
        int herds = 0;
        foreach (WildlifeHerd herd in Object.FindObjectsByType<WildlifeHerd>())
        {
            if (herd.name.Contains("Grazer"))
            {
                herd.SetHollowed(hollow);
                EditorUtility.SetDirty(herd);
                herds++;
            }
        }

        GlimpseWatcher watcher = Object.FindFirstObjectByType<GlimpseWatcher>();
        if (watcher != null)
        {
            var data = new SerializedObject(watcher);
            SerializedProperty entries = data.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("prefab").objectReferenceValue == hollow)
                {
                    entry.FindPropertyRelative("moreWithBlight").floatValue = 3f;
                }
            }

            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (wasClean)
        {
            EditorSceneManager.SaveScene(scene);
        }

        return (wasClean ? "scene: festivals, geese, spirits (saved)" : "scene: festivals, geese, spirits (not saved: it had other changes)") + $", {herds} herds may hold a hollowed one";
    }

    private static void Folder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }

    private static T Ensure<T>(GameObject target) where T : Component => target.TryGetComponent(out T found) ? found : target.AddComponent<T>();
}
