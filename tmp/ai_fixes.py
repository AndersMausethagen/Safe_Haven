import os, re
os.chdir(r"D:\Safe Haven\Safe Haven\Assets\_Project\Scripts")


def edit(rel, pairs):
    raw = open(rel, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        assert s.count(old) == 1, (rel, old[:90], s.count(old))
        s = s.replace(old, new)
    if crlf:
        s = s.replace("\n", "\r\n")
    open(rel, "w", encoding="utf-8", newline="").write(s)
    print("ok", rel)


# HIGH 1: a grudge never alerts the pack, and the pack never pulls a grudge-holder back.
edit("AI/MonsterAI.Targets.cs", [
    ("""    private void SetTarget(IDamageable newTarget, Collider hitCollider)
    {""", """    private void SetTarget(IDamageable newTarget, Collider hitCollider) => SetTarget(newTarget, hitCollider, true);

    // alertPack false: a target of its own (a grudge), not one its pack shares.
    private void SetTarget(IDamageable newTarget, Collider hitCollider, bool alertPack)
    {"""),
    ("""        if (pack != null && !RaisesAlarm)
        {
            pack.Alert(newTarget);
        }""", """        if (alertPack && pack != null && !RaisesAlarm)
        {
            pack.Alert(newTarget);
        }"""),
    ("""    private void ClearTarget()
    {
        target = null;""", """    private void ClearTarget()
    {
        // A call planned for a target it no longer has is never raised later at someone else.
        alarmAction = -1;
        target = null;"""),
])
edit("AI/MonsterPack.cs", [
    ("""            if (member != null && member.isActiveAndEnabled && member.Target != target)
            {
                member.Engage(target);
            }""", """            // One holding a grudge against a companion keeps it (MonsterAI.TakeGrudge).
            if (member != null && member.isActiveAndEnabled && member.Target != target && !member.HoldsGrudgeNow)
            {
                member.Engage(target);
            }"""),
])

edit("AI/MonsterAI.Fighting.cs", [
    # grudge: no pack alert; public read.
    ("""        spottingByBlow = true;
        SetTarget(attackerTarget, null);
        spottingByBlow = false;
        return true;
    }""", """        spottingByBlow = true;
        SetTarget(attackerTarget, null, false);
        spottingByBlow = false;
        return true;
    }

    /// <summary>True while it holds a grudge against a companion that struck it (its pack leaves it to that fight).</summary>
    public bool HoldsGrudgeNow => HoldsGrudge;"""),
    # LOW: the new early steps come after the defensive and cowardly checks.
    ("""        // The alarm goes first, before any blow, when it can be raised (§8).
        if (alarmAction >= 0)
        {
            int call = alarmAction;
            alarmAction = -1;
            if (attacks.IsReady && call < definition.Attacks.Count)
            {
                movement.Stop();
                SetState(State.Chasing);
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
""", ""),
    ("""        if (definition.Attacks.Count > 0)
        {
            int chosen = attacks.ChooseAction(distance, IsHowler, ReadFoe(toTarget));
            if (chosen >= 0 && attacks.IsReady)
            {
                if (HoldsBack(""", """        // The alarm goes first, before any blow, when it can be raised (§8).
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
                if (HoldsBack("""),
    # LOW: "struck last" only for strikes at the player, never the alarm.
    ("""        turnAskedAt = -1f;
        lastStrikeAt = Time.time;
        breakingAwayUntil = -1f;""", """        turnAskedAt = -1f;
        if (IsAtThePlayer && !attacks.RaisingAlarm)
        {
            lastStrikeAt = Time.time;
        }

        breakingAwayUntil = -1f;"""),
    # LOW: break-away not at a structure.
    ("""    private void StartBreakAway(MonsterAttack doing)
    {
        if (targetComponent == null)
        {
            return;
        }""", """    private void StartBreakAway(MonsterAttack doing)
    {
        // At a wall or a building it keeps at it: hit and run is for living foes.
        if (targetComponent == null || (target is Damageable struck && struck.Category == TargetCategory.Structure))
        {
            return;
        }"""),
    # LOW: a flanker in place skips the wait and its rank; it must really reach the spot.
    ("""            Vector3 left = behind - transform.position;
            left.y = 0f;
            if (left.magnitude > 1f && movement.TryFindGround(behind, 1.5f, out Vector3 ground))
            {
                SetState(State.Chasing);
                flankInPlace = false;
                movement.MoveTo(ground);
                return true;
            }

            flankInPlace = true;
            return false;""", """            Vector3 left = behind - transform.position;
            left.y = 0f;
            if (left.magnitude > 1f)
            {
                if (!movement.TryFindGround(behind, 1.5f, out Vector3 ground))
                {
                    // No ground behind the target (a wall at its back): the role goes.
                    flankers.Remove(target);
                    flankInPlace = false;
                    return false;
                }

                SetState(State.Chasing);
                flankInPlace = false;
                movement.MoveTo(ground);
                return true;
            }

            flankInPlace = true;
            return false;"""),
    # the pick of the flanker ignores those that can't act.
    ("""            if (other == null || other.target != target || other.CurrentState == State.Dead || !other.CanFlank)
            {
                continue;
            }""", """            if (other == null || other.target != target || other.CurrentState == State.Dead || !other.CanFlank || other.IsRouted
                || other.CurrentState == State.Staggered)
            {
                continue;
            }"""),
    # HoldsBack: a flanker in place goes without waiting for its rank or the off-screen beat (its growl was the warning).
    ("""        AttackTurn turn = AttackTurns.Decide(closingIn, rules.AttackersAtOnce, Time.time, punish ? 0f : nextTurnAt);
        if (turn == AttackTurn.Strike && OffScreen(rules))""", """        AttackTurn turn = AttackTurns.Decide(closingIn, rules.AttackersAtOnce, Time.time, punish ? 0f : nextTurnAt);
        bool flanking = IsFlankerInPlace;
        if (turn == AttackTurn.Strike && OffScreen(rules) && !flanking)"""),
    ("""        if (turn == AttackTurn.Strike && !punish && !HasTheTurn(rules))""", """        if (turn == AttackTurn.Strike && !punish && !flanking && !HasTheTurn(rules))"""),
    # LOW: the cornered timer is only for a retreat kept up.
    ("""        foreach (float turn in RetreatTurns)
        {""", """        if (Time.time - lastRetreatAt > 0.5f)
        {
            retreatBlockedSince = -1f;
        }

        lastRetreatAt = Time.time;
        foreach (float turn in RetreatTurns)
        {"""),
    ("""    private float retreatBlockedSince = -1f;""", """    private float retreatBlockedSince = -1f;
    private float lastRetreatAt = -1f;"""),
    # perf: a rank once a frame.
    ("""    private float TurnRank(CombatTechniqueSettings rules)
    {""", """    private int rankedFrame = -1;
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
    {"""),
])

# HIGH 2: the alarm keeps its action's rules (tier, cooldown, phase, health); MEDIUM 3: only from its own senses.
edit("AI/MonsterAttacks.cs", [
    ("""    public MonsterAttack Action => action;""", """    public MonsterAttack Action => action;

    /// <summary>True while the call being raised is an alarm (Docs/EnemyAIReview_2026-11-19.md §8): only then does it carry Alarm Radius.</summary>
    public bool RaisingAlarm { get; set; }

    /// <summary>Whether an action may be used now by its own rules (its tier, phase, health and cooldown), wherever the foe stands: the
    /// alarm call on spotting, which isn't a blow and so has no reach to keep to.</summary>
    public bool CanUse(int index, bool isLeader)
    {
        if (index < 0 || index >= definition.Attacks.Count || definition.Attacks[index] == null)
        {
            return false;
        }

        MonsterAttack candidate = definition.Attacks[index];
        float inReach = Mathf.Max(candidate.minimumRange, Mathf.Min(candidate.maximumRange, candidate.minimumRange + 0.1f));
        return candidate.IsAvailable(inReach, Time.time, readyAt[index], isLeader, RaidTier, Phase, HealthShare);
    }"""),
    ("""        float alertReach = action.onSpotted ? Mathf.Max(action.buffRadius, action.alarmRadius) : action.buffRadius;
        if (action.alertsOthers && foe != null)
        {
            if (action.onSpotted)
            {
                PlaytestLogger.Note(PlaytestLog.EnemyAI, "Alarm", 0, self.name);
            }
""", """        bool alarm = action.onSpotted && RaisingAlarm;
        RaisingAlarm = false;
        float alertReach = alarm ? Mathf.Max(action.buffRadius, action.alarmRadius) : action.buffRadius;
        if (action.alertsOthers && foe != null)
        {
            if (alarm)
            {
                PlaytestLogger.Note(PlaytestLog.EnemyAI, "Alarm", 0, self.name);
            }
"""),
])
# A strike begun any other way is no alarm.
edit("AI/MonsterAttacks.cs", [
    ("""    public void Begin(int index, Vector3 toTarget)
    {""", """    public void Begin(int index, Vector3 toTarget)
    {
        // Only the call raised on spotting is an alarm; MonsterAI sets RaisingAlarm just before it, and anything else clears it.
        if (index < 0 || definition.Attacks[index] == null || !definition.Attacks[index].onSpotted)
        {
            RaisingAlarm = false;
        }"""),
])

edit("AI/MonsterAI.Awareness.cs", [
    ("""        if (senses.Look(elapsed))
        {
            SetTarget(senses.Spotted, senses.SpottedCollider);
            return;
        }""", """        if (senses.Look(elapsed))
        {
            // Spotted with its own eyes: only this raises an alarm, never being sent by another's call (alarms set off no chain).
            spottedWithOwnEyes = true;
            SetTarget(senses.Spotted, senses.SpottedCollider);
            spottedWithOwnEyes = false;
            return;
        }"""),
    ("""    private void PlanAlarm()
    {
        alarmAction = -1;
        if (IsRemote || definition == null || (pack != null && !pack.IsLeader(this) && PackHasCaller()))""", """    private bool spottedWithOwnEyes;

    private void PlanAlarm()
    {
        alarmAction = -1;
        if (!spottedWithOwnEyes || IsRemote || definition == null || (pack != null && !pack.IsLeader(this) && PackHasCaller()))"""),
    ("""            if (action != null && action.onSpotted && action.kind == MonsterAttackKind.Howl && (!action.packLeaderOnly || IsHowler))
            {""", """            if (action != null && action.onSpotted && action.kind == MonsterAttackKind.Howl && attacks.CanUse(i, IsHowler))
            {"""),
])

# MEDIUM 4: a noise or a glimpse during a sweep ends it, so the search goes where the sound was.
edit("AI/MonsterAI.Investigation.cs", [
    ("""        bool fresh = CurrentState != State.Investigating;
        searchingLostTarget = lostTarget || (!fresh && searchingLostTarget);""", """        bool fresh = CurrentState != State.Investigating;
        searchingLostTarget = lostTarget || (!fresh && searchingLostTarget);
        if (!lostTarget)
        {
            // Something new to go and see: the sweep along the lost target's way gives way to it.
            searchPoint = searchPoints.Count;
        }"""),
    ("""        if (senses.IsSeeingSomething)
        {
            // Another glimpse: go there instead of searching here.
            searchUntil = -1f;
        }""", """        if (senses.IsSeeingSomething)
        {
            // Another glimpse: go there instead of searching here, and the sweep with it.
            searchUntil = -1f;
            searchPoint = searchPoints.Count;
        }"""),
    # perf: only the first point is asked for a path; the rest fail by themselves if they must.
    ("""        foreach (Vector3 point in AttackTurns.SearchPoints(lastKnownTargetPosition, lastSeenTargetVelocity, more, share, random))
        {
            Vector3 spot = InvestigationSpot(point);
            if (movement.TryFindGround(spot, 3f, out Vector3 ground) && targeting.CanReach(ground))
            {
                searchPoints.Add(ground);
            }
        }""", """        foreach (Vector3 point in AttackTurns.SearchPoints(lastKnownTargetPosition, lastSeenTargetVelocity, more, share, random))
        {
            Vector3 spot = InvestigationSpot(point);
            if (movement.TryFindGround(spot, 3f, out Vector3 ground) && (searchPoints.Count > 0 || targeting.CanReach(ground)))
            {
                searchPoints.Add(ground);
            }
        }"""),
])

# MEDIUM 5: the pacing can't stall: the score is capped, only hunters near and able to reach the player count, and the fade ends.
edit("Raids/RaidIntensity.cs", [
    ("""    private const float RelaxTravel = 40f;""", """    private const float RelaxTravel = 40f;
    // Never far over the peak (a long fight doesn't buy a long silence), and a fade that ends whatever happens.
    private const float MostScore = PeakAt * 1.2f;
    private const float LongestFade = 45f;
    private float fadeUntil;"""),
    ("""        Score += 100f * Mathf.Max(0f, healthShare);
        quietFor = 0f;""", """        Score = Mathf.Min(MostScore, Score + 100f * Mathf.Max(0f, healthShare));
        quietFor = 0f;"""),
    ("""        Score += 8f;
        quietFor = 0f;""", """        Score = Mathf.Min(MostScore, Score + 8f);
        quietFor = 0f;"""),
    ("""            Score += Mathf.Min(10f, 2f * hunting) * seconds;
            quietFor = 0f;""", """            Score = Mathf.Min(MostScore, Score + Mathf.Min(10f, 2f * hunting) * seconds);
            quietFor = 0f;"""),
    ("""                if (now >= phaseUntil)
                {
                    Now = Phase.Fade;
                }

                break;
            case Phase.Fade:
                if (Score < FadeBelow)""", """                if (now >= phaseUntil)
                {
                    Now = Phase.Fade;
                    fadeUntil = now + LongestFade;
                }

                break;
            case Phase.Fade:
                if (Score < FadeBelow || now >= fadeUntil)"""),
])
edit("Raids/RoamingSpawner.cs", [
    ("""        RaidIntensity.Phase now = intensity.Tick(seconds, MonsterAI.CountHuntingPlayerSide(), player, Time.time, random);""",
     """        RaidIntensity.Phase now = intensity.Tick(seconds, MonsterAI.CountHuntingPlayerSide(player, PacingReach), player, Time.time, random);"""),
    ("""    private readonly RaidIntensity intensity = new RaidIntensity();""", """    private readonly RaidIntensity intensity = new RaidIntensity();
    // Only a fight near the player counts toward the pacing: a hunter far off, or one stuck where it can't reach, is no fight.
    private const float PacingReach = 30f;"""),
    ("""        if (director != null && director.Player != null && victim.gameObject == director.Player && !result.Blocked)""",
     """        // This game's player, or a friend's body in the host's game: every player's fight counts.
        if (director != null && director.Player != null && (victim.gameObject == director.Player || victim.GetComponent<FriendHitbox>() != null)
            && !result.Blocked)"""),
])
edit("AI/MonsterAI.cs", [
    ("""    /// <summary>How many creatures are chasing or striking the player's side now (the raid's pacing, RaidIntensity).</summary>
    public static int CountHuntingPlayerSide()
    {
        int count = 0;
        foreach (MonsterAI monster in active)
        {
            if (monster != null && monster.CurrentState != State.Dead && monster.IsHuntingPlayerSide)""", """    /// <summary>How many creatures within <paramref name="range"/> of <paramref name="point"/> are chasing or striking the player's side and
    /// can get at them (the raid's pacing, RaidIntensity).</summary>
    public static int CountHuntingPlayerSide(Vector3 point, float range)
    {
        int count = 0;
        foreach (MonsterAI monster in active)
        {
            if (monster != null && monster.CurrentState != State.Dead && monster.IsHuntingPlayerSide && monster.movement != null && !monster.movement.IsCutOff
                && (monster.transform.position - point).sqrMagnitude <= range * range)"""),
])
print("fixes applied")
