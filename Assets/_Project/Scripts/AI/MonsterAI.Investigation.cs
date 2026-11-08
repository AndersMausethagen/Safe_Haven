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
        if (senses.Stage != AwarenessStage.Unaware)
        {
            BeginInvestigate(false);
        }
    }

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

        senses.Awareness.LoseTrack(lastKnownTargetPosition, PerceptionSettings.Current.LostTrackAwareness);
        BeginInvestigate(true);
    }

    private void BeginInvestigate(bool lostTarget)
    {
        bool fresh = CurrentState != State.Investigating;
        searchingLostTarget = lostTarget || (!fresh && searchingLostTarget);
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
            // Another glimpse: go there instead of searching here.
            searchUntil = -1f;
        }

        if (searchUntil < 0f)
        {
            Vector3 spot = InvestigationSpot(senses.Awareness.Stimulus);
            if (HorizontalDistance(spot) > ArriveDistance && Time.time < investigateGiveUpAt && movement.MoveTo(spot))
            {
                return;
            }

            movement.Stop();
            searchUntil = Time.time + rules.SearchSeconds;
            lookDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        transform.Rotate(0f, lookDirection * rules.LookAroundSpeed * Time.deltaTime, 0f);
        if (Time.time >= searchUntil)
        {
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
