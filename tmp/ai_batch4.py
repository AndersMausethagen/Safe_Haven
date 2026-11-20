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


# ---- Data fields.
edit("Content/MonsterDefinition.cs", [
    ("""    [SerializeField, Range(0f, 1f)] private float flankSpread;""",
     """    [SerializeField, Range(0f, 1f)] private float flankSpread;
    [Tooltip("From this raid tier on (1 = always, 0 = never), one of its kind may flank a player who turtles behind the guard or stands " +
             "still while others are at them: it goes round behind with a growl and takes the next turn (Docs/EnemyAIReview_2026-11-19.md §4). " +
             "Moving, dodging or turning to face it ends it.")]
    [SerializeField, Min(0)] private int flanksFromTier;
    [Tooltip("Heard as it goes round to flank. Empty: its attack sound.")]
    [SerializeField] private SoundDefinition flankSound;"""),
    ("""    public float FlankSpread => flankSpread;""",
     """    public float FlankSpread => flankSpread;
    public int FlanksFromTier => flanksFromTier;
    public SoundDefinition FlankSound => flankSound;"""),
])

# ---- The player's committed whiff, readable by the creatures.
edit("Player/PlayerCombat.cs", [
    ("""    private SwingInfo current;""", """    private SwingInfo current;
    // How many the swing under way struck: -1 before its blow lands.
    private int landedThisSwing = -1;

    /// <summary>In the recovery of a heavy, charged or art swing that struck nothing (Docs/EnemyAIReview_2026-11-19.md, a committed
    /// whiff): from tier II, a creature standing ready takes the opening at once.</summary>
    public bool IsSpent => IsAttacking && !hitPending && landedThisSwing == 0
        && (current.Technique & (HitTechnique.Heavy | HitTechnique.Charged | HitTechnique.WeaponArt)) != 0;"""),
])
edit("Player/PlayerCombat.Swings.cs", [
    ("""        SwingStarted?.Invoke(current);""", """        landedThisSwing = -1;
        SwingStarted?.Invoke(current);"""),
    ("""            int hits = LandSwing();
            SwingLanded?.Invoke(current, hits);""", """            int hits = LandSwing();
            landedThisSwing = hits;
            SwingLanded?.Invoke(current, hits);"""),
])

# ---- MonsterAI: the flanker, the whiff punish, the grudge.
edit("AI/MonsterAI.Fighting.cs", [
    ("""        if (Time.time < breakingAwayUntil && BreaksAway(toTarget, distance))
        {
            return;
        }
""", """        if (Time.time < breakingAwayUntil && BreaksAway(toTarget, distance))
        {
            return;
        }

        if (Flanks(toTarget))
        {
            return;
        }
"""),
    ("""        AttackTurn turn = AttackTurns.Decide(closingIn, rules.AttackersAtOnce, Time.time, nextTurnAt);""",
     """        // A committed whiff (a heavy that struck nothing), from tier II: the one standing ready takes the opening without waiting for the
        // spacing or its rank; the limit of how many at once still holds.
        bool punish = RaidTierNow >= WhiffPunishFromTier && targetComponent.TryGetComponent(out PlayerCombat foeCombat) && foeCombat.IsSpent;
        AttackTurn turn = AttackTurns.Decide(closingIn, rules.AttackersAtOnce, Time.time, punish ? 0f : nextTurnAt);"""),
    ("""        if (turn == AttackTurn.Strike && !HasTheTurn(rules))""", """        if (turn == AttackTurn.Strike && !punish && !HasTheTurn(rules))"""),
    ("""    private float TurnRank(CombatTechniqueSettings rules)
    {
        bool front = targetComponent != null""", """    private float TurnRank(CombatTechniqueSettings rules)
    {
        // A flanker in place ranks as if in front: it is there to take the next turn.
        bool front = IsFlankerInPlace || targetComponent != null"""),
    ("""    private void Retreat(Vector3 toTarget, float distance)
    {
        Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
        if (Time.time < corneredUntil)""", """    private const int WhiffPunishFromTier = 2;

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

            Vector3 left = behind - transform.position;
            left.y = 0f;
            if (left.magnitude > 1f && movement.TryFindGround(behind, 1.5f, out Vector3 ground))
            {
                SetState(State.Chasing);
                flankInPlace = false;
                movement.MoveTo(ground);
                return true;
            }

            flankInPlace = true;
            return false;
        }

        flankInPlace = false;
        if (role.who != null || CurrentState == State.Attacking)
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
            if (other == null || other.target != target || other.CurrentState == State.Dead || !other.CanFlank)
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
        SetTarget(attackerTarget, null);
        spottingByBlow = false;
        return true;
    }

    private void Retreat(Vector3 toTarget, float distance)
    {
        Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
        if (Time.time < corneredUntil)"""),
])

edit("AI/MonsterAI.Reactions.cs", [
    ("""        // A wall-breaker at a wall turns on whoever hurts it (HomeAssault); it goes back to the walls when they stop.
        bool atStructure = target is Damageable structure && structure != null && structure.Category == TargetCategory.Structure;
        if (!CanTarget(target) || (atStructure && definition.BreaksWalls))""",
     """        // A wall-breaker at a wall turns on whoever hurts it (HomeAssault); it goes back to the walls when they stop.
        bool atStructure = target is Damageable structure && structure != null && structure.Category == TargetCategory.Structure;
        if (CanTarget(target) && !atStructure && TakeGrudge(attacker, attackerTarget))
        {
            provokedUntil = Time.time + PerceptionSettings.Current.ProvokedSeconds;
            return;
        }

        if (!CanTarget(target) || (atStructure && definition.BreaksWalls))"""),
])

edit("AI/MonsterAI.Targets.cs", [
    ("""        if (targeting.TryFindBetter(target, DistanceToTarget(), senses, home, Relentless, out IDamageable better, out Collider betterCollider))""",
     """        // Holding a grudge against a companion that struck it: the player doesn't pull it back until the grudge is done.
        if (HoldsGrudge)
        {
            return;
        }

        if (targeting.TryFindBetter(target, DistanceToTarget(), senses, home, Relentless, out IDamageable better, out Collider betterCollider))"""),
])

edit("AI/MonsterAI.cs", [
    ("""        searched.Clear();""", """        searched.Clear();
        flankers.Clear();
        standing.Clear();
        grudges.Clear();"""),
])

# ---- 9. The pacing director for roamers.
edit("Raids/RoamingSpawner.cs", [
    ("""        nextThink = Time.time + ThinkInterval;
        Vector3 player = director.Player.transform.position;
        Tidy(settings, player);""", """        float thought = nextThink < 0f ? ThinkInterval : Mathf.Max(0f, Time.time - (nextThink - ThinkInterval));
        nextThink = Time.time + ThinkInterval;
        Vector3 player = director.Player.transform.position;
        Pace(thought, player);
        Tidy(settings, player);"""),
    ("""        int cap = settings.AliveCap(director.StageIndex, director.Creatures.AlarmsRaised, director.Session.Definition.SessionEnemyDensity) + ExtraCap;
        if (Time.time >= nextSpawn && AliveCount < cap && spent < settings.TotalLimit && !AtGlobalLimit())""",
     """        int cap = settings.AliveCap(director.StageIndex, director.Creatures.AlarmsRaised, director.Session.Definition.SessionEnemyDensity) + ExtraCap;
        if (intensity.AllowsRoamers && Time.time >= nextSpawn && AliveCount < cap && spent < settings.TotalLimit && !AtGlobalLimit())"""),
    ("""            if (!busy && idle && Flat(position - player) > settings.HuntSpread && Time.time >= nextHunt[root])""",
     """            if (!busy && idle && intensity.AllowsRoamers && Flat(position - player) > settings.HuntSpread && Time.time >= nextHunt[root])"""),
    ("""    private void Update()
    {
        RoamingSpawnSettings settings = Settings;""", """    // The raid's pacing (Docs/EnemyAIReview_2026-11-19.md §9; Warframe, Left 4 Dead): after a hard fight the roamers hold off for a breather.
    private readonly RaidIntensity intensity = new RaidIntensity();

    /// <summary>The pacing's phase now, for tests and the log.</summary>
    public RaidIntensity.Phase PacingPhase => intensity.Now;

    private void OnEnable()
    {
        Damageable.AnyDamaged += OnAnyDamaged;
        Damageable.AnyDied += OnAnyDied;
    }

    private void OnDisable()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        Damageable.AnyDied -= OnAnyDied;
    }

    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (director != null && director.Player != null && victim == director.Player.Health && !result.Blocked)
        {
            intensity.Hurt(result.Damage / Mathf.Max(1f, victim.MaxHealth));
        }
    }

    private void OnAnyDied(Damageable victim, DamageInfo blow)
    {
        if (victim != null && victim.TryGetComponent(out MonsterAI _) && blow.Source != null && blow.Source.Faction.IsPlayerSide())
        {
            intensity.Kill();
        }
    }

    private void Pace(float seconds, Vector3 player)
    {
        RaidIntensity.Phase was = intensity.Now;
        RaidIntensity.Phase now = intensity.Tick(seconds, MonsterAI.CountHuntingPlayerSide(), player, Time.time, random);
        if (now == was)
        {
            return;
        }

        if (now == RaidIntensity.Phase.Peak)
        {
            PlaytestLogger.Note(PlaytestLog.EnemyAI, "Intensity.Peak", Mathf.RoundToInt(intensity.Score), director.Session.Definition.Id);
        }
        else if (now == RaidIntensity.Phase.Relax)
        {
            // Said, so the breather is the woods' and not a hidden hand.
            GameMessages.Post("The woods go quiet.", MessageKind.Info);
            PlaytestLogger.Note(PlaytestLog.EnemyAI, "Intensity.Relax", Mathf.RoundToInt(intensity.Score), director.Session.Definition.Id);
        }
    }

    private void Update()
    {
        RoamingSpawnSettings settings = Settings;"""),
])

edit("AI/MonsterAI.cs", [
    ("""    public static bool AnyHuntingPlayerSideWithin(Vector3 point, float range) => AnyHuntingPlayerSideWithin(point, range, null);""",
     """    public static bool AnyHuntingPlayerSideWithin(Vector3 point, float range) => AnyHuntingPlayerSideWithin(point, range, null);

    /// <summary>How many creatures are chasing or striking the player's side now (the raid's pacing, RaidIntensity).</summary>
    public static int CountHuntingPlayerSide()
    {
        int count = 0;
        foreach (MonsterAI monster in active)
        {
            if (monster != null && monster.CurrentState != State.Dead && monster.IsHuntingPlayerSide)
            {
                count++;
            }
        }

        return count;
    }"""),
])
print("batch 4 done")
