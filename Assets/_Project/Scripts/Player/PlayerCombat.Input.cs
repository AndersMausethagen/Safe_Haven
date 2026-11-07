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
            if (HasShield)
            {
                Request(SwingKind.ShieldBash, ChainPress.Late);
                return;
            }

            // Without a shield, attacking drops the guard: into the weapon art if Focus pays for it, else into a swing (a
            // ready counter is spent here).
            guard.Lower();
            if (CanAffordArt)
            {
                Request(SwingKind.Art, ChainPress.Late);
                return;
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
        // hold made every light swing late). Held on, UpdateHeldPress turns it into the heavy one.
        Request(SwingKind.Light, pressJudge);
        bool hasHeavy = weapon != null && weapon.HeavyAttack != null;
        pressTime = hasHeavy ? Time.time : -1f;
        heavyStarted = false;
    }

    private void OnAttackReleased()
    {
        attackHeld = false;
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

    private bool CanAffordArt => weapon != null && weapon.WeaponArt != null && !weapon.TakesAttackInput
        && focus != null && focus.CanSpend(weapon.WeaponArtFocus);

    // The middle button: the next arrow with a bow (PlayerArchery), the weapon's art with anything else.
    private void OnArtRequested()
    {
        if (weapon == null || weapon.TakesAttackInput || weapon.WeaponArt == null)
        {
            return;
        }

        if (focus != null && !focus.CanSpend(weapon.WeaponArtFocus))
        {
            focus.TrySpend(weapon.WeaponArtFocus); // refused: lets the HUD say so
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
        bool turn = activeKind == SwingKind.Light && activeStartTime >= pressTime && hitPending && (activeTechnique & HitTechnique.Counter) == 0;
        heavyStarted = true;
        pressTime = -1f;
        if (turn)
        {
            float credit = CostOf(SwingKind.Light, activeAttack);
            float extra = Mathf.Max(0f, CostOf(SwingKind.Heavy, weapon.HeavyAttack) - credit);
            if (stamina != null && stamina.Current < extra)
            {
                return;
            }

            EndSwing();
            heavyCredit = credit;
            Begin(SwingKind.Heavy, ChainPress.Late);
            heavyCredit = 0f;
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

    private void Request(SwingKind kind, ChainPress judge)
    {
        if (eating != null && eating.IsEating)
        {
            return; // eating takes both hands
        }

        bool busy = activeKind != SwingKind.None || IsFlinching || (dodge != null && dodge.IsDodging) || (guard != null && guard.IsStunned);
        if (busy)
        {
            // Buffered: it starts as soon as the current swing has recovered or the dodge has ended.
            queuedKind = kind;
            queuedJudge = judge;
            return;
        }

        Begin(kind, judge);
    }
}
