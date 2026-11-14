#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The data of a tree coming down. Run Tools/Audio/woodcutting.py and Tools/Art/timber_textures.py first, then
/// unity command run_script --file AgentScripts/BuildWoodcutting.cs --entry BuildWoodcutting.All
/// Makes the sounds, particle effects, feedback profiles, the Felled Tree node and prefab, and fills in
/// TreeChoppingSettings. Re-running updates in place.
/// </summary>
public static class BuildWoodcutting
{
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Woodcutting/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";
    private const string Textures = "Assets/_Project/Art/Forest/Textures/";
    private const string Materials = "Assets/_Project/Art/Forest/Materials/";
    private const string Effects = "Assets/_Project/Prefabs/Effects/Gathering/";
    private const string Feedback = "Assets/_Project/Data/Gathering/Feedback/";
    private const string Tree = "Assets/_Project/Data/Gathering/Tree.asset";
    private const string FelledNode = "Assets/_Project/Data/Gathering/FelledTree.asset";
    private const string Proxy = "Assets/_Project/Prefabs/Systems/TerrainTreeProxy.prefab";
    private const string FelledPrefab = "Assets/_Project/Prefabs/Systems/FelledTree.prefab";

    public static string All()
    {
        AssetDatabase.Refresh();
        ImportTexture(Textures + "LogCutFace.png", false);
        ImportTexture(Textures + "FallingLeaves.png", true);
        ImportTexture(Textures + "DustPuff.png", true);

        SoundDefinition creak = Sound("Tree Creak", 4, 0.5f, new Vector2(0.92f, 1.08f), 4f, 28f, 2);
        SoundDefinition crack = Sound("Tree Crack", 3, 0.8f, new Vector2(0.96f, 1.04f), 8f, 50f, 2);
        SoundDefinition rush = Sound("Tree Rush", 2, 0.55f, new Vector2(0.94f, 1.06f), 8f, 40f, 2);
        SoundDefinition landing = Sound("Tree Landing", 3, 1f, new Vector2(0.94f, 1.04f), 10f, 70f, 2);
        SoundDefinition snap = Sound("Branch Snap", 4, 0.6f, new Vector2(0.9f, 1.1f), 4f, 30f, 3);
        SoundDefinition limbing = Sound("Limbing", 3, 0.6f, new Vector2(0.94f, 1.06f), 4f, 25f, 2);
        SoundDefinition split = Sound("Log Split", 4, 0.75f, new Vector2(0.94f, 1.06f), 4f, 28f, 2);
        SoundDefinition knock = Sound("Log Knock", 4, 0.45f, new Vector2(0.9f, 1.1f), 3f, 18f, 4);
        SoundDefinition strike = Sound("Tree Strike", 3, 0.8f, new Vector2(0.92f, 1.08f), 6f, 40f, 3);
        SoundDefinition crush = Sound("Tree Crush", 3, 0.9f, new Vector2(0.94f, 1.06f), 6f, 40f, 3);

        Material leaf = ParticleMaterial("Falling Leaf", Textures + "FallingLeaves.png");
        Material dust = ParticleMaterial("Landing Dust", Textures + "DustPuff.png");
        GameObject leafFall = LeafFall(leaf);
        GameObject landingDust = LandingDust(dust);
        Material face = CutFace();
        PhysicsMaterial surface = Surface();

        HarvestFeedbackProfile treeFeedback = Profile("TreeFeedback", 3f, 1.5f, 0.5f, crack);
        HarvestFeedbackProfile logFeedback = Profile("LogFeedback", 0f, 0f, 0f, split);
        Set(AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(Tree), so => so.FindProperty("feedback").objectReferenceValue = treeFeedback);
        GatheringNodeDefinition node = FelledTreeNode(logFeedback);
        GameObject prefab = Prefab(node);
        HandSwingsToThePlayer();

        var settings = Resources.Load<TreeChoppingSettings>("TreeChoppingSettings");
        Set(settings, so =>
        {
            so.FindProperty("felledTreePrefab").objectReferenceValue = prefab;
            so.FindProperty("sectionsPerSize").floatValue = 2f;
            so.FindProperty("dustTint").colorValue = new Color(0.46f, 0.39f, 0.29f, 0.34f);
            so.FindProperty("timberSurface").objectReferenceValue = surface;
            so.FindProperty("limbingItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset");
            so.FindProperty("logBark").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Bark.mat");
            so.FindProperty("cutFace").objectReferenceValue = face;
            so.FindProperty("leafFall").objectReferenceValue = leafFall;
            so.FindProperty("landingDust").objectReferenceValue = landingDust;
            so.FindProperty("creakSound").objectReferenceValue = creak;
            so.FindProperty("rushSound").objectReferenceValue = rush;
            so.FindProperty("landingSound").objectReferenceValue = landing;
            so.FindProperty("branchSnapSound").objectReferenceValue = snap;
            so.FindProperty("limbingSound").objectReferenceValue = limbing;
            so.FindProperty("logKnockSound").objectReferenceValue = knock;
            so.FindProperty("strikeSound").objectReferenceValue = strike;
            so.FindProperty("crushSound").objectReferenceValue = crush;
        });

        AssetDatabase.SaveAssets();
        return "Woodcutting built.";
    }

    // The swing clips loop by themselves; with WorkSpeed on them the player's press can hold and release them. It rests at 1,
    // so anything else using these states plays as it always did. Contacts are measured from the clips, as combat's are.
    private static void HandSwingsToThePlayer()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/_Project/Animation/Controllers/PlayerAnimator.controller");
        bool known = false;
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            known |= parameter.name == "WorkSpeed";
        }

        if (!known)
        {
            controller.AddParameter(new AnimatorControllerParameter { name = "WorkSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        }

        float wall = 0f;
        float ground = 0f;
        foreach (UnityEditor.Animations.ChildAnimatorState child in controller.layers[controller.layers.Length - 2].stateMachine.states)
        {
            UnityEditor.Animations.AnimatorState state = child.state;
            if (state.name != "Mine Wall Right" && state.name != "Mine Wall Left" && state.name != "Mine Ground Right" && state.name != "Mine Ground Left")
            {
                continue;
            }

            state.speedParameter = "WorkSpeed";
            state.speedParameterActive = true;
            EditorUtility.SetDirty(state);
            List<float> contacts = CombatAnimationSetup.ContactTimes(state.motion as AnimationClip, state.name.EndsWith("Left") ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (contacts.Count > 0 && state.name.StartsWith("Mine Wall"))
            {
                wall = contacts[0];
            }
            else if (contacts.Count > 0)
            {
                ground = contacts[0];
            }
        }

        EditorUtility.SetDirty(controller);
        if (wall <= 0f || ground <= 0f)
        {
            throw new System.Exception("The swing states weren't found");
        }

        Set(Resources.Load<GatheringMinigameSettings>("GatheringMinigameSettings"), so =>
        {
            so.FindProperty("wallSwingContact").floatValue = wall;
            so.FindProperty("groundSwingContact").floatValue = ground;
        });
    }

    private static void Set(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void ImportTexture(string path, bool alpha)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = alpha;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    private static SoundDefinition Sound(string name, int variants, float volume, Vector2 pitch, float full, float audible, int atOnce)
    {
        string path = Sounds + name + ".asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        Set(sound, so =>
        {
            so.FindProperty("displayName").stringValue = name;
            SerializedProperty clips = so.FindProperty("clips");
            clips.arraySize = variants;
            for (int i = 0; i < variants; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Clips}{name} {i + 1}.wav");
                clips.GetArrayElementAtIndex(i).objectReferenceValue = clip != null ? clip : throw new System.Exception("No clip " + name);
            }

            so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
            so.FindProperty("volume").floatValue = volume;
            so.FindProperty("pitchRange").vector2Value = pitch;
            so.FindProperty("positional").boolValue = true;
            so.FindProperty("fullVolumeDistance").floatValue = full;
            so.FindProperty("audibleDistance").floatValue = audible;
            so.FindProperty("maxAtOnce").intValue = atOnce;
        });
        return sound;
    }

    private static Material ParticleMaterial(string name, string texture)
    {
        string path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CutFace()
    {
        string path = Materials + "Log Cut Face.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "LogCutFace.png"));
        material.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicsMaterial Surface()
    {
        const string path = "Assets/_Project/Data/Gathering/Timber.asset";
        var surface = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (surface == null)
        {
            surface = new PhysicsMaterial("Timber");
            AssetDatabase.CreateAsset(surface, path);
        }

        surface.dynamicFriction = 0.7f;
        surface.staticFriction = 0.9f;
        surface.bounciness = 0.12f;
        surface.bounceCombine = PhysicsMaterialCombine.Maximum;
        EditorUtility.SetDirty(surface);
        return surface;
    }

    private static GameObject LeafFall(Material material)
    {
        return Effect("LeafFall", material, (system, renderer) =>
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
            // Well over life size: the game is seen from far above, and a real leaf there is two pixels.
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.07f;
            main.maxParticles = 320;

            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            ParticleSystem.NoiseModule flutter = system.noise;
            flutter.enabled = true;
            flutter.strength = 0.9f;
            flutter.frequency = 0.5f;
            flutter.scrollSpeed = 0.4f;
            flutter.damping = true;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
            Fade(system, 0.08f, 0.8f);
        });
    }

    private static GameObject LandingDust(Material material)
    {
        return Effect("LandingDust", material, (system, renderer) =>
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.015f;
            main.maxParticles = 160;
            ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.5f));
            Fade(system, 0.1f, 0.5f);
        });
    }

    // Both effects are boxes that TreeEffects fits to the tree, one burst at the start.
    private static GameObject Effect(string name, Material material, System.Action<ParticleSystem, ParticleSystemRenderer> setUp)
    {
        var root = new GameObject(name);
        var system = root.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)20) });
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        setUp(system, renderer);
        root.AddComponent<PooledEffect>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Effects + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void Fade(ParticleSystem system, float fadeIn, float fadeOutFrom)
    {
        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOutFrom), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
    }

    private static HarvestFeedbackProfile Profile(string name, float critical, float normal, float glancing, SoundDefinition harvested)
    {
        string path = Feedback + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<HarvestFeedbackProfile>(path) == null)
        {
            AssetDatabase.CopyAsset(Feedback + "WoodFeedback.asset", path);
        }

        var profile = AssetDatabase.LoadAssetAtPath<HarvestFeedbackProfile>(path);
        Set(profile, so =>
        {
            so.FindProperty("critical").FindPropertyRelative("shakeDegrees").floatValue = critical;
            so.FindProperty("normal").FindPropertyRelative("shakeDegrees").floatValue = normal;
            so.FindProperty("glancing").FindPropertyRelative("shakeDegrees").floatValue = glancing;
            so.FindProperty("harvested").FindPropertyRelative("sound").objectReferenceValue = harvested;
            // It doesn't break where it stands: the tree falls and the log comes apart.
            so.FindProperty("breakSeconds").floatValue = 0f;
        });
        return profile;
    }

    private static GatheringNodeDefinition FelledTreeNode(HarvestFeedbackProfile feedback)
    {
        if (AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(FelledNode) == null)
        {
            AssetDatabase.CopyAsset(Tree, FelledNode);
        }

        var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(FelledNode);
        Set(node, so =>
        {
            so.FindProperty("id").stringValue = "felled-tree";
            so.FindProperty("displayName").stringValue = "Felled Tree";
            so.FindProperty("swings").intValue = 2;
            so.FindProperty("swingSpeed").floatValue = 1.3f;
            so.FindProperty("gatherRange").floatValue = 2.2f;
            so.FindProperty("depleteOnGather").boolValue = false;
            so.FindProperty("respawnSeconds").floatValue = 0f;
            so.FindProperty("feedback").objectReferenceValue = feedback;
        });
        return node;
    }

    private static GameObject Prefab(GatheringNodeDefinition node)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FelledPrefab) == null)
        {
            AssetDatabase.CopyAsset(Proxy, FelledPrefab);
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(FelledPrefab);
        try
        {
            contents.name = "FelledTree";
            if (contents.TryGetComponent(out ChoppableTree choppable))
            {
                Object.DestroyImmediate(choppable);
            }

            if (!contents.TryGetComponent(out Rigidbody body))
            {
                body = contents.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            if (!contents.TryGetComponent(out FelledLog _))
            {
                contents.AddComponent<FelledLog>();
            }

            if (!contents.TryGetComponent(out FelledTree _))
            {
                contents.AddComponent<FelledTree>();
            }

            if (!contents.TryGetComponent(out TreeImpacts _))
            {
                contents.AddComponent<TreeImpacts>();
            }

            Set(contents.GetComponent<ResourceNode>(), so => so.FindProperty("node").objectReferenceValue = node);
            Set(contents.GetComponent<ResourceSource>(), so => so.FindProperty("grantOnInteractionComplete").boolValue = false);
            // A log lies at the feet, so it takes the swing down at the ground, not the one at a wall.
            contents.GetComponent<Interactable>().Configure(InteractionAnimation.MineGround, 0f, 2.2f, false);
            PrefabUtility.SaveAsPrefabAsset(contents, FelledPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(FelledPrefab);
    }
}
