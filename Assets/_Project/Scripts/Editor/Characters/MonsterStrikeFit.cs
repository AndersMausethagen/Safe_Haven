using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Measures every humanoid monster's strike clips on the body that plays them (the clip its override controller puts in each strike
/// state) and writes them to its MonsterAnimationDriver's Strike Clips, so MonsterStrikePhases can land each blow on its contact. One
/// attack is often struck by bodies playing different clips (the Draug's Cleave: a sword chop on the Draug, a shield bash on the
/// Shieldbearer), so the phases belong to the body, not to the attack. Not a builder: it only writes the measured list, and
/// MonsterStrikeFitTests fails when a list is out of date.
/// </summary>
public static class MonsterStrikeFit
{
    private const string Prefabs = "Assets/_Project/Prefabs";

    /// <summary>The strike clips each humanoid monster prefab should have, by prefab path.</summary>
    public static Dictionary<string, MonsterStrikeClip[]> Measure()
    {
        var measured = new Dictionary<AnimationClip, (bool ok, float windupEnd, float contact)>();
        var result = new Dictionary<string, MonsterStrikeClip[]>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var ai = prefab.GetComponent<MonsterAI>();
            var driver = prefab.GetComponentInChildren<MonsterAnimationDriver>(true);
            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (ai == null || ai.Definition == null || driver == null || animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                continue;
            }

            RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
            var overrides = runtime as AnimatorOverrideController;
            var controller = (overrides != null ? overrides.runtimeAnimatorController : runtime) as AnimatorController;
            if (controller == null)
            {
                continue;
            }

            var clips = new List<MonsterStrikeClip>();
            foreach (string name in StrikeStates(ai.Definition).Distinct().OrderBy(n => n, System.StringComparer.Ordinal))
            {
                AnimatorState state = Find(controller.layers[0].stateMachine, name);
                // Only a state whose pace the driver sets (ActionSpeed) can be played in phases.
                if (state == null || !(state.motion is AnimationClip clip) || !state.speedParameterActive || state.speedParameter != "ActionSpeed"
                    || Find(controller.layers[0].stateMachine, name + " Release") != null)
                {
                    continue;
                }

                if (overrides != null && overrides[clip] != null)
                {
                    clip = overrides[clip];
                }

                if (clip.isLooping)
                {
                    continue;
                }

                if (!measured.TryGetValue(clip, out var phases))
                {
                    bool ok = CombatSwingTiming.TryMeasureEitherHand(clip, out float windupEnd, out float contact);
                    phases = (ok, windupEnd, contact);
                    measured[clip] = phases;
                }

                if (!phases.ok)
                {
                    continue;
                }

                // In seconds of the state at speed 1: its own speed multiplies the attack's.
                float speed = Mathf.Max(0.05f, Mathf.Abs(state.speed));
                clips.Add(new MonsterStrikeClip
                {
                    state = name,
                    windupEnd = Round(phases.windupEnd / speed),
                    contact = Round(phases.contact / speed),
                    length = Round(clip.length / speed),
                });
            }

            result[path] = clips.ToArray();
        }

        return result;
    }

    [MenuItem("Safe Haven/Tools/Fit Monster Strikes")]
    public static void Fit()
    {
        var report = new List<string>();
        foreach (KeyValuePair<string, MonsterStrikeClip[]> entry in Measure())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Key);
            var driver = prefab.GetComponentInChildren<MonsterAnimationDriver>(true);
            if (Same(driver.StrikeClips, entry.Value))
            {
                continue;
            }

            var data = new SerializedObject(driver);
            SerializedProperty list = data.FindProperty("strikeClips");
            list.arraySize = entry.Value.Length;
            for (int i = 0; i < entry.Value.Length; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("state").stringValue = entry.Value[i].state;
                item.FindPropertyRelative("windupEnd").floatValue = entry.Value[i].windupEnd;
                item.FindPropertyRelative("contact").floatValue = entry.Value[i].contact;
                item.FindPropertyRelative("length").floatValue = entry.Value[i].length;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            report.Add($"{prefab.name}: {string.Join(", ", entry.Value.Select(c => $"{c.state} {c.windupEnd:0.00}/{c.contact:0.00}"))}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log(report.Count == 0 ? "Fit Monster Strikes: all current." : "Fit Monster Strikes:\n" + string.Join("\n", report));
    }

    /// <summary>Same states and phases, to 0.006 s.</summary>
    public static bool Same(IReadOnlyList<MonsterStrikeClip> a, IReadOnlyList<MonsterStrikeClip> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].state != b[i].state || Mathf.Abs(a[i].windupEnd - b[i].windupEnd) > 0.006f
                || Mathf.Abs(a[i].contact - b[i].contact) > 0.006f || Mathf.Abs(a[i].length - b[i].length) > 0.006f)
            {
                return false;
            }
        }

        return true;
    }

    // The states a monster's own strikes play: its attack, its actions' and their follow-ups'. A roar is a call, not a blow.
    private static IEnumerable<string> StrikeStates(MonsterDefinition definition)
    {
        var attacks = new List<AttackDefinition> { definition.Attack };
        foreach (MonsterAttack action in definition.Attacks)
        {
            attacks.Add(action.attack);
            if (action.followUps != null)
            {
                attacks.AddRange(action.followUps);
            }
        }

        return attacks.Where(a => a != null && !string.IsNullOrEmpty(a.AnimationState) && a.AnimationState != "Roar").Select(a => a.AnimationState);
    }

    private static AnimatorState Find(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == name)
            {
                return child.state;
            }
        }

        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorState found = Find(child.stateMachine, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static float Round(float seconds) => Mathf.Round(seconds * 1000f) / 1000f;
}
