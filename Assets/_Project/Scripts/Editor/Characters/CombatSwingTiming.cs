using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// How a player swing's clip is spread over its gameplay timing (the user, 2026-11-15: "a lot of the attacks read as too weak, like the
/// character holds back while swinging"). Every clip has an anticipation, a short burst where the weapon actually swings, and a settle.
/// The attack's Hit Delay and Recovery Time (when the blow lands, when the next may start: cadence and damage per second) stay as they
/// are; only the clip is fitted to them:
/// <list type="bullet">
/// <item>the anticipation [0, end of wind-up] plays at Animation Speed: all the slowing that makes a blow readable lives here;</item>
/// <item>the swing [end of wind-up, contact] plays at Strike Speed, never slower than the clip (Strike Lead covers exactly it);</item>
/// <item>the blow lands on the clip's contact, not after the weapon has passed;</item>
/// <item>the follow-through plays at the clip's own momentum (0.9 to 1.3), fitted to the chain point only within that, unless the
/// clip's tail is too short to last until the recovery even at 0.9: then just slow enough to reach it.</item>
/// </list>
/// Measured on the player's rig (the weapon hand's speed), as CombatAnimationSetup measures contacts. Used once to re-time the attack
/// assets and by CombatSwingTimingTests to keep them so; not a builder.
/// </summary>
public static class CombatSwingTiming
{
    public const float SwingSpeed = 1.25f;
    public const float MostWindupSpeed = 1.4f;
    public const float SlowestFollow = 0.9f;
    public const float FastestFollow = 1.3f;
    private const string Controller = "Assets/_Project/Animation/Controllers/PlayerAnimator.controller";
    private const float Step = 1f / 60f;

    /// <summary>A clip's phases (seconds into it) and the speeds that fit them to an attack.</summary>
    public struct Timing
    {
        public float WindupEnd, Contact, Settle, Length;
        public float AnimationSpeed, StrikeSpeed, StrikeLead, FollowThrough;
        /// <summary>The hit comes so soon that the wind-up had to play faster than Most Windup Speed.</summary>
        public bool Strained;
        /// <summary>Clip time the blow lands at with these speeds.</summary>
        public float ClipAtHit(float hitDelay) => AnimationSpeed * (hitDelay - StrikeLead) + StrikeSpeed * StrikeLead;
    }

    /// <summary>Every attack the player swings with its own clip: weapons' (combos, heavies, rushes, counters, arts...) and art runes'.</summary>
    public static List<(AttackDefinition attack, AnimationClip clip)> PlayerSwings()
    {
        var clips = new Dictionary<string, AnimatorState>();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name == "Combat Full Body")
            {
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    clips[child.state.name] = child.state;
                }
            }
        }

        var attacks = new HashSet<AttackDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (weapon == null || weapon.IsBow)
            {
                continue;
            }

            SerializedProperty field = new SerializedObject(weapon).GetIterator();
            while (field.Next(true))
            {
                if (field.propertyType == SerializedPropertyType.ObjectReference && field.objectReferenceValue is AttackDefinition attack)
                {
                    attacks.Add(attack);
                }
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && item.RuneArt != null)
            {
                attacks.Add(item.RuneArt);
            }
        }

        var swings = new List<(AttackDefinition, AnimationClip)>();
        foreach (AttackDefinition attack in attacks.OrderBy(a => a.name))
        {
            if (string.IsNullOrEmpty(attack.AnimationState) || !clips.TryGetValue(attack.AnimationState, out AnimatorState state)
                || !(state.motion is AnimationClip clip) || clip.isLooping || LeavesLate(state) || clip.name.Contains("Kick"))
            {
                continue;
            }

            swings.Add((attack, clip));
        }

        return swings;
    }

    /// <summary>
    /// Clip time at which the attack's standing (Combat Full Body) state starts to blend out by itself: its earliest exit-time
    /// transition, else the clip's end. Past it the character heads back to idle whatever the swing's gameplay is doing.
    /// </summary>
    public static float BlendsOutAt(AttackDefinition attack, AnimationClip clip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name != "Combat Full Body")
            {
                continue;
            }

            foreach (ChildAnimatorState child in layer.stateMachine.states)
            {
                if (child.state.name == attack.AnimationState)
                {
                    float exit = child.state.transitions.Where(t => t.hasExitTime).Select(t => t.exitTime).DefaultIfEmpty(1f).Min();
                    return Mathf.Min(1f, exit) * clip.length;
                }
            }
        }

        return clip.length;
    }

    // A state left after more than one pass of its clip loops it (a whirlwind): it has no one contact to fit.
    private static bool LeavesLate(AnimatorState state) => state.transitions.Any(t => t.hasExitTime && t.exitTime > 1.01f);

    /// <summary>
    /// The clip's phases on the player's rig (the faster of the two hands), and the speeds that fit them to the attack's Hit Delay and
    /// Recovery Time. False when the clip has no clear swing.
    /// </summary>
    public static bool TryTime(AttackDefinition attack, AnimationClip clip, out Timing timing)
    {
        timing = default;
        if (attack == null || clip == null || attack.HitDelay < 0.08f)
        {
            return false;
        }

        // The weapon hand: the right, but for a clip made for the off hand.
        HumanBodyBones hand = clip.name.Contains("_Off") || clip.name.EndsWith("_L") ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        float[] speeds = HandSpeeds(clip, hand);
        if (speeds == null)
        {
            return false;
        }

        float contact = CombatAnimationSetup.DrivenContact(clip, hand);
        int c = Mathf.Clamp(Mathf.RoundToInt(contact / Step), 0, speeds.Length - 1);
        // The swing's own top speed is at or just before the contact.
        float peak = 0f;
        for (int i = Mathf.Max(0, c - 4); i <= c; i++)
        {
            peak = Mathf.Max(peak, speeds[i]);
        }

        // The wind-up ends where the swing's drive begins: walking back from the contact while the hand keeps getting slower (the foot
        // of the drive, the top of the backswing), but no further than where it falls under a fifth of the swing's top speed, so a
        // clip that starts from a still pose doesn't count its whole raise as the swing.
        // From the swing's top speed (the contact lies on or just past it).
        int top = c;
        for (int i = Mathf.Max(0, c - Mathf.RoundToInt(0.25f / Step)); i <= c; i++)
        {
            if (speeds[i] > speeds[top])
            {
                top = i;
            }
        }

        peak = speeds[top];
        int a = top;
        while (a > 1 && speeds[a - 1] <= speeds[a])
        {
            a--;
        }

        int slow = top;
        while (slow > 0 && speeds[slow] >= peak * 0.2f)
        {
            slow--;
        }

        a = Mathf.Max(a, slow);

        int f = c + 3;
        while (f < speeds.Length - 1 && speeds[f] >= peak * 0.2f)
        {
            f++;
        }

        float windupEnd = a * Step, settle = Mathf.Min(clip.length, f * Step);
        float swing = Mathf.Max(0.05f, contact - windupEnd);
        if (contact <= 0.02f || peak <= 0.01f)
        {
            return false;
        }

        float hit = attack.HitDelay;
        float tail = clip.length - contact;
        float chain = attack.RecoveryTime - hit;
        float follow = chain > 0.05f ? Mathf.Clamp(tail / chain, SlowestFollow, FastestFollow) : 1f;
        // A tail too short to last until the recovery even at Slowest Follow plays slower still, just enough to reach it: else the
        // clip ran out and blended to idle while the swing was still recovering (review 2026-11-16).
        if (chain > 0.05f && tail > 0f && tail / follow < chain)
        {
            follow = tail / chain;
        }

        // A clip that is already moving from its first frame has no wind-up to slow: it plays evenly to the contact.
        if (windupEnd < 0.06f)
        {
            float even = contact / hit;
            timing = new Timing
            {
                WindupEnd = windupEnd, Contact = contact, Settle = settle, Length = clip.length,
                AnimationSpeed = Mathf.Clamp(even, 0.05f, 3f), StrikeSpeed = Mathf.Clamp(even, 0.25f, 4f), StrikeLead = 0f,
                FollowThrough = follow, Strained = even > MostWindupSpeed,
            };
            return true;
        }

        float strikeSpeed = SwingSpeed;
        float lead = swing / strikeSpeed;
        if (lead > hit * 0.7f)
        {
            lead = hit * 0.7f;
            strikeSpeed = swing / lead;
        }

        float windupSpeed = windupEnd / Mathf.Max(0.01f, hit - lead);
        bool strained = false;
        if (windupSpeed > MostWindupSpeed)
        {
            // Too little time before the blow: the wind-up goes no faster than this, and the swing takes what is left.
            strained = true;
            windupSpeed = MostWindupSpeed;
            lead = Mathf.Max(0.02f, hit - windupEnd / windupSpeed);
            strikeSpeed = swing / lead;
        }

        // A blow so quick that even the wind-up has to run faster than the swing: the whole of it plays evenly at that pace.
        if (windupSpeed > strikeSpeed)
        {
            windupSpeed = strikeSpeed = contact / hit;
            lead = 0f;
        }

        timing = new Timing
        {
            WindupEnd = windupEnd, Contact = contact, Settle = settle, Length = clip.length,
            AnimationSpeed = Mathf.Clamp(windupSpeed, 0.05f, 3f),
            StrikeSpeed = Mathf.Clamp(strikeSpeed, 0.25f, 4f),
            StrikeLead = lead,
            FollowThrough = follow,
            Strained = strained,
        };
        return true;
    }

    // The hand's speed (character space) at each frame of the clip, on one copy of the player's rig.
    private static float[] HandSpeeds(AnimationClip clip, HumanBodyBones hand)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        Animator source = prefab != null ? prefab.GetComponentInChildren<Animator>() : null;
        if (source == null || source.avatar == null)
        {
            return null;
        }

        GameObject rig = Object.Instantiate(source.gameObject);
        rig.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var animator = rig.GetComponent<Animator>();
            animator.applyRootMotion = false;
            Transform bone = animator.GetBoneTransform(hand);
            if (bone == null)
            {
                return null;
            }

            int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length / Step) + 1);
            var positions = new Vector3[frames];
            for (int i = 0; i < frames; i++)
            {
                clip.SampleAnimation(rig, Mathf.Min(clip.length, i * Step));
                positions[i] = rig.transform.InverseTransformPoint(bone.position);
            }

            var speeds = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                int from = Mathf.Max(0, i - 2), to = Mathf.Min(frames - 1, i + 2);
                speeds[i] = (positions[to] - positions[from]).magnitude / Mathf.Max(Step, (to - from) * Step);
            }

            return speeds;
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }
    }
}
