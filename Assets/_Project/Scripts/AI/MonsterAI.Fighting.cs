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

    /// <summary>A broken pack: it runs from its target for this long, and doesn't strike. Elites and those sent on a mission stand.
    /// It cries out as it turns (after <paramref name="cryAfter"/> seconds, so a pack's cries come one after another) and its mark
    /// shows it is beaten (Docs/EnemyAIReview_2026-11-19.md §3: a rout made no sound and changed nothing to see).</summary>
    public void Rout(float seconds, float cryAfter = 0f)
    {
        if (seconds <= 0f || IsElite || Relentless || CurrentState == State.Dead)
        {
            return;
        }

        routedUntil = Time.time + seconds;
        circling = false;
        breakingAwayUntil = -1f;
        if (isActiveAndEnabled)
        {
            StartCoroutine(CryAfter(cryAfter));
        }
        if (CurrentState == State.Attacking)
        {
            attacks.Cancel();
            pose?.CancelAttack();
            SetState(State.Chasing);
        }
    }

    private bool IsRouted => Time.time < routedUntil;

    /// <summary>True while it runs from a broken pack (its mark shows it).</summary>
    public bool IsRoutedNow => IsRouted;

    private System.Collections.IEnumerator CryAfter(float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        if (CurrentState != State.Dead)
        {
            CryOut();
        }
    }

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

        if (HoldsWhileDrawing(toTarget) || DodgesArrow())
        {
            return;
        }

        if (Time.time < breakingAwayUntil && BreaksAway(toTarget, distance))
        {
            return;
        }

        if (circling)
        {
            if (Time.time < circlingUntil)
            {
                MarkWaiting();
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
                NoteFleeing();
                SetState(State.Chasing);
                Retreat(toTarget, definition.FleeDistance - distance);
                return;
            }
        }

        if (definition.Attacks.Count > 0)
        {
            int chosen = attacks.ChooseAction(distance, IsHowler, ReadFoe(toTarget));
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
                    // Holding its distance, eyes on the foe: waiting for its next cast.
                    MarkWaiting();
                    movement.Stop();
                    movement.TurnTowards(toTarget);
                }
            }
            else
            {
                if (attacks.AnyStrikeReaches(distance, IsHowler))
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
        attacks.Target = CanTarget(target) ? target : null;
        attacks.Begin(action, toTarget);
        if (IsAtThePlayer)
        {
            nextTurnAt = Time.time + CombatTechniqueSettings.Current.AttackTurnSpacing;
        }

        // Its turn is taken: it starts waiting again from nothing, and it is the last to have struck this target.
        turnAskedAt = -1f;
        lastStrikeAt = Time.time;
        breakingAwayUntil = -1f;

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
        MonsterAttack doing = attacks.Action;
        if (!attacks.Update(CanTarget(target) ? targetComponent.transform.position : (Vector3?)null))
        {
            // What the blow met may already have moved it on (a parry staggers it as it lands): that state stands.
            if (CurrentState != State.Attacking)
            {
                return;
            }

            SetState(State.Chasing);
            // Spent by it (Open After): it reels a while, open to blows, as a staggered creature is; or, with Topple After, it goes down
            // for that long (the Heartroot's Hollow's Hold ends in its kill window).
            if (doing != null && doing.openAfter > 0f && CurrentState != State.Dead)
            {
                if (doing.toppleAfter)
                {
                    Topple(-transform.forward, doing.openAfter, false, true);
                }
                else
                {
                    StaggerAtLeast(doing.openAfter);
                }
            }

            if (doing != null && doing.breakAway > 0f && doing.kind == MonsterAttackKind.Melee && CurrentState == State.Chasing)
            {
                StartBreakAway(doing);
            }

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
        if (MonsterAttacks.IsRanged(kind))
        {
            // A shot from out of frame waits the same beat a blow does, so it comes from where it can be read (research G1).
            CombatTechniqueSettings shotRules = CombatTechniqueSettings.Current;
            if (IsAtThePlayer && OffScreen(shotRules))
            {
                shotOffScreenSince = shotOffScreenSince >= 0f ? shotOffScreenSince : Time.time;
                if (Time.time < shotOffScreenSince + shotRules.OffScreenWait)
                {
                    MarkWaiting();
                    movement.TurnTowards(toTarget);
                    return true;
                }
            }
            else
            {
                shotOffScreenSince = -1f;
            }

            return IsAtThePlayer && WaitsToShoot(toTarget);
        }

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

        // Off screen it waits its moment: nobody else closing in, and a beat since it got there, so blows come from where
        // they can be read (anti-overwhelm 3, 2026-11-14).
        AttackTurn turn = AttackTurns.Decide(closingIn, rules.AttackersAtOnce, Time.time, nextTurnAt);
        if (turn == AttackTurn.Strike && OffScreen(rules))
        {
            offScreenSince = offScreenSince >= 0f ? offScreenSince : Time.time;
            if (closingIn > 0 || Time.time < offScreenSince + rules.OffScreenWait)
            {
                turn = AttackTurn.Circle;
            }
        }
        else if (turn == AttackTurn.Strike)
        {
            offScreenSince = -1f;
        }

        // Of those ready, the one ranked highest goes; the rest stand ready (Docs/EnemyAIReview_2026-11-19.md §2, fair turns).
        if (turn == AttackTurn.Strike && !HasTheTurn(rules))
        {
            turn = AttackTurn.StandReady;
        }

        switch (turn)
        {
            case AttackTurn.Circle:
                MarkWaiting();
                circling = true;
                circlingUntil = Time.time + UnityEngine.Random.Range(rules.CircleSeconds.x, Mathf.Max(rules.CircleSeconds.x, rules.CircleSeconds.y));
                circleSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                ApplySpeed();
                Circle(toTarget, distance);
                return true;
            case AttackTurn.StandReady:
                MarkWaiting();
                movement.Stop();
                SetState(State.Chasing);
                movement.TurnTowards(toTarget);
                return true;
            default:
                return false;
        }
    }

    // At most so many shoot or throw at the player at once (each holds its turn a while after it starts); the rest keep their
    // aim and their distance until one goes quiet. Every rank waits, elites too: the rule is about how much comes in at once.
    private bool WaitsToShoot(Vector3 toTarget)
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        int shooting = 0;
        foreach (MonsterAI other in active)
        {
            if (other != this && other.target == target && Time.time - other.attacks.RangedStartedAt < rules.RangedTurnSeconds)
            {
                shooting++;
            }
        }

        if (shooting < rules.RangedAtOnce)
        {
            return false;
        }

        MarkWaiting();
        movement.Stop();
        SetState(State.Chasing);
        movement.TurnTowards(toTarget);
        return true;
    }

    private float offScreenSince = -1f;

    // Out of the local player's view: its body (at chest height) outside the screen's edges, by the camera's real viewport
    // (Docs/GameDesignResearch_2026-11-17.md G1: an angle from the camera counted creatures ahead of or behind the player as on
    // screen from a top-down view). Only for this game's own player. Off Screen Angle stays as the test for a camera without a view.
    private bool OffScreen(CombatTechniqueSettings rules)
    {
        Camera view = Camera.main;
        if (view == null || targetComponent == null || !global::Player.IsPlayer(targetComponent.gameObject))
        {
            return false;
        }

        Vector3 seen = view.WorldToViewportPoint(transform.position + Vector3.up * 1.2f);
        if (seen.z > 0f)
        {
            return seen.x < -OffScreenMargin || seen.x > 1f + OffScreenMargin || seen.y < -OffScreenMargin || seen.y > 1f + OffScreenMargin;
        }

        Vector3 look = view.transform.forward, to = transform.position - view.transform.position;
        look.y = 0f;
        to.y = 0f;
        return look.sqrMagnitude > 0.001f && to.sqrMagnitude > 0.001f && Vector3.Angle(look, to) > rules.OffScreenAngle;
    }

    /// <summary>True when this game's player is its target and it stands out of their view (the camera's frame).</summary>
    public bool IsOffScreenForLocalPlayer => OffScreen(CombatTechniqueSettings.Current);

    /// <summary>How many creatures are after <paramref name="who"/> within <paramref name="range"/> metres of it: a crowd, for the log.</summary>
    public static int CountAt(IDamageable who, Vector3 at, float range)
    {
        int count = 0;
        foreach (MonsterAI monster in active)
        {
            if (monster != null && monster.target == who && monster.CurrentState != State.Dead && (monster.transform.position - at).sqrMagnitude <= range * range)
            {
                count++;
            }
        }

        return count;
    }

    // How far past the screen's edge (a share of the screen) a body still counts as seen: its outline shows a little beyond its centre.
    private const float OffScreenMargin = 0.02f;
    private float shotOffScreenSince = -1f;

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

    // Fair turns (§2): since when it has been ready and asking for a turn at its target, when it last asked, when it last struck.
    private float readySince;
    private float turnAskedAt = -1f;
    private float lastStrikeAt = -1f;
    private const float StillAsking = 0.3f;

    // Asks for the turn: true when no other creature asking for one at the same target ranks above it (the closer wins a tie).
    private bool HasTheTurn(CombatTechniqueSettings rules)
    {
        float now = Time.time;
        if (now - turnAskedAt > 0.5f)
        {
            readySince = now;
        }

        turnAskedAt = now;
        float mine = TurnRank(rules);
        float near = (targetComponent.transform.position - transform.position).sqrMagnitude;
        foreach (MonsterAI other in active)
        {
            if (other == this || other.target != target || now - other.turnAskedAt > StillAsking || other.CurrentState == State.Attacking)
            {
                continue;
            }

            float theirs = other.TurnRank(rules);
            if (theirs > mine + 0.001f
                || (Mathf.Abs(theirs - mine) <= 0.001f && (targetComponent.transform.position - other.transform.position).sqrMagnitude < near))
            {
                return false;
            }
        }

        return true;
    }

    private float TurnRank(CombatTechniqueSettings rules)
    {
        bool front = targetComponent != null
            && AttackTurns.InFront(transform.position, targetComponent.transform.position, targetComponent.transform.forward, rules.FrontHalfArc);
        return AttackTurns.Rank(Time.time - readySince, front, !OffScreen(rules), StruckLast(), rules.FrontBonus, rules.OnScreenBonus,
            rules.LastStrikerPenalty);
    }

    // The last of those at this target to have struck it.
    private bool StruckLast()
    {
        if (lastStrikeAt < 0f)
        {
            return false;
        }

        foreach (MonsterAI other in active)
        {
            if (other != this && other.target == target && other.lastStrikeAt > lastStrikeAt)
            {
                return false;
            }
        }

        return true;
    }

    // Bite and break away (Docs/EnemyAIReview_2026-11-19.md §1): after a strike with Break Away it runs off to a spot round from where it
    // struck, away from the others, and holds ready there for its seconds before another close strike; the others get their turns.
    private float breakingAwayUntil = -1f;
    private Vector3 breakAwaySpot;
    private bool breakAwayArrived;

    private void StartBreakAway(MonsterAttack doing)
    {
        if (targetComponent == null)
        {
            return;
        }

        Vector3 at = targetComponent.transform.position;
        Vector3? nearest = null;
        float best = 36f;
        foreach (MonsterAI other in active)
        {
            if (other != this && other.target == target && other.CurrentState != State.Dead)
            {
                float d = (other.transform.position - at).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = other.transform.position;
                }
            }
        }

        Vector3 spot = AttackTurns.BreakAwaySpot(transform.position, at, nearest, doing.breakAway, UnityEngine.Random.value < 0.5f ? -1f : 1f,
            UnityEngine.Random.Range(50f, 70f));
        if (!movement.TryFindGround(spot, 2f, out Vector3 ground) || (!Relentless && Leash > 0f && Vector3.Distance(home, ground) > Leash))
        {
            return;
        }

        breakAwaySpot = ground;
        breakAwayArrived = false;
        float least = Mathf.Min(doing.breakAwaySeconds.x, doing.breakAwaySeconds.y), most = Mathf.Max(doing.breakAwaySeconds.x, doing.breakAwaySeconds.y);
        // The run there, then the hold.
        breakingAwayUntil = Time.time + UnityEngine.Random.Range(least, most) + 0.6f;
    }

    // True while it is still running off or holding ready; a leap or a charge it now has room for may go at once.
    private bool BreaksAway(Vector3 toTarget, float distance)
    {
        if (targetComponent == null)
        {
            breakingAwayUntil = -1f;
            return false;
        }

        if (definition.Attacks.Count > 0)
        {
            int chosen = attacks.ChooseAction(distance, IsHowler, ReadFoe(toTarget));
            if (chosen >= 0 && attacks.IsReady && definition.Attacks[chosen].kind != MonsterAttackKind.Melee)
            {
                breakingAwayUntil = -1f;
                return false;
            }
        }

        SetState(State.Chasing);
        Vector3 left = breakAwaySpot - transform.position;
        left.y = 0f;
        if (!breakAwayArrived && left.magnitude > 0.8f)
        {
            movement.MoveTo(breakAwaySpot);
            return true;
        }

        breakAwayArrived = true;
        MarkWaiting();
        movement.Stop();
        movement.TurnTowards(toTarget);
        return true;
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
