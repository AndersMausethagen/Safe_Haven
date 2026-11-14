#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// New creatures (`unity command run_script --file AgentScripts/BuildCreatures.cs --entry BuildCreatures.Apply`), each with
/// its own way to be read and beaten (GDD §10.5). Run BuildGear and BuildFood first.
/// <list type="bullet">
/// <item>Marsh Light: a hovering wisp that keeps its distance, slows you with cold bolts and flees when hurt.</item>
/// <item>Draug: slow, armoured, hard to stagger, chills on a hit and lunges from afar. Fire hurts it more.</item>
/// <item>Bog Spitter: spits poison from range; weak up close. Bring an antidote.</item>
/// <item>Blight Boar: leaves you alone unless you come close, then charges and stands open afterwards.</item>
/// <item>Rootling: small and fast, in swarms; a grasp can hold your feet.</item>
/// <item>Moss Shaman: hangs back, calls up Rootlings and rallies its side. Kill it first.</item>
/// </list>
/// Bodies are placeholders built from primitives (the Draug and the Shaman from existing bodies). Also adds them and the
/// new wild plants to the Misty Woods' spawn lists and new finds to the supply cache. Safe to run again.
/// </summary>
public static class BuildCreatures
{
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string Attacks = "Assets/_Project/Data/Combat/Attacks/";
    private const string StatsFolder = "Assets/_Project/Data/Combat/Stats/";
    private const string Status = "Assets/_Project/Data/Combat/Status/";
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string Visuals = "Assets/_Project/Art/Enemies/";
    private const string Raid = "Assets/_Project/Data/Raids/MistyWoods.asset";

    public static string Apply()
    {
        var chill = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(Status + "Chill.asset");
        var poison = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(Status + "Poison.asset");
        var roots = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(Status + "Roots.asset");

        GameObject wispBolt = Projectile("WispBolt", Emissive("Wisp Glow", new Color(0.6f, 0.9f, 1f), new Color(1.2f, 2.2f, 3f)), true);
        GameObject bogSpit = Projectile("BogSpit", Emissive("Bog Slime", new Color(0.45f, 0.7f, 0.2f), new Color(0.2f, 0.45f, 0.05f)), false);
        GameObject stone = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "TrollStone.prefab");

        MarshLight(wispBolt, chill);
        Draug(chill);
        BogSpitter(bogSpit, poison);
        BlightBoar();
        MonsterDefinition rootling = Rootling(roots);
        GameObject swarm = RootlingSwarm(rootling);
        MossShaman(rootling, stone);
        AssetDatabase.SaveAssets();

        string spawns = Spawns(swarm);
        string cache = SupplyCache();
        AssetDatabase.SaveAssets();
        Refresh();
        return $"6 creatures; {spawns}; {cache}";
    }

    // ----- The creatures -----

    private static void MarshLight(GameObject bolt, StatusEffectDefinition chill)
    {
        CombatStatsDefinition stats = Stats("MarshLightStats", "MistwingStats", 40f, 0f);
        AttackDefinition attack = Attack("WispBolt", "TrollStoneBurst", "Wisp Bolt", so =>
        {
            so.FindProperty("damageType").intValue = (int)DamageType.Frost;
            so.FindProperty("hitDelay").floatValue = 0.7f;
            so.FindProperty("recoveryTime").floatValue = 1.3f;
            so.FindProperty("appliesStatus").objectReferenceValue = chill;
            so.FindProperty("statusChance").floatValue = 1f;
        });
        Material glow = Emissive("Wisp Glow", new Color(0.6f, 0.9f, 1f), new Color(1.2f, 2.2f, 3f));
        GameObject visual = Visual("MarshLightVisual", root =>
        {
            Piece(root, PrimitiveType.Sphere, Vector3.up * 0.2f, Vector3.one * 0.38f, glow);
            Piece(root, PrimitiveType.Sphere, Vector3.up * 0.2f, Vector3.one * 0.62f, Transparent("Wisp Halo", new Color(0.55f, 0.85f, 1f, 0.22f)));
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = Vector3.up * 0.2f;
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.85f, 1f);
            light.intensity = 2.5f;
            light.range = 7f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<LightFlicker>();
            light.gameObject.AddComponent<AtmosphereLight>();
        });
        MonsterDefinition definition = Monster("MarshLight", "Marsh Light", "A pale light drifting over wet ground. Follow it and it leads you off the path; get close and it runs. Its cold bolts slow you for what comes next.",
            stats, attack, 4.2f, 15f, 24f, 40f, 300f, 2f, 0.6f, 1.1f, 15f, MonsterMovementTactic.KeepDistance, MonsterTemperament.Aggressive | MonsterTemperament.Cowardly, 1.1f,
            so =>
            {
                so.FindProperty("preferredDistance").floatValue = 9f;
                so.FindProperty("fleeHealthFraction").floatValue = 0.35f;
                SetAttacks(so, new ActionSpec { Attack = attack, Kind = MonsterAttackKind.Projectile, Damage = 9f, Min = 3f, Max = 13f, Priority = 10, Cooldown = 2.2f, Prefab = bolt, Speed = 9f, Radius = 0.2f });
                SetDrops(so, ("WispEmber", 1, 1, 0.6f), ("!CursedEssence", 1, 2, 0.8f));
            });
        Prefab(definition, visual);
    }

    private static void Draug(StatusEffectDefinition chill)
    {
        CombatStatsDefinition stats = Stats("DraugStats", "RootGuardianStats", 220f, 25f, (StatType.FireResistance, -0.25f), (StatType.FrostResistance, 0.3f));
        AttackDefinition cleave = Attack("DraugCleave", "GuardianSlam", "Draug Cleave", so =>
        {
            so.FindProperty("range").floatValue = 2.3f;
            so.FindProperty("hitDelay").floatValue = 0.75f;
            so.FindProperty("recoveryTime").floatValue = 1.5f;
            so.FindProperty("appliesStatus").objectReferenceValue = chill;
            so.FindProperty("statusChance").floatValue = 0.4f;
        });
        AttackDefinition lunge = Attack("DraugLunge", "MistwingCharge", "Draug Lunge", so =>
        {
            so.FindProperty("range").floatValue = 1.6f;
            so.FindProperty("arc").floatValue = 40f;
            so.FindProperty("hitDelay").floatValue = 1f;
            so.FindProperty("recoveryTime").floatValue = 1.2f;
        });
        MonsterDefinition definition = Monster("Draug", "Draug", "The dead of the barrows, still in their mail. Slow, patient and hard to stop; its touch is grave-cold. Old stories say fire ends them.",
            stats, cleave, 2.8f, 9f, 16f, 22f, 100f, 3f, 1.1f, 0.8f, 140f, MonsterMovementTactic.Chase, MonsterTemperament.Aggressive, 0f,
            so =>
            {
                SetAttacks(so,
                    new ActionSpec { Attack = cleave, Kind = MonsterAttackKind.Melee, Damage = 24f, Min = 0f, Max = 2.2f, Priority = 10, Cooldown = 1f },
                    new ActionSpec { Attack = lunge, Kind = MonsterAttackKind.Charge, Damage = 30f, Min = 3f, Max = 8f, Priority = 20, Cooldown = 9f, Speed = 8f, Distance = 6f, Exposed = 2f });
                SetDrops(so, ("GraveIron", 1, 2, 0.7f), ("OldSilverPenny", 1, 3, 0.5f), ("!CursedEssence", 2, 3, 1f));
            });

        // A humanoid body: the Root Guardian's, in grave colours, with a blade in its hand.
        string path = Enemies + "Draug.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            AssetDatabase.CopyAsset(Enemies + "RootGuardian.prefab", path);
        }

        Material grave = Solid("Draug Body", new Color(0.24f, 0.28f, 0.33f));
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = "Draug";
            Transform model = root.transform.Find("Model");
            model.localScale = Vector3.one * 1.1f;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(grave, renderer.sharedMaterials.Length).ToArray();
            }

            Animator animator = model.GetComponentInChildren<Animator>();
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var blade = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Items/Gear/Models/GraveBlade Model.prefab");
            if (hand != null && blade != null && hand.Find("Blade") == null)
            {
                var held = (GameObject)PrefabUtility.InstantiatePrefab(blade);
                PrefabUtility.UnpackPrefabInstance(held, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                held.name = "Blade";
                held.transform.SetParent(hand, false);
            }

            Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());
            Transform bar = root.transform.Find("HealthBar");
            if (bar != null)
            {
                Object.DestroyImmediate(bar.gameObject);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Edit(definition, so => so.FindProperty("prefab").objectReferenceValue = prefab);
        MonsterPrefabBuilder.Update(prefab, definition);
        prefab.GetComponent<SaveableEntity>().RefreshPrefabId();
    }

    private static void BogSpitter(GameObject spit, StatusEffectDefinition poison)
    {
        CombatStatsDefinition stats = Stats("BogSpitterStats", "SmallMossTrollStats", 70f, 3f);
        AttackDefinition glob = Attack("VenomSpit", "TrollStoneBurst", "Venom Spit", so =>
        {
            so.FindProperty("hitDelay").floatValue = 0.8f;
            so.FindProperty("recoveryTime").floatValue = 1.4f;
            so.FindProperty("appliesStatus").objectReferenceValue = poison;
            so.FindProperty("statusChance").floatValue = 1f;
        });
        AttackDefinition slap = Attack("TongueLash", "TrollShove", "Tongue Lash", so => { });
        Material skin = Solid("Bog Spitter Skin", new Color(0.26f, 0.32f, 0.16f));
        Material sac = Emissive("Bog Slime", new Color(0.45f, 0.7f, 0.2f), new Color(0.2f, 0.45f, 0.05f));
        GameObject visual = Visual("BogSpitterVisual", root =>
        {
            Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), new Vector3(1f, 0.72f, 1.2f), skin);
            Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.72f, 0.42f), new Vector3(0.6f, 0.42f, 0.5f), skin);
            Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0.72f), new Vector3(0.42f, 0.34f, 0.3f), sac);
            Piece(root, PrimitiveType.Sphere, new Vector3(0.22f, 0.92f, 0.56f), Vector3.one * 0.14f, sac);
            Piece(root, PrimitiveType.Sphere, new Vector3(-0.22f, 0.92f, 0.56f), Vector3.one * 0.14f, sac);
            Piece(root, PrimitiveType.Sphere, new Vector3(0.45f, 0.14f, 0.3f), new Vector3(0.34f, 0.2f, 0.5f), skin);
            Piece(root, PrimitiveType.Sphere, new Vector3(-0.45f, 0.14f, 0.3f), new Vector3(0.34f, 0.2f, 0.5f), skin);
        });
        MonsterDefinition definition = Monster("BogSpitter", "Bog Spitter", "A bloated thing the size of a calf, squatting where the ground is wet. It spits venom that keeps working long after it lands. Close the distance, or carry an antidote.",
            stats, glob, 3f, 13f, 18f, 22f, 150f, 2.5f, 1f, 0.9f, 35f, MonsterMovementTactic.KeepDistance, MonsterTemperament.Aggressive, 0f,
            so =>
            {
                so.FindProperty("preferredDistance").floatValue = 8f;
                SetAttacks(so,
                    new ActionSpec { Attack = slap, Kind = MonsterAttackKind.Melee, Damage = 6f, Min = 0f, Max = 2.2f, Priority = 20, Cooldown = 1.6f },
                    new ActionSpec { Attack = glob, Kind = MonsterAttackKind.Projectile, Damage = 6f, Min = 2.5f, Max = 12f, Priority = 10, Cooldown = 2.8f, Prefab = spit, Speed = 11f, Radius = 0.22f });
                SetDrops(so, ("BogVenom", 1, 1, 0.7f), ("!CursedEssence", 1, 2, 0.9f));
            });
        Prefab(definition, visual);
    }

    private static void BlightBoar()
    {
        CombatStatsDefinition stats = Stats("BlightBoarStats", "ForestBittenWolfStats", 120f, 8f);
        AttackDefinition charge = Attack("BoarCharge", "MistwingCharge", "Boar Charge", so =>
        {
            so.FindProperty("range").floatValue = 1.6f;
            so.FindProperty("arc").floatValue = 50f;
            so.FindProperty("poiseDamage").floatValue = 25f;
        });
        AttackDefinition gore = Attack("BoarGore", "MonsterStrike", "Boar Gore", so => so.FindProperty("range").floatValue = 1.9f);
        Material hide = Solid("Boar Hide", new Color(0.2f, 0.16f, 0.13f));
        Material tusk = Solid("Boar Tusk", new Color(0.9f, 0.85f, 0.68f));
        GameObject visual = Visual("BlightBoarVisual", root =>
        {
            Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.68f, -0.1f), new Vector3(0.85f, 0.85f, 1.5f), hide);
            Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.72f, 0.75f), new Vector3(0.6f, 0.55f, 0.7f), hide);
            Piece(root, PrimitiveType.Cube, new Vector3(0f, 1.08f, -0.15f), new Vector3(0.1f, 0.2f, 1.1f), hide);
            foreach (float side in new[] { -1f, 1f })
            {
                Piece(root, PrimitiveType.Cylinder, new Vector3(side * 0.16f, 0.62f, 1.1f), new Vector3(0.06f, 0.18f, 0.06f), tusk).localRotation = Quaternion.Euler(60f, 0f, side * -15f);
                Piece(root, PrimitiveType.Cube, new Vector3(side * 0.28f, 0.2f, 0.4f), new Vector3(0.16f, 0.4f, 0.16f), hide);
                Piece(root, PrimitiveType.Cube, new Vector3(side * 0.28f, 0.2f, -0.55f), new Vector3(0.16f, 0.4f, 0.16f), hide);
            }
        });
        MonsterDefinition definition = Monster("BlightBoar", "Blight Boar", "A boar gone sour in the blighted thickets. It will let you pass at a distance. Step too close and it comes straight at you, and stands blown and open when the charge is spent.",
            stats, charge, 4.6f, 9f, 18f, 24f, 140f, 3f, 1f, 1f, 70f, MonsterMovementTactic.Chase, MonsterTemperament.Aggressive | MonsterTemperament.Defensive, 0f,
            so =>
            {
                so.FindProperty("defensiveRadius").floatValue = 11f;
                SetAttacks(so,
                    new ActionSpec { Attack = charge, Kind = MonsterAttackKind.Charge, Damage = 20f, Min = 3f, Max = 9f, Priority = 20, Cooldown = 4f, Speed = 13f, Distance = 9f, Exposed = 2.5f },
                    new ActionSpec { Attack = gore, Kind = MonsterAttackKind.Melee, Damage = 12f, Min = 0f, Max = 1.8f, Priority = 10, Cooldown = 1f });
                SetDrops(so, ("BoarMeat", 2, 3, 1f), ("RoughHide", 1, 2, 0.8f), ("BoarTusk", 1, 1, 0.4f), ("!CursedEssence", 1, 1, 0.5f));
            });
        Prefab(definition, visual);
    }

    private static MonsterDefinition Rootling(StatusEffectDefinition roots)
    {
        CombatStatsDefinition stats = Stats("RootlingStats", "MistwingStats", 28f, 2f);
        AttackDefinition grasp = Attack("RootGrasp", "MonsterStrike", "Root Grasp", so =>
        {
            so.FindProperty("range").floatValue = 1.6f;
            so.FindProperty("hitDelay").floatValue = 0.5f;
            so.FindProperty("recoveryTime").floatValue = 0.9f;
            so.FindProperty("appliesStatus").objectReferenceValue = roots;
            so.FindProperty("statusChance").floatValue = 0.3f;
        });
        Material bark = Solid("Rootling Bark", new Color(0.3f, 0.21f, 0.13f));
        Material moss = Solid("Rootling Moss", new Color(0.3f, 0.42f, 0.2f));
        GameObject visual = Visual("RootlingVisual", root =>
        {
            Piece(root, PrimitiveType.Capsule, new Vector3(0f, 0.42f, 0f), new Vector3(0.36f, 0.42f, 0.36f), bark);
            Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.86f, 0f), new Vector3(0.42f, 0.3f, 0.42f), moss);
            foreach (float side in new[] { -1f, 1f })
            {
                Piece(root, PrimitiveType.Capsule, new Vector3(side * 0.3f, 0.5f, 0.08f), new Vector3(0.09f, 0.28f, 0.09f), bark).localRotation = Quaternion.Euler(30f, 0f, side * -35f);
            }
        });
        MonsterDefinition definition = Monster("Rootling", "Rootling", "A knee-high scrap of root and moss that has learned to walk. One is nothing. They are never one.",
            stats, grasp, 4.4f, 9f, 16f, 18f, 160f, 2.5f, 1f, 1f, 10f, MonsterMovementTactic.Chase, MonsterTemperament.Aggressive, 0f,
            so =>
            {
                SetAttacks(so, new ActionSpec { Attack = grasp, Kind = MonsterAttackKind.Melee, Damage = 5f, Min = 0f, Max = 1.5f, Priority = 10, Cooldown = 1.8f });
                SetDrops(so, ("RootlingBark", 1, 2, 0.8f), ("!CursedEssence", 1, 1, 0.3f));
            });
        Prefab(definition, visual);
        return definition;
    }

    // Three Rootlings that alert together and take turns, for the spawn lists.
    private static GameObject RootlingSwarm(MonsterDefinition rootling)
    {
        string path = Enemies + "RootlingSwarm.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject("RootlingSwarm");
        try
        {
            var pack = root.AddComponent<MonsterPack>();
            MonsterAI leader = null;
            Vector3[] places = { new Vector3(0f, 0f, 0.8f), new Vector3(0.9f, 0f, -0.5f), new Vector3(-0.9f, 0f, -0.5f) };
            foreach (Vector3 place in places)
            {
                var member = (GameObject)PrefabUtility.InstantiatePrefab(rootling.Prefab, root.transform);
                member.transform.localPosition = place;
                leader = leader != null ? leader : member.GetComponent<MonsterAI>();
            }

            var so = new SerializedObject(pack);
            so.FindProperty("leader").objectReferenceValue = leader;
            so.FindProperty("attackSpacing").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void MossShaman(MonsterDefinition rootling, GameObject stone)
    {
        CombatStatsDefinition stats = Stats("MossShamanStats", "SmallMossTrollStats", 110f, 6f);
        AttackDefinition call = Attack("CallRootlings", "WolfHowl", "Call Rootlings", so =>
        {
            so.FindProperty("hitDelay").floatValue = 1.2f;
            so.FindProperty("recoveryTime").floatValue = 1.7f;
        });
        AttackDefinition rally = Attack("ShamanRally", "WolfHowl", "Shaman's Rally", so => { });
        AttackDefinition hurl = Attack("ShamanStone", "TrollStoneBurst", "Hurled Stone", so => { });
        AttackDefinition shove = Attack("ShamanShove", "TrollShove", "Shaman's Shove", so => { });
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + "SmallMossTrollVisual.prefab");
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        Material orb = Emissive("Shaman Orb", new Color(0.5f, 0.85f, 0.4f), new Color(0.6f, 1.8f, 0.5f));
        GameObject visual = Visual("MossShamanVisual", root =>
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(source, root);
            PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            body.transform.localScale = Vector3.one * 1.15f;
            Piece(root, PrimitiveType.Cylinder, new Vector3(0.55f, 1f, 0.15f), new Vector3(0.07f, 1f, 0.07f), wood);
            Piece(root, PrimitiveType.Sphere, new Vector3(0.55f, 2.05f, 0.15f), Vector3.one * 0.26f, orb);
        });
        MonsterDefinition definition = Monster("MossShaman", "Moss Shaman", "An old troll with a staff of living wood. It doesn't want to fight you: it wants the ground to. Rootlings rise wherever it points. Kill it first, if you can reach it.",
            stats, hurl, 3f, 13f, 20f, 28f, 130f, 3f, 1.1f, 1f, 60f, MonsterMovementTactic.KeepDistance, MonsterTemperament.Aggressive | MonsterTemperament.Cowardly, 0f,
            so =>
            {
                so.FindProperty("preferredDistance").floatValue = 10f;
                so.FindProperty("fleeHealthFraction").floatValue = 0.3f;
                SetAttacks(so,
                    new ActionSpec { Attack = shove, Kind = MonsterAttackKind.Melee, Damage = 6f, Min = 0f, Max = 2f, Priority = 30, Cooldown = 2f },
                    new ActionSpec { Attack = call, Kind = MonsterAttackKind.Summon, Min = 0f, Max = 14f, Priority = 25, Cooldown = 14f, Interruptible = true, Summon = rootling.Prefab, SummonCount = 2, SummonRadius = 3f, SummonLimit = 4 },
                    new ActionSpec { Attack = rally, Kind = MonsterAttackKind.Howl, Min = 0f, Max = 12f, Priority = 20, Cooldown = 18f, Interruptible = true, BuffRadius = 12f, BuffSeconds = 8f, BuffBonus = 0.1f },
                    new ActionSpec { Attack = hurl, Kind = MonsterAttackKind.Projectile, Damage = 7f, Min = 2.2f, Max = 14f, Priority = 10, Cooldown = 2.4f, Prefab = stone, Speed = 12f, Radius = 0.15f });
                SetDrops(so, ("RuneBook", 1, 1, 0.15f), ("OldSilverPenny", 1, 2, 0.5f), ("!CursedEssence", 3, 4, 1f));
            });
        Prefab(definition, visual);
    }

    // ----- Spawns -----

    private static string Spawns(GameObject swarm)
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(Raid);
        var so = new SerializedObject(raid);
        SerializedProperty lists = so.FindProperty("spawnLists");
        SerializedProperty creatures = FindList(lists, "Creatures");
        SerializedProperty resources = FindList(lists, "Resources");
        GameObject Enemy(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + name + ".prefab");
        GameObject Node(string file)
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/" + file + ".asset");
            return node != null ? node.WorldPrefab : throw new Exception("No node " + file);
        }

        // Shares of the creature list: the old four keep their order of size, and the new ones spread the rest.
        SetEntries(creatures,
            (Enemy("WolfPack"), 22f, 0, 0), (Enemy("RootGuardian"), 18f, 0, 0), (Enemy("SmallMossTroll"), 12f, 0, 0), (Enemy("Mistwing"), 8f, 0, 0),
            (swarm, 10f, 0, 0), (Enemy("BlightBoar"), 10f, 0, 0), (Enemy("BogSpitter"), 8f, 0, 0), (Enemy("MarshLight"), 6f, 0, 0), (Enemy("Draug"), 4f, 2, 0), (Enemy("MossShaman"), 2f, 1, 0));
        // Wild food: always at least one of each, so seeds for the farm can be found on every run.
        var kept = new List<(GameObject, float, int, int)>();
        SerializedProperty existing = resources.FindPropertyRelative("entries");
        var planted = new HashSet<GameObject> { Node("Chanterelles"), Node("WildTurnips"), Node("WildBarley"), Node("CloudberryPatch") };
        for (int i = 0; i < existing.arraySize; i++)
        {
            SerializedProperty entry = existing.GetArrayElementAtIndex(i);
            var prefab = (GameObject)entry.FindPropertyRelative("prefab").objectReferenceValue;
            if (prefab != null && !planted.Contains(prefab))
            {
                kept.Add((prefab, entry.FindPropertyRelative("weight").floatValue, entry.FindPropertyRelative("maxPerGroup").intValue, entry.FindPropertyRelative("minPerGroup").intValue));
            }
        }

        float keptTotal = kept.Sum(k => k.Item2);
        var all = kept.Select(k => (k.Item1, k.Item2 / keptTotal * 84f, k.Item3, k.Item4)).ToList();
        all.Add((Node("Chanterelles"), 7f, 0, 1));
        all.Add((Node("WildTurnips"), 4f, 0, 1));
        all.Add((Node("WildBarley"), 4f, 0, 1));
        all.Add((Node("CloudberryPatch"), 1f, 3, 1));
        SetEntries(resources, all.ToArray());

        SerializedProperty known = so.FindProperty("knownEnemies");
        foreach (string file in new[] { "BlightBoar", "BogSpitter", "Rootling" })
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Monsters + file + ".asset");
            if (!Enumerable.Range(0, known.arraySize).Any(i => known.GetArrayElementAtIndex(i).objectReferenceValue == monster))
            {
                known.InsertArrayElementAtIndex(known.arraySize);
                known.GetArrayElementAtIndex(known.arraySize - 1).objectReferenceValue = monster;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(raid);

        var roamers = AssetDatabase.LoadAssetAtPath<SpawnTable>("Assets/_Project/Data/Raids/SpawnTables/MistyWoods_Roamers.asset");
        var entries = roamers.Entries.Where(e => e.prefab != null && !new[] { "BlightBoar", "BogSpitter", "MarshLight", "RootlingSwarm" }.Contains(e.prefab.name)).ToList();
        entries.Add(new SpawnTable.Entry { prefab = Enemy("BogSpitter"), weight = 1f });
        entries.Add(new SpawnTable.Entry { prefab = Enemy("MarshLight"), weight = 1f });
        entries.Add(new SpawnTable.Entry { prefab = swarm, weight = 1f });
        roamers.SetEntries(entries);
        EditorUtility.SetDirty(roamers);
        return $"{creatures.FindPropertyRelative("entries").arraySize} creature entries, {resources.FindPropertyRelative("entries").arraySize} resource entries";
    }

    private static SerializedProperty FindList(SerializedProperty lists, string name)
    {
        for (int i = 0; i < lists.arraySize; i++)
        {
            SerializedProperty list = lists.GetArrayElementAtIndex(i);
            if (list.FindPropertyRelative("name").stringValue == name)
            {
                return list;
            }
        }

        throw new Exception("No spawn list " + name);
    }

    private static void SetEntries(SerializedProperty list, params (GameObject prefab, float weight, int max, int min)[] rows)
    {
        SerializedProperty entries = list.FindPropertyRelative("entries");
        entries.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].prefab == null)
            {
                throw new Exception("A spawn entry has no prefab: row " + i);
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("prefab").objectReferenceValue = rows[i].prefab;
            entry.FindPropertyRelative("weight").floatValue = rows[i].weight;
            entry.FindPropertyRelative("maxPerGroup").intValue = rows[i].max;
            entry.FindPropertyRelative("minPerGroup").intValue = rows[i].min;
        }
    }

    // New finds in the supply cache: some of the new gear, seeds for the farm, and what a raid can go wrong with.
    private static string SupplyCache()
    {
        const string path = "Assets/_Project/Prefabs/Raids/SupplyCache.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var cache = root.GetComponent<LootCache>();
            var so = new SerializedObject(cache);
            SerializedProperty entries = so.FindProperty("drops").FindPropertyRelative("entries");
            int added = 0;
            foreach ((string file, int min, int max, float chance) in new[]
                     {
                         ("Materials/Barley", 1, 2, 0.3f), ("Materials/WildTurnip", 1, 2, 0.3f), ("Materials/Honey", 1, 1, 0.15f), ("Food/Antidote", 1, 1, 0.15f),
                         ("Jewelry/BoneCharm", 1, 1, 0.08f), ("Jewelry/CopperBand", 1, 1, 0.06f), ("Armor/CuredLeatherCap", 1, 1, 0.05f), ("Weapons/RoundShield", 1, 1, 0.05f),
                     })
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + file + ".asset");
                if (item == null)
                {
                    throw new Exception("No item " + file);
                }

                if (Enumerable.Range(0, entries.arraySize).Any(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item))
                {
                    continue;
                }

                entries.InsertArrayElementAtIndex(entries.arraySize);
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("resource").objectReferenceValue = null;
                entry.FindPropertyRelative("minQuantity").intValue = min;
                entry.FindPropertyRelative("maxQuantity").intValue = max;
                entry.FindPropertyRelative("probability").floatValue = chance;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"{added} new cache finds";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ----- Builders -----

    private sealed class ActionSpec
    {
        public AttackDefinition Attack;
        public MonsterAttackKind Kind;
        public float Damage, Min, Max = 2f, Cooldown = 1f;
        public int Priority;
        public bool Interruptible;
        public GameObject Prefab, Summon;
        public float Speed = 12f, Radius = 0.15f, Distance = 8f, Exposed = 2f;
        public float BuffRadius = 10f, BuffSeconds = 8f, BuffBonus = 0.05f, SummonRadius = 3f;
        public int SummonCount = 2, SummonLimit = 4;
    }

    private static void SetAttacks(SerializedObject so, params ActionSpec[] actions)
    {
        SerializedProperty list = so.FindProperty("attacks");
        list.ClearArray();
        foreach (ActionSpec action in actions)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("attack").objectReferenceValue = action.Attack;
            entry.FindPropertyRelative("kind").intValue = (int)action.Kind;
            entry.FindPropertyRelative("damage").floatValue = action.Damage;
            entry.FindPropertyRelative("minimumRange").floatValue = action.Min;
            entry.FindPropertyRelative("maximumRange").floatValue = action.Max;
            entry.FindPropertyRelative("priority").intValue = action.Priority;
            entry.FindPropertyRelative("cooldown").floatValue = action.Cooldown;
            entry.FindPropertyRelative("interruptible").boolValue = action.Interruptible;
            entry.FindPropertyRelative("projectilePrefab").objectReferenceValue = action.Prefab;
            entry.FindPropertyRelative("burstCount").intValue = 1;
            entry.FindPropertyRelative("burstInterval").floatValue = 0.2f;
            entry.FindPropertyRelative("projectileSpeed").floatValue = action.Speed;
            entry.FindPropertyRelative("projectileRadius").floatValue = action.Radius;
            entry.FindPropertyRelative("chargeSpeed").floatValue = action.Speed;
            entry.FindPropertyRelative("chargeDistance").floatValue = action.Distance;
            entry.FindPropertyRelative("exposedSeconds").floatValue = action.Exposed;
            entry.FindPropertyRelative("buffRadius").floatValue = action.BuffRadius;
            entry.FindPropertyRelative("buffSeconds").floatValue = action.BuffSeconds;
            entry.FindPropertyRelative("damageBonus").floatValue = action.BuffBonus;
            entry.FindPropertyRelative("packLeaderOnly").boolValue = false;
            entry.FindPropertyRelative("summonPrefab").objectReferenceValue = action.Summon;
            entry.FindPropertyRelative("summonCount").intValue = action.SummonCount;
            entry.FindPropertyRelative("summonRadius").floatValue = action.SummonRadius;
            entry.FindPropertyRelative("summonLimit").intValue = action.SummonLimit;
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
                ? AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/_Project/Data/ResourceInventory/Definitions/" + drops[i].what.Substring(1) + ".asset")
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
            var found = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + folder + file + ".asset");
            if (found != null)
            {
                return found;
            }
        }

        throw new Exception("No item " + file);
    }

    private static MonsterDefinition Monster(string file, string label, string text, CombatStatsDefinition stats, AttackDefinition attack, float speed, float detection, float chase, float leash,
        float sight, float close, float hearing, float alertness, float poise, MonsterMovementTactic tactic, MonsterTemperament temperament, float hover, Action<SerializedObject> extra)
    {
        string path = Monsters + file + ".asset";
        var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(path);
        if (monster == null)
        {
            monster = ScriptableObject.CreateInstance<MonsterDefinition>();
            AssetDatabase.CreateAsset(monster, path);
        }

        Edit(monster, so =>
        {
            so.FindProperty("id").stringValue = file;
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("combatStats").objectReferenceValue = stats;
            so.FindProperty("faction").intValue = (int)Faction.Enemy;
            so.FindProperty("attack").objectReferenceValue = attack;
            so.FindProperty("moveSpeed").floatValue = speed;
            so.FindProperty("detectionRange").floatValue = detection;
            so.FindProperty("chaseRange").floatValue = chase;
            so.FindProperty("leashRange").floatValue = leash;
            so.FindProperty("sightAngle").floatValue = sight;
            so.FindProperty("closeSenseRange").floatValue = close;
            so.FindProperty("hearing").floatValue = hearing;
            so.FindProperty("alertness").floatValue = alertness;
            so.FindProperty("poise").floatValue = poise;
            so.FindProperty("movementTactic").intValue = (int)tactic;
            so.FindProperty("temperament").intValue = (int)temperament;
            so.FindProperty("hoverHeight").floatValue = hover;
            extra(so);
        });
        return monster;
    }

    private static void Prefab(MonsterDefinition definition, GameObject visual)
    {
        GameObject prefab = definition.Prefab;
        if (prefab == null)
        {
            prefab = MonsterPrefabBuilder.Create(definition, visual);
            Edit(definition, so => so.FindProperty("prefab").objectReferenceValue = prefab);
        }

        MonsterPrefabBuilder.Update(prefab, definition);
    }

    private static CombatStatsDefinition Stats(string file, string template, float health, float armor, params (StatType stat, float value)[] extra)
    {
        string path = StatsFolder + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(StatsFolder + template + ".asset", path);
        }

        var stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(path);
        Edit(stats, so =>
        {
            SerializedProperty list = so.FindProperty("stats");
            Set(list, StatType.MaxHealth, health);
            Set(list, StatType.Armor, armor);
            foreach ((StatType stat, float value) in extra)
            {
                Set(list, stat, value);
            }
        });
        return stats;
    }

    private static void Set(SerializedProperty list, StatType stat, float value)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("stat").intValue == (int)stat)
            {
                entry.FindPropertyRelative("value").floatValue = value;
                return;
            }
        }

        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
        added.FindPropertyRelative("stat").intValue = (int)stat;
        added.FindPropertyRelative("value").floatValue = value;
    }

    private static AttackDefinition Attack(string file, string template, string label, Action<SerializedObject> change)
    {
        string path = Attacks + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<AttackDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(Attacks + template + ".asset", path);
        }

        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
        Edit(attack, so =>
        {
            so.FindProperty("displayName").stringValue = label;
            change(so);
        });
        return attack;
    }

    private static GameObject Projectile(string file, Material material, bool glows)
    {
        string path = Enemies + file + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            AssetDatabase.CopyAsset(Enemies + "TrollStone.prefab", path);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = file;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }

            if (glows && root.GetComponentInChildren<Light>() == null)
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Point;
                light.color = new Color(0.55f, 0.85f, 1f);
                light.intensity = 1.5f;
                light.range = 4f;
                light.shadows = LightShadows.None;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static GameObject Visual(string file, Action<Transform> shape)
    {
        string path = Visuals + file + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject(file);
        try
        {
            shape(root.transform);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform Piece(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = material;
        return piece.transform;
    }

    private static Material Solid(string name, Color color)
    {
        string path = Visuals + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static Material Emissive(string name, Color color, Color emission)
    {
        Material material = Solid(name, color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material Transparent(string name, Color color)
    {
        Material material = Solid(name, color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Edit(Object asset, Action<SerializedObject> change)
    {
        var so = new SerializedObject(asset);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void Refresh()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database.CollectAll();
            EditorUtility.SetDirty(database);
        }

        AssetDatabase.SaveAssets();
    }
}
