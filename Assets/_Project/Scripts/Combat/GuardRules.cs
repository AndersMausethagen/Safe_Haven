using UnityEngine;

/// <summary>How a guard took a hit.</summary>
public enum GuardOutcome
{
    /// <summary>Not guarded: the hit lands in full.</summary>
    None,
    /// <summary>Caught on the guard: part of it gets through and it costs stamina.</summary>
    Blocked,
    /// <summary>The guard went up just before the hit: nothing gets through and the attacker staggers.</summary>
    Parried,
    /// <summary>Not enough stamina to hold it: the guard breaks and the hit lands in full.</summary>
    GuardBroken,
}

/// <summary>Something on a target that can stop hits before they land (the player's guard). Damageable asks every
/// guard on its object before applying a hit.</summary>
public interface IHitGuard
{
    /// <summary>Decides what happens to <paramref name="damage"/>; <paramref name="letThrough"/> is the share that still
    /// lands (1 when not guarded).</summary>
    GuardOutcome Guard(in DamageInfo damage, out float letThrough);
}

/// <summary>The guard's decision as a plain function, so it can be tested without a scene.</summary>
public static class GuardRules
{
    /// <param name="facing">The way the guard faces (horizontal).</param>
    /// <param name="hitDirection">The way the hit travels, from attacker to target.</param>
    /// <param name="raisedFor">Seconds since the guard went up.</param>
    public static GuardOutcome Resolve(bool guarding, float raisedFor, Vector3 facing, Vector3 hitDirection, float blockArc,
        float parryWindow, float stamina, float staminaCost)
    {
        if (!guarding)
        {
            return GuardOutcome.None;
        }

        facing.y = 0f;
        Vector3 fromAttacker = -hitDirection;
        fromAttacker.y = 0f;
        if (facing.sqrMagnitude > 1e-6f && fromAttacker.sqrMagnitude > 1e-6f && Vector3.Angle(facing, fromAttacker) > blockArc * 0.5f)
        {
            return GuardOutcome.None; // from behind or the side: the guard doesn't cover it
        }

        if (raisedFor <= parryWindow)
        {
            return GuardOutcome.Parried;
        }

        return stamina >= staminaCost ? GuardOutcome.Blocked : GuardOutcome.GuardBroken;
    }
}
