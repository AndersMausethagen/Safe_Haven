using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Every player swing reads as a committed blow (CombatSwingTiming; the user, 2026-11-15: "like the character holds back while
/// swinging"): its clip's swing plays at least at the clip's own speed, the blow lands on the clip's contact rather than after the weapon
/// has passed, and the follow-through keeps the clip's momentum. The slowing that makes a blow readable stays in the anticipation.
/// Measured on the player's rig, so a new attack or clip is held to it too.
/// </summary>
public sealed class CombatSwingTimingTests
{
    [Test]
    public void EverySwing_StrikesAtItsContact_AtFullSpeed_AndFollowsThroughWithItsMomentum()
    {
        List<(AttackDefinition attack, UnityEngine.AnimationClip clip)> swings = CombatSwingTiming.PlayerSwings();
        Assert.Greater(swings.Count, 40, "the player's swings were found");
        var problems = new List<string>();
        foreach ((AttackDefinition attack, UnityEngine.AnimationClip clip) in swings)
        {
            if (!CombatSwingTiming.TryTime(attack, clip, out CombatSwingTiming.Timing measured))
            {
                continue;
            }

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

            if (attack.FollowThroughSpeed < CombatSwingTiming.SlowestFollow - 0.01f || attack.FollowThroughSpeed > CombatSwingTiming.FastestFollow + 0.01f)
            {
                problems.Add($"{attack.name}: follow-through at {attack.FollowThroughSpeed:0.00}x");
            }
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }
}
