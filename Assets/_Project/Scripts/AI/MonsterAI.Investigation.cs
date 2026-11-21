using UnityEngine;

/// <summary>
/// What a monster does with what it hears and half sees: going to look, searching, and giving up. Part of the state machine, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    /// <summary>
    /// A sound of <paramref name="radius"/> metres at <paramref name="position"/> (WorldNoise). An idle, roaming or
    /// investigating monster that hears it grows suspicious and comes to look; one that hunts, fights or walks home
    /// ignores it. Cover muffles a sound, and the further off it is the less exactly the monster can place it. A sound
    /// alone never makes it spot anyone.
    /// </summary>
    public void Hear(Vector3 position, float radius)
    {
        // Game (deer, foxes) doesn't come to look: a loud enough sound near it sends it running from it.
        if (definition != null && definition.Timid && CurrentState != State.Dead && !IsRemote)
        {
            float heard = PerceptionRules.HeardLevel(HorizontalDistance(position), radius, definition.Hearing, PerceptionSettings.Current);
            if (heard >= PerceptionSettings.Current.GameStartleLevel)
            {
                Bolt(position);
            }

            return;
        }

        if (!CanInvestigate())
        {
            return;
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        float distance = HorizontalDistance(position);
        // Out of reach even in the open, so the line check is only paid for sounds that could be heard.
        if (PerceptionRules.HeardLevel(distance, radius, definition.Hearing, rules) <= 0f)
        {
            return;
        }

        bool muffled = !senses.HasClearPathTo(position);
        float level = PerceptionRules.HeardLevel(distance, radius, definition.Hearing, rules, muffled);
        if (level <= 0f)
        {
            return;
        }

        Vector2 miss = UnityEngine.Random.insideUnitCircle * PerceptionRules.HeardError(distance, radius, definition.Hearing, rules, muffled);
        senses.Awareness.Hear(level, position + new Vector3(miss.x, 0f, miss.y));
        // Something loud right beside it: it turns to face it now, all but aware, and its eyes do the rest on the next look.
        if (!muffled && level >= rules.TurnToSoundLevel && rules.TurnToSoundLevel < 1f)
        {
            senses.Awareness.Hear(1f, position);
            FaceNow(position);
        }
        if (senses.Stage != AwarenessStage.Unaware)
        {
            BeginInvestigate(false);
        }
        else if (CurrentState == State.Idle)
        {
            // Too faint for a "?": it only turns its head that way a while (Dishonored's harmless sounds).
            Vector3 toward = position - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.01f)
            {
                glanceHeading = toward.normalized;
                nextGlanceAt = Time.time + FaintSoundLookSeconds;
            }
        }
    }

    // Turns it quickly toward a position (a loud sound beside it), and keeps its idle gaze there a while. Fast, not at once (the
    // review of 2026-11-21): a half turn takes about a third of a second, a moment for the player to see it coming.
    private const float QuickTurnSpeed = 540f;
    private const float QuickTurnSeconds = 0.5f;
    private Vector3 quickTurnHeading;
    private float quickTurnUntil = -1f;

    private void FaceNow(Vector3 position)
    {
        Vector3 toward = position - transform.position;
        toward.y = 0f;
        if (toward.sqrMagnitude < 0.01f)
        {
            return;
        }

        quickTurnHeading = toward.normalized;
        quickTurnUntil = Time.time + QuickTurnSeconds;
        glanceHeading = toward.normalized;
        nextGlanceAt = Time.time + FaintSoundLookSeconds;
    }

    // Each frame of a quick turn: toward the sound until faced, or its moment is over, or something else takes over (a fight).
    private void UpdateQuickTurn()
    {
        if (Time.time >= quickTurnUntil || CanTarget(target))
        {
            return;
        }

        Quaternion facing = Quaternion.LookRotation(quickTurnHeading);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, QuickTurnSpeed * Time.deltaTime);
        if (Quaternion.Angle(transform.rotation, facing) < 2f)
        {
            quickTurnUntil = -1f;
        }
    }

    // How long a sound too faint to make it suspicious holds its gaze.
    private const float FaintSoundLookSeconds = 2.5f;

    /// <summary>Sends it to look at a position as if it had heard something loud there (an angry forest).</summary>
    public void Investigate(Vector3 position)
    {
        if (!CanInvestigate())
        {
            return;
        }

        senses.Awareness.Hear(PerceptionSettings.Current.HeardAwarenessNear, position);
        BeginInvestigate(false);
    }

    private bool CanInvestigate()
    {
        if (definition == null || senses == null || !isActiveAndEnabled || !IsWatchful())
        {
            return false;
        }

        return CurrentState == State.Idle || CurrentState == State.Investigating
            || (CurrentState == State.Returning && roaming);
    }

    // Out of sight for too long or out of chase range: it goes to where it last saw the target and searches there.
    /// <summary>Raised when a monster that was hunting the player loses track of them (Sneaking experience).</summary>
    public static event System.Action<MonsterAI> LostTrackOfPlayer;

    private void LoseTrack()
    {
        bool wasPlayer = targetComponent != null && global::Player.IsPlayer(targetComponent.gameObject);
        ClearTarget();
        if (wasPlayer)
        {
            LostTrackOfPlayer?.Invoke(this);
        }

        PlanSearch();
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
            if (movement.TryFindGround(spot, 3f, out Vector3 ground) && (searchPoints.Count > 0 || targeting.CanReach(ground)))
            {
                searchPoints.Add(ground);
            }
        }
    }

    private void BeginInvestigate(bool lostTarget)
    {
        bool fresh = CurrentState != State.Investigating;
        searchingLostTarget = lostTarget || (!fresh && searchingLostTarget);
        if (!lostTarget)
        {
            // Something new to go and see: the sweep along the lost target's way gives way to it.
            searchPoint = searchPoints.Count;
        }
        searchUntil = -1f;
        investigateGiveUpAt = Time.time + PerceptionSettings.Current.MaxInvestigateSeconds;
        movement.RepathNow();
        if (fresh && !lostTarget)
        {
            AudioManager.Play(PerceptionSettings.Current.SuspiciousSound, transform.position);
        }

        EnsureIndicator();
        SetState(State.Investigating);
        RefreshAmbush();
    }

    private void UpdateInvestigation()
    {
        if (CanTarget(target))
        {
            // Spotted it, was hit or was told by its pack: the hunt starts where it stands.
            if (roaming)
            {
                roaming = false;
                if (!HoldsHome)
                {
                    home = transform.position;
                }
            }
            searchingLostTarget = false;
            SetState(State.Chasing);
            return;
        }

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            Sense();
            if (CurrentState != State.Investigating || CanTarget(target))
            {
                return;
            }
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        if (senses.IsSeeingSomething)
        {
            // Another glimpse: go there instead of searching here, and the sweep with it.
            searchUntil = -1f;
            searchPoint = searchPoints.Count;
        }

        if (searchUntil < 0f)
        {
            bool sweeping = searchingLostTarget && searchPoint < searchPoints.Count;
            Vector3 spot = sweeping ? searchPoints[searchPoint] : InvestigationSpot(senses.Awareness.Stimulus);
            if (HorizontalDistance(spot) > ArriveDistance && Time.time < investigateGiveUpAt && movement.MoveTo(spot))
            {
                return;
            }

            movement.Stop();
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
        }
    }

    // Where to look, kept inside its leash (and defensive radius), so checking a noise never drags it off for good.
    private Vector3 InvestigationSpot(Vector3 stimulus)
    {
        float limit = Leash;
        if (definition.HasTemperament(MonsterTemperament.Defensive))
        {
            limit = limit > 0f ? Mathf.Min(limit, definition.DefensiveRadius) : definition.DefensiveRadius;
        }

        if (IsAvoided(stimulus))
        {
            return transform.position;
        }

        Vector3 offset = stimulus - home;
        offset.y = 0f;
        if (roaming || Relentless || limit <= 0f || offset.magnitude <= limit)
        {
            return stimulus;
        }

        return home + offset.normalized * (limit * 0.9f);
    }

    private void EndInvestigation()
    {
        searchingLostTarget = false;
        BeginReturn();
    }
}
