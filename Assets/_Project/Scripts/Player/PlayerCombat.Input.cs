using UnityEngine;

/// <summary>
/// How presses become swings: a light swing on the press that turns heavy while held, the rush on the run or out of a
/// dodge, the art, the shield bash, the press judged against the swing before and one press buffered while a swing or
/// dodge is going. Part of PlayerCombat, in its own file.
/// </summary>
public sealed partial class PlayerCombat
{
    private void OnAttackPressed()
    {
        if (weapon != null && weapon.TakesAttackInput)
        {
            return; // PlayerArchery draws and shoots; PlayerFishing casts
        }

        if (guard != null && guard.IsGuarding)
        {
            // Just after a parry the press is the counter itself (the light swing a ready counter becomes), not the art or a bash: the
            // parry leads into aggression (No Rest for the Wicked lets a parry cancel into the attack).
            if (CounterIsReady)
            {
                guard.Lower();
            }
            // Just after a block: the guard counter, a hard answer out of the guard (Elden Ring).
            else if (weapon != null && !weapon.TakesAttackInput && guard.JustBlocked(Rules.GuardCounterSeconds) && HasGuardCounter)
            {
                // Lowered first (a swing can't start under a raised guard); refused, the guard goes up again by itself while held and
                // the block can still be answered.
                guard.Lower();
                if (Request(SwingKind.GuardCounter, ChainPress.Late))
                {
                    guard.SpendBlock();
                }

                return;
            }
            else if (HasShield)
            {
                Request(SwingKind.ShieldBash, ChainPress.Late);
                return;
            }
            else
            {
                // Without a shield, attacking drops the guard into an art, paid in Focus or else in stamina: the attuned rune's
                // (ArtRunes) when there is one, else the weapon's own; a plain swing only when neither can pay.
                artFromRune = ArtRunes.For(weapon) != null;
                if (CanAffordArt)
                {
                    // Not ready yet (its cooldown, or the weapon out on a throw): the guard stays up and the press asks for nothing
                    // else, instead of dropping the guard for an art that then quietly doesn't start (review 2026-11-16).
                    if (ArtCoolingDown)
                    {
                        artFromRune = false;
                        ArtNotReady?.Invoke();
                        return;
                    }

                    guard.Lower();
                    Request(SwingKind.Art, ChainPress.Late);
                    return;
                }

                guard.Lower();
                artFromRune = false;
            }
        }

        attackHeld = true;
        if (WantsRush())
        {
            pressTime = -1f;
            Request(SwingKind.Rush, ChainPress.Late);
            return;
        }

        pressJudge = JudgePress();
        if (pressJudge == ChainPress.Early && Rules.EarlyPressFumbles && activeKind == SwingKind.Light)
        {
            fumbled = true;
        }

        ChainJudged?.Invoke(pressJudge);

        // The light swing starts on the press, so a tap is answered at once (waiting for the release to tell a tap from a
        // hold made every light swing late). Held on, UpdateHeldPress turns it into the heavy one. A press too early to keep
        // (the bounded buffer) is dropped; still held when the window opens, it counts then (UpdateEarlyPress).
        bool taken = Request(SwingKind.Light, pressJudge);
        bool hasHeavy = weapon != null && weapon.HeavyAttack != null;
        pressTime = taken && hasHeavy ? Time.time : -1f;
        heldEarlyPress = !taken && IsAttacking;
        heavyStarted = false;
    }

    // Holding the button through a swing keeps the chain going: the press it began with was too early to keep, so it is made
    // again as the buffer window opens (and from then on is a press like any, so holding on makes it the heavy one).
    private void UpdateEarlyPress()
    {
        if (!heldEarlyPress)
        {
            return;
        }

        if (!attackHeld || !IsAttacking)
        {
            heldEarlyPress = false;
            return;
        }

        if (queuedKind == SwingKind.None && BufferOpen(SwingKind.Light))
        {
            heldEarlyPress = false;
            OnAttackPressed();
        }
    }

    private void OnAttackReleased()
    {
        attackHeld = false;
        heldEarlyPress = false;
        if (pressTime < 0f || weapon != null && weapon.TakesAttackInput)
        {
            return;
        }

        pressTime = -1f;
    }

    // An attack out of a dodge, or on the run toward where it aims, is the weapon's rush attack.
    private bool WantsRush()
    {
        if (weapon == null || weapon.RushAttack == null || activeKind != SwingKind.None)
        {
            return false;
        }

        if (dodge != null && (dodge.IsDodging || dodge.JustDodged(Rules.RushAfterDodgeSeconds)))
        {
            return true;
        }

        return StrikeRules.IsRunningAttack(runningFor, Rules.RushRunSeconds, Vector3.Angle(movement.HorizontalVelocity, movement.AimDirection), Rules.RushRunAngle);
    }

    // Focus pays for the art; short of it, stamina does (Combat Technique Settings › Art Stamina Without Focus). It used to
    // be Focus alone, and guard-then-attack turned into a plain swing most of the time.
    private bool CanAffordArt => weapon != null && ArtAttack != null && !weapon.TakesAttackInput
        && (focus == null || focus.CanSpend(ArtFocus) || stamina == null || stamina.Current >= stamina.Scaled(CostOfArtOnStamina()));

    // The art the next Art swing strikes: the attuned rune's, asked for by attacking out of the guard, else the weapon's own.
    private bool artFromRune;
    private ItemDefinition ArtRune => artFromRune ? ArtRunes.For(weapon) : null;
    private AttackDefinition ArtAttack => ArtRune != null ? ArtRune.RuneArt : weapon != null ? weapon.WeaponArt : null;
    private float ArtFocus => ArtRune != null ? ArtRune.RuneFocus : weapon != null ? weapon.WeaponArtFocus : 0f;

    private bool HasGuardCounter => (HasShield && Shield != null && Shield.OwnGuardCounter != null) || (weapon != null && weapon.GuardCounterAttack != null);

    private float CostOfArtOnStamina() => Rules.ArtStaminaWithoutFocus * Mathf.Max(0.1f, 1f + combatant.Stats.Get(StatType.MeleeStaminaCost));

    // The middle button: the next arrow with a bow (PlayerArchery), the weapon's art with anything else.
    private void OnArtRequested()
    {
        artFromRune = false;
        if (weapon == null || weapon.TakesAttackInput || weapon.WeaponArt == null)
        {
            return;
        }

        if (!CanAffordArt)
        {
            if (focus != null) focus.TrySpend(weapon.WeaponArtFocus); // refused: lets the HUD say so
            return;
        }

        // Not ready yet: the guard stays up and nothing is spent.
        if (ArtCoolingDown)
        {
            ArtNotReady?.Invoke();
            return;
        }

        if (guard != null && guard.IsGuarding)
        {
            guard.Lower();
        }

        Request(SwingKind.Art, ChainPress.Late);
    }

    private void UpdateHeldPress()
    {
        if (pressTime < 0f || heavyStarted)
        {
            return;
        }

        if (Time.time - pressTime < Rules.HeavyHoldSeconds)
        {
            return;
        }

        // Still winding up the light swing this press began (at once, or buffered behind the swing before): it becomes the
        // heavy one, paying only the difference. A counter stays a counter, and without the stamina for the difference the
        // light swing goes on.
        bool turn = weapon != null && activeKind == SwingKind.Light && activeStartTime >= pressTime && hitPending && (activeTechnique & HitTechnique.Counter) == 0;
        heavyStarted = true;
        pressTime = -1f;
        if (turn)
        {
            float credit = CostOf(SwingKind.Light, activeAttack);
            float extra = Mathf.Max(0f, CostOf(SwingKind.Heavy, weapon.HeavyAttack) - credit);
            if (stamina != null && stamina.Current < stamina.Scaled(extra))
            {
                return;
            }

            // The arms are already part way up the light wind-up: the heavy one goes on from as far, not from its first frame
            // (the user, 2026-11-15: holding for a heavy looked like a light swing changing its mind).
            float elapsed = ActiveElapsed;
            HeavyGrownFrom = Mathf.Clamp(elapsed / Mathf.Max(0.01f, elapsed + TimeToHit), 0f, 0.8f);
            EndSwing();
            heavyCredit = credit;
            Begin(SwingKind.Heavy, ChainPress.Late);
            heavyCredit = 0f;
            HeavyGrownFrom = 0f;
            return;
        }

        // A light swing that has already landed (or a press buffered behind another swing): the heavy one follows.
        Request(SwingKind.Heavy, ChainPress.Late);
    }

    // Kept for gamepads and scripts that ask for a bash directly.
    private void OnShieldBashRequested()
    {
        if (HasShield)
        {
            Request(SwingKind.ShieldBash, ChainPress.Late);
        }
    }

    private ChainPress JudgePress()
    {
        if (activeKind != SwingKind.None)
        {
            return ComboRules.Judge(ActiveElapsed, HitAt(activeAttack, activeSpeed), RecoverAt(activeAttack, activeSpeed), Rules);
        }

        return ComboRules.Judge(Time.time - lastStartTime, lastHitAt, lastRecoverAt, Rules);
    }

    // True when the swing starts now or is kept for later; false when the press is dropped (eating, the lockout after a dodge cut a
    // wind-up short, or too early to keep: the bounded buffer).
    private bool Request(SwingKind kind, ChainPress judge)
    {
        if (eating != null && eating.IsEating)
        {
            return false; // eating takes both hands
        }

        if (Time.time < attackLockedUntil)
        {
            return false;
        }

        bool busy = activeKind != SwingKind.None || IsFlinching || (dodge != null && dodge.IsDodging) || (guard != null && guard.IsStunned);
        if (busy)
        {
            if (!BufferOpen(kind))
            {
                return false;
            }

            // Buffered: it starts as soon as the current swing has recovered or the dodge has ended.
            queuedKind = kind;
            queuedJudge = judge;
            return true;
        }

        Begin(kind, judge);
        return true;
    }

    // The bounded buffer (SwingMotion.Buffers): a press is kept for the next swing only in the last moments before the swing in
    // progress can chain, or before a dodge, flinch or broken guard ends. A rush out of a dodge is the dodge's own attack, kept
    // at any point of it.
    private bool BufferOpen(SwingKind kind)
    {
        if (activeKind != SwingKind.None)
        {
            return SwingMotion.Buffers(ActiveElapsed, RecoverAt(activeAttack, activeSpeed), Rules);
        }

        if (dodge != null && dodge.IsDodging)
        {
            return kind == SwingKind.Rush || SwingMotion.BuffersWithin(dodge.TimeLeft, Rules);
        }

        if (IsFlinching)
        {
            return SwingMotion.BuffersWithin(flinchUntil - Time.time, Rules);
        }

        return guard == null || !guard.IsStunned || SwingMotion.BuffersWithin(guard.StunLeft, Rules);
    }
}
