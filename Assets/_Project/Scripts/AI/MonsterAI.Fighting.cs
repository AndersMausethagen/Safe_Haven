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
        if (Time.time - cutOffSince < rules.CutOffSeconds || CanShoot || WaitsAtShore())
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

    // Its target swam out into deep water, where it doesn't go (DeepWater): it waits at the shore a while rather than giving up at
    // once and turning its back (the review of 2026-11-21: a pond was a free escape from any fight).
    private const float ShoreWaitSeconds = 20f;

    private bool WaitsAtShore()
    {
        if (definition == null || definition.Swims || targetComponent == null || Time.time - cutOffSince > PerceptionSettings.Current.CutOffSeconds + ShoreWaitSeconds)
        {
            return false;
        }

        PlayerStateMachine swimmer = targetComponent.GetComponentInParent<PlayerStateMachine>();
        return swimmer != null && swimmer.IsSwimming;
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

        // The alarm goes first, before any blow, when it can be raised (§8).
        if (alarmAction >= 0)
        {
            int call = alarmAction;
            alarmAction = -1;
            if (attacks.IsReady && attacks.CanUse(call, IsHowler))
            {
                movement.Stop();
                SetState(State.Chasing);
                attacks.RaisingAlarm = true;
                BeginAttack(call, toTarget);
                return;
            }

            // The call wasn't ready: its pack comes at once, as it did before.
            if (pack != null && target != null)
            {
                pack.Alert(target);
            }
        }

        if (Time.time < breakingAwayUntil && BreaksAway(toTarget, distance))
        {
            return;
        }

        if (Flanks(toTarget))
        {
            return;
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
                else if (relocatePending || relocating)
                {
                    Relocate(toTarget);
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
        if (IsAtThePlayer && !attacks.RaisingAlarm)
        {
            lastStrikeAt = Time.time;
        }

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
        // The blow under way (a combo's last follow-up, not its first): the break-away's ring goes on from its opening.
        AttackDefinition lastBlow = attacks.Attack;
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
                StartBreakAway(doing, lastBlow);
            }

            // A shooter moves on after its shot (§6), instead of standing as a turret.
            if (doing != null && MonsterAttacks.IsRanged(doing.kind) && definition.MovementTactic == MonsterMovementTactic.KeepDistance)
            {
                relocatePending = true;
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
        // A committed whiff (a heavy that struck nothing), from tier II: the one standing ready takes the opening without waiting for the
        // spacing or its rank; the limit of how many at once still holds.
        bool punish = RaidTierNow >= WhiffPunishFromTier && targetComponent.TryGetComponent(out PlayerCombat foeCombat) && foeCombat.IsSpent;
        AttackTurn turn = AttackTurns.Decide(closingIn, rules.AttackersAtOnce, Time.time, punish ? 0f : nextTurnAt);
        bool flanking = IsFlankerInPlace;
        if (turn == AttackTurn.Strike && OffScreen(rules) && !flanking)
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
        if (turn == AttackTurn.Strike && !punish && !flanking && !HasTheTurn(rules))
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
    // aim and their distance until one goes quiet. Every rank waits, elites too: the rule is about how much comes in at once. With a
    // crowd already on the player, fewer (CombatTechniqueSettings.RangedAtOnceWith).
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

        int allowed = rules.RangedAtOnce;
        if (shooting > 0 && target is Component body)
        {
            allowed = rules.RangedAtOnceWith(CountAt(target, body.transform.position, rules.CrowdRadius));
        }

        if (shooting < allowed)
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

    /// <summary>How many creatures are engaging <paramref name="who"/> within <paramref name="range"/> metres of it: a crowd, for the log and
    /// for the Focus a crowd is worth (PlayerFocus.CrowdScale). Engaging, not merely holding it as a target (searching, going home), and
    /// in co-op the host's creatures after this player count too (IsEngaging: a friend's copies have no target of their own).</summary>
    public static int CountAt(IDamageable who, Vector3 at, float range)
    {
        int count = 0;
        foreach (MonsterAI monster in active)
        {
            if (monster != null && monster.IsEngaging(who) && (monster.transform.position - at).sqrMagnitude <= range * range)
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

    private int rankedFrame = -1;
    private float rankThisFrame;

    private float TurnRank(CombatTechniqueSettings rules)
    {
        if (rankedFrame == Time.frameCount)
        {
            return rankThisFrame;
        }

        rankedFrame = Time.frameCount;
        rankThisFrame = RankNow(rules);
        return rankThisFrame;
    }

    private float RankNow(CombatTechniqueSettings rules)
    {
        // A flanker in place ranks as if in front: it is there to take the next turn.
        bool front = IsFlankerInPlace || targetComponent != null
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
    // Break Away Delay: it stands where it struck until then, open (TryGetOpening shows the ring), and only then runs.
    private float breakAwayRunAt = -1f;
    // The whole opening the ring shows: the strike's own after its blow, and the wait.
    private float breakAwayOpening;

    /// <summary>True while it stands open after a strike before breaking away, with the seconds left and in all (the strike's own
    /// opening counted in, so the ring shrinks on through both).</summary>
    private bool TryGetBreakAwayOpening(out float left, out float length)
    {
        left = breakAwayRunAt - Time.time;
        length = breakAwayOpening;
        return left > 0f && Time.time < breakingAwayUntil;
    }

    /// <summary>The seconds a strike's break-away waits (Break Away Delay), for the ring shown while the strike is still recovering: 0
    /// when it won't break away from what it struck (a wall). Ground and leash are judged only as it starts, so rarely the ring
    /// promises a wait that doesn't come.</summary>
    private float BreakAwayWaitOf(MonsterAttack action) =>
        action != null && action.breakAway > 0f && action.kind == MonsterAttackKind.Melee && BreaksAwayFromTarget ? action.breakAwayDelay : 0f;

    // At a wall or a building it keeps at it: hit and run is for living foes.
    private bool BreaksAwayFromTarget => targetComponent != null && !(target is Damageable struck && struck.Category == TargetCategory.Structure);

    private void StartBreakAway(MonsterAttack doing, AttackDefinition lastBlow)
    {
        if (!BreaksAwayFromTarget)
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
        // The open beat, the run there, then the hold.
        AttackDefinition spent = lastBlow != null ? lastBlow : doing.attack;
        float strikeOpening = spent != null ? Mathf.Max(0f, spent.RecoveryTime - spent.HitDelay) : 0f;
        breakAwayOpening = strikeOpening + doing.breakAwayDelay;
        breakAwayRunAt = Time.time + doing.breakAwayDelay;
        breakingAwayUntil = breakAwayRunAt + UnityEngine.Random.Range(least, most) + 0.6f;
    }

    // True while it is still running off or holding ready; a leap or a charge it now has room for may go at once.
    private bool BreaksAway(Vector3 toTarget, float distance)
    {
        if (targetComponent == null)
        {
            breakingAwayUntil = -1f;
            return false;
        }

        // The open beat: it stands, facing its foe, and takes no new action until it runs.
        if (TryGetBreakAwayOpening(out _, out _))
        {
            SetState(State.Chasing);
            movement.Stop();
            movement.TurnTowards(toTarget);
            return true;
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

    // Straight back, else 45° to either side (§6: against a rock it simply stopped). With none of them open for Cornered Seconds it
    // stops trying, and fights where it stands.
    private const int WhiffPunishFromTier = 2;

    // ---- The flanker (Docs/EnemyAIReview_2026-11-19.md §4): one at most per target, only while the target is passive (the guard held a
    // while, or standing still) and two or more of those that flank are at it. It goes round behind with a growl and then ranks as if in
    // front, so it takes the next turn; the target moving, dodging or turning to face it ends the role.
    private static readonly System.Collections.Generic.Dictionary<IDamageable, (MonsterAI who, float until, Vector3 anchor)> flankers =
        new System.Collections.Generic.Dictionary<IDamageable, (MonsterAI, float, Vector3)>();
    private static readonly System.Collections.Generic.Dictionary<IDamageable, (Vector3 at, float since)> standing =
        new System.Collections.Generic.Dictionary<IDamageable, (Vector3, float)>();
    private const float FlankBehind = 3.5f;
    private const float FlankHolds = 4f;
    private bool flankInPlace;
    // Sitting out the flank for a while: it didn't get round in time, or there was no ground there.
    private float flankRestUntil = -1f;
    private const float FlankRest = 6f;

    private bool IsFlankerInPlace => flankInPlace && target != null && flankers.TryGetValue(target, out var role) && role.who == this;

    private bool CanFlank => definition.FlanksFromTier > 0 && Mathf.Max(1, RaidTierNow) >= definition.FlanksFromTier && OpenWhileStriking;

    /// <summary>Whether a target is passive: its guard up at least <paramref name="guardedFor"/>... or it has stood within half a metre for 2 s.</summary>
    public static bool IsPassive(float guardedFor, float stillFor) => guardedFor >= 1.2f || stillFor >= 2f;

    private bool Flanks(Vector3 toTarget)
    {
        if (!CanFlank || !IsAtThePlayer || target == null)
        {
            flankInPlace = false;
            return false;
        }

        Vector3 at = targetComponent.transform.position;
        float now = Time.time;
        if (!standing.TryGetValue(target, out var still) || (at - still.at).sqrMagnitude > 0.25f)
        {
            standing[target] = still = (at, now);
        }

        if (flankers.TryGetValue(target, out var role) && (role.who == null || now > role.until || role.who.target != target))
        {
            // Out of time before it got round (a stone at the target's back): it sits the next ones out, so another gets the chance
            // and it gets its turns at the front.
            if (role.who != null && !role.who.flankInPlace)
            {
                role.who.flankRestUntil = now + FlankRest;
            }

            flankers.Remove(target);
            role = default;
        }

        Vector3 behind = at - targetComponent.transform.forward * FlankBehind;
        if (role.who == this)
        {
            bool dodging = targetComponent.TryGetComponent(out PlayerDodge dodge) && dodge.IsDodging;
            bool faced = flankInPlace && AttackTurns.InFront(transform.position, at, targetComponent.transform.forward, CombatTechniqueSettings.Current.FrontHalfArc);
            if ((at - role.anchor).sqrMagnitude > 2.25f || dodging || faced)
            {
                flankers.Remove(target);
                flankInPlace = false;
                return false;
            }

            if (flankInPlace)
            {
                return false;
            }

            if (!movement.TryFindGround(behind, 1.5f, out Vector3 ground))
            {
                // No ground behind the target (a wall at its back): the role goes.
                flankers.Remove(target);
                flankRestUntil = now + FlankRest;
                return false;
            }

            // Arrived at the ground found there, not the point in the air behind the target.
            Vector3 left = ground - transform.position;
            left.y = 0f;
            if (left.magnitude > 1f)
            {
                SetState(State.Chasing);
                movement.MoveTo(ground);
                return true;
            }

            flankInPlace = true;
            return false;
        }

        flankInPlace = false;
        if (role.who != null || CurrentState == State.Attacking || now < flankRestUntil)
        {
            return false;
        }

        float guarded = targetComponent.TryGetComponent(out PlayerGuard guard) ? guard.GuardedFor : 0f;
        if (!IsPassive(guarded, now - still.since))
        {
            return false;
        }

        // Two or more that flank at it, and this the one with the shortest way round.
        int flankersAtIt = 0;
        float mine = (behind - transform.position).sqrMagnitude;
        foreach (MonsterAI other in active)
        {
            if (other == null || other.target != target || other.CurrentState == State.Dead || !other.CanFlank || other.IsRouted
                || other.CurrentState == State.Staggered || now < other.flankRestUntil)
            {
                continue;
            }

            flankersAtIt++;
            if (other != this && other.CurrentState != State.Attacking && (behind - other.transform.position).sqrMagnitude < mine)
            {
                return false;
            }
        }

        if (flankersAtIt < 2)
        {
            return false;
        }

        flankers[target] = (this, now + FlankHolds, at);
        AudioManager.Play(definition.FlankSound != null ? definition.FlankSound : definition.AttackSound, transform.position);
        PlaytestLogger.Note(PlaytestLog.EnemyAI, "Flank", 0, definition.name);
        return false;
    }

    // ---- A grudge (Docs/EnemyAIReview_2026-11-19.md §7): one waiting its turn at the player that a companion strikes turns on that
    // companion for a while, one at a time for each companion, instead of circling the player while it is cut.
    private static readonly System.Collections.Generic.Dictionary<Combatant, MonsterAI> grudges = new System.Collections.Generic.Dictionary<Combatant, MonsterAI>();
    private float grudgeUntil = -1f;
    private Combatant grudgeAgainst;
    private const float GrudgeSeconds = 4f;
    private const float GrudgeReach = 6f;

    private bool HoldsGrudge => Time.time < grudgeUntil && grudgeAgainst != null
        && (grudgeAgainst.transform.position - transform.position).sqrMagnitude <= GrudgeReach * GrudgeReach;

    // Called from a blow taken: true when it turned on its striker.
    private bool TakeGrudge(Combatant attacker, IDamageable attackerTarget)
    {
        if (attacker == null || attackerTarget == null || global::Player.IsPlayer(attacker.gameObject) || attacker.GetComponent<FriendHitbox>() != null
            || !(IsWaitingTurn || circling || Time.time < breakingAwayUntil) || !IsAtThePlayer
            || (attacker.transform.position - transform.position).sqrMagnitude > GrudgeReach * GrudgeReach)
        {
            return false;
        }

        if (grudges.TryGetValue(attacker, out MonsterAI holder) && holder != null && holder != this && holder.HoldsGrudge)
        {
            return false;
        }

        grudges[attacker] = this;
        grudgeAgainst = attacker;
        grudgeUntil = Time.time + GrudgeSeconds;
        circling = false;
        breakingAwayUntil = -1f;
        spottingByBlow = true;
        SetTarget(attackerTarget, null, false);
        spottingByBlow = false;
        return true;
    }

    /// <summary>True while it holds a grudge against a companion that struck it (its pack leaves it to that fight).</summary>
    public bool HoldsGrudgeNow => HoldsGrudge;

    private void Retreat(Vector3 toTarget, float distance)
    {
        Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
        if (Time.time < corneredUntil)
        {
            movement.Stop();
            movement.TurnTowards(toTarget);
            return;
        }

        if (Time.time - lastRetreatAt > 0.5f)
        {
            retreatBlockedSince = -1f;
        }

        lastRetreatAt = Time.time;
        foreach (float turn in RetreatTurns)
        {
            Vector3 way = Quaternion.Euler(0f, turn, 0f) * away;
            Vector3 destination = transform.position + way * Mathf.Max(2f, distance);
            if (movement.TryFindGround(destination, 2f, out Vector3 ground) && HorizontalDistance(ground) > 1f
                && (Relentless || IsRouted || Leash <= 0f || Vector3.Distance(home, ground) <= Leash))
            {
                retreatBlockedSince = -1f;
                movement.MoveTo(ground);
                return;
            }
        }

        movement.Stop();
        retreatBlockedSince = retreatBlockedSince < 0f ? Time.time : retreatBlockedSince;
        if (Time.time - retreatBlockedSince > CorneredAfter)
        {
            // Cornered: it gives up backing off for a while; a creature with a close strike uses it when the foe comes in.
            corneredUntil = Time.time + 4f;
            retreatBlockedSince = -1f;
        }
    }

    private static readonly float[] RetreatTurns = { 0f, -45f, 45f };
    private const float CorneredAfter = 1.5f;
    private float retreatBlockedSince = -1f;
    private float lastRetreatAt = -1f;
    private float corneredUntil = -1f;

    // Between shots (§6): one move to a better spot on its ring round the target, chosen once, walked to facing the target.
    private bool relocatePending;
    private bool relocating;
    private Vector3 relocateTo;
    private float relocateGiveUpAt;

    private void Relocate(Vector3 toTarget)
    {
        if (relocatePending)
        {
            relocatePending = false;
            relocating = positioning.FindShotSpot(targetComponent.transform.position, definition.PreferredDistance, home,
                Relentless ? 0f : Leash, this, out relocateTo);
            relocateGiveUpAt = Time.time + 4f;
        }

        Vector3 left = relocateTo - transform.position;
        left.y = 0f;
        if (!relocating || left.magnitude < 0.8f || Time.time > relocateGiveUpAt)
        {
            relocating = false;
            MarkWaiting();
            movement.Stop();
            movement.TurnTowards(toTarget);
            return;
        }

        movement.Strafe(relocateTo, toTarget);
    }
}
