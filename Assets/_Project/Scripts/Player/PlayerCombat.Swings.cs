using UnityEngine;

/// <summary>
/// A swing from start to finish: starting it (cost, aim, the counter), its wind-up and heavy charge, following the
/// focused enemy and stepping into the blow, landing it on its targets in the order the blade reaches them, backstabs
/// and the recovery after. Part of PlayerCombat, in its own file.
/// </summary>
public sealed partial class PlayerCombat
{
    // True when the swing started; false when it was refused (the weapon out on a throw, an art cooling down, nothing to strike with,
    // the stamina short), so a press that asked for it can keep what it would have spent (a guard counter keeps its block).
    private bool Begin(SwingKind kind, ChainPress judge)
    {
        queuedKind = SwingKind.None;
        // A press held over from a swing or a dodge fires later than it was made: by then the player may be swimming, at the oars or
        // at work, where a new swing is refused (the state machine's own gate, which a fresh press passes through).
        if ((health != null && !health.IsAlive) || (stateMachine != null && !stateMachine.CanStartAttack))
        {
            return false;
        }

        // The weapon is out on a throw (a spear toss): nothing to swing until it is back in the hand. And an art waits out its
        // cooldown, so a second press mid-throw doesn't strike again.
        if (WeaponInFlight || (kind == SwingKind.Art && Time.time < artReadyAt && !ArtFollowUpOpen))
        {
            if (kind == SwingKind.Art)
            {
                ArtNotReady?.Invoke();
            }

            return false;
        }

        int step = 0;
        int chain = weapon != null ? weapon.ComboLength : 1;
        AttackDefinition attack;
        HitTechnique technique = HitTechnique.None;
        bool followUp = false;
        switch (kind)
        {
            case SwingKind.Heavy:
                // Held again as a heavy lands: the weapon's heavy follow-up, then back to the heavy (a heavy chain of two). A full charge
                // let go is the weapon's charged attack (forcedAttack).
                followUp = forcedAttack == null && weapon != null && weapon.HeavyFollowUp != null && !lastHeavyWasFollowUp
                    && Time.time - lastHeavyEndedAt <= Rules.ChainGrace + Rules.HeavyHoldSeconds + Rules.BufferWindow;
                attack = forcedAttack != null ? forcedAttack : weapon == null ? null : followUp ? weapon.HeavyFollowUp : weapon.HeavyAttack;
                technique |= HitTechnique.Heavy;
                if (forcedAttack != null)
                {
                    technique |= HitTechnique.Charged;
                }

                break;
            case SwingKind.GuardCounter:
                attack = HasShield && Shield != null && Shield.OwnGuardCounter != null ? Shield.OwnGuardCounter : weapon != null ? weapon.GuardCounterAttack : null;
                technique |= HitTechnique.GuardCounter;
                if (attack == null)
                {
                    return false;
                }

                break;
            case SwingKind.ShieldBash:
                attack = shieldBash;
                technique |= HitTechnique.ShieldBash;
                break;
            case SwingKind.Art:
                attack = ArtAttack;
                technique |= HitTechnique.WeaponArt;
                if (attack == null)
                {
                    return false;
                }

                // Paid in Focus alone, and no stamina: a circle, or nothing for a follow-up in its window. A buffered art no full
                // circle can pay for any more doesn't start.
                artPaid = 0f;
                artIsFollowUp = ArtFollowUpOpen;
                if (focus != null && !artIsFollowUp && (artPaid = focus.TrySpendCircle()) <= 0f)
                {
                    return false;
                }

                artFollowUpFromRune = artFromRune;

                artFromRune = false;

                break;
            case SwingKind.Rush:
                // Out of a perfect dodge the counter is the better blow; out of a dodge away from the aim (a backstep) the weapon's
                // backstep attack; a rush leads on into the chain's second swing.
                attack = CounterIsReady && weapon != null && weapon.CounterAttack != null ? weapon.CounterAttack
                    : weapon != null && weapon.BackstepAttack != null && IsBackstep() ? weapon.BackstepAttack
                    : weapon != null ? weapon.RushAttack : null;
                break;
            default:
                // A light swing ends a heavy chain: grown into a heavy it is the weapon's heavy, not its follow-up.
                lastHeavyEndedAt = float.NegativeInfinity;
                lastHeavyWasFollowUp = false;
                // Out of a lunge (Wolf's Lunge) the chain goes on from its second swing, as out of a rush (the user, 2026-11-23: "allow us
                // to follow up with combo attacks after a lunge attack"). Once per lunge.
                if (skills != null && skills.LungeEndsAt > lungeLedFrom && skills.LungedWithin(Rules.ChainGrace + Rules.BufferWindow))
                {
                    lungeLedFrom = skills.LungeEndsAt;
                    lastStep = 0;
                    judge = ChainPress.InTime;
                    fumbled = false;
                }

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
        // A finishing blow that takes a chain swing's place keeps the chain where it was: the next press goes on to the swing after
        // (the user, 2026-11-19: hitting an enemy seemed to reset the combo, as breaking its poise turned the next press into this).
        int finishKeepsStep = toFinish != null && kind == SwingKind.Light ? step : -1;
        if (toFinish != null)
        {
            // The weapon's own rush: same numbers and time as any, so a finishing blow changes the moment and not the balance.
            kind = SwingKind.Rush;
            attack = weapon.RushAttack;
            step = 0;
        }

        // Pay the full cost when the swing actually starts, rechecking affordability for buffered swings.
        float owed = Mathf.Max(0f, CostOf(kind, attack) - heavyCredit);
        // Nothing owed (a charge already paid for, an art paid in Focus): never refused for an empty bar.
        if (stamina != null && owed > 0f && !stamina.TrySpendOnAttack(owed))
        {
            forcedAttack = null;
            return false;
        }

        // Only a rush that is paid for cuts the dodge short: refused for want of stamina, it stopped the roll dead inside the blow
        // being escaped.
        if (kind == SwingKind.Rush && dodge != null)
        {
            dodge.CutShort();
        }

        LastSwingStaminaPaid = stamina != null && owed > 0f ? stamina.Scaled(owed) : 0f;
        if (kind == SwingKind.Art)
        {
            ArtsBegun++;
            ArtFocusPaid = artPaid;
        }

        forcedAttack = null;
        activeIsFollowUp = followUp;
        // The cooldown runs from an art that really begins: one refused for want of stamina above doesn't start it. A follow-up keeps
        // the first one's cooldown and spends one of its follow-ups; the window shuts until this one lands.
        if (kind == SwingKind.Art)
        {
            if (artIsFollowUp)
            {
                artFollowUpsLeft--;
            }
            else
            {
                artReadyAt = Time.time + Rules.ArtCooldown;
                artFollowUpsLeft = ArtFollowUps;
                artFollowUpWeapon = weapon;
            }

            artFollowUpUntil = -1f;
        }

        fumbled = false;
        rhythm = kind == SwingKind.Light && step > 0 ? (judge == ChainPress.Rhythm ? Mathf.Min(rhythm + 1, MaxRhythm) : 0) : 0;
        // Momentum fades with a pause: a new chain, or longer than the chain's grace since a swing last landed.
        if ((kind == SwingKind.Light && step == 0 && judge == ChainPress.Late) || Time.time - momentumAt > Rules.ChainGrace + MomentumHoldSeconds)
        {
            momentum = 0;
        }

        bool finisher = kind == SwingKind.Light && chain > 1 && step == chain - 1;
        lastStep = kind == SwingKind.Rush || kind == SwingKind.GuardCounter ? 0 : kind == SwingKind.Light && !finisher ? step : -1;
        if (finishKeepsStep >= 0)
        {
            lastStep = chain > 1 && finishKeepsStep < chain - 1 ? finishKeepsStep : -1;
        }

        activeKind = kind;
        activeAttack = attack;
        activeWeaponDamage = kind == SwingKind.ShieldBash || weapon == null ? 0f : weapon.Damage;
        // The light chain plays faster than its clips' own pace (CombatTechniqueSettings › Light Swing Pace): its blow and recovery
        // come as much sooner, so the clip still meets the blow on its contact frame.
        activeSpeed = ComboRules.SpeedMultiplier(rhythm, Rules) * (kind == SwingKind.Light ? Rules.LightSwingPace : 1f);
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
        // Arts strike softer than the player's other blows by Art Damage (on top of Player Damage, which every blow takes).
        if (kind == SwingKind.Art)
        {
            activeDamageMultiplier *= Rules.ArtDamage;
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

        if (kind == SwingKind.GuardCounter)
        {
            activePoiseMultiplier *= Rules.GuardCounterPoise;
        }

        // Fists' Flurry: each rhythm stack adds weight; a greataxe's Momentum: each swing in a row that landed adds poise.
        if (Has(WeaponSignature.Flurry) && rhythm > 0)
        {
            activePoiseMultiplier *= 1f + weapon.SignaturePower * rhythm;
        }

        if (Has(WeaponSignature.Momentum) && momentum > 0 && (kind == SwingKind.Light || kind == SwingKind.Heavy))
        {
            activePoiseMultiplier *= 1f + MomentumPoisePerStack * momentum;
        }

        activeLunge = attack != null ? attack.Lunge : 0f;
        if (CounterIsReady && kind != SwingKind.ShieldBash && kind != SwingKind.GuardCounter)
        {
            technique |= HitTechnique.Counter;
            activeDamageMultiplier *= Rules.CounterDamageMultiplier;
            activePoiseMultiplier *= 2f;
            activeForceCritical = true;
            // A sword's Counter Chain readies more than one: the window opens again for the next.
            counterCharges--;
            counterUntil = counterCharges > 0 ? Time.time + Rules.CounterSeconds * CounterChainWindow : float.NegativeInfinity;
            if (counterLunges && weapon != null && weapon.RushAttack != null)
            {
                activeLunge = Mathf.Max(activeLunge, weapon.RushAttack.Lunge);
            }

            counterLunges &= counterCharges > 0;
        }

        activeForceCritical |= attack != null && attack.AlwaysCritical;
        activeTechnique = technique;
        // A full charge let go (ChargedFromHold): the hold was its wind-up, so its clock starts where only the strike is left. Set
        // here, before SwingStarted, so the animation and the whoosh time themselves from the real moment of the blow (shifting it
        // afterwards left the whoosh scheduled after the blow, so it never played).
        // Taken up part way into its wind-up (a heavy grown from the light swing, out of the guard, a heavy chain): its clock starts as
        // far in, so the clip plays on at its own pace and the blow comes sooner, instead of the rest crawling to the old moment.
        WindupTaken = ChargedFromHold || attack == null ? 0f
            : kind == SwingKind.Heavy && HeavyGrownFrom > 0f ? HeavyGrownFrom
            : WindupTakenUp != null ? Mathf.Clamp(WindupTakenUp(kind, attack), 0f, 0.8f) : 0f;
        float headStart = ChargedFromHold && attack != null ? ChargedWindupSkipped(attack, activeSpeed)
            : WindupTaken * Mathf.Max(0f, attack != null ? (attack.HitDelay - attack.StrikeLead) / activeSpeed : 0f);
        activeBeganAt = Time.time;
        activeStartTime = Time.time - headStart;
        chargeHeld = 0f;
        heldPastFull = 0f;
        holdWaited = 0f;
        waitingForHold = false;
        charging = false;
        hitPending = attack != null;
        stepPending = activeLunge > 0f;
        // Toward the aim, or the target it snaps to; while it winds up it may still follow the focused enemy.
        if (focusTarget != null)
        {
            focusTarget.Refresh();
        }

        activeDirection = toFinish != null ? toFinish.transform.forward : AimAssist(movement.AimDirection, attack, out _);
        activeMotion = kind == SwingKind.Art && attack != null ? attack.Motion : ArtMotion.None;
        // A weapon art's own look as it begins (a dagger's smoke where it leaves, a dash's dust kicked up, a throw's gust).
        Vector3 artFrom = transform.position;
        if (activeMotion == ArtMotion.BlinkBehind && !BlinkBehindFocus(attack.MotionDistance))
        {
            activeMotion = ArtMotion.None;
        }

        if (kind == SwingKind.Art)
        {
            SkillFx.ArtBegin(attack, artFrom, activeDirection);
        }

        movement.SnapFacing(activeDirection);
        // Seconds from now to the blow: less than the attack's Hit Delay when the swing began with a head start.
        float hitIn = HitAt(attack, activeSpeed) - headStart;
        if (toFinish != null)
        {
            stepPending = false;
            BeginFinish(toFinish, hitIn);
        }

        // Facing is the swing's own until a moment after the blow: early in the wind-up it may still turn toward the aim (TrackAim)
        // or the focused enemy (TrackFocus). The feet keep the attack's share of speed (SwingMotion): planted for a heavy swing or a
        // two-hander, walking for a dagger; a swing walked through plays on the arms over the legs.
        activeWindupMove = SwingMotion.WindupMove(attack, kind, Rules);
        activeLungeLead = SwingMotion.LungeLead(attack, kind, hitIn, LungeSeconds, attack != null ? Mathf.Max(attack.StrikeLead, LungeSeconds) : 0f, Rules);
        // A leap or a dash closes its whole distance over the wind-up, as a rush does; a dash goes through bodies.
        bool travels = activeMotion == ArtMotion.LeapTo || activeMotion == ArtMotion.DashThrough;
        if (travels && hitPending)
        {
            activeLunge = attack.MotionDistance;
            activeLungeLead = hitIn;
            stepPending = true;
            dashFrom = transform.position;
            if (activeMotion == ArtMotion.DashThrough)
            {
                PassThroughBodies();
            }
        }

        // A leaping swing begun on the move (the sword's finisher walked into) is swung over the walking legs: no leap on top of the walk,
        // which lifted the player off the ground mid-stride (the user, 2026-11-20: "makes him float"). Its lunge still closes the gap.
        activeHop = attack != null ? attack.Hop : 0f;
        if (activeHop > 0f && movement.MovementInput.sqrMagnitude >= 0.01f && !travels && kind == SwingKind.Light)
        {
            activeHop = 0f;
        }

        feetReleased = false;
        movement.FacingLocked = true;
        movement.SetSpeedScale(this, activeWindupMove);
        if (!hitPending)
        {
            ReleaseFeet();
        }
        else if ((kind == SwingKind.Rush || travels) && stepPending)
        {
            // A rush carries the movement it came with: the lunge runs for the whole wind-up instead of waiting for the strike.
            StepIntoBlow(hitIn);
        }

        EnterCombat();

        AttackDefinition looks = toFinish != null && Rules.FinishStab != null ? Rules.FinishStab : attack;
        current = new SwingInfo(kind, looks, step, chain, rhythm, activeSpeed, technique, activeDirection);
        landedThisSwing = -1;
        SwingStarted?.Invoke(current);
        if (kind == SwingKind.ShieldBash)
        {
            ShieldBashPerformed?.Invoke();
        }
        else
        {
            AttackPerformed?.Invoke();
        }

        return true;
    }

    // Silent Takedown: a blow from the shadows finishes an ordinary creature it has nearly done for anyway. Never a boss or
    // an elite, and never one that knows you are there.
    // <paramref name="damage"/> is the swing's multiplier so far, and what comes back is the multiplier to strike with.
    private float SilentTakedown(IDamageable target, in PendingHit pending, float damage)
    {
        if (!(target is Damageable body) || !body.TryGetComponent(out MonsterAI monster) || !monster.IsUnawareOf(transform.position)
            || body.GetComponent<EliteCreature>() != null || body.GetComponentInParent<BossEncounter>() != null
            || !Techniques.Has(PerkTechnique.SilentTakedown))
        {
            return damage;
        }

        // What this blow is worth against this creature, as the focus ring's estimate (WouldTakedown) works it out too: the one rule.
        // It read the creature's last Damage Taken before (often 1), not what being caught unaware adds as the blow lands.
        float blow = PlayerBlows.UnawareBlowWorth(combatant, body, pending.Attack, pending.WeaponDamage, damage);
        if (!StrikeRules.IsTakedown(body.CurrentHealth, blow, TechniqueSettings.Current.TakedownReach))
        {
            return damage;
        }

        GameMessages.Post("Silent takedown.", MessageKind.Gain);
        Techniques.Used(PerkTechnique.SilentTakedown);
        // As hard as it takes to finish it and a little over, so the number shown is about what the creature had left.
        return damage * Mathf.Max(1f, body.CurrentHealth / blow * 1.05f);
    }

    /// <summary>The stamina a swing of this kind with this attack takes from the bar, all told (the Melee skill, Action Cost Scale and the
    /// burden): what the Fight card counts swings with.</summary>
    public float StaminaCostOf(SwingKind kind, AttackDefinition attack) => stamina != null ? stamina.Scaled(CostOf(kind, attack)) : 0f;

    // The swing's stamina, after the Melee skill: a beginner tires sooner, a master later. A staff's cast is a spell (it trains
    // Runecraft), so Spell Stamina Cost prices it instead: since abilities stopped costing stamina (2026-11-25), Runecraft, Spellsung
    // and Mist Custard's lighter breath is felt in the staff.
    private float CostOf(SwingKind kind, AttackDefinition attack) =>
        BaseCostOf(kind, attack) * Mathf.Max(0.1f, 1f + combatant.Stats.Get(attack != null && attack.CastProjectile != null
            ? StatType.SpellStaminaCost : StatType.MeleeStaminaCost));

    private float BaseCostOf(SwingKind kind, AttackDefinition attack)
    {
        if (stamina == null)
        {
            return 0f;
        }

        if (kind == SwingKind.Art)
        {
            return 0f;
        }

        if (attack != null && attack.OwnStaminaCost.HasValue)
        {
            return attack.OwnStaminaCost.Value;
        }

        return kind == SwingKind.ShieldBash ? stamina.ShieldBashCost : kind == SwingKind.Heavy ? stamina.AttackCost * 2f : stamina.AttackCost;
    }

    private void UpdateSwing()
    {
        UpdateFinish();
        if (activeKind == SwingKind.None)
        {
            bool dodgeInTheWay = dodge != null && dodge.IsDodging && !(queuedKind == SwingKind.Rush && dodge.CanBeCutShort);
            // A skill under way (Wolf's Lunge in flight): a press made in it waits for it, as one made in a dodge does.
            bool skillInTheWay = skills != null && skills.IsBusy;
            if (queuedKind != SwingKind.None && !IsFlinching && !dodgeInTheWay && !skillInTheWay && (guard == null || !guard.IsStunned))
            {
                Begin(queuedKind, queuedJudge);
            }

            return;
        }

        float hitAt = HitAt(activeAttack, activeSpeed);
        if (UpdateCharge(hitAt) || WaitsForHold(hitAt))
        {
            return;
        }

        float elapsed = ActiveElapsed;
        UpdateHop(elapsed, hitAt);
        if (hitPending && elapsed < hitAt - activeLungeLead)
        {
            TrackFocus();
            TrackAim(elapsed, hitAt, false);
        }
        else if (hitPending && stepPending)
        {
            StepIntoBlow(hitAt - elapsed);
        }

        if (hitPending && elapsed >= hitAt)
        {
            hitPending = false;
            int hits = LandSwing();
            landedThisSwing = hits;
            // The blow throws the mist the way it was aimed as it landed (a throw or a cast strikes from afar, not here).
            if (activeAttack != null && activeMotion != ArtMotion.Throw && activeAttack.CastProjectile == null)
            {
                MistWakes.Strike(transform.position, activeDirection, activeAttack.Range, activeAttack.Arc, this);
            }

            SwingLanded?.Invoke(current, hits);
        }

        // A moment after the blow the legs and facing come back (Move At); the swing itself goes on to its recovery.
        float recoverAt = RecoverAt(activeAttack, activeSpeed);
        if (!hitPending && !feetReleased && elapsed >= SwingMotion.MoveAt(activeAttack, hitAt, recoverAt, activeSpeed, Rules))
        {
            ReleaseFeet();
        }

        if (elapsed >= recoverAt)
        {
            SwingKind next = queuedKind;
            ChainPress judge = queuedJudge;
            // Pressed again during a heavy and still held past a click as it recovers: the heavy chain goes on from where this one
            // ended (its follow-up, or the heavy again), not through a light swing that then grows (the user, 2026-11-20: "two heavy
            // attacks should chain, making the second start from where the first one ends").
            // A press too fresh to tell a tap from a hold waits for it (at most Heavy Hold Seconds, the hold that makes a heavy anywhere
            // else; after a heavy there is no quick chain to slow).
            bool heavyChain = next == SwingKind.Light && activeKind == SwingKind.Heavy && !hitPending && attackHeld && pressTime >= 0f
                && !heavyStarted && (activeTechnique & HitTechnique.Charged) == 0;
            if (heavyChain && Time.time - pressTime < Rules.HeavyHoldSeconds)
            {
                return;
            }

            if (heavyChain)
            {
                heavyStarted = true;
                pressTime = -1f;
                EndSwing();
                // Refused (the stamina short for a heavy), the light swing the press asked for still comes.
                if (!Begin(SwingKind.Heavy, ChainPress.Late))
                {
                    Begin(SwingKind.Light, judge);
                }

                return;
            }

            // Nothing pressed, the player still: the swing waits on its last pose (Swing Hold Seconds), the next press still chaining.
            if (next == SwingKind.None && HoldsPose(elapsed - recoverAt))
            {
                if (!holdingPose)
                {
                    holdingPose = true;
                    SwingHeld?.Invoke(true);
                }

                return;
            }

            EndSwing();
            if (next != SwingKind.None)
            {
                Begin(next, judge);
            }
        }
    }

    // True while a recovered swing may go on waiting on its last pose: a light, rush or uncharged heavy, held for less than Swing Hold
    // Seconds, with no move pressed and the guard down. Moving or running (any move), a roll or the guard ends it at once.
    private bool HoldsPose(float heldFor) => SwingMotion.HoldsPose(activeKind, (activeTechnique & HitTechnique.Charged) != 0, heldFor,
        Rules.SwingHoldSeconds, attackHeld, movement.MovementInput, guard != null && guard.IsGuarding, dodge != null && dodge.IsDodging);

    // A heavy swing held on waits at the top of its wind-up, where its strike would begin, and everything about it stands still for as
    // long as the button is held (the user, 2026-11-20: "hold a charging attack for as long as he wants"). Full after Heavy Charge
    // Seconds (the blink); held past that it costs Held Charge Drain a second, and run dry it strikes. Let go full: the weapon's
    // charged attack, its strongest blow (Hades II's Omega moves); let go sooner, the heavy. True while it waits.
    private bool UpdateCharge(float hitAt)
    {
        float most = Rules.HeavyChargeSeconds;
        bool full = most > 0f && chargeHeld >= most;
        bool spent = full && stamina != null && stamina.IsEmpty;
        // Once at the top it stays there while held: re-judging the clock each frame let it slip a frame on and back (a flicker).
        bool waits = activeKind == SwingKind.Heavy && hitPending && attackHeld && most > 0f && !spent
            && (activeTechnique & HitTechnique.Charged) == 0
            && (charging || ActiveElapsed >= hitAt - Mathf.Max(activeAttack.StrikeLead, LungeSeconds));
        if (!waits)
        {
            bool wasCharging = charging;
            if (charging)
            {
                // Let go (or run dry): the feet keep the swing's own share again for its strike.
                movement.SetSpeedScale(this, activeWindupMove);
            }

            charging = false;
            if (wasCharging && full && weapon != null && weapon.ChargedAttack != null && hitPending && activeKind == SwingKind.Heavy)
            {
                ReleaseCharged();
                return true;
            }

            return false;
        }

        if (!charging)
        {
            movement.SetSpeedScale(this, Rules.ChargeMoveSpeed);
        }

        charging = true;
        if (full)
        {
            heldPastFull += Time.deltaTime;
        }

        chargeHeld = Mathf.Min(most, chargeHeld + Time.deltaTime);
        if (stamina != null)
        {
            if (full)
            {
                stamina.Drain(Rules.HeldChargeDrain * Time.deltaTime);
            }
            else
            {
                stamina.HoldRegeneration();
            }
        }

        TrackFocus();
        // A charging blow can be aimed as it is held.
        TrackAim(ActiveElapsed, hitAt, true);
        if (!full && chargeHeld >= most)
        {
            ChargeFull?.Invoke();
        }

        return true;
    }

    // A press still held may yet become the heavy one: the light swing it began waits at the top of its swing until the hold decides.
    // Let go, it strikes at once (a tap is answered as before); held on, UpdateHeldPress grows it into the heavy from that pose. A
    // fast weapon's light blow used to land before the hold could be told apart, so a held press swung, stood, and only then began
    // the heavy (the user, 2026-11-15). True while it waits.
    private const float HoldTellSeconds = 0.12f;

    private bool WaitsForHold(float hitAt)
    {
        // Only once the press has outlasted a click (HoldTellSeconds): a tap on a fast weapon (fists, dagger) whose blow comes
        // before then strikes on time, rather than waiting for the finger to lift (review 2026-11-16: up to 0.07 s late). And only
        // for a heavy that grows out of the light swing: one that is another motion (the fists' high kick) doesn't hold a punch half
        // thrown and then start over (the user, 2026-11-20: "weird timing on unarmed charged"); the punch lands and the kick follows.
        waitingForHold = activeKind == SwingKind.Light && hitPending && attackHeld && pressTime >= 0f && !heavyStarted
            && weapon != null && weapon.HeavyAttack != null && weapon.HeavyAttack.GrowsFromLight
            && activeBeganAt >= pressTime && (activeTechnique & HitTechnique.Counter) == 0
            && Time.time - pressTime >= HoldTellSeconds
            && Time.time - pressTime < Rules.HeavyHoldSeconds + 0.05f
            && ActiveElapsed >= hitAt - Mathf.Max(activeAttack.StrikeLead, Mathf.Max(activeLungeLead, LungeSeconds));
        if (waitingForHold)
        {
            holdWaited += Time.deltaTime;
            // It stands still as a charging heavy does, and can be aimed as that can.
            TrackFocus();
            TrackAim(ActiveElapsed, hitAt, true);
        }

        return waitingForHold;
    }

    // The heavy swing hands over to the charged attack, already paid for, from the top of its wind-up.
    private void ReleaseCharged()
    {
        AttackDefinition charged = weapon.ChargedAttack;
        EndSwing();
        forcedAttack = charged;
        heavyCredit = float.MaxValue;
        // The hold was its wind-up: it strikes now, not after a second one (the user, 2026-11-15: it went back to a standing pose,
        // wound up again, then struck). Begin starts its clock past the wind-up (ChargedWindupSkipped).
        ChargedFromHold = true;
        Begin(SwingKind.Heavy, ChainPress.Late);
        ChargedFromHold = false;
        heavyCredit = 0f;
    }

    // How much of a charged attack's wind-up the hold stood in for: all but the strike's own moment, and never less than the blend
    // into it (ChargedReleaseSeconds) left before the blow.
    private static float ChargedWindupSkipped(AttackDefinition charged, float speed) =>
        Mathf.Max(0f, HitAt(charged, speed) - Mathf.Max(charged.StrikeLead, ChargedReleaseSeconds));

    // Out of a dodge that went away from where the player aims: the backstep, and the weapon's backstep attack.
    private bool IsBackstep() => dodge != null && (dodge.IsDodging || dodge.JustDodged(Rules.RushAfterDodgeSeconds))
        && lastDodgeDirection.sqrMagnitude > 0.01f && Vector3.Angle(lastDodgeDirection, movement.AimDirection) >= Rules.BackstepAngle;

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

    // No Rest for the Wicked's "change targets during attack windups": with no enemy in focus, early in the wind-up the blow turns
    // toward where the player aims, at the attack's turn rate (the focused enemy is TrackFocus's). <paramref name="always"/>: a
    // charging heavy, aimed for as long as it is held.
    private void TrackAim(float elapsed, float hitAt, bool always)
    {
        if (focusTarget != null && focusTarget.HasFocus)
        {
            return;
        }

        float rate = SwingMotion.TurnRate(activeAttack, Rules);
        if (rate <= 0f || (!always && !SwingMotion.CanTurn(elapsed, hitAt, activeLungeLead, Rules)))
        {
            return;
        }

        Vector3 aim = movement.AimDirection;
        aim.y = 0f;
        if (aim.sqrMagnitude < 0.0001f)
        {
            return;
        }

        activeDirection = Vector3.RotateTowards(activeDirection, aim.normalized, rate * Mathf.Deg2Rad * Time.deltaTime, 0f);
        movement.SnapFacing(activeDirection);
    }

    // The last part of the wind-up (the attack's Lunge Share of it): the body travels with the strike, as far as the target (or
    // half way with none), so the step ends as it lands instead of sliding at the start and then standing still through the swing.
    private void StepIntoBlow(float secondsLeft)
    {
        stepPending = false;
        // A light swing at an enemy in focus just out of reach steps further toward it (Light Reach Assist): a wolf that bit and
        // backed off is still caught.
        float extra = activeKind == SwingKind.Light && activeMotion == ArtMotion.None && focusTarget != null && focusTarget.Target != null
            ? Rules.LightReachAssist : 0f;
        activeDirection = MeleeTargeting.AimAssist(transform.position, transform.forward, activeDirection, activeAttack, aimAssistAngle,
            aimAssistReach + extra, hitLayers, combatant.Faction, out float targetDistance);
        movement.SnapFacing(activeDirection);
        float distance = activeMotion == ArtMotion.DashThrough ? activeLunge
            : SwingMotion.StepDistance(targetDistance, activeLunge, extra, LungeStandOff);
        // Walking back or aside while swinging keeps that way: an ordinary blow doesn't drag the player forward (the user, 2026-11-15).
        // An art that moves the player (a leap, a dash: all its distance is this step) and a heavy, charged or counter blow stepping in
        // toward a target keep their step: those are the move itself, and strafing left a leap art going nowhere (review 2026-11-16).
        Vector3 walking = movement.MovementInput;
        walking.y = 0f;
        bool committed = activeMotion != ArtMotion.None
            || (targetDistance >= 0f && (activeKind == SwingKind.Heavy || (activeTechnique & (HitTechnique.Charged | HitTechnique.Counter)) != 0));
        if (!committed && walking.sqrMagnitude > 0.04f && Vector3.Dot(walking.normalized, activeDirection) < 0.5f)
        {
            distance = 0f;
        }

        float seconds = Mathf.Max(0.05f, secondsLeft);
        movement.Lunge(activeDirection * (distance / seconds), seconds, eased: true);
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
        if (activeMotion == ArtMotion.Throw && ThrowWeapon(damage, poise))
        {
            return 0;
        }

        if (activeAttack.CastProjectile != null)
        {
            Cast(damage, poise);
            return 0;
        }

        if (activeMotion == ArtMotion.DashThrough)
        {
            // Everything the dash went through, and what stands just past its end.
            HitDetector.FindAlong(dashFrom, origin + activeDirection * (activeAttack.Range * 0.5f), Mathf.Max(0.8f, activeAttack.Range * 0.5f),
                combatant.Faction, hitLayers, activeAttack.MaxTargets, foundHits);
            GiveBodiesBack();
        }
        else
        {
            float arcBonus = Has(WeaponSignature.Momentum) ? MomentumArcPerStack * momentum : 0f;
            HitDetector.FindTargets(origin, activeDirection, activeAttack, combatant.Faction, hitLayers, foundHits, arcBonus);
        }

        if (Has(WeaponSignature.Momentum) && (activeKind == SwingKind.Light || activeKind == SwingKind.Heavy))
        {
            momentum = foundHits.Count > 0 ? Mathf.Min(momentum + 1, Mathf.RoundToInt(weapon.SignaturePower)) : 0;
            momentumAt = Time.time;
        }
        // A swing that met nobody may still meet a boulder or a tree, and glance off it (HitMaterials).
        if (foundHits.Count == 0)
        {
            HitMaterials.Glance(origin, activeDirection, activeAttack.Range, gameObject);
        }

        // A weapon art's own look as it lands: over its reach, along a dash's path, on everything it struck.
        if (activeKind == SwingKind.Art)
        {
            artStruck.Clear();
            foreach (HitDetector.Hit hit in foundHits)
            {
                artStruck.Add(hit.Point);
            }

            SkillFx.ArtLand(activeAttack, origin, activeDirection, artStruck, activeMotion == ArtMotion.DashThrough ? dashFrom : origin);
            if (artFollowUpsLeft > 0)
            {
                // From the end of its recovery, when a press queued during it would start.
                float recoveryLeft = Mathf.Max(0f, RecoverAt(activeAttack, activeSpeed) - ActiveElapsed);
                artFollowUpUntil = Time.time + recoveryLeft + ArtFollowUpWindow;
            }
        }

        // A heavy blow or a weapon art strikes the ground too, hit or miss: wind, dust, a mark (GroundImpacts).
        if (activeKind == SwingKind.Heavy || activeKind == SwingKind.Art)
        {
            GroundImpacts.PlayerSwing(activeAttack, combatant, origin, activeDirection, weapon != null && weapon.Stance == WeaponStance.TwoHanded, charge);
        }
        else
        {
            // Any other blow is a sudden wind through the grass ahead, and nothing else (the user, 2026-11-15).
            GroundImpacts.LightSwing(combatant, origin, activeDirection);
        }

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
        float poise = pending.Poise;
        HitTechnique technique = pending.Technique;
        // A sneak attack is always a critical, and Ambush Damage adds to it (PlayerBlows: as every way a player's blow lands).
        bool critical = PlayerBlows.FromTheShadows(combatant, target, transform.position, pending.Critical, out float ambush);
        bool backstab = IsBackstab(target, out MonsterAI backstabbed);
        if (backstab)
        {
            // A stagger is earned, and pays most (more with a dagger: Assassin); catching it mid-strike pays less, and no sure critical.
            float staggered = Has(WeaponSignature.Assassin) ? Mathf.Max(Rules.BackstabDamage, weapon.SignaturePower) : Rules.BackstabDamage;
            damage *= backstabbed.IsStaggered ? staggered : Rules.BackstabOnStrikeDamage;
            technique |= HitTechnique.Backstab;
            critical |= backstabbed.IsStaggered;
        }

        // The Melee skill scales every swing alike, before nothing else changes, so the order of blows (plain, backstab,
        // counter) holds at every level; a perk can add to a blow from the shadows.
        damage *= Mathf.Max(0f, 1f + combatant.Stats.Get(StatType.MeleeDamage)) * ambush;
        // From the saddle: Horsemanship's Mounted Damage, and the gallop's weight behind it.
        float mounted = PlayerRiding.MountedScale(gameObject);
        damage *= mounted;
        if (mounted != 1f)
        {
            PlayerHorsemanship.Practise(HorseWork.MountedHit);
        }

        damage = SilentTakedown(target, pending, damage);
        technique |= HitTechnique.Melee;
        MonsterAI monster = target is Component struck && struck != null && struck.TryGetComponent(out MonsterAI found) ? found : null;
        // An axe's Guard Breaker on a raised shield; a spear's Anti Charge against a creature charging in.
        bool shieldUp = monster != null && monster.ShieldIsUp;
        bool charging = monster != null && monster.IsCharging;
        if (Has(WeaponSignature.GuardBreaker) && shieldUp)
        {
            poise *= weapon.SignaturePower;
        }

        if (Has(WeaponSignature.AntiCharge) && charging)
        {
            poise *= weapon.SignaturePower;
        }

        // A hammer's Crush: a heavy blow on poise already worn low staggers at once, great beasts too.
        const HitTechnique Weighty = HitTechnique.Heavy | HitTechnique.Finisher | HitTechnique.Charged;
        bool crush = Has(WeaponSignature.Crush) && (technique & Weighty) != 0 && monster != null && monster.Poise != null
            && monster.Poise.Fraction < weapon.SignaturePower && !monster.IsStaggered;
        // A charge: at a gallop the blow knocks the struck off their feet.
        DamageResult result = AttackResolver.Apply(combatant, pending.Attack, pending.WeaponDamage, pending.Hit, pending.Origin, pending.Direction, damage,
            poise * PlayerRiding.ChargePoise(gameObject), technique, critical);
        // A coated blade (Ember Pitch): the swing's fire after it, and now and then a burn.
        CoatedFire.AfterSwing(combatant, target, result, pending.WeaponDamage, pending.Hit.Point, pending.Direction, technique);
        if (monster != null && target.IsAlive)
        {
            // Only a blow that struck home crushes: not one it hopped clear of or parried. Its poise breaks as a blow's would: a stagger, or
            // a topple (its poise growing for the next) for a boss that topples.
            if (crush && !monster.IsStaggered && result.Landed && !result.Parried)
            {
                monster.Crushed(pending.Direction, Rules.StaggerSeconds);
                SignatureUsed?.Invoke(WeaponSignature.Crush, monster);
            }

            if (Has(WeaponSignature.AntiCharge) && charging)
            {
                monster.StopCharge(combatant);
                SignatureUsed?.Invoke(WeaponSignature.AntiCharge, monster);
            }

            if (Has(WeaponSignature.GuardBreaker) && shieldUp)
            {
                SignatureUsed?.Invoke(WeaponSignature.GuardBreaker, monster);
            }
        }
        if (backstab)
        {
            // A toppled boss's first backstab strikes its heart too (Monster Definition › Topple Strike): a blow of its own, after this one.
            if (result.Landed && !result.Parried && target.IsAlive)
            {
                backstabbed.HeartStrike(combatant, pending.Hit.Point, pending.Direction);
            }

            Backstabbed?.Invoke(target);
        }
    }

    private bool IsBackstab(IDamageable target, out MonsterAI monster)
    {
        monster = null;
        return target is Component body && body.TryGetComponent(out monster) && monster.IsOpenToBackstab
            && StrikeRules.IsBehind(body.transform.position, body.transform.forward, transform.position, Rules.BackstabArc);
    }

    // The blow has landed and Move At has passed: the rest is follow-through, walked out of at the attack's speed (SwingMotion). At 0
    // the player stays planted and facing the blow until the swing ends. The swing itself goes on, so walking off doesn't bring the
    // next attack sooner.
    private void ReleaseFeet()
    {
        feetReleased = true;
        float speed = SwingMotion.AfterContactMove(activeAttack, activeKind, Rules);
        movement.FacingLocked = speed <= 0f;
        movement.SetSpeedScale(this, speed);
    }

    // A leaping swing (Attack Definition › Hop): the body rises over the wind-up and lands HopLanding seconds after the blow. The rig's
    // hips are lifted after the animation each frame (LateUpdate), so the whole body leaves the ground; the collider stays.
    private const float HopLanding = 0.14f;
    private Transform hopHips;
    private float hopLift;
    private float activeHop;

    private void UpdateHop(float elapsed, float hitAt)
    {
        float hop = activeAttack != null ? activeHop : 0f;
        if (hop <= 0f)
        {
            // A hop broken off is still coming down (LateUpdate).
            if (hopDropSpeed <= 0f)
            {
                hopLift = 0f;
            }

            return;
        }

        hopDropSpeed = 0f;

        if (hopHips == null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            hopHips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        }

        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, hitAt + HopLanding / Mathf.Max(0.01f, activeSpeed)));
        hopLift = hop * Mathf.Sin(Mathf.PI * t);
    }

    // A hop cut off mid-air (a flinch, a knockdown, a dodge) comes down over HopDropSeconds instead of dropping the hips up
    // to 0.9 m in one frame (technical animation audit, 2026-11-20).
    private const float HopDropSeconds = 0.15f;
    private float hopDropSpeed;

    private void EndHop()
    {
        hopDropSpeed = hopLift > 0f ? hopLift / HopDropSeconds : 0f;
        if (hopDropSpeed <= 0f)
        {
            hopLift = 0f;
        }
    }

    private void LateUpdate()
    {
        if (hopDropSpeed > 0f)
        {
            hopLift = Mathf.MoveTowards(hopLift, 0f, hopDropSpeed * Time.deltaTime);
            if (hopLift <= 0f)
            {
                hopDropSpeed = 0f;
            }
        }

        if (hopLift > 0f && hopHips != null)
        {
            hopHips.position += Vector3.up * hopLift;
        }
    }

    private void EndSwing()
    {
        EndFinish();
        EndHop();
        GiveBodiesBack();
        // A swing lost before its blow (a flinch, a dodge) stops its step into it: a dash or leap mustn't slide on.
        if (activeKind != SwingKind.None && hitPending)
        {
            movement.CancelLunge();
        }

        activeMotion = ArtMotion.None;
        if (activeKind != SwingKind.None)
        {
            lastStartTime = activeStartTime + chargeHeld + heldPastFull + holdWaited;
            lastHitAt = HitAt(activeAttack, activeSpeed);
            // A swing that waited on its last pose: the chain's grace runs from the end of the wait.
            float waited = holdingPose ? Mathf.Max(0f, ActiveElapsed - RecoverAt(activeAttack, activeSpeed)) : 0f;
            lastRecoverAt = RecoverAt(activeAttack, activeSpeed) + waited;
            // A heavy that landed can be followed by the heavy follow-up; any other swing that landed ends that chain.
            if (!hitPending && activeKind == SwingKind.Heavy && (activeTechnique & HitTechnique.Charged) == 0)
            {
                lastHeavyEndedAt = Time.time;
                lastHeavyWasFollowUp = activeIsFollowUp;
            }
            else if (!hitPending)
            {
                lastHeavyEndedAt = float.NegativeInfinity;
                lastHeavyWasFollowUp = false;
            }
        }

        activeIsFollowUp = false;
        if (holdingPose)
        {
            holdingPose = false;
            SwingHeld?.Invoke(false);
        }

        activeKind = SwingKind.None;
        activeAttack = null;
        hitPending = false;
        stepPending = false;
        feetReleased = false;
        activeWindupMove = 0f;
        charging = false;
        chargeHeld = 0f;
        heldPastFull = 0f;
        holdWaited = 0f;
        waitingForHold = false;
        movement.FacingLocked = false;
        movement.SetSpeedScale(this, 1f);
    }

    private static float HitAt(AttackDefinition attack, float speed) => attack != null ? attack.HitDelay / speed : 0f;

    private static float RecoverAt(AttackDefinition attack, float speed) => (attack != null ? attack.RecoveryTime : FallbackRecoveryTime) / speed;

    /// <summary>The guard going up ends the current swing where it gives way to the guard (CanGuardFrom): its follow-through cut
    /// short, a light wind-up broken off, a heavy still held let go.</summary>
    public void GiveWayToGuard()
    {
        // Past its blow, a heavy still held, or a wind-up whose cancels take the guard (broken off unstruck: what it cost stays spent).
        // No Cancel Lockout as a dodge out of a wind-up gets: the guard's own Whiff Relock already keeps a tap fishing for a parry
        // down, and a lockout would eat the counter out of a blow caught on the raised guard.
        if (activeKind != SwingKind.None && (BreaksOffIntoGuard || CancelsNow(SwingCancels.Guard)))
        {
            if (charging)
            {
                charging = false;
                movement.SetSpeedScale(this, 1f);
            }

            EndSwing();
            queuedKind = SwingKind.None;
            // The attack press still held is spent with it, as a dodge spends it (review 2026-11-20: held on, it turned into a heavy
            // under the raised guard at the hold threshold).
            pressTime = -1f;
            heavyAfterLight = false;
        }
    }

    /// <summary>Whether a skill may start now over the swing going: where the swing gives way to the guard, a normal strike at once and
    /// a heavy once its blow has landed (the user, 2026-11-25: "normal strikes should automatically stop", for a parry and for War Cry).</summary>
    public bool GivesWayToSkill => activeKind == SwingKind.None || CancelsNow(SwingCancels.Guard);

    /// <summary>A skill is cast: the swing going ends where it gives way to it (GivesWayToSkill), its blow not struck.</summary>
    public void GiveWayToSkill()
    {
        if (activeKind != SwingKind.None && CancelsNow(SwingCancels.Guard))
        {
            EndSwing();
            queuedKind = SwingKind.None;
            pressTime = -1f;
            heavyAfterLight = false;
        }
    }

    /// <summary>Uses up a ready counter without a swing (a bow's instant shot after a perfect dodge).</summary>
    public void SpendCounter()
    {
        counterUntil = float.NegativeInfinity;
        counterCharges = 0;
        counterLunges = false;
    }

    /// <summary>Readies a counter: the next swing within the settings' counter time is a critical counter.</summary>
    public void GrantCounter() => GrantCounter(Rules.CounterSeconds, false);

    /// <summary>Readies a counter for <paramref name="seconds"/> (never less than one already ready); with <paramref name="lunges"/> (the
    /// Riposte perk) it steps in as far as the weapon's rush.</summary>
    public void GrantCounter(float seconds, bool lunges)
    {
        bool chain = Has(WeaponSignature.CounterChain);
        counterCharges = chain ? Mathf.Max(1, Mathf.RoundToInt(weapon.SignaturePower)) : 1;
        counterUntil = Mathf.Max(counterUntil, Time.time + seconds * (chain ? CounterChainWindow : 1f));
        counterLunges = lunges;
        CounterReady?.Invoke();
    }

    // The nearest hostile target within the assist cone and reach, else the aim as given (MeleeTargeting).
    private Vector3 AimAssist(Vector3 aim, AttackDefinition attack, out float targetDistance) =>
        MeleeTargeting.AimAssist(transform.position, transform.forward, aim, attack, aimAssistAngle, aimAssistReach, hitLayers, combatant.Faction, out targetDistance);
}
