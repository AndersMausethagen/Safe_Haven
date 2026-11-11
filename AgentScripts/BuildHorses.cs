using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Horses (`unity command script run --file AgentScripts/BuildHorses.cs --entry BuildHorses.Apply`): the Quaternius horse
/// (CC0, Art/Wildlife/Horse, converted from GLB in Blender) imported as Generic with its clips, an Animator that plays one
/// clip at a time (HorseMotion), its colours muted for the north as the deer's are, the Wild Horse prefab, its definition
/// (Data/Horses), the lasso and the hide saddle with their recipes (by hand; the Workshop), and HorseSettings. Safe to run
/// again: it replaces what it made.
/// </summary>
public static class BuildHorses
{
    private const string Art = "Assets/_Project/Art/Wildlife/Horse";
    private const string Model = "Assets/Malbers Animations/Horse AnimSet Pro/Undead Horse/Models/Undead_Horse_Re.fbx";
    private const string ControllerPath = Art + "/Horse.controller";
    private const string PrefabPath = "Assets/_Project/Prefabs/World/Living/Wild Horse.prefab";
    private const string Data = "Assets/_Project/Data/Horses";
    private const string Items = "Assets/_Project/Data/Items";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes";
    private const string Lit = "Universal Render Pipeline/Lit";
    // Head up, ears to hooves: a horse about 1.6 m at the withers.
    private const float Height = 2.2f;
    // The rider's hips stand 1.02 m above their feet and sit about 0.1 m above the saddle's seat.
    private const float HipHeight = 0.92f;

    public static string Apply()
    {
        Folder(Data);
        GameObject model = Import();
        AnimatorController controller = Controller();
        CombatStatsDefinition stats = Stats();
        ItemDefinition lasso = Item("Lasso", "lasso", "Lasso", "A noose of nettle cord, stiffened with hide. Thrown over a wild horse from close by, it holds it, if you can.",
            ItemCategory.Tool, 2, 12, 1.2f, Load<ItemDefinition>(Items + "/Materials/NettleCord.asset"), so => so.FindProperty("lasso").boolValue = true);
        ItemDefinition saddle = Item("HideSaddle", "hide_saddle", "Hide Saddle", "Ten hides stitched over a frame, with bags either side. On a tamed horse it carries eight stacks.",
            ItemCategory.Equipment, 2, 40, 6f, Load<ItemDefinition>(Items + "/HideRucksack.asset"), so => so.FindProperty("saddleSlots").intValue = 8);
        Recipe("Lasso", "lasso", lasso, CraftingStationType.None, 6f, ("Materials/NettleCord", 4), ("Materials/RoughHide", 1));
        Recipe("HideSaddle", "hide_saddle", saddle, CraftingStationType.Workbench, 14f, ("Materials/RoughHide", 10));

        HorseDefinition definition = Definition(lasso);
        Horse prefab = Prefab(model, controller, stats, definition);
        var data = new SerializedObject(definition);
        data.FindProperty("prefab").objectReferenceValue = prefab;
        data.ApplyModifiedPropertiesWithoutUndo();
        Settings(definition);

        AssetDatabase.SaveAssets();
        Load<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset").CollectAll();
        AssetDatabase.SaveAssets();
        return $"horse {prefab.name} (seat {prefab.Seat.name}), {controller.name}, gaits {lastGaits} m/s, lasso, saddle, recipes, settings";
    }

    // ----- The model -----

    // The Undead Horse & Knight pack (MalberS, Asset Store; only the realistic horse and the Horse AnimSet Pro clips it
    // needs were taken from it), made a living horse: its own body, mane and tail, a coat baked in Blender in place of the
    // rotting one (Art/Wildlife/Horse/HorseCoat.png), no armour; the pack's saddle shows once it is saddled.
    private const string Pack = "Assets/Malbers Animations/Horse AnimSet Pro/";
    private const string Clips = Pack + "2 - Animations/Animations Clips/Horse/";
    private static readonly string[] Gaits = { "Locomotion/H_Walk", "Locomotion/H_Trot", "Locomotion/H_Canter", "Locomotion/H_Gallop", "Locomotion/H_Sprint" };

    private static GameObject Import()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = false;
        importer.SaveAndReimport();
        foreach (string clip in Gaits.Select(g => g + "_IP").Concat(new[] { "Idles/H_Idle_01", "Actions/H_Eat" }))
        {
            Loop(Clips + clip + ".FBX", true);
        }

        foreach (string clip in new[] { "Attacks/H_Attack_Back_Legs", "Deaths/H_Death01", "Actions/H_Neigh", "Damage/H_Get Hit_Left" })
        {
            Loop(Clips + clip + ".FBX", false);
        }

        Texture(Art + "/HorseCoat.png", 2048);
        Texture(Pack + "Undead Horse/Materials & Textures/Realistic/Body/Undead_Horse_Mane_AlbedoTransparency.png", 1024);
        Texture(Pack + "Undead Horse/Materials & Textures/Realistic/Body/Undead_Horse_Mane_Normal.png", 1024, normal: true);
        return Load<GameObject>(Model);
    }

    private static void Loop(string path, bool loop)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            changed |= clip.loopTime != loop;
            clip.loopTime = loop;
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static void Texture(string path, int size, bool normal = false)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null || (importer.maxTextureSize == size && (importer.textureType == TextureImporterType.NormalMap) == normal))
        {
            return;
        }

        importer.maxTextureSize = size;
        if (normal)
        {
            importer.textureType = TextureImporterType.NormalMap;
        }

        importer.SaveAndReimport();
    }

    private static AnimationClip Clip(string file) =>
        AssetDatabase.LoadAllAssetsAtPath(Clips + file + ".FBX").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    // What each gait's clip shows, from its root-motion twin: metres a second at the pack's size, times the body's scale.
    private static float[] GaitSpeeds(float scale) => Gaits.Select(g =>
    {
        AnimationClip clip = Clip(g);
        float Travel(string axis)
        {
            EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip).FirstOrDefault(b => b.path == "" && b.propertyName == "RootT." + axis);
            AnimationCurve curve = binding.propertyName != null ? AnimationUtility.GetEditorCurve(clip, binding) : null;
            return curve != null ? curve.Evaluate(clip.length) - curve.Evaluate(0f) : 0f;
        }

        return Mathf.Round(new Vector2(Travel("x"), Travel("z")).magnitude / clip.length * scale * 100f) / 100f;
    }).ToArray();

    private static AnimatorController Controller()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Gait", AnimatorControllerParameterType.Float);
        controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Rear", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState move = controller.CreateBlendTreeInController("Move", out BlendTree tree);
        tree.blendParameter = "Gait";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip("Idles/H_Idle_01"), 0f);
        for (int i = 0; i < Gaits.Length; i++)
        {
            tree.AddChild(Clip(Gaits[i] + "_IP"), i + 1);
        }

        machine.defaultState = move;

        AnimatorState eat = machine.AddState("Eating");
        eat.motion = Clip("Actions/H_Eat");
        Transition(move, eat, "Eating", true, 0.35f);
        Transition(eat, move, "Eating", false, 0.25f);

        // Bucking on the rope: a kick of the back legs, then back to moving.
        AnimatorState rear = machine.AddState("Rear");
        rear.motion = Clip("Attacks/H_Attack_Back_Legs");
        AnimatorStateTransition toRear = machine.AddAnyStateTransition(rear);
        toRear.AddCondition(AnimatorConditionMode.If, 0f, "Rear");
        toRear.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        toRear.duration = 0.12f;
        toRear.canTransitionToSelf = false;
        AnimatorStateTransition fromRear = rear.AddTransition(move);
        fromRear.hasExitTime = true;
        fromRear.exitTime = 0.9f;
        fromRear.duration = 0.2f;

        AnimatorState death = machine.AddState("Death");
        death.motion = Clip("Deaths/H_Death01");
        AnimatorStateTransition die = machine.AddAnyStateTransition(death);
        die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        die.duration = 0.15f;
        die.canTransitionToSelf = false;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void Transition(AnimatorState from, AnimatorState to, string parameter, bool when, float seconds)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(when ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        transition.hasExitTime = false;
        transition.duration = seconds;
    }

    private static Material Material(string name, Texture2D albedo, Color colour, float smoothness, Texture2D normal = null, bool cutout = false)
    {
        string path = $"{Art}/Horse {name}.mat";
        Material material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(Lit));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", albedo);
        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", smoothness);
        material.SetTexture("_BumpMap", normal);
        if (normal != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            material.DisableKeyword("_NORMALMAP");
        }

        // Hair cards: cut out at their edges and seen from both sides.
        material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
        material.SetFloat("_Cutoff", 0.45f);
        material.SetFloat("_Cull", cutout ? 0f : 2f);
        if (cutout)
        {
            material.EnableKeyword("_ALPHATEST_ON");
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ----- Data -----

    private static CombatStatsDefinition Stats()
    {
        string path = Data + "/Horse Combat Stats.asset";
        CombatStatsDefinition stats = Load<CombatStatsDefinition>(path);
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<CombatStatsDefinition>();
            AssetDatabase.CreateAsset(stats, path);
        }

        // Max health (the horse's own roll replaces it), a little armour, and some resistance to being pushed about.
        var so = new SerializedObject(stats);
        SerializedProperty list = so.FindProperty("stats");
        list.arraySize = 3;
        (StatType stat, float value)[] values = { (StatType.MaxHealth, 160f), ((StatType)3, 0.05f), ((StatType)4, 0.4f) };
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue = (int)values[i].stat;
            list.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = values[i].value;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return stats;
    }

    private static HorseDefinition Definition(ItemDefinition lasso)
    {
        string path = Data + "/Wild Horse.asset";
        HorseDefinition definition = Load<HorseDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<HorseDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        var so = new SerializedObject(definition);
        so.FindProperty("lasso").objectReferenceValue = lasso;
        so.FindProperty("barley").objectReferenceValue = Load<ItemDefinition>(Items + "/Materials/Barley.asset");
        // A quiet player may come within 4 m; walking (5 m footsteps) it bolts at about 10 m, sneaking at about 6 m, so the
        // lasso (9 m) is thrown from a crouch.
        so.FindProperty("shyDistance").floatValue = 4f;
        so.FindProperty("perHeardMetre").floatValue = 1.3f;
        so.FindProperty("lassoRange").floatValue = 9f;
        so.FindProperty("pullStrain").floatValue = 0.35f;
        so.FindProperty("tapHold").floatValue = 0.1f;
        so.FindProperty("tapDrain").floatValue = 3f;
        so.FindProperty("trotSpeed").floatValue = 6f;
        // Bay, chestnut, dun, grey, black: the colours of northern horses.
        Color[] coats =
        {
            new Color(0.42f, 0.25f, 0.14f), new Color(0.55f, 0.3f, 0.14f), new Color(0.66f, 0.55f, 0.38f), new Color(0.6f, 0.6f, 0.58f),
            new Color(0.16f, 0.14f, 0.13f),
        };
        SerializedProperty list = so.FindProperty("coats");
        list.arraySize = coats.Length;
        for (int i = 0; i < coats.Length; i++)
        {
            list.GetArrayElementAtIndex(i).colorValue = coats[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static ItemDefinition Item(string file, string id, string name, string description, ItemCategory category, int tier, int value, float weight,
        ItemDefinition iconFrom, System.Action<SerializedObject> kind)
    {
        string path = $"{Items}/{file}.asset";
        ItemDefinition item = Load<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("category").intValue = (int)category;
        so.FindProperty("tier").intValue = tier;
        so.FindProperty("value").intValue = value;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("stackable").boolValue = false;
        so.FindProperty("maximumStack").intValue = 1;
        // A stand-in picture until it has its own (LocalSpawn).
        if (so.FindProperty("icon").objectReferenceValue == null && iconFrom != null)
        {
            so.FindProperty("icon").objectReferenceValue = iconFrom.Icon;
        }

        kind(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void Recipe(string file, string id, ItemDefinition output, CraftingStationType station, float seconds, params (string item, int amount)[] cost)
    {
        string path = $"{Recipes}/{file}.asset";
        RecipeDefinition recipe = Load<RecipeDefinition>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        var so = new SerializedObject(recipe);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("output").objectReferenceValue = output;
        so.FindProperty("outputAmount").intValue = 1;
        so.FindProperty("station").intValue = (int)station;
        so.FindProperty("craftSeconds").floatValue = seconds;
        so.FindProperty("knownFromStart").boolValue = true;
        SerializedProperty items = so.FindProperty("itemCost");
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Load<ItemDefinition>($"{Items}/{cost[i].item}.asset");
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static void Settings(HorseDefinition definition)
    {
        string path = "Assets/_Project/Resources/HorseSettings.asset";
        HorseSettings settings = Load<HorseSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<HorseSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        var so = new SerializedObject(settings);
        so.FindProperty("wildHorse").objectReferenceValue = definition;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    // ----- The prefab -----

    private static Horse Prefab(GameObject model, AnimatorController controller, CombatStatsDefinition stats, HorseDefinition definition)
    {
        var root = new GameObject("Wild Horse");
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.name = "Model";
            Transform Part(string name) => body.transform.Find(name);
            // A living horse: no armour, no reins; the saddle only once it wears one.
            foreach (string part in new[] { "Horse_Undead_Armor_Re", "Horse_Undead_Reins_Re", "Horse_Undead_ReinsHead_Re" })
            {
                Transform found = Part(part);
                if (found != null)
                {
                    found.gameObject.SetActive(false);
                }
            }

            Transform saddleModel = Part("Horse_Undead_Saddle_Re");
            string textures = Pack + "Undead Horse/Materials & Textures/";
            Material coat = Material("Main", Load<Texture2D>(Art + "/HorseCoat.png"), Color.white, 0.22f);
            Material hair = Material("Hair", Load<Texture2D>(textures + "Realistic/Body/Undead_Horse_Mane_AlbedoTransparency.png"), new Color(0.2f, 0.16f, 0.13f),
                0.3f, Load<Texture2D>(textures + "Realistic/Body/Undead_Horse_Mane_Normal.png"), cutout: true);
            Material eyes = Material("Eye", null, new Color(0.03f, 0.025f, 0.02f), 0.85f);
            Material leather = Material("Saddle", null, new Color(0.32f, 0.2f, 0.11f), 0.3f);
            void Wear(string part, Material material)
            {
                Transform found = Part(part);
                if (found != null && found.TryGetComponent(out Renderer drawn))
                {
                    drawn.sharedMaterials = Enumerable.Repeat(material, drawn.sharedMaterials.Length).ToArray();
                    drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }

            Wear("Horse_Undead_Body_Re", coat);
            Wear("Horse_Undead_Mane_Re", hair);
            Wear("Horse_Undead_Tail_Re", hair);
            Wear("Horse_Undead_Eyes_Re", eyes);
            Wear("Horse_Undead_Saddle_Re", leather);

            Bounds raw = BoundsOf(body);
            float scale = Height / Mathf.Max(0.01f, raw.size.y);
            body.transform.localScale = Vector3.one * scale;
            // Facing +Z: turn the body round if its head is behind its hips.
            Transform head = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Head");
            Transform hips = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Pelvis");
            if (head != null && hips != null && head.position.z < hips.position.z)
            {
                body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            Bounds bounds = BoundsOf(body);
            body.transform.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            Animator animator = Ensure<Animator>(body);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            bounds = BoundsOf(body);
            float length = Mathf.Max(bounds.size.x, bounds.size.z);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.55f;
            agent.height = 1.8f;
            agent.speed = 1.6f;
            agent.acceleration = 12f;
            agent.angularSpeed = 260f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = 0.48f;
            capsule.height = length * 0.85f;
            capsule.center = new Vector3(0f, 1.15f, 0f);

            var combatant = root.AddComponent<Combatant>();
            var combat = new SerializedObject(combatant);
            combat.FindProperty("faction").intValue = (int)Faction.Ally;
            combat.FindProperty("baseStats").objectReferenceValue = stats;
            combat.ApplyModifiedPropertiesWithoutUndo();
            var health = root.AddComponent<Damageable>();
            var life = new SerializedObject(health);
            life.FindProperty("destroyDelay").floatValue = 40f;
            life.ApplyModifiedPropertiesWithoutUndo();
            var interactable = root.AddComponent<Interactable>();
            var use = new SerializedObject(interactable);
            use.FindProperty("animationType").intValue = 0;
            use.FindProperty("duration").floatValue = 0f;
            use.FindProperty("range").floatValue = 2.6f;
            use.FindProperty("deactivateOnComplete").boolValue = false;
            use.FindProperty("kind").intValue = (int)InteractionKind.Use;
            use.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<SaveableEntity>();

            Transform seat = Seat(body, saddleModel != null ? saddleModel.GetComponent<SkinnedMeshRenderer>() : null, bounds);
            if (saddleModel != null)
            {
                saddleModel.gameObject.SetActive(false);
            }

            var horse = root.AddComponent<Horse>();
            var motion = root.AddComponent<HorseMotion>();
            root.AddComponent<HorseTaming>();
            var data = new SerializedObject(horse);
            data.FindProperty("definition").objectReferenceValue = definition;
            data.FindProperty("seat").objectReferenceValue = seat;
            data.FindProperty("seatOffset").vector3Value = seat.InverseTransformVector(Vector3.down * HipHeight);
            data.FindProperty("saddleModel").objectReferenceValue = saddleModel != null ? saddleModel.gameObject : null;
            data.ApplyModifiedPropertiesWithoutUndo();
            var moving = new SerializedObject(motion);
            float[] speeds = GaitSpeeds(scale);
            SerializedProperty gaits = moving.FindProperty("gaitSpeeds");
            gaits.arraySize = speeds.Length;
            for (int i = 0; i < speeds.Length; i++)
            {
                gaits.GetArrayElementAtIndex(i).floatValue = speeds[i];
            }

            moving.ApplyModifiedPropertiesWithoutUndo();
            lastGaits = string.Join(", ", speeds.Select(v => v.ToString("0.00")));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            var entity = new SerializedObject(saved.GetComponent<SaveableEntity>());
            entity.FindProperty("prefabId").stringValue = AssetDatabase.AssetPathToGUID(PrefabPath);
            entity.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(saved);
            return saved.GetComponent<Horse>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static string lastGaits = "";

    // Where the rider sits: in the saddle's seat (its lowest point between pommel and cantle, not its top), on the spine so
    // it moves with the back, facing the way the horse does. The seat was once set on the saddle's top edge: 0.2 m too high.
    private static Transform Seat(GameObject body, SkinnedMeshRenderer saddle, Bounds fallback)
    {
        Transform[] bones = body.GetComponentsInChildren<Transform>(true);
        Transform spine = bones.FirstOrDefault(t => t.name == "Spine1") ?? bones.FirstOrDefault(t => t.name == "Spine") ?? body.transform;
        Vector3 at = new Vector3(fallback.center.x, fallback.min.y + fallback.size.y * 0.7f, fallback.center.z);
        if (saddle != null)
        {
            var baked = new Mesh();
            saddle.BakeMesh(baked, true);
            Matrix4x4 toWorld = saddle.transform.localToWorldMatrix;
            Bounds bounds = saddle.bounds;
            Vector3 forward = body.transform.parent.forward, right = body.transform.parent.right;
            float seat = float.MinValue;
            foreach (Vector3 vertex in baked.vertices)
            {
                Vector3 offset = toWorld.MultiplyPoint3x4(vertex) - bounds.center;
                if (Mathf.Abs(Vector3.Dot(offset, right)) < 0.08f && Mathf.Abs(Vector3.Dot(offset, forward)) < 0.08f)
                {
                    seat = Mathf.Max(seat, bounds.center.y + offset.y);
                }
            }

            Object.DestroyImmediate(baked);
            at = new Vector3(bounds.center.x, seat > float.MinValue ? seat : bounds.center.y, bounds.center.z);
        }

        var made = new GameObject("Seat").transform;
        made.SetParent(spine, false);
        made.position = at;
        made.rotation = body.transform.parent.rotation;
        return made;
    }

    private static Bounds BoundsOf(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>().Where(r => r.gameObject.activeInHierarchy).ToArray();
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one);
        foreach (Renderer part in renderers)
        {
            bounds.Encapsulate(part.bounds);
        }

        return bounds;
    }

    private static T Ensure<T>(GameObject target) where T : Component => target.TryGetComponent(out T found) ? found : target.AddComponent<T>();

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    private static void Folder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
