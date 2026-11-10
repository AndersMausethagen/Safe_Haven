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

    /// <summary>
    /// The stamina a block costs: a share of what the blow is worth to the one blocking it, which is its damage after their
    /// armor and resistances (<paramref name="worth"/>), as a hit that got through would be. Going by the damage as it
    /// arrived made blocking dearer with every raid tier (enemy damage is up to 3.4 times tier I's, stamina is not), until
    /// one blow broke any guard whatever was worn.
    /// </summary>
    public static float BlockCost(float worth, float staminaPerDamage, float costScale) =>
        Mathf.Max(0f, worth) * Mathf.Max(0f, staminaPerDamage) * Mathf.Max(0f, costScale);

    /// <summary>A shield held up all the time, with no stamina or parry to it: it takes what comes from the front, unless the blow is
    /// heavy enough to go through. Its poise still pays for what it takes, so it can be worn down too.</summary>
    public static GuardOutcome ResolveShield(float arc, Vector3 facing, Vector3 hitDirection, HitTechnique technique)
    {
        const HitTechnique goesThrough = HitTechnique.Heavy | HitTechnique.WeaponArt | HitTechnique.ShieldBash | HitTechnique.OverTime;
        if (arc <= 0f || (technique & goesThrough) != 0)
        {
            return GuardOutcome.None;
        }

        return Resolve(true, 1f, facing, hitDirection, arc, 0f, 0f, 0f);
    }
}
