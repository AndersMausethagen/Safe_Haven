using UnityEngine;

/// <summary>
/// A monster in a fight: closing in and finding a line, holding back, circling and backing off between blows, choosing
/// and starting its attacks (MonsterAttacks), shooting only on a clear line, walking off when cut off, and fleeing when
/// routed. Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    private void TrackTargetMovement()
    {
        if (targetComponent == null || attacks == null || Time.deltaTime <= 0f)
        {
            return;
        }

        attacks.TargetVelocity = positioning.TrackTarget(targetComponent.transform.position, Time.deltaTime);
    }

    // It can do nothing here and knows it: it gives up for a while, unless it can shoot from where it stands.
    private bool WalksOffCutOff()
    {
        if (Relentless || !movement.IsCutOff)
        {
            cutOffSince = -1f;
            return false;
        }

        if (cutOffSince < 0f)
        {
            cutOffSince = Time.time;
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        if (Time.time - cutOffSince < rules.CutOffSeconds || CanShoot)
        {
            return false;
        }

        // The spot it was walking to may be what's out of reach (a flank in a wall), not the target.
        cutOffSince = -1f;
        if (targeting.CanReach(targetComponent.transform.position))
        {
            return false;
        }

        ignoresTargetsUntil = Time.time + rules.CutOffForgetSeconds;
        ClearTarget();
        BeginReturn();
        return true;
    }

    private bool CanShoot
    {
        get
        {
            foreach (MonsterAttack action in definition.Attacks)
            {
                if (action != null && action.kind == MonsterAttackKind.Projectile)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>A broken pack: it runs from its target for this long, and doesn't strike. Elites and those sent on a mission stand.</summary>
    public void Rout(float seconds)
    {
        if (seconds <= 0f || IsElite || Relentless || CurrentState == State.Dead)
        {
            return;
        }

        routedUntil = Time.time + seconds;
        circling = false;
        if (CurrentState == State.Attacking)
        {
            attacks.Cancel();
            pose?.CancelAttack();
            SetState(State.Chasing);
        }
    }

    private bool IsRouted => Time.time < routedUntil;

    private bool ShotIsClear() => positioning.ShotIsClear(targetComponent.transform.position);

    private void FindALine(Vector3 toTarget, float distance)
    {
        SetState(State.Chasing);
        positioning.WalkToALine(targetComponent.transform.position, toTarget, distance);
    }

    private Vector3 ChaseSpot(float distance) => positioning.ChaseSpot(this, target, targetComponent.transform.position, distance, IsAtThePlayer);

    /// <summary>True while it is chasing or striking this target, for those choosing a side of it.</summary>
    public bool IsClosingOn(IDamageable who) => target == who && (CurrentState == State.Chasing || CurrentState == State.Attacking);

    private void Engage()
    {
        Vector3 toTarget = TargetPoint() - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (IsRouted)
        {
            SetState(State.Chasing);
            Retreat(toTarget, definition.FleeDistance);
            return;
        }

        if (circling)
        {
            if (Time.time < circlingUntil)
            {
                Circle(toTarget, distance);
                return;
            }

            circling = false;
            ApplySpeed();
        }

        // Defending its ground ends where its ground does, but not while it answers a blow.
        if (!Relentless && !IsProvoked && definition.HasTemperament(MonsterTemperament.Defensive)
            && (HorizontalDistance(home) > definition.DefensiveRadius
                || Vector3.Distance(home, targetComponent.transform.position) > definition.DefensiveRadius))
        {
            ClearTarget();
            BeginReturn();
            return;
        }
        if (definition.HasTemperament(MonsterTemperament.Cowardly))
        {
            fleeing = damageable.HealthFraction <= definition.FleeHealthFraction
                && distance < definition.FleeDistance + (fleeing ? definition.DistanceTolerance : -definition.DistanceTolerance);
            if (fleeing)
            {
                SetState(State.Chasing);
                Retreat(toTarget, definition.FleeDistance - distance);
                return;
            }
        }

        if (definition.Attacks.Count > 0)
        {
            int chosen = attacks.ChooseAction(distance, IsHowler);
            if (chosen >= 0 && attacks.IsReady)
            {
                if (HoldsBack(definition.Attacks[chosen].kind, toTarget, distance))
                {
                    return;
                }

                if (definition.Attacks[chosen].kind == MonsterAttackKind.Projectile && !ShotIsClear())
                {
                    FindALine(toTarget, distance);
                    return;
                }

                movement.Stop();
                SetState(State.Chasing);
                if (movement.TurnTowards(toTarget) && (pack == null || pack.TryAttack()))
                {
                    BeginAttack(chosen, toTarget);
                }
                return;
            }
            SetState(State.Chasing);
            if (definition.MovementTactic == MonsterMovementTactic.KeepDistance)
            {
                if (distance < definition.PreferredDistance - definition.DistanceTolerance)
                {
                    Retreat(toTarget, definition.PreferredDistance - distance);
                }
                else if (distance > definition.PreferredDistance + definition.DistanceTolerance)
                {
                    movement.MoveTo(targetComponent.transform.position);
                }
                else
                {
                    movement.Stop();
                    movement.TurnTowards(toTarget);
                }
            }
            else
            {
                if (attacks.AnyStrikeReaches(distance))
                {
                    movement.Stop();
                    movement.TurnTowards(toTarget);
                }
                else
                {
                    movement.MoveTo(ChaseSpot(distance));
                }
            }
            return;
        }
        AttackDefinition attack = definition.Attack;

        if (attack != null && toTarget.magnitude <= attack.Range * AttackReachFactor)
        {
            if (attacks.IsReady && HoldsBack(MonsterAttackKind.Melee, toTarget, distance))
            {
                return;
            }

            movement.Stop();
            SetState(State.Chasing);
            bool facing = movement.TurnTowards(toTarget);
            if (facing && attacks.IsReady && (pack == null || pack.TryAttack()))
            {
                BeginAttack(-1, toTarget);
            }

            return;
        }

        SetState(State.Chasing);
        movement.MoveTo(ChaseSpot(distance));
    }

    // Action index, or -1 for the definition's plain attack.
    private void BeginAttack(int action, Vector3 toTarget)
    {
        LastAction = action;
        circling = false;
        attacks.Begin(action, toTarget);
        if (IsAtThePlayer)
        {
            nextTurnAt = Time.time + CombatTechniqueSettings.Current.AttackTurnSpacing;
        }

        SetState(State.Attacking);
        AnnounceStrike();
    }

    // A strike's warning, to the eye and the ear, and the body going into it.
    private void AnnounceStrike()
    {
        pose?.Attack(CurrentAttack, Time.time);
        MonsterAttack action = attacks.Action;
        AudioManager.Play(action != null && action.sound != null ? action.sound : Definition.AttackSound, transform.position);
        AttackStarted?.Invoke();
    }

    private void UpdateAttack()
    {
        int blow = attacks.Blow;
        if (!attacks.Update(CanTarget(target) ? targetComponent.transform.position : (Vector3?)null))
        {
            SetState(State.Chasing);
            return;
        }

        // The next blow of a combo: a body and a warning of its own, as a strike has.
        if (attacks.Blow != blow)
        {
            AnnounceStrike();
        }
    }

    // The player, or a friend's body in the host's game (co-op): both get attack turns and the player's chase spacing.
    private bool IsAtThePlayer => targetComponent != null && (targetComponent is FriendHitbox || global::Player.IsPlayer(targetComponent.gameObject));

    // Only so many close in on the player at once, so a crowd can be read: one crowded out circles until its next try, and one that only
    // has to let another's blow start first stands ready. Ranged and calling actions don't wait, nor do great beasts and elites, though
    // they are counted. True when it held back.
    private bool HoldsBack(MonsterAttackKind kind, Vector3 toTarget, float distance)
    {
        if ((kind != MonsterAttackKind.Melee && kind != MonsterAttackKind.Charge) || !OpenWhileStriking || !IsAtThePlayer)
        {
            return false;
        }

        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        int closingIn = 0;
        foreach (MonsterAI other in active)
        {
            if (other != this && other.target == target && other.CurrentState == State.Attacking && other.attacks.IsClosingIn)
            {
                closingIn++;
            }
        }

        switch (AttackTurns.Decide(closingIn, rules.AttackersAtOnce, Time.time, nextTurnAt))
        {
            case AttackTurn.Circle:
                circling = true;
                circlingUntil = Time.time + UnityEngine.Random.Range(rules.CircleSeconds.x, Mathf.Max(rules.CircleSeconds.x, rules.CircleSeconds.y));
                circleSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                ApplySpeed();
                Circle(toTarget, distance);
                return true;
            case AttackTurn.StandReady:
                movement.Stop();
                SetState(State.Chasing);
                movement.TurnTowards(toTarget);
                return true;
            default:
                return false;
        }
    }

    // Sideways around the target, eyes on it, drifting to the distance it waits at.
    private void Circle(Vector3 toTarget, float distance)
    {
        SetState(State.Chasing);
        Vector3 spot = AttackTurns.CircleSpot(transform.position, toTarget, circleSide, CombatTechniqueSettings.Current.CircleDistance);
        if (movement.TryFindGround(spot, 1.5f, out Vector3 ground))
        {
            movement.Strafe(ground, toTarget);
        }
        else
        {
            movement.Stop();
            movement.TurnTowards(toTarget);
        }
    }

    private void Retreat(Vector3 toTarget, float distance)
    {
        Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
        Vector3 destination = transform.position + away * Mathf.Max(2f, distance);
        if (movement.TryFindGround(destination, 2f, out Vector3 ground)
            && (Relentless || IsRouted || Leash <= 0f || Vector3.Distance(home, ground) <= Leash))
        {
            movement.MoveTo(ground);
        }
        else
        {
            movement.Stop();
        }
    }
}
