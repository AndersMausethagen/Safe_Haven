using UnityEngine;

/// <summary>
/// A swing from start to finish: starting it (cost, aim, the counter), its wind-up and heavy charge, following the
/// focused enemy and stepping into the blow, landing it on its targets in the order the blade reaches them, backstabs
/// and the recovery after. Part of PlayerCombat, in its own file.
/// </summary>
public sealed partial class PlayerCombat
{
    private void Begin(SwingKind kind, ChainPress judge)
    {
        queuedKind = SwingKind.None;
        int step = 0;
        int chain = weapon != null ? weapon.ComboLength : 1;
        AttackDefinition attack;
        HitTechnique technique = HitTechnique.None;
        switch (kind)
        {
            case SwingKind.Heavy:
                attack = weapon != null ? weapon.HeavyAttack : null;
                technique |= HitTechnique.Heavy;
                break;
            case SwingKind.ShieldBash:
                attack = shieldBash;
                technique |= HitTechnique.ShieldBash;
                break;
            case SwingKind.Art:
                attack = weapon != null ? weapon.WeaponArt : null;
                technique |= HitTechnique.WeaponArt;
                if (attack == null)
                {
                    return;
                }

                // Paid in Focus when there is enough (no stamina then), else in stamina (BaseCostOf).
                artOnStamina = focus != null && !focus.TrySpend(weapon.WeaponArtFocus);

                break;
            case SwingKind.Rush:
                // Out of a perfect dodge the counter is the better blow; a rush leads on into the chain's second swing.
                attack = CounterIsReady && weapon != null && weapon.CounterAttack != null ? weapon.CounterAttack : weapon != null ? weapon.RushAttack : null;
                if (dodge != null)
                {
                    dodge.CutShort();
                }

                break;
            default:
                step = ComboRules.NextStep(lastStep, chain, judge, fumbled);
                attack = weapon != null ? weapon.ComboStep(step) : null;
                if (CounterIsReady && weapon != null && weapon.CounterAttack != null)
                {
                    attack = weapon.CounterAttack;
                    step = 0;
                }

                break;
        }

        MonsterAI toFinish = kind == SwingKind.Light || kind == SwingKind.Rush ? FindFinishable() : null;
        if (toFinish != null)
        {
            // The weapon's own rush: same numbers and time as any, so a finishing blow changes the moment and not the balance.
            kind = SwingKind.Rush;
            attack = weapon.RushAttack;
            step = 0;
        }

        // Pay the full cost when the swing actually starts, rechecking affordability for buffered swings.
        if (stamina != null && !stamina.TrySpend(Mathf.Max(0f, CostOf(kind, attack) - heavyCredit)))
        {
            return;
        }

        fumbled = false;
        rhythm = kind == SwingKind.Light && step > 0 ? ComboRules.NextRhythm(rhythm, judge, Rules) : 0;
        bool finisher = kind == SwingKind.Light && chain > 1 && step == chain - 1;
        lastStep = kind == SwingKind.Rush ? 0 : kind == SwingKind.Light && !finisher ? step : -1;

        activeKind = kind;
        activeAttack = attack;
        activeWeaponDamage = kind == SwingKind.ShieldBash || weapon == null ? 0f : weapon.Damage;
        activeSpeed = ComboRules.SpeedMultiplier(rhythm, Rules);
        activeDamageMultiplier = ComboRules.DamageMultiplier(rhythm, Rules);
        activePoiseMultiplier = 1f;
        // A practised hand's Weapon Art has its weapon's flourish: a one-handed art cuts deeper, a two-handed one sunders.
        if (kind == SwingKind.Art && weapon != null && Techniques.HasFlourish)
        {
            TechniqueSettings flourish = TechniqueSettings.Current;
            if (weapon.Stance == WeaponStance.OneHanded)
            {
                activeDamageMultiplier *= flourish.FlurryDamage;
            }
            else if (weapon.Stance == WeaponStance.TwoHanded)
            {
                activePoiseMultiplier *= flourish.SunderPoise;
            }
        }
        activeForceCritical = false;
        if (rhythm > 0)
        {
            technique |= HitTechnique.Rhythm;
        }

        if (finisher)
        {
            technique |= HitTechnique.Finisher;
            activePoiseMultiplier *= 1.5f;
        }

        if (CounterIsReady && kind != SwingKind.ShieldBash)
        {
            technique |= HitTechnique.Counter;
            activeDamageMultiplier *= Rules.CounterDamageMultiplier;
            activePoiseMultiplier *= 2f;
            activeForceCritical = true;
            counterUntil = float.NegativeInfinity;
        }

        activeTechnique = technique;
        activeStartTime = Time.time;
        chargeHeld = 0f;
        charging = false;
        hitPending = attack != null;
        stepPending = attack != null && attack.Lunge > 0f;
        // Toward the aim, or the target it snaps to; while it winds up it may still follow the focused enemy.
        if (focusTarget != null)
        {
            focusTarget.Refresh();
        }

        activeDirection = toFinish != null ? toFinish.transform.forward : AimAssist(movement.AimDirection, attack, out _);
        movement.SnapFacing(activeDirection);
        if (toFinish != null)
        {
            stepPending = false;
            BeginFinish(toFinish, HitAt(attack, activeSpeed));
        }

        // Planted, facing the blow, until it lands: legs running under a swing and a body skating through one both look wrong.
        movement.FacingLocked = true;
        movement.SetSpeedScale(this, Rules.WindupMoveSpeed);
        if (!hitPending)
        {
            ReleaseFeet();
        }
        else if (kind == SwingKind.Rush && stepPending)
        {
            // A rush carries the movement it came with: the lunge runs for the whole wind-up instead of waiting for the strike.
            StepIntoBlow(HitAt(attack, activeSpeed));
        }

        EnterCombat();

        AttackDefinition looks = toFinish != null && Rules.FinishStab != null ? Rules.FinishStab : attack;
        current = new SwingInfo(kind, looks, step, chain, rhythm, activeSpeed, technique, activeDirection);
        SwingStarted?.Invoke(current);
        if (kind == SwingKind.ShieldBash)
        {
            ShieldBashPerformed?.Invoke();
        }
        else
        {
            AttackPerformed?.Invoke();
        }
    }

    // Silent Takedown: a blow from the shadows finishes an ordinary creature it has nearly done for anyway. Never a boss or
    // an elite, and never one that knows you are there.
    // <paramref name="damage"/> is the swing's multiplier so far, and what comes back is the multiplier to strike with.
    private float SilentTakedown(IDamageable target, in PendingHit pending, float damage)
    {
        if (!(target is Damageable body) || !body.TryGetComponent(out MonsterAI monster) || !monster.IsUnaware
            || body.GetComponent<EliteCreature>() != null || body.GetComponentInParent<BossEncounter>() != null
            || !Techniques.Has(PerkTechnique.SilentTakedown) || !body.TryGetComponent(out Combatant theirs))
        {
            return damage;
        }

        // What this blow is worth against this creature: as it leaves the hand (no lucky critical counted), then through the
        // creature's own defenses and what being caught unaware adds.
        float outgoing = DamageCalculator.CalculateOutgoing(combatant.Stats, combatant.Rules, pending.Attack, pending.WeaponDamage, target.Category, 1f, out _) * damage;
        float blow = DamageCalculator.Mitigate(outgoing, pending.Attack.DamageType, theirs.Stats, theirs.Rules) * body.DamageTakenMultiplier;
        if (!StrikeRules.IsTakedown(body.CurrentHealth, blow, TechniqueSettings.Current.TakedownReach))
        {
            return damage;
        }

        GameMessages.Post("Silent takedown.", MessageKind.Gain);
        Techniques.Used(PerkTechnique.SilentTakedown);
        // As hard as it takes to finish it and a little over, so the number shown is about what the creature had left.
        return damage * Mathf.Max(1f, body.CurrentHealth / blow * 1.05f);
    }

    // The swing's stamina, after the Melee skill: a beginner tires sooner, a master later.
    private float CostOf(SwingKind kind, AttackDefinition attack) =>
        BaseCostOf(kind, attack) * Mathf.Max(0.1f, 1f + combatant.Stats.Get(StatType.MeleeStaminaCost));

    private float BaseCostOf(SwingKind kind, AttackDefinition attack)
    {
        if (stamina == null)
        {
            return 0f;
        }

        if (kind == SwingKind.Art)
        {
            return artOnStamina ? Rules.ArtStaminaWithoutFocus : 0f;
        }

        if (attack != null && attack.StaminaCost >= 0f)
        {
            return attack.StaminaCost;
        }

        return kind == SwingKind.ShieldBash ? stamina.ShieldBashCost : kind == SwingKind.Heavy ? stamina.AttackCost * 2f : stamina.AttackCost;
    }

    private void UpdateSwing()
    {
        UpdateFinish();
        if (activeKind == SwingKind.None)
        {
            bool dodgeInTheWay = dodge != null && dodge.IsDodging && !(queuedKind == SwingKind.Rush && dodge.CanBeCutShort);
            if (queuedKind != SwingKind.None && !IsFlinching && !dodgeInTheWay && (guard == null || !guard.IsStunned))
            {
                Begin(queuedKind, queuedJudge);
            }

            return;
        }

        float hitAt = HitAt(activeAttack, activeSpeed);
        if (UpdateCharge(hitAt))
        {
            return;
        }

        float elapsed = ActiveElapsed;
        if (hitPending && elapsed < hitAt - LungeSeconds)
        {
            TrackFocus();
        }
        else if (hitPending && stepPending)
        {
            StepIntoBlow(hitAt - elapsed);
        }

        if (hitPending && elapsed >= hitAt)
        {
            hitPending = false;
            int hits = LandSwing();
            ReleaseFeet();
            SwingLanded?.Invoke(current, hits);
        }

        if (elapsed >= RecoverAt(activeAttack, activeSpeed))
        {
            SwingKind next = queuedKind;
            ChainPress judge = queuedJudge;
            EndSwing();
            if (next != SwingKind.None)
            {
                Begin(next, judge);
            }
        }
    }

    // A heavy swing held on waits at the top of its wind-up, where its strike would begin, and everything about it stands
    // still until the button is let go or the charge is full. True while it waits.
    private bool UpdateCharge(float hitAt)
    {
        float most = Rules.HeavyChargeSeconds;
        bool waits = activeKind == SwingKind.Heavy && hitPending && attackHeld && most > 0f && chargeHeld < most
            && ActiveElapsed >= hitAt - Mathf.Max(activeAttack.StrikeLead, LungeSeconds);
        if (!waits)
        {
            charging = false;
            return false;
        }

        charging = true;
        chargeHeld = Mathf.Min(most, chargeHeld + Time.deltaTime);
        TrackFocus();
        if (chargeHeld >= most)
        {
            charging = false;
            ChargeFull?.Invoke();
        }

        return charging;
    }

    // Until it steps into the blow, a swing keeps turning toward the enemy it is focused on.
    private void TrackFocus()
    {
        if (focusTarget == null || !focusTarget.HasFocus || Rules.SwingTracking <= 0f)
        {
            return;
        }

        Vector3 to = focusTarget.DirectionToTarget;
        activeDirection = Vector3.RotateTowards(activeDirection, to, Rules.SwingTracking * Mathf.Deg2Rad * Time.deltaTime, 0f);
        movement.SnapFacing(activeDirection);
    }

    // The last moment before the blow: the body travels with the strike, as far as the target (or half way with none), so the
    // step ends as it lands instead of sliding at the start and then standing still through the swing.
    private void StepIntoBlow(float secondsLeft)
    {
        stepPending = false;
        activeDirection = AimAssist(activeDirection, activeAttack, out float targetDistance);
        movement.SnapFacing(activeDirection);
        float distance = targetDistance >= 0f ? Mathf.Clamp(targetDistance - LungeStandOff, 0f, activeAttack.Lunge) : activeAttack.Lunge * 0.5f;
        float seconds = Mathf.Max(0.05f, secondsLeft);
        movement.Lunge(activeDirection * (distance / seconds), seconds);
    }

    private int LandSwing()
    {
        if (activeAttack == null)
        {
            return 0;
        }

        float charge = activeKind == SwingKind.Heavy ? Charge : 0f;
        float damage = activeDamageMultiplier * Mathf.Lerp(1f, Rules.HeavyChargeDamage, charge);
        float poise = activePoiseMultiplier * Mathf.Lerp(1f, Rules.HeavyChargePoise, charge);
        Vector3 origin = transform.position;
        HitDetector.FindTargets(origin, activeDirection, activeAttack, combatant.Faction, hitLayers, foundHits);

        float first = float.MaxValue;
        float sweep = StrikeRules.SweepSeconds(activeAttack.Arc, Rules.SweepSeconds);
        foreach (HitDetector.Hit hit in foundHits)
        {
            first = Mathf.Min(first, StrikeRules.SweepPosition(activeDirection, hit.Point - origin, activeAttack.Arc, activeAttack.SlashSweep));
        }

        foreach (HitDetector.Hit hit in foundHits)
        {
            float through = StrikeRules.SweepPosition(activeDirection, hit.Point - origin, activeAttack.Arc, activeAttack.SlashSweep);
            pendingHits.Add(new PendingHit
            {
                Hit = hit,
                At = Time.time + (through - first) * sweep,
                Attack = activeAttack,
                WeaponDamage = activeWeaponDamage,
                Origin = origin,
                Direction = activeDirection,
                Damage = damage,
                Poise = poise,
                Technique = activeTechnique,
                Critical = activeForceCritical,
            });
        }

        ApplyPendingHits();
        return foundHits.Count;
    }

    private void ApplyPendingHits()
    {
        for (int i = pendingHits.Count - 1; i >= 0; i--)
        {
            PendingHit pending = pendingHits[i];
            if (Time.time < pending.At)
            {
                continue;
            }

            pendingHits.RemoveAt(i);
            Strike(pending);
        }
    }

    private void Strike(in PendingHit pending)
    {
        IDamageable target = pending.Hit.Target;
        // It may have died, or gone, in the moment the blade took to reach it.
        if (target == null || (target is UnityEngine.Object living && living == null) || !target.IsAlive)
        {
            return;
        }

        float damage = pending.Damage;
        HitTechnique technique = pending.Technique;
        bool critical = pending.Critical;
        bool backstab = IsBackstab(target, out MonsterAI backstabbed);
        if (backstab)
        {
            // A stagger is earned, and pays most; catching it mid-strike pays less, and no sure critical.
            damage *= backstabbed.IsStaggered ? Rules.BackstabDamage : Rules.BackstabOnStrikeDamage;
            technique |= HitTechnique.Backstab;
            critical |= backstabbed.IsStaggered;
        }

        // The Melee skill scales every swing alike, before nothing else changes, so the order of blows (plain, backstab,
        // counter) holds at every level; a perk can add to a blow from the shadows.
        damage *= Mathf.Max(0f, 1f + combatant.Stats.Get(StatType.MeleeDamage)) * PlayerProficiencies.AmbushScale(combatant.Stats, target);
        // From the saddle: Horsemanship's Mounted Damage, and the gallop's weight behind it.
        float mounted = PlayerRiding.MountedScale(gameObject);
        damage *= mounted;
        if (mounted != 1f)
        {
            PlayerHorsemanship.Practise(HorseWork.MountedHit);
        }

        damage = SilentTakedown(target, pending, damage);
        technique |= HitTechnique.Melee;
        AttackResolver.Apply(combatant, pending.Attack, pending.WeaponDamage, pending.Hit, pending.Origin, pending.Direction, damage,
            pending.Poise, technique, critical);
        if (backstab)
        {
            Backstabbed?.Invoke(target);
        }
    }

    private bool IsBackstab(IDamageable target, out MonsterAI monster)
    {
        monster = null;
        return target is Component body && body.TryGetComponent(out monster) && monster.IsOpenToBackstab
            && StrikeRules.IsBehind(body.transform.position, body.transform.forward, transform.position, Rules.BackstabArc);
    }

    // The blow has landed: the rest is follow-through, walked out of at the rules' speed. At 0 the player stays planted
    // and facing the blow until the swing ends. The swing itself goes on, so walking off doesn't bring the next attack sooner.
    private void ReleaseFeet()
    {
        float speed = activeKind == SwingKind.Heavy ? Rules.HeavyMoveSpeed : Rules.SwingMoveSpeed;
        movement.FacingLocked = speed <= 0f;
        movement.SetSpeedScale(this, speed);
    }

    private void EndSwing()
    {
        EndFinish();
        if (activeKind != SwingKind.None)
        {
            lastStartTime = activeStartTime + chargeHeld;
            lastHitAt = HitAt(activeAttack, activeSpeed);
            lastRecoverAt = RecoverAt(activeAttack, activeSpeed);
        }

        activeKind = SwingKind.None;
        activeAttack = null;
        hitPending = false;
        stepPending = false;
        charging = false;
        chargeHeld = 0f;
        movement.FacingLocked = false;
        movement.SetSpeedScale(this, 1f);
    }

    private static float HitAt(AttackDefinition attack, float speed) => attack != null ? attack.HitDelay / speed : 0f;

    private static float RecoverAt(AttackDefinition attack, float speed) => (attack != null ? attack.RecoveryTime : FallbackRecoveryTime) / speed;

    /// <summary>Ends the current swing if its hit has already landed (raising the guard cuts the follow-through short).</summary>
    public void CancelRecovery()
    {
        if (activeKind != SwingKind.None && !hitPending)
        {
            EndSwing();
            queuedKind = SwingKind.None;
        }
    }

    /// <summary>Uses up a ready counter without a swing (a bow's instant shot after a perfect dodge).</summary>
    public void SpendCounter() => counterUntil = float.NegativeInfinity;

    /// <summary>Readies a counter: the next swing within the settings' counter time is a critical counter.</summary>
    public void GrantCounter()
    {
        counterUntil = Time.time + Rules.CounterSeconds;
        CounterReady?.Invoke();
    }

    // The nearest hostile target within the assist cone and reach, else the aim as given (MeleeTargeting).
    private Vector3 AimAssist(Vector3 aim, AttackDefinition attack, out float targetDistance) =>
        MeleeTargeting.AimAssist(transform.position, transform.forward, aim, attack, aimAssistAngle, aimAssistReach, hitLayers, combatant.Faction, out targetDistance);
}
