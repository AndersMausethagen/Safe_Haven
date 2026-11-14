#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The undead knights (`unity command script run --file AgentScripts/BuildUndeadKnights.cs --entry BuildUndeadKnights.Apply`):
/// the Undead Horse &amp; Knight pack's knight on its undead horse, a rare elite of the raids in three kinds - Fire, Ice and
/// Toxic - each in its own hide, barding and glow. The knight is a monster (MonsterAI on the Draug's body, seated on the
/// horse's back) with the pack's rider clips in place of the Draug's (an override controller: its strikes are the saddle
/// sword's), the horse a KnightSteed driven by how the monster moves. They slash from the saddle and charge, burn, chill or
/// poison by kind, take what their kind fears, and drop grave iron, essence and now and then a barding. They join each
/// raid's Creatures and Hunters from tier 2. Run after BuildHorses and BuildCreatures. Safe to run again.
/// </summary>
public static class BuildUndeadKnights
{
    private const string Pack = "Assets/Malbers Animations/Horse AnimSet Pro/";
    private const string HorseModel = Pack + "Undead Horse/Models/Undead_Horse_Re.fbx";
    private const string KnightModel = Pack + "Undead Horse/Models/Undead_Knight.fbx";
    private const string Textures = Pack + "Undead Horse/Materials & Textures/";
    private const string RiderClips = Pack + "2 - Animations/Animations Clips/Rider/";
    private const string Art = "Assets/_Project/Art/Enemies/Undead Knight";
    private const string HorseArt = "Assets/_Project/Art/Wildlife/Horse";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string AttackFolder = "Assets/_Project/Data/Combat/Attacks/";
    private const string StatsFolder = "Assets/_Project/Data/Combat/Stats/";
    private const string Controller = "Assets/_Project/Animation/Controllers/Beasts/Undead Knight.overrideController";
    private const string Lit = "Universal Render Pipeline/Lit";
    private const float HorseHeight = 2.2f;
    private const float HipHeight = 0.92f;

    private sealed class Kind
    {
        public string Name, Body, Text;
        public DamageType Damage;
        public string Status;
        public int Barding;
        public Color Glow;
        public (StatType stat, float value)[] Resists;
        public string Drop;
        public string Lore, Advice;
    }

    private static readonly Kind[] Kinds =
    {
        new Kind
        {
            Name = "Fire", Body = "Fire", Damage = DamageType.Fire, Status = null, Barding = 1, Glow = new Color(1f, 0.45f, 0.12f),
            Text = "A dead rider in blackened plate on a horse that smoulders from within. Its blade burns. Frost undoes it; fire only feeds it.",
            Resists = new[] { (StatType.FireResistance, 0.6f), (StatType.FrostResistance, -0.35f) }, Drop = "BlackenedBarding",
            Lore = "Some who rode out against the dark never came home, and some came back wrong. The burning knights ride the edges of the forest where the old fires were, their horses smouldering under the plate, and anything they cut keeps burning.",
            Advice = "Frost cuts deep into it, fire hardly at all. It charges from far off: step aside as it comes and strike its flank as it turns. Its blows burn, so don't trade them. It frightens horses.",
        },
        new Kind
        {
            Name = "Ice", Body = "Ice", Damage = DamageType.Frost, Status = "Chill", Barding = 2, Glow = new Color(0.45f, 0.85f, 1f),
            Text = "A dead rider rimed in frost on a horse of ice and bone. Its blows chill to the marrow. Fire undoes it; frost is its own.",
            Resists = new[] { (StatType.FrostResistance, 0.6f), (StatType.FireResistance, -0.35f) }, Drop = "SilveredBarding",
            Lore = "A rider of the long winters, its armour furred with frost and its horse more ice than bone. Where it has passed, the grass is white in the morning, and the cold does not lift.",
            Advice = "Fire undoes it; frost is its own. Its blows chill and slow: keep something warm to cure it, and keep moving, for a slowed traveller can't sidestep its charge. It frightens horses.",
        },
        new Kind
        {
            Name = "Toxic", Body = "Toxic", Damage = DamageType.Physical, Status = "Poison", Barding = 3, Glow = new Color(0.45f, 1f, 0.3f),
            Text = "A dead rider out of the bog, its horse grown through with glowing green rot. Its blade poisons. Holy light and fire undo it.",
            Resists = new[] { (StatType.HolyResistance, -0.4f), (StatType.FireResistance, -0.2f) }, Drop = "PaintedBarding",
            Lore = "The bog keeps what falls into it. This one fell in long ago, horse and rider, and came up again grown through with the same green rot as the wild horses of the mire. It rides out when the mist is thick.",
            Advice = "Holy light and fire both hurt it. Its blade poisons, so carry an antidote, and don't stand in the fog it leaves. It charges from far off: sidestep and strike as it turns. It frightens horses.",
        },
    };

    private static string report = "";

    public static string Apply()
    {
        report = "";
        Folder("Assets/_Project/Art/Enemies", "Undead Knight");
        Import();
        AnimatorOverrideController controller = KnightController();
        var made = new List<GameObject>();
        foreach (Kind kind in Kinds)
        {
            made.Add(Build(kind, controller));
        }

        string spawns = AddToRaids(made);
        string journal = InJournal();
        AssetDatabase.SaveAssets();
        Load<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset").CollectAll();
        AssetDatabase.SaveAssets();
        return $"{made.Count} knights ({string.Join(", ", made.Select(m => m.name))}); {spawns}; {journal}{report}";
    }

    // ----- The models and the knight's clips -----

    private static void Import()
    {
        var knight = (ModelImporter)AssetImporter.GetAtPath(KnightModel);
        knight.materialImportMode = ModelImporterMaterialImportMode.None;
        knight.SaveAndReimport();
        foreach (string texture in new[] { "Knight_AlbedoTransparency", "Knight_Emission", "Knight_Rough_Metallic_AO" })
        {
            Texture($"{Textures}Knight/{texture}.png", 1024, false);
        }

        Texture($"{Textures}Knight/Knight_Normal.png", 1024, true);
        foreach (string body in new[] { "Fire", "Ice" })
        {
            Texture($"{Textures}Realistic/Body/Undead_Horse_{body}_AlbedoTransparency.png", 1024, false);
            Texture($"{Textures}Realistic/Body/Undead_Horse_{body}_Normal.png", 1024, true);
            Texture($"{Textures}Realistic/Body/Undead_Horse_{body}_MetallicSmoothness.png", 1024, false);
            Texture($"{Textures}Realistic/Body/Undead_Horse_{body}_Emission.png", 1024, false);
        }
    }

    private static AnimationClip RiderClip(string file, string take = null) =>
        AssetDatabase.LoadAllAssetsAtPath(RiderClips + file + ".FBX").OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (take == null || c.name == take));

    // The Draug's controller with the pack's rider clips in its places: it sits and canters, swings the saddle sword for
    // every strike, rears for its roar, flinches and falls as a rider.
    private static AnimatorOverrideController KnightController()
    {
        var draug = Load<AnimatorOverrideController>("Assets/_Project/Animation/Controllers/Beasts/Draug.overrideController");
        var controller = Load<AnimatorOverrideController>(Controller);
        if (controller == null)
        {
            controller = new AnimatorOverrideController(draug.runtimeAnimatorController);
            AssetDatabase.CreateAsset(controller, Controller);
        }

        controller.runtimeAnimatorController = draug.runtimeAnimatorController;
        string sword = "Weapons While Riding/Rider_Weapon_Sword";
        var wanted = new Dictionary<string, AnimationClip>
        {
            { "Zombie_Idle_Loop", RiderClip("Idles/Rider_Idle_01") },
            { "Zombie_Walk_Fwd_Loop", RiderClip("Locomotion/Rider_Canter") },
            { "HumanM@Attack1H01_R", RiderClip(sword, "Sword Attack Right Forward RH") },
            { "HumanM@Death01", RiderClip("Deaths/Rider_Death01") },
            { "Hit_Knockback", RiderClip("Damage/Rider_Get Hit_Front_Left") },
            { "Combat_2H_Attack", RiderClip(sword, "Sword Attack Right Forward RH") },
            { "Combat_2H_Attack_Base", RiderClip(sword, "Sword Attack Left Forward RH") },
            { "Combat_1H_Attack", RiderClip(sword, "Sword Attack Right Backward RH") },
            { "Combat_1H_Attack02", RiderClip(sword, "Sword Attack Left Backward RH") },
            { "Combat_Unarmed_Attack", RiderClip("Attacks/Rider_Attack_Front_Legs") },
            { "Emote_Roar", RiderClip("Actions/Rider_Neigh") },
            { "Combat_1H_Hit", RiderClip("Damage/Rider_Get Hit_Left") },
            { "Combat_1H_Dodge", RiderClip("Idles/Rider_Idle04_Step") },
            { "Crouch_Idle_Loop", RiderClip("Idles/Rider_Idle_HeadShake") },
        };
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        int set = 0;
        for (int i = 0; i < overrides.Count; i++)
        {
            if (wanted.TryGetValue(overrides[i].Key.name, out AnimationClip clip) && clip != null)
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
                set++;
            }
        }

        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
        report += $"; knight controller: {set} clips";
        return controller;
    }

    // ----- One knight -----

    private static GameObject Build(Kind kind, AnimatorOverrideController controller)
    {
        string name = $"Undead Knight {kind.Name}";
        AttackDefinition slash = Attack($"Knight{kind.Name}Slash", "DraugCleave", $"{kind.Name} Knight's Slash", so =>
        {
            so.FindProperty("range").floatValue = 3.1f;
            so.FindProperty("arc").floatValue = 120f;
            so.FindProperty("hitDelay").floatValue = 0.55f;
            so.FindProperty("recoveryTime").floatValue = 1.3f;
            so.FindProperty("damageType").intValue = (int)kind.Damage;
            so.FindProperty("appliesStatus").objectReferenceValue = kind.Status != null ? Load<StatusEffectDefinition>($"Assets/_Project/Data/Combat/Status/{kind.Status}.asset") : null;
            so.FindProperty("statusChance").floatValue = 0.45f;
        });
        AttackDefinition charge = Attack($"Knight{kind.Name}Charge", "DraugLunge", $"{kind.Name} Knight's Charge", so =>
        {
            so.FindProperty("range").floatValue = 2.4f;
            so.FindProperty("arc").floatValue = 60f;
            so.FindProperty("hitDelay").floatValue = 0.7f;
            so.FindProperty("recoveryTime").floatValue = 1.6f;
            so.FindProperty("poiseDamage").floatValue = 60f;
            so.FindProperty("damageType").intValue = (int)kind.Damage;
        });
        CombatStatsDefinition stats = Stats($"UndeadKnight{kind.Name}Stats", "DraugStats", 520f, 45f, kind.Resists);
        MonsterDefinition definition = Monster($"UndeadKnight{kind.Name}", $"{kind.Name} Knight", kind.Text, stats, slash, kind);
        Edit(definition, so =>
        {
            SetAttacks(so, (slash, MonsterAttackKind.Melee, 32f, 0f, 3f, 10, 1f, 0f, 0f, 0f), (charge, MonsterAttackKind.Charge, 45f, 5f, 14f, 20, 8f, 12f, 10f, 2.2f));
            SetDrops(so, ("GraveIron", 2, 3, 0.9f), ("OldSilverPenny", 2, 4, 0.7f), (kind.Drop, 1, 1, 0.2f), ("!CursedEssence", 4, 6, 1f));
        });

        string path = Enemies + name + ".prefab";
        if (Load<GameObject>(path) == null)
        {
            AssetDatabase.CopyAsset(Enemies + "Draug.prefab", path);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = name;
            foreach (string old in new[] { "Model", "Steed" })
            {
                Transform found = root.transform.Find(old);
                if (found != null)
                {
                    Object.DestroyImmediate(found.gameObject);
                }
            }

            Animator steedAnimator = Steed(root.transform, kind, out Transform seat);
            Animator knight = Knight(seat, kind, controller);
            var driver = root.GetComponent<MonsterAnimationDriver>();
            if (driver != null)
            {
                var data = new SerializedObject(driver);
                data.FindProperty("animator").objectReferenceValue = knight;
                data.FindProperty("strideSpeed").floatValue = 6f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            KnightSteed steed = root.GetComponent<KnightSteed>() ?? root.AddComponent<KnightSteed>();
            var steedData = new SerializedObject(steed);
            steedData.FindProperty("steed").objectReferenceValue = steedAnimator;
            HorseMotion living = HorseSettings.Current.WildHorse.Prefab.GetComponent<HorseMotion>();
            var livingData = new SerializedObject(living);
            SerializedProperty from = livingData.FindProperty("gaitSpeeds");
            SerializedProperty to = steedData.FindProperty("gaitSpeeds");
            to.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++)
            {
                to.GetArrayElementAtIndex(i).floatValue = from.GetArrayElementAtIndex(i).floatValue;
            }

            steedData.FindProperty("turnInPlaceRate").floatValue = livingData.FindProperty("turnInPlaceRate").floatValue;
            steedData.FindProperty("hooves").objectReferenceValue = HorseSettings.Current.WildHorse.Hooves;
            steedData.ApplyModifiedPropertiesWithoutUndo();
            var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.radius = 0.6f;
                agent.height = 2.6f;
                agent.angularSpeed = 160f;
            }

            Transform bar = root.transform.Find("HealthBar");
            if (bar != null)
            {
                Object.DestroyImmediate(bar.gameObject);
            }

            Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var prefab = Load<GameObject>(path);
        Edit(definition, so => so.FindProperty("prefab").objectReferenceValue = prefab);
        MonsterPrefabBuilder.Update(prefab, definition);
        prefab.GetComponent<SaveableEntity>().RefreshPrefabId();
        return prefab;
    }

    // The undead horse: its kind's hide, mane and eyes, the bridle, saddle and its kind's barding, a haze of its element,
    // and the living horse's Animator.
    private static Animator Steed(Transform root, Kind kind, out Transform seat)
    {
        var body = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(HorseModel), root);
        PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        body.name = "Steed";
        Transform Part(string part) => body.transform.Find(part);
        string realistic = Textures + "Realistic/Body/Undead_Horse_";
        Material hide = kind.Name == "Toxic" ? Load<Material>(HorseArt + "/Horse Toxic Body.mat")
            : Material($"{kind.Name} Steed", Tex($"{realistic}{kind.Body}_AlbedoTransparency.png"), Color.white, 0.4f, Tex($"{realistic}{kind.Body}_Normal.png"),
                Tex($"{realistic}{kind.Body}_Emission.png"), kind.Glow * 2f);
        Material hair = Material($"{kind.Name} Steed Mane", Tex(realistic + "Mane_AlbedoTransparency.png"), Color.Lerp(new Color(0.12f, 0.12f, 0.12f), kind.Glow, 0.25f), 0.3f,
            Tex(realistic + "Mane_Normal.png"), null, Color.black, cutout: true);
        Material eyes = Material($"{kind.Name} Steed Eye", null, kind.Glow, 0.9f, null, null, kind.Glow * 4f);
        Material barding = Material($"{kind.Name} Steed Barding", Tex($"{Textures}Realistic/Armor/Undead_Horse Armor 0{kind.Barding + 1}_D.png"), Color.white, 0.45f,
            Tex($"{Textures}Realistic/Armor/Undead_Horse Armor 0{kind.Barding + 1}_N.png"), Tex($"{Textures}Realistic/Armor/Undead_Horse Armor 0{kind.Barding + 1}_E.png"), kind.Glow * 1.8f);
        barding.SetFloat("_Metallic", 0.55f);
        Material leather = Load<Material>(HorseArt + "/Horse Saddle.mat");
        void Wear(string part, Material material)
        {
            Transform found = Part(part);
            if (found != null && found.TryGetComponent(out Renderer drawn))
            {
                drawn.sharedMaterials = Enumerable.Repeat(material, drawn.sharedMaterials.Length).ToArray();
                drawn.gameObject.SetActive(true);
            }
        }

        Wear("Horse_Undead_Body_Re", hide);
        Wear("Horse_Undead_Mane_Re", hair);
        Wear("Horse_Undead_Tail_Re", hair);
        Wear("Horse_Undead_Eyes_Re", eyes);
        Wear("Horse_Undead_Saddle_Re", leather);
        Wear("Horse_Undead_Reins_Re", leather);
        Wear("Horse_Undead_ReinsHead_Re", leather);
        Wear("Horse_Undead_Armor_Re", barding);

        Bounds raw = BoundsOf(body);
        float scale = HorseHeight / Mathf.Max(0.01f, raw.size.y);
        body.transform.localScale = Vector3.one * scale;
        Transform head = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Head");
        Transform hips = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Pelvis");
        if (head != null && hips != null && head.position.z < hips.position.z)
        {
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        Bounds bounds = BoundsOf(body);
        body.transform.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        Animator animator = body.GetComponent<Animator>() ?? body.AddComponent<Animator>();
        animator.runtimeAnimatorController = Load<RuntimeAnimatorController>(HorseArt + "/Horse.controller");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        Transform saddle = Part("Horse_Undead_Saddle_Re");
        seat = Seat(body, saddle != null ? saddle.GetComponent<SkinnedMeshRenderer>() : null, BoundsOf(body));
        Haze(body, kind);
        return animator;
    }

    // The knight in the saddle: its kind's glow, the grave blade in its hand.
    private static Animator Knight(Transform seat, Kind kind, AnimatorOverrideController controller)
    {
        var knight = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(KnightModel));
        PrefabUtility.UnpackPrefabInstance(knight, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        knight.name = "Model";
        knight.transform.SetParent(seat, false);
        // The seat hangs on the horse's (scaled) spine: undo its scale, and sit the hips in the saddle.
        float undo = 1f / Mathf.Max(0.001f, seat.lossyScale.x);
        knight.transform.localScale = Vector3.one * undo;
        knight.transform.localRotation = Quaternion.identity;
        knight.transform.localPosition = seat.InverseTransformVector(Vector3.down * HipHeight);
        string folder = Textures + "Knight/";
        Material plate = Material($"{kind.Name} Knight", Tex(folder + "Knight_AlbedoTransparency.png"), Color.white, 0.5f, Tex(folder + "Knight_Normal.png"),
            Tex(folder + "Knight_Emission.png"), kind.Glow * 2.5f, cutout: true);
        plate.SetFloat("_Metallic", 0.6f);
        foreach (Renderer drawn in knight.GetComponentsInChildren<Renderer>(true))
        {
            drawn.sharedMaterials = Enumerable.Repeat(plate, drawn.sharedMaterials.Length).ToArray();
        }

        Animator animator = knight.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        Transform hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        var blade = Load<GameObject>("Assets/_Project/Art/Items/Gear/Models/GraveBlade Model.prefab");
        if (hand != null && blade != null)
        {
            var held = (GameObject)PrefabUtility.InstantiatePrefab(blade);
            PrefabUtility.UnpackPrefabInstance(held, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            held.name = "Blade";
            held.transform.SetParent(hand, false);
            held.transform.localScale = Vector3.one / Mathf.Max(0.001f, hand.lossyScale.x);
        }

        return animator;
    }

    // A slow haze of its element about the horse: embers, frost mist or green fog (the Haven's hearth smoke, tinted).
    private static void Haze(GameObject body, Kind kind)
    {
        Material gas = Load<Material>($"{Art}/{kind.Name} Haze.mat");
        var smoke = Load<Material>("Assets/_Project/Art/Effects/Hearth Smoke.mat");
        if (gas == null)
        {
            gas = new Material(smoke);
            AssetDatabase.CreateAsset(gas, $"{Art}/{kind.Name} Haze.mat");
        }
        else
        {
            gas.CopyPropertiesFromMaterial(smoke);
        }

        foreach (string colour in new[] { "_BaseColor", "_Color", "_TintColor" })
        {
            if (gas.HasProperty(colour))
            {
                gas.SetColor(colour, new Color(kind.Glow.r, kind.Glow.g, kind.Glow.b, 0.4f));
            }
        }

        EditorUtility.SetDirty(gas);
        Transform spine = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Spine1") ?? body.transform;
        var holder = new GameObject($"{kind.Name} Haze");
        holder.transform.SetParent(spine, false);
        var particles = holder.AddComponent<ParticleSystem>();
        float unit = 1f / Mathf.Max(0.001f, spine.lossyScale.x);
        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f * unit, 1.4f * unit);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f * unit, 0.3f * unit);
        main.startColor = new Color(kind.Glow.r, kind.Glow.g, kind.Glow.b, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = kind.Name == "Fire" ? -0.05f : -0.01f;
        main.maxParticles = 60;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 7f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.7f * unit;
        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var drawn = holder.GetComponent<ParticleSystemRenderer>();
        drawn.sharedMaterial = gas;
        drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ----- In the raids -----

    // Rare in each raid's Creatures from tier 2, and among the Hunters that ride out after the player.
    private static string AddToRaids(List<GameObject> knights)
    {
        int added = 0;
        foreach (string raid in new[] { "MistyWoods", "TrollValley" })
        {
            var asset = Load<RaidDefinition>($"Assets/_Project/Data/Raids/{raid}.asset");
            if (asset == null)
            {
                continue;
            }

            var so = new SerializedObject(asset);
            SerializedProperty lists = so.FindProperty("spawnLists");
            for (int i = 0; i < lists.arraySize; i++)
            {
                SerializedProperty list = lists.GetArrayElementAtIndex(i);
                string name = list.FindPropertyRelative("name").stringValue;
                if (name != "Creatures" && name != "Hunters")
                {
                    continue;
                }

                SerializedProperty entries = list.FindPropertyRelative("entries");
                foreach (GameObject knight in knights)
                {
                    SerializedProperty entry = null;
                    for (int e = 0; e < entries.arraySize; e++)
                    {
                        if (entries.GetArrayElementAtIndex(e).FindPropertyRelative("prefab").objectReferenceValue == knight)
                        {
                            entry = entries.GetArrayElementAtIndex(e);
                        }
                    }

                    if (entry == null)
                    {
                        entries.InsertArrayElementAtIndex(entries.arraySize);
                        entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                        added++;
                    }

                    entry.FindPropertyRelative("prefab").objectReferenceValue = knight;
                    entry.FindPropertyRelative("weight").floatValue = name == "Hunters" ? 2f : 1f;
                    entry.FindPropertyRelative("maxPerGroup").intValue = 1;
                    entry.FindPropertyRelative("minPerGroup").intValue = 0;
                    entry.FindPropertyRelative("fromTier").intValue = 2;
                    entry.FindPropertyRelative("toTier").intValue = 0;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        return $"{added} spawn entries added";
    }

    // The journal's Creatures tab lists them (their lore and advice are on their definitions).
    private static string InJournal()
    {
        var settings = Load<JournalSettings>("Assets/_Project/Resources/JournalSettings.asset");
        var so = new SerializedObject(settings);
        SerializedProperty creatures = so.FindProperty("creatures");
        int added = 0;
        foreach (Kind kind in Kinds)
        {
            var definition = Load<MonsterDefinition>($"{Monsters}UndeadKnight{kind.Name}.asset");
            bool listed = false;
            for (int i = 0; i < creatures.arraySize; i++)
            {
                listed |= creatures.GetArrayElementAtIndex(i).objectReferenceValue == definition;
            }

            if (!listed && definition != null)
            {
                creatures.InsertArrayElementAtIndex(creatures.arraySize);
                creatures.GetArrayElementAtIndex(creatures.arraySize - 1).objectReferenceValue = definition;
                added++;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return $"{added} added to the journal";
    }

    // ----- Data helpers (as BuildCreatures') -----

    private static AttackDefinition Attack(string file, string template, string label, Action<SerializedObject> change)
    {
        string path = AttackFolder + file + ".asset";
        if (Load<AttackDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(AttackFolder + template + ".asset", path);
        }

        var attack = Load<AttackDefinition>(path);
        Edit(attack, so =>
        {
            so.FindProperty("displayName").stringValue = label;
            change(so);
        });
        return attack;
    }

    private static CombatStatsDefinition Stats(string file, string template, float health, float armour, (StatType stat, float value)[] extra)
    {
        string path = StatsFolder + file + ".asset";
        if (Load<CombatStatsDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(StatsFolder + template + ".asset", path);
        }

        var stats = Load<CombatStatsDefinition>(path);
        Edit(stats, so =>
        {
            SerializedProperty list = so.FindProperty("stats");
            SetStat(list, StatType.MaxHealth, health);
            SetStat(list, StatType.Armor, armour);
            foreach ((StatType stat, float value) in extra)
            {
                SetStat(list, stat, value);
            }
        });
        return stats;
    }

    private static void SetStat(SerializedProperty list, StatType stat, float value)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue == (int)stat)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = value;
                return;
            }
        }

        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("stat").intValue = (int)stat;
        entry.FindPropertyRelative("value").floatValue = value;
    }

    private static MonsterDefinition Monster(string file, string label, string text, CombatStatsDefinition stats, AttackDefinition attack, Kind kind)
    {
        string path = Monsters + file + ".asset";
        var monster = Load<MonsterDefinition>(path);
        if (monster == null)
        {
            AssetDatabase.CopyAsset(Monsters + "Draug.asset", path);
            monster = Load<MonsterDefinition>(path);
        }

        Edit(monster, so =>
        {
            so.FindProperty("id").stringValue = file;
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("combatStats").objectReferenceValue = stats;
            so.FindProperty("faction").intValue = (int)Faction.Enemy;
            so.FindProperty("attack").objectReferenceValue = attack;
            // A rider: fast, far-sighted, slow to give up the chase; it doesn't stagger easily.
            so.FindProperty("moveSpeed").floatValue = 6.5f;
            so.FindProperty("detectionRange").floatValue = 18f;
            so.FindProperty("chaseRange").floatValue = 34f;
            so.FindProperty("leashRange").floatValue = 140f;
            so.FindProperty("sightAngle").floatValue = 150f;
            so.FindProperty("closeSenseRange").floatValue = 4f;
            so.FindProperty("hearing").floatValue = 1.2f;
            so.FindProperty("alertness").floatValue = 1.1f;
            so.FindProperty("poise").floatValue = 220f;
            so.FindProperty("movementTactic").intValue = (int)MonsterMovementTactic.Chase;
            so.FindProperty("temperament").intValue = (int)MonsterTemperament.Aggressive;
            so.FindProperty("hoverHeight").floatValue = 0f;
            so.FindProperty("prefab").objectReferenceValue = null;
            // Each body keeps its own Animator: the knight its override, the horse the Horse controller. (The prefab builder
            // would give the definition's controller to the first it finds: the horse's.)
            so.FindProperty("animatorController").objectReferenceValue = null;
            so.FindProperty("frightensHorses").floatValue = 14f;
            so.FindProperty("journalLore").stringValue = kind.Lore;
            so.FindProperty("journalAdvice").stringValue = kind.Advice;
            // Its own portrait once painted (BuildJournal.PaintedPortraits); until then none, not the Draug's.
            if (so.FindProperty("portrait").objectReferenceValue != null && so.FindProperty("portrait").objectReferenceValue.name.Contains("Draug"))
            {
                so.FindProperty("portrait").objectReferenceValue = null;
            }
            so.FindProperty("huntsHorses").floatValue = 1f;
        });
        return monster;
    }

    private static void SetAttacks(SerializedObject so, params (AttackDefinition attack, MonsterAttackKind kind, float damage, float min, float max, int priority, float cooldown, float speed, float distance, float exposed)[] actions)
    {
        SerializedProperty list = so.FindProperty("attacks");
        list.ClearArray();
        foreach (var action in actions)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("attack").objectReferenceValue = action.attack;
            entry.FindPropertyRelative("kind").intValue = (int)action.kind;
            entry.FindPropertyRelative("damage").floatValue = action.damage;
            entry.FindPropertyRelative("minimumRange").floatValue = action.min;
            entry.FindPropertyRelative("maximumRange").floatValue = action.max;
            entry.FindPropertyRelative("priority").intValue = action.priority;
            entry.FindPropertyRelative("cooldown").floatValue = action.cooldown;
            entry.FindPropertyRelative("interruptible").boolValue = false;
            entry.FindPropertyRelative("burstCount").intValue = 1;
            entry.FindPropertyRelative("burstInterval").floatValue = 0.2f;
            entry.FindPropertyRelative("chargeSpeed").floatValue = action.speed > 0f ? action.speed : 12f;
            entry.FindPropertyRelative("chargeDistance").floatValue = action.distance > 0f ? action.distance : 8f;
            entry.FindPropertyRelative("exposedSeconds").floatValue = action.exposed > 0f ? action.exposed : 2f;
            entry.FindPropertyRelative("packLeaderOnly").boolValue = false;
        }
    }

    // "!" marks a resource (essences), the rest are items.
    private static void SetDrops(SerializedObject so, params (string what, int min, int max, float chance)[] drops)
    {
        SerializedProperty entries = so.FindProperty("drops").FindPropertyRelative("entries");
        entries.arraySize = drops.Length;
        for (int i = 0; i < drops.Length; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            bool resource = drops[i].what.StartsWith("!");
            entry.FindPropertyRelative("item").objectReferenceValue = resource ? null : FindItem(drops[i].what);
            entry.FindPropertyRelative("resource").objectReferenceValue = resource
                ? Load<ResourceDefinition>("Assets/_Project/Data/ResourceInventory/Definitions/" + drops[i].what.Substring(1) + ".asset")
                : null;
            entry.FindPropertyRelative("minQuantity").intValue = drops[i].min;
            entry.FindPropertyRelative("maxQuantity").intValue = drops[i].max;
            entry.FindPropertyRelative("probability").floatValue = drops[i].chance;
        }
    }

    private static ItemDefinition FindItem(string file)
    {
        foreach (string folder in new[] { "Materials/", "", "Food/" })
        {
            var found = Load<ItemDefinition>("Assets/_Project/Data/Items/" + folder + file + ".asset");
            if (found != null)
            {
                return found;
            }
        }

        throw new Exception("No item " + file);
    }

    // ----- Materials and shapes -----

    private static Material Material(string name, Texture2D albedo, Color colour, float smoothness, Texture2D normal, Texture2D emissionMap, Color emission, bool cutout = false)
    {
        string path = $"{Art}/{name}.mat";
        Material material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(Lit));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", albedo);
        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        material.SetTexture("_BumpMap", normal);
        if (normal != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            material.DisableKeyword("_NORMALMAP");
        }

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

        bool glows = emission.maxColorComponent > 0.001f;
        material.SetTexture("_EmissionMap", emissionMap);
        material.SetColor("_EmissionColor", glows ? emission : Color.black);
        material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        if (glows)
        {
            material.EnableKeyword("_EMISSION");
        }
        else
        {
            material.DisableKeyword("_EMISSION");
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Where the rider sits: the saddle's lowest point between pommel and cantle, on the spine (as BuildHorses).
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

    private static void Texture(string path, int size, bool normal)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null || importer.maxTextureSize == size && (importer.textureType == TextureImporterType.NormalMap) == normal)
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

    private static Texture2D Tex(string path) => Load<Texture2D>(path);

    private static void Edit(Object asset, Action<SerializedObject> change)
    {
        var so = new SerializedObject(asset);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    private static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
