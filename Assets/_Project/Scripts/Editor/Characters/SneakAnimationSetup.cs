using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static partial class CombatAnimationSetup
{
    public const string SneakLayer = "Sneak";
    private const string SneakState = "Sneak";
    private const string SneakingParameter = "Sneaking";
    // Foot speed of Crouch_Fwd_Loop as authored (its root-motion twin in UAL1_Standard_RM moves 0.73 m/s).
    private const float CrouchWalkSpeed = 0.73f;
    // Matching the feet exactly at sneak speed would scuttle at over 3x, so a little foot slide is the better trade.
    private const float MaxCrouchPlayback = 2.5f;

    /// <summary>
    /// The crouched walk: a full-body layer right above Base, so Base keeps running underneath and every action layer
    /// (arms, attacks, interactions) still plays over it. Sneaking fades it in and out. A blend tree on Speed goes from the
    /// crouched idle to the crouched walk, sped up to the player's sneaking speed. Idempotent.
    /// </summary>
    [MenuItem("Safe Haven/Characters/Set Up Sneak Animation")]
    public static void BuildSneakLayer()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        AnimationClip idle = Ual1("Crouch_Idle_Loop");
        AnimationClip walk = Ual1("Crouch_Fwd_Loop");
        if (controller == null || idle == null || walk == null)
        {
            Debug.LogError("Sneak animation: the player controller or the Quaternius crouch clips are missing.");
            return;
        }

        Parameter(controller, SneakingParameter, AnimatorControllerParameterType.Bool, 0f);
        AnimatorStateMachine machine = EnsureSneakLayer(controller);
        AnimatorState empty = State(machine, "Empty", null, new Vector3(300f, 0f));
        machine.defaultState = empty;
        AnimatorState sneak = State(machine, SneakState, SneakTree(controller, machine, idle, walk), new Vector3(300f, 120f));
        ClearTransitions(empty);
        ClearTransitions(sneak);
        Fade(empty, sneak, AnimatorConditionMode.If);
        Fade(sneak, empty, AnimatorConditionMode.IfNot);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Sneak animation set up on the player controller.");
    }

    private static void Fade(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(mode, 0f, SneakingParameter);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.2f;
    }

    // Layer order is how animator layers stack, so it goes right above Base and under everything that overrides the body.
    private static AnimatorStateMachine EnsureSneakLayer(AnimatorController controller)
    {
        List<AnimatorControllerLayer> layers = controller.layers.ToList();
        AnimatorControllerLayer layer = layers.FirstOrDefault(l => l.name == SneakLayer);
        if (layer == null)
        {
            var machine = new AnimatorStateMachine { name = SneakLayer, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            layer = new AnimatorControllerLayer
            {
                name = SneakLayer, defaultWeight = 1f, blendingMode = AnimatorLayerBlendingMode.Override, stateMachine = machine,
            };
        }

        layers.Remove(layer);
        layers.Insert(1, layer);
        controller.layers = layers.ToArray();
        return layer.stateMachine;
    }

    private static BlendTree SneakTree(AnimatorController controller, AnimatorStateMachine machine, AnimationClip idle, AnimationClip walk)
    {
        AnimatorState existing = Find(machine, SneakState);
        BlendTree tree = existing != null ? existing.motion as BlendTree : null;
        if (tree == null)
        {
            tree = new BlendTree { name = SneakState, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, controller);
        }

        var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(MovementSettingsPath());
        float sneakSpeed = (settings != null ? settings.MoveSpeed : 5f) * PerceptionSettings.Current.SneakSpeedScale;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.children = new[]
        {
            new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
            new ChildMotion { motion = walk, threshold = sneakSpeed, timeScale = Mathf.Clamp(sneakSpeed / CrouchWalkSpeed, 1f, MaxCrouchPlayback) },
        };
        EditorUtility.SetDirty(tree);
        return tree;
    }

    private static string MovementSettingsPath()
    {
        string guid = AssetDatabase.FindAssets("t:PlayerMovementSettings").FirstOrDefault();
        return guid != null ? AssetDatabase.GUIDToAssetPath(guid) : null;
    }
}
