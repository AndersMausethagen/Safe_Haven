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
    private const string Model = Art + "/Horse.fbx";
    private const string ControllerPath = Art + "/Horse.controller";
    private const string PrefabPath = "Assets/_Project/Prefabs/World/Living/Wild Horse.prefab";
    private const string Data = "Assets/_Project/Data/Horses";
    private const string Items = "Assets/_Project/Data/Items";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes";
    private const string Lit = "Universal Render Pipeline/Lit";
    // Head up, ears to hooves: a horse about 1.6 m at the withers.
    private const float Height = 2.2f;
    private const float HipHeight = 0.86f;

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
        return $"horse {prefab.name} (seat {prefab.Seat.name}), {controller.name}, lasso, saddle, recipes, settings";
    }

    // ----- The model -----

    private static GameObject Import()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            string shortName = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
            clip.name = shortName;
            clip.loopTime = shortName.StartsWith("Idle") || shortName == "Eating" || shortName == "Walk" || shortName == "Gallop";
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return Load<GameObject>(Model);
    }

    private static AnimationClip Clip(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview__"));

    private static AnimatorController Controller()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Rear", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState move = controller.CreateBlendTreeInController("Move", out BlendTree tree);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip("Idle"), 0f);
        tree.AddChild(Clip("Walk"), HorseMotion.WalkClip);
        tree.AddChild(Clip("Gallop"), HorseMotion.GallopClip);
        machine.defaultState = move;

        AnimatorState eat = machine.AddState("Eating");
        eat.motion = Clip("Eating");
        Transition(move, eat, "Eating", true, 0.35f);
        Transition(eat, move, "Eating", false, 0.25f);

        // Bucking on the rope: the kick, then back to moving.
        AnimatorState rear = machine.AddState("Rear");
        rear.motion = Clip("Attack_Kick");
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
        death.motion = Clip("Death");
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

    // The pack's bright colours turned down for a northern wood, as the deer's are.
    private static Material Muted(Material source)
    {
        if (source == null)
        {
            return null;
        }

        string path = $"{Art}/Horse {source.name.Replace('.', ' ')}.mat";
        Material material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(Lit));
            AssetDatabase.CreateAsset(material, path);
        }

        Color colour = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.color : Color.grey;
        Color.RGBToHSV(colour, out float h, out float s, out float v);
        bool eye = source.name.ToLowerInvariant().Contains("eye");
        material.SetColor("_BaseColor", Color.HSVToRGB(h, s * 0.8f, v * 0.88f));
        material.SetFloat("_Smoothness", eye ? 0.6f : 0.08f);
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
            Bounds raw = BoundsOf(body);
            float scale = Height / Mathf.Max(0.01f, raw.size.y);
            body.transform.localScale = Vector3.one * scale;
            Bounds bounds = BoundsOf(body);
            // Facing +Z: the head is the end with the higher top.
            body.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            Animator animator = Ensure<Animator>(body);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (Renderer part in body.GetComponentsInChildren<Renderer>())
            {
                part.sharedMaterials = part.sharedMaterials.Select(Muted).ToArray();
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

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
            capsule.direction = bounds.size.z >= bounds.size.x ? 2 : 0;
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

            Transform seat = Seat(body, bounds);
            var horse = root.AddComponent<Horse>();
            root.AddComponent<HorseMotion>();
            root.AddComponent<HorseTaming>();
            var data = new SerializedObject(horse);
            data.FindProperty("definition").objectReferenceValue = definition;
            data.FindProperty("seat").objectReferenceValue = seat;
            data.FindProperty("seatOffset").vector3Value = seat.InverseTransformVector(Vector3.down * HipHeight);
            data.ApplyModifiedPropertiesWithoutUndo();

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

    // Where the rider sits: on top of the back between the legs, on the Torso bone so it moves with the body. The seat
    // faces the way the horse does in its resting pose, so the rider faces forward.
    private static Transform Seat(GameObject body, Bounds bounds)
    {
        Transform torso = body.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Torso")
                          ?? body.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Back") ?? body.transform;
        float top = bounds.min.y + bounds.size.y * 0.66f;
        var baked = new Mesh();
        foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.BakeMesh(baked, true);
            Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
            foreach (Vector3 vertex in baked.vertices)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(vertex);
                if (Mathf.Abs(world.x - bounds.center.x) < 0.22f && Mathf.Abs(world.z - bounds.center.z) < 0.3f)
                {
                    top = Mathf.Max(top, world.y);
                }
            }
        }

        Object.DestroyImmediate(baked);
        var seat = new GameObject("Seat").transform;
        seat.SetParent(torso, false);
        seat.position = new Vector3(bounds.center.x, top + 0.02f, bounds.center.z);
        seat.rotation = body.transform.parent.rotation;
        return seat;
    }

    private static Bounds BoundsOf(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
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
