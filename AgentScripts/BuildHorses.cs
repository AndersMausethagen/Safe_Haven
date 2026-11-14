#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Horses (`unity command script run --file AgentScripts/BuildHorses.cs --entry BuildHorses.Apply`): the Undead Horse &amp;
/// Knight pack's realistic horse made a living one (Art/Wildlife/Horse), with the Horse AnimSet Pro clips it needs (every
/// gait, the idles, eating, drinking, sleeping, rearing, bucking, striking, six hit reactions, five deaths and swimming), an
/// Animator for HorseMotion, the pack's horse sounds, five manes, a textured saddle with its reins, and four barding looks
/// from the pack's armour; the Wild Horse and the bog's Toxic Horse (its own body, gas and barding) with their definitions
/// (Data/Horses); the lasso, three saddles and three bardings with their recipes; which foods a horse eats; HorseSettings
/// (temperaments, the whistle, the bog horse); the Stable (Data/Buildings); and the journal's page on horses. Run
/// BuildProficiencies after it for the Horsemanship skill (its mentor is the Stable). Safe to run again: it replaces what it
/// made.
/// </summary>
public static class BuildHorses
{
    private const string Art = "Assets/_Project/Art/Wildlife/Horse";
    private const string Model = "Assets/Malbers Animations/Horse AnimSet Pro/Undead Horse/Models/Undead_Horse_Re.fbx";
    private const string ControllerPath = Art + "/Horse.controller";
    private const string PrefabPath = "Assets/_Project/Prefabs/World/Living/Wild Horse.prefab";
    // The wild horse's breed since 2026-11-13: the user's pony from LocalSpawn, fitted to the pack's skeleton with its own
    // mane and tail cards (Tools/Blender/horse_hair.py) and HairSway. The toxic horse keeps the pack's undead model.
    private const string Pony = Art + "/Pony/Pony.fbx";
    private const string HairCard = Art + "/Hair/Hair Card";
    private const string ToxicPrefabPath = "Assets/_Project/Prefabs/World/Living/Toxic Horse.prefab";
    private const string Data = "Assets/_Project/Data/Horses";
    private const string Items = "Assets/_Project/Data/Items";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";
    private const string Lit = "Universal Render Pipeline/Lit";
    // Head up, ears to hooves: a horse about 1.6 m at the withers.
    private const float Height = 2.2f;
    // The rider's hips stand 1.02 m above their feet and sit about 0.1 m above the saddle's seat.
    private const float HipHeight = 0.92f;

    private const string Pack = "Assets/Malbers Animations/Horse AnimSet Pro/";
    private const string Clips = Pack + "2 - Animations/Animations Clips/Horse/";
    private const string Audio = Pack + "6 - Audio/Horse/";
    private const string Textures = Pack + "Undead Horse/Materials & Textures/";
    private static readonly string[] Gaits = { "Locomotion/H_Walk", "Locomotion/H_Trot", "Locomotion/H_Canter", "Locomotion/H_Gallop", "Locomotion/H_Sprint" };
    private static readonly string[] Fidgets = { "Idles/H_Idle_02_HeadShake", "Idles/H_Idle_03_Look", "Idles/H_Idle_03_Look_Opp", "Idles/H_Idle_04_Step", "Idles/H_Idle_05_Hop" };
    // HorseMotion.HitSide: front left, front right, left, right, back left, back right.
    private static readonly string[] Hits =
    {
        "Damage/H_Get Hit_Front_Left", "Damage/H_Get Hit_Front_Right", "Damage/H_Get Hit_Left", "Damage/H_Get Hit_Right",
        "Damage/H_Get Hit_Back_Left", "Damage/H_Get Hit_Back_Right",
    };
    // HorseMotion.Die: four standing falls, then the tumble at a gallop.
    private static readonly string[] Deaths = { "Deaths/H_Death01", "Deaths/H_Death01_Opp", "Deaths/H_Death02", "Deaths/H_Death02_Opp", "Deaths/H_Death_Gallop" };
    private static readonly string[] Looping =
    {
        "Idles/H_Idle_01", "Actions/H_Eat", "Actions/H_Drink", "Actions/H_Sleep", "Swim/H_Swim_Idle", "Swim/H_Swim_Forward",
    };
    private static readonly string[] Once = { "Actions/H_Neigh", "Attacks/H_Attack_Back_Legs", "Attacks/H_Attack_Front_Legs",
        "Jumps/H_Jump_Forward", "Jumps/H_Jump_Canter", "Jumps/H_Jump_Gallop", "Jumps/H_Jump_Sprint" };
    // Turning: on the spot, and each gait's own turning to either side (in place, as the straight ones are).
    private static readonly string[] Turning = { "Locomotion/H_Turn_Left", "Locomotion/H_Turn_Right" };

    private static string report = "";

    public static string Apply()
    {
        report = "";
        Folder(Data);
        GameObject model = Import();
        AnimatorController controller = Controller();
        CombatStatsDefinition stats = Stats("Horse Combat Stats", 160f, 15f);
        CombatStatsDefinition toxicStats = Stats("Toxic Horse Combat Stats", 260f, 40f);
        Materials looks = MakeMaterials();
        HorseSounds sounds = MakeSounds();

        ItemDefinition lasso = Item("Lasso", "lasso", "Lasso", "A noose of nettle cord, stiffened with hide. Thrown over a wild horse from close by, it holds it, if you can.",
            ItemCategory.Tool, 2, 12, 1.2f, Load<ItemDefinition>(Items + "/Materials/NettleCord.asset"), so => so.FindProperty("lasso").boolValue = true);
        Recipe("Lasso", "lasso", lasso, CraftingStationType.None, 6f, ("Materials/NettleCord", 4), ("Materials/RoughHide", 1));
        Saddles();
        Bardings();
        Treats();

        HorseDefinition wild = Definition("Wild Horse", lasso, sounds, looks, toxic: false);
        HorseDefinition toxic = Definition("Toxic Horse", lasso, sounds, looks, toxic: true);
        PonyLooks(wild, looks);
        Horse wildPrefab = Prefab(ImportPony(), controller, stats, wild, looks, PrefabPath, toxic: false, pony: true);
        Horse toxicPrefab = Prefab(model, controller, toxicStats, toxic, looks, ToxicPrefabPath, toxic: true);
        Link(wild, wildPrefab);
        Link(toxic, toxicPrefab);
        Settings(wild, toxic, sounds.Whistle);
        string stable = Stable();
        string journal = JournalPage();
        string rider = RiderLayer();
        string monsters = MonsterReactions();

        AssetDatabase.SaveAssets();
        Load<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset").CollectAll();
        AssetDatabase.SaveAssets();
        return $"horses {wildPrefab.name} + {toxicPrefab.name} (seat {wildPrefab.Seat.name}), {controller.name}, gaits {lastGaits} m/s; {stable}; {journal}; {rider}; {monsters}{report}";
    }

    // ----- The model and its clips -----

    private static GameObject Import()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = false;
        importer.SaveAndReimport();
        foreach (string clip in Gaits.Select(g => g + "_IP").Concat(Gaits.Select(g => g + "_Left_IP")).Concat(Gaits.Select(g => g + "_Right_IP")).Concat(Turning).Concat(Looping))
        {
            Loop(Clips + clip + ".FBX", true);
        }

        foreach (string clip in Once.Concat(Fidgets).Concat(Hits).Concat(Deaths))
        {
            Loop(Clips + clip + ".FBX", false);
        }

        Texture(Art + "/HorseCoat.png", 2048);
        // Rare coats' patterns (Tools/Horses/make_coats.py).
        Texture(Art + "/HorseCoat Dapple.png", 2048);
        Texture(Art + "/HorseCoat Pinto.png", 2048);
        foreach (string body in new[] { "Mane", "Toxic" })
        {
            Texture($"{Textures}Realistic/Body/Undead_Horse_{body}_AlbedoTransparency.png", 1024);
            Texture($"{Textures}Realistic/Body/Undead_Horse_{body}_Normal.png", 1024, normal: true);
            Texture($"{Textures}Realistic/Body/Undead_Horse_{body}_MetallicSmoothness.png", 1024);
        }

        Texture($"{Textures}Realistic/Body/Undead_Horse_Toxic_Emission.png", 1024);
        for (int i = 1; i <= 4; i++)
        {
            Texture($"{Textures}Realistic/Armor/Undead_Horse Armor 0{i}_D.png", 1024);
            Texture($"{Textures}Realistic/Armor/Undead_Horse Armor 0{i}_N.png", 1024, normal: true);
            Texture($"{Textures}Realistic/Armor/Undead_Horse Armor 0{i}_E.png", 1024);
        }

        Texture($"{Textures}Realistic/Armor/Undead_Horse Armor 01_MS.png", 1024);
        return Load<GameObject>(Model);
    }

    private static void Loop(string path, bool loop)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            report += $"; no clip {path}";
            return;
        }

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

    private static AnimationCurve RootCurve(AnimationClip clip, string property)
    {
        EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip).FirstOrDefault(b => b.path == "" && b.propertyName == property);
        return binding.propertyName != null ? AnimationUtility.GetEditorCurve(clip, binding) : null;
    }

    private static float RootYaw(AnimationClip clip, float time)
    {
        AnimationCurve x = RootCurve(clip, "RootQ.x"), y = RootCurve(clip, "RootQ.y"), z = RootCurve(clip, "RootQ.z"), w = RootCurve(clip, "RootQ.w");
        return w == null ? 0f : new Quaternion(x.Evaluate(time), y.Evaluate(time), z.Evaluate(time), w.Evaluate(time)).eulerAngles.y;
    }

    // Degrees a second the turn on the spot shows, from its root's yaw.
    private static float TurnRate()
    {
        AnimationClip clip = Clip(Turning[0]);
        if (clip == null || clip.length <= 0f)
        {
            return 84f;
        }

        float turned = 0f;
        for (int i = 0; i < 16; i++)
        {
            turned += Mathf.DeltaAngle(RootYaw(clip, clip.length * i / 16f), RootYaw(clip, clip.length * (i + 1) / 16f));
        }

        return Mathf.Round(Mathf.Abs(turned) / clip.length);
    }

    // The rider's path onto a horse from its left (the pack's Rider_Mount_Left): the body's offset from where it ends in the
    // saddle and its turn, at even steps through the clip (HorseSettings › Mount Path).
    private static (Vector4[] path, float seconds) MountPath()
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(Pack + "2 - Animations/Animations Clips/Rider/Mount Dismount/Rider_Mount_Dismount_Left.FBX")
            .OfType<AnimationClip>().FirstOrDefault(c => c.name == "Rider_Mount_Left");
        AnimationCurve x = clip != null ? RootCurve(clip, "RootT.x") : null;
        if (x == null)
        {
            report += "; no mount path";
            return (System.Array.Empty<Vector4>(), 1.67f);
        }

        AnimationCurve y = RootCurve(clip, "RootT.y"), z = RootCurve(clip, "RootT.z");
        Vector3 end = new Vector3(x.Evaluate(clip.length), y.Evaluate(clip.length), z.Evaluate(clip.length));
        float endYaw = RootYaw(clip, clip.length);
        var path = new Vector4[17];
        for (int i = 0; i < path.Length; i++)
        {
            float time = clip.length * i / (path.Length - 1f);
            Vector3 at = new Vector3(x.Evaluate(time), y.Evaluate(time), z.Evaluate(time)) - end;
            path[i] = new Vector4(at.x, at.y, at.z, Mathf.DeltaAngle(endYaw, RootYaw(clip, time)));
        }

        return (path, clip.length);
    }

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

    // ----- The Animator (HorseMotion's parameters) -----

    private static AnimatorController Controller()
    {
        // Rebuilt in place, never deleted: the knights' steeds and the horse prefabs keep their reference to it.
        AnimatorController controller = Load<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }
        else
        {
            while (controller.parameters.Length > 0)
            {
                controller.RemoveParameter(0);
            }

            for (int i = controller.layers.Length - 1; i > 0; i--)
            {
                controller.RemoveLayer(i);
            }

            AnimatorStateMachine old = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in old.states)
            {
                old.RemoveState(state.state);
            }

            foreach (AnimatorStateTransition transition in old.anyStateTransitions)
            {
                old.RemoveAnyStateTransition(transition);
            }

            foreach (BlendTree orphan in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>())
            {
                AssetDatabase.RemoveObjectFromAsset(orphan);
            }
        }

        controller.AddParameter("Gait", AnimatorControllerParameterType.Float);
        controller.AddParameter("Turn", AnimatorControllerParameterType.Float);
        foreach (string flag in new[] { "Eating", "Drinking", "Sleeping", "Swimming", "Dead" })
        {
            controller.AddParameter(flag, AnimatorControllerParameterType.Bool);
        }

        foreach (string number in new[] { "HitSide", "Fidget", "DeathKind" })
        {
            controller.AddParameter(number, AnimatorControllerParameterType.Int);
        }

        foreach (string trigger in new[] { "Rear", "Buck", "Strike", "Hit", "Fidgeting", "Jump" })
        {
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        // By gait (0 standing ... 5 sprint) and turn (-1 left ... 1 right): standing it steps round on the spot, moving it
        // leans into the turn, so it never slides round with its legs still.
        AnimatorState move = controller.CreateBlendTreeInController("Move", out BlendTree tree);
        tree.blendType = BlendTreeType.FreeformCartesian2D;
        tree.blendParameter = "Gait";
        tree.blendParameterY = "Turn";
        tree.AddChild(Clip("Idles/H_Idle_01"), new Vector2(0f, 0f));
        tree.AddChild(Clip(Turning[0]), new Vector2(0f, -1f));
        tree.AddChild(Clip(Turning[1]), new Vector2(0f, 1f));
        for (int i = 0; i < Gaits.Length; i++)
        {
            tree.AddChild(Clip(Gaits[i] + "_IP"), new Vector2(i + 1, 0f));
            tree.AddChild(Clip(Gaits[i] + "_Left_IP") ?? Clip(Gaits[i] + "_IP"), new Vector2(i + 1, -1f));
            tree.AddChild(Clip(Gaits[i] + "_Right_IP") ?? Clip(Gaits[i] + "_IP"), new Vector2(i + 1, 1f));
        }

        machine.defaultState = move;

        // Swimming: treading water, and swimming on.
        AnimatorState swim = controller.CreateBlendTreeInController("Swim", out BlendTree strokes);
        strokes.blendParameter = "Gait";
        strokes.useAutomaticThresholds = false;
        strokes.AddChild(Clip("Swim/H_Swim_Idle"), 0f);
        strokes.AddChild(Clip("Swim/H_Swim_Forward"), 1f);
        Transition(move, swim, "Swimming", true, 0.3f);
        Transition(swim, move, "Swimming", false, 0.3f);

        foreach ((string state, string clip, string flag) in new[] { ("Eating", "Actions/H_Eat", "Eating"), ("Drinking", "Actions/H_Drink", "Drinking"), ("Sleeping", "Actions/H_Sleep", "Sleeping") })
        {
            AnimatorState held = machine.AddState(state);
            held.motion = Clip(clip);
            Transition(move, held, flag, true, 0.4f);
            Transition(held, move, flag, false, 0.3f);
        }

        // Idle life: a shake of the head, a look round, a step, a hop; from standing only, and back.
        for (int i = 0; i < Fidgets.Length; i++)
        {
            AnimatorState fidget = machine.AddState("Fidget " + i);
            fidget.motion = Clip(Fidgets[i]);
            AnimatorStateTransition to = move.AddTransition(fidget);
            to.AddCondition(AnimatorConditionMode.If, 0f, "Fidgeting");
            to.AddCondition(AnimatorConditionMode.Equals, i, "Fidget");
            to.AddCondition(AnimatorConditionMode.Less, 0.2f, "Gait");
            to.hasExitTime = false;
            to.duration = 0.25f;
            Back(fidget, move, 0.85f);
        }

        // A jump, by the gait it was going at: from a trot, a canter, a gallop or a sprint.
        AnimatorState jump = controller.CreateBlendTreeInController("Jump", out BlendTree leaps);
        leaps.blendParameter = "Gait";
        leaps.useAutomaticThresholds = false;
        leaps.AddChild(Clip("Jumps/H_Jump_Forward"), 2f);
        leaps.AddChild(Clip("Jumps/H_Jump_Canter"), 3f);
        leaps.AddChild(Clip("Jumps/H_Jump_Gallop"), 4f);
        leaps.AddChild(Clip("Jumps/H_Jump_Sprint"), 5f);
        AnimatorStateTransition toJump = machine.AddAnyStateTransition(jump);
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        toJump.duration = 0.08f;
        toJump.canTransitionToSelf = false;
        Back(jump, move, 0.85f);

        // Rearing (the pack's neigh, up on its hind legs), bucking and striking, from anywhere but death.
        Action(machine, move, "Rear", "Actions/H_Neigh", "Rear");
        Action(machine, move, "Buck", "Attacks/H_Attack_Back_Legs", "Buck");
        Action(machine, move, "Strike", "Attacks/H_Attack_Front_Legs", "Strike");

        for (int i = 0; i < Hits.Length; i++)
        {
            AnimatorState hit = machine.AddState("Hit " + i);
            hit.motion = Clip(Hits[i]);
            AnimatorStateTransition to = machine.AddAnyStateTransition(hit);
            to.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            to.AddCondition(AnimatorConditionMode.Equals, i, "HitSide");
            to.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            to.AddCondition(AnimatorConditionMode.IfNot, 0f, "Swimming");
            to.duration = 0.1f;
            to.canTransitionToSelf = false;
            Back(hit, move, 0.8f);
        }

        for (int i = 0; i < Deaths.Length; i++)
        {
            AnimatorState death = machine.AddState("Death " + i);
            death.motion = Clip(Deaths[i]);
            AnimatorStateTransition die = machine.AddAnyStateTransition(death);
            die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            die.AddCondition(AnimatorConditionMode.Equals, i, "DeathKind");
            die.duration = 0.15f;
            die.canTransitionToSelf = false;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void Action(AnimatorStateMachine machine, AnimatorState move, string name, string clip, string trigger)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = Clip(clip);
        AnimatorStateTransition to = machine.AddAnyStateTransition(state);
        to.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        to.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        to.AddCondition(AnimatorConditionMode.IfNot, 0f, "Swimming");
        to.duration = 0.12f;
        to.canTransitionToSelf = false;
        Back(state, move, 0.9f);
    }

    private static void Back(AnimatorState from, AnimatorState to, float exit)
    {
        AnimatorStateTransition back = from.AddTransition(to);
        back.hasExitTime = true;
        back.exitTime = exit;
        back.duration = 0.2f;
    }

    private static void Transition(AnimatorState from, AnimatorState to, string parameter, bool when, float seconds)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(when ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        transition.hasExitTime = false;
        transition.duration = seconds;
    }

    // ----- Monsters and horses -----

    // Wolves go for horses first; the dead and the big frighten them (within these metres).
    private static string MonsterReactions()
    {
        (string monster, float hunts, float frightens)[] reactions =
        {
            ("ForestBittenWolf", 2.5f, 0f), ("Draug", 1f, 12f), ("BarrowShieldbearer", 1f, 10f), ("OldMossTroll", 1f, 18f),
        };
        int set = 0;
        foreach ((string monster, float hunts, float frightens) in reactions)
        {
            var kind = Load<MonsterDefinition>($"Assets/_Project/Data/Monsters/{monster}.asset");
            if (kind == null)
            {
                report += $"; no monster {monster}";
                continue;
            }

            var so = new SerializedObject(kind);
            so.FindProperty("huntsHorses").floatValue = hunts;
            so.FindProperty("frightensHorses").floatValue = frightens;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(kind);
            set++;
        }

        return $"{set} monsters react to horses";
    }

    // ----- The rider (the player's Animator) -----

    private const string RiderClips = Pack + "2 - Animations/Animations Clips/Rider/";

    private static AnimationClip RiderClip(string file, string take = null) =>
        AssetDatabase.LoadAllAssetsAtPath(RiderClips + file + ".FBX").OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (take == null || c.name == take));

    // PlayerRidingAnimation's layer: the pack's rider clips, full body, over everything but the emotes. Its states are
    // played by name; the gaits and swimming follow parameters.
    // The pack's saddle-sword clips were made at another height from its other rider clips (their body 0.06 high, the rest
    // 0.75): played in the saddle they dropped the rider under the horse. Each take is lifted to the riding clips' height.
    private static string LevelSwordClips()
    {
        string path = RiderClips + "Weapons While Riding/Rider_Weapon_Sword.FBX";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        AnimationClip idle = RiderClip("Idles/Rider_Idle_01");
        AnimationCurve idleY = idle != null ? RootCurve(idle, "RootT.y") : null;
        if (importer == null || idleY == null)
        {
            return "no sword clips";
        }

        float target = idleY.Evaluate(0f);
        float Height(string take)
        {
            AnimationClip clip = RiderClip("Weapons While Riding/Rider_Weapon_Sword", take);
            AnimationCurve y = clip != null ? RootCurve(clip, "RootT.y") : null;
            if (y == null)
            {
                return target;
            }

            float sum = 0f;
            for (int i = 0; i <= 8; i++)
            {
                sum += y.Evaluate(clip.length * i / 8f);
            }

            return sum / 9f;
        }

        // The offset doesn't show in the clip's curves (it is applied as the clip plays), so it is set outright from the
        // curves each run, never added to: running this again changes nothing.
        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            // A positive offset lowers the body (seen in Play: +0.7 put the rider on the ground), so lifting it is negative.
            float off = Mathf.Round((Height(clip.name) - target) * 100f) / 100f;
            changed |= !Mathf.Approximately(clip.heightOffset, off) || !clip.keepOriginalPositionXZ;
            clip.heightOffset = off;
            clip.keepOriginalPositionXZ = true;
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        string result = $"sword clips offset {importer.clipAnimations[0].heightOffset:0.00} (riding {target:0.00})";
        return result.Length > 0 ? result : "sword clips level";
    }

    private static string RiderLayer()
    {
        string level = LevelSwordClips();
        report += "; " + level;
        var player = Load<AnimatorController>(CharacterAnimatorBuilder.PlayerControllerPath);
        if (player == null)
        {
            return "no player controller";
        }

        for (int i = player.layers.Length - 1; i >= 0; i--)
        {
            if (player.layers[i].name == PlayerRidingAnimation.LayerName)
            {
                player.RemoveLayer(i);
            }
        }

        foreach ((string name, AnimatorControllerParameterType type) in new[] { ("RideGait", AnimatorControllerParameterType.Float), ("RideSpeed", AnimatorControllerParameterType.Float), ("RideSwim", AnimatorControllerParameterType.Bool) })
        {
            if (player.parameters.All(p => p.name != name))
            {
                player.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = name == "RideSpeed" ? 1f : 0f });
            }
        }

        player.AddLayer(PlayerRidingAnimation.LayerName);
        AnimatorControllerLayer[] layers = player.layers;
        int index = layers.Length - 1;
        AnimatorControllerLayer layer = layers[index];
        layer.defaultWeight = 0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        // RiderIK's pass: hands on the reins, feet in the stirrups.
        layer.iKPass = true;
        layers[index] = layer;
        // The emotes stay on top.
        int emote = System.Array.FindIndex(layers, l => l.name == CharacterEmotes.LayerName);
        if (emote >= 0 && emote < index)
        {
            AnimatorControllerLayer top = layers[emote];
            for (int i = emote; i < index; i++)
            {
                layers[i] = layers[i + 1];
            }

            layers[index] = top;
        }

        player.layers = layers;
        AnimatorStateMachine machine = layer.stateMachine;
        int missing = 0;
        AnimationClip Need(AnimationClip clip)
        {
            missing += clip == null ? 1 : 0;
            return clip;
        }

        AnimatorState ride = player.CreateBlendTreeInController("Ride", out BlendTree gaits, player.layers.ToList().FindIndex(l => l.name == PlayerRidingAnimation.LayerName));
        gaits.blendParameter = "RideGait";
        gaits.useAutomaticThresholds = false;
        string[] gaitClips = { "Idles/Rider_Idle_01", "Locomotion/Rider_Walk", "Locomotion/Rider_Trot", "Locomotion/Rider_Canter", "Locomotion/Rider_Gallop", "Locomotion/Rider_Sprint" };
        for (int i = 0; i < gaitClips.Length; i++)
        {
            gaits.AddChild(Need(RiderClip(gaitClips[i])), i);
        }

        ride.speedParameter = "RideSpeed";
        ride.speedParameterActive = true;
        machine.defaultState = ride;
        int layerIndex = player.layers.ToList().FindIndex(l => l.name == PlayerRidingAnimation.LayerName);
        AnimatorState swim = player.CreateBlendTreeInController("Ride Swim", out BlendTree strokes, layerIndex);
        strokes.blendParameter = "RideGait";
        strokes.useAutomaticThresholds = false;
        strokes.AddChild(Need(RiderClip("Swim/Rider_Swim_Idle")), 0f);
        strokes.AddChild(Need(RiderClip("Swim/Rider_Swim_Forward")), 1f);
        Transition(ride, swim, "RideSwim", true, 0.3f);
        Transition(swim, ride, "RideSwim", false, 0.3f);

        AnimatorState Once(string name, AnimationClip clip, float exit, AnimatorState then)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = Need(clip);
            if (then != null)
            {
                Back(state, then, exit);
            }

            return state;
        }

        Once("Ride Rear", RiderClip("Actions/Rider_Neigh"), 0.9f, ride);
        AnimatorState rideJump = player.CreateBlendTreeInController("Ride Jump", out BlendTree riderLeaps, layerIndex);
        riderLeaps.blendParameter = "RideGait";
        riderLeaps.useAutomaticThresholds = false;
        riderLeaps.AddChild(Need(RiderClip("Jumps/Rider_Jump_Forward")), 2f);
        riderLeaps.AddChild(Need(RiderClip("Jumps/Rider_Jump_Canter")), 3f);
        riderLeaps.AddChild(Need(RiderClip("Jumps/Rider_Jump_Gallop")), 4f);
        riderLeaps.AddChild(Need(RiderClip("Jumps/Rider_Jump_Sprint")), 5f);
        Back(rideJump, ride, 0.85f);
        Once("Ride Buck", RiderClip("Attacks/Rider_Attack_Back_Legs"), 0.9f, ride);
        Once("Ride Strike", RiderClip("Attacks/Rider_Attack_Front_Legs"), 0.9f, ride);
        string[] fidgets = { "Idles/Rider_Idle_HeadShake", "Idles/Rider_Idle03_Look", "Idles/Rider_Idle03_Look", "Idles/Rider_Idle04_Step", "Idles/Rider_Idle_05_Hop" };
        for (int i = 0; i < fidgets.Length; i++)
        {
            Once("Ride Fidget " + i, RiderClip(fidgets[i]), 0.85f, ride);
        }

        string[] hits = { "Front_Left", "Front_Right", "Left", "Right", "Back_Left", "Back_Right" };
        for (int i = 0; i < hits.Length; i++)
        {
            Once("Ride Hit " + i, RiderClip("Damage/Rider_Get Hit_" + hits[i]), 0.8f, ride);
        }

        foreach (string side in new[] { "Left", "Right" })
        {
            Once("Mount " + side, RiderClip("Mount Dismount/Rider_Mount_Dismount_" + side, "Rider_Mount_" + side), 0.95f, ride);
            // Getting off holds its last frame while the layer fades out.
            Once("Dismount " + side, RiderClip("Mount Dismount/Rider_Mount_Dismount_" + side, "Rider_Dismount_" + side), 0f, null);
        }

        Once("Ride Sword Right", RiderClip("Weapons While Riding/Rider_Weapon_Sword", "Sword Attack Right Forward RH"), 0.92f, ride);
        Once("Ride Sword Left", RiderClip("Weapons While Riding/Rider_Weapon_Sword", "Sword Attack Left Forward RH"), 0.92f, ride);
        foreach (string way in new[] { "N", "E", "S", "W" })
        {
            AnimatorState hold = Once($"Ride Bow {way} Hold", RiderClip("Weapons While Riding/Rider_Weapon_Bow", $"Bow Hold {way} 0"), 0f, null);
            Once($"Ride Bow {way} Prepare", RiderClip("Weapons While Riding/Rider_Weapon_Bow", $"Bow Prepare {way}"), 0.95f, hold);
            Once($"Ride Bow {way} Release", RiderClip("Weapons While Riding/Rider_Weapon_Bow", $"Bow Release {way}"), 0.9f, ride);
        }

        EditorUtility.SetDirty(player);
        return $"rider layer: {machine.states.Length} states" + (missing > 0 ? $", {missing} clips missing" : "");
    }

    // ----- Materials -----

    private sealed class Materials
    {
        public Material Coat, Hair, Eyes, Saddle, ToxicBody, ToxicHair, ToxicEyes, Gas;
        public Material[] Manes;
        public Material[] Barding;
        public Material PonyCoat;
        public Material PonySaddle;
        public Material[] PonyManes;
    }

    // The pack's mane colours (its Horse_Mane_D materials), on the mane texture that fits this horse.
    private static readonly (string name, Color colour, float smoothness)[] ManeColours =
    {
        ("Black", new Color(0.05f, 0.045f, 0.04f), 0.5f), ("Brown", new Color(0.29f, 0.145f, 0.098f), 0.42f),
        ("Blond", new Color(0.915f, 0.72f, 0.47f), 0.34f), ("Gray", new Color(0.547f, 0.547f, 0.547f), 0.36f),
        ("White", Color.white, 0.32f),
    };

    private static readonly string[] BardingNames = { "Crimson", "Blackened", "Silvered", "Bone" };

    private static Materials MakeMaterials()
    {
        Texture2D Tex(string path) => Load<Texture2D>(path);
        string body = Textures + "Realistic/Body/";
        string armour = Textures + "Realistic/Armor/Undead_Horse Armor 0";
        var made = new Materials
        {
            Coat = Material("Main", Tex(Art + "/HorseCoat.png"), Color.white, 0.22f),
            Eyes = Material("Eye", null, new Color(0.03f, 0.025f, 0.02f), 0.85f),
            Manes = ManeColours.Select(m => Material("Mane " + m.name, Tex(body + "Undead_Horse_Mane_AlbedoTransparency.png"), m.colour, m.smoothness,
                Tex(body + "Undead_Horse_Mane_Normal.png"), cutout: true)).ToArray(),
        };
        // The default (the prefab's own) is brown; each horse wears one of the five (Horse).
        made.Hair = made.Manes[1];
        // The saddle and reins: the pack's leather and studs (its first armour sheet, whose metal is in the MS map's red).
        made.Saddle = Material("Saddle", Tex(armour + "1_D.png"), Color.white, 0.4f, Tex(armour + "1_N.png"));
        Metal(made.Saddle, Tex(armour + "1_MS.png"));
        made.Barding = new Material[4];
        for (int i = 0; i < 4; i++)
        {
            Material barding = Material("Barding " + BardingNames[i], Tex(armour + (i + 1) + "_D.png"), Color.white, 0.45f, Tex(armour + (i + 1) + "_N.png"));
            barding.SetFloat("_Metallic", 0.55f);
            // The bone look is the toxic horse's own, and glows as its body does; the player's three don't.
            Emission(barding, i == 3 ? Tex(armour + "4_E.png") : null, new Color(0.45f, 1f, 0.35f) * 1.6f);
            made.Barding[i] = barding;
        }

        made.ToxicBody = Material("Toxic Body", Tex(body + "Undead_Horse_Toxic_AlbedoTransparency.png"), Color.white, 0.4f, Tex(body + "Undead_Horse_Toxic_Normal.png"));
        Metal(made.ToxicBody, Tex(body + "Undead_Horse_Toxic_MetallicSmoothness.png"));
        Emission(made.ToxicBody, Tex(body + "Undead_Horse_Toxic_Emission.png"), new Color(0.45f, 1f, 0.3f) * 2f);
        made.ToxicHair = Material("Toxic Mane", Tex(body + "Undead_Horse_Mane_AlbedoTransparency.png"), new Color(0.2f, 0.28f, 0.14f), 0.3f,
            Tex(body + "Undead_Horse_Mane_Normal.png"), cutout: true);
        made.ToxicEyes = Material("Toxic Eye", null, new Color(0.3f, 0.9f, 0.2f), 0.9f);
        // The pony: its generated texture made neutral (Tools/Horses/make_coats.py Pony) for the coat tints, matte (it read
        // far too shiny with the generator's own roughness), and manes of the same five colours on the hair cards.
        // The pony's own saddle (a LocalSpawn western saddle fitted in Blender): leather, matte.
        made.PonySaddle = Material("Pony Saddle", Tex(Art + "/Pony/Pony Saddle_Albedo.png"), Color.white, 0.3f, Tex(Art + "/Pony/Pony Saddle_Normal.png"));
        made.PonyCoat = Material("Pony Main", Tex(Art + "/Pony/PonyCoat.png"), Color.white, 0.18f, Tex(Art + "/Pony/Pony_Normal.png"));
        // Matte: at the pack mane's 0.3-0.5 smoothness the cards caught the sky and a black mane read pale blue-grey.
        made.PonyManes = ManeColours.Select(m => Material("Pony Mane " + m.name, Tex(HairCard + "_Albedo.png"), m.colour, 0.12f,
            Tex(HairCard + "_Normal.png"), cutout: true)).ToArray();
        Emission(made.ToxicEyes, null, new Color(0.4f, 1f, 0.25f) * 4f);
        made.Gas = Gas();
        return made;
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
        material.SetFloat("_Metallic", 0f);
        material.SetTexture("_MetallicGlossMap", null);
        material.DisableKeyword("_METALLICSPECGLOSSMAP");
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

        Emission(material, null, Color.black);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Metal(Material material, Texture2D map)
    {
        if (map == null)
        {
            return;
        }

        material.SetTexture("_MetallicGlossMap", map);
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 0.6f);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
    }

    private static void Emission(Material material, Texture2D map, Color colour)
    {
        bool glows = colour.maxColorComponent > 0.001f;
        material.SetTexture("_EmissionMap", map);
        material.SetColor("_EmissionColor", glows ? colour : Color.black);
        material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        if (glows)
        {
            material.EnableKeyword("_EMISSION");
        }
        else
        {
            material.DisableKeyword("_EMISSION");
        }
    }

    // The toxic horse's breath of gas: the Haven's hearth smoke (a URP particle material), sickly green. The pack's own gas
    // materials are built-in particle shaders without their textures, pink in URP.
    private static Material Gas()
    {
        string path = $"{Art}/Horse Toxic Gas.mat";
        var smoke = Load<Material>("Assets/_Project/Art/Effects/Hearth Smoke.mat");
        Material gas = Load<Material>(path);
        if (gas == null)
        {
            gas = new Material(smoke);
            AssetDatabase.CreateAsset(gas, path);
        }
        else
        {
            gas.CopyPropertiesFromMaterial(smoke);
        }

        foreach (string colour in new[] { "_BaseColor", "_Color", "_TintColor" })
        {
            if (gas.HasProperty(colour))
            {
                gas.SetColor(colour, new Color(0.45f, 0.75f, 0.25f, 0.45f));
            }
        }

        EditorUtility.SetDirty(gas);
        return gas;
    }

    // ----- Sounds (the pack's) -----

    private sealed class HorseSounds
    {
        public SoundDefinition Whinny, Neigh, Snort, Breathing, Hooves, Whistle, HoovesStone, HoovesWood, HoovesWater;
    }

    private static HorseSounds MakeSounds() => new HorseSounds
    {
        Whinny = Sound("Horse Whinny", 0.8f, SoundPriority.Normal, 6f, 55f, 3, "horse-whinny_01", "horse-whinny_02", "horse-whinny_03"),
        Neigh = Sound("Horse Neigh", 0.85f, SoundPriority.Normal, 8f, 70f, 2, "Neigh1", "Neigh2", "Neigh3"),
        Snort = Sound("Horse Snort", 0.55f, SoundPriority.Background, 3f, 22f, 2, "Horse HeadShake", "Horse Nose Sound"),
        Breathing = Sound("Horse Breathing", 0.5f, SoundPriority.Background, 3f, 18f, 2, "Horse Breathing", "Horse Breathing_01", "Horse Breathing_02"),
        Hooves = Sound("Horse Hooves", 0.32f, SoundPriority.Background, 4f, 30f, 6, "Horse Step_01", "Horse Step_02", "Horse Step_03", "Horse Step_04", "Horse Step_05"),
        Whistle = Sound("Horse Whistle", 0.7f, SoundPriority.Normal, 10f, 60f, 1, "Whistle"),
        // On other ground: made from the pack's steps by Tools/Audio/horse_hooves.py.
        HoovesStone = Sound("Horse Hooves Stone", 0.55f, SoundPriority.Background, 4f, 30f, 6, OwnHooves("Stone")),
        HoovesWood = Sound("Horse Hooves Wood", 0.3f, SoundPriority.Background, 4f, 30f, 6, OwnHooves("Wood")),
        HoovesWater = Sound("Horse Hooves Water", 0.32f, SoundPriority.Background, 4f, 28f, 6, OwnHooves("Water")),
    };

    // "#" marks a clip of the game's own sound folder (Audio/SFX/Horse) rather than the pack's.
    private static string[] OwnHooves(string kind) => Enumerable.Range(1, 4).Select(i => $"#Horse Hoof {kind} {i}").ToArray();

    private static SoundDefinition Sound(string name, float volume, SoundPriority priority, float full, float audible, int atOnce, params string[] clips)
    {
        string path = Sounds + name + ".asset";
        var sound = Load<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var so = new SerializedObject(sound);
        so.FindProperty("displayName").stringValue = name;
        SerializedProperty list = so.FindProperty("clips");
        var found = clips.Select(c => Load<AudioClip>(c.StartsWith("#") ? "Assets/_Project/Audio/SFX/Horse/" + c.Substring(1) + ".wav" : Audio + c + ".wav"))
            .Where(c => c != null).ToList();
        if (found.Count < clips.Length)
        {
            report += $"; {name}: {clips.Length - found.Count} clips missing";
        }

        list.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }

        so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("volumeVariation").floatValue = 0.1f;
        so.FindProperty("pitchRange").vector2Value = new Vector2(0.93f, 1.07f);
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = full;
        so.FindProperty("audibleDistance").floatValue = audible;
        so.FindProperty("maxAtOnce").intValue = atOnce;
        so.FindProperty("priority").intValue = (int)priority;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    // ----- Data -----

    private static CombatStatsDefinition Stats(string file, float health, float armour)
    {
        string path = $"{Data}/{file}.asset";
        CombatStatsDefinition stats = Load<CombatStatsDefinition>(path);
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<CombatStatsDefinition>();
            AssetDatabase.CreateAsset(stats, path);
        }

        // Max health (the horse's own roll replaces it) and a hide's armour. (Once two crit stats by mistake.)
        var so = new SerializedObject(stats);
        SerializedProperty list = so.FindProperty("stats");
        (StatType stat, float value)[] values = { (StatType.MaxHealth, health), (StatType.Armor, armour) };
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue = (int)values[i].stat;
            list.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = values[i].value;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return stats;
    }

    private static HorseDefinition Definition(string file, ItemDefinition lasso, HorseSounds sounds, Materials looks, bool toxic)
    {
        string path = $"{Data}/{file}.asset";
        HorseDefinition definition = Load<HorseDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<HorseDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        var so = new SerializedObject(definition);
        so.FindProperty("lasso").objectReferenceValue = lasso;
        so.FindProperty("barley").objectReferenceValue = Load<ItemDefinition>(Items + "/Materials/Barley.asset");
        so.FindProperty("whinny").objectReferenceValue = sounds.Whinny;
        so.FindProperty("neigh").objectReferenceValue = sounds.Neigh;
        so.FindProperty("snort").objectReferenceValue = sounds.Snort;
        so.FindProperty("breathing").objectReferenceValue = sounds.Breathing;
        so.FindProperty("hooves").objectReferenceValue = sounds.Hooves;
        so.FindProperty("hoovesStone").objectReferenceValue = sounds.HoovesStone;
        so.FindProperty("hoovesWood").objectReferenceValue = sounds.HoovesWood;
        so.FindProperty("hoovesWater").objectReferenceValue = sounds.HoovesWater;
        // A quiet player may come within 4 m; walking (5 m footsteps) it bolts at about 10 m, sneaking at about 6 m, so the
        // lasso (9 m) is thrown from a crouch.
        so.FindProperty("shyDistance").floatValue = toxic ? 4.5f : 4f;
        so.FindProperty("perHeardMetre").floatValue = 1.3f;
        so.FindProperty("lassoRange").floatValue = 9f;
        so.FindProperty("pullStrain").floatValue = toxic ? 0.42f : 0.35f;
        so.FindProperty("tapHold").floatValue = 0.1f;
        so.FindProperty("tapDrain").floatValue = 3f;
        so.FindProperty("struggleStamina").floatValue = toxic ? 140f : 100f;
        so.FindProperty("trotSpeed").floatValue = 6f;
        // A tap of Space picks a canter, free of stamina, between the trot and the gallop.
        so.FindProperty("canterSpeed").floatValue = toxic ? 9.5f : 8.5f;
        so.FindProperty("abilities").intValue = toxic ? (int)(HorseAbility.BogStrider | HorseAbility.PoisonProof | HorseAbility.NightGlow) : 0;
        so.FindProperty("strikeDamage").floatValue = toxic ? 26f : 16f;
        // The breaking ride: barley every 6-14 seconds, 2-4 times (3-5 for the toxic horse), so it is over in about half a
        // minute; and a spent horse waits 12 seconds for the player to walk up to it.
        so.FindProperty("feedInterval").vector2Value = toxic ? new Vector2(7f, 15f) : new Vector2(6f, 14f);
        so.FindProperty("feedsNeeded").vector2IntValue = toxic ? new Vector2Int(3, 5) : new Vector2Int(2, 4);
        so.FindProperty("mountSeconds").floatValue = 12f;
        if (toxic)
        {
            so.FindProperty("displayName").stringValue = "Toxic Horse";
            so.FindProperty("description").stringValue = "A horse of the bog, its hide grown through with something green that glows, its breath a fog. It comes out at dusk, and it is stronger and faster than any other.";
            so.FindProperty("health").vector2Value = new Vector2(220f, 300f);
            so.FindProperty("stamina").vector2Value = new Vector2(120f, 160f);
            so.FindProperty("gallopSpeed").vector2Value = new Vector2(11.5f, 13.5f);
            so.FindProperty("carryWeight").vector2Value = new Vector2(70f, 100f);
            so.FindProperty("home").intValue = (int)HorseHome.Bog;
            so.FindProperty("special").boolValue = true;
            so.FindProperty("alwaysArmoured").boolValue = true;
            so.FindProperty("spawnChancePerHour").floatValue = 0.05f;
            so.FindProperty("mostWild").intValue = 1;
            // Out at dusk and through the night, until a little after dawn.
            so.FindProperty("activeHours").vector2Value = new Vector2(18f, 7f);
            HomeFromRecipe(so);
            so.FindProperty("coats").arraySize = 0;
            SetMaterials(so.FindProperty("manes"), new[] { looks.ToxicHair });
            so.FindProperty("coatManes").arraySize = 0;
        }
        else
        {
            // Bay, chestnut, dun, grey, black: the colours of northern horses; each with the manes such a horse has.
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

            SetMaterials(so.FindProperty("manes"), looks.Manes);
            // Rare coats: palomino (gold, a white mane), silver dapple (the dapple pattern, grey), pinto (bay with white
            // patches, coloured in, so a white tint), cremello (cream, a white mane). One wild horse in about sixteen.
            (string name, Color tint, string pattern, int mane)[] rare =
            {
                ("Palomino", new Color(0.86f, 0.66f, 0.36f), null, 4), ("Silver Dapple", new Color(0.66f, 0.66f, 0.68f), "Dapple", 4),
                ("Pinto", Color.white, "Pinto", -1), ("Cremello", new Color(0.95f, 0.9f, 0.78f), null, 4),
            };
            SerializedProperty rareList = so.FindProperty("rareCoats");
            rareList.arraySize = rare.Length;
            for (int i = 0; i < rare.Length; i++)
            {
                SerializedProperty coat = rareList.GetArrayElementAtIndex(i);
                coat.FindPropertyRelative("name").stringValue = rare[i].name;
                coat.FindPropertyRelative("tint").colorValue = rare[i].tint;
                coat.FindPropertyRelative("pattern").objectReferenceValue = rare[i].pattern != null ? Load<Texture2D>($"{Art}/HorseCoat {rare[i].pattern}.png") : null;
                coat.FindPropertyRelative("mane").intValue = rare[i].mane;
            }

            so.FindProperty("rareCoatChance").floatValue = 0.06f;
            // Manes: 0 black, 1 brown, 2 blond, 3 grey, 4 white.
            string[] fits = { "0,1", "1,2", "0,1,2", "3,4", "0" };
            SerializedProperty manes = so.FindProperty("coatManes");
            manes.arraySize = fits.Length;
            for (int i = 0; i < fits.Length; i++)
            {
                manes.GetArrayElementAtIndex(i).stringValue = fits[i];
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    // The bog where the toxic horse lives, from the world's recipe (HavenWorldRecipe › bogAt, bogRadius).
    private static void HomeFromRecipe(SerializedObject so)
    {
        string guid = AssetDatabase.FindAssets("t:HavenWorldRecipe").FirstOrDefault();
        var recipe = guid != null ? Load<HavenWorldRecipe>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        if (recipe == null)
        {
            report += "; no world recipe for the bog";
            return;
        }

        so.FindProperty("homeAt").vector2Value = recipe.bogAt;
        so.FindProperty("homeRadius").floatValue = recipe.bogRadius;
    }

    private static void SetMaterials(SerializedProperty list, Material[] materials)
    {
        list.arraySize = materials.Length;
        for (int i = 0; i < materials.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
        }
    }

    // The wild horse's looks as a pony: its manes on the hair cards, its dapple and pinto from its own coat.
    private static void PonyLooks(HorseDefinition wild, Materials looks)
    {
        var so = new SerializedObject(wild);
        SetMaterials(so.FindProperty("manes"), looks.PonyManes);
        SerializedProperty rare = so.FindProperty("rareCoats");
        for (int i = 0; i < rare.arraySize; i++)
        {
            SerializedProperty coat = rare.GetArrayElementAtIndex(i);
            var pattern = coat.FindPropertyRelative("pattern").objectReferenceValue as Texture2D;
            if (pattern != null)
            {
                string kind = pattern.name.Replace("HorseCoat ", "").Replace("PonyCoat ", "");
                coat.FindPropertyRelative("pattern").objectReferenceValue = Load<Texture2D>($"{Art}/Pony/PonyCoat {kind}.png");
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wild);
    }

    private static GameObject ImportPony()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Pony);
        var pack = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = false;
        importer.globalScale = pack.globalScale;
        importer.useFileScale = pack.useFileScale;
        importer.SaveAndReimport();
        foreach (string coat in new[] { "PonyCoat", "PonyCoat Dapple", "PonyCoat Pinto" }) Texture($"{Art}/Pony/{coat}.png", 2048);
        Texture(Art + "/Pony/Pony_Normal.png", 2048, normal: true);
        Texture(HairCard + "_Albedo.png", 1024);
        Texture(HairCard + "_Normal.png", 1024, normal: true);
        var albedo = (TextureImporter)AssetImporter.GetAtPath(HairCard + "_Albedo.png");
        // The cards are cut out at the material's 0.45: without coverage-preserving mips the smaller mips average the
        // strands away and a mane thins to a few strands at a distance.
        if (albedo != null && (!albedo.alphaIsTransparency || !albedo.mipMapsPreserveCoverage || !Mathf.Approximately(albedo.alphaTestReferenceValue, 0.45f)))
        {
            albedo.alphaIsTransparency = true;
            albedo.mipMapsPreserveCoverage = true;
            albedo.alphaTestReferenceValue = 0.45f;
            albedo.SaveAndReimport();
        }

        Texture(Art + "/Pony/Pony Saddle_Albedo.png", 2048);
        Texture(Art + "/Pony/Pony Saddle_Normal.png", 2048, normal: true);

        return Load<GameObject>(Pony);
    }

    /// <summary>
    /// Only the two horses' prefabs and looks (`--entry BuildHorses.Breeds`): the wild horse on the pony, the toxic horse on
    /// the pack's undead model. Leaves the controller, items, stable, journal and the rest as Apply made them.
    /// </summary>
    public static string Breeds()
    {
        report = "";
        GameObject model = Import();
        GameObject pony = ImportPony();
        AnimatorController controller = Load<AnimatorController>(ControllerPath);
        CombatStatsDefinition stats = Load<CombatStatsDefinition>($"{Data}/Horse Combat Stats.asset");
        CombatStatsDefinition toxicStats = Load<CombatStatsDefinition>($"{Data}/Toxic Horse Combat Stats.asset");
        Materials looks = MakeMaterials();
        HorseDefinition wild = Load<HorseDefinition>($"{Data}/Wild Horse.asset");
        HorseDefinition toxic = Load<HorseDefinition>($"{Data}/Toxic Horse.asset");
        PonyLooks(wild, looks);
        Horse wildPrefab = Prefab(pony, controller, stats, wild, looks, PrefabPath, toxic: false, pony: true);
        Horse toxicPrefab = Prefab(model, controller, toxicStats, toxic, looks, ToxicPrefabPath, toxic: true);
        Link(wild, wildPrefab);
        Link(toxic, toxicPrefab);
        AssetDatabase.SaveAssets();
        return $"horses {wildPrefab.name} (pony, sway {wildPrefab.GetComponentInChildren<HairSway>()?.ChainCount}) + {toxicPrefab.name}, gaits {lastGaits} m/s{report}";
    }

    private static void Link(HorseDefinition definition, Horse prefab)
    {
        var data = new SerializedObject(definition);
        data.FindProperty("prefab").objectReferenceValue = prefab;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Saddles()
    {
        ItemDefinition hide = Load<ItemDefinition>(Items + "/HideRucksack.asset");
        ItemDefinition saddle = Item("HideSaddle", "hide_saddle", "Hide Saddle", "Ten hides stitched over a frame, with bags either side. On a tamed horse it carries eight stacks.",
            ItemCategory.Equipment, 2, 40, 6f, hide, so => so.FindProperty("saddleSlots").intValue = 8);
        Recipe("HideSaddle", "hide_saddle", saddle, CraftingStationType.Workbench, 14f, ("Materials/RoughHide", 10));
        ItemDefinition leather = Item("LeatherSaddle", "leather_saddle", "Leather Saddle", "Cured leather on a bent-wood tree, with deep bags and straps for more. On a tamed horse it carries twelve stacks.",
            ItemCategory.Equipment, 3, 90, 7f, hide, so => so.FindProperty("saddleSlots").intValue = 12);
        Recipe("LeatherSaddle", "leather_saddle", leather, CraftingStationType.Tannery, 20f, ("Materials/CuredLeather", 6), ("Materials/RoughHide", 4), ("Materials/NettleCord", 3));
        ItemDefinition reinforced = Item("ReinforcedSaddle", "reinforced_saddle", "Reinforced Saddle", "Thick hide over leather, iron at every buckle, panniers either side. On a tamed horse it carries sixteen stacks.",
            ItemCategory.Equipment, 4, 180, 9f, hide, so => so.FindProperty("saddleSlots").intValue = 16);
        Recipe("ReinforcedSaddle", "reinforced_saddle", reinforced, CraftingStationType.Anvil, 26f, ("Materials/CuredLeather", 8), ("Materials/ThickHide", 3), ("Materials/IronIngot", 2));
    }

    // Horse armour, in the pack's colourings: painted plate, blackened iron, silvered steel.
    private static void Bardings()
    {
        ItemDefinition icon = Load<ItemDefinition>(Items + "/Armor/IronHauberk.asset") ?? Load<ItemDefinition>(Items + "/HideRucksack.asset");
        (string file, string id, string name, string text, int tier, int value, float armour, int look, (string, int)[] cost)[] bardings =
        {
            ("PaintedBarding", "painted_barding", "Painted Barding", "Iron plates over a quilted coat, painted red with old signs. On a tamed horse it keeps a fifth of every blow off.",
                3, 120, 0.2f, 0, new[] { ("Materials/IronIngot", 6), ("Materials/CuredLeather", 4), ("Materials/Linen", 2) }),
            ("BlackenedBarding", "blackened_barding", "Blackened Barding", "Iron blackened in the fire and riveted over mail. On a tamed horse it keeps a quarter of every blow off.",
                4, 200, 0.25f, 1, new[] { ("Materials/IronIngot", 9), ("Materials/Charcoal", 4), ("Materials/CuredLeather", 4) }),
            ("SilveredBarding", "silvered_barding", "Silvered Barding", "Moonsilver over steel, bright as frost. On a tamed horse it keeps a third of every blow off.",
                5, 360, 0.33f, 2, new[] { ("Materials/MoonsilverIngot", 3), ("Materials/IronIngot", 6), ("Materials/CuredLeather", 4) }),
        };
        foreach (var barding in bardings)
        {
            ItemDefinition item = Item(barding.file, barding.id, barding.name, barding.text, ItemCategory.Equipment, barding.tier, barding.value, 14f, icon, so =>
            {
                so.FindProperty("horseArmour").floatValue = barding.armour;
                so.FindProperty("bardingLook").intValue = barding.look;
            });
            Recipe(barding.file, barding.id, item, CraftingStationType.Anvil, 30f, barding.cost);
        }
    }

    // What a horse eats from the hand, and the bond it gives.
    private static void Treats()
    {
        foreach ((string item, float bond) in new[] { ("Materials/Barley", 4f), ("Materials/WildTurnip", 5f), ("Materials/Berries", 2f), ("Materials/Cloudberries", 3f), ("Food/BarleyBread", 7f) })
        {
            ItemDefinition food = Load<ItemDefinition>($"{Items}/{item}.asset");
            if (food == null)
            {
                report += $"; no {item}";
                continue;
            }

            var so = new SerializedObject(food);
            so.FindProperty("horseTreat").floatValue = bond;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(food);
        }
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
            ItemDefinition needed = Load<ItemDefinition>($"{Items}/{cost[i].item}.asset");
            if (needed == null)
            {
                report += $"; {file} needs a missing {cost[i].item}";
            }

            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = needed;
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static void Settings(HorseDefinition wild, HorseDefinition toxic, SoundDefinition whistle)
    {
        string path = "Assets/_Project/Resources/HorseSettings.asset";
        HorseSettings settings = Load<HorseSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<HorseSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        var so = new SerializedObject(settings);
        so.FindProperty("wildHorse").objectReferenceValue = wild;
        SerializedProperty special = so.FindProperty("specialHorses");
        special.arraySize = 1;
        special.GetArrayElementAtIndex(0).objectReferenceValue = toxic;
        so.FindProperty("whistle").objectReferenceValue = whistle;
        (Vector4[] mountPath, float mountSeconds) = MountPath();
        SerializedProperty points = so.FindProperty("mountPath");
        points.arraySize = mountPath.Length;
        for (int i = 0; i < mountPath.Length; i++)
        {
            points.GetArrayElementAtIndex(i).vector4Value = mountPath[i];
        }

        so.FindProperty("mountSeconds").floatValue = mountSeconds;
        // The horse menu opens from 2.6 m: getting on works from where it opens.
        so.FindProperty("mountReach").floatValue = 2.8f;
        // The rope: a light shake as it strains, a jolt when it turns or rears, a horse that drags a player who lets it.
        so.FindProperty("ropeShake").floatValue = 0.15f;
        so.FindProperty("ropeJolt").floatValue = 0.26f;
        so.FindProperty("dragSpeed").floatValue = 2.6f;
        // Work at the Haven: beside the farm plots (ploughing), the sawbench (hauling timber), the wainwright's yard.
        (string name, string text, CraftingStationType station, float bonus, float reach)[] jobs =
        {
            ("Plough the fields", "It pulls the plough between the farm plots", CraftingStationType.Farm, 0.35f, 16f),
            ("Haul timber", "It drags logs up to the sawbench", CraftingStationType.Sawbench, 0.3f, 14f),
            ("Haul for the wainwright", "It pulls the wainwright's loads", CraftingStationType.Wainwright, 0.25f, 14f),
        };
        SerializedProperty jobList = so.FindProperty("jobs");
        jobList.arraySize = jobs.Length;
        for (int i = 0; i < jobs.Length; i++)
        {
            SerializedProperty job = jobList.GetArrayElementAtIndex(i);
            job.FindPropertyRelative("name").stringValue = jobs[i].name;
            job.FindPropertyRelative("description").stringValue = jobs[i].text;
            job.FindPropertyRelative("station").intValue = (int)jobs[i].station;
            job.FindPropertyRelative("bonus").floatValue = jobs[i].bonus;
            job.FindPropertyRelative("reach").floatValue = jobs[i].reach;
        }
        // Grounds, by a word in the Haven's and the raids' terrain layers: paths quick, mud and bog slow, rock and forest floor a
        // little slow.
        (string word, float speed, bool soft)[] grounds =
        {
            ("Path", 1.15f, false), ("Gravel", 1.1f, false), ("Road", 1.15f, false), ("Muddy", 0.72f, true), ("Mud", 0.72f, true),
            ("Bog", 0.68f, true), ("Damp", 0.82f, true), ("Bank", 0.85f, true), ("Cliff", 0.8f, false), ("Rock", 0.88f, false),
            ("Litter", 0.92f, false), ("Humus", 0.92f, false), ("Snow", 0.8f, true),
        };
        SerializedProperty groundList = so.FindProperty("grounds");
        groundList.arraySize = grounds.Length;
        for (int i = 0; i < grounds.Length; i++)
        {
            SerializedProperty ground = groundList.GetArrayElementAtIndex(i);
            ground.FindPropertyRelative("word").stringValue = grounds[i].word;
            ground.FindPropertyRelative("speed").floatValue = grounds[i].speed;
            ground.FindPropertyRelative("soft").boolValue = grounds[i].soft;
        }
        // How often each is born, how near it lets you come, how hard it pulls, how much fight it has, feeds more or fewer,
        // how hard it bucks, how long it runs when hurt.
        (HorseTemperament kind, float weight, float shy, float pull, float fight, int feeds, float bucking, float panic, string text)[] natures =
        {
            (HorseTemperament.Calm, 3f, 0.75f, 0.8f, 0.8f, -1, 0.65f, 0.7f, "Lets you near, gives in sooner and runs less far."),
            (HorseTemperament.Skittish, 2f, 1.4f, 0.9f, 0.85f, 0, 1.25f, 1.5f, "Bolts at a sound, bucks hard and runs far; but tires soon on the rope."),
            (HorseTemperament.Stubborn, 2f, 1f, 1.25f, 1.35f, 1, 1f, 0.9f, "Pulls hard, fights long and takes more feeding to break."),
            (HorseTemperament.Bold, 1.5f, 0.8f, 1.1f, 1.1f, 0, 0.9f, 0.45f, "Stands its ground: hurt, it strikes back instead of running."),
        };
        SerializedProperty list = so.FindProperty("temperaments");
        list.arraySize = natures.Length;
        for (int i = 0; i < natures.Length; i++)
        {
            SerializedProperty rules = list.GetArrayElementAtIndex(i);
            rules.FindPropertyRelative("temperament").intValue = (int)natures[i].kind;
            rules.FindPropertyRelative("weight").floatValue = natures[i].weight;
            rules.FindPropertyRelative("shyness").floatValue = natures[i].shy;
            rules.FindPropertyRelative("pull").floatValue = natures[i].pull;
            rules.FindPropertyRelative("fight").floatValue = natures[i].fight;
            rules.FindPropertyRelative("feeds").intValue = natures[i].feeds;
            rules.FindPropertyRelative("bucking").floatValue = natures[i].bucking;
            rules.FindPropertyRelative("panic").floatValue = natures[i].panic;
            rules.FindPropertyRelative("description").stringValue = natures[i].text;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    // ----- The prefabs -----

    private static Horse Prefab(GameObject model, AnimatorController controller, CombatStatsDefinition stats, HorseDefinition definition, Materials looks,
        string prefabPath, bool toxic, bool pony = false)
    {
        var root = new GameObject(definition.DisplayName);
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.name = "Model";
            Transform Part(string name) => body.transform.Find(name);
            Renderer Drawn(string name) => Part(name) != null ? Part(name).GetComponent<Renderer>() : null;
            void Wear(string part, Material material)
            {
                Renderer drawn = Drawn(part);
                if (drawn != null)
                {
                    drawn.sharedMaterials = Enumerable.Repeat(material, drawn.sharedMaterials.Length).ToArray();
                    drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }

            if (pony)
            {
                Wear("Pony", looks.PonyCoat);
                Wear("Pony Hair", looks.PonyManes[1]);
                if (Drawn("Pony Hair") != null) Drawn("Pony Hair").shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // The pack's clips set bones from its own model's defaults (the walk keys no Pelvis position): the pony's
                // skeleton takes the pack model's default pose, bone for bone (its bind poses are the same).
                var pack = Load<GameObject>(Model).GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
                foreach (Transform bone in body.GetComponentsInChildren<Transform>(true))
                {
                    if (bone != body.transform && bone.GetComponent<Renderer>() == null && pack.TryGetValue(bone.name, out Transform from))
                    {
                        bone.localPosition = from.localPosition;
                        bone.localRotation = from.localRotation;
                        bone.localScale = from.localScale;
                    }
                }
            }
            else
            {
                Wear("Horse_Undead_Body_Re", toxic ? looks.ToxicBody : looks.Coat);
                Wear("Horse_Undead_Mane_Re", toxic ? looks.ToxicHair : looks.Hair);
                Wear("Horse_Undead_Tail_Re", toxic ? looks.ToxicHair : looks.Hair);
                Wear("Horse_Undead_Eyes_Re", toxic ? looks.ToxicEyes : looks.Eyes);
            }

            // The pony wears its own saddle (fitted to its back); the toxic horse the pack's.
            string saddleName = pony ? "Pony Saddle" : "Horse_Undead_Saddle_Re";
            Wear(saddleName, pony ? looks.PonySaddle : looks.Saddle);
            Wear("Horse_Undead_Reins_Re", looks.Saddle);
            Wear("Horse_Undead_ReinsHead_Re", looks.Saddle);
            Wear("Horse_Undead_Armor_Re", toxic ? looks.Barding[3] : looks.Barding[0]);

            // Measured with the armour and tack off, as a living horse stands.
            Transform saddleModel = Part(saddleName);
            GameObject[] tack = new[] { "Horse_Undead_Reins_Re", "Horse_Undead_ReinsHead_Re" }.Select(Part).Where(t => t != null).Select(t => t.gameObject).ToArray();
            Renderer armour = Drawn("Horse_Undead_Armor_Re");
            foreach (GameObject part in tack.Append(armour != null ? armour.gameObject : null).Where(p => p != null))
            {
                part.SetActive(false);
            }

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
            // Its agent turns it no faster than it can step round (HorseMotion's turning clips).
            agent.angularSpeed = 140f;
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

            // The toxic horse wears its own barding and bridle from birth, and its breath is a green fog.
            if (toxic)
            {
                foreach (GameObject part in tack.Append(armour != null ? armour.gameObject : null).Where(p => p != null))
                {
                    part.SetActive(true);
                }

                Fog(body, looks.Gas);
                // It glows at night (NightGlow): a soft green light about it, which HorseGlow turns up after dusk.
                var light = new GameObject("Night Glow").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                light.type = LightType.Point;
                light.color = new Color(0.55f, 1f, 0.4f);
                light.range = 8f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                var glow = new SerializedObject(root.AddComponent<HorseGlow>());
                glow.FindProperty("glow").objectReferenceValue = light;
                glow.ApplyModifiedPropertiesWithoutUndo();
            }

            var horse = root.AddComponent<Horse>();
            var motion = root.AddComponent<HorseMotion>();
            root.AddComponent<HorseTaming>();
            var data = new SerializedObject(horse);
            data.FindProperty("definition").objectReferenceValue = definition;
            data.FindProperty("seat").objectReferenceValue = seat;
            data.FindProperty("seatOffset").vector3Value = seat.InverseTransformVector(Vector3.down * HipHeight);
            data.FindProperty("saddleModel").objectReferenceValue = saddleModel != null ? saddleModel.gameObject : null;
            SerializedProperty tackList = data.FindProperty("tack");
            tackList.arraySize = tack.Length;
            for (int i = 0; i < tack.Length; i++)
            {
                tackList.GetArrayElementAtIndex(i).objectReferenceValue = tack[i];
            }

            data.FindProperty("armourModel").objectReferenceValue = armour;
            SetMaterials(data.FindProperty("armourLooks"), looks.Barding);
            Renderer[] hair = (pony ? new[] { "Pony Hair" } : new[] { "Horse_Undead_Mane_Re", "Horse_Undead_Tail_Re" }).Select(Drawn).Where(r => r != null).ToArray();
            SerializedProperty hairList = data.FindProperty("hairParts");
            hairList.arraySize = hair.Length;
            for (int i = 0; i < hair.Length; i++)
            {
                hairList.GetArrayElementAtIndex(i).objectReferenceValue = hair[i];
            }

            data.FindProperty("body").objectReferenceValue = body.transform;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (pony)
            {
                // The mane and tail swing (HairSway): each mane bone on its own, the tail as one chain.
                Transform[] all = body.GetComponentsInChildren<Transform>(true);
                Transform Bone(string name) => all.FirstOrDefault(t => t.name == name);
                var chains = all.Where(t => t.name.StartsWith("Mane ") && !t.name.EndsWith(" End"))
                    .Select(t => new HairSway.Chain { bones = new[] { t }, end = Bone(t.name + " End") })
                    .Where(c => c.end != null).ToList();
                Transform[] tail = new[] { "Tail01", "Tail02", "Tail03", "Tail04", "Tail05" }.Select(Bone).Where(t => t != null).ToArray();
                if (tail.Length == 5 && Bone("Tail05 End") != null)
                {
                    chains.Add(new HairSway.Chain { bones = tail, end = Bone("Tail05 End") });
                }

                Ensure<HairSway>(body).Configure(chains.ToArray());
            }
            var moving = new SerializedObject(motion);
            float[] speeds = GaitSpeeds(scale);
            SerializedProperty gaits = moving.FindProperty("gaitSpeeds");
            gaits.arraySize = speeds.Length;
            for (int i = 0; i < speeds.Length; i++)
            {
                gaits.GetArrayElementAtIndex(i).floatValue = speeds[i];
            }

            moving.FindProperty("turnInPlaceRate").floatValue = TurnRate();
            moving.ApplyModifiedPropertiesWithoutUndo();
            lastGaits = string.Join(", ", speeds.Select(v => v.ToString("0.00")));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            var entity = new SerializedObject(saved.GetComponent<SaveableEntity>());
            entity.FindProperty("prefabId").stringValue = AssetDatabase.AssetPathToGUID(prefabPath);
            entity.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(saved);
            return saved.GetComponent<Horse>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // A slow green fog about its body and a wisp at its nostrils: particle systems of our own (the pack's are pink in URP).
    private static void Fog(GameObject body, Material gas)
    {
        Transform[] bones = body.GetComponentsInChildren<Transform>(true);
        Transform spine = bones.FirstOrDefault(t => t.name == "Spine1") ?? body.transform;
        Transform head = bones.FirstOrDefault(t => t.name == "Head") ?? spine;
        ParticleSystem Make(string name, Transform under, float rate, float size, float speed, float radius, float life)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(under, false);
            var particles = holder.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startColor = new Color(0.5f, 0.85f, 0.3f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            main.maxParticles = 60;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            var drawn = holder.GetComponent<ParticleSystemRenderer>();
            drawn.sharedMaterial = gas;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            drawn.receiveShadows = false;
            return particles;
        }

        // The bones are scaled with the body: sizes are in the bone's space, so divide by its world scale.
        float unit = 1f / Mathf.Max(0.001f, spine.lossyScale.x);
        Make("Toxic Fog", spine, 6f, 1.4f * unit, 0.15f * unit, 0.6f * unit, 3.5f);
        float headUnit = 1f / Mathf.Max(0.001f, head.lossyScale.x);
        Make("Toxic Breath", head, 4f, 0.5f * headUnit, 0.3f * headUnit, 0.12f * headUnit, 1.6f);
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

    // ----- The stable -----

    private const string StableModelPath = "Assets/_Project/Art/Haven/Models/Stable Model.prefab";

    // A Skigard-fenced paddock with three sheds along its back for shelter, four stalls inside facing the gap at the front.
    // The model is kit-built from its concept (Tools/Blender/stable.py); the stand-in below only until that exists.
    private static string Stable()
    {
        string path = "Assets/_Project/Data/Buildings/Stable.asset";
        var building = Load<BuildingDefinition>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(building, path);
        }

        GameObject model = StableModel();
        var so = new SerializedObject(building);
        so.FindProperty("id").stringValue = "stable";
        so.FindProperty("displayName").stringValue = "Stable";
        so.FindProperty("description").stringValue = "A fenced paddock with stalls under cover. Horses kept here are safe from what comes at night, mend " +
                                                       "their hurts and get their breath back; a mare and a stallion kept together, both fond of you, may foal.";
        so.FindProperty("combatStats").objectReferenceValue = Load<CombatStatsDefinition>("Assets/_Project/Data/Combat/Stats/WoodenStructureStats.asset");
        SerializedProperty items = so.FindProperty("constructionItems");
        (string item, int amount)[] cost = { ("Materials/OakPlank", 16), ("Materials/OakWood", 10), ("Materials/NettleCord", 6), ("Materials/Barley", 10) };
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Load<ItemDefinition>($"{Items}/{cost[i].item}.asset");
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.FindProperty("station").intValue = (int)CraftingStationType.None;
        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("showInBuildMenu").boolValue = true;
        so.FindProperty("sortOrder").intValue = 21;
        so.FindProperty("placeInLines").boolValue = false;
        if (so.FindProperty("icon").objectReferenceValue == null)
        {
            so.FindProperty("icon").objectReferenceValue = IconRenderer.RenderToSprite(model, "Assets/_Project/Art/Haven/Icons/Stable Icon.png");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        building.ConfigureBuildingUp(12f, 6, new BuildingLevel[0]);
        EditorUtility.SetDirty(building);
        if (building.Prefab == null)
        {
            GameObject prefab = BuildingPrefabBuilder.Create(building, model);
            so.Update();
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        string prefabPath = AssetDatabase.GetAssetPath(building.Prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.GetComponent<Construction>() == null)
            {
                root.AddComponent<Construction>();
            }

            Transform Point(string name, Vector3 at)
            {
                Transform found = root.transform.Find(name);
                if (found == null)
                {
                    found = new GameObject(name).transform;
                    found.SetParent(root.transform, false);
                }

                found.localPosition = at;
                found.localRotation = Quaternion.identity;
                return found;
            }

            // In the model's space: the stable is shown at its ModelScale (BuildBuildingScale).
            float size = building.ModelScale;
            Transform[] stalls = Enumerable.Range(0, 4).Select(i => Point("Stall " + i, new Vector3(-3.6f + i * 2.4f, 0f, 0.9f) * size)).ToArray();
            Transform door = Point("Door", new Vector3(0f, 0f, 5.2f) * size);
            HorseStable stable = Ensure<HorseStable>(root);
            var keep = new SerializedObject(stable);
            SetObjects(keep.FindProperty("stalls"), stalls);
            keep.FindProperty("door").objectReferenceValue = door;
            keep.ApplyModifiedPropertiesWithoutUndo();
            Interactable interactable = Ensure<Interactable>(root);
            var use = new SerializedObject(interactable);
            use.FindProperty("animationType").intValue = 0;
            use.FindProperty("duration").floatValue = 0f;
            use.FindProperty("range").floatValue = 4f;
            use.FindProperty("deactivateOnComplete").boolValue = false;
            use.FindProperty("kind").intValue = (int)InteractionKind.Use;
            use.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return $"stable {prefabPath}";
    }

    private static GameObject StableModel()
    {
        // Kit-built from its concept (Tools/Blender/stable.py, BuildStructureModels): kept, not remade from stand-ins.
        if (Load<GameObject>("Assets/_Project/Art/Haven/Structures/Stable/Stable.fbx") != null && Load<GameObject>(StableModelPath) != null)
        {
            return Load<GameObject>(StableModelPath);
        }

        var root = new GameObject("Stable Model");
        try
        {
            var shed = Load<GameObject>("Assets/_Project/Environment/Stations/StorageShedModel.prefab");
            var skigard = Load<GameObject>("Assets/_Project/Prefabs/Structures/Skigard.prefab");
            Transform fence = skigard != null ? skigard.transform.Find("Model") : null;
            foreach (float x in new[] { -3.3f, 0f, 3.3f })
            {
                if (shed != null)
                {
                    var made = (GameObject)Object.Instantiate(shed, root.transform);
                    made.name = "Shed";
                    made.transform.localPosition = new Vector3(x, 0f, -1.6f);
                    made.transform.localRotation = Quaternion.identity;
                }
            }

            void Rail(Vector3 at, float yaw)
            {
                if (fence == null)
                {
                    return;
                }

                var made = Object.Instantiate(fence.gameObject, root.transform);
                made.name = "Skigard";
                made.transform.localPosition = at;
                made.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                made.transform.localScale = fence.localScale;
            }

            Rail(new Vector3(-5f, 0f, 1f), 90f);
            Rail(new Vector3(5f, 0f, 1f), 90f);
            Rail(new Vector3(-3f, 0f, 3.1f), 0f);
            Rail(new Vector3(3f, 0f, 3.1f), 0f);
            if (shed == null || fence == null)
            {
                report += "; stable model: shed or fence missing";
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, StableModelPath);
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetObjects(SerializedProperty list, Object[] objects)
    {
        list.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }
    }

    // ----- The journal -----

    /// <summary>Only the journal's Horses page (`--entry BuildHorses.Journal`): for a change to its words.</summary>
    public static string Journal()
    {
        string done = JournalPage();
        AssetDatabase.SaveAssets();
        return done;
    }

    private static string JournalPage()
    {
        var settings = Load<JournalSettings>("Assets/_Project/Resources/JournalSettings.asset");
        var skill = Load<ProficiencyDefinition>("Assets/_Project/Data/Proficiencies/Horsemanship.asset");
        if (settings == null)
        {
            return "no journal settings";
        }

        var so = new SerializedObject(settings);
        SerializedProperty pages = so.FindProperty("mechanics");
        SerializedProperty page = null;
        for (int i = 0; i < pages.arraySize; i++)
        {
            if (pages.GetArrayElementAtIndex(i).FindPropertyRelative("title").stringValue == "Horses")
            {
                page = pages.GetArrayElementAtIndex(i);
            }
        }

        if (page == null)
        {
            pages.InsertArrayElementAtIndex(pages.arraySize);
            page = pages.GetArrayElementAtIndex(pages.arraySize - 1);
        }

        page.FindPropertyRelative("group").stringValue = "Getting about";
        page.FindPropertyRelative("title").stringValue = "Horses";
        page.FindPropertyRelative("line").stringValue = "A rope, barley, and a seat kept";
        page.FindPropertyRelative("body").stringValue =
            "Wild horses graze in the land round the Haven. Come close crouched: a walk is heard ten metres off. With a lasso in hand, " +
            "throw it from within its reach ({key:Lasso}).\n\n" +
            "On the rope it fights: tap {key:Interact} or left click, and lean away from it with the movement keys (that costs your breath). Tire it " +
            "before the rope slips, then get on its back.\n\n" +
            "Breaking it in, it veers and bucks: steer against it to keep your seat. When it tosses its head, give it barley ({key:Feed}). Fed " +
            "enough, it is yours. In the saddle, {key:Dismount} gets you down ({key:Feed} never does, so feeding can't throw you off).\n\n" +
            "Each horse has its nature: calm, skittish, stubborn or bold. Feed it, groom it once a day and ride it, and it grows fond of " +
            "you: more stamina, less panic, and it comes when you whistle ({key:Whistle}). Saddles carry its bags; barding keeps blows off.\n\n" +
            "A Stable keeps horses safe at night and mends them. A mare and a stallion stabled together, both fond of you, may foal. " +
            "Deep water it swims, on its stamina.\n\n" +
            "Something else lives in the bog, and comes out at dusk.";
        page.FindPropertyRelative("summary").stringValue = "Taming, keeping and riding horses.";
        page.FindPropertyRelative("unlockedBySkill").objectReferenceValue = skill;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return skill != null ? "journal page" : "journal page (no skill yet: run BuildProficiencies, then this again)";
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
