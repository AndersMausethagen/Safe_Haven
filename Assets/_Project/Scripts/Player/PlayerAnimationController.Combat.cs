using UnityEngine;

/// <summary>
/// The player fighting: swings at their speed (on the arms while walking, the whole body when planted, walking out of a
/// recovery), the shield bash, guard, parry and guard break, dodges, skills, and being hit. Part of
/// PlayerAnimationController, in its own file.
/// </summary>
public sealed partial class PlayerAnimationController
{
    // A swing's shape: a slow build-up, a fast strike, and it stands at the top of its wind-up while a heavy swing charges.
    private void UpdateSwingSpeed()
    {
        if (combat == null || !combat.IsWindingUp)
        {
            ReleasePlantedSwing();
            return;
        }

        AttackDefinition attack = combat.ActiveAttack;
        if (attack == null || string.IsNullOrEmpty(attack.AnimationState))
        {
            return;
        }

        // A heavy swing grown out of a light one started part way into its wind-up clip: the rest of the wind-up plays slower by as
        // much, so the strike frame still meets the blow.
        float windup = attack.AnimationSpeed * (1f - grownFrom);
        float speed = combat.IsHoldingSwing ? 0f : combat.TimeToHit <= attack.StrikeLead ? attack.StrikeSpeed : windup;
        SetFloat(ActionSpeedHash, speed * swingSpeed);
        if (plantedSwing != 0 && !combat.IsCharging && combat.TimeToHit <= Mathf.Max(attack.StrikeLead, PlantedStrikeLead))
        {
            StrikeWithWholeBody();
        }
    }

    // A heavy swing winds up and charges on the upper body, with the feet planted on the ground: its clip may stride or leap, and
    // holding a pose like that for as long as the button is held leaves the player hanging in the air. The legs join in for the strike.
    private void StrikeWithWholeBody()
    {
        int upper = upperBodyLayer;
        AnimatorStateInfo state = animator.IsInTransition(upper) && animator.GetNextAnimatorStateInfo(upper).shortNameHash == plantedSwing
            ? animator.GetNextAnimatorStateInfo(upper) : animator.GetCurrentAnimatorStateInfo(upper);
        AnimatorClipInfo[] clips = animator.IsInTransition(upper) ? animator.GetNextAnimatorClipInfo(upper) : animator.GetCurrentAnimatorClipInfo(upper);
        float clipTime = clips.Length > 0 ? state.normalizedTime * clips[0].clip.length : 0f;
        if (fullBodyLayer >= 0 && animator.HasState(fullBodyLayer, plantedSwing))
        {
            animator.CrossFadeInFixedTime(plantedSwing, PlantedStrikeFade, fullBodyLayer, clipTime);
            animator.CrossFadeInFixedTime(EmptyStateHash, PlantedStrikeFade, upper);
        }

        plantedSwing = 0;
    }

    // A heavy swing lost before it struck (a flinch): the arms mustn't stay standing at the top of its wind-up.
    private void ReleasePlantedSwing()
    {
        if (plantedSwing == 0)
        {
            return;
        }

        plantedSwing = 0;
        if (dodge == null || !dodge.IsDodging)
        {
            SetFloat(ActionSpeedHash, 1f);
            if (animator.GetCurrentAnimatorStateInfo(upperBodyLayer).shortNameHash != KnockbackStateHash)
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, HeavyFade, upperBodyLayer);
            }
        }
    }

    // A blow that has landed gives the legs back: when the player walks off, the arms finish the swing over them from
    // where the body had got to, instead of a whole-body swing skating along the ground. That includes a swing whose
    // gameplay has ended but whose animation tail is still on the whole body. A swing that lets the player walk through its
    // wind-up (SwingMotion) hands over the same way when they start walking, but not while it steps into the blow: that step
    // is the legs' own.
    private void WalkOutOfSwing()
    {
        if (combat == null || movement.HorizontalSpeed <= ArmsOnlySpeed
            || fullBodyLayer < 0 || attackArmsLayer < 0 || animator.IsInTransition(fullBodyLayer))
        {
            return;
        }

        if (combat.IsWindingUp && (combat.WindupMoveShare <= 0f || combat.IsSteppingIn || movement.MovementInput.sqrMagnitude < 0.01f))
        {
            return;
        }

        AnimatorStateInfo swing = animator.GetCurrentAnimatorStateInfo(fullBodyLayer);
        if (swing.shortNameHash == EmptyStateHash)
        {
            return;
        }

        // Swings are the full-body states the arms layer also has; dodges and hit reactions are left alone.
        bool isSwing = animator.HasState(attackArmsLayer, swing.shortNameHash);
        if (!isSwing && !combat.IsAttacking)
        {
            return;
        }

        if (isSwing)
        {
            animator.CrossFadeInFixedTime(swing.shortNameHash, WalkOutFade, MovingSwingLayer(swing.shortNameHash), swing.normalizedTime * swing.length);
        }

        animator.CrossFadeInFixedTime(EmptyStateHash, WalkOutFade, fullBodyLayer);
    }

    // A swing over walking legs plays on the Upper Body layer (Human Body Upper Mask: spine, chest, head and arms), as the pack's author
    // mixes them: the torso turns into the blow while the legs walk or strafe (Combat Locomotion, the same pack's Walk01/Run01). The
    // arms-only layer left the swing coming out of an upright walking torso, which is what looked wrong (the user, 2026-11-15).
    private int MovingSwingLayer(int hash) =>
        upperBodyLayer >= 0 && hash != 0 && animator.HasState(upperBodyLayer, hash) ? upperBodyLayer : attackArmsLayer;

    /// <summary>Plays the attack for the current weapon stance. One-handed attacks alternate between the two slashes.</summary>
    public void PlayAttack()
    {
        // A request made while one is still waiting for the animator folds into it, so only the first one alternates.
        bool alreadyQueued = parameters.Contains(AttackHash) && animator.GetBool(AttackHash);
        if (weaponStance <= WeaponStance.OneHanded && !alreadyQueued)
        {
            attackIndex = 1 - attackIndex;
            SetInt(AttackIndexHash, attackIndex);
        }

        SetTrigger(AttackHash);
    }

    public void PlayShieldBash()
    {
        SetTrigger(ShieldBashHash);
    }

    // A swing with its own animation state plays it at once: with the whole body (the legs step into the blow, which is
    // what reads from above) when it starts standing. Started on the move, by a swing that lets the player walk through its
    // wind-up (SwingMotion), it goes on the arms over the walking legs. One without a state uses the stance's older triggered attack.
    private void OnSwingStarted(SwingInfo swing)
    {
        string stateName = swing.Attack != null ? swing.Attack.AnimationState : null;
        int standing = fullBodyLayer >= 0 ? fullBodyLayer : upperBodyLayer;
        bool onTheRun = combat != null && combat.WindupMoveShare > 0f && movement.HorizontalSpeed > ArmsOnlySpeed
            && movement.MovementInput.sqrMagnitude >= 0.01f;
        int hash = string.IsNullOrEmpty(stateName) ? 0 : Animator.StringToHash(stateName);
        int layer = onTheRun && attackArmsLayer >= 0 ? MovingSwingLayer(hash) : standing;
        plantedSwing = 0;
        // A charged blow is already past its charge: it strikes with the whole body (it may leap).
        if (swing.Kind == SwingKind.Heavy && (swing.Technique & HitTechnique.Charged) == 0 && hash != 0 && upperBodyLayer >= 0
            && animator.HasState(upperBodyLayer, hash))
        {
            layer = upperBodyLayer;
            plantedSwing = hash;
        }

        if (hash == 0 || layer < 0 || !animator.HasState(layer, hash))
        {
            if (swing.Kind == SwingKind.ShieldBash)
            {
                PlayShieldBash();
            }
            else
            {
                PlayAttack();
            }

            return;
        }

        swingSpeed = swing.Speed;
        // Grown out of the light swing its press began: take up the heavy clip as far into its wind-up as the light one had got,
        // so the arms go on rising instead of dropping back to the first frame.
        grownFrom = swing.Kind == SwingKind.Heavy && combat != null ? combat.HeavyGrownFrom : 0f;
        float start = 0f;
        if (grownFrom > 0f)
        {
            float windupClip = Mathf.Max(0f, combat.TimeToHit - swing.Attack.StrikeLead) * swing.Attack.AnimationSpeed * swing.Speed;
            start = windupClip * grownFrom;
        }

        // A full charge let go: the charged attack starts at the top of its backswing (where its wind-up ends), as the hold stood there.
        bool fromHold = swing.Kind == SwingKind.Heavy && combat != null && combat.ChargedFromHold;
        if (fromHold)
        {
            AttackDefinition charged = swing.Attack;
            start = charged.AnimationSpeed * Mathf.Max(0f, charged.HitDelay - charged.StrikeLead * swing.Speed);
        }

        SetFloat(ActionSpeedHash, fromHold ? swing.Attack.StrikeSpeed * swing.Speed : swing.Attack.AnimationSpeed * swing.Speed * (1f - grownFrom));
        float fade = swing.Kind == SwingKind.Heavy ? HeavyFade : swing.Kind == SwingKind.Rush ? RushFade : ActionFade;
        animator.CrossFadeInFixedTime(hash, fade, layer, start);
        // Grown out of a light swing, the legs leave its stance for the planted heavy's slowly, so the hips don't twitch.
        float othersFade = grownFrom > 0f ? GrownLegsFade : fade;
        foreach (int other in new[] { upperBodyLayer, attackArmsLayer, fullBodyLayer })
        {
            if (other >= 0 && other != layer && animator.HasState(other, EmptyStateHash))
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, othersFade, other);
            }
        }
    }

    // The wind-up plays slower so it can be read; from the hit on, the strike plays at its own speed and snaps through.
    private void OnSwingLanded(SwingInfo swing, int hits)
    {
        if (swing.Attack != null && !string.IsNullOrEmpty(swing.Attack.AnimationState))
        {
            SetFloat(ActionSpeedHash, swing.Attack.FollowThroughSpeed * swing.Speed);
        }
    }

    private void OnGuardChanged(bool guarding)
    {
        SetBool(HasShieldHash, combat != null && combat.HasShield);
        SetBool(GuardingHash, guarding);
        if (guarding)
        {
            // Straight into the guard, cutting off whatever swing was ending.
            CrossFadeUpper(combat != null && combat.HasShield ? GuardShieldStateHash : GuardStateHash, 0.08f);
            foreach (int other in new[] { attackArmsLayer, fullBodyLayer })
            {
                if (other >= 0 && animator.HasState(other, EmptyStateHash))
                {
                    animator.CrossFadeInFixedTime(EmptyStateHash, 0.08f, other);
                }
            }
        }
    }

    private void OnParried(DamageInfo damage) => CrossFadeUpper(ParryStateHash, 0.04f);

    // The dodge the gear allows (quickstep, roll, heavy roll) plays with the whole body; anything on the other layers stops.
    private void OnDodgeStarted(DodgeStyle style, DodgeShape shape)
    {
        int hash = string.IsNullOrEmpty(shape.animationState) ? 0 : Animator.StringToHash(shape.animationState);
        if (fullBodyLayer < 0 || hash == 0 || !animator.HasState(fullBodyLayer, hash))
        {
            return;
        }

        SetFloat(ActionSpeedHash, shape.animationSpeed * (dodge != null ? dodge.AnimationPace : 1f));
        animator.CrossFadeInFixedTime(hash, 0.04f, fullBodyLayer, 0f);
        foreach (int other in new[] { upperBodyLayer, attackArmsLayer })
        {
            if (other >= 0 && animator.HasState(other, EmptyStateHash))
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, 0.04f, other);
            }
        }
    }

    private void OnGuardBroken(float seconds) => CrossFadeUpper(GuardBreakStateHash, 0.05f);

    // A skill plays its own state (a cast, a dash, a spin) where it has one; else the weapon stance's old attack.
    private void OnSkillCast(int slot, SkillDefinition skill)
    {
        if (!skill.PlayAttackAnimation)
        {
            return;
        }

        int state = string.IsNullOrEmpty(skill.AnimationState) ? 0 : Animator.StringToHash(skill.AnimationState);
        int layer = fullBodyLayer >= 0 ? fullBodyLayer : upperBodyLayer;
        if (state != 0 && layer >= 0 && animator.HasState(layer, state))
        {
            SetFloat(ActionSpeedHash, skill.AnimationSpeed);
            animator.CrossFadeInFixedTime(state, ActionFade, layer, 0f);
            foreach (int other in new[] { upperBodyLayer, attackArmsLayer, fullBodyLayer })
            {
                if (other >= 0 && other != layer && animator.HasState(other, EmptyStateHash))
                {
                    animator.CrossFadeInFixedTime(EmptyStateHash, ActionFade, other);
                }
            }

            return;
        }

        PlayAttack();
    }

    public void PlayHit()
    {
        SetTrigger(HitHash);
    }

    // A flinch on every hit that hurts; the killing blow is left to whatever handles death.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (result.Damage <= 0f || result.Killed || result.Blocked || damage.IsOverTime)
        {
            return;
        }

        if (health != null && result.Damage >= health.MaxHealth * KnockbackShare && upperBodyLayer >= 0
            && animator.HasState(upperBodyLayer, KnockbackStateHash))
        {
            CrossFadeUpper(KnockbackStateHash, 0.05f);
            return;
        }

        // A flinch for the stance in hand (the Stylized pack's own per stance), else the old one.
        int stanceHit = Animator.StringToHash(weaponStance == WeaponStance.OneHanded ? "Hit 1H" : weaponStance == WeaponStance.TwoHanded ? "Hit 2H"
            : weaponStance == WeaponStance.Polearm ? "Hit Polearm" : "Hit Unarmed");
        if (upperBodyLayer >= 0 && weaponStance != WeaponStance.Bow && animator.HasState(upperBodyLayer, stanceHit))
        {
            CrossFadeUpper(stanceHit, 0.05f);
            return;
        }

        PlayHit();
    }

    // Floored by a huge blow (PlayerCombat: Knockdown Share): the whole body goes down and gets up again.
    private void OnKnockedDown(float seconds)
    {
        int state = Animator.StringToHash("Knockdown");
        if (fullBodyLayer < 0 || !animator.HasState(fullBodyLayer, state))
        {
            return;
        }

        SetFloat(ActionSpeedHash, 1f);
        animator.CrossFadeInFixedTime(state, 0.06f, fullBodyLayer, 0f);
        foreach (int other in new[] { upperBodyLayer, attackArmsLayer })
        {
            if (other >= 0 && animator.HasState(other, EmptyStateHash))
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, 0.06f, other);
            }
        }
    }

    // A dodge cancels the swing in gameplay, so a swing still waiting for the animator shouldn't play afterwards.
    private void OnDodged(Vector3 direction)
    {
        if (parameters.Contains(AttackHash))
        {
            animator.ResetTrigger(AttackHash);
        }

        if (parameters.Contains(ShieldBashHash))
        {
            animator.ResetTrigger(ShieldBashHash);
        }
    }
}
