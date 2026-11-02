using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the combat effects (Safe Haven › Combat › Build Combat Effects): generated textures (soft dot, spark streak,
/// ring, star), their additive materials ("Safe Haven/Effect Particle"), pooled particle prefabs for hits, critical
/// hits, blocks, parries, staggers, perfect dodges and the parry cue, the slash arc material, and wires them all into
/// Resources/CombatFeel. Running it again rebuilds them in place (prefab GUIDs are kept, so references survive).
/// </summary>
public static class CombatEffectsBuilder
{
    public const string ArtFolder = "Assets/_Project/Art/Effects/Combat";
    public const string PrefabFolder = "Assets/_Project/Prefabs/Effects/Combat";
    private const string FeelPath = "Assets/_Project/Resources/CombatFeel.asset";
    private const string WoodChipsPath = "Assets/_Project/Prefabs/Effects/Gathering/WoodChips.prefab";

    private static Material dot, streak, ring, star;

    [MenuItem("Safe Haven/Combat/Build Combat Effects")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(ArtFolder);
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh();
        dot = ParticleMaterial("Dot", DotPixels);
        streak = ParticleMaterial("Streak", StreakPixels);
        ring = ParticleMaterial("Ring", RingPixels);
        star = ParticleMaterial("Star", StarPixels);
        Material slash = SlashMaterial();

        var warm = new Color(1f, 0.82f, 0.55f);
        var gold = new Color(1f, 0.75f, 0.3f);
        var ichor = new Color(0.95f, 0.4f, 0.18f);
        var steel = new Color(1f, 0.92f, 0.6f);
        var frost = new Color(0.6f, 0.88f, 1f);

        GameObject hit = Effect("Hit Spark", root =>
        {
            Sparks(root, 12, 4f, 9f, 0.15f, 0.32f, 0.05f, warm, 35f, 1.5f);
            Flash(root, dot, 0.9f, 0.08f, warm * 2.5f);
            Motes(root, 6, 1f, 2.5f, 0.4f, 0.7f, 0.08f, ichor);
        });
        GameObject critical = Effect("Critical Hit Spark", root =>
        {
            Sparks(root, 22, 6f, 12f, 0.18f, 0.38f, 0.07f, gold, 45f, 1.5f);
            Flash(root, star, 1.7f, 0.12f, gold * 3f);
            Ring(root, false, 0.3f, 1.6f, 0.18f, gold * 2f);
            Motes(root, 10, 1.5f, 3.5f, 0.45f, 0.8f, 0.1f, ichor);
        });
        GameObject block = Effect("Block Spark", root =>
        {
            Sparks(root, 18, 3f, 8f, 0.2f, 0.45f, 0.04f, steel, 60f, 2.5f);
            Flash(root, dot, 0.6f, 0.06f, Color.white * 2f);
        });
        GameObject parry = Effect("Parry Flash", root =>
        {
            Flash(root, star, 2.4f, 0.2f, frost * 3.5f);
            Ring(root, false, 0.4f, 2.4f, 0.25f, frost * 2.5f);
            Sparks(root, 28, 5f, 11f, 0.2f, 0.45f, 0.05f, Color.Lerp(frost, Color.white, 0.5f), 360f, 1f);
        });
        GameObject stagger = Effect("Stagger Burst", root =>
        {
            Ring(root, true, 0.5f, 2.6f, 0.4f, gold * 1.6f);
            Motes(root, 10, 0.5f, 1.8f, 0.5f, 0.9f, 0.12f, gold * 0.8f, upward: true);
        });
        GameObject perfectDodge = Effect("Perfect Dodge", root =>
        {
            Ring(root, true, 0.5f, 3f, 0.35f, frost * 2f);
            Motes(root, 14, 0.5f, 2f, 0.5f, 0.9f, 0.08f, frost, upward: true);
        });
        GameObject cue = Effect("Parry Cue", root =>
        {
            Flash(root, star, 0.8f, 0.22f, new Color(1f, 0.95f, 0.8f) * 3f);
        });

        var feel = AssetDatabase.LoadAssetAtPath<CombatFeelSettings>(FeelPath);
        if (feel != null)
        {
            var data = new SerializedObject(feel);
            data.FindProperty("hitEffect").objectReferenceValue = hit;
            data.FindProperty("criticalHitEffect").objectReferenceValue = critical;
            data.FindProperty("structureHitEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(WoodChipsPath);
            data.FindProperty("blockEffect").objectReferenceValue = block;
            data.FindProperty("parryEffect").objectReferenceValue = parry;
            data.FindProperty("staggerEffect").objectReferenceValue = stagger;
            data.FindProperty("perfectDodgeEffect").objectReferenceValue = perfectDodge;
            data.FindProperty("parryCueEffect").objectReferenceValue = cue;
            data.FindProperty("slashMaterial").objectReferenceValue = slash;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        BuildWeaponEffects();
        BuildBloodAndMarkers();
        AssetDatabase.SaveAssets();
        Debug.Log($"Combat effects built in {PrefabFolder} and set in Combat Feel.");
    }

    /// <summary>The blood prefab (three world-space particle systems, sprayed by BloodEffects) and the ring under the enemy in
    /// focus, set in Combat Feel. Build Combat Effects runs it too.</summary>
    [MenuItem("Safe Haven/Combat/Build Blood and Markers")]
    public static void BuildBloodAndMarkers()
    {
        Directory.CreateDirectory(ArtFolder);
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh();

        Material dropMaterial = BlendedMaterial("Blood Drop", BloodDropPixels, 64);
        Material mistMaterial = BlendedMaterial("Blood Mist", BloodMistPixels, 64);
        Material stainMaterial = BlendedMaterial("Blood Stain", StainPixels, 512);
        Material poolMaterial = BlendedMaterial("Blood Pool", PoolPixels, 512);
        Material ringMaterial = BlendedMaterial("Focus Ring", FocusRingPixels, 128);

        var root = new GameObject("Blood Effects");
        GameObject prefab;
        try
        {
            ParticleSystem drops = BloodSystem(root, "Droplets", dropMaterial, 1500);
            ParticleSystem.MainModule dropMain = drops.main;
            dropMain.gravityModifier = 1.5f;
            var dropRenderer = drops.GetComponent<ParticleSystemRenderer>();
            dropRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            dropRenderer.lengthScale = 1.6f;
            dropRenderer.velocityScale = 0.03f;
            ParticleSystem.LimitVelocityOverLifetimeModule air = drops.limitVelocityOverLifetime;
            air.enabled = true;
            air.drag = 0.8f;
            BloodFade(drops, new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f));

            ParticleSystem haze = BloodSystem(root, "Mist", mistMaterial, 120);
            ParticleSystem.MainModule hazeMain = haze.main;
            hazeMain.gravityModifier = 0.15f;
            BloodFade(haze, new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f));
            ParticleSystem.SizeOverLifetimeModule hazeGrows = haze.sizeOverLifetime;
            hazeGrows.enabled = true;
            hazeGrows.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.5f));

            ParticleSystem stains = BloodSystem(root, "Stains", stainMaterial, 1000);
            ParticleSystem.MainModule stainMain = stains.main;
            stainMain.startRotation3D = true;
            var stainRenderer = stains.GetComponent<ParticleSystemRenderer>();
            stainRenderer.alignment = ParticleSystemRenderSpace.Local;
            stainRenderer.sortingFudge = 40f;
            // A stain appears as its drops land, about a third of a second in out of half a minute, and fades at the end.
            BloodFade(stains, new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0f, 0.008f), new GradientAlphaKey(1f, 0.016f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f));
            ParticleSystem.TextureSheetAnimationModule sheet = stains.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);

            // Pools: spreading is the size over lifetime, quick at first and slowing, in the first fifth of the pool's life (9 of 45 seconds).
            ParticleSystem spreading = BloodSystem(root, "Pools", poolMaterial, 120);
            ParticleSystem.MainModule poolMain = spreading.main;
            poolMain.startRotation3D = true;
            var poolRenderer = spreading.GetComponent<ParticleSystemRenderer>();
            poolRenderer.alignment = ParticleSystemRenderSpace.Local;
            poolRenderer.sortingFudge = 60f;
            BloodFade(spreading, new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.01f), new GradientAlphaKey(1f, 0.82f), new GradientAlphaKey(0f, 1f));
            ParticleSystem.SizeOverLifetimeModule spreads = spreading.sizeOverLifetime;
            spreads.enabled = true;
            spreads.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.08f, 0f, 14f), new Keyframe(0.07f, 0.68f, 3f, 3f), new Keyframe(0.2f, 1f, 0f, 0f), new Keyframe(1f, 1f)));
            ParticleSystem.TextureSheetAnimationModule poolSheet = spreading.textureSheetAnimation;
            poolSheet.enabled = true;
            poolSheet.numTilesX = 2;
            poolSheet.numTilesY = 2;
            poolSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            poolSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);

            root.AddComponent<BloodEffects>().Configure(drops, haze, stains, spreading);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Blood Effects.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        var feel = AssetDatabase.LoadAssetAtPath<CombatFeelSettings>(FeelPath);
        if (feel != null)
        {
            var data = new SerializedObject(feel);
            data.FindProperty("bloodEffects").objectReferenceValue = prefab.GetComponent<BloodEffects>();
            data.FindProperty("focusMarkerMaterial").objectReferenceValue = ringMaterial;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
    }

    // Always playing with nothing emitting: BloodEffects emits each particle itself, with its own velocity.
    private static ParticleSystem BloodSystem(GameObject root, string name, Material material, int capacity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.duration = 5f;
        main.loop = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = capacity;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static void BloodFade(ParticleSystem system, params GradientAlphaKey[] alpha)
    {
        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
        colour.color = gradient;
    }

    private static Material BlendedMaterial(string name, System.Func<float, float, float> pixels, int size)
    {
        Texture2D texture = Texture(name, pixels, size);
        string path = $"{ArtFolder}/Combat {name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Effect Particle"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = Shader.Find("Safe Haven/Effect Particle");
        material.SetTexture("_MainTex", texture);
        material.SetColor("_TintColor", Color.white);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        EditorUtility.SetDirty(material);
        return material;
    }

    // A round drop with a firm edge; stretched along its flight.
    private static float BloodDropPixels(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        return Mathf.Clamp01((1f - d) * 5f);
    }

    private static float BloodMistPixels(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        float grain = Mathf.PerlinNoise(u * 3.1f + 7f, v * 3.1f + 3f);
        return Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f) * Mathf.Lerp(0.55f, 1f, grain);
    }

    // Four different splats in a 2 x 2 sheet: a blob with a torn edge and flung specks around it.
    private static float StainPixels(float u, float v)
    {
        float x = (u + 1f) * 0.5f * 2f, y = (v + 1f) * 0.5f * 2f;
        int tile = (int)Mathf.Min(x, 1.999f) + (int)Mathf.Min(y, 1.999f) * 2;
        float tu = (x - Mathf.Floor(Mathf.Min(x, 1.999f))) * 2f - 1f, tv = (y - Mathf.Floor(Mathf.Min(y, 1.999f))) * 2f - 1f;
        float seed = 11.3f + tile * 37.7f;
        float d = Mathf.Sqrt(tu * tu + tv * tv);
        float angle = Mathf.Atan2(tv, tu);
        float torn = Mathf.PerlinNoise(Mathf.Cos(angle) * 2.2f + seed, Mathf.Sin(angle) * 2.2f + seed) * 0.42f
                     + Mathf.PerlinNoise(Mathf.Cos(angle) * 7f + seed, Mathf.Sin(angle) * 7f + seed * 2f) * 0.16f;
        float blob = Mathf.Clamp01((0.24f + torn - d) * 30f);
        float specks = 0f;
        for (int i = 0; i < 14; i++)
        {
            float a = Mathf.PerlinNoise(seed + i * 3.7f, 0.5f) * Mathf.PI * 6f;
            float r = 0.42f + Mathf.PerlinNoise(seed * 2f, i * 2.3f + 0.5f) * 0.5f;
            float size = 0.012f + Mathf.PerlinNoise(i * 5.1f + 0.5f, seed) * 0.05f;
            float dx = tu - Mathf.Cos(a) * r, dy = tv - Mathf.Sin(a) * r;
            specks = Mathf.Max(specks, Mathf.Clamp01((size - Mathf.Sqrt(dx * dx + dy * dy)) * 60f));
        }

        // Thin at the rim of the tile, so no edge ever shows.
        float rim = Mathf.Clamp01((1f - d) * 8f);
        float wet = Mathf.Lerp(0.72f, 1f, Mathf.PerlinNoise(tu * 4f + seed, tv * 4f + seed));
        return Mathf.Max(blob * wet, specks * 0.9f) * rim;
    }

    // Four pools in a 2 x 2 sheet: solid through the middle with a smooth, slowly wandering edge, as blood lies on the ground.
    private static float PoolPixels(float u, float v)
    {
        float x = (u + 1f) * 0.5f * 2f, y = (v + 1f) * 0.5f * 2f;
        int tile = (int)Mathf.Min(x, 1.999f) + (int)Mathf.Min(y, 1.999f) * 2;
        float tu = (x - Mathf.Floor(Mathf.Min(x, 1.999f))) * 2f - 1f, tv = (y - Mathf.Floor(Mathf.Min(y, 1.999f))) * 2f - 1f;
        float seed = 23.7f + tile * 41.3f;
        float d = Mathf.Sqrt(tu * tu + tv * tv);
        float angle = Mathf.Atan2(tv, tu);
        float edge = 0.5f + Mathf.PerlinNoise(Mathf.Cos(angle) * 1.4f + seed, Mathf.Sin(angle) * 1.4f + seed) * 0.32f
                     + Mathf.PerlinNoise(Mathf.Cos(angle) * 3.6f + seed, Mathf.Sin(angle) * 3.6f + seed * 2f) * 0.1f;
        float body = Mathf.Clamp01((edge - d) * 22f);
        // Thicker toward the middle, thinner where it has only just reached.
        return body * Mathf.Lerp(0.82f, 1f, Mathf.Clamp01((edge - d) * 3f)) * Mathf.Clamp01((1f - d) * 10f);
    }

    private static float FocusRingPixels(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        float angle = Mathf.Atan2(v, u);
        // A broken ring: four arcs, soft inside so it sits on the ground rather than floating over it.
        float arcs = Mathf.Clamp01((Mathf.Abs(Mathf.Sin(angle * 2f)) - 0.18f) * 8f);
        float band = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.09f);
        float glow = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.28f) * 0.18f;
        return (band * arcs + glow) * (d < 1f ? 1f : 0f);
    }

    /// <summary>Hit effects of a weapon's own, set where the weapon has none: fists land with a soft flash, a quick shock
    /// ring and a puff of dust instead of a blade's sparks. Build Combat Effects runs it too.</summary>
    [MenuItem("Safe Haven/Combat/Build Weapon Hit Effects")]
    public static void BuildWeaponEffects()
    {
        // Run on its own, it reuses the particle materials Build Combat Effects made.
        if (dot == null) dot = AssetDatabase.LoadAssetAtPath<Material>($"{ArtFolder}/Combat Dot.mat");
        if (dot == null) dot = ParticleMaterial("Dot", DotPixels);
        if (ring == null) ring = AssetDatabase.LoadAssetAtPath<Material>($"{ArtFolder}/Combat Ring.mat");
        if (ring == null) ring = ParticleMaterial("Ring", RingPixels);
        var pale = new Color(1f, 0.93f, 0.82f);
        var dust = new Color(0.62f, 0.56f, 0.48f);
        GameObject punch = Effect("Punch Impact", root =>
        {
            Flash(root, dot, 0.55f, 0.07f, pale * 1.6f);
            Ring(root, false, 0.15f, 0.9f, 0.14f, pale * 1.2f);
            Motes(root, 7, 0.8f, 2f, 0.25f, 0.45f, 0.06f, dust);
        });

        var fists = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/Fists.asset");
        if (fists != null)
        {
            var data = new SerializedObject(fists);
            SerializedProperty effect = data.FindProperty("hitEffect");
            if (effect.objectReferenceValue == null)
            {
                effect.objectReferenceValue = punch;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        AssetDatabase.SaveAssets();
    }

    // ----- Prefabs -----

    private static GameObject Effect(string name, System.Action<GameObject> build)
    {
        var root = new GameObject(name);
        try
        {
            build(root);
            root.AddComponent<PooledEffect>();
            return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ParticleSystem System(GameObject root, string name, Material material, int count, float lifeMin, float lifeMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(count, 1) * 2;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static void Fade(ParticleSystem system, Color color, float endSize = 1f)
    {
        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
        ParticleSystem.MainModule main = system.main;
        main.startColor = color;
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));
    }

    private static void Sparks(GameObject root, int count, float speedMin, float speedMax, float lifeMin, float lifeMax, float size,
        Color color, float cone, float gravity)
    {
        ParticleSystem system = System(root, "Sparks", streak, count, lifeMin, lifeMax);
        ParticleSystem.MainModule main = system.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        // Seen from the top-down camera, so they are thicker than real sparks would be.
        main.startSize = new ParticleSystem.MinMaxCurve(size * 1.2f, size * 2f);
        main.gravityModifier = gravity;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        if (cone >= 360f)
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone * 0.5f;
            shape.radius = 0.05f;
        }

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.5f;
        renderer.velocityScale = 0.04f;
        Fade(system, color * 2.2f, 0.4f);
        ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 2f;
    }

    private static void Flash(GameObject root, Material material, float size, float life, Color color)
    {
        ParticleSystem system = System(root, "Flash", material, 1, life, life);
        ParticleSystem.MainModule main = system.main;
        main.startSpeed = 0f;
        main.startSize = size;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        Fade(system, color, 1.4f);
    }

    private static void Ring(GameObject root, bool flat, float from, float to, float life, Color color)
    {
        ParticleSystem system = System(root, flat ? "Ground Ring" : "Ring", ring, 1, life, life);
        ParticleSystem.MainModule main = system.main;
        main.startSpeed = 0f;
        main.startSize = to;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        if (flat)
        {
            system.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }

        Fade(system, color);
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from / to, 0f, 2f), new Keyframe(1f, 1f)));
    }

    private static void Motes(GameObject root, int count, float speedMin, float speedMax, float lifeMin, float lifeMax, float size,
        Color color, bool upward = false)
    {
        ParticleSystem system = System(root, "Motes", dot, count, lifeMin, lifeMax);
        ParticleSystem.MainModule main = system.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(size, size * 1.8f);
        main.gravityModifier = upward ? -0.25f : 0.6f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = upward ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Hemisphere;
        shape.radius = upward ? 0.6f : 0.15f;
        if (upward)
        {
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        Fade(system, color * 1.8f, 0.3f);
        ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 3f;
    }

    // ----- Materials and textures -----

    private static Material ParticleMaterial(string name, System.Func<float, float, float> pixels)
    {
        Texture2D texture = Texture(name, pixels);
        string path = $"{ArtFolder}/Combat {name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Effect Particle"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = Shader.Find("Safe Haven/Effect Particle");
        material.SetTexture("_MainTex", texture);
        material.SetColor("_TintColor", Color.white);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material SlashMaterial()
    {
        string path = $"{ArtFolder}/Combat Slash.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Slash"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = Shader.Find("Safe Haven/Slash");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D Texture(string name, System.Func<float, float, float> alpha, int size = 64)
    {
        string path = $"{ArtFolder}/Combat {name}.png";
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static float DotPixels(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        return Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
    }

    // Long along u (the stretch direction), thin along v.
    private static float StreakPixels(float u, float v)
    {
        return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v) * 1.1f), 3f) * Mathf.Clamp01(1f - Mathf.Abs(u)) * 1.3f;
    }

    private static float RingPixels(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        return Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.12f) * (d < 1f ? 1f : 0f);
    }

    private static float StarPixels(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        float rays = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(u) * 12f) * (1f - Mathf.Abs(v)), Mathf.Clamp01(1f - Mathf.Abs(v) * 12f) * (1f - Mathf.Abs(u)));
        return Mathf.Clamp01(rays * 1.2f + Mathf.Pow(Mathf.Clamp01(1f - d * 1.6f), 2f));
    }
}
