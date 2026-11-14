#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fire (`unity command script run --file AgentScripts/BuildFire.cs --entry BuildFire.Apply`): every fire's flames move to
/// the Fire shader (Safe Haven/Fire): StationFire, the material of the torches, campfires, the furnace, fire arrows and
/// the prologue's fires, draws its sphere as rising, flickering flames; the bonfire's flame particles use its Flame From
/// UV mode. Fire lights turn a warm yellow that sinks to orange as it dims (LightFlicker), and the bigger fires get
/// rising sparks (Resources/Fire Sparks). Re-runnable.
/// </summary>
public static class BuildFire
{
    private static readonly Color WarmYellow = new Color(1f, 0.8f, 0.46f);
    private static readonly Color DeepOrange = new Color(1f, 0.52f, 0.2f, 0.4f);

    // The fires, and whether sparks rise from them.
    private static readonly (string path, bool sparks)[] Fires =
    {
        ("Assets/_Project/Prefabs/Structures/Campfire.prefab", true),
        ("Assets/_Project/Prefabs/Structures/Torch.prefab", true),
        ("Assets/_Project/Prefabs/Items/Torch.prefab", false),
        ("Assets/_Project/Prefabs/Items/Models/HandTorchModel.prefab", true),
        ("Assets/_Project/Prefabs/Items/Models/FireArrowModel.prefab", false),
        ("Assets/_Project/Prefabs/Effects/Combat/FireArrowProjectile.prefab", false),
        ("Assets/_Project/Prefabs/Raids/Events/Abandoned Camp.prefab", true),
        ("Assets/_Project/Prefabs/World/Living/Festival Bonfire.prefab", true),
    };

    public static string Apply()
    {
        var report = new List<string>();
        Shader fire = Shader.Find("Safe Haven/Fire");
        if (fire == null)
        {
            return "no Safe Haven/Fire shader";
        }

        report.Add(Flames(fire));
        report.Add(Particles(fire));
        report.Add(SparkMaterial());
        int lit = 0;
        foreach ((string path, bool sparks) in Fires)
        {
            lit += Lights(path, sparks);
        }

        report.Add($"{lit} fire lights warm yellow");
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    // The flames of every station fire: the sphere they have always been, drawn as fire.
    private static string Flames(Shader fire)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Stations/StationFire.mat");
        if (material == null)
        {
            return "no StationFire material";
        }

        material.shader = fire;
        // Yellow at the heart rather than white: the bloom and tone mapping whiten what is too bright.
        material.SetColor("_CoreColor", new Color(1f, 0.82f, 0.38f));
        material.SetColor("_MidColor", new Color(1f, 0.55f, 0.1f));
        material.SetColor("_EdgeColor", new Color(0.8f, 0.18f, 0.02f));
        material.SetFloat("_Intensity", 2.6f);
        material.SetFloat("_Height", 1.9f);
        material.SetFloat("_Taper", 0.8f);
        material.SetFloat("_Sway", 0.1f);
        material.SetFloat("_Speed", 1.8f);
        material.SetFloat("_Flicker", 0.28f);
        material.SetFloat("_NoiseScale", 3.6f);
        material.SetFloat("_Burn", 0.6f);
        material.DisableKeyword("_FIRE_UV");
        material.DisableKeyword("_FIRE_BILLBOARD");
        material.SetFloat("_FireUV", 0f);
        material.SetFloat("_FireBillboard", 0f);
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return "station fires drawn as flames";
    }

    // The bonfire's flame particles: each a flame shaped in its own quad.
    private static string Particles(Shader fire)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Effects/Bonfire Flame.mat");
        if (material == null)
        {
            return "no Bonfire Flame material";
        }

        material.shader = fire;
        material.SetColor("_CoreColor", new Color(1f, 0.82f, 0.38f));
        material.SetColor("_MidColor", new Color(1f, 0.55f, 0.1f));
        material.SetColor("_EdgeColor", new Color(0.8f, 0.18f, 0.02f));
        material.SetFloat("_Intensity", 1.8f);
        material.SetFloat("_Flicker", 0.2f);
        material.SetFloat("_NoiseScale", 3f);
        material.SetFloat("_FireUV", 1f);
        material.EnableKeyword("_FIRE_UV");
        material.SetFloat("_FireBillboard", 0f);
        material.DisableKeyword("_FIRE_BILLBOARD");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return "bonfire particles as flames";
    }

    private static string SparkMaterial()
    {
        const string path = "Assets/_Project/Resources/Fire Sparks.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Effect Particle"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Effects/Smoke Puff.png"));
        material.SetColor("_TintColor", new Color(2.4f, 1.5f, 0.6f, 1f));
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        EditorUtility.SetDirty(material);
        return "sparks";
    }

    // Flame particles: upright, low and dense, so they read as one blaze rather than sparks of flame flying off.
    public static void Upright(ParticleSystem flames)
    {
        ParticleSystem.MainModule main = flames.main;
        main.startRotation = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.maxParticles = 160;
        ParticleSystem.EmissionModule emission = flames.emission;
        emission.rateOverTime = 70f;
        ParticleSystem.ShapeModule shape = flames.shape;
        shape.radius = 0.6f;
        ParticleSystem.SizeOverLifetimeModule size = flames.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.35f)));
        ParticleSystem.ColorOverLifetimeModule colour = flames.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.2f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.55f), new GradientAlphaKey(0f, 0.85f) });
        colour.color = fade;
    }

    // A fire's lights: warm yellow, sinking to orange as they dim, wandering a little; sparks on the bigger fires.
    private static int Lights(string path, bool sparks)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            return 0;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        int lit = 0;
        try
        {
            foreach (LightFlicker flicker in root.GetComponentsInChildren<LightFlicker>(true))
            {
                Light light = flicker.GetComponent<Light>();
                // Fire, not the cold blue lights of wisps and stones.
                if (light.color.b > light.color.r * 0.7f)
                {
                    continue;
                }

                light.color = WarmYellow;
                // Flame particles stand upright (they were turned every which way for the old soft blobs).
                foreach (ParticleSystem flames in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (flames.name == "Flames")
                    {
                        Upright(flames);
                    }
                }

                flicker.Configure(DeepOrange, 0.03f, sparks, path.Contains("Bonfire") ? 8f : 3f);
                lit++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return lit;
    }
}
