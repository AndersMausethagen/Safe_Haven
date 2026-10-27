using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The player's "Bow" animator layer (PlayerArcheryAnimation drives it), from Kevin Iglesias' Human Archer Animations:
/// an upper-body layer just under "Upper Body", so guarding, eating and knockbacks still show over the bow.
/// Bow Idle → (BowDrawing) Bow Load at DrawSpeed → Bow Hold; BowShot plays Bow Release at ReloadSpeed, back to idle or
/// straight into the next draw. Re-running rebuilds the layer's states and keeps the rest of the controller.
/// </summary>
public static partial class CombatAnimationSetup
{
    private const string BowClips = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/";
    private const string UpperBodyMask = "Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Upper Mask.mask";

    public static string BuildBowLayer()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        AnimationClip idle = BowClip("HumanM@BowIdle01");
        AnimationClip load = BowClip("HumanM@BowShot01 - Load");
        AnimationClip hold = BowClip("HumanM@BowShot01 - Hold");
        AnimationClip release = BowClip("HumanM@BowShot01 - Release");
        if (controller == null || idle == null || load == null || hold == null || release == null)
        {
            return "Bow layer: the controller or the archer clips are missing.";
        }

        Parameter(controller, "BowDrawing", AnimatorControllerParameterType.Bool, 0f);
        Parameter(controller, "BowShot", AnimatorControllerParameterType.Trigger, 0f);
        Parameter(controller, "DrawSpeed", AnimatorControllerParameterType.Float, 1f);
        Parameter(controller, "ReloadSpeed", AnimatorControllerParameterType.Float, 1f);

        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers.FirstOrDefault(l => l.name == PlayerArcheryAnimation.LayerName);
        if (layer == null)
        {
            var machine = new AnimatorStateMachine { name = PlayerArcheryAnimation.LayerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            layer = new AnimatorControllerLayer { name = PlayerArcheryAnimation.LayerName, stateMachine = machine };
            int below = System.Array.FindIndex(layers, l => l.name == "Upper Body");
            var list = layers.ToList();
            list.Insert(below >= 0 ? below : list.Count, layer);
            layers = list.ToArray();
        }

        // Faded in by PlayerArcheryAnimation while a bow is in hand.
        layer.defaultWeight = 0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMask);
        controller.layers = layers;

        AnimatorStateMachine bow = controller.layers.First(l => l.name == PlayerArcheryAnimation.LayerName).stateMachine;
        AnimatorState idleState = State(bow, "Bow Idle", idle, new Vector3(300f, 0f));
        AnimatorState loadState = State(bow, "Bow Load", load, new Vector3(300f, 120f));
        AnimatorState holdState = State(bow, "Bow Hold", hold, new Vector3(600f, 120f));
        AnimatorState releaseState = State(bow, "Bow Release", release, new Vector3(600f, 0f));
        loadState.speedParameterActive = true;
        loadState.speedParameter = "DrawSpeed";
        releaseState.speedParameterActive = true;
        releaseState.speedParameter = "ReloadSpeed";
        bow.defaultState = idleState;
        foreach (AnimatorState state in new[] { idleState, loadState, holdState, releaseState })
        {
            ClearTransitions(state);
        }

        // Order matters: a shot and the end of the draw arrive in the same frame, and the shot must win.
        When(idleState, loadState, 0.1f, ("BowDrawing", true));
        When(loadState, releaseState, 0.05f, ("BowShot", true));
        When(loadState, idleState, 0.15f, ("BowDrawing", false));
        Exit(loadState, holdState, 0.98f, 0.08f);
        When(holdState, releaseState, 0.05f, ("BowShot", true));
        When(holdState, idleState, 0.15f, ("BowDrawing", false));
        When(releaseState, loadState, 0.1f, ("BowDrawing", true));
        Exit(releaseState, idleState, 0.95f, 0.15f);

        string arms = FitArmsToBow(controller);
        EditorUtility.SetDirty(controller);
        return $"Bow layer built ({load.length:F2}s load, {release.length:F2}s release); {arms}";
    }

    private static AnimationClip BowClip(string file) =>
        AssetDatabase.LoadAllAssetsAtPath(BowClips + file + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));

    // The Arms layer picked the polearm hold for any stance above Two Handed; the bow (stance 4) has its own pose, so
    // the arms let go of any hold for it.
    private static string FitArmsToBow(AnimatorController controller)
    {
        AnimatorControllerLayer arms = controller.layers.FirstOrDefault(l => l.name == "Arms");
        AnimatorState polearm = arms != null ? Find(arms.stateMachine, "Hold Polearm") : null;
        AnimatorState empty = arms != null ? Find(arms.stateMachine, "Empty") : null;
        if (polearm == null || empty == null)
        {
            return "no Arms layer to fit";
        }

        foreach (AnimatorState state in arms.stateMachine.states.Select(s => s.state))
        {
            foreach (AnimatorStateTransition transition in state.transitions.Where(t => t.destinationState == polearm))
            {
                if (!transition.conditions.Any(c => c.parameter == "WeaponStance" && c.mode == AnimatorConditionMode.Less))
                {
                    transition.AddCondition(AnimatorConditionMode.Less, 3.5f, "WeaponStance");
                }
            }
        }

        foreach (AnimatorState hold in arms.stateMachine.states.Select(s => s.state).Where(s => s != empty))
        {
            if (!hold.transitions.Any(t => t.destinationState == empty && t.conditions.Any(c => c.mode == AnimatorConditionMode.Greater && c.threshold > 3f)))
            {
                AnimatorStateTransition toEmpty = hold.AddTransition(empty);
                toEmpty.hasExitTime = false;
                toEmpty.hasFixedDuration = true;
                toEmpty.duration = 0.15f;
                toEmpty.AddCondition(AnimatorConditionMode.Greater, 3.5f, "WeaponStance");
            }
        }

        return "the arms let go of their hold for the bow";
    }
}
