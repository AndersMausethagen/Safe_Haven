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

        AssetDatabase.SaveAssets();
        Debug.Log($"Combat effects built in {PrefabFolder} and set in Combat Feel.");
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

    private static Texture2D Texture(string name, System.Func<float, float, float> alpha)
    {
        const int size = 64;
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
