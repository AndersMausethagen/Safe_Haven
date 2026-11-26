using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The exaggeration pass on a swing (Docs/CombatFeelAnalysis_2026-11-26.md §3.6). The user, 2026-11-24: "the most important thing is to
/// have satisfying and smooth combat", and a clip may be exaggerated for feel. Pack heavies are made for realistic swordplay and from the
/// high camera read barely bigger than a light swing. This makes a copy of a Humanoid clip whose wind-up coils further: through the
/// wind-up each trunk and arm muscle is drawn further from where it will be at contact (the backswing, the anticipation), by Strength at
/// the top of the wind-up, easing in and back out, so the copy starts on the source's first pose and reaches its contact pose on the
/// same frame, over a longer path. (Drawn from the first pose instead, a backhand's "top" fell on the contact itself.) The blade at contact, the timings and everything after contact
/// stay the source's. Legs, head and fingers are untouched (the feet stay planted). Used like Blender's deeper wind-up pass, but from
/// data: change Strength and make it again.
/// </summary>
public static class SwingExaggeration
{
    public const string Folder = "Assets/_Project/Animation/Clips/Exaggerated";

    /// <summary>How much further the big blows coil (0.35: a third again as deep at the top of the wind-up). Raise it and run
    /// Safe Haven › Tools › Exaggerate Big Swings to make the copies again.</summary>
    public const float Strength = 0.35f;

    // The source state, its copy's state and clip, and the attack whose contact it keeps (the others playing the copy share that clip
    // and contact). The sword's charged spin-leap starts from its backswing top, and the axe's finisher left its chain no room to blend:
    // they keep their clips (2026-11-26).
    private static readonly (string from, string clip, string attack)[] Swings =
    {
        ("Sword Heavy", "Sword_Heavy_X", "Sword Heavy"),
        ("Sword Finisher", "Sword_Finisher_X", "Sword Finisher"),
        ("KI 1H Backhand", "KI_1H_Backhand_X", "Chop Heavy"),
    };

    /// <summary>Makes every copy again at <see cref="Strength"/> over its old asset (the states keep playing it), then refits the
    /// attack blends and the torso aim, which read the clips. Run CombatSwingTimingTests and AttackFlowTests after: a stronger coil can
    /// start the strike sooner (raise the attack's Strike Lead to the measured swing) or leave a chain too little room to blend.</summary>
    [MenuItem("Safe Haven/Tools/Exaggerate Big Swings")]
    public static void RemakeAll()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CombatAnimationSetup.PlayerControllerPath);
        var clips = new Dictionary<string, AnimationClip>();
        foreach (UnityEditor.Animations.AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name == CombatAnimationSetup.FullBodyLayer)
            {
                foreach (UnityEditor.Animations.ChildAnimatorState child in layer.stateMachine.states)
                {
                    clips[child.state.name] = child.state.motion as AnimationClip;
                }
            }
        }

        foreach ((string from, string clip, string attackName) in Swings)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>($"{CombatAnimationSetup.ComboFolder}/{attackName}.asset");
            Make(clips[from], clip, attack.HitDelay, Strength);
        }

        Debug.Log(AttackFlow.Bake());
        Debug.Log(SwingTorsoAimFit.Fit());
    }
    // Resampled at this rate where it is drawn out (the source keys may be sparse).
    private const float SampleRate = 60f;
    // The top of the wind-up is looked for between these shares of the way to contact: the first frames are the ready pose easing, the
    // last the strike itself.
    private const float TopSearchFrom = 0.15f;
    private const float TopSearchTo = 0.8f;

    /// <summary>True for the muscles drawn out: the trunk (spine, chest, upper chest: bend, tilt, twist), the shoulders and the arms.</summary>
    public static bool Drawn(string muscle) =>
        muscle.StartsWith("Spine ") || muscle.StartsWith("Chest ") || muscle.StartsWith("UpperChest ")
        || muscle.Contains("Shoulder ") || muscle.Contains(" Arm ") || muscle.Contains(" Forearm ");

    /// <summary>How much of the drawing-out applies at <paramref name="time"/>: 0 at the start, 1 at the top of the wind-up, 0 again at
    /// contact and after (smoothstep each way).</summary>
    public static float Weight(float time, float top, float contact)
    {
        if (time <= 0f || time >= contact)
        {
            return 0f;
        }

        return time <= top ? Mathf.SmoothStep(0f, 1f, time / Mathf.Max(0.001f, top))
            : Mathf.SmoothStep(1f, 0f, (time - top) / Mathf.Max(0.001f, contact - top));
    }

    /// <summary>The top of the wind-up: the hang before the strike, where the striking hand moves slowest on the player's own rig
    /// (muscle distance couldn't find it: in these clips the ready pose is already the furthest from contact).</summary>
    public static float WindupTop(AnimationClip clip, float contact, HumanBodyBones hand = HumanBodyBones.RightHand)
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        Animator source = player != null ? player.GetComponentInChildren<Animator>() : null;
        float best = contact * 0.5f;
        if (source == null || source.avatar == null)
        {
            return best;
        }

        GameObject rig = CombatAnimationSetup.BareRig(source);
        try
        {
            var animator = rig.GetComponent<Animator>();
            animator.applyRootMotion = false;
            Transform bone = animator.GetBoneTransform(hand);
            if (bone == null)
            {
                return best;
            }

            float step = 1f / SampleRate;
            var times = new List<float>();
            var speeds = new List<float>();
            for (float t = contact * TopSearchFrom; t <= contact * TopSearchTo; t += step)
            {
                clip.SampleAnimation(rig, t - step * 0.5f);
                Vector3 a = rig.transform.InverseTransformPoint(bone.position);
                clip.SampleAnimation(rig, t + step * 0.5f);
                times.Add(t);
                speeds.Add((rig.transform.InverseTransformPoint(bone.position) - a).magnitude / step);
            }

            // The last pause before the strike (the hang), not the stillest moment: a heavy grown from a held pose is stillest at its
            // start. Smoothed over three samples so one jittery frame isn't a pause.
            float Smoothed(int i) => (speeds[Mathf.Max(0, i - 1)] + speeds[i] + speeds[Mathf.Min(speeds.Count - 1, i + 1)]) / 3f;
            int slowest = 0;
            for (int i = 0; i < speeds.Count; i++)
            {
                if (Smoothed(i) < Smoothed(slowest))
                {
                    slowest = i;
                }
            }

            for (int i = speeds.Count - 2; i >= 1; i--)
            {
                if (Smoothed(i) <= Smoothed(i - 1) && Smoothed(i) <= Smoothed(i + 1))
                {
                    return times[i];
                }
            }

            return speeds.Count > 0 ? times[slowest] : best;
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }
    }

    /// <summary>
    /// Writes <paramref name="name"/>.anim to <see cref="Folder"/>: <paramref name="source"/> with its wind-up drawn out by
    /// <paramref name="strength"/> (0.35: a third again as deep) up to <paramref name="contact"/> seconds into the clip. Made again
    /// over an older copy, so the asset (and the states playing it) keep their GUID.
    /// </summary>
    public static AnimationClip Make(AnimationClip source, string name, float contact, float strength)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Animation/Clips", "Exaggerated");
        }

        string path = $"{Folder}/{name}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool made = clip == null;
        if (made)
        {
            clip = new AnimationClip();
        }
        else
        {
            clip.ClearCurves();
        }

        clip.name = name;
        clip.frameRate = SampleRate;
        float top = WindupTop(source, contact);
        BakeOnPlayerRig(source, clip, top, contact, strength);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(source));
        if (made)
        {
            AssetDatabase.CreateAsset(clip, path);
        }

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    // The humanoid bake: the source posed on the player's own rig every frame, read back as muscles (HumanPoseHandler) and written as
    // muscle and body curves, the drawn muscles moved by v + s·w(t)·(v − v(contact)). Copying the source's curve bindings instead was
    // lossy (a model clip carries muscle and bone curves both: at strength 0 the hand was 0.28 m off by contact).
    private static void BakeOnPlayerRig(AnimationClip source, AnimationClip clip, float top, float contact, float strength)
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        Animator template = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (template == null || template.avatar == null)
        {
            throw new System.InvalidOperationException("SwingExaggeration needs the player prefab's humanoid Animator.");
        }

        GameObject rig = CombatAnimationSetup.BareRig(template);
        try
        {
            var animator = rig.GetComponent<Animator>();
            animator.applyRootMotion = false;
            var handler = new HumanPoseHandler(animator.avatar, rig.transform);
            var pose = new HumanPose();
            int muscles = HumanTrait.MuscleCount;
            source.SampleAnimation(rig, contact);
            handler.GetHumanPose(ref pose);
            float[] atContact = (float[])pose.muscles.Clone();
            bool[] drawn = new bool[muscles];
            for (int m = 0; m < muscles; m++)
            {
                drawn[m] = Drawn(HumanTrait.MuscleName[m]);
            }

            int frames = Mathf.CeilToInt(source.length * SampleRate) + 1;
            var muscleKeys = new Keyframe[muscles][];
            for (int m = 0; m < muscles; m++)
            {
                muscleKeys[m] = new Keyframe[frames];
            }

            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(source.length, f / SampleRate);
                source.SampleAnimation(rig, t);
                handler.GetHumanPose(ref pose);
                float w = strength * Weight(t, top, contact);
                for (int m = 0; m < muscles; m++)
                {
                    float value = pose.muscles[m];
                    if (drawn[m] && w > 0f)
                    {
                        // Held within the muscle's range, but never cut below the source's own value: some clips go past ±1, and
                        // clamping those cut the source pose itself wherever the weight was above 0 (a hand 0.35 m off by contact).
                        float limit = Mathf.Max(1f, Mathf.Abs(value));
                        value = Mathf.Clamp(value + w * (value - atContact[m]), -limit, limit);
                    }

                    muscleKeys[m][f] = new Keyframe(t, value);
                }
            }

            // The body muscles, baked, under the names the clip itself uses (the trunk and limbs match HumanTrait's; the fingers don't:
            // "LeftHand.Thumb.1 Stretched" against "Left Thumb 1 Stretched", so they're copied below with the rest).
            var written = new HashSet<string>();
            var own = new HashSet<string>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type == typeof(Animator))
                {
                    own.Add(binding.propertyName);
                }
            }

            for (int m = 0; m < muscles; m++)
            {
                if (own.Contains(HumanTrait.MuscleName[m]))
                {
                    SetCurve(clip, HumanTrait.MuscleName[m], muscleKeys[m]);
                    written.Add(HumanTrait.MuscleName[m]);
                }
            }

            // Everything else the clip holds for a humanoid, as it is: the body's place and turn (RootT/RootQ), the translation DOFs,
            // the hand and foot goals, the fingers. Not the bones' own transform curves, which a copy would play on top.
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type == typeof(Animator) && !written.Contains(binding.propertyName))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(source, binding));
                }
            }

            handler.Dispose();
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }
    }

    // Linear between frames sampled at 60 a second: no tangent smoothing to move a swing's fastest frame.
    private static void SetCurve(AnimationClip clip, string property, Keyframe[] keys)
    {
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
    }
}
