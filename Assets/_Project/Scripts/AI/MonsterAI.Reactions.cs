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

        BeginStagger(seconds);
        if (IsRemote)
        {
            NoteStaggeredHere(seconds);
        }
    }

    /// <summary>
    /// Its strike was parried (PlayerGuard). By its Parry Response: an ordinary creature staggers for <paramref name="staggerSeconds"/>;
    /// a great beast, boss or elite loses the settings' Parry Poise Share of its poise and reels back a moment, staggering only when
    /// that breaks it (two parries, or one and some blows). A friend's copy in co-op keeps no poise, so there it only reels.
    /// </summary>
    public void Parried(float staggerSeconds)
    {
        if (definition == null || CurrentState == State.Dead)
        {
            return;
        }

        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        switch (GuardRules.ParryResponseOf(definition.ParryResponse, IsHeavyweight))
        {
            case ParryResponse.Stagger:
                Stagger(staggerSeconds);
                break;
            case ParryResponse.PoiseDamage:
                if (!IsRemote && poise != null && CurrentState != State.Staggered && poise.Hit(poise.Max * rules.ParryPoiseShare, Time.time))
                {
                    Stagger(staggerSeconds);
                }
                else
                {
                    Recoil(rules.ParryRecoilSeconds);
                }

                break;
            default:
                Recoil(rules.ParryRecoilSeconds);
                break;
        }
    }

    /// <summary>Reels back a moment: the strike in progress and what would follow it are broken off and it strikes again only after
    /// <paramref name="seconds"/>, but it isn't staggered (no backstab opening, no extra damage taken).</summary>
    public void Recoil(float seconds)
    {
        if (definition == null || CurrentState == State.Dead || CurrentState == State.Staggered || seconds <= 0f)
        {
            return;
        }

        if (CurrentState == State.Attacking)
        {
            attacks.Cancel();
            pose?.CancelAttack();
            SetState(State.Chasing);
        }

        attacks.Delay(seconds);
        pose?.Hit(-transform.forward, 0.8f, Time.time);
    }

    /// <summary>Wears its poise down without a blow (a shield block knocking it back); a break staggers it as a blow's would.</summary>
    public void TakePoise(float amount)
    {
        if (IsRemote || poise == null || amount <= 0f || CurrentState == State.Staggered || CurrentState == State.Dead)
        {
            return;
        }

        if (poise.Hit(amount, Time.time))
        {
            Stagger(CombatTechniqueSettings.Current.StaggerSeconds);
        }
    }

    // Combat Technique Settings › Break Through: a heavy swing, a counter, a finisher, a weapon art, or a blow whose poise damage is a good
    // share of its poise, breaks off a wind-up the strike's own Interruptible doesn't allow. Never a great beast's, boss's or elite's.
    private bool BreaksThrough(in DamageInfo damage)
    {
        return GuardRules.BreaksThrough(damage.Technique, damage.Poise, poise != null ? poise.Max : 0f,
            CombatTechniqueSettings.Current.BreakThroughPoiseShare, IsHeavyweight);
    }

    /// <summary>Staggers it for at least this long: a new stagger, or more of the one it is in (never less).</summary>
    public void StaggerAtLeast(float seconds)
    {
        if (CurrentState == State.Staggered)
        {
            staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds);
            return;
        }

        Stagger(seconds);
    }

    private void BeginStagger(float seconds)
    {
        attacks.Cancel();
        movement.Stop();
        staggerUntil = Time.time + seconds;
        pose?.Stagger(Time.time, seconds);
        // After the state, which clears what the last stagger added.
        SetState(State.Staggered);
        damageable.DamageTakenMultiplier = CombatTechniqueSettings.Current.StaggeredDamageTaken;
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
        if (IsRemote)
        {
            NoteStaggeredHere(seconds);
        }

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
            // The recoil grows with the blow's poise damage, so a hammer rocks it more than a dagger (reactions by weight).
            float weight = poise != null ? Mathf.Clamp(0.45f + damage.Poise / poise.Max * 1.5f, 0.45f, 1.2f) : 0.6f;
            pose.Hit(damage.Direction, strong ? Mathf.Max(1f, weight) : weight, Time.time);
        }

        if (CurrentState == State.Attacking && attacks.TryInterrupt(result.HealthLost, BreaksThrough(damage)))
        {
            pose?.CancelAttack();
            SetState(State.Chasing);
            attacks.Delay(CombatTechniqueSettings.Current.InterruptDelay);
            AnyInterrupted?.Invoke(this, damage.Source);
        }

        if ((damage.Technique & HitTechnique.Backstab) != 0)
        {
            OnBackstabbed(result.Killed);
        }

        // A friend's copy of the host's creature (co-op) keeps no poise of its own: the blow goes to the host's game, whose
        // creature's poise it wears down, and the stagger comes back from there for everyone.
        if (!IsRemote && poise != null && CurrentState != State.Staggered && CurrentState != State.Dead && !result.Killed
            && poise.Hit(damage.Poise, Time.time))
        {
            Stagger(CombatTechniqueSettings.Current.StaggerSeconds);
        }

        Combatant attacker = damage.Source;
        // Game runs from whatever struck it (MonsterAI.Timid).
        if (definition != null && definition.Timid && !IsRemote && !result.Killed)
        {
            Bolt(attacker != null ? attacker.transform.position : transform.position - damage.Direction);
        }

        if (definition == null || CurrentState == State.Dead || attacker == null)
        {
            return;
        }

        IDamageable attackerTarget = attacker.GetComponent<IDamageable>();
        if (!CanTarget(attackerTarget))
        {
            return;
        }

        // A wall-breaker at a wall turns on whoever hurts it (HomeAssault); it goes back to the walls when they stop.
        bool atStructure = target is Damageable structure && structure != null && structure.Category == TargetCategory.Structure;
        if (!CanTarget(target) || (atStructure && definition.BreaksWalls))
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
        // Its death cry carries: others near enough come to see what happened (a big creature's further).
        if (!IsRemote)
        {
            float size = Mathf.Clamp(transform.lossyScale.y * (TryGetComponent(out CapsuleCollider body) ? body.height : 2f) / 2f, 0.5f, 3f);
            WorldNoise.Emit(transform.position, PerceptionSettings.Current.DeathCryNoise * size);
        }
    }
}
