using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Measures every way into and out of the player's attacks and keeps them smooth (the user, 2026-11-20: "make sure all attack
/// animations transition smoothly and fit in a chain"). A way in is the pose before a swing (the weapon's ready pose, the chain's
/// swing before it at the moment the next one starts, a light swing before the heavy follow-up, the guard before a counter) against
/// the swing's first frame; a way out is the swing's last frame against the ready pose. Poses are compared on the Humanoid muscle
/// curves of the body and arms (legs are the locomotion's), mirrored where the state is.
/// <para>
/// The rule (AttackFlowRules): a blend in moves no faster than the swing does and is over before its strike, so no way in may need a
/// longer blend than there is room for (AttackFlowTests). <see cref="Bake"/> writes each attack's Blend In (the longest any way into
/// it needs) and each swing's settle back into the ready pose.
/// </para>
/// </summary>
public static class AttackFlow
{
    public const string ControllerPath = "Assets/_Project/Animation/Controllers/PlayerAnimator.controller";
    private const string FullBodyLayer = "Combat Full Body";
    private const int MuscleCount = 55;
    private static readonly int[] SideMuscles = { 1, 2, 4, 5, 7, 8, 10, 11, 13, 14, 20 };

    /// <summary>A way into an attack: what came before, the attack, and the blend it needs against the room before its strike.</summary>
    public readonly struct Link
    {
        public readonly string Weapon, From;
        public readonly AttackDefinition To;
        public readonly float Pop, Need, Room;

        public Link(string weapon, string from, AttackDefinition to, float pop, float need, float room)
        {
            Weapon = weapon;
            From = from;
            To = to;
            Pop = pop;
            Need = need;
            Room = room;
        }

        public bool Smooth => Need <= Room + 0.0005f;
        public override string ToString() => $"{Weapon}: {From} -> {To.name} [{To.AnimationState}] pose {Pop:0.0}, blend {Need:0.00} s, room {Room:0.00} s";
    }

    /// <summary>A way out: a swing state settling back into a weapon's ready pose.</summary>
    public readonly struct Settle
    {
        public readonly string Weapon, State;
        public readonly float Pop;

        public Settle(string weapon, string state, float pop)
        {
            Weapon = weapon;
            State = state;
            Pop = pop;
        }

        public float Need => Mathf.Clamp(Pop / AttackFlowRules.SettleRate, AttackFlowRules.ShortestSettle, AttackFlowRules.LongestSettle);
        public bool Smooth => Pop / AttackFlowRules.SettleRate <= AttackFlowRules.LongestSettle + 0.0005f;
        public override string ToString() => $"{Weapon}: {State} -> ready pose {Pop:0.0}, settle {Pop / AttackFlowRules.SettleRate:0.00} s";
    }

    private sealed class Context
    {
        public AnimatorController Controller;
        public readonly Dictionary<string, AnimatorState> FullBody = new Dictionary<string, AnimatorState>();
        public BlendTree ReadyTree, GuardTree, CombatLocomotion;
        public AnimationClip ExploringIdle;
        public readonly Dictionary<string, AnimationClip> Holds = new Dictionary<string, AnimationClip>();
        public readonly Dictionary<AnimationClip, AnimationCurve[]> Curves = new Dictionary<AnimationClip, AnimationCurve[]>();
    }

    // ---------------------------------------------------------------- measuring

    public static List<Link> Links() => Measure(out _);

    public static List<Settle> Settles()
    {
        Measure(out List<Settle> settles);
        return settles;
    }

    public static List<Link> Measure(out List<Settle> settles)
    {
        Context context = Load();
        var links = new List<Link>();
        var outs = new List<Settle>();
        settles = outs;
        var seen = new HashSet<string>();
        var settled = new HashSet<string>();
        foreach (WeaponDefinition weapon in MeleeWeapons())
        {
            float ready = ReadyPoseValue(weapon);
            float[] readyPose = TreePose(context, context.ReadyTree, ready);
            float[] guardPose = TreePose(context, context.GuardTree, ready);
            float[] exploringPose = ExploringPose(context, weapon);
            float[] walkingPose = WalkingPose(context, weapon);
            if (readyPose == null)
            {
                continue;
            }

            void Into(string from, float[] pose, AttackDefinition to)
            {
                if (pose == null || !Playable(context, to) || !seen.Add(weapon.name + "|" + from + "|" + to.name))
                {
                    return;
                }

                float pop = Distance(pose, StartPose(context, to));
                links.Add(new Link(weapon.name, from, to, pop, AttackFlowRules.BlendNeeded(pop, WindupRate(context, to), StrikeRate(context, to)),
                    AttackFlowRules.BlendRoom(to.HitDelay, to.StrikeLead)));
            }

            void Out(AttackDefinition attack)
            {
                if (!Playable(context, attack) || !settled.Add(weapon.name + "|" + attack.AnimationState))
                {
                    return;
                }

                outs.Add(new Settle(weapon.name, attack.AnimationState, Distance(EndPose(context, attack), readyPose)));
            }

            IReadOnlyList<AttackDefinition> combo = weapon.Combo;
            AttackDefinition heavyAfterLight = weapon.HeavyFollowUp != null ? weapon.HeavyFollowUp : weapon.HeavyAttack;
            for (int i = 0; i < combo.Count; i++)
            {
                AttackDefinition swing = combo[i];
                if (!Playable(context, swing))
                {
                    continue;
                }

                if (i == 0)
                {
                    Into("ready", readyPose, swing);
                }

                float[] handoff = HandoffPose(context, swing);
                if (combo.Count > 1)
                {
                    Into(swing.name, handoff, combo[(i + 1) % combo.Count]);
                }

                Into(swing.name + " (heavy)", handoff, heavyAfterLight);
                Out(swing);
            }

            foreach (AttackDefinition fromReady in new[] { weapon.HeavyAttack, weapon.BackstepAttack, weapon.WeaponArt, weapon.ChargedAttack })
            {
                Into("ready", readyPose, fromReady);
                Out(fromReady);
            }

            // The first swing of a fight starts from the exploring pose (the fight begins with it); a swing started on the move from
            // the combat walk's upper body (it plays on Upper Body over the walking legs).
            if (combo.Count > 0)
            {
                Into("exploring", exploringPose, combo[0]);
                Into("walking", walkingPose, combo[0]);
            }

            Into("exploring", exploringPose, weapon.HeavyAttack);
            Into("guard", guardPose, weapon.GuardCounterAttack);
            Into("guard", guardPose, weapon.CounterAttack);
            Out(weapon.GuardCounterAttack);
            Out(weapon.CounterAttack);
            Out(heavyAfterLight);
        }

        return links;
    }

    /// <summary>The Combat Idle / guard trees' ReadyPose for a weapon (the stance, or the weapon's own).</summary>
    public static float ReadyPoseValue(WeaponDefinition weapon) =>
        weapon.ReadyPose == WeaponReadyPose.Staff ? PlayerAnimationController.StaffReadyPose : (float)weapon.Stance;

    public static IEnumerable<WeaponDefinition> MeleeWeapons() =>
        AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/_Project" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(weapon => weapon != null && weapon.Kind == WeaponKind.Melee && weapon.Stance != WeaponStance.Bow);

    private static Context Load()
    {
        var context = new Context { Controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) };
        foreach (AnimatorControllerLayer layer in context.Controller.layers)
        {
            if (layer.name == FullBodyLayer)
            {
                foreach (ChildAnimatorState state in layer.stateMachine.states)
                {
                    context.FullBody[state.state.name] = state.state;
                }
            }

            foreach (ChildAnimatorState state in layer.stateMachine.states)
            {
                if (layer.name == "Base" && state.state.name == "Combat Idle")
                {
                    context.ReadyTree = state.state.motion as BlendTree;
                }

                if (layer.name == "Upper Body" && state.state.name == "Guard")
                {
                    context.GuardTree = state.state.motion as BlendTree;
                }

                if (layer.name == "Base" && state.state.name == "Idle")
                {
                    context.ExploringIdle = state.state.motion as AnimationClip;
                }

                if (layer.name == "Base" && state.state.name == "Combat Locomotion")
                {
                    context.CombatLocomotion = state.state.motion as BlendTree;
                }

                if (layer.name == "Arms" && state.state.motion is AnimationClip hold)
                {
                    context.Holds[state.state.name] = hold;
                }
            }
        }

        return context;
    }

    private static bool Playable(Context context, AttackDefinition attack) =>
        attack != null && !string.IsNullOrEmpty(attack.AnimationState) && context.FullBody.TryGetValue(attack.AnimationState, out AnimatorState state)
        && state.motion is AnimationClip;

    private static AnimationClip ClipOf(Context context, AttackDefinition attack) => (AnimationClip)context.FullBody[attack.AnimationState].motion;
    private static bool Mirrored(Context context, AttackDefinition attack) => context.FullBody[attack.AnimationState].mirror;

    // Clip seconds at the blow and when the swing has recovered (the next one starts), as the attack's speeds play it.
    public static float ClipAtHit(AttackDefinition attack) =>
        attack.AnimationSpeed * Mathf.Max(0f, attack.HitDelay - attack.StrikeLead) + attack.StrikeSpeed * attack.StrikeLead;

    private static float ClipAtRecovery(AttackDefinition attack, AnimationClip clip) =>
        Mathf.Min(clip.length, ClipAtHit(attack) + attack.FollowThroughSpeed * Mathf.Max(0f, attack.RecoveryTime - attack.HitDelay));

    private static float[] StartPose(Context context, AttackDefinition attack) => Pose(context, ClipOf(context, attack), 0f, Mirrored(context, attack));

    private static float[] HandoffPose(Context context, AttackDefinition attack)
    {
        AnimationClip clip = ClipOf(context, attack);
        return Pose(context, clip, ClipAtRecovery(attack, clip), Mirrored(context, attack));
    }

    // Where the swing leaves for the ready pose: its state's exit to Empty.
    private static float[] EndPose(Context context, AttackDefinition attack)
    {
        AnimatorState state = context.FullBody[attack.AnimationState];
        AnimationClip clip = (AnimationClip)state.motion;
        AnimatorStateTransition exit = state.transitions.FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Empty" && t.hasExitTime);
        float exitAt = exit != null ? exit.exitTime : 0.92f;
        float normalized = exitAt > 1f ? Mathf.Repeat(exitAt, 1f) : exitAt;
        return Pose(context, clip, normalized * clip.length, state.mirror);
    }

    // How fast the swing moves through its wind-up, and through its strike, in muscle units a second as played.
    private static float WindupRate(Context context, AttackDefinition attack)
    {
        float hit = Mathf.Max(0.05f, ClipAtHit(attack));
        return Travel(context, attack, 0f, hit) / Mathf.Max(0.05f, attack.HitDelay);
    }

    private static float StrikeRate(Context context, AttackDefinition attack)
    {
        float lead = Mathf.Max(0.03f, attack.StrikeLead);
        float hit = ClipAtHit(attack);
        return Travel(context, attack, Mathf.Max(0f, hit - attack.StrikeSpeed * lead), hit) / lead;
    }

    private static float Travel(Context context, AttackDefinition attack, float from, float to)
    {
        const int Steps = 16;
        AnimationClip clip = ClipOf(context, attack);
        bool mirror = Mirrored(context, attack);
        float sum = 0f;
        for (int i = 0; i < Steps; i++)
        {
            sum += Distance(Pose(context, clip, Mathf.Lerp(from, to, i / (float)Steps), mirror), Pose(context, clip, Mathf.Lerp(from, to, (i + 1) / (float)Steps), mirror));
        }

        return sum;
    }

    // Exploring: the plain idle's body, its arms the carry hold for the weapon (the Arms layer's, out of a fight).
    private static float[] ExploringPose(Context context, WeaponDefinition weapon)
    {
        if (context.ExploringIdle == null)
        {
            return null;
        }

        float[] pose = Pose(context, context.ExploringIdle, context.ExploringIdle.length * 0.25f, false);
        string hold = weapon.ReadyPose == WeaponReadyPose.Staff ? "Hold Staff"
            : weapon.Stance == WeaponStance.TwoHanded ? "Hold Two Handed" : weapon.Stance == WeaponStance.Polearm ? "Hold Polearm" : null;
        if (hold != null && context.Holds.TryGetValue(hold, out AnimationClip clip))
        {
            float[] arms = Pose(context, clip, clip.length * 0.25f, false);
            for (int i = 37; i < MuscleCount; i++)
            {
                pose[i] = arms[i];
            }
        }

        return pose;
    }

    // Walking in a fight: the stance's combat strafe walking forward (Combat Locomotion > Combat Legs > Combat Strafe).
    private static float[] WalkingPose(Context context, WeaponDefinition weapon)
    {
        BlendTree legs = context.CombatLocomotion?.children.Select(c => c.motion as BlendTree)
            .FirstOrDefault(tree => tree != null && tree.name.EndsWith(weapon.Stance.ToString()));
        BlendTree strafe = legs?.children.Select(c => c.motion as BlendTree).FirstOrDefault(tree => tree != null && tree.name.StartsWith("Combat Strafe"));
        if (strafe == null)
        {
            return null;
        }

        ChildMotion forward = strafe.children.Where(c => c.motion is AnimationClip && c.position.y > 0.5f && Mathf.Abs(c.position.x) < 0.1f)
            .OrderBy(c => c.position.y).FirstOrDefault();
        return forward.motion is AnimationClip walk ? Pose(context, walk, walk.length * 0.25f, forward.mirror) : null;
    }

    // The tree child at a ReadyPose value (a step tree: the nearest threshold).
    private static float[] TreePose(Context context, BlendTree tree, float value)
    {
        if (tree == null || tree.children.Length == 0)
        {
            return null;
        }

        ChildMotion child = tree.children.OrderBy(c => Mathf.Abs(c.threshold - value)).First();
        return child.motion is AnimationClip clip ? Pose(context, clip, clip.length * 0.25f, child.mirror) : null;
    }

    /// <summary>The body's Humanoid muscles (spine to head, arms) of a clip at a time, mirrored as a mirrored state plays it.</summary>
    public static float[] Pose(AnimationClip clip, float time, bool mirror) => Pose(new Context(), clip, time, mirror);

    private static float[] Pose(Context context, AnimationClip clip, float time, bool mirror)
    {
        if (!context.Curves.TryGetValue(clip, out AnimationCurve[] curves))
        {
            curves = new AnimationCurve[MuscleCount];
            string[] names = HumanTrait.MuscleName;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Animator))
                {
                    continue;
                }

                int index = System.Array.IndexOf(names, binding.propertyName);
                if (index >= 0 && index < MuscleCount)
                {
                    curves[index] = AnimationUtility.GetEditorCurve(clip, binding);
                }
            }

            context.Curves[clip] = curves;
        }

        var pose = new float[MuscleCount];
        for (int i = 0; i < MuscleCount; i++)
        {
            pose[i] = curves[i] != null ? curves[i].Evaluate(time) : 0f;
        }

        if (!mirror)
        {
            return pose;
        }

        // Mirrored: left and right limbs trade places, and the trunk's sideways bends and twists turn the other way.
        var mirrored = (float[])pose.Clone();
        for (int i = 0; i < 9; i++)
        {
            mirrored[37 + i] = pose[46 + i];
            mirrored[46 + i] = pose[37 + i];
        }

        for (int i = 0; i < 8; i++)
        {
            mirrored[21 + i] = pose[29 + i];
            mirrored[29 + i] = pose[21 + i];
        }

        foreach (int side in SideMuscles)
        {
            mirrored[side] = -pose[side];
        }

        return mirrored;
    }

    /// <summary>Distance between two poses over the body and arms (not the eyes, jaw or legs).</summary>
    public static float Distance(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < MuscleCount; i++)
        {
            if (i >= 15 && i < 37)
            {
                continue;
            }

            float d = a[i] - b[i];
            sum += d * d;
        }

        return Mathf.Sqrt(sum);
    }

    // ---------------------------------------------------------------- baking

    [MenuItem("Safe Haven/Tools/Fit Attack Blends")]
    private static void BakeFromMenu() => Debug.Log(Bake());

    /// <summary>Writes each attack's Blend In (the longest any way into it needs, within the room before its strike) and each swing
    /// state's settle back to the ready pose (its exits to Empty on the swing layers). Returns a report, the ways that can't be
    /// made smooth first.</summary>
    public static string Bake()
    {
        List<Link> links = Measure(out List<Settle> settles);
        var report = new List<string>();
        foreach (Link link in links.Where(l => !l.Smooth))
        {
            report.Add("NOT SMOOTH " + link);
        }

        foreach (Settle settle in settles.Where(s => !s.Smooth))
        {
            report.Add("NOT SMOOTH " + settle);
        }

        foreach (IGrouping<AttackDefinition, Link> into in links.GroupBy(l => l.To))
        {
            AttackDefinition attack = into.Key;
            float need = into.Max(l => l.Need);
            float room = into.Min(l => l.Room);
            // Rounded up to what it needs, never past the room before the strike.
            float blend = Mathf.Min(Mathf.Ceil(Mathf.Max(need, 0.1f) * 100f - 0.001f) / 100f, Mathf.Floor(room * 100f + 0.001f) / 100f);
            var data = new SerializedObject(attack);
            SerializedProperty field = data.FindProperty("blendIn");
            if (!Mathf.Approximately(field.floatValue, blend))
            {
                field.floatValue = blend;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(attack);
                AssetDatabase.SaveAssetIfDirty(attack);
                report.Add($"{attack.name}: blend in {blend:0.00} s");
            }
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        int exits = 0;
        foreach (IGrouping<string, Settle> state in settles.GroupBy(s => s.State))
        {
            float need = state.Max(s => s.Need);
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != FullBodyLayer && layer.name != "Upper Body" && layer.name != "Attack Arms")
                {
                    continue;
                }

                AnimatorState found = layer.stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == state.Key);
                if (found == null)
                {
                    continue;
                }

                foreach (AnimatorStateTransition exit in found.transitions)
                {
                    if (exit.destinationState != null && exit.destinationState.name == "Empty" && exit.hasExitTime
                        && Mathf.Abs(exit.duration - need) > 0.005f)
                    {
                        exit.duration = Mathf.Round(need * 100f) / 100f;
                        EditorUtility.SetDirty(exit);
                        exits++;
                    }
                }
            }
        }

        if (exits > 0)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            report.Add($"{exits} exits to the ready pose refitted");
        }

        report.Insert(0, $"{links.Count} ways into attacks, {links.Count(l => !l.Smooth)} not smooth; {settles.Count} ways out, {settles.Count(s => !s.Smooth)} not smooth");
        return string.Join("\n", report);
    }
}
