using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Every way into and out of the player's attacks is smooth (the user, 2026-11-20: "make sure all attack animations transition
/// smoothly and fit in a chain"): measured on the clips by AttackFlow, against AttackFlowRules. A new clip, a reordered chain or
/// retimed numbers that break it fail here; run Safe Haven › Tools › Fit Attack Blends after a change that only needs new blends.
/// </summary>
public sealed class AttackFlowTests
{
    [Test]
    public void EveryWayIntoAnAttack_BlendsNoFasterThanTheSwing_AndIsOverBeforeItsStrike()
    {
        List<AttackFlow.Link> links = AttackFlow.Links();
        // Ready pose into every opener, heavy, backstep, art and charged blow; each chain link and its wrap; a light into the heavy
        // follow-up; the guard into the counters: some 280 over the moveset. Fewer means the measuring found nothing to measure.
        Assert.Greater(links.Count, 200, "ways into attacks measured");
        string[] rough = links.Where(link => !link.Smooth).Select(link => link.ToString()).ToArray();
        Assert.IsEmpty(rough, "these need a longer blend than there is before their strike: change the clip, the chain's order or the ready pose\n" + string.Join("\n", rough));
    }

    [Test]
    public void EveryAttack_BlendsInAsMeasured()
    {
        string[] stale = AttackFlow.Links()
            .GroupBy(link => link.To)
            .Where(into => into.Key.BlendIn + 0.006f < Mathf.Min(into.Max(l => l.Need), into.Min(l => l.Room)) || into.Key.BlendIn > into.Min(l => l.Room) + 0.006f)
            .Select(into => $"{into.Key.name}: blend in {into.Key.BlendIn:0.00} s, needs {into.Max(l => l.Need):0.00} s within {into.Min(l => l.Room):0.00} s")
            .ToArray();
        Assert.IsEmpty(stale, "run Safe Haven › Tools › Fit Attack Blends\n" + string.Join("\n", stale));
    }

    [Test]
    public void EverySwing_SettlesIntoTheReadyPose()
    {
        List<AttackFlow.Settle> settles = AttackFlow.Settles();
        Assert.Greater(settles.Count, 100, "ways out of attacks measured");
        string[] rough = settles.Where(settle => !settle.Smooth).Select(settle => settle.ToString()).ToArray();
        Assert.IsEmpty(rough, "these end too far from the weapon's ready pose to settle within " + AttackFlowRules.LongestSettle + " s\n" + string.Join("\n", rough));

        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(AttackFlow.ControllerPath);
        var full = controller.layers.First(l => l.name == "Combat Full Body").stateMachine.states.Select(s => s.state).ToDictionary(s => s.name);
        var tooQuick = new List<string>();
        foreach (IGrouping<string, AttackFlow.Settle> state in settles.GroupBy(s => s.State))
        {
            float need = state.Max(s => s.Need);
            foreach (var exit in full[state.Key].transitions.Where(t => t.destinationState != null && t.destinationState.name == "Empty" && t.hasExitTime))
            {
                if (exit.duration + 0.006f < need)
                {
                    tooQuick.Add($"{state.Key}: settles over {exit.duration:0.00} s, needs {need:0.00} s");
                }
            }
        }

        Assert.IsEmpty(tooQuick, "run Safe Haven › Tools › Fit Attack Blends\n" + string.Join("\n", tooQuick));
    }

    // The rule can fail: a big change of pose on a quick swing needs more blend than there is room for, a small one doesn't.
    [Test]
    public void TheRule_RejectsAPopOnAQuickSwing()
    {
        float room = AttackFlowRules.BlendRoom(0.25f, 0.08f);
        Assert.Greater(AttackFlowRules.BlendNeeded(6f, 10f, 30f), room, "a 6-unit change of pose can't blend in within 0.14 s at 20 a second");
        Assert.Less(AttackFlowRules.BlendNeeded(1f, 10f, 30f), room);
        // A swing that moves faster than the cap may be blended into as fast as half its strike.
        Assert.Less(AttackFlowRules.BlendNeeded(4f, 10f, 160f), AttackFlowRules.BlendNeeded(4f, 10f, 30f));
    }

    // The poses compared are the clip's, mirrored as a mirrored state plays it: a one-sided swing mirrored is far from itself, and
    // mirroring twice gives it back.
    [Test]
    public void Poses_AreMirroredAsTheStatePlaysThem()
    {
        AnimationClip swing = AssetDatabase.LoadAllAssetsAtPath("Assets/Quaternius/Universal Animation Library 2/UAL2_Standard.fbx")
            .OfType<AnimationClip>().First(c => c.name == "Sword_Regular_A");
        float at = swing.length * 0.5f;
        float[] plain = AttackFlow.Pose(swing, at, false);
        float[] mirrored = AttackFlow.Pose(swing, at, true);
        Assert.Greater(AttackFlow.Distance(plain, mirrored), 1f, "a right-handed slash mirrored is a left-handed one");
        Assert.AreEqual(0f, AttackFlow.Distance(plain, plain), 1e-5f);
    }

    // The fists stand in the UAL punches' own guard (where the jab starts and the hook's recovery ends), keeping the idle's breathing.
    [Test]
    public void TheFistsReadyPose_IsThePunchesGuard()
    {
        var ready = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackFlow.FistsReadyPath);
        Assert.IsNotNull(ready, AttackFlow.FistsReadyPath);
        Assert.IsTrue(ready.isLooping);
        AnimationClip jab = AssetDatabase.LoadAllAssetsAtPath("Assets/Quaternius/Universal Animation Library/UAL1_Standard.fbx")
            .OfType<AnimationClip>().First(c => c.name == "Punch_Jab");
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatIdle01.fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
        float fromReady = AttackFlow.Distance(AttackFlow.Pose(ready, ready.length * 0.25f, false), AttackFlow.Pose(jab, 0f, false));
        float fromIdle = AttackFlow.Distance(AttackFlow.Pose(idle, idle.length * 0.25f, false), AttackFlow.Pose(jab, 0f, false));
        Assert.Less(fromReady, 0.6f, "the jab starts from the fists' ready pose");
        Assert.Greater(fromIdle, fromReady * 2f, "the plain KI idle is the pose it replaced");
    }
}
