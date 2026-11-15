using UnityEngine;

/// <summary>
/// What happens to the player's swings when the fight answers: a dodge cuts a swing short, a real hit makes the player
/// flinch and lose a swing still winding up, a monster's parry throws them back, and a perfect dodge readies a counter.
/// Being hit, guarding and landing blows keep the game in Combat (PlayerCombatMode). Part of PlayerCombat, in its own
/// file.
/// </summary>
public sealed partial class PlayerCombat
{
    // Dodging is the way out of trouble, so it cuts a swing short; a swing that hadn't landed yet deals no damage. The
    // chain survives it, so dodge-cancelled combos still count if the next press is in time. Cutting a wind-up short locks
    // attacking for a moment (Cancel Lockout), so swing-dodge-swing can't fish for the dodge's invulnerability.
    private void OnDodged(Vector3 direction)
    {
        // A charging heavy dodged out of is a feint (Elden Ring), not a cancel fished for: no lockout.
        if (activeKind != SwingKind.None && hitPending && !charging && Rules.CancelLockout > 0f)
        {
            attackLockedUntil = Time.time + Rules.CancelLockout;
        }

        lastDodgeDirection = direction;

        EndSwing();
        queuedKind = SwingKind.None;
        pressTime = -1f;
    }

    // The player is no super soldier: a real hit (not blocked) staggers them for a moment, and a swing still winding up
    // is lost. Trading blows loses; read the telegraph, guard or get out of the way.
    private void OnHurt(DamageInfo damage, DamageResult result)
    {
        // Being attacked is a fight, whether or not the player hits back.
        if (!damage.IsOverTime && damage.Source != null)
        {
            EnterCombat();
        }

        if (result.Blocked || result.Killed || damage.IsOverTime || health == null)
        {
            return;
        }

        // A heavy hit buys a moment to answer before the next lands (anti-overwhelm, 2026-11-14).
        if (result.Damage >= health.MaxHealth * Rules.HeavyHitShare && Rules.HeavyHitInvulnerable > 0f)
        {
            heavyHitSafeUntil = Time.time + Rules.HeavyHitInvulnerable;
            if (!heavyHitSafe)
            {
                heavyHitSafe = true;
                health.AddInvulnerability(this);
            }
        }

        // Inside the grace after a flinch the hit still hurts, but doesn't stagger again.
        if (result.Damage < health.MaxHealth * Rules.FlinchShare || IsPushingThrough || Time.time < flinchGraceUntil)
        {
            return;
        }

        bool lostSwing = activeKind != SwingKind.None && hitPending;
        if (lostSwing)
        {
            EndSwing();
        }

        queuedKind = SwingKind.None;
        pressTime = -1f;
        flinchUntil = Time.time + Rules.FlinchSeconds;
        flinchGraceUntil = flinchUntil + Rules.FlinchGrace;
        movement.SetSpeedScale(Flinch, 0.25f);
        Flinched?.Invoke(Rules.FlinchSeconds, lostSwing);
    }

    /// <summary>Loses their footing for a moment (a troll's stomp shaking the ground under them, GroundImpacts): a flinch
    /// that breaks off a swing still winding up.</summary>
    public void Stumble(float seconds)
    {
        if (seconds <= 0f || IsFlinching || Time.time < flinchGraceUntil)
        {
            return;
        }

        bool lostSwing = activeKind != SwingKind.None && hitPending;
        if (lostSwing)
        {
            EndSwing();
        }

        queuedKind = SwingKind.None;
        pressTime = -1f;
        flinchUntil = Time.time + seconds;
        flinchGraceUntil = flinchUntil + Rules.FlinchGrace;
        movement.SetSpeedScale(Flinch, 0.25f);
        Flinched?.Invoke(seconds, lostSwing);
    }

    /// <summary>Thrown back by a monster's parry of the blow that just landed. The swing has already been spent, so it plays out.</summary>
    public void Rebuff(float seconds)
    {
        queuedKind = SwingKind.None;
        pressTime = -1f;
        flinchUntil = Time.time + seconds;
        movement.SetSpeedScale(Flinch, 0.25f);
        Flinched?.Invoke(seconds, false);
    }

    private void OnGuarded(DamageInfo damage, GuardOutcome outcome) => EnterCombat();

    // A hit evaded during the dodge's invulnerability is a perfect dodge: it readies a counter.
    private void OnEvaded(DamageInfo damage)
    {
        if (damage.Source != null)
        {
            EnterCombat();
        }

        if (dodge != null && dodge.IsDodging && damage.Source != null)
        {
            GrantCounter();
            PerfectDodge?.Invoke(damage);
        }
    }

    private void OnDamageDealt(IDamageable target, DamageInfo damage, DamageResult result) => mode.NoteActivity();

    /// <summary>Enters (or stays in) GameState.Combat as if attacking, e.g. while drawing a bow.</summary>
    public void KeepInCombat() => EnterCombat();

    private void EnterCombat() => mode.Enter();
}
