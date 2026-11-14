#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives the creatures real bodies from the CC0 models in Art/Enemies/Downloaded (see its CREDITS.md; run with `unity command script run
/// --file AgentScripts/BuildCreatureModels.cs --entry BuildCreatureModels.Apply`). Person-shaped ones are imported as Humanoid and keep the
/// MonsterHumanoid controller, so their strikes, staggers and finishing blows stay as they were, with clips of their own laid over what doesn't
/// change a blow's timing. Animals are Generic with a small controller of their own, on the same parameters. The creature's "Model" child is replaced,
/// its collision and agent are sized to it and its eyes and gear hung on its bones; nothing else on the prefab changes. Re-running updates them.
/// </summary>
public static class BuildCreatureModels
{
    private const string Root = "Assets/_Project/Art/Enemies/Downloaded/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string Prologue = "Assets/_Project/Data/Prologue/";
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string Humanoid = "Assets/_Project/Animation/Controllers/MonsterHumanoid.controller";
    private const string BeastFolder = "Assets/_Project/Animation/Controllers/Beasts/";
    private const string UniversalClips = "Assets/Quaternius/Universal Animation Library/UAL1_Standard.fbx";
    private const string ZombieClips = "Assets/Quaternius/Universal Animation Library 2/UAL2_Standard.fbx";

    private static readonly Dictionary<string, Dictionary<string, string>> Rigs = new Dictionary<string, Dictionary<string, string>>
    {
        ["ForestMonster"] = Map("Hips", "Spine1", "Spine2", "Spine3", "Neck", "Head", "Shoulder_{0}", "Arm_{0}", "Forearm_{0}", "Hand_{0}", "Thigh_{0}", "Leg_{0}", "Foot_{0}", null, "L", "R"),
        ["Goblin"] = Map("spine1", "spine2", "spine3", "spine4", "neck", "head", "shoulder_{0}", "upperarm_{0}", "forearm_{0}", "hand_{0}", "upperleg_{0}", "lowerleg_{0}", "foot_{0}", "toe_{0}", "L", "R"),
        ["Bartender"] = Map("Hips", "Spine", "Spine1", null, "Neck", "Head", "Shoulder_{0}", "Arm_{0}", "ForeArm_{0}", "Hand_{0}", "UpLeg_{0}", "Leg_{0}", "Foot_{0}", "ToeBase_{0}", "L", "R"),
        ["Treant1"] = Treant(),
        ["Treant2"] = Treant(),
        // The user's forest troll (Hunyuan3D, rigged with Mixamo's skeleton in LocalSpawn), 2026-11-13.
        ["ForestTroll"] = Mixamo(),
        // The folk-tale creatures (BuildFolkCreatures), rigged by Make-It-Animatable with the same skeleton.
        ["Fossegrim"] = Mixamo(),
        ["Trollkjerring"] = Mixamo(),
        ["Bergjutul"] = Mixamo(),
        ["Boneman"] = Map("LowerSpine", "UpperSpine", null, null, "LowerNek", "Head", "{0}Clavicle", "{0}Humerus", "{0}Radius", "{0}Hand", "{0}Femur", "IK{0}Leg", "{0}Foot", null, "Left", "Right"),
    };

    // Mixamo's 52 bones (LocalSpawn's rigger gives every humanoid these).
    private static Dictionary<string, string> Mixamo() => Map("mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
        "mixamorig:Neck", "mixamorig:Head", "mixamorig:{0}Shoulder", "mixamorig:{0}Arm", "mixamorig:{0}ForeArm", "mixamorig:{0}Hand",
        "mixamorig:{0}UpLeg", "mixamorig:{0}Leg", "mixamorig:{0}Foot", "mixamorig:{0}ToeBase", "Left", "Right");

    // Both treants share one rig, with the head on the upper spine.
    private static Dictionary<string, string> Treant() => Map("spine", "spine.001", "spine.002", "spine.003", "spine.004", "spine.005", "shoulder.{0}", "upper_arm.{0}", "forearm.{0}", "hand.{0}", "thigh.{0}", "shin.{0}", "foot.{0}", "toe.{0}", "L", "R");

    private static Dictionary<string, string> Map(string hips, string spine, string chest, string upperChest, string neck, string head, string shoulder, string upperArm, string lowerArm, string hand, string upperLeg, string lowerLeg, string foot, string toes, string left, string right)
    {
        var map = new Dictionary<string, string> { ["Hips"] = hips, ["Spine"] = spine, ["Neck"] = neck, ["Head"] = head };
        if (chest != null) map["Chest"] = chest;
        if (upperChest != null) map["UpperChest"] = upperChest;
        foreach ((string human, string bone) in new[] { ("Left", left), ("Right", right) })
        {
            map[human + "Shoulder"] = string.Format(shoulder, bone);
            map[human + "UpperArm"] = string.Format(upperArm, bone);
            map[human + "LowerArm"] = string.Format(lowerArm, bone);
            map[human + "Hand"] = string.Format(hand, bone);
            map[human + "UpperLeg"] = string.Format(upperLeg, bone);
            map[human + "LowerLeg"] = string.Format(lowerLeg, bone);
            map[human + "Foot"] = string.Format(foot, bone);
            if (toes != null) map[human + "Toes"] = string.Format(toes, bone);
        }

        return map;
    }

    /// <summary>A material for one slot of a model's mesh: matched by the slot's own name or the mesh's, else by its place.</summary>
    private sealed class Skin
    {
        public string Slot, Base, Normal, EmissionMap;
        public Color Tint = Color.white;
        public float Smooth = 0.12f;
        public bool Cutout;
        public bool TwoSided;
        // The model's own eyes: they are lit as the creature's, in this colour.
        public bool Eye;
    }

    /// <summary>A pair of eyes, in metres from the head bone on the finished body in its standing pose: to the side, up and forward.</summary>
    private sealed class Eyes
    {
        public string Bone;
        public Vector3 One, Other;
        public float Size;
        public Color Colour;

        public static Eyes Level(string bone, float side, float up, float forward, float size, Color colour) =>
            new Eyes { Bone = bone, One = new Vector3(side, up, forward), Other = new Vector3(-side, up, forward), Size = size, Colour = colour };
    }

    private sealed class Body
    {
        public string Creature, Model, Label;
        public string Folder = Enemies;
        public float Height;
        public Skin[] Skins;
        public string[] Hide = Array.Empty<string>();
        public float Lift;
        public float Turn;
        public float Radius;
        public float AgentRadius;
        public float SizeVariation, ShadeVariation;
        public bool FallsByAnimation;
        public Eyes Eyes;
        // A Generic body's clips, by name.
        public string Idle, Walk, Run, Attack, Death, Rest, Stagger;
        // Metres a second its stride keeps up with. 0: its clip doesn't follow its speed.
        public float Stride;
        // A Humanoid body's clips of its own: which clip file, and whether it shambles.
        public string OwnClips;
        public bool Shambles;
    }

    // Bodies of one model share a controller, and replacing it would leave the ones already done holding nothing.
    private static readonly Dictionary<string, RuntimeAnimatorController> Built = new Dictionary<string, RuntimeAnimatorController>();

    private static readonly Color Moss = new Color(1f, 1.08f, 0.88f);
    private static readonly Color Grave = new Color(0.58f, 0.62f, 0.6f);
    private static readonly Color CurseGreen = new Color(0.5f, 2.2f, 0.15f);
    private static readonly Color Ember = new Color(2.4f, 0.75f, 0.1f);
    private static readonly Color GraveLight = new Color(0.45f, 1.1f, 2.4f);

    private static Skin[] ForestSkin(Color tint) => new[]
    {
        new Skin { Slot = "Monster", Base = "monster-skin.png", Tint = tint },
        new Skin { Slot = "Tree", Base = "tree.png", Tint = new Color(1.5f, 1.55f, 1.2f), Cutout = true },
    };

    private static Skin[] TreantSkin(string tree, string albedo, Color tint, string emission = null) => new[]
    {
        new Skin { Slot = tree, Base = albedo, Normal = tree + " Normal.png", Tint = tint, Smooth = 0.1f, EmissionMap = emission },
    };

    private static Skin[] GoblinSkin(Color tint, Color eyes) => new[]
    {
        new Skin { Slot = "goblin", Base = "gobli-tex.png", Normal = "goblin-norm.png", Tint = tint },
        new Skin { Slot = "Gold", Tint = new Color(0.5f, 0.42f, 0.22f), Smooth = 0.5f },
        new Skin { Slot = "Eyes", Tint = eyes, Eye = true },
    };

    // One texture painted with everything, eyes included.
    private static Skin[] ForestTrollSkin(Color tint) => new[]
    {
        new Skin { Slot = "Material_0", Base = "ForestTroll_BaseColor.png", Normal = "ForestTroll_Normal.png", Tint = tint, Smooth = 0.22f },
    };

    // Unity makes the coarser levels at import (Mesh LOD), keeping the texture's seams: Blender's collapse tore them.
    // Level 5 (1.2k) smeared the face, so it stops at level 4 (2.4k).
    private static readonly Dictionary<string, int> MeshLods = new Dictionary<string, int> { ["ForestTroll"] = 4, ["Fossegrim"] = 3, ["Trollkjerring"] = 3, ["Bergjutul"] = 3 };

    // A generated creature's one painted texture, matched by the mesh's name.
    private static Skin[] FolkSkin(string name, Color tint, float smooth = 0.2f) => new[]
    {
        new Skin { Slot = name, Base = name + "_BaseColor.png", Normal = name + "_Normal.png", Tint = tint, Smooth = smooth },
    };

    private static Skin[] WolfSkin() => new[]
    {
        new Skin { Slot = "Main", Tint = new Color(0.27f, 0.25f, 0.24f), Smooth = 0.05f },
        new Skin { Slot = "Main_Light", Tint = new Color(0.42f, 0.4f, 0.37f), Smooth = 0.05f },
        new Skin { Slot = "Nose", Tint = new Color(0.03f, 0.03f, 0.03f), Smooth = 0.5f },
        new Skin { Slot = "Eyes_Black", Tint = CurseGreen, Eye = true },
    };

    private static Skin[] BoarSkin() => new[] { new Skin { Slot = "Material", Base = "boar.png", Tint = new Color(0.62f, 0.6f, 0.52f), Smooth = 0.05f } };

    private static Skin[] BatSkin() => new[]
    {
        new Skin { Slot = "bat", Base = "bat_tex.png", Tint = new Color(2f, 1.85f, 2.1f), TwoSided = true },
        new Skin { Slot = "bat_parts", Base = "bat_parts.png" },
    };

    private static Body Wolf(string creature, string folder) => new Body
    {
        Creature = creature, Folder = folder, Model = "Wolf", Label = "Wolf", Height = 1.05f, Radius = 0.42f, AgentRadius = 0.38f, SizeVariation = 0.08f, ShadeVariation = 0.14f, Skins = WolfSkin(),
        Idle = "Idle", Walk = "Walk", Run = "Gallop", Attack = "Attack", Rest = "Idle_2_HeadLow", Stagger = "Idle_HitReact_Left", Stride = 5.5f,
    };

    private static Body Boar(string creature, string folder) => new Body
    {
        Creature = creature, Folder = folder, Model = "Boar", Label = "Boar", Height = 1.15f, Turn = 180f, Radius = 0.5f, AgentRadius = 0.43f, SizeVariation = 0.08f, ShadeVariation = 0.12f, Skins = BoarSkin(),
        Idle = "Stand", Walk = "walk", Attack = "attack", Stride = 2.6f,
        Eyes = Eyes.Level("head", 0.095f, -0.085f, 0.23f, 0.028f, CurseGreen),
    };

    private static Body Bat(string creature, string folder) => new Body
    {
        Creature = creature, Folder = folder, Model = "VampireBat", Label = "Mistwing", Height = 0.75f, Lift = 0.6f, Radius = 0.45f, AgentRadius = 0.33f, SizeVariation = 0.1f, ShadeVariation = 0.1f, Skins = BatSkin(),
        Idle = "Bat_Flying", Walk = "Bat_Flying", Attack = "Bat_Attack", Death = "Bat_Die",
    };

    // The user's own forest troll since 2026-11-13 (it was the Kelgar goblin, still the Shaman's): the same Humanoid clips.
    private static Body SmallTroll(string creature, string folder) => new Body
    {
        Creature = creature, Folder = folder, Model = "ForestTroll", Label = "Small Troll", Height = 1.6f, Radius = 0.45f, AgentRadius = 0.45f, SizeVariation = 0.08f, ShadeVariation = 0.12f, Skins = ForestTrollSkin(Moss),
    };

    private static Body Rootling(string creature, string folder) => new Body
    {
        Creature = creature, Folder = folder, Model = "Treant1", Label = "Rootling", Height = 0.95f, Radius = 0.26f, AgentRadius = 0.23f, SizeVariation = 0.16f, ShadeVariation = 0.16f, FallsByAnimation = true, OwnClips = "Treant1",
        Skins = TreantSkin("Tree01", "Tree01 Albedo Light.png", Color.white),
        Eyes = Eyes.Level("spine.005", 0.02f, 0.046f, 0.108f, 0.012f, CurseGreen),
    };

    private static readonly Body[] Bodies =
    {
        Wolf("ExpeditionWolf", Prologue),
        Boar("ExpeditionBoar", Prologue),
        Bat("ExpeditionMistwing", Prologue),
        SmallTroll("ExpeditionSmallMossTroll", Prologue),
        Rootling("ExpeditionRootling", Prologue),
        new Body { Creature = "OldMossTroll", Model = "ForestMonster", Label = "Old Troll", Height = 5.3f, Radius = 1.1f, AgentRadius = 0.9f, Skins = ForestSkin(Moss),
            Eyes = new Eyes { Bone = "Head", One = new Vector3(0.31f, 0.03f, 0.32f), Other = new Vector3(0.35f, -0.07f, 0.03f), Size = 0.075f, Colour = Ember } },
        new Body { Creature = "RootGuardian", Model = "Treant1", Label = "Guardian", Height = 2.9f, Radius = 0.65f, AgentRadius = 0.55f, SizeVariation = 0.06f, ShadeVariation = 0.1f, FallsByAnimation = true, OwnClips = "Treant1",
            Skins = TreantSkin("Tree01", "Tree01 Albedo Dark.png", Color.white),
            Eyes = Eyes.Level("spine.005", 0.06f, 0.14f, 0.33f, 0.035f, CurseGreen) },
        new Body { Creature = "TheHeartroot", Model = "Treant2", Label = "Heartroot", Height = 4.9f, Radius = 1.05f, AgentRadius = 0.9f, FallsByAnimation = true, OwnClips = "Treant2",
            Skins = TreantSkin("Tree02", "Tree02 Albedo Dark.png", new Color(0.92f, 0.86f, 0.8f), "Tree02 Veins.png"),
            Eyes = Eyes.Level("spine.005", 0.1f, 0.35f, 0.55f, 0.06f, CurseGreen) },
        Rootling("Rootling", Enemies),
        SmallTroll("SmallMossTroll", Enemies),
        // Folk-tale creatures (BuildFolkCreatures), 2026-11-13.
        new Body { Creature = "Fossegrim", Model = "Fossegrim", Label = "Fossegrim", Height = 2.1f, Radius = 0.4f, AgentRadius = 0.4f, SizeVariation = 0.05f, ShadeVariation = 0.08f,
            Skins = FolkSkin("Fossegrim", Color.white, 0.45f) },
        new Body { Creature = "Trollkjerring", Model = "Trollkjerring", Label = "Trollkjerring", Height = 1.9f, Radius = 0.5f, AgentRadius = 0.5f, SizeVariation = 0.06f, ShadeVariation = 0.1f,
            Skins = FolkSkin("Trollkjerring", Color.white) },
        new Body { Creature = "Bergjutul", Model = "Bergjutul", Label = "Berg-jutul", Height = 3.6f, Radius = 0.95f, AgentRadius = 0.85f, SizeVariation = 0.08f, ShadeVariation = 0.08f,
            Skins = FolkSkin("Bergjutul", Color.white, 0.1f) },
        // The thief (BuildTusse): the same forest troll, smaller and paler, the colour of old roots.
        new Body { Creature = "Tusse", Model = "ForestTroll", Label = "Tusse", Height = 1.15f, Radius = 0.35f, AgentRadius = 0.35f, SizeVariation = 0.1f, ShadeVariation = 0.1f,
            Skins = ForestTrollSkin(new Color(0.78f, 0.82f, 0.74f)) },
        new Body { Creature = "MossShaman", Model = "Goblin", Label = "Shaman", Height = 2f, Radius = 0.5f, AgentRadius = 0.5f, SizeVariation = 0.05f, ShadeVariation = 0.08f, Skins = GoblinSkin(new Color(0.62f, 0.76f, 0.58f), CurseGreen), Hide = new[] { "Ear", "Neck" } },
        new Body { Creature = "Draug", Model = "Boneman", Label = "Draug", Height = 2.1f, Radius = 0.38f, AgentRadius = 0.4f, SizeVariation = 0.05f, ShadeVariation = 0.08f, Shambles = true, Skins = new[] { new Skin { Slot = "Skeli", Base = "SkeliCL.png", Normal = "skin_n.png", Tint = Grave } },
            Eyes = new Eyes { Bone = "Head", One = new Vector3(0.042f, 0.055f, 0.09f), Other = new Vector3(-0.03f, 0.055f, 0.09f), Size = 0.02f, Colour = GraveLight } },
        new Body { Creature = "Huldra", Model = "Bartender", Label = "Huldra", Height = 1.85f, Radius = 0.42f, AgentRadius = 0.45f, Skins = new[]
        {
            new Skin { Slot = "Hair", Base = "hair-skin.png", Tint = new Color(0.7f, 0.74f, 0.55f) },
            new Skin { Slot = "Head", Base = "head-skin.png", Normal = "head-norm.png", Tint = new Color(0.9f, 0.94f, 0.84f) },
            new Skin { Slot = "Body", Base = "body-skin.png", Normal = "body-norm.png", Tint = new Color(0.72f, 0.84f, 0.66f) },
        } },
        Wolf("ForestBittenWolf", Enemies),
        Boar("BlightBoar", Enemies),
        new Body { Creature = "BogSpitter", Model = "Limule", Label = "Bog Spitter", Height = 0.55f, Radius = 0.62f, AgentRadius = 0.48f, SizeVariation = 0.12f, ShadeVariation = 0.14f, Skins = new[] { new Skin { Slot = "Material", Base = "lim_tex_by_designandshine-ch.png", Normal = "limule-Nor.png", Smooth = 0.3f } }, Idle = "Idle", Walk = "Forward", Attack = "Attack.000", Stride = 1.6f },
        Bat("Mistwing", Enemies),
    };

    public static string Apply()
    {
        var log = new List<string>();
        foreach (string model in Bodies.Select(b => b.Model).Distinct())
        {
            Import(model, log);
        }

        foreach (string model in new[] { "Treant1", "Treant2" })
        {
            ImportOwnClips(model, log);
        }

        EnsureRestState(log);
        foreach (string gear in new[] { "DraugMail", "DraugHelm", "HuldraBack" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath($"{Root}Gear/{gear}.fbx");
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        return RefitBodies(log);
    }

    /// <summary>Only the forest troll's import and the bodies made of it (`--entry BuildCreatureModels.ForestTrolls`).</summary>
    public static string ForestTrolls()
    {
        var log = new List<string>();
        AssetDatabase.Refresh();
        Import("ForestTroll", log);
        foreach (Body body in Bodies.Where(b => b.Model == "ForestTroll"))
        {
            Swap(body, log);
            Definition(body);
        }

        AssetDatabase.SaveAssets();
        return string.Join(" ", log);
    }

    /// <summary>Only the folk-tale creatures' imports and bodies (`--entry BuildCreatureModels.FolkBodies`), after BuildFolkCreatures.</summary>
    public static string FolkBodies()
    {
        var log = new List<string>();
        AssetDatabase.Refresh();
        string[] folk = { "Fossegrim", "Trollkjerring", "Bergjutul" };
        foreach (string model in folk.Where(m => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}{m}/{m}.fbx") != null))
        {
            Import(model, log);
        }

        foreach (Body body in Bodies.Where(b => folk.Contains(b.Model) && AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}{b.Model}/{b.Model}.fbx") != null))
        {
            Swap(body, log);
            Definition(body);
        }

        AssetDatabase.SaveAssets();
        return string.Join(" ", log);
    }

    /// <summary>Only what goes on the prefabs, for tuning eyes and gear without importing the models again.</summary>
    public static string Refit() => RefitBodies(new List<string>());

    private static string RefitBodies(List<string> log)
    {
        ShieldCopy shield = KeepShield();
        foreach (Body body in Bodies)
        {
            Swap(body, log);
            Definition(body);
        }

        GiveBackShield(shield, log);
        BossGlow(log);
        AssetDatabase.SaveAssets();
        return string.Join(" ", log);
    }

    private static void Definition(Body body)
    {
        var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{Monsters}{body.Creature}.asset");
        if (definition == null)
        {
            return;
        }

        var so = new SerializedObject(definition);
        so.FindProperty("sizeVariation").floatValue = body.SizeVariation;
        so.FindProperty("shadeVariation").floatValue = body.ShadeVariation;
        so.FindProperty("fallsByAnimation").boolValue = body.FallsByAnimation;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The fight's stages can be seen on the Heartroot: its green veins wake, a little more each time.
    private static void BossGlow(List<string> log)
    {
        var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>($"{Monsters}Boss The Heartroot.asset");
        if (boss == null)
        {
            log.Add("No Heartroot boss.");
            return;
        }

        var so = new SerializedObject(boss);
        SerializedProperty phases = so.FindProperty("phases");
        for (int i = 0; i < phases.arraySize; i++)
        {
            float strength = (i + 1) * 1.1f;
            phases.GetArrayElementAtIndex(i).FindPropertyRelative("glow").colorValue = new Color(0.25f * strength, 1.2f * strength, 0.2f * strength, 1f);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"{phases.arraySize} boss stages glow.");
    }

    private static void Import(string model, List<string> log)
    {
        string path = $"{Root}{model}/{model}.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        foreach (string file in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + model }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var texture = (TextureImporter)AssetImporter.GetAtPath(file);
            bool normal = file.Contains("norm") || file.Contains("Normal") || file.Contains("Nor.") || file.Contains("_n.");
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (texture.textureType != type || texture.maxTextureSize != 1024)
            {
                texture.textureType = type;
                texture.maxTextureSize = 1024;
                texture.SaveAndReimport();
            }
        }

        // Kept so a slot can be told by its name. The slots get this project's own materials; these are never shown.
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.generateMeshLods = MeshLods.TryGetValue(model, out int coarsest);
        if (importer.generateMeshLods)
        {
            importer.maximumMeshLod = coarsest;
        }

        if (Rigs.TryGetValue(model, out Dictionary<string, string> rig))
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.SaveAndReimport();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            HumanDescription description = importer.humanDescription;
            description.human = HumanBones(rig);
            description.skeleton = asset.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            importer.humanDescription = description;
            importer.SaveAndReimport();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            log.Add($"{model}: human={(avatar != null && avatar.isValid && avatar.isHuman)}.");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.SaveAndReimport();
        var clips = new List<ModelImporterClipAnimation>(importer.defaultClipAnimations);
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.name = ShortName(clip.takeName);
            clip.loopTime = LoopsByName(clip.name);
        }

        // A model with no idle of its own stands on the first frame of its walk.
        if (model == "Boar")
        {
            ModelImporterClipAnimation walk = clips.First(c => c.name == "walk");
            clips.Add(new ModelImporterClipAnimation { name = "Stand", takeName = walk.takeName, firstFrame = walk.firstFrame, lastFrame = walk.firstFrame + 1f, loopTime = true });
        }

        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();
        log.Add($"{model}: {clips.Count} clips.");
    }

    private static HumanBone[] HumanBones(Dictionary<string, string> rig) =>
        rig.Select(pair => new HumanBone { humanName = HumanName(pair.Key), boneName = pair.Value, limit = new HumanLimit { useDefaultValues = true } }).ToArray();

    private static string ShortName(string take) => take.Contains("|") ? take.Substring(take.LastIndexOf('|') + 1) : take;

    private static bool LoopsByName(string name)
    {
        string lower = name.ToLowerInvariant();
        return !lower.Contains("attack") && !lower.Contains("die") && !lower.Contains("death") && !lower.Contains("hitreact") && !lower.Contains("jump") && !lower.Contains("taunt");
    }

    // The treants' own clips, in the file they came in. Its rest pose has the arms down, which a Humanoid would take for its T-pose and so hold them up
    // in every clip: the description gets the same bones with the limbs straightened, as the Avatar window's Enforce T-Pose would do.
    private static void ImportOwnClips(string model, List<string> log)
    {
        string path = $"{Root}{model}/{model}Clips.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        Dictionary<string, string> rig = Rigs[model];
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.SaveAndReimport();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            Transform Bone(string human) => FindDeep(body.transform, rig[human]);
            Transform hips = Bone("Hips");
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform upperArm = Bone(side + "UpperArm");
                Transform lowerArm = Bone(side + "LowerArm");
                Transform hand = Bone(side + "Hand");
                Vector3 outward = Vector3.right * Mathf.Sign(upperArm.position.x - hips.position.x);
                Align(upperArm, lowerArm, outward);
                Align(lowerArm, hand, outward);
                Transform upperLeg = Bone(side + "UpperLeg");
                Transform lowerLeg = Bone(side + "LowerLeg");
                Transform foot = Bone(side + "Foot");
                Align(upperLeg, lowerLeg, Vector3.down);
                Align(lowerLeg, foot, Vector3.down);
            }

            HumanDescription description = importer.humanDescription;
            description.human = HumanBones(rig);
            description.skeleton = body.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            importer.humanDescription = description;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.name = ShortName(clip.takeName);
            clip.loopTime = LoopsByName(clip.name);
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        log.Add($"{model} clips: {clips.Length}, human={(avatar != null && avatar.isValid && avatar.isHuman)}.");
    }

    private static void Align(Transform bone, Transform next, Vector3 direction)
    {
        Vector3 now = next.position - bone.position;
        if (now.sqrMagnitude > 1e-8f)
        {
            bone.rotation = Quaternion.FromToRotation(now.normalized, direction) * bone.rotation;
        }
    }

    // Mecanim's own spelling of a bone, whichever way it spaces them.
    private static string HumanName(string key)
    {
        string wanted = key.Replace(" ", "");
        return HumanTrait.BoneName.First(name => name.Replace(" ", "") == wanted);
    }

    // The humanoid controller's place to rest: what a creature does while grazing or asleep, instead of being tipped and pressed down whole.
    private static void EnsureRestState(List<string> log)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Humanoid);
        AnimationClip crouch = Universal("Crouch_Idle_Loop");
        if (controller.parameters.Any(p => p.name == MonsterAnimationDriver.RestingParameter) || crouch == null)
        {
            return;
        }

        controller.AddParameter(MonsterAnimationDriver.RestingParameter, AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = machine.states.First(s => s.state.name == "Locomotion").state;
        AnimatorState rest = machine.AddState("Rest");
        rest.motion = crouch;
        AnimatorStateTransition down = locomotion.AddTransition(rest);
        down.hasExitTime = false;
        down.duration = 0.5f;
        down.AddCondition(AnimatorConditionMode.If, 0f, MonsterAnimationDriver.RestingParameter);
        AnimatorStateTransition up = rest.AddTransition(locomotion);
        up.hasExitTime = false;
        up.duration = 0.35f;
        up.AddCondition(AnimatorConditionMode.IfNot, 0f, MonsterAnimationDriver.RestingParameter);
        EditorUtility.SetDirty(controller);
        log.Add("Rest state added.");
    }

    private static AnimationClip Universal(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(UniversalClips).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

    private static RuntimeAnimatorController HumanoidController(Body body)
    {
        var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Humanoid);
        if (body.OwnClips == null && !body.Shambles)
        {
            return baseController;
        }

        var replace = new Dictionary<string, AnimationClip>();
        AnimatorState[] states = baseController.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        string ClipOf(string state) => (states.First(s => s.name == state).motion as AnimationClip)?.name;
        ChildMotion[] strides = ((BlendTree)states.First(s => s.name == "Locomotion").motion).children;
        if (body.OwnClips != null)
        {
            AnimationClip[] own = AssetDatabase.LoadAllAssetsAtPath($"{Root}{body.OwnClips}/{body.OwnClips}Clips.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            AnimationClip Own(string name) => own.FirstOrDefault(c => c.name == name);
            replace[strides[0].motion.name] = Own("Idle");
            replace[strides[strides.Length - 1].motion.name] = Own("Run");
            replace[ClipOf("Roar")] = Own("Taunt");
            replace[ClipOf("Death")] = Own("Death1");
        }

        if (body.Shambles)
        {
            AnimationClip[] all = AssetDatabase.LoadAllAssetsAtPath(ZombieClips).OfType<AnimationClip>().ToArray();
            AnimationClip idle = all.FirstOrDefault(c => c.name == "Zombie_Idle_Loop");
            AnimationClip walk = all.FirstOrDefault(c => c.name == "Zombie_Walk_Fwd_Loop");
            if (idle != null) replace[strides[0].motion.name] = idle;
            if (walk != null)
            {
                for (int i = 1; i < strides.Length; i++)
                {
                    replace[strides[i].motion.name] = walk;
                }
            }
        }

        System.IO.Directory.CreateDirectory(BeastFolder);
        string path = $"{BeastFolder}{(body.OwnClips ?? body.Label)}.overrideController";
        if (Built.TryGetValue(path, out RuntimeAnimatorController built))
        {
            return built;
        }

        AssetDatabase.DeleteAsset(path);
        var controller = new AnimatorOverrideController(baseController);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            if (overrides[i].Key != null && replace.TryGetValue(overrides[i].Key.name, out AnimationClip clip) && clip != null)
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
            }
        }

        controller.ApplyOverrides(overrides);
        AssetDatabase.CreateAsset(controller, path);
        Built[path] = controller;
        return controller;
    }

    private static Material MaterialFor(Body body, Skin skin)
    {
        string folder = $"{Root}{body.Model}/";
        if (skin.Eye)
        {
            return EyeMaterial(body.Label, skin.Tint);
        }

        string path = $"{folder}{body.Label} {skin.Slot}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", skin.Tint);
        material.SetFloat("_Smoothness", skin.Smooth);
        // Cleared as well as set, so a slot that gives up a texture on a re-run really does.
        material.SetTexture("_BaseMap", skin.Base != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(folder + skin.Base) : null);
        material.SetTexture("_MainTex", material.GetTexture("_BaseMap"));
        material.SetTexture("_BumpMap", skin.Normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(folder + skin.Normal) : null);
        if (skin.Normal != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            material.DisableKeyword("_NORMALMAP");
        }

        material.SetTexture("_EmissionMap", skin.EmissionMap != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(folder + skin.EmissionMap) : null);
        material.SetColor("_EmissionColor", Color.black);
        if (skin.EmissionMap != null)
        {
            // On at black, so the variant is in the build and CreatureLook only has to change the colour.
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
        }

        material.SetFloat("_AlphaClip", skin.Cutout ? 1f : 0f);
        material.SetFloat("_Cull", skin.Cutout || skin.TwoSided ? 0f : 2f);
        if (skin.Cutout)
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cutoff", 0.4f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = -1;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material PlainMaterial(string name, Color colour, float smooth, float metal, string texture = null, Vector2? tile = null)
    {
        string path = $"{Root}Gear/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", smooth);
        material.SetFloat("_Metallic", metal);
        if (texture != null)
        {
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            material.SetTextureScale("_BaseMap", tile ?? Vector2.one);
        }

        material.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Swap(Body body, List<string> log)
    {
        string prefabPath = $"{body.Folder}{body.Creature}.prefab";
        string modelPath = $"{Root}{body.Model}/{body.Model}.fbx";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform blade = FindDeep(root.transform, "Blade");
            Vector3 bladeFromHand = Vector3.zero;
            Quaternion bladeRotation = Quaternion.identity;
            Vector3 bladeScale = Vector3.one;
            if (blade != null)
            {
                bladeFromHand = blade.position - blade.parent.position;
                bladeRotation = blade.rotation;
                bladeScale = blade.lossyScale;
                blade.SetParent(null, true);
            }

            RemoveOldBody(root);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            model.name = "Model";
            model.transform.SetSiblingIndex(0);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, body.Turn, 0f);
            model.transform.localScale = Vector3.one;
            Dress(body, model);

            Bounds bounds = BoundsOf(model);
            float scale = body.Height / Mathf.Max(0.001f, bounds.size.y);
            model.transform.localScale = Vector3.one * scale;
            bounds = BoundsOf(model);
            model.transform.localPosition = new Vector3(0f, -bounds.min.y + body.Lift, 0f);

            var animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.runtimeAnimatorController = Rigs.ContainsKey(body.Model) ? HumanoidController(body) : BeastController(body, modelPath);

            if (Rigs.TryGetValue(body.Model, out Dictionary<string, string> rig))
            {
                Transform hand = FindDeep(model.transform, rig["RightHand"]);
                if (blade != null && hand != null)
                {
                    blade.SetParent(hand, true);
                    blade.position = hand.position + bladeFromHand;
                    blade.rotation = bladeRotation;
                    blade.localScale = new Vector3(bladeScale.x / hand.lossyScale.x, bladeScale.y / hand.lossyScale.y, bladeScale.z / hand.lossyScale.z);
                }

                if (body.Creature == "MossShaman" && hand != null)
                {
                    Staff(hand);
                }

                if (body.Creature == "Draug")
                {
                    DraugGear(model.transform, rig);
                }

                if (body.Creature == "Huldra")
                {
                    HollowBack(model.transform, rig);
                }
            }
            else if (blade != null)
            {
                UnityEngine.Object.DestroyImmediate(blade.gameObject);
            }

            AddEyes(body, model, log);
            Fit(root, body);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            log.Add($"{body.Creature} {BoundsOf(model).size.y:0.0} m.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Collision and the agent as the body now stands, so blows and arrows land on what they appear to.
    private static void Fit(GameObject root, Body body)
    {
        if (root.TryGetComponent(out CapsuleCollider capsule) && body.Radius > 0f)
        {
            capsule.radius = body.Radius;
            capsule.height = Mathf.Max(body.Height, body.Radius * 2f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f + body.Lift, 0f);
        }

        if (root.TryGetComponent(out NavMeshAgent agent) && body.AgentRadius > 0f)
        {
            agent.radius = body.AgentRadius;
            agent.height = body.Height;
        }

        if (body.Stride > 0f && root.TryGetComponent(out MonsterAnimationDriver driver))
        {
            var so = new SerializedObject(driver);
            so.FindProperty("strideSpeed").floatValue = body.Stride;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AddEyes(Body body, GameObject model, List<string> log)
    {
        if (body.Eyes == null)
        {
            return;
        }

        Transform head = FindDeep(model.transform, body.Eyes.Bone);
        if (head == null)
        {
            log.Add($"{body.Creature}: no bone {body.Eyes.Bone} for its eyes.");
            return;
        }

        Material material = EyeMaterial(body.Label, body.Eyes.Colour);
        foreach (Vector3 offset in new[] { body.Eyes.One, body.Eyes.Other })
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.name = "Eye";
            var renderer = eye.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            InStandingPose(model, () =>
            {
                eye.transform.SetParent(head, false);
                eye.transform.position = head.position + model.transform.parent.rotation * offset;
                SetWorldScale(eye.transform, Vector3.one * body.Eyes.Size);
            });
        }
    }

    // Unlit, so they show in the dark; the name is how CreatureLook knows them.
    private static Material EyeMaterial(string label, Color colour)
    {
        string path = $"{Root}Gear/{CreatureLook.EyeMaterial}{label}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", colour);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Eyes are measured on the pose a creature is seen in, which can be far from the one its file was modelled in. The bones go back afterwards.
    private static void InStandingPose(GameObject model, Action place)
    {
        (Transform bone, Vector3 position, Quaternion rotation, Vector3 scale)[] rest = model.GetComponentsInChildren<Transform>(true)
            .Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToArray();
        AnimationClip clip = StandingClip(model.GetComponent<Animator>().runtimeAnimatorController);
        if (clip != null)
        {
            clip.SampleAnimation(model, 0f);
        }

        place();
        foreach ((Transform bone, Vector3 position, Quaternion rotation, Vector3 scale) in rest)
        {
            bone.localPosition = position;
            bone.localRotation = rotation;
            bone.localScale = scale;
        }
    }

    private static AnimationClip StandingClip(RuntimeAnimatorController runtime)
    {
        var over = runtime as AnimatorOverrideController;
        var controller = (over != null ? over.runtimeAnimatorController : runtime) as AnimatorController;
        Motion motion = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state.motion;
        AnimationClip clip = motion as AnimationClip ?? (motion as BlendTree)?.children[0].motion as AnimationClip;
        return over != null && clip != null ? over[clip] : clip;
    }

    // What the prefab's body was: its Model child, or a Huldra's Animator and body on the root itself.
    private static void RemoveOldBody(GameObject root)
    {
        Transform old = root.transform.Find("Model");
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            return;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(root))
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        }

        foreach (string name in new[] { "HumanF_BodyMesh", "HumanM_BodyMesh", "Rig" })
        {
            Transform part = root.transform.Find(name);
            if (part != null)
            {
                UnityEngine.Object.DestroyImmediate(part.gameObject);
            }
        }

        if (root.TryGetComponent(out Animator animator))
        {
            UnityEngine.Object.DestroyImmediate(animator);
        }
    }

    private static void Dress(Body body, GameObject model)
    {
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (body.Hide.Contains(renderer.name))
            {
                renderer.gameObject.SetActive(false);
                continue;
            }

            Material[] imported = renderer.sharedMaterials;
            var materials = new Material[renderer.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                string slot = i < imported.Length && imported[i] != null ? imported[i].name : null;
                Skin skin = body.Skins.FirstOrDefault(s => s.Slot == slot) ?? body.Skins.FirstOrDefault(s => s.Slot == renderer.name) ?? body.Skins[Mathf.Min(i, body.Skins.Length - 1)];
                materials[i] = MaterialFor(body, skin);
            }

            renderer.sharedMaterials = materials;
            renderer.updateWhenOffscreen = false;
        }
    }

    private static Bounds BoundsOf(GameObject model)
    {
        var bounds = new Bounds(model.transform.position, Vector3.zero);
        bool any = false;
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private static GameObject Piece(string fbx, Transform parent)
    {
        var piece = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}Gear/{fbx}.fbx"));
        PrefabUtility.UnpackPrefabInstance(piece, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        piece.transform.SetParent(parent, true);
        return piece;
    }

    // "Still in their mail": a torn shirt on the spine and a nasal helm on the skull, rigid pieces on a skeleton with little to bend them.
    private static void DraugGear(Transform model, Dictionary<string, string> rig)
    {
        Transform hips = FindDeep(model, rig["Hips"]);
        Transform chest = FindDeep(model, rig["Spine"]);
        Transform head = FindDeep(model, rig["Head"]);
        Transform neck = FindDeep(model, rig["Neck"]);
        Transform leftArm = FindDeep(model, rig["LeftUpperArm"]);
        Transform rightArm = FindDeep(model, rig["RightUpperArm"]);
        float span = Vector3.Distance(leftArm.position, rightArm.position);
        float tall = neck.position.y - hips.position.y;

        GameObject shirt = Piece("DraugMail", chest);
        shirt.name = "Mail";
        Material mail = PlainMaterial("Draug Mail", new Color(0.55f, 0.52f, 0.47f), 0.3f, 0.5f, $"{Root}Gear/mail.png", new Vector2(1.6f, 1.2f));
        foreach (Renderer part in shirt.GetComponentsInChildren<Renderer>())
        {
            part.sharedMaterial = mail;
        }

        shirt.transform.position = new Vector3(hips.position.x, hips.position.y - tall * 0.16f, hips.position.z);
        SetWorldScale(shirt.transform, new Vector3(span * 0.88f, tall * 1.18f, span * 0.82f));

        GameObject helm = Piece("DraugHelm", head);
        helm.name = "Helm";
        Material iron = PlainMaterial("Draug Helm", new Color(0.3f, 0.29f, 0.27f), 0.42f, 0.85f);
        foreach (Renderer part in helm.GetComponentsInChildren<Renderer>())
        {
            part.sharedMaterial = iron;
        }

        float skull = span * 0.37f;
        helm.transform.position = head.position + new Vector3(-0.011f, skull * 1.2f, skull * 0.35f);
        SetWorldScale(helm.transform, Vector3.one * skull);
    }

    // "With a hollow back like a rotten tree": bark from the nape to the hips, and dark inside.
    private static void HollowBack(Transform model, Dictionary<string, string> rig)
    {
        Transform chest = FindDeep(model, rig["Chest"]);
        Transform hips = FindDeep(model, rig["Hips"]);
        Transform neck = FindDeep(model, rig["Neck"]);
        Transform leftArm = FindDeep(model, rig["LeftUpperArm"]);
        Transform rightArm = FindDeep(model, rig["RightUpperArm"]);
        float span = Vector3.Distance(leftArm.position, rightArm.position);
        float tall = neck.position.y - hips.position.y;
        GameObject back = Piece("HuldraBack", chest);
        back.name = "Hollow Back";
        Material bark = PlainMaterial("Huldra Bark", Color.white, 0.08f, 0f, $"{Root}Treant1/Tree01 Albedo Dark.png");
        Material hollow = PlainMaterial("Huldra Hollow", new Color(0.02f, 0.015f, 0.01f), 0.02f, 0f);
        foreach (Renderer part in back.GetComponentsInChildren<Renderer>())
        {
            part.sharedMaterial = part.name == "Hollow" ? hollow : bark;
        }

        back.transform.position = new Vector3(chest.position.x, hips.position.y + tall * 0.54f, chest.position.z - span * 0.25f);
        SetWorldScale(back.transform, new Vector3(span * 0.92f, tall * 1.02f, span * 0.72f));
    }

    private static void Staff(Transform hand)
    {
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        Material orb = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Enemies/Shaman Orb.mat");
        var staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        UnityEngine.Object.DestroyImmediate(staff.GetComponent<Collider>());
        staff.name = "Staff";
        staff.GetComponent<Renderer>().sharedMaterial = wood;
        staff.transform.SetParent(hand, false);
        staff.transform.SetPositionAndRotation(hand.position + Vector3.up * 0.1f, Quaternion.identity);
        SetWorldScale(staff.transform, new Vector3(0.07f, 1f, 0.07f));
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());
        head.name = "Orb";
        head.GetComponent<Renderer>().sharedMaterial = orb;
        head.transform.SetParent(hand, false);
        head.transform.position = hand.position + Vector3.up * 1.15f;
        SetWorldScale(head.transform, Vector3.one * 0.26f);
    }

    private static void SetWorldScale(Transform transform, Vector3 world)
    {
        Vector3 parent = transform.parent.lossyScale;
        transform.localScale = new Vector3(world.x / parent.x, world.y / parent.y, world.z / parent.z);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }

    private static RuntimeAnimatorController BeastController(Body body, string modelPath)
    {
        System.IO.Directory.CreateDirectory(BeastFolder);
        string path = $"{BeastFolder}{body.Model}.controller";
        if (Built.TryGetValue(path, out RuntimeAnimatorController built))
        {
            return built;
        }

        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);
        controller.AddParameter(new AnimatorControllerParameter { name = "ActionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        AnimationClip Clip(string name) => name == null ? null : clips.FirstOrDefault(c => c.name == name);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip(body.Idle), 0f);
        tree.AddChild(Clip(body.Walk), 1f);
        if (Clip(body.Run) != null)
        {
            tree.AddChild(Clip(body.Run), 3.2f);
        }

        machine.defaultState = locomotion;
        if (body.Stride > 0f)
        {
            controller.AddParameter(new AnimatorControllerParameter { name = "StrideRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            locomotion.speedParameter = "StrideRate";
            locomotion.speedParameterActive = true;
        }

        if (Clip(body.Attack) != null)
        {
            AnimatorState attack = machine.AddState("Attack");
            attack.motion = Clip(body.Attack);
            attack.speedParameter = "ActionSpeed";
            attack.speedParameterActive = true;
            AnimatorStateTransition into = machine.AddAnyStateTransition(attack);
            into.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            into.duration = 0.05f;
            into.canTransitionToSelf = false;
            AnimatorStateTransition back = attack.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.1f;
        }

        if (Clip(body.Stagger) != null)
        {
            AnimatorState stagger = machine.AddState("Stagger");
            stagger.motion = Clip(body.Stagger);
            AnimatorStateTransition into = machine.AddAnyStateTransition(stagger);
            into.AddCondition(AnimatorConditionMode.If, 0f, "Stagger");
            into.duration = 0.05f;
            into.canTransitionToSelf = false;
            AnimatorStateTransition back = stagger.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.1f;
        }

        if (Clip(body.Rest) != null)
        {
            controller.AddParameter(MonsterAnimationDriver.RestingParameter, AnimatorControllerParameterType.Bool);
            AnimatorState rest = machine.AddState("Rest");
            rest.motion = Clip(body.Rest);
            AnimatorStateTransition down = locomotion.AddTransition(rest);
            down.hasExitTime = false;
            down.duration = 0.4f;
            down.AddCondition(AnimatorConditionMode.If, 0f, MonsterAnimationDriver.RestingParameter);
            AnimatorStateTransition up = rest.AddTransition(locomotion);
            up.hasExitTime = false;
            up.duration = 0.3f;
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, MonsterAnimationDriver.RestingParameter);
        }

        if (Clip(body.Death) != null)
        {
            AnimatorState death = machine.AddState("Death");
            death.motion = Clip(body.Death);
            AnimatorStateTransition into = machine.AddAnyStateTransition(death);
            into.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            into.duration = 0.05f;
        }

        EditorUtility.SetDirty(controller);
        Built[path] = controller;
        return controller;
    }

    // ----- The Barrow Shieldbearer is a variant of the Draug: its shield hangs on the old body's hand, so it has to be carried across. -----

    private const string ShieldTemp = Root + "_ShieldTemp.prefab";

    private sealed class ShieldCopy
    {
        public Quaternion Rotation;
        public Vector3 FromHand;
        public Vector3 WorldScale;
    }

    private static ShieldCopy KeepShield()
    {
        GameObject root = PrefabUtility.LoadPrefabContents($"{Enemies}BarrowShieldbearer.prefab");
        try
        {
            // The one still on a hand: a re-run can leave strays on the root from the body it replaced.
            Transform shield = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Shield" && t.parent != root.transform)
                ?? FindDeep(root.transform, "Shield");
            if (shield == null)
            {
                return null;
            }

            var copy = new ShieldCopy { Rotation = shield.rotation, FromHand = shield.position - shield.parent.position, WorldScale = shield.lossyScale };
            GameObject clone = UnityEngine.Object.Instantiate(shield.gameObject);
            clone.name = "Shield";
            PrefabUtility.SaveAsPrefabAsset(clone, ShieldTemp);
            UnityEngine.Object.DestroyImmediate(clone);
            return copy;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private const float ShieldAcross = 0.95f;

    private static void FitTo(Transform item, float across)
    {
        // From the mesh and the scale it wears: a renderer's bounds aren't brought up to date inside prefab contents.
        float now = 0f;
        foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>())
        {
            Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.lossyScale);
            now = Mathf.Max(now, Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        if (now > 0.0001f)
        {
            item.localScale *= across / now;
        }
    }

    private static void GiveBackShield(ShieldCopy copy, List<string> log)
    {
        if (copy == null)
        {
            log.Add("No shield found to carry across.");
            return;
        }

        string path = $"{Enemies}BarrowShieldbearer.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (Transform old in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Shield").ToArray())
            {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            Transform model = root.transform.Find("Model");
            Transform hand = FindDeep(model, Rigs["Boneman"]["LeftHand"]);
            var shield = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ShieldTemp));
            PrefabUtility.UnpackPrefabInstance(shield, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            shield.name = "Shield";
            shield.transform.SetParent(hand, true);
            shield.transform.position = hand.position + copy.FromHand;
            shield.transform.rotation = copy.Rotation;
            FitTo(shield.transform, ShieldAcross);
            var pose = new SerializedObject(root.GetComponent<MonsterShieldPose>());
            pose.FindProperty("shield").objectReferenceValue = shield.transform;
            pose.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.Add("Shield carried across.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.DeleteAsset(ShieldTemp);
        }
    }
}
