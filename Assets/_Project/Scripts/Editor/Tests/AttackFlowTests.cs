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
        // Each of an attack's three blends (from the ready pose, out of the guard, in a chain) is what its ways in of that kind need.
        var stale = new List<string>();
        foreach (IGrouping<AttackDefinition, AttackFlow.Link> into in AttackFlow.Links().GroupBy(link => link.To))
        {
            foreach ((string context, float baked) in new[] { ("ready", into.Key.BlendInFromReady), ("guard", into.Key.BlendInFromGuard), ("chain", into.Key.BlendIn) })
            {
                AttackFlow.Link[] ways = into.Where(link => AttackFlow.ContextOf(link) == context).ToArray();
                float wanted = ways.Length > 0 ? AttackFlow.BlendFor(ways) : 0f;
                if (Mathf.Abs(baked - wanted) > 0.006f)
                {
                    stale.Add($"{into.Key.name}: {context} {baked:0.00} s, wants {wanted:0.00} s");
                }
            }
        }

        Assert.IsEmpty(stale, "run Safe Haven › Tools › Fit Attack Blends\n" + string.Join("\n", stale));
    }

    [Test]
    public void OutOfTheGuard_SwingsPickUpWhereTheyDipLeast()
    {
        Dictionary<AttackDefinition, float> best = AttackFlow.GuardStarts();
        Assert.Greater(best.Count, 5, "attacks a press out of the guard starts");
        string[] stale = best.Where(entry => Mathf.Abs(entry.Key.FromGuardStart - entry.Value) > 0.001f)
            .Select(entry => $"{entry.Key.name}: out of the guard from {entry.Key.FromGuardStart:0.0}, measured best {entry.Value:0.0}").ToArray();
        Assert.IsEmpty(stale, "run Safe Haven › Tools › Fit Attack Blends\n" + string.Join("\n", stale));
    }

    // Two heavies in a row flow as a chain's light swings do (the user, 2026-11-20): the second picks up where the first ended.
    [Test]
    public void OutOfAHeavy_TheNextHeavyPicksUpWhereItDipsLeast()
    {
        Dictionary<AttackDefinition, float> best = AttackFlow.HeavyStarts();
        Assert.Greater(best.Count, 5, "heavies a heavy chain starts");
        string[] stale = best.Where(entry => Mathf.Abs(entry.Key.FromHeavyStart - entry.Value) > 0.001f)
            .Select(entry => $"{entry.Key.name}: out of a heavy from {entry.Key.FromHeavyStart:0.0}, measured best {entry.Value:0.0}").ToArray();
        Assert.IsEmpty(stale, "run Safe Haven › Tools › Fit Attack Blends\n" + string.Join("\n", stale));
        Assert.IsTrue(AttackFlow.Links().Any(link => link.From.EndsWith(AttackFlow.HeavyChain)), "heavy chains are measured ways in");
    }

    // The dip measure can fail: a blend that passes through the ready pose dips by the whole way, one that moves away from it not at all.
    [Test]
    public void TheDip_IsTheDropTowardTheReadyPose()
    {
        var ready = new float[55];
        var raised = new float[55];
        var other = new float[55];
        raised[48] = 1f;
        other[48] = -1f;
        Assert.AreEqual(1f, AttackFlow.Dip(raised, other, ready), 0.001f, "through the ready pose");
        var higher = new float[55];
        higher[48] = 2f;
        Assert.AreEqual(0f, AttackFlow.Dip(raised, higher, ready), 0.001f, "away from it");
    }

    // Two kinds of way in, measured apart: an opener from the ready pose needs less than the chain's worst link into it, often.
    [Test]
    public void ReadyAndChainWaysIn_AreTold()
    {
        List<AttackFlow.Link> links = AttackFlow.Links();
        Assert.IsTrue(links.Any(AttackFlow.IsFromReady), "ways in from the ready pose");
        Assert.IsTrue(links.Any(link => !AttackFlow.IsFromReady(link)), "ways in from a swing");
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
}
