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
    /// Broken poise and parried attacks call this. In a strike that ends in a topple (Monster Attack › Topple After: the
    /// Heartroot's Take Root) it is that topple instead, for the strike's Open After.
    /// </summary>
    public void Stagger(float seconds)
    {
        // An Unyielding elite's armour layer takes no stagger until it breaks.
        if (definition == null || CurrentState == State.Dead || seconds <= 0f || IsUnyielding)
        {
            return;
        }

        // Cancelled by the stagger, the strike would be spent (an opener) and the kill window it ends in would never come.
        if (!toppling && ToppleAfterNow > 0f)
        {
            Topple(-transform.forward, seconds, false);
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
    /// that breaks it (two parries, or one and some blows). A friend's copy in co-op keeps no poise: there it reels at once, and the
    /// host's game is told (<see cref="RemotePoiseBlow"/>), which takes the share off the real one (a break staggers or topples it for everyone).
    /// </summary>
    public void Parried(float staggerSeconds)
    {
        if (definition == null || CurrentState == State.Dead)
        {
            return;
        }

        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        if (IsUnyielding)
        {
            Recoil(rules.ParryRecoilSeconds);
            return;
        }

        switch (GuardRules.ParryResponseOf(definition.ParryResponse, IsHeavyweight))
        {
            case ParryResponse.Stagger:
                Stagger(staggerSeconds);
                break;
            case ParryResponse.PoiseDamage:
                if (IsRemote)
                {
                    RemotePoiseBlow?.Invoke(this, PoiseBlow.Parry);
                    Recoil(rules.ParryRecoilSeconds);
                }
                // A share of its own poise, not of what topples have grown it to: growth makes the next topple take more parries too.
                else if (poise != null && CurrentState != State.Staggered && poise.Hit(poise.BaseMax * rules.ParryPoiseShare, Time.time))
                {
                    if (Topples)
                    {
                        Topple(-transform.forward, definition.ToppleSeconds, true);
                    }
                    else
                    {
                        Stagger(staggerSeconds);
                    }
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
    /// <paramref name="seconds"/>, but it isn't staggered (no backstab opening, no extra damage taken). A strike that ends in a topple
    /// (Topple After) isn't broken off: it goes down for its Open After.</summary>
    public void Recoil(float seconds)
    {
        if (definition == null || CurrentState == State.Dead || CurrentState == State.Staggered || seconds <= 0f)
        {
            return;
        }

        if (CurrentState == State.Attacking && ToppleInsteadOfBreakingOff())
        {
            return;
        }

        if (CurrentState == State.Attacking)
        {
            attacks.Cancel();
            pose?.CancelAttack();
            SetState(State.Chasing);
            RaiseInterrupted();
        }

        attacks.DelayAtLeast(seconds);
        pose?.Hit(-transform.forward, 0.8f, Time.time);
    }

    /// <summary>Wears its poise down without a blow (a shield block knocking it back); a break staggers it as a blow's would.</summary>
    public void TakePoise(float amount)
    {
        if (IsRemote || poise == null || amount <= 0f || CurrentState == State.Staggered || CurrentState == State.Dead)
        {
            return;
        }

        if (poise.Hit(amount * MarkScale, Time.time))
        {
            if (Topples)
            {
                Topple(-transform.forward, definition.ToppleSeconds, true);
            }
            else
            {
                Stagger(CombatTechniqueSettings.Current.StaggerSeconds);
            }
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
        IsFelled = felling;
        // Only a topple's own stagger is a topple: any other (a phase's reel begun anew, a parry) ends one.
        toppledNow = toppling;
        strikeToppledNow = toppling && toppleForStrike;
        weaponGuardUntil = float.NegativeInfinity;
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
        attacks.DelayAtLeast(0.35f);
        SetState(State.Chasing);
    }

    // Being hit by something it would fight makes the monster fight back, even from outside its detection range: without
    // this, an arrow from beyond its chase range was forgotten the next moment.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        // Before it reacts: a hit from the shadows is judged by how things stood when it landed.
        LastHitAmbushed = damageable.DamageTakenMultiplier > 1f && IsUnawareOf(BlowOrigin(damage));
        // A raised shield taking a blow: the arm jolts and the body is shoved back a little.
        if (result.Blocked && ShieldArcNow > 0f && !IsWeaponGuarding)
        {
            pose?.Hit(damage.Direction, 0.35f, Time.time);
            ShieldBlocked?.Invoke();
        }

        // Burning ticks don't jolt the body; strong blows rock it hardest.
        if (pose != null && result.Damage > 0f && !result.Blocked && (damage.Technique & HitTechnique.OverTime) == 0)
        {
            bool strong = result.IsCritical || result.Killed
                || (damage.Technique & (HitTechnique.Heavy | HitTechnique.Finisher | HitTechnique.Counter)) != 0;
            // The recoil grows with the blow's poise damage, so a hammer rocks it more than a dagger (reactions by weight).
            float weight = poise != null ? Mathf.Clamp(0.45f + damage.Poise / poise.Max * 1.5f, 0.45f, 1.2f) : 0.6f;
            pose.Hit(damage.Direction, strong ? Mathf.Max(1f, weight) : weight, Time.time);
        }

        // Read before the interrupt, which ends the strike and lets its action go.
        MonsterAttack breaking = CurrentState == State.Attacking ? attacks.Action : null;
        bool broken = false;
        if (CurrentState == State.Attacking && !IsUnyielding && attacks.TryInterrupt(result.HealthLost, BreaksThrough(damage)))
        {
            pose?.CancelAttack();
            SetState(State.Chasing);
            attacks.Delay(CombatTechniqueSettings.Current.InterruptDelay);
            AnyInterrupted?.Invoke(this, damage.Source);
            RaiseInterrupted();
            broken = true;
            // A strike that ends in a topple (Topple After) goes down at once for its Open After, as its end would have had it.
            if (breaking != null && breaking.toppleAfter && breaking.openAfter > 0f && !result.Killed)
            {
                Topple(-transform.forward, breaking.openAfter, false, true);
            }
        }

        if ((damage.Technique & HitTechnique.Backstab) != 0)
        {
            OnBackstabbed(result.Killed);
        }

        // A friend's copy of the host's creature (co-op) keeps no poise of its own: the blow goes to the host's game, whose
        // creature's poise it wears down, and the stagger comes back from there for everyone. A boss stage's reel this very blow began
        // (ReelForStage, raised before this) doesn't keep it from counting: it stays down at least what the reel had left.
        NoteMark(damage);
        bool stageReel = TakeStageReel();
        if (!IsRemote && poise != null && (CurrentState != State.Staggered || stageReel) && CurrentState != State.Dead && !result.Killed)
        {
            float reelUntil = stageReel ? staggerUntil : float.NegativeInfinity;
            TakeBlowOnPoise(damage, result);
            if (stageReel && CurrentState == State.Staggered && staggerUntil < reelUntil)
            {
                staggerUntil = reelUntil;
            }
        }

        // Broken in its wind-up, it pays (Monster Attack › Reel When Broken): it reels, open to blows and backstabs, rather than only
        // waiting a moment; after the blow's poise, which a reel begun first threw away. A topple that poise brought on stands as it is,
        // and a stagger it brought on lasts at least the reel. On a friend's copy the stagger is told to the host (Stagger), which reels
        // for everyone.
        if (broken && breaking != null && breaking.reelWhenBroken > 0f && !breaking.toppleAfter && CurrentState != State.Dead && !result.Killed
            && !IsToppled)
        {
            StaggerAtLeast(breaking.reelWhenBroken);
        }

        NoteLightBlow(damage, result);

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
        if (CanTarget(target) && !atStructure && TakeGrudge(attacker, attackerTarget))
        {
            provokedUntil = Time.time + PerceptionSettings.Current.ProvokedSeconds;
            return;
        }

        if (!CanTarget(target) || (atStructure && definition.BreaksWalls))
        {
            spottingByBlow = true;
            SetTarget(attackerTarget, null);
            spottingByBlow = false;
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

        JudgeAmbushFrom(BlowOrigin(damage));

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

        GuardOutcome? weapon = WeaponGuard(damage, out letThrough);
        if (weapon.HasValue)
        {
            return weapon.Value;
        }

        if (CurrentState != State.Chasing || GuardRules.ResolveShield(ShieldArcNow, transform.forward, damage.Direction, damage.Technique) != GuardOutcome.Blocked)
        {
            return GuardOutcome.None;
        }

        letThrough = definition.ShieldLetsThrough;
        return GuardOutcome.Blocked;
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        bool wasStaggered = CurrentState == State.Staggered;
        damageable.DamageTakenMultiplier = 1f;
        ClearTarget();
        movement.Halt();
        // A strike under way ends with it: a pouncer's leap height no longer holds the body up where it died.
        attacks.Cancel();
        pose?.CancelAttack();
        SetState(State.Dead);
        reelingTell?.Clear();
        ClearAnswers();
        if (pack != null)
        {
            pack.MemberFell(this);
        }

        ShakeTheOthers(killingBlow, wasStaggered);
        // Its death cry carries: others near enough come to see what happened (a big creature's further).
        if (!IsRemote)
        {
            float size = Mathf.Clamp(transform.lossyScale.y * (TryGetComponent(out CapsuleCollider body) ? body.height : 2f) / 2f, 0.5f, 3f);
            // Killed unaware, it hardly cries out (Perception Settings › Ambush Death Cry).
            float cry = LastHitAmbushed ? PerceptionSettings.Current.AmbushDeathCry : 1f;
            WorldNoise.Emit(transform.position, PerceptionSettings.Current.DeathCryNoise * size * cry);
        }
    }
}
