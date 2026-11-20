using System;
using UnityEngine;

/// <summary>
/// A monster's strike clip measured on its own body (MonsterAnimationDriver's Strike Clips, written by Safe Haven › Tools › Fit Monster
/// Strikes): where its wind-up ends (the top of the backswing) and where the blow meets, in clip seconds.
/// </summary>
[Serializable]
public struct MonsterStrikeClip
{
    public string state;
    public float windupEnd;
    public float contact;
    public float length;
}

/// <summary>
/// How a monster's strike clip is spread over its attack's Hit Delay and Recovery Time (the user, 2026-11-20: a slow wind-up, a fast
/// strike, a follow-through, for every monster that strikes, not only the Lost Ones). The gameplay timing stays as it is; only the clip
/// is fitted to it:
/// <list type="bullet">
/// <item>the raise plays no slower than <see cref="SlowestRaise"/>: a long telegraph on a short clip doesn't crawl in slow motion, it
/// raises, then hangs near the top of the backswing (the last <see cref="HangShare"/> of the wind-up) until the strike;</item>
/// <item>the strike, from the top to the contact, plays at <see cref="StrikeSpeed"/> (faster when the hit leaves too little time), so the
/// blow lands on the contact;</item>
/// <item>the follow-through plays at the clip's own momentum (0.9 to 1.3), slower only to last until the recovery.</item>
/// </list>
/// Pure arithmetic so it can be tested (MonsterStrikePhasesTests); the same shape as the player's (CombatSwingTiming).
/// </summary>
public readonly struct MonsterStrikePhases
{
    public const float StrikeSpeed = 1.25f;
    public const float SlowestRaise = 0.4f;
    public const float FastestRaise = 1.4f;
    public const float HangShare = 0.15f;
    public const float SlowestFollow = 0.9f;
    public const float FastestFollow = 1.3f;
    /// <summary>A follow-through slowed to reach the recovery goes no slower than this: past it the tail reads as a stall.</summary>
    public const float StalledFollow = 0.6f;

    /// <summary>Speed of the raise, from the first frame to the hang.</summary>
    public readonly float RaiseSpeed;
    /// <summary>Seconds after the start the hang begins (the raise's end); equal to <see cref="StrikeAt"/> when there is no hang.</summary>
    public readonly float HangAt;
    public readonly float HangSpeed;
    /// <summary>Seconds after the start the strike begins (the Hit Delay less the strike's lead).</summary>
    public readonly float StrikeAt;
    public readonly float Strike;
    public readonly float Follow;

    private MonsterStrikePhases(float raise, float hangAt, float hang, float strikeAt, float strike, float follow)
    {
        RaiseSpeed = raise;
        HangAt = hangAt;
        HangSpeed = hang;
        StrikeAt = strikeAt;
        Strike = strike;
        Follow = follow;
    }

    /// <summary>Clip seconds reached <paramref name="seconds"/> after the start (no hold).</summary>
    public float ClipAt(float seconds, float hitDelay)
    {
        float t = Mathf.Max(0f, seconds);
        float clip = RaiseSpeed * Mathf.Min(t, HangAt);
        clip += HangSpeed * Mathf.Clamp(t - HangAt, 0f, StrikeAt - HangAt);
        clip += Strike * Mathf.Clamp(t - StrikeAt, 0f, hitDelay - StrikeAt);
        return clip + Follow * Mathf.Max(0f, t - hitDelay);
    }

    /// <summary>The phases that land <paramref name="clip"/>'s contact at <paramref name="hitDelay"/>.</summary>
    public static MonsterStrikePhases Fit(MonsterStrikeClip clip, float hitDelay, float recovery)
    {
        float hit = Mathf.Max(0.05f, hitDelay);
        float contact = Mathf.Clamp(clip.contact, 0.02f, Mathf.Max(0.02f, clip.length));
        float top = Mathf.Clamp(clip.windupEnd, 0f, contact);
        float follow = FollowSpeed(clip.length - contact, recovery - hit);

        // A clip already moving from its first frame has no wind-up to shape: it plays evenly to the contact.
        float swing = contact - top;
        if (top < 0.06f || swing < 0.02f)
        {
            return Even(contact, hit, follow);
        }

        // The strike starts no sooner than a held wind-up would hold it (MonsterAttacks.HoldAtShare of the hit): a feint holds at the
        // top of the backswing, not in the middle of the blow (review 2026-11-20).
        float strike = StrikeSpeed;
        float lead = swing / strike;
        float longestLead = hit * (1f - MonsterAttacks.HoldAtShare);
        if (lead > longestLead)
        {
            lead = longestLead;
            strike = swing / lead;
        }

        float windup = hit - lead;
        float raise = top / windup;
        if (raise > FastestRaise)
        {
            // Too little time before the blow: the raise goes no faster than this and the strike takes what is left.
            raise = FastestRaise;
            lead = hit - top / raise;
            if (lead < 0.02f)
            {
                return Even(contact, hit, follow);
            }

            windup = hit - lead;
            strike = swing / lead;
        }

        if (raise > strike)
        {
            return Even(contact, hit, follow);
        }

        if (raise >= SlowestRaise)
        {
            return new MonsterStrikePhases(raise, windup, raise, windup, strike, follow);
        }

        // A long telegraph: raise at the slowest pace that still reads as moving, then hang near the top until the strike.
        float raised = top * (1f - HangShare);
        float hangAt = raised / SlowestRaise;
        return new MonsterStrikePhases(SlowestRaise, hangAt, (top - raised) / (windup - hangAt), windup, strike, follow);
    }

    private static MonsterStrikePhases Even(float contact, float hit, float follow)
    {
        float even = contact / hit;
        return new MonsterStrikePhases(even, hit, even, hit, even, follow);
    }

    private static float FollowSpeed(float tail, float chain)
    {
        if (chain <= 0.05f || tail <= 0f)
        {
            return 1f;
        }

        float follow = Mathf.Clamp(tail / chain, SlowestFollow, FastestFollow);
        // A tail too short to last until the recovery plays slower still, just enough to reach it (not to a stall).
        return tail / follow < chain ? Mathf.Max(StalledFollow, tail / chain) : follow;
    }
}
