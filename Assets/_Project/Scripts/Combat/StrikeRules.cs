using UnityEngine;

/// <summary>
/// The arithmetic of how a blow lands, kept pure so it can be tested: the wind-up speed that keeps a swing's timing when its
/// strike is sped up, the order a sweep reaches its targets, whether an attacker stands behind a target, how hard a blow is
/// and which way what it strikes flies. PlayerCombat, CombatFeedback and the ragdoll read it.
/// </summary>
public static class StrikeRules
{
    /// <summary>
    /// The speed the build-up must play at so that a clip reaching contact at <paramref name="contact"/> seconds still lands
    /// <paramref name="landsAfter"/> seconds after it starts, when its last <paramref name="strikeClip"/> seconds play at
    /// <paramref name="strikeSpeed"/>. Timing, and so damage per second, stays what it was; only the shape of the swing changes.
    /// </summary>
    public static float WindupSpeed(float contact, float landsAfter, float strikeClip, float strikeSpeed)
    {
        strikeClip = Mathf.Clamp(strikeClip, 0f, contact);
        float buildUp = landsAfter - strikeClip / Mathf.Max(0.01f, strikeSpeed);
        return buildUp > 0.001f ? Mathf.Clamp((contact - strikeClip) / buildUp, 0.05f, 3f) : 3f;
    }

    /// <summary>How far through the sweep (0 first, 1 last) a target at <paramref name="toTarget"/> is reached.</summary>
    public static float SweepPosition(Vector3 direction, Vector3 toTarget, float arc, SlashSweep sweep)
    {
        direction.y = 0f;
        toTarget.y = 0f;
        if (arc <= 0f || direction.sqrMagnitude < 1e-6f || toTarget.sqrMagnitude < 1e-6f)
        {
            return 0f;
        }

        float angle = Vector3.SignedAngle(direction, toTarget, Vector3.up);
        float through = Mathf.Clamp01((angle + arc * 0.5f) / arc);
        return sweep == SlashSweep.LeftToRight ? through : 1f - through;
    }

    /// <summary>Seconds a sweep of <paramref name="arc"/> degrees takes, when a half circle takes <paramref name="halfCircleSeconds"/>.</summary>
    public static float SweepSeconds(float arc, float halfCircleSeconds) => Mathf.Clamp01(arc / 180f) * Mathf.Max(0f, halfCircleSeconds);

    /// <summary>True when <paramref name="attacker"/> stands within <paramref name="arc"/> degrees of straight behind the target.</summary>
    /// <summary>A finishing blow is on: it staggers, it can take one, and the player is close behind it.</summary>
    public static bool CanFinish(bool staggered, bool canBeFinished, Vector3 target, Vector3 targetForward, Vector3 attacker, float behindArc, float reach)
    {
        Vector3 between = target - attacker;
        between.y = 0f;
        return staggered && canBeFinished && between.magnitude <= reach && IsBehind(target, targetForward, attacker, behindArc);
    }

    /// <summary>Where the attacker stands for a finishing blow: straight behind the target, this far off.</summary>
    public static Vector3 FinishSpot(Vector3 target, Vector3 targetForward, float gap)
    {
        targetForward.y = 0f;
        targetForward = targetForward.sqrMagnitude > 0.0001f ? targetForward.normalized : Vector3.forward;
        return target - targetForward * gap;
    }

    public static bool IsBehind(Vector3 target, Vector3 targetForward, Vector3 attacker, float arc)
    {
        Vector3 toAttacker = attacker - target;
        toAttacker.y = 0f;
        targetForward.y = 0f;
        if (toAttacker.sqrMagnitude < 1e-6f || targetForward.sqrMagnitude < 1e-6f)
        {
            return false;
        }

        return Vector3.Angle(-targetForward, toAttacker) <= arc * 0.5f;
    }

    /// <summary>
    /// How hard a blow struck, from 0 to 1: the share of the target's health it took, how much the attack weighs (its Impact) and
    /// whether it was a strong or a killing one. Blood and ragdolls scale with it.
    /// </summary>
    public static float HitForce(float healthShare, float impact, bool strong, bool killed)
    {
        float force = 0.2f + Mathf.Clamp01(healthShare) * 0.8f + (strong ? 0.2f : 0f) + (killed ? 0.15f : 0f);
        return Mathf.Clamp01(force * Mathf.Max(0.25f, impact));
    }

    /// <summary>True when an attack pressed now is a running attack: running long enough, and toward where the attack aims.</summary>
    public static bool IsRunningAttack(float runningFor, float needed, float angleToAim, float widestAngle) =>
        runningFor >= needed && angleToAim <= widestAngle;

    /// <summary>The fight goes on while an enemy is after the player, however long since a blow was traded; otherwise it ends after
    /// the timeout (0: never).</summary>
    public static bool FightIsOver(float sinceLastActivity, float timeout, bool hunted) =>
        !hunted && timeout > 0f && sinceLastActivity >= timeout;

    /// <summary>
    /// The way what a blow strikes flies: along the blow, carried sideways by the sweep of a wide swing (a thrust goes straight)
    /// and lifted a little. Normalised.
    /// </summary>
    public static Vector3 SprayDirection(Vector3 hitDirection, float arc, SlashSweep sweep, float lift)
    {
        hitDirection.y = 0f;
        hitDirection = hitDirection.sqrMagnitude > 1e-6f ? hitDirection.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, hitDirection);
        float across = Mathf.Clamp01(arc / 180f) * 0.7f * (sweep == SlashSweep.LeftToRight ? 1f : -1f);
        return (hitDirection + right * across + Vector3.up * lift).normalized;
    }
}
