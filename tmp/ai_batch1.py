import os
os.chdir(r"D:\Safe Haven\Safe Haven\Assets\_Project\Scripts")


def edit(rel, pairs):
    raw = open(rel, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        assert s.count(old) == 1, (rel, old[:80], s.count(old))
        s = s.replace(old, new)
    if crlf:
        s = s.replace("\n", "\r\n")
    open(rel, "w", encoding="utf-8", newline="").write(s)
    print("ok", rel)


edit("AI/MonsterAI.Fighting.cs", [
    ("""        if (HoldsWhileDrawing(toTarget) || DodgesArrow())
        {
            return;
        }

        if (circling)""", """        if (HoldsWhileDrawing(toTarget) || DodgesArrow())
        {
            return;
        }

        if (Time.time < breakingAwayUntil && BreaksAway(toTarget, distance))
        {
            return;
        }

        if (circling)"""),
    ("""        attacks.Begin(action, toTarget);
        if (IsAtThePlayer)
        {
            nextTurnAt = Time.time + CombatTechniqueSettings.Current.AttackTurnSpacing;
        }""", """        attacks.Begin(action, toTarget);
        if (IsAtThePlayer)
        {
            nextTurnAt = Time.time + CombatTechniqueSettings.Current.AttackTurnSpacing;
        }

        // Its turn is taken: it starts waiting again from nothing, and it is the last to have struck this target.
        turnAskedAt = -1f;
        lastStrikeAt = Time.time;
        breakingAwayUntil = -1f;"""),
    ("""            if (doing != null && doing.openAfter > 0f && CurrentState != State.Dead)
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

            return;""", """            if (doing != null && doing.openAfter > 0f && CurrentState != State.Dead)
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

            return;"""),
    ("""        else if (turn == AttackTurn.Strike)
        {
            offScreenSince = -1f;
        }

        switch (turn)""", """        else if (turn == AttackTurn.Strike)
        {
            offScreenSince = -1f;
        }

        // Of those ready, the one ranked highest goes; the rest stand ready (Docs/EnemyAIReview_2026-11-19.md §2, fair turns).
        if (turn == AttackTurn.Strike && !HasTheTurn(rules))
        {
            turn = AttackTurn.StandReady;
        }

        switch (turn)"""),
    ("""    private void Retreat(Vector3 toTarget, float distance)
    {""", """    // Fair turns (§2): since when it has been ready and asking for a turn at its target, when it last asked, when it last struck.
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
        PlaytestLogger.Note(PlaytestLog.EnemyAI, "BreakAway", 0, definition != null ? definition.name : name);
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
    {"""),
])

edit("Playtest/PlaytestLog.cs", [
    ("""    public const string StageReveal = "StageReveal";""", """    public const string StageReveal = "StageReveal";
    /// <summary>A creature's group behaviour (subject what: BreakAway, Rout, Flank, Alarm, SearchPoint, Intensity.Peak, Intensity.Relax;
    /// detail the creature or the stage).</summary>
    public const string EnemyAI = "EnemyAI";"""),
])
