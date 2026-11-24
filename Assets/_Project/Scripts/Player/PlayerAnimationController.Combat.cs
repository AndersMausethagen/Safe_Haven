using UnityEngine;

/// <summary>
/// The player fighting: swings at their speed (on the arms while walking, the whole body when planted, walking out of a
/// recovery), the shield bash, guard, parry and guard break, dodges, skills, and being hit. Part of
/// PlayerAnimationController, in its own file.
/// </summary>
public sealed partial class PlayerAnimationController
{
    // The swing started before this one was a plain heavy (not a charged release): a heavy chain picks up where it ended.
    private bool lastWasPlainHeavy;

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
        // A slow wind-up gathers itself: faster at first, slowing into the top of the backswing, the same frame at the strike (SwingWindup).
        if (windupSpan > 0f)
        {
            windup = SwingWindup.Speed(windup, windup, 1f - Mathf.Max(0f, combat.TimeToHit - attack.StrikeLead) / windupSpan);
        }

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
            // ActionSpeed was just set for this frame (UpdateSwingSpeed), and the offset is read at that speed.
            // The upper body stays on the same swing until the whole body covers it (PlayerAnimationController.Layers).
            IntoFullBody(plantedSwing, PlantedStrikeFade, StateTime(clipTime, animator.GetFloat(ActionSpeedHash)));
        }

        plantedSwing = 0;
    }

    // CrossFadeInFixedTime's offset is in the state's own seconds, which run at its speed: a point of the clip at clipSeconds
    // is clipSeconds / speed into the state (measured: 0.3 at ActionSpeed 0.5 landed 0.15 s into the clip). Passing clip seconds
    // had planted heavies drop back towards the start of their wind-up at the strike (technical animation audit, 2026-11-20).
    private static float StateTime(float clipSeconds, float speed) => clipSeconds / Mathf.Max(0.01f, speed);

    // A heavy swing lost before it struck (a flinch): the arms mustn't stay standing at the top of its wind-up.
    private void ReleasePlantedSwing()
    {
        if (plantedSwing == 0)
        {
            return;
        }

        plantedSwing = 0;
        // Broken off into the guard: the guard has the upper body now (OnGuardChanged), it isn't faded out under it.
        if ((dodge == null || !dodge.IsDodging) && (guard == null || !guard.IsGuarding))
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
        if (combat == null || movement.HorizontalSpeed <= ArmsOnlySpeed || fullBodyLayer < 0 || attackArmsLayer < 0)
        {
            return;
        }

        // A skill's own dash (Wolf's Lunge, Mountain Leap) is the move, not walking: its whole-body clip plays on to the blow.
        if (skills != null && Time.time < skills.LungeEndsAt)
        {
            return;
        }

        // Settling back to the ready pose (its exit to Empty, up to 0.45 s) is walked out of too; another change on the way isn't.
        if (animator.IsInTransition(fullBodyLayer) && animator.GetNextAnimatorStateInfo(fullBodyLayer).shortNameHash != EmptyStateHash)
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

        // Once per swing: during the hand-over (Full Body on its way to Empty) the arms already play it.
        bool leaving = animator.IsInTransition(fullBodyLayer);
        if (isSwing)
        {
            int moving = MovingSwingLayer(swing.shortNameHash);
            bool handedOver = animator.GetCurrentAnimatorStateInfo(moving).shortNameHash == swing.shortNameHash
                              || (animator.IsInTransition(moving) && animator.GetNextAnimatorStateInfo(moving).shortNameHash == swing.shortNameHash);
            if (!handedOver)
            {
                UnderFullBody(moving, swing.shortNameHash, WalkOutFade, swing.normalizedTime * swing.length, WalkOutFade, true);
            }

            return;
        }

        if (!leaving)
        {
            animator.CrossFadeInFixedTime(EmptyStateHash, Blend(WalkOutFade, true), fullBodyLayer);
        }
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
    // The swing in hand, for its follow-through speed after a hold on its last pose.
    private SwingInfo shownSwing;
    // Nearly still, not frozen: a breath of motion keeps a held pose from reading as a hitch.
    private const float HeldPoseSpeed = 0.04f;

    // A recovered swing waits on its last pose (PlayerCombat.SwingHeld): the clip all but stops there, and goes on when the wait ends.
    private void OnSwingHeld(bool holding)
    {
        if (holding)
        {
            SetFloat(ActionSpeedHash, HeldPoseSpeed);
        }
        else if (shownSwing.Attack != null)
        {
            SetFloat(ActionSpeedHash, shownSwing.Attack.FollowThroughSpeed * shownSwing.Speed);
        }
    }

    private void OnSwingStarted(SwingInfo swing)
    {
        shownSwing = swing;
        string stateName = swing.Attack != null ? swing.Attack.AnimationState : null;
        int standing = fullBodyLayer >= 0 ? fullBodyLayer : upperBodyLayer;
        bool onTheRun = combat != null && combat.WindupMoveShare > 0f && movement.HorizontalSpeed > ArmsOnlySpeed
            && movement.MovementInput.sqrMagnitude >= 0.01f;
        int hash = string.IsNullOrEmpty(stateName) ? 0 : Animator.StringToHash(stateName);
        int layer = onTheRun && attackArmsLayer >= 0 ? MovingSwingLayer(hash) : standing;
        plantedSwing = 0;
        bool afterPlainHeavy = lastWasPlainHeavy;
        lastWasPlainHeavy = swing.Kind == SwingKind.Heavy && (swing.Technique & HitTechnique.Charged) == 0;
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
        // so the arms go on rising instead of dropping back to the first frame. Out of the guard (after a parry or a block) a swing
        // picks up as far in as its Out Of Guard Start says, at the frame nearest the guard. Either way the rest of the wind-up plays
        // slower by as much (grownFrom), so the blow lands on time.
        // Straight out of a heavy (a heavy chain), it picks up at the frame nearest where that heavy ended (From Heavy Start).
        bool grown = swing.Kind == SwingKind.Heavy && combat != null && combat.HeavyGrownFrom > 0f;
        bool outOfHeavy = swing.Kind == SwingKind.Heavy && lastWasPlainHeavy && SwingShowing();
        grownFrom = grown ? combat.HeavyGrownFrom : OutOfTheGuard() ? swing.Attack.FromGuardStart
            : outOfHeavy ? swing.Attack.FromHeavyStart : 0f;

        float start = 0f;
        if (grownFrom > 0f)
        {
            float windupClip = Mathf.Max(0f, combat.TimeToHit - swing.Attack.StrikeLead) * swing.Attack.AnimationSpeed * swing.Speed;
            start = windupClip * grownFrom;
        }

        // A full charge let go: the charged attack starts at the top of its backswing (where its wind-up ends), as the hold stood there,
        // or as much before it as the blow is still away (PlayerCombat leaves at least the blend's length), and blends in quickly: only
        // the strike is left, and the long heavy blend had the blow land mid-blend (review 2026-11-16).
        bool fromHold = swing.Kind == SwingKind.Heavy && combat != null && combat.ChargedFromHold;
        bool striking = false;
        if (fromHold)
        {
            AttackDefinition charged = swing.Attack;
            float left = Mathf.Max(combat.TimeToHit, charged.StrikeLead);
            start = charged.AnimationSpeed * swing.Speed * Mathf.Max(0f, charged.HitDelay / swing.Speed - left);
            striking = combat.TimeToHit <= charged.StrikeLead + 0.005f;
        }

        // The wind-up's span, for its shape (SwingWindup), judged at the speed it really plays (a grown heavy's rest is slower); a
        // charged release is past its wind-up.
        float effectiveWindup = swing.Attack.AnimationSpeed * (1f - grownFrom);
        windupSpan = !fromHold && combat != null && SwingWindup.Strength(effectiveWindup) > 0f
            ? Mathf.Max(0.01f, combat.TimeToHit - swing.Attack.StrikeLead) : 0f;
        float actionSpeed = striking ? swing.Attack.StrikeSpeed * swing.Speed : effectiveWindup * swing.Speed;
        if (!striking && windupSpan > 0f)
        {
            actionSpeed = SwingWindup.Speed(actionSpeed, effectiveWindup, 0f);
        }

        // Taking a weapon out or putting it away gives way to a swing: the weapon shows in the hand now (review 2026-11-20).
        if (equipment != null)
        {
            equipment.FinishHandMove();
        }
        SetFloat(ActionSpeedHash, actionSpeed);
        float fade = fromHold ? ChargedReleaseFade : swing.Kind == SwingKind.Heavy ? HeavyFade : swing.Kind == SwingKind.Rush ? RushFade : ActionFade;
        // From a swing still showing (a chain's next step, a heavy grown from the light): moving into moving, blended by coasting.
        bool chained = false;
        // Each swing blends in over its measured Blend In (AttackFlow: no way into it moves the body faster than the swing itself,
        // and it is over before the strike), the user's "every attack transitions smoothly and fits in a chain" (2026-11-20).
        // Without one, up to ChainFade, done by half-way to the blow.
        if (!fromHold)
        {
            float speed = Mathf.Max(0.01f, swing.Speed);
            // Never into the strike, not even the usual short blend (the fists' cross has 0.08 s).
            float beforeStrike = AttackFlowRules.BlendRoom(swing.Attack.HitDelay, swing.Attack.StrikeLead) / speed;
            // The blend AttackFlow measured for the way in it is: from the chain's swing before it, or from the ready pose (a guard,
            // the exploring or walking pose). A grown heavy takes up the light swing's pose, the same motion: a heavy's short blend.
            chained = SwingShowing();
            float measured = grown ? 0f : chained ? swing.Attack.BlendIn
                : OutOfTheGuard() && swing.Attack.BlendInFromGuard > 0f ? swing.Attack.BlendInFromGuard : swing.Attack.BlendInFromReady;
            if (measured <= 0f && !grown)
            {
                measured = swing.Attack.BlendIn;
            }

            float wanted = grown ? HeavyFade : measured > 0f ? measured / speed : Mathf.Min(ChainFade, 0.5f * swing.Attack.HitDelay / speed);
            fade = Mathf.Min(Mathf.Max(fade, wanted), beforeStrike);
        }

        // Swing states run at speed 1 times ActionSpeed (PlayerAnimator.controller), so the clip point is start / actionSpeed in.
        float offset = start > 0f ? StateTime(start, actionSpeed) : 0f;
        // Grown out of a light swing, the legs leave its stance for the planted heavy's over GrownLegsFade, so the hips don't twitch.
        float othersFade = grown ? GrownLegsFade : fade;
        // Hand-overs that never show the ready pose between (PlayerAnimationController.Layers).
        if (layer == fullBodyLayer)
        {
            IntoFullBody(hash, fade, offset, chained);
        }
        else
        {
            UnderFullBody(layer, hash, fade, offset, othersFade, chained);
            foreach (int other in new[] { upperBodyLayer, attackArmsLayer })
            {
                if (other >= 0 && other != layer && animator.HasState(other, EmptyStateHash) && LayerBusy(other))
                {
                    animator.CrossFadeInFixedTime(EmptyStateHash, Blend(othersFade, chained), other);
                }
            }
        }
    }

    // The guard (or its parry, or a blow taken on it) is showing on the upper body: a swing now starts out of the guard.
    private bool OutOfTheGuard()
    {
        if (upperBodyLayer < 0)
        {
            return false;
        }

        // Blending into one counts too: right after a parry the upper body is still on its way into it, slowed by the parry's
        // hit-stop (seen in the probe, 2026-11-20).
        bool Guarded(int state) => state == GuardStateHash || state == GuardShieldStateHash || state == ParryStateHash || state == ParryWeaponStateHash
                                   || state == GuardHitStateHash || state == GuardShieldHitStateHash;
        return Guarded(animator.GetCurrentAnimatorStateInfo(upperBodyLayer).shortNameHash)
               || (animator.IsInTransition(upperBodyLayer) && Guarded(animator.GetNextAnimatorStateInfo(upperBodyLayer).shortNameHash));
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
            // A Hit trigger left from a blow the guard state couldn't take would fire a late flinch once it ends.
            if (parameters.Contains(HitHash))
            {
                animator.ResetTrigger(HitHash);
            }

            if (equipment != null)
            {
                equipment.FinishHandMove();
            }

            // Straight into the guard, cutting off whatever swing was ending (Full Body handed over in CrossFadeUpper): the block is up
            // at once in play, but a follow-through it cuts eases over 0.15 s rather than snapping (the user, 2026-11-20).
            CrossFadeUpper(combat != null && combat.HasShield ? GuardShieldStateHash : GuardStateHash, SwingShowing() ? 0.15f : 0.08f);
            if (attackArmsLayer >= 0 && animator.HasState(attackArmsLayer, EmptyStateHash) && LayerBusy(attackArmsLayer))
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, 0.08f, attackArmsLayer);
            }
        }
    }

    // A shield parries with the shield; a weapon with the stance's own deflection (Parry Weapon), not a shield's motion.
    // Taking a weapon out or putting it away: the right hand reaches the hip or the back (KI's (un)sheathe clips on the upper body),
    // the model changing over at its grab (PlayerEquipment). Not over a swing, a guard or a flinch.
    private void OnWeaponMoving(bool puttingAway, bool onBack)
    {
        if (upperBodyLayer < 0 || (combat != null && combat.IsAttacking) || (guard != null && guard.IsGuarding))
        {
            return;
        }

        int state = puttingAway ? (onBack ? SheatheBackStateHash : SheatheHipStateHash) : (onBack ? DrawBackStateHash : DrawHipStateHash);
        if (HasUpperState(state))
        {
            // Over a swing's tail on Full Body too, which used to hide the reach (review 2026-11-20).
            UnderFullBody(upperBodyLayer, state, 0.1f, 0f, 0.15f);
        }
    }

    // A press undid the reach: the arm leaves the draw or put-away at once.
    private void OnHandMoveCancelled()
    {
        if (upperBodyLayer < 0)
        {
            return;
        }

        int now = animator.GetCurrentAnimatorStateInfo(upperBodyLayer).shortNameHash;
        if (now == DrawHipStateHash || now == DrawBackStateHash || now == SheatheHipStateHash || now == SheatheBackStateHash)
        {
            animator.CrossFadeInFixedTime(EmptyStateHash, 0.1f, upperBodyLayer);
        }
    }

    private void OnParried(DamageInfo damage) =>
        CrossFadeUpper(combat != null && !combat.HasShield && HasUpperState(ParryWeaponStateHash) ? ParryWeaponStateHash : ParryStateHash, 0.04f);

    // A blow taken on the guard shows: the arms give with it and come back to the guard (nothing played before, audit 2026-11-20).
    private void OnBlocked(DamageInfo damage)
    {
        int hit = combat != null && combat.HasShield ? GuardShieldHitStateHash : GuardHitStateHash;
        if (HasUpperState(hit))
        {
            CrossFadeUpper(hit, 0.05f);
        }
    }

    private bool HasUpperState(int hash) => upperBodyLayer >= 0 && animator.HasState(upperBodyLayer, hash);

    // The dodge the gear allows (quickstep, roll, heavy roll) plays with the whole body; anything on the other layers stops.
    private void OnDodgeStarted(DodgeStyle style, DodgeShape shape)
    {
        int hash = string.IsNullOrEmpty(shape.animationState) ? 0 : Animator.StringToHash(shape.animationState);
        if (fullBodyLayer < 0 || hash == 0 || !animator.HasState(fullBodyLayer, hash))
        {
            return;
        }

        SetFloat(ActionSpeedHash, shape.animationSpeed * (dodge != null ? dodge.AnimationPace : 1f));
        IntoFullBody(hash, 0.04f, 0f);
    }

    // Guard broken: the stagger (Idle_Shield_Break, about 1 s), then dazed (Guard Stunned) for as long as the stun lasts, so
    // the body doesn't stand ready while the player still can't act.
    private float guardStunnedUntil = float.NegativeInfinity;

    private void OnGuardBroken(float seconds)
    {
        guardStunnedUntil = Time.time + seconds;
        CrossFadeUpper(GuardBreakStateHash, 0.05f);
    }

    private void UpdateGuardStun()
    {
        if (upperBodyLayer < 0)
        {
            return;
        }

        bool changing = animator.IsInTransition(upperBodyLayer);
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(upperBodyLayer);
        bool stunned = Time.time < guardStunnedUntil;
        // Leaving Guard Break by its own exit (to Empty) while still stunned is caught too: a long frame could jump past 0.85.
        bool breakEnding = state.shortNameHash == GuardBreakStateHash
                           && (changing ? animator.GetNextAnimatorStateInfo(upperBodyLayer).shortNameHash == EmptyStateHash : state.normalizedTime >= 0.85f);
        if (changing && !breakEnding)
        {
            return;
        }

        if (breakEnding && stunned && HasUpperState(GuardStunnedStateHash))
        {
            animator.CrossFadeInFixedTime(GuardStunnedStateHash, 0.2f, upperBodyLayer);
        }
        else if (state.shortNameHash == GuardStunnedStateHash && !stunned)
        {
            animator.CrossFadeInFixedTime(EmptyStateHash, 0.2f, upperBodyLayer);
        }
    }

    // A skill plays its own state (a cast, a dash, a spin) where it has one; else the weapon stance's old attack.
    // A channel let go or run out: the whole body leaves its loop for the ready pose.
    private void OnChannelEnded()
    {
        if (fullBodyLayer >= 0)
        {
            animator.CrossFadeInFixedTime(EmptyStateHash, 0.2f, fullBodyLayer);
        }
    }

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
            if (layer == fullBodyLayer)
            {
                IntoFullBody(state, ActionFade, 0f);
            }
            else
            {
                animator.CrossFadeInFixedTime(state, ActionFade, layer, 0f);
            }

            return;
        }

        PlayAttack();
    }

    public void PlayHit()
    {
        SetTrigger(HitHash);
    }

    // What a hit asks of the animation, gathered from Damageable.Damaged and PlayerCombat.Flinched (which come in either order
    // from the same blow) and played in LateUpdate (ResolveHitReaction).
    private float pendingHitShare = -1f;
    private bool pendingFlinch;
    private bool pendingFlinchLostSwing;
    private bool pendingKnockdown;

    // A hit that hurts; the killing blow is left to whatever handles death.
    private int flinchPlayedFrame = -1;
    private float flinchPlayedShare;

    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (result.Damage <= 0f || result.Killed || result.Blocked || damage.IsOverTime)
        {
            return;
        }

        float share = health != null && health.MaxHealth > 0f ? result.Damage / health.MaxHealth : 0f;
        // The flinch for this blow already played (Flinched came first): a blow heavy enough for the knockback upgrades it.
        if (flinchPlayedFrame == Time.frameCount)
        {
            if (share >= KnockbackShare && flinchPlayedShare < KnockbackShare)
            {
                flinchPlayedShare = share;
                PlayHitReaction(share);
            }

            return;
        }

        pendingHitShare = Mathf.Max(pendingHitShare, share);
    }

    // The gameplay flinch plays at once, in the frame the blow lands: in LateUpdate it came a frame late, into the hit-stop, and
    // crept in for some five frames (review 2026-11-20). Hits that don't flinch wait for LateUpdate (ResolveHitReaction).
    private void OnFlinched(float seconds, bool lostSwing)
    {
        pendingFlinch = true;
        pendingFlinchLostSwing |= lostSwing;
        flinchPlayedShare = Mathf.Max(0f, pendingHitShare);
        ResolveHitReaction();
        flinchPlayedFrame = Time.frameCount;
    }

    // The flinch is the gameplay's (PlayerCombat.Flinched): a hit that doesn't flinch (a scratch, pushing through, the grace
    // after a flinch, a ward) used to play one anyway and cut the swing that went on landing (technical animation audit,
    // 2026-11-20). Now such a hit only shows on an idle or walking body; a swing, dodge or knockdown in progress is left alone.
    private void ResolveHitReaction()
    {
        bool hit = pendingHitShare >= 0f;
        bool flinched = pendingFlinch;
        bool lostSwing = pendingFlinchLostSwing;
        float share = Mathf.Max(0f, pendingHitShare);
        bool knockedDown = pendingKnockdown;
        pendingHitShare = -1f;
        pendingFlinch = false;
        pendingFlinchLostSwing = false;
        pendingKnockdown = false;
        if ((!hit && !flinched) || knockedDown)
        {
            return;
        }

        bool attacking = combat != null && combat.IsAttacking;
        if (combat != null && !flinched)
        {
            bool fullBodyBusy = fullBodyLayer >= 0 && animator.GetCurrentAnimatorStateInfo(fullBodyLayer).shortNameHash != EmptyStateHash;
            if (attacking || fullBodyBusy || (dodge != null && dodge.IsDodging))
            {
                return;
            }
        }
        // Thrown back by a monster's parry (Rebuff: flinched, unhurt, the swing spent) shows too: the stance's flinch over the
        // swing's tail, so being parried reads.
        PlayHitReaction(share);
    }

    private void PlayHitReaction(float share)
    {
        // A planted heavy lost to this blow would otherwise be released to Empty later this LateUpdate (UpdateSwingSpeed), over
        // the flinch (seen in Play, 2026-11-20).
        plantedSwing = 0;
        if (share >= KnockbackShare && upperBodyLayer >= 0 && animator.HasState(upperBodyLayer, KnockbackStateHash))
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
        pendingKnockdown = true;
        int state = Animator.StringToHash("Knockdown");
        if (fullBodyLayer < 0 || !animator.HasState(fullBodyLayer, state))
        {
            return;
        }

        SetFloat(ActionSpeedHash, 1f);
        IntoFullBody(state, 0.06f, 0f);
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
