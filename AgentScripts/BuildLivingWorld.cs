using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The living world (`unity command script run --file AgentScripts/BuildLivingWorld.cs --entry BuildLivingWorld.Apply`):
/// sounds (Tools/Audio/living.py), hearth smoke and lit windows on the Haven's homes, a washing line where people live,
/// timid wildlife (Quaternius deer, stag and fox), glimpses (a hollowed deer, the Huldra, a small spirit), offerings at
/// the waystones, words in passing, and the SafeHaven scene's ambience: dawn chorus, owls, ridge wind, dead trees creaking,
/// howls from where the creatures are, the Nøkk over still water, morning mist and what a fight leaves. Re-runnable.
/// The scene part saves the scene only if it was clean when this began.
/// </summary>
public static class BuildLivingWorld
{
    private const string Sfx = "Assets/_Project/Audio/SFX/Safe Haven Living World/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Living";
    private const string Wild = "Assets/_Project/Art/Wildlife";
    private const string Prefabs = "Assets/_Project/Prefabs/World/Living";
    private const string Structures = "Assets/_Project/Prefabs/Structures/";
    private const string Lit = "Universal Render Pipeline/Lit";

    public static string Apply()
    {
        var report = new List<string>();
        Folder(Sounds);
        Folder(Prefabs);
        SoundDefinition owl = Sound("Owl", "Owl Hoot", 0.55f, 4f, 45f, AudioChannel.Ambience);
        SoundDefinition wind = Sound("Ridge Wind", "Ridge Wind", 0.45f, 8f, 60f, AudioChannel.Ambience);
        SoundDefinition nokk = Sound("Nokk Song", "Nokk Song", 0.4f, 6f, 45f, AudioChannel.Ambience);
        SoundDefinition alarm = Sound("Deer Alarm", "Deer Alarm", 0.6f, 4f, 40f, AudioChannel.Effects);
        SoundDefinition offered = Sound("Offering Taken", "Offering Taken", 0.6f, 3f, 20f, AudioChannel.Effects);
        SoundDefinition gone = Sound("Glimpse Gone", "Glimpse Gone", 0.5f, 4f, 30f, AudioChannel.Effects);
        report.Add("6 sounds");

        report.Add(Hearths());
        Dictionary<string, Wildlife> animals = WildlifePrefabs(alarm);
        report.Add($"{animals.Count} animals");
        List<GlimpseWatcher.Entry> glimpses = GlimpsePrefabs(gone);
        report.Add($"{glimpses.Count} glimpses");
        report.Add(Offerings(offered));
        report.Add(Asides());
        report.Add(Recipe(animals));
        SeasonCycle year = Year();
        report.Add(Deciduous());
        report.Add(Scene(glimpses, owl, wind, nokk, year));
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    // ----- Sounds -----

    private static SoundDefinition Sound(string name, string files, float volume, float full, float audible, AudioChannel channel)
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
        for (int i = 1; i <= 3; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Sfx}{files} {i}.wav");
            if (clip != null)
            {
                clips.arraySize++;
                clips.GetArrayElementAtIndex(clips.arraySize - 1).objectReferenceValue = clip;
            }
        }

        data.FindProperty("channel").enumValueIndex = (int)channel;
        data.FindProperty("volume").floatValue = volume;
        data.FindProperty("fullVolumeDistance").floatValue = full;
        data.FindProperty("audibleDistance").floatValue = audible;
        data.FindProperty("maxAtOnce").intValue = 2;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    // ----- Hearths -----

    private static string Hearths()
    {
        Material smoke = SmokeMaterial();
        // Building, whether it has a lit window, and whether people live in it (a washing line).
        var homes = new (string name, bool light, bool washing)[]
        {
            ("Cottage", true, true), ("Hut", true, true), ("Kitchen", false, false), ("Smithy", true, false),
            ("Sauna", true, false), ("TradingPost", true, false), ("Furnace", false, false),
        };
        int done = 0;
        foreach ((string name, bool light, bool washing) in homes)
        {
            string path = Structures + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // The old washing line goes before the house is measured: counted in, it moved the next one a
                // step further out on every run (it stood 8 m from the Cottage).
                Transform old = root.transform.Find("Lived In");
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                Bounds bounds = BoundsOf(root);

                GameObject props = washing ? WashingLine(root.transform, bounds) : null;
                HearthSmoke hearth = Ensure<HearthSmoke>(root);
                // Smoke from the roof's top, a little back from the middle; the light low in the front, where a door is.
                Vector3 chimney = root.transform.InverseTransformPoint(new Vector3(bounds.center.x + bounds.extents.x * 0.35f, bounds.max.y, bounds.center.z - bounds.extents.z * 0.25f));
                Vector3 window = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + 1.1f, bounds.max.z + 0.4f));
                hearth.Configure(chimney, window, light ? 6f : 0f, smoke, props);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                done++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return $"hearth smoke on {done} buildings";
    }

    private static Material SmokeMaterial()
    {
        string path = "Assets/_Project/Art/Effects/Hearth Smoke.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Effect Particle"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_MainTex", PuffTexture());
        material.SetColor("_TintColor", Color.white);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    // A soft, lumpy puff: white, its alpha falling off from the middle and broken up by noise, so a column of them reads as
    // smoke rather than a string of dots.
    private static Texture2D PuffTexture()
    {
        string path = "Assets/_Project/Art/Effects/Smoke Puff.png";
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float noise = Mathf.PerlinNoise(x * 0.06f + 3.1f, y * 0.06f + 7.7f) * 0.6f + Mathf.PerlinNoise(x * 0.14f + 11f, y * 0.14f - 4f) * 0.4f;
                float edge = 1f - Mathf.SmoothStep(0.25f, 1f, r + (noise - 0.5f) * 0.45f);
                float alpha = Mathf.Clamp01(edge * (0.6f + 0.55f * noise));
                byte shade = (byte)(215 + 40 * noise);
                pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Two posts beside the house, a line between, and a few cloths hung out to dry: someone lives here.
    private static GameObject WashingLine(Transform parent, Bounds bounds)
    {
        var props = new GameObject("Lived In");
        props.transform.SetParent(parent, false);
        Material timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Haven/Base Timber.mat");
        Material cloth = ClothMaterial();
        // Across the house's side, east to west, so the cloths face the camera (it looks north): seen from above, not edge on.
        Vector3 side = parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.min.z - 1.6f));
        float length = Mathf.Clamp(bounds.size.x * 0.8f, 2.4f, 3.6f);
        Vector3 a = side + new Vector3(-length * 0.5f, 0f, 0f), b = side + new Vector3(length * 0.5f, 0f, 0f);
        foreach (Vector3 foot in new[] { a, b })
        {
            Part(props.transform, "Post", foot + Vector3.up * 0.9f, new Vector3(0.08f, 1.8f, 0.08f), timber, PrimitiveType.Cube);
        }

        Part(props.transform, "Line", (a + b) * 0.5f + Vector3.up * 1.72f, new Vector3(length, 0.015f, 0.015f), timber, PrimitiveType.Cube);
        float[] at = { 0.22f, 0.5f, 0.76f };
        Vector3[] sizes = { new Vector3(0.5f, 0.55f, 0.02f), new Vector3(0.38f, 0.7f, 0.02f), new Vector3(0.55f, 0.45f, 0.02f) };
        for (int i = 0; i < at.Length; i++)
        {
            Vector3 hang = Vector3.Lerp(a, b, at[i]) + Vector3.up * (1.7f - sizes[i].y * 0.5f);
            Part(props.transform, "Cloth", hang, sizes[i], cloth, PrimitiveType.Cube, 4f * (i - 1));
        }

        return props;
    }

    private static Material ClothMaterial()
    {
        string path = "Assets/_Project/Materials/Haven/Washing Cloth.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(Lit));
            AssetDatabase.CreateAsset(material, path);
        }

        // Undyed wool and linen, weathered.
        material.SetColor("_BaseColor", new Color(0.72f, 0.66f, 0.55f));
        material.SetFloat("_Smoothness", 0.05f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Part(Transform parent, string name, Vector3 at, Vector3 size, Material material, PrimitiveType shape, float tilt = 0f)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = at;
        part.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        part.transform.localScale = size;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    // ----- Wildlife -----

    private static Dictionary<string, Wildlife> WildlifePrefabs(SoundDefinition alarm)
    {
        var made = new Dictionary<string, Wildlife>();
        // Name, height (metres), walk and run speed, shy distance, and how much their colours are muted for the north.
        foreach ((string name, float height, float walk, float run, float shy) in new[]
                 {
                     ("Deer", 1.6f, 1.1f, 7.5f, 8f), ("Stag", 2.0f, 1.0f, 7f, 10f), ("Fox", 0.65f, 1.3f, 6f, 6f),
                 })
        {
            GameObject model = Import(name);
            if (model == null)
            {
                continue;
            }

            AnimatorController controller = Controller(name);
            Wildlife animal = Animal(name, model, controller, height, walk, run, shy, alarm, false);
            made[name] = animal;
        }

        return made;
    }

    private static GameObject Import(string name)
    {
        string path = $"{Wild}/{name}/{name}.fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            return null;
        }

        bool changed = importer.animationType != ModelImporterAnimationType.Generic;
        importer.animationType = ModelImporterAnimationType.Generic;
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            string shortName = clip.name.Contains("|") ? clip.name.Substring(clip.name.IndexOf('|') + 1) : clip.name;
            clip.name = shortName;
            clip.loopTime = shortName.StartsWith("Idle") || shortName == "Eating" || shortName == "Walk" || shortName == "Gallop";
        }

        importer.clipAnimations = clips;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.SaveAndReimport();
        _ = changed;
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static AnimationClip Clip(string name, string clip) =>
        AssetDatabase.LoadAllAssetsAtPath($"{Wild}/{name}/{name}.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == clip && !c.name.StartsWith("__preview__"));

    private static AnimatorController Controller(string name)
    {
        string path = $"{Wild}/{name}/{name} Wildlife.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState move = controller.CreateBlendTreeInController("Move", out BlendTree tree);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip(name, "Idle"), 0f);
        tree.AddChild(Clip(name, "Walk"), 1.2f);
        tree.AddChild(Clip(name, "Gallop"), 6f);
        AnimatorState eat = machine.AddState("Eating");
        eat.motion = Clip(name, "Eating");
        machine.defaultState = move;
        AnimatorStateTransition down = move.AddTransition(eat);
        down.AddCondition(AnimatorConditionMode.If, 0f, "Eating");
        down.hasExitTime = false;
        down.duration = 0.35f;
        AnimatorStateTransition up = eat.AddTransition(move);
        up.AddCondition(AnimatorConditionMode.IfNot, 0f, "Eating");
        up.hasExitTime = false;
        up.duration = 0.25f;
        return controller;
    }

    private static Wildlife Animal(string name, GameObject model, AnimatorController controller, float height, float walk, float run, float shy,
                                   SoundDefinition alarm, bool hollow)
    {
        var root = new GameObject(name);
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.name = "Model";
            float scale = height / Mathf.Max(0.01f, BoundsOf(body).size.y);
            body.transform.localScale = Vector3.one * scale;
            Animator animator = Ensure<Animator>(body);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            foreach (Renderer part in body.GetComponentsInChildren<Renderer>())
            {
                part.sharedMaterials = part.sharedMaterials.Select(m => Muted(name, m, hollow)).ToArray();
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = Mathf.Max(0.2f, height * 0.22f);
            agent.height = height;
            agent.speed = walk;
            agent.acceleration = 14f;
            agent.angularSpeed = 300f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var animal = root.AddComponent<Wildlife>();
            var data = new SerializedObject(animal);
            data.FindProperty("walkSpeed").floatValue = walk;
            data.FindProperty("runSpeed").floatValue = run;
            data.FindProperty("shyDistance").floatValue = shy;
            data.FindProperty("alarm").objectReferenceValue = name == "Fox" ? null : alarm;
            data.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, $"{Prefabs}/{name}.prefab").GetComponent<Wildlife>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // The pack's bright colours turned down for a northern wood; a hollowed animal is grey all through, with pale eyes.
    private static Material Muted(string animal, Material source, bool hollow)
    {
        if (source == null)
        {
            return null;
        }

        string path = $"{Wild}/{animal}/{animal} {(hollow ? "Hollow " : "")}{source.name.Replace('.', ' ')}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(Lit));
            AssetDatabase.CreateAsset(material, path);
        }

        Color colour = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.color : Color.grey;
        Color.RGBToHSV(colour, out float h, out float s, out float v);
        bool eye = source.name.ToLowerInvariant().Contains("eye");
        if (hollow)
        {
            colour = eye ? new Color(0.85f, 0.9f, 0.95f) : Color.HSVToRGB(h, s * 0.08f, Mathf.Lerp(v, 0.42f, 0.7f));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", eye ? new Color(1.2f, 1.3f, 1.5f) : Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            colour = Color.HSVToRGB(h, s * 0.8f, v * 0.88f);
        }

        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", eye ? 0.6f : 0.05f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ----- Glimpses -----

    private static List<GlimpseWatcher.Entry> GlimpsePrefabs(SoundDefinition gone)
    {
        var entries = new List<GlimpseWatcher.Entry>();
        GameObject deer = AssetDatabase.LoadAssetAtPath<GameObject>($"{Wild}/Deer/Deer.fbx");
        if (deer != null)
        {
            Glimpse hollow = HollowDeer(deer, gone);
            GlimpseWatcher.Entry hollowed = Entry("Hollowed Deer", hollow, 260f, Curve((0f, 1f), (5f, 1f), (7.5f, 0.25f), (17f, 0.1f), (19.5f, 0.9f), (24f, 1f)), new Vector2(18f, 24f), 1f);
            hollowed.moreWithBlight = 3f; // the blight round home brings them out, and nearer
            entries.Add(hollowed);
        }

        Glimpse huldra = HuldraGlimpse(gone);
        if (huldra != null)
        {
            entries.Add(Entry("Huldra", huldra, 240f, Curve((0f, 0.7f), (5f, 0.3f), (8f, 0f), (17.5f, 0f), (19f, 1f), (24f, 0.7f)), new Vector2(20f, 26f), 0.45f));
        }

        Glimpse spirit = Spirit(gone);
        entries.Add(Entry("Small Spirit", spirit, 90f, Curve((0f, 0.6f), (5f, 0.8f), (8f, 0.1f), (17f, 0.1f), (19f, 1f), (24f, 0.6f)), new Vector2(9f, 14f), 1f));
        return entries;
    }

    private static GlimpseWatcher.Entry Entry(string name, Glimpse prefab, float beyond, AnimationCurve hours, Vector2 distance, float weight) =>
        new GlimpseWatcher.Entry { name = name, prefab = prefab, beyondPortal = beyond, byHour = hours, distance = distance, weight = weight };

    private static AnimationCurve Curve(params (float hour, float value)[] keys) =>
        new AnimationCurve(keys.Select(k => new Keyframe(k.hour, k.value)).ToArray());

    private static Glimpse HollowDeer(GameObject model, SoundDefinition gone)
    {
        var root = new GameObject("Hollowed Deer");
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.name = "Model";
            body.transform.localScale = Vector3.one * (1.6f / Mathf.Max(0.01f, BoundsOf(body).size.y));
            Animator animator = Ensure<Animator>(body);
            animator.runtimeAnimatorController = StandingController("Deer", "Idle_Headlow");
            foreach (Renderer part in body.GetComponentsInChildren<Renderer>())
            {
                part.sharedMaterials = part.sharedMaterials.Select(m => Muted("Deer", m, true)).ToArray();
            }

            Glimpse glimpse = root.AddComponent<Glimpse>();
            Configure(glimpse, gone, 11f, true, 2.2f);
            return SavePrefab(root, $"{Prefabs}/Hollowed Deer.prefab").GetComponent<Glimpse>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static AnimatorController StandingController(string name, string clip)
    {
        string path = $"{Wild}/{name}/{name} Standing.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorState state = controller.layers[0].stateMachine.AddState("Stand");
        state.motion = Clip(name, clip) ?? Clip(name, "Idle");
        controller.layers[0].stateMachine.defaultState = state;
        return controller;
    }

    // The Huldra's body and her idle, nothing of her fight: seen between the trees, watching, then gone.
    private static Glimpse HuldraGlimpse(SoundDefinition gone)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Huldra.prefab");
        if (source == null)
        {
            return null;
        }

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Huldra Glimpse";
            foreach (Component part in root.GetComponentsInChildren<Component>(true).Reverse())
            {
                if (part is Transform || part is Renderer || part is MeshFilter || part is Animator || part is Light)
                {
                    continue;
                }

                Object.DestroyImmediate(part);
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Health") || t.name.Contains("Indicator") || t.name.Contains("Outline") || t.name.Contains("Mask")).ToList())
            {
                if (child != null && child != root.transform)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            Animator animator = root.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController is AnimatorController fight)
            {
                Motion idle = fight.layers[0].stateMachine.defaultState != null ? fight.layers[0].stateMachine.defaultState.motion : null;
                string path = $"{Prefabs}/Huldra Glimpse.controller";
                AssetDatabase.DeleteAsset(path);
                AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                AnimatorState state = controller.layers[0].stateMachine.AddState("Stand");
                state.motion = idle is BlendTree tree && tree.children.Length > 0 ? tree.children[0].motion : idle;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }

            Glimpse glimpse = root.AddComponent<Glimpse>();
            Configure(glimpse, gone, 14f, true, 2.6f);
            glimpse.SetSpirit(SpiritBonds.Huldra);
            return SavePrefab(root, $"{Prefabs}/Huldra Glimpse.prefab").GetComponent<Glimpse>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // A little light in the moss with two dark eyes: a vette peering out, gone into the ground if approached.
    private static Glimpse Spirit(SoundDefinition gone)
    {
        var root = new GameObject("Small Spirit");
        try
        {
            Material glow = SpiritMaterial();
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Glow";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            body.transform.localScale = new Vector3(0.22f, 0.26f, 0.22f);
            body.GetComponent<MeshRenderer>().sharedMaterial = glow;
            body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material eyes = AssetDatabase.LoadAssetAtPath<Material>($"{Wild}/Deer/Deer Eye_Black.mat");
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.name = "Eye";
                eye.transform.SetParent(root.transform, false);
                eye.transform.localPosition = new Vector3(side * 0.045f, 0.36f, 0.095f);
                eye.transform.localScale = Vector3.one * 0.035f;
                if (eyes != null)
                {
                    eye.GetComponent<MeshRenderer>().sharedMaterial = eyes;
                }
            }

            var holder = new GameObject("Light");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2.5f;
            light.intensity = 1.6f;
            light.color = new Color(0.65f, 0.95f, 0.75f);
            light.shadows = LightShadows.None;
            Glimpse glimpse = root.AddComponent<Glimpse>();
            Configure(glimpse, gone, 7f, false, 0.7f);
            return SavePrefab(root, $"{Prefabs}/Small Spirit.prefab").GetComponent<Glimpse>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Material SpiritMaterial()
    {
        string path = $"{Prefabs}/Small Spirit Glow.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(Lit));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.6f));
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(0.9f, 1.6f, 1.1f));
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Configure(Glimpse glimpse, SoundDefinition gone, float shy, bool watches, float sinks)
    {
        var data = new SerializedObject(glimpse);
        data.FindProperty("shyWithin").floatValue = shy;
        data.FindProperty("watches").boolValue = watches;
        data.FindProperty("sinks").floatValue = sinks;
        data.FindProperty("goneSound").objectReferenceValue = gone;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // ----- Offerings -----

    private static string Offerings(SoundDefinition offered)
    {
        string path = "Assets/_Project/Prefabs/World/Along the Way/Waystone.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            return "no waystone";
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Interactable interactable = Ensure<Interactable>(root);
            interactable.Configure(InteractionAnimation.GatherLow, 1.2f, 2.4f, false);
            if (root.GetComponent<Collider>() == null)
            {
                var hit = root.AddComponent<CapsuleCollider>();
                hit.center = new Vector3(0f, 1.2f, 0f);
                hit.height = 2.6f;
                hit.radius = 0.8f;
            }

            OfferingStone stone = Ensure<OfferingStone>(root);
            stone.Configure(offered);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return "offerings at the waystones";
    }

    // ----- Words in passing -----

    private static string Asides()
    {
        SettlementSettings settings = SettlementSettings.Current;
        if (settings == null)
        {
            return "no settlement settings";
        }

        settings.ConfigureAsides(
            new[]
            {
                "{0} is home. Looks at us like strangers, but home.",
                "Don't push {0}. It'll come back to them, bit by bit.",
                "{0} asked me my name this morning. Twice.",
            },
            new[]
            {
                "Mist's thick in the hollows today. Mind your step.",
                "Heard the deer down by the trees at first light.",
                "Smoke's going straight up. Fair day, then.",
                "Up early? The portal hums louder before the sun.",
            },
            new[]
            {
                "Crows have been at something out past the groves.",
                "If you find any good cloth out there, I'll not say no.",
                "My grandmother left bread on the old stones. Never hurts.",
                "The woods are quiet today. I don't like it quiet.",
            },
            new[]
            {
                "Keep to the light after dark. That's all I'll say.",
                "Something was singing down by the water last night.",
                "Fire's lit. Come and sit, if you've a moment.",
                "The owls are out. Means the night's begun proper.",
            });
        EditorUtility.SetDirty(settings);
        return "words in passing";
    }

    // ----- Recipe and scene -----

    private static string Recipe(Dictionary<string, Wildlife> animals)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        if (recipe == null)
        {
            return "no recipe";
        }

        recipe.grazers = new List<Wildlife>();
        foreach (string name in new[] { "Deer", "Stag" })
        {
            if (animals.TryGetValue(name, out Wildlife animal))
            {
                recipe.grazers.Add(animal);
            }
        }

        recipe.foxes = animals.TryGetValue("Fox", out Wildlife fox) ? new List<Wildlife> { fox } : new List<Wildlife>();
        EditorUtility.SetDirty(recipe);
        return $"recipe: {recipe.grazers.Count} grazers, {recipe.foxes.Count} fox";
    }

    // ----- Seasons -----

    // A short year, as a game counts days: a long summer, autumn turning and falling, a frosty winter, spring.
    private static SeasonCycle Year()
    {
        string path = "Assets/_Project/Data/World/Season Cycle.asset";
        var year = AssetDatabase.LoadAssetAtPath<SeasonCycle>(path);
        if (year == null)
        {
            year = ScriptableObject.CreateInstance<SeasonCycle>();
            AssetDatabase.CreateAsset(year, path);
        }

        year.Configure(new List<SeasonCycle.Season>
        {
            new SeasonCycle.Season { name = "Summer", days = 10 },
            new SeasonCycle.Season { name = "Autumn", days = 6, autumn = 0.9f, leafFall = 0.1f },
            new SeasonCycle.Season { name = "Late Autumn", days = 4, autumn = 1f, leafFall = 0.65f, frost = 0.15f },
            new SeasonCycle.Season { name = "Winter", days = 6, autumn = 1f, leafFall = 1f, frost = 0.75f, snows = true, lyingSnow = 0.6f },
            new SeasonCycle.Season { name = "Spring", days = 6, frost = 0.05f },
        }, 2);
        EditorUtility.SetDirty(year);
        return year;
    }

    // Broadleaf leaves turn and fall (maples, broadleaf shrubs); cedars and other needles keep theirs.
    private static string Deciduous()
    {
        int marked = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Art/Vegetation" }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null || material.shader == null || material.shader.name != "Safe Haven/Tree Wind" || !material.HasProperty("_Deciduous"))
            {
                continue;
            }

            string name = material.name.ToLowerInvariant();
            // By the plant, not the slot: the pack converter calls every leaf card "Needles". Ferns and grass stay as they are.
            bool broadleaf = !name.Contains("cedar") && (name.Contains("maple") || name.Contains("broadleaf") || name.Contains("forest shrub") || name.Contains("birch"));
            material.SetFloat("_Deciduous", broadleaf ? 1f : 0f);
            EditorUtility.SetDirty(material);
            marked += broadleaf ? 1 : 0;
        }

        return $"{marked} broadleaf leaf materials";
    }

    private static string Scene(List<GlimpseWatcher.Entry> glimpses, SoundDefinition owl, SoundDefinition wind, SoundDefinition nokk, SeasonCycle year)
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SafeHaven")
        {
            return "scene not set up (open SafeHaven)";
        }

        bool wasClean = !scene.isDirty;
        GameObject ambience = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "07 Ambience") ?? new GameObject("07 Ambience");
        Transform old = ambience.transform.Find("Living World");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        var root = new GameObject("Living World");
        root.transform.SetParent(ambience.transform, false);
        root.AddComponent<GlimpseWatcher>().Configure(glimpses, new Vector2(70f, 150f), 0.35f);
        root.AddComponent<Aftermath>();
        root.AddComponent<Seasons>().Configure(year, 2f);

        var chirp = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Bird Chirp.asset");
        var creak = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Tree Creak.asset");
        var howl = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Creatures/Wolf Howl.asset");
        // The dawn chorus: birdsong thick at first light, a few calls by day, none at night.
        Ambient(root, "Dawn Chorus", chirp, new Vector2(1.5f, 4f), new Vector2(6f, 18f), Curve((0f, 0f), (4f, 0f), (5f, 0.9f), (6.5f, 1f), (8.5f, 0.15f), (17f, 0.1f), (19f, 0f), (24f, 0f)));
        Ambient(root, "Owls", owl, new Vector2(20f, 45f), new Vector2(15f, 30f), Curve((0f, 1f), (4f, 0.8f), (5.5f, 0f), (20f, 0f), (21.5f, 1f), (24f, 1f)));
        Placed(root, "Ridge Wind", PlacedAmbience.Source.Ridge, wind, new Vector2(18f, 40f), Curve((0f, 0.6f), (12f, 1f), (24f, 0.6f)), 40f);
        Placed(root, "Dead Trees Creaking", PlacedAmbience.Source.DeadTrees, creak, new Vector2(14f, 30f), new AnimationCurve(), 30f);
        Placed(root, "Distant Howls", PlacedAmbience.Source.SpawnZone, howl, new Vector2(60f, 140f), Curve((0f, 1f), (4.5f, 0.6f), (6f, 0f), (19f, 0f), (20.5f, 1f), (24f, 1f)), 170f);
        Placed(root, "The Nokk", PlacedAmbience.Source.StillWater, nokk, new Vector2(45f, 90f), Curve((0f, 0.8f), (3f, 0.5f), (4f, 0f), (21f, 0f), (22.5f, 1f), (24f, 0.8f)), 45f);

        // Morning mist in the hollows and over the water.
        foreach (TerrainMist mist in Object.FindObjectsByType<TerrainMist>())
        {
            Undo.RecordObject(mist, "Morning Mist");
            mist.SetMorningAmount(0.8f);
            EditorUtility.SetDirty(mist);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (wasClean)
        {
            EditorSceneManager.SaveScene(scene);
            return "scene set up and saved";
        }

        return "scene set up (it had unsaved changes before, so not saved)";
    }

    private static void Ambient(GameObject root, string name, SoundDefinition sound, Vector2 every, Vector2 metres, AnimationCurve hours)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(root.transform, false);
        var ambient = holder.AddComponent<AmbientOneShots>();
        ambient.Configure(sound, every, metres, 0f);
        ambient.ConfigureHours(hours);
    }

    private static void Placed(GameObject root, string name, PlacedAmbience.Source source, SoundDefinition sound, Vector2 every, AnimationCurve hours, float radius)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(root.transform, false);
        holder.AddComponent<PlacedAmbience>().Configure(source, sound, every, hours, radius);
    }

    // ----- Shared -----

    private static Bounds BoundsOf(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer part in renderers)
        {
            bounds.Encapsulate(part.bounds);
        }

        return bounds;
    }

    private static GameObject SavePrefab(GameObject root, string path) => PrefabUtility.SaveAsPrefabAsset(root, path);

    // Unity's missing components aren't null to C#'s ??, so this asks properly.
    private static T Ensure<T>(GameObject target) where T : Component => target.TryGetComponent(out T found) ? found : target.AddComponent<T>();

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
