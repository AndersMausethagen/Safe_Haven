using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Targeted tools for adding one swing at a time (the bulk Set Up Combat Animations builder is retired: it overwrote newer work). A
/// swing state goes on the player's three swing layers (Upper Body for a planted heavy's wind-up, Attack Arms for a swing walked
/// through, Combat Full Body for one struck standing), and an attack's animation timing is fitted to its clip so the blow lands at the
/// attack's Hit Delay and the clip plays out by its Recovery Time, the cadence (and so damage per second) staying the attack's own.
/// The fitting is the retired builder's (CombatAnimationBuild.MakeAttack, fitted path), kept in one place here.
/// </summary>
public static class CombatClipKit
{
    public const string ActionSpeed = "ActionSpeed";
    private static readonly string[] SwingLayers = { "Upper Body", "Attack Arms", CombatAnimationSetup.FullBodyLayer };

    /// <summary>The clip in a model file by name (null: its first clip).</summary>
    public static AnimationClip Clip(string path, string name = null) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
        .FirstOrDefault(clip => !clip.name.StartsWith("__preview__") && (name == null || clip.name == name));

    /// <summary>Adds or refreshes <paramref name="state"/> playing <paramref name="clip"/> on the player's swing layers, at the
    /// ActionSpeed parameter, leaving for Empty near its end. <paramref name="mirror"/> plays it on the other side (two-handed swings only:
    /// a one-handed swing would change hands). Returns what it did.</summary>
    public static string PlayerSwingState(string state, AnimationClip clip, bool mirror = false, float exitTime = 0.92f)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatAnimationSetup.PlayerControllerPath);
        if (controller == null || clip == null)
        {
            return $"{state}: no controller or clip";
        }

        int made = 0, refreshed = 0;
        foreach (AnimatorControllerLayer layer in controller.layers.Where(l => SwingLayers.Contains(l.name)))
        {
            if (Configure(layer.stateMachine, state, clip, mirror, exitTime, "Empty"))
            {
                made++;
            }
            else
            {
                refreshed++;
            }
        }

        EditorUtility.SetDirty(controller);
        return $"{state}: {made} made, {refreshed} refreshed ({clip.name}{(mirror ? ", mirrored" : "")})";
    }

    /// <summary>A state on one player layer only (a hit reaction on Upper Body, a knockdown on Combat Full Body).</summary>
    public static string PlayerLayerState(string layerName, string state, AnimationClip clip, float exitTime = 0.9f, float speed = 1f)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatAnimationSetup.PlayerControllerPath);
        AnimatorControllerLayer layer = controller != null ? controller.layers.FirstOrDefault(l => l.name == layerName) : null;
        if (layer == null || clip == null)
        {
            return $"{state}: no layer {layerName} or clip";
        }

        bool made = Configure(layer.stateMachine, state, clip, false, exitTime, "Empty", speedParameter: false);
        layer.stateMachine.states.First(s => s.state.name == state).state.speed = speed;
        EditorUtility.SetDirty(controller);
        return $"{state} on {layerName}: {(made ? "made" : "refreshed")}";
    }

    /// <summary>A state on the humanoid monsters' controller, leaving for Locomotion near its end. <paramref name="fitted"/>: it plays at
    /// the ActionSpeed parameter (a strike fitted to its attack's Hit Delay), times <paramref name="speed"/>.</summary>
    public static string MonsterState(string state, AnimationClip clip, float speed, float exitTime = 0.85f, bool fitted = false)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatAnimationSetup.MonsterControllerPath);
        if (controller == null || clip == null)
        {
            return $"{state}: no monster controller or clip";
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        bool made = Configure(machine, state, clip, false, exitTime, "Locomotion", speedParameter: fitted);
        machine.states.First(s => s.state.name == state).state.speed = speed;
        EditorUtility.SetDirty(controller);
        return $"monster {state}: {(made ? "made" : "refreshed")}";
    }

    private static bool Configure(AnimatorStateMachine machine, string name, Motion clip, bool mirror, float exitTime, string exitTo, bool speedParameter = true)
    {
        AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        bool made = state == null;
        if (made)
        {
            state = machine.AddState(name, new Vector3(1500f, 40f * machine.states.Length));
        }

        state.motion = clip;
        state.mirror = mirror;
        state.writeDefaultValues = true;
        state.speedParameterActive = speedParameter;
        state.speedParameter = speedParameter ? ActionSpeed : state.speedParameter;
        foreach (AnimatorStateTransition old in state.transitions.ToArray())
        {
            state.RemoveTransition(old);
        }

        AnimatorState to = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == exitTo);
        if (to != null)
        {
            AnimatorStateTransition exit = state.AddTransition(to);
            exit.hasExitTime = true;
            exit.exitTime = exitTime;
            exit.hasFixedDuration = true;
            exit.duration = 0.12f;
        }

        return made;
    }

    /// <summary>An attack asset in the combo folder: loaded if it exists, else a copy of <paramref name="template"/> made there.</summary>
    public static AttackDefinition Attack(string name, AttackDefinition template)
    {
        string path = $"{CombatAnimationSetup.ComboFolder}/{name}.asset";
        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
        if (attack != null)
        {
            return attack;
        }

        attack = template != null ? Object.Instantiate(template) : ScriptableObject.CreateInstance<AttackDefinition>();
        AssetDatabase.CreateAsset(attack, path);
        var data = new SerializedObject(attack);
        data.FindProperty("displayName").stringValue = name;
        data.ApplyModifiedPropertiesWithoutUndo();
        return attack;
    }

    /// <summary>
    /// Fits <paramref name="attack"/> to <paramref name="clip"/> in <paramref name="state"/> at the clip's own pace (the user,
    /// 2026-11-25: smooth animation first, damage tuned after; CombatClipPace): the blow lands at the clip's <paramref name="contact"/>,
    /// seconds in, the strike covers the last <paramref name="strikeClip"/> seconds before it (a held heavy charges just before), the
    /// next swing may start at <paramref name="recoversAt"/> seconds into the clip, and every phase plays at 1x. The slash arc runs the
    /// way the hand moves at contact (mirrored for a mirrored state).
    /// </summary>
    public static void FitAttack(AttackDefinition attack, string state, AnimationClip clip, float contact, float recoversAt,
        bool mirror = false, float strikeClip = 0.12f, HumanBodyBones hand = HumanBodyBones.RightHand)
    {
        float length = clip.length;
        contact = Mathf.Clamp(contact, 0.05f, length);
        recoversAt = Mathf.Clamp(recoversAt, contact + 0.05f, Mathf.Max(contact + 0.05f, length));
        Vector3 velocity = CombatAnimationSetup.HandVelocity(clip, contact, hand);
        if (mirror)
        {
            velocity.x = -velocity.x;
        }

        (SlashSweep sweep, float tilt) = CombatAnimationSetup.SlashFor(velocity);
        var data = new SerializedObject(attack);
        data.FindProperty("hitDelay").floatValue = contact;
        data.FindProperty("recoveryTime").floatValue = recoversAt;
        data.FindProperty("animationState").stringValue = state;
        data.FindProperty("animationSpeed").floatValue = 1f;
        data.FindProperty("strikeSpeed").floatValue = 1f;
        data.FindProperty("strikeLead").floatValue = Mathf.Min(strikeClip, contact * 0.7f);
        data.FindProperty("followThroughSpeed").floatValue = 1f;
        data.FindProperty("slashSweep").enumValueIndex = (int)sweep;
        data.FindProperty("slashTilt").floatValue = tilt;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
    }

    /// <summary>Sets serialized fields by name (float, int, bool, enum index, string, object), reporting what changed.</summary>
    public static string Set(Object asset, params (string field, object value)[] values)
    {
        var data = new SerializedObject(asset);
        var parts = new System.Collections.Generic.List<string>();
        foreach ((string field, object value) in values)
        {
            SerializedProperty property = data.FindProperty(field);
            if (property == null)
            {
                parts.Add(field + " MISSING");
                continue;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: property.floatValue = System.Convert.ToSingle(value); break;
                case SerializedPropertyType.Integer: property.intValue = System.Convert.ToInt32(value); break;
                case SerializedPropertyType.Boolean: property.boolValue = (bool)value; break;
                case SerializedPropertyType.Enum: property.enumValueIndex = System.Convert.ToInt32(value); break;
                case SerializedPropertyType.String: property.stringValue = (string)value; break;
                case SerializedPropertyType.ObjectReference: property.objectReferenceValue = (Object)value; break;
                default: parts.Add(field + " UNSUPPORTED"); continue;
            }

            parts.Add($"{field}={value}");
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset.name + ": " + string.Join(", ", parts);
    }
}
