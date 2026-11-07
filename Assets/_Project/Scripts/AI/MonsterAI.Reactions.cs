using UnityEngine;

/// <summary>
/// What blows do to a monster: its poise breaking into a stagger, holding still for a finishing blow, being hurt (it
/// turns on whoever hit it), its shield guarding blows from the front, and dying. Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    /// <summary>
    /// Makes it reel for <paramref name="seconds"/>: any attack it was winding up is cancelled (its cooldown still
    /// applies), it stands still and takes extra damage (<see cref="CombatTechniqueSettings.StaggeredDamageTaken"/>).
    /// Broken poise and parried attacks call this.
    /// </summary>
    public void Stagger(float seconds)
    {
        if (definition == null || CurrentState == State.Dead || seconds <= 0f)
        {
            return;
        }

        attacks.Cancel();
        movement.Stop();
        staggerUntil = Time.time + seconds;
        pose?.Stagger(Time.time, seconds);
        damageable.DamageTakenMultiplier = CombatTechniqueSettings.Current.StaggeredDamageTaken;
        SetState(State.Staggered);
        Staggered?.Invoke(seconds);
        AnyStaggered?.Invoke(this, seconds);
    }

    /// <summary>A finishing blow has it: it reels on, facing away, until the blow is done whatever its stagger had left.</summary>
    public void HoldForFinish(float seconds, float bladeIn)
    {
        if (CurrentState != State.Staggered)
        {
            return;
        }

        staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds);
        pose?.Stagger(Time.time, seconds);
        HeldForFinish?.Invoke(bladeIn);
    }

    private void UpdateStagger()
    {
        if (Time.time < staggerUntil)
        {
            return;
        }

        damageable.DamageTakenMultiplier = 1f;
        attacks.Delay(0.35f);
        SetState(State.Chasing);
    }

    // Being hit by something it would fight makes the monster fight back, even from outside its detection range: without
    // this, an arrow from beyond its chase range was forgotten the next moment.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        // Before it reacts: a hit from the shadows is judged by how things stood when it landed.
        LastHitAmbushed = damageable.DamageTakenMultiplier > 1f && IsUnaware;
        // Burning ticks don't jolt the body; strong blows rock it hardest.
        if (pose != null && result.Damage > 0f && !result.Blocked && (damage.Technique & HitTechnique.OverTime) == 0)
        {
            bool strong = result.IsCritical || result.Killed
                || (damage.Technique & (HitTechnique.Heavy | HitTechnique.Finisher | HitTechnique.Counter)) != 0;
            pose.Hit(damage.Direction, strong ? 1f : 0.6f, Time.time);
        }

        if (CurrentState == State.Attacking && attacks.TryInterrupt(result.HealthLost))
        {
            pose?.CancelAttack();
            SetState(State.Chasing);
        }

        if ((damage.Technique & HitTechnique.Backstab) != 0)
        {
            OnBackstabbed(result.Killed);
        }

        if (poise != null && CurrentState != State.Staggered && CurrentState != State.Dead && !result.Killed
            && poise.Hit(damage.Poise, Time.time))
        {
            Stagger(CombatTechniqueSettings.Current.StaggerSeconds);
        }

        Combatant attacker = damage.Source;
        if (definition == null || CurrentState == State.Dead || attacker == null)
        {
            return;
        }

        IDamageable attackerTarget = attacker.GetComponent<IDamageable>();
        if (!CanTarget(attackerTarget))
        {
            return;
        }

        if (!CanTarget(target))
        {
            SetTarget(attackerTarget, null);
        }

        if (target == attackerTarget)
        {
            provokedUntil = Time.time + PerceptionSettings.Current.ProvokedSeconds;
        }
    }

    /// <summary>Damageable asks before every hit: a parry of a swing it saw coming, or its shield while it hunts.</summary>
    public GuardOutcome Guard(in DamageInfo damage, out float letThrough)
    {
        letThrough = 1f;
        if (definition == null || damage.IsOverTime)
        {
            return GuardOutcome.None;
        }

        if (Time.time < parryUntil)
        {
            CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
            parryUntil = -1f;
            answerAt = Time.time + rules.AnswerDelay;
            if (damage.Source != null && damage.Source.TryGetComponent(out PlayerCombat swinger) && HorizontalDistance(swinger.transform.position) <= rules.ReadRange)
            {
                swinger.Rebuff(rules.ParriedFlinchSeconds);
            }

            letThrough = 0f;
            return GuardOutcome.Parried;
        }

        if (CurrentState != State.Chasing || GuardRules.ResolveShield(definition.ShieldArc, transform.forward, damage.Direction, damage.Technique) != GuardOutcome.Blocked)
        {
            return GuardOutcome.None;
        }

        letThrough = definition.ShieldLetsThrough;
        return GuardOutcome.Blocked;
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        damageable.DamageTakenMultiplier = 1f;
        ClearTarget();
        movement.Halt();
        SetState(State.Dead);
        pack?.MemberFell(this);
    }
}
