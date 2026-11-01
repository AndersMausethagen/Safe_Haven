using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Puts the modular character pack's animations into the game's Animator controllers: the pack's walk, run, sprint,
/// sneak and talk replace the old ones on the player, a "Villager" controller (MonsterHumanoid's states with a relaxed
/// idle and the pack's walk and run) is made for people, and both get an "Emote" layer with a state per
/// EmoteDefinition, which CharacterEmotes plays. Safe to run again.
/// </summary>
public static class CharacterAnimatorBuilder
{
    public const string PlayerControllerPath = "Assets/_Project/Animation/Controllers/PlayerAnimator.controller";
    public const string VillagerControllerPath = "Assets/_Project/Animation/Controllers/Villager.controller";
    public const string ArcherOverridePath = "Assets/_Project/Data/Companions/ArcherAnimations.overrideController";

    // Old clip name -> the pack's (male) clip, wherever the old clip is used.
    private static readonly Dictionary<string, string> PlayerSwaps = new Dictionary<string, string>
    {
        { "HumanM@Walk01_Forward", "Walk_Forward" },
        { "HumanM@Walk01_Backward", "Walk_Backwards" },
        { "HumanM@Run01_Forward", "Run_Forward" },
        { "HumanM@Run01_Backward", "Run_Backwards" },
        { "HumanM@Sprint01_Forward", "Sprint_Forward" },
        { "HumanM@StrafeRun01_Left", "Run_Left" },
        { "HumanM@StrafeRun01_Right", "Run_Right" },
        { "HumanM@StrafeRun01_ForwardLeft", "Run_ForwardLeft" },
        { "HumanM@StrafeRun01_ForwardRight", "Run_ForwardRight" },
        { "HumanM@StrafeRun01_BackwardLeft", "Run_BackwardLeft" },
        { "HumanM@StrafeRun01_BackwardRight", "Run_BackwardRight" },
        { "Crouch_Idle_Loop", "Idle_Stealth" },
        { "Crouch_Fwd_Loop", "Walk_Stealth_Forward" },
    };

    // Only the ordinary standing states get the pack's relaxed idle; combat keeps its guard.
    private static readonly HashSet<string> RelaxedStates = new HashSet<string>
    {
        "Locomotion", "Idle", "Idle To Alt", "Idle Alt", "Alt To Idle",
    };

    private static readonly Dictionary<string, string> RelaxedSwaps = new Dictionary<string, string>
    {
        { "HumanM@Idle01", "idle" },
        { "HumanM@Idle01-Idle02", "idle" },
        { "HumanM@Idle02-Idle01", "idle" },
        { "HumanM@Idle02", "Idle02" },
    };

    [MenuItem("Safe Haven/Characters/Build Character Animators")]
    public static void BuildFromMenu() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        Dictionary<string, AnimationClip> clips = CharacterLibraryBuilder.Clips(CharacterLibraryBuilder.MaleClipsPath);
        IReadOnlyList<EmoteDefinition> emotes = CharacterCatalog.Current.Emotes;
        if (emotes.Count == 0)
        {
            throw new System.InvalidOperationException("Build the character library first: the catalog has no emotes.");
        }

        var report = new StringBuilder();
        var player = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        int swapped = 0;
        foreach (AnimatorControllerLayer layer in player.layers)
        {
            swapped += SwapIn(layer.stateMachine, clips, layer.name == "Base");
        }

        AnimatorState talkA = FindState(player, "Interaction", "Talk A");
        AnimatorState talkB = FindState(player, "Interaction", "Talk B");
        if (talkA != null)
        {
            talkA.motion = clips["Emote_Talk"];
        }

        if (talkB != null)
        {
            talkB.motion = clips["Emote_TalkQuestion"];
        }

        AddEmoteLayer(player, emotes);
        report.Append($"Player: {swapped} clips swapped, {emotes.Count} emotes. ");
        report.Append(BuildLocomotion(player, clips));

        AnimatorController villager = Villager(clips);
        AddEmoteLayer(villager, emotes);
        CharacterCatalog.Current.EditorSetPreviewAnimations(villager);
        EditorUtility.SetDirty(CharacterCatalog.Current);
        report.Append($"Villager: {VillagerControllerPath}. ");

        var archer = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ArcherOverridePath);
        if (archer != null && archer.runtimeAnimatorController != villager)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            archer.GetOverrides(overrides);
            archer.runtimeAnimatorController = villager;
            archer.ApplyOverrides(overrides.Where(o => o.Value != null).ToList());
            EditorUtility.SetDirty(archer);
            report.Append("Archer animations now override the Villager controller. ");
        }

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(villager);
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    private static int SwapIn(AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips, bool baseLayer)
    {
        int count = 0;
        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            bool relaxed = baseLayer && RelaxedStates.Contains(state.name);
            if (state.motion is BlendTree tree)
            {
                count += SwapIn(tree, clips, relaxed);
            }
            else if (state.motion is AnimationClip clip && TrySwap(clip, clips, relaxed, out AnimationClip own))
            {
                state.motion = own;
                count++;
            }
        }

        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
        {
            count += SwapIn(sub.stateMachine, clips, baseLayer);
        }

        return count;
    }

    private static int SwapIn(BlendTree tree, Dictionary<string, AnimationClip> clips, bool relaxed)
    {
        int count = 0;
        ChildMotion[] children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion is BlendTree inner)
            {
                count += SwapIn(inner, clips, relaxed);
            }
            else if (children[i].motion is AnimationClip clip && TrySwap(clip, clips, relaxed, out AnimationClip own))
            {
                children[i].motion = own;
                count++;
            }
        }

        tree.children = children;
        EditorUtility.SetDirty(tree);
        return count;
    }

    private static bool TrySwap(AnimationClip clip, Dictionary<string, AnimationClip> clips, bool relaxed, out AnimationClip own)
    {
        own = null;
        if (relaxed && RelaxedSwaps.TryGetValue(clip.name, out string relaxedName))
        {
            own = clips[relaxedName];
        }
        else if (PlayerSwaps.TryGetValue(clip.name, out string name))
        {
            own = clips[name];
        }

        return own != null;
    }

    private static AnimatorController Villager(Dictionary<string, AnimationClip> clips)
    {
        var villager = AssetDatabase.LoadAssetAtPath<AnimatorController>(VillagerControllerPath);
        if (villager == null)
        {
            AssetDatabase.CopyAsset(MonsterPrefabBuilder.DefaultControllerPath, VillagerControllerPath);
            villager = AssetDatabase.LoadAssetAtPath<AnimatorController>(VillagerControllerPath);
        }

        AnimatorState locomotion = FindState(villager, villager.layers[0].name, "Locomotion");
        if (locomotion != null && locomotion.motion is BlendTree tree)
        {
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                children[i].motion = i == 0 ? clips["idle"] : i == 1 ? clips["Walk_Forward"] : clips["Run_Forward"];
            }

            tree.children = children;
            EditorUtility.SetDirty(tree);
        }

        return villager;
    }

    private static AnimatorState FindState(AnimatorController controller, string layerName, string stateName)
    {
        AnimatorControllerLayer layer = controller.layers.FirstOrDefault(l => l.name == layerName);
        return layer?.stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
    }

    // Ground speeds (m/s) of the pack's locomotion, measured from the planted foot: every walk 1.6, the forward and
    // sideways runs 4.8, the backpedals 2.1, in every weapon set. PlayerAnimationController's runClipSpeed and
    // backRunClipSpeed must match; it speeds the runs up past them.
    private const float WalkSpeed = 1.6f;
    private const float RunSpeed = 4.8f;
    private const float BackRunSpeed = 2.1f;

    // Per WeaponStance (the index): the run set that holds that weapon, and the walk set.
    private static readonly (string run, string walk)[] StanceSets =
    {
        ("Run_", "Walk_"), ("Run_1H_", "Walk_"), ("Run_2H_", "Walk_"), ("Run_2HL_", "Walk_2HL_"), ("Run_Bow_", "Walk_"),
    };

    private static readonly string[] Directions =
    {
        "Forward", "ForwardRight", "Right", "BackwardRight", "Backwards", "BackwardLeft", "Left", "ForwardLeft",
    };

    [MenuItem("Safe Haven/Characters/Build Player Locomotion")]
    public static void BuildLocomotionFromMenu()
    {
        var player = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        Debug.Log(BuildLocomotion(player, CharacterLibraryBuilder.Clips(CharacterLibraryBuilder.MaleClipsPath)));
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Rebuilds the player's Locomotion and Combat Locomotion as a blend on WeaponStance of one tree per stance, so a
    /// held weapon runs with the pack's matching arms instead of the empty-handed pump. Clips sit at their measured
    /// ground speeds so the feet match the movement; MotionSpeed takes over above them.
    /// </summary>
    public static string BuildLocomotion(AnimatorController player, Dictionary<string, AnimationClip> clips)
    {
        AnimatorState explore = FindState(player, "Base", "Locomotion");
        AnimatorState combat = FindState(player, "Base", "Combat Locomotion");
        if (explore?.motion is not BlendTree exploreRoot || combat?.motion is not BlendTree combatRoot ||
            FindState(player, "Base", "Combat Idle")?.motion is not BlendTree combatIdle)
        {
            throw new System.InvalidOperationException("PlayerAnimator is missing Locomotion, Combat Locomotion or Combat Idle.");
        }

        ResetToStanceBlend(exploreRoot);
        ResetToStanceBlend(combatRoot);
        for (int stance = 0; stance < StanceSets.Length; stance++)
        {
            (string run, string walk) = StanceSets[stance];

            BlendTree line = exploreRoot.CreateBlendTreeChild(stance);
            line.name = $"Locomotion {(WeaponStance)stance}";
            line.blendType = BlendTreeType.Simple1D;
            line.blendParameter = "Speed";
            line.useAutomaticThresholds = false;
            line.AddChild(clips["idle"], 0f);
            line.AddChild(Clip(clips, walk, "Forward"), WalkSpeed);
            line.AddChild(Clip(clips, run, "Forward"), RunSpeed);

            BlendTree rings = combatRoot.CreateBlendTreeChild(stance);
            rings.name = $"Combat Locomotion {(WeaponStance)stance}";
            rings.blendType = BlendTreeType.FreeformDirectional2D;
            rings.blendParameter = "MoveX";
            rings.blendParameterY = "MoveZ";
            rings.AddChild(IdleFor(combatIdle, stance), Vector2.zero);
            for (int d = 0; d < Directions.Length; d++)
            {
                Vector2 direction = DirectionOf(d);
                bool back = direction.y < -0.1f;
                rings.AddChild(Clip(clips, walk, Directions[d]), direction * WalkSpeed);
                rings.AddChild(Clip(clips, run, Directions[d]), direction * (back ? BackRunSpeed : RunSpeed));
            }
        }

        explore.speedParameterActive = true;
        explore.speedParameter = "MotionSpeed";
        combat.speedParameterActive = true;
        combat.speedParameter = "MotionSpeed";
        EditorUtility.SetDirty(player);
        return $"Locomotion: {StanceSets.Length} stance sets (walk {WalkSpeed}, run {RunSpeed}, backpedal {BackRunSpeed} m/s). ";
    }

    // Clears a state's root tree (and the sub-trees an earlier build left in the controller) for a WeaponStance blend.
    private static void ResetToStanceBlend(BlendTree root)
    {
        foreach (ChildMotion child in root.children)
        {
            if (child.motion is BlendTree nested)
            {
                ResetToStanceBlend(nested);
                AssetDatabase.RemoveObjectFromAsset(nested);
                Object.DestroyImmediate(nested, true);
            }
        }

        root.children = new ChildMotion[0];
        root.blendType = BlendTreeType.Simple1D;
        root.blendParameter = "WeaponStance";
        root.useAutomaticThresholds = false;
        EditorUtility.SetDirty(root);
    }

    private static Vector2 DirectionOf(int index)
    {
        float radians = index * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    private static Motion IdleFor(BlendTree combatIdle, int stance)
    {
        return combatIdle.children.OrderBy(c => Mathf.Abs(c.threshold - stance)).First().motion;
    }

    // The 2HL walk spells it "Backward".
    private static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string prefix, string direction)
    {
        if (clips.TryGetValue(prefix + direction, out AnimationClip clip) ||
            (direction == "Backwards" && clips.TryGetValue(prefix + "Backward", out clip)))
        {
            return clip;
        }

        throw new KeyNotFoundException($"The pack has no clip {prefix}{direction}.");
    }

    // A full-body layer over everything else, weight 0 until CharacterEmotes plays on it. Held emotes go Begin -> loop
    // by themselves; their End is played by CharacterEmotes and holds its last frame while the layer fades out.
    private static void AddEmoteLayer(AnimatorController controller, IReadOnlyList<EmoteDefinition> emotes)
    {
        for (int i = controller.layers.Length - 1; i >= 0; i--)
        {
            if (controller.layers[i].name == CharacterEmotes.LayerName)
            {
                controller.RemoveLayer(i);
            }
        }

        controller.AddLayer(CharacterEmotes.LayerName);
        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers[layers.Length - 1];
        layer.defaultWeight = 0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layers[layers.Length - 1] = layer;
        controller.layers = layers;

        AnimatorStateMachine machine = layer.stateMachine;
        AnimatorState empty = machine.AddState("Empty", new Vector3(250f, 0f, 0f));
        machine.defaultState = empty;

        int row = 1;
        foreach (EmoteDefinition emote in emotes)
        {
            if (emote == null || emote.Clip == null)
            {
                continue;
            }

            var y = 60f * row++;
            AnimatorState main = machine.AddState(emote.StateName, new Vector3(500f, y, 0f));
            main.motion = emote.Clip;
            if (emote.HasBegin)
            {
                AnimatorState begin = machine.AddState(emote.BeginState, new Vector3(250f, y, 0f));
                begin.motion = emote.Begin;
                AnimatorStateTransition next = begin.AddTransition(main);
                next.hasExitTime = true;
                next.exitTime = 0.9f;
                next.hasFixedDuration = true;
                next.duration = 0.15f;
            }

            if (emote.HasEnd)
            {
                AnimatorState end = machine.AddState(emote.EndState, new Vector3(750f, y, 0f));
                end.motion = emote.End;
            }
        }
    }
}
