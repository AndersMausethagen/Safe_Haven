using UnityEngine;

/// <summary>
/// What a monster notices and how it shows it: its senses on each scan, keeping track of what it hunts, the awareness
/// mark over its head and the cry when it spots someone, the extra damage it takes while unaware (an ambush), and its
/// idle life and glances while nothing is going on. Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    private bool UpdateIdleLife()
    {
        if (CurrentState != State.Idle || idleLife == null || !idleLife.IsOn || KeepAwake || Relentless || roaming)
        {
            return false;
        }

        idleDriver.Tick(home);
        if (idleLife.Now == MonsterIdleLife.Doing.Standing)
        {
            UpdateIdleGaze();
        }

        return true;
    }

    // Monsters that start fights look and listen for targets; retaliation-only ones only react to being hit.
    private bool IsWatchful()
    {
        return definition.HasTemperament(MonsterTemperament.Aggressive)
            || definition.HasTemperament(MonsterTemperament.Defensive);
    }

    // Looks for targets: while hunting one, keeps track of it; otherwise fills awareness and, once suspicious, goes to look.
    private void Sense()
    {
        float elapsed = Mathf.Min(Time.time - lastSenseTime, 1f);
        lastSenseTime = Time.time;
        if (Time.time < ignoresTargetsUntil && !CanTarget(target))
        {
            return;
        }

        if (CanTarget(target))
        {
            TrackTarget();
            return;
        }

        if (senses.Look(elapsed))
        {
            SetTarget(senses.Spotted, senses.SpottedCollider);
            return;
        }

        if (senses.Stage == AwarenessStage.Suspicious && CurrentState != State.Investigating && CanInvestigate())
        {
            BeginInvestigate(false);
        }

        RefreshAmbush();
    }

    private void TrackTarget()
    {
        // Whoever just hit it can't hide by standing far off: it knows where the blow came from.
        if (IsProvoked || senses.CanSee(targetComponent.transform, definition.ChaseRange))
        {
            lastSeenTargetAt = Time.time;
            lastKnownTargetPosition = targetComponent.transform.position;
            senses.Awareness.Alert(lastKnownTargetPosition);
            ScanForNearerTarget();
            return;
        }

        if (!Relentless && Time.time - lastSeenTargetAt > PerceptionSettings.Current.LoseTrackSeconds)
        {
            LoseTrack();
        }
    }

    // Idle monsters glance around the way they first faced, so a sneaking player has to watch them.
    private void UpdateIdleGaze()
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        if (Time.time >= nextGlanceAt)
        {
            Vector2 seconds = rules.IdleGlanceSeconds;
            nextGlanceAt = Time.time + UnityEngine.Random.Range(seconds.x, Mathf.Max(seconds.x, seconds.y));
            glanceHeading = Quaternion.Euler(0f, UnityEngine.Random.Range(-rules.IdleGlanceAngle, rules.IdleGlanceAngle), 0f) * homeForward;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(glanceHeading),
            rules.LookAroundSpeed * Time.deltaTime);
    }

    /// <summary>Not hunting anyone and hasn't noticed anything: a blow now is an ambush.</summary>
    public bool IsUnaware => senses != null && CurrentState != State.Dead && CurrentState != State.Staggered
        && !CanTarget(target) && senses.Stage == AwarenessStage.Unaware;

    /// <summary>Whether the last blow it took found it unaware (read after the blow, when it already knows).</summary>
    public bool LastHitAmbushed { get; private set; }

    // Unaware monsters take extra damage, a hit from the shadows (PerceptionSettings › Ambush).
    private void RefreshAmbush()
    {
        if (senses == null || CurrentState == State.Staggered || CurrentState == State.Dead)
        {
            return;
        }

        bool unaware = !CanTarget(target) && senses.Stage == AwarenessStage.Unaware;
        damageable.DamageTakenMultiplier = unaware ? PerceptionSettings.Current.AmbushDamageTaken : 1f;
    }

    private AwarenessIndicator EnsureIndicator()
    {
        if (indicator == null && PerceptionSettings.Current.ShowIndicators)
        {
            indicator = AwarenessIndicator.CreateFor(this);
        }

        return indicator;
    }

    // It has just spotted (or been set on) a target: the "!", a sound when it's the player, and a call that sends idle
    // monsters nearby to look.
    private void OnSpotted()
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        AwarenessIndicator spotted = EnsureIndicator();
        if (spotted != null)
        {
            spotted.ShowSpotted();
        }
        if (global::Player.IsPlayer(targetComponent.gameObject))
        {
            CreatureRecords.NoteMet(definition);
        }

        if (global::Player.IsPlayer(targetComponent.gameObject) && Time.time >= nextSpottedSoundAt)
        {
            nextSpottedSoundAt = Time.time + SpottedSoundSpacing;
            AudioManager.Play(rules.SpottedSound, transform.position);
        }

        WorldNoise.Emit(transform.position, rules.SpottedCallRadius);
    }
}
