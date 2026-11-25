using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Every player swing reads as a committed blow (CombatSwingTiming; the user, 2026-11-15: "like the character holds back while
/// swinging"): the blow lands on the clip's contact rather than after the weapon has passed, and since 2026-11-25 every phase plays at the
/// clip's own pace (CombatClipPace: the timing follows the clip, not damage per second). Measured on the player's rig, so a new attack or
/// clip is held to it too.
/// </summary>
public sealed class CombatSwingTimingTests
{
    // The user, 2026-11-25: smooth, responsive animation over holding damage per second; the clips from the packs feel smoothest at their
    // own speed. So no player attack or skill stretches its clip: the timing follows the clip (CombatClipPace), damage is tuned after.
    [Test]
    public void EveryPlayerAttackAndSkill_PlaysItsClipAtItsOwnPace()
    {
        var problems = new List<string>();
        var attacks = CombatClipPace.PlayerAttacks();
        Assert.Greater(attacks.Count, 60, "the player's attacks were found");
        foreach (AttackDefinition attack in attacks)
        {
            if (!CombatClipPace.AtClipPace(attack))
            {
                problems.Add($"{attack.name}: {attack.AnimationSpeed:0.00}/{attack.StrikeSpeed:0.00}/{attack.FollowThroughSpeed:0.00}x");
            }
        }

        foreach (SkillDefinition skill in CombatClipPace.Skills())
        {
            if (UnityEngine.Mathf.Abs(skill.AnimationSpeed - 1f) > 0.001f)
            {
                problems.Add($"{skill.name}: {skill.AnimationSpeed:0.00}x");
            }
        }

        Assert.IsEmpty(problems, "run Safe Haven › Tools › Combat › Play Swings At Their Clips' Pace\n" + string.Join("\n", problems));
    }

    [Test]
    public void EverySwing_StrikesAtItsContact_AtFullSpeed_AndFollowsThroughWithItsMomentum()
    {
        List<(AttackDefinition attack, UnityEngine.AnimationClip clip)> swings = CombatSwingTiming.PlayerSwings();
        Assert.Greater(swings.Count, 40, "the player's swings were found");
        var problems = new List<string>();
        int measuredSwings = 0;
        foreach ((AttackDefinition attack, UnityEngine.AnimationClip clip) in swings)
        {
            if (!CombatSwingTiming.TryTime(attack, clip, out CombatSwingTiming.Timing measured))
            {
                continue;
            }

            measuredSwings++;
            float hit = attack.HitDelay;
            float lead = UnityEngine.Mathf.Min(attack.StrikeLead, hit);
            float clipAtHit = attack.AnimationSpeed * (hit - lead) + attack.StrikeSpeed * lead;
            if (UnityEngine.Mathf.Abs(clipAtHit - measured.Contact) > 0.03f)
            {
                problems.Add($"{attack.name}: lands at {clipAtHit:0.00} s of {clip.name}, its contact is {measured.Contact:0.00}");
            }

            if (lead > 0f && (attack.StrikeSpeed < 0.99f || attack.StrikeSpeed * lead < measured.Contact - measured.WindupEnd - 0.03f))
            {
                problems.Add($"{attack.name}: the swing ({measured.WindupEnd:0.00}-{measured.Contact:0.00}) plays at {attack.StrikeSpeed:0.00}x for {attack.StrikeSpeed * lead:0.00} s of clip");
            }

            // Slower than Slowest Follow only where the clip's tail needs it to last until the recovery (the tool's own measure).
            float slowest = UnityEngine.Mathf.Min(CombatSwingTiming.SlowestFollow, measured.FollowThrough);
            if (attack.FollowThroughSpeed < slowest - 0.01f || attack.FollowThroughSpeed > CombatSwingTiming.FastestFollow + 0.01f)
            {
                problems.Add($"{attack.name}: follow-through at {attack.FollowThroughSpeed:0.00}x");
            }
        }

        // TryTime gives up on a swing it can't measure (no player rig, no hand bone, a near-instant hit), and gives up on every
        // one at once if the rig is gone: counting the swings found would then pass having judged none.
        Assert.Greater(measuredSwings, swings.Count / 2, $"only {measuredSwings} of {swings.Count} swings could be measured");
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    // The follow-through plays no slower than Slowest Follow (0.9x), so a clip whose tail is short against its recovery could run out
    // and blend to idle while the swing is still recovering (review 2026-11-16): the character would stand idle, yet unable to act.
    [Test]
    public void EverySwingClip_LastsUntilItsRecovery()
    {
        List<(AttackDefinition attack, UnityEngine.AnimationClip clip)> swings = CombatSwingTiming.PlayerSwings();
        Assert.Greater(swings.Count, 40, "the player's swings were found");
        var problems = new List<string>();
        foreach ((AttackDefinition attack, UnityEngine.AnimationClip clip) in swings)
        {
            float hit = attack.HitDelay;
            float lead = UnityEngine.Mathf.Min(attack.StrikeLead, hit);
            float clipAtHit = attack.AnimationSpeed * (hit - lead) + attack.StrikeSpeed * lead;
            float leaves = CombatSwingTiming.BlendsOutAt(attack, clip);
            float endsAt = hit + UnityEngine.Mathf.Max(0f, leaves - clipAtHit) / UnityEngine.Mathf.Max(0.01f, attack.FollowThroughSpeed);
            if (endsAt < attack.RecoveryTime - 0.02f)
            {
                problems.Add($"{attack.name}: {clip.name} blends out at {endsAt:0.00} s, the swing recovers at {attack.RecoveryTime:0.00} s "
                    + $"(follow-through {attack.FollowThroughSpeed:0.00}x over {leaves - clipAtHit:0.00} s of clip)");
            }
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }
}
