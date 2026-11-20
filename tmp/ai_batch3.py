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


# ---- 5. A search that hunts: ahead along where the player was going, over a few points, shared out in a pack.
edit("AI/Perception/PerceptionSettings.cs", [
    ("""    [SerializeField, Min(1f)] private float maxInvestigateSeconds = 15f;""",
     """    [SerializeField, Min(1f)] private float maxInvestigateSeconds = 15f;
    [Tooltip("A search for a target it lost (not a noise): seconds in all, over its first guess and up to two more points ahead along " +
             "where the target was going (Docs/EnemyAIReview_2026-11-19.md §5), looking Sweep Look Seconds at each but the last.")]
    [SerializeField, Min(1f)] private float lostSearchSeconds = 20f;
    [SerializeField, Min(0.5f)] private float sweepLookSeconds = 2f;"""),
    ("""    public float MaxInvestigateSeconds => maxInvestigateSeconds;""",
     """    public float MaxInvestigateSeconds => maxInvestigateSeconds;
    public float LostSearchSeconds => lostSearchSeconds;
    public float SweepLookSeconds => sweepLookSeconds;"""),
])

edit("AI/AttackTurns.cs", [
    ("""    /// <summary>Where to walk next while circling:""", """    /// <summary>
    /// Where a creature that lost its target looks (Docs/EnemyAIReview_2026-11-19.md §5): first ahead of where it last saw it, along the
    /// way it was going (<paramref name="velocity"/> × 1.5 s, at most 8 m further); then <paramref name="more"/> points 5-9 m further on,
    /// within 50° either side of that heading, turned by <paramref name="share"/> (its place among the pack searching the same ground:
    /// 0, then one side, then the other), so a pack spreads instead of stacking. Not where the target is: where it was going.
    /// </summary>
    public static List<Vector3> SearchPoints(Vector3 lastKnown, Vector3 velocity, int more, int share, System.Random random)
    {
        var points = new List<Vector3>();
        velocity.y = 0f;
        Vector3 ahead = Vector3.ClampMagnitude(velocity * 1.5f, 8f);
        Vector3 first = lastKnown + ahead;
        points.Add(first);
        if (velocity.sqrMagnitude < 0.25f)
        {
            return points;
        }

        float heading = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg + (share == 0 ? 0f : (share % 2 == 1 ? 35f : -35f));
        Vector3 from = first;
        for (int i = 0; i < more; i++)
        {
            float turn = heading + ((float)random.NextDouble() * 100f - 50f);
            float step = 5f + (float)random.NextDouble() * 4f;
            from += new Vector3(Mathf.Sin(turn * Mathf.Deg2Rad), 0f, Mathf.Cos(turn * Mathf.Deg2Rad)) * step;
            points.Add(from);
        }

        return points;
    }

    /// <summary>
    /// How good a spot is for a shooter to move to between shots (§6): higher ground pays (a point a metre, at most 2), standing within
    /// 3 m of another shooter costs 2. Spots with no clear shot, in water or past its leash are left out before scoring.
    /// </summary>
    public static float ScoreSpot(float heightGain, bool nearAnotherShooter) => Mathf.Clamp(heightGain, 0f, 2f) - (nearAnotherShooter ? 2f : 0f);

    /// <summary>Where to walk next while circling:"""),
])

edit("AI/MonsterAI.Awareness.cs", [
    ("""            lastSeenTargetAt = Time.time;
            lastKnownTargetPosition = targetComponent.transform.position;""",
     """            lastSeenTargetAt = Time.time;
            lastKnownTargetPosition = targetComponent.transform.position;
            // The way it was going, as last seen: a search goes on from there, never from where it really is now.
            lastSeenTargetVelocity = attacks != null ? attacks.TargetVelocity : Vector3.zero;"""),
])

edit("AI/MonsterAI.Investigation.cs", [
    ("""        senses.Awareness.LoseTrack(lastKnownTargetPosition, PerceptionSettings.Current.LostTrackAwareness);
        BeginInvestigate(true);
    }""", """        PlanSearch();
        senses.Awareness.LoseTrack(searchPoints.Count > 0 ? searchPoints[0] : lastKnownTargetPosition, PerceptionSettings.Current.LostTrackAwareness);
        BeginInvestigate(true);
        investigateGiveUpAt = Time.time + PerceptionSettings.Current.LostSearchSeconds;
    }

    // The search after losing a target (Docs/EnemyAIReview_2026-11-19.md §5): its points ahead along the target's way, each kept
    // reachable and inside its leash; a third searcher of the same ground and later stays at the first and looks along the way.
    private Vector3 lastSeenTargetVelocity;
    private readonly System.Collections.Generic.List<Vector3> searchPoints = new System.Collections.Generic.List<Vector3>();
    private int searchPoint;
    private static readonly System.Collections.Generic.Dictionary<Vector2Int, (int claims, float at)> searched =
        new System.Collections.Generic.Dictionary<Vector2Int, (int, float)>();

    private void PlanSearch()
    {
        searchPoints.Clear();
        searchPoint = 0;
        Vector3 guess = lastKnownTargetPosition + Vector3.ClampMagnitude(new Vector3(lastSeenTargetVelocity.x, 0f, lastSeenTargetVelocity.z) * 1.5f, 8f);
        var bucket = new Vector2Int(Mathf.FloorToInt(guess.x / 6f), Mathf.FloorToInt(guess.z / 6f));
        int share = searched.TryGetValue(bucket, out var claim) && Time.time - claim.at < 10f ? claim.claims : 0;
        searched[bucket] = (share + 1, Time.time);
        int more = share >= 2 ? 0 : 2;
        var random = new System.Random(GetHashCode() ^ Mathf.RoundToInt(Time.time * 10f));
        foreach (Vector3 point in AttackTurns.SearchPoints(lastKnownTargetPosition, lastSeenTargetVelocity, more, share, random))
        {
            Vector3 spot = InvestigationSpot(point);
            if (movement.TryFindGround(spot, 3f, out Vector3 ground) && targeting.CanReach(ground))
            {
                searchPoints.Add(ground);
            }
        }
    }"""),
    ("""        if (searchUntil < 0f)
        {
            Vector3 spot = InvestigationSpot(senses.Awareness.Stimulus);""", """        if (searchUntil < 0f)
        {
            bool sweeping = searchingLostTarget && searchPoint < searchPoints.Count;
            Vector3 spot = sweeping ? searchPoints[searchPoint] : InvestigationSpot(senses.Awareness.Stimulus);"""),
    ("""            movement.Stop();
            searchUntil = Time.time + rules.SearchSeconds;
            lookDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        transform.Rotate(0f, lookDirection * rules.LookAroundSpeed * Time.deltaTime, 0f);
        if (Time.time >= searchUntil)
        {
            EndInvestigation();
        }""", """            movement.Stop();
            // A short look at each point of a sweep but the last, which gets the whole look.
            bool more = searchingLostTarget && searchPoint + 1 < searchPoints.Count;
            searchUntil = Time.time + (more ? rules.SweepLookSeconds : rules.SearchSeconds);
            lookDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        transform.Rotate(0f, lookDirection * rules.LookAroundSpeed * Time.deltaTime, 0f);
        if (Time.time >= searchUntil)
        {
            if (searchingLostTarget && searchPoint + 1 < searchPoints.Count && Time.time < investigateGiveUpAt)
            {
                searchPoint++;
                searchUntil = -1f;
                movement.RepathNow();
                PlaytestLogger.Note(PlaytestLog.EnemyAI, "SearchPoint", searchPoint, definition != null ? definition.name : name);
                return;
            }

            EndInvestigation();
        }"""),
])

edit("AI/MonsterAI.cs", [
    ("""        nextTurnAt = 0f;""", """        nextTurnAt = 0f;
        searched.Clear();"""),
])

# ---- 6. Ranged creatures move between shots, retreat round an obstacle, and stop trying when cornered.
edit("AI/MonsterAI.Fighting.cs", [
    ("""            if (doing != null && doing.breakAway > 0f && doing.kind == MonsterAttackKind.Melee && CurrentState == State.Chasing)
            {
                StartBreakAway(doing);
            }""", """            if (doing != null && doing.breakAway > 0f && doing.kind == MonsterAttackKind.Melee && CurrentState == State.Chasing)
            {
                StartBreakAway(doing);
            }

            // A shooter moves on after its shot (§6), instead of standing as a turret.
            if (doing != null && MonsterAttacks.IsRanged(doing.kind) && definition.MovementTactic == MonsterMovementTactic.KeepDistance)
            {
                relocatePending = true;
            }"""),
    ("""                else
                {
                    // Holding its distance, eyes on the foe: waiting for its next cast.
                    MarkWaiting();
                    movement.Stop();
                    movement.TurnTowards(toTarget);
                }""", """                else if (relocatePending || relocating)
                {
                    Relocate(toTarget);
                }
                else
                {
                    // Holding its distance, eyes on the foe: waiting for its next cast.
                    MarkWaiting();
                    movement.Stop();
                    movement.TurnTowards(toTarget);
                }"""),
    ("""    private void Retreat(Vector3 toTarget, float distance)
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
    }""", """    // Straight back, else 45° to either side (§6: against a rock it simply stopped). With none of them open for Cornered Seconds it
    // stops trying, and fights where it stands.
    private void Retreat(Vector3 toTarget, float distance)
    {
        Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
        if (Time.time < corneredUntil)
        {
            movement.Stop();
            movement.TurnTowards(toTarget);
            return;
        }

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
    }"""),
])

edit("AI/MonsterPositioning.cs", [
    ("""    /// <summary>Round to where there is a line, and a little closer, instead of shooting the trees.</summary>""",
     """    /// <summary>
    /// A better spot to shoot from (Docs/EnemyAIReview_2026-11-19.md §6): six on the ring of <paramref name="distance"/> round the target,
    /// within 60° of where it stands now; those in water or past its leash left out; scored by height gained and by keeping clear of other
    /// shooters (AttackTurns.ScoreSpot); the best three tried for a clear shot, the first with one taken. False when none would do.
    /// </summary>
    public bool FindShotSpot(Vector3 target, float distance, Vector3 home, float leash, MonsterAI owner, out Vector3 spot)
    {
        spot = self.position;
        Vector3 from = self.position - target;
        from.y = 0f;
        float bearing = from.sqrMagnitude > 0.01f ? Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg : 0f;
        var candidates = new List<(Vector3 point, float score)>(6);
        for (int i = 0; i < 6; i++)
        {
            float turn = (bearing + Mathf.Lerp(-60f, 60f, i / 5f)) * Mathf.Deg2Rad;
            Vector3 point = target + new Vector3(Mathf.Sin(turn), 0f, Mathf.Cos(turn)) * Mathf.Max(2f, distance);
            if (!movement.TryFindGround(point, 2f, out Vector3 ground) || !RaidSpots.IsDryEnough(ground)
                || (leash > 0f && Vector3.Distance(home, ground) > leash) || (ground - self.position).sqrMagnitude < 2f)
            {
                continue;
            }

            bool crowded = false;
            foreach (MonsterAI other in MonsterAI.Active)
            {
                if (other != owner && other.Definition != null && other.Definition.MovementTactic == MonsterMovementTactic.KeepDistance
                    && (other.transform.position - ground).sqrMagnitude < 9f)
                {
                    crowded = true;
                    break;
                }
            }

            candidates.Add((ground, AttackTurns.ScoreSpot(ground.y - self.position.y, crowded)));
        }

        candidates.Sort((a, b) => b.score.CompareTo(a.score));
        for (int i = 0; i < candidates.Count && i < 3; i++)
        {
            if (senses.HasClearShotFrom(candidates[i].point, target))
            {
                spot = candidates[i].point;
                return true;
            }
        }

        return false;
    }

    /// <summary>Round to where there is a line, and a little closer, instead of shooting the trees.</summary>"""),
])

edit("AI/Perception/MonsterSenses.cs", [
    ("""    public bool HasClearShotTo(Vector3 position)
    {""", """    /// <summary>Whether a shot from <paramref name="from"/> (a spot on the ground it might move to) would reach <paramref name="position"/>.</summary>
    public bool HasClearShotFrom(Vector3 from, Vector3 position)
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        Vector3 eye = from + (Eye(rules) - self.position), point = position + Vector3.up * rules.TargetHeight;
        return HasLineOfSight(eye, point, self) && !CreatureInTheWay(eye, point);
    }

    public bool HasClearShotTo(Vector3 position)
    {"""),
])
print("batch 3 done")
