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

        // The swing the guard cut short is still recovering (GiveWayToGuard). A blow just caught on the guard can still be answered with
        // the guard counter. Under the guard the press is let go and the guard stays up; with the guard down it is kept, and the swing
        // starts once the recovery is paid, as a press in any recovery is (review 2026-11-27: it was dropped).
        bool guarding = guard != null && guard.IsGuarding;
        if (OwesRecovery && !(guarding && HasGuardCounter && weapon != null && guard.JustBlocked(Rules.GuardCounterSeconds)))
        {
            if (!guarding)
            {
                queuedKind = SwingKind.Light;
                queuedJudge = ChainPress.Late;
            }

            return;
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
            // With a shield, out of the guard the weapon's art when Focus will pay for it and it's ready (the user, 2026-11-19: the
            // shield took the sword's special away), else the bash.
            else if (HasShield && !(ArtReadyFromGuard()))
            {
                Request(SwingKind.ShieldBash, ChainPress.Late);
                return;
            }
            else
            {
                // Attacking drops the guard into an art, paid in Focus: the attuned rune's (ArtRunes) when there is one, else the
                // weapon's own; a plain swing when Focus is short (without a shield).
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
        attackPressedAt = Time.time;
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
        heavyAfterLight = false;
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

    // The key taken away (a pause, a window, a hold), not let go: the press is spent, nothing queued starts, and a heavy waiting at the
    // top of its charge is broken off unstruck, as the guard breaks it off (review 2026-11-27: it struck, charged, as the game resumed).
    private void OnAttackKeyTaken()
    {
        attackHeld = false;
        heldEarlyPress = false;
        pressTime = -1f;
        queuedKind = SwingKind.None;
        heavyAfterLight = false;
        if (charging && activeKind == SwingKind.Heavy && hitPending)
        {
            EndSwing();
        }
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

        // A press that still chains on from the last swing is the chain's next swing, walked or not: walking out of a swing and
        // pressing again made a rush and started the chain over (the user, 2026-11-19; 77 rushes in 751 swings of their playtest).
        if (lastStep >= 0 && JudgePress() != ChainPress.Late)
        {
            return false;
        }

        return StrikeRules.IsRunningAttack(runningFor, Rules.RushRunSeconds, Vector3.Angle(movement.HorizontalVelocity, movement.AimDirection), Rules.RushRunAngle);
    }

    // Focus alone pays for an art (the user, 2026-11-16: arts were far too strong on stamina as well; short of Focus, guard then
    // attack is a plain swing): one full circle (2026-11-24), and its follow-ups nothing while their window is open.
    private bool CanAffordArt => weapon != null && ArtAttack != null && !weapon.TakesAttackInput && (focus == null || ArtFollowUpOpen || focus.HasCircle);

    // The art the next Art swing strikes: the attuned rune's, asked for by attacking out of the guard, else the weapon's own.
    private bool artFromRune;
    private ItemDefinition ArtRune => artFromRune ? ArtRunes.For(weapon) : null;
    private AttackDefinition ArtAttack => ArtRune != null ? ArtRune.RuneArt : weapon != null ? weapon.WeaponArt : null;

    // Whether attacking out of the guard would start an art now: one to strike, Focus for it, and off its cooldown.
    private bool ArtReadyFromGuard()
    {
        bool fromRune = artFromRune;
        artFromRune = ArtRunes.For(weapon) != null;
        bool ready = CanAffordArt && !ArtCoolingDown;
        artFromRune = fromRune;
        return ready;
    }

    private bool HasGuardCounter => (HasShield && Shield != null && Shield.OwnGuardCounter != null) || (weapon != null && weapon.GuardCounterAttack != null);

    /// <summary>
    /// The art of the weapon in hand, asked for from the skill bar (SkillDefinition.WeaponArt): its own, or its attuned rune's. The same
    /// checks and costs as the middle button and the guard's art; true when it started.
    /// </summary>
    public bool TryArt(bool fromRune)
    {
        artFromRune = fromRune;
        if (weapon == null || weapon.TakesAttackInput || ArtAttack == null || (fromRune && ArtRune == null)
            || (stateMachine != null && !stateMachine.CanStartAttack))
        {
            artFromRune = false;
            return false;
        }

        if (!CanAffordArt)
        {
            if (focus != null) focus.TrySpendCircle(); // refused: lets the HUD say so
            artFromRune = false;
            return false;
        }

        if (ArtCoolingDown)
        {
            ArtNotReady?.Invoke();
            artFromRune = false;
            return false;
        }

        if (guard != null && guard.IsGuarding)
        {
            guard.Lower();
        }

        // The swing may still refuse (eating, a lock after a dodge): then nothing started, and the rune is forgotten.
        if (!Request(SwingKind.Art, ChainPress.Late))
        {
            artFromRune = false;
            return false;
        }

        return true;
    }

    /// <summary>Whether the weapon in hand has that art to strike: its own, or a rune attuned to it.</summary>
    public bool HasArt(bool fromRune) => weapon != null && (fromRune ? ArtRunes.For(weapon) != null && ArtRunes.For(weapon).RuneArt != null : weapon.WeaponArt != null);

    /// <summary>Share of the art's cooldown still to go (the bar's sweep), 1 just used to 0 ready.</summary>
    public float ArtCooldownFraction => ArtFollowUpOpen ? 0f : Rules.ArtCooldown > 0f ? Mathf.Clamp01((artReadyAt - Time.time) / Rules.ArtCooldown) : 0f;

    // The middle button: the next arrow with a bow (PlayerArchery), the weapon's art with anything else.
    private void OnArtRequested()
    {
        artFromRune = false;
        // As any attack: not swimming, at the oars or a cart's handles, nor mid-interaction (gathering, crafting).
        if (weapon == null || weapon.TakesAttackInput || weapon.WeaponArt == null || (stateMachine != null && !stateMachine.CanStartAttack))
        {
            return;
        }

        if (!CanAffordArt)
        {
            if (focus != null) focus.TrySpendCircle(); // refused: lets the HUD say so
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

    // A heavy that is another motion than the light swing (Grows From Light off: the fists' high kick) follows the light one instead
    // of replacing it mid-wind-up: asked for as soon as the light one can be chained from, kept until then.
    private bool heavyAfterLight;

    private void UpdateHeldPress()
    {
        // Only behind the light swing it follows: once that is gone (guarded out of, swapped, a flinch, a dodge) it is dropped
        // (review 2026-11-20: a guard raised over the punch's recovery still kicked).
        if (heavyAfterLight && (activeKind != SwingKind.Light || (guard != null && guard.IsGuarding)))
        {
            heavyAfterLight = false;
        }

        if (heavyAfterLight && BufferOpen(SwingKind.Heavy))
        {
            heavyAfterLight = false;
            Request(SwingKind.Heavy, ChainPress.Late);
        }

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
        bool turn = weapon != null && activeKind == SwingKind.Light && activeBeganAt >= pressTime && hitPending && (activeTechnique & HitTechnique.Counter) == 0;
        heavyStarted = true;
        pressTime = -1f;
        if (turn && (weapon.HeavyAttack == null || !weapon.HeavyAttack.GrowsFromLight))
        {
            // Another motion: the light swing lands, then the heavy one (a punch, then the kick).
            heavyAfterLight = true;
            return;
        }
        if (turn)
        {
            float credit = CostOf(SwingKind.Light, activeAttack);
            float extra = Mathf.Max(0f, CostOf(SwingKind.Heavy, weapon.HeavyAttack) - credit);
            if (stamina != null && stamina.Current < stamina.Scaled(extra))
            {
                return;
            }

            // The arms are already part way up the light wind-up: the heavy one goes on from as far, not from its first frame
            // (the user, 2026-11-15: holding for a heavy looked like a light swing changing its mind). Not when the heavy is
            // another motion (Grows From Light off): the fists' kick took up a punch's pose 80% in and crawled through the rest
            // of its wind-up at a fifth of its speed, then struck all at once (the user, 2026-11-16).
            float elapsed = ActiveElapsed;
            HeavyGrownFrom = weapon.HeavyAttack != null && weapon.HeavyAttack.GrowsFromLight
                ? Mathf.Clamp(elapsed / Mathf.Max(0.01f, elapsed + TimeToHit), 0f, 0.8f) : 0f;
            EndSwing();
            heavyCredit = credit;
            Begin(SwingKind.Heavy, ChainPress.Late);
            heavyCredit = 0f;
            HeavyGrownFrom = 0f;
            return;
        }

        // A light swing that has already landed (or a press buffered behind another swing): the heavy one follows, kept until the
        // swing can be chained from if it can't yet (it used to be dropped).
        if (!Request(SwingKind.Heavy, ChainPress.Late) && IsAttacking)
        {
            heavyAfterLight = true;
        }
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

        if (Time.time < attackLockedUntil || (OwesRecovery && kind != SwingKind.GuardCounter))
        {
            return false;
        }

        bool busy = activeKind != SwingKind.None || IsFlinching || (dodge != null && dodge.IsDodging) || (guard != null && guard.IsStunned)
                    || (skills != null && skills.IsBusy);
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

        return Begin(kind, judge);
    }

    // The bounded buffer (SwingMotion.Buffers): a press is kept for the next swing only in the last moments before the swing in
    // progress can chain, or before a dodge, flinch or broken guard ends. A rush out of a dodge is the dodge's own attack, kept
    // at any point of it.
    private bool BlowResolved => activeAttack != null && !hitPending;

    private bool BufferOpen(SwingKind kind)
    {
        if (activeKind != SwingKind.None)
        {
            // Once the blow has landed, a press is kept for the next swing wherever it comes (the user, 2026-11-23: "sometimes when I
            // want to combo, it just restarts at the first attack": a press in the follow-through was dropped). In the wind-up it is
            // still only kept in the buffer's last moments, so mashing doesn't queue swings. "Landed" needs a blow that was due: a swing
            // with no attack never has one pending, and must not keep presses from its first frame.
            return BlowResolved || SwingMotion.Buffers(ActiveElapsed, RecoverAt(activeAttack, activeSpeed), Rules);
        }

        if (dodge != null && dodge.IsDodging)
        {
            return kind == SwingKind.Rush || SwingMotion.BuffersWithin(dodge.TimeLeft, Rules);
        }

        // A skill under way (a lunge in flight): a swing pressed in it follows it, wherever in it the press came.
        if (skills != null && skills.IsBusy)
        {
            return kind == SwingKind.Light || kind == SwingKind.Heavy || SwingMotion.BuffersWithin(skills.BusyLeft, Rules);
        }

        if (IsFlinching)
        {
            return SwingMotion.BuffersWithin(flinchUntil - Time.time, Rules);
        }

        return guard == null || !guard.IsStunned || SwingMotion.BuffersWithin(guard.StunLeft, Rules);
    }
}
