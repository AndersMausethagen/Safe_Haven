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
            // Spotted with its own eyes: only this raises an alarm, never being sent by another's call (alarms set off no chain).
            spottedWithOwnEyes = true;
            SetTarget(senses.Spotted, senses.SpottedCollider);
            spottedWithOwnEyes = false;
            return;
        }

        // Noticing the player, before any "?": its mark is made now, to show the fill.
        if (senses.Noticing is Component noticed && noticed != null && global::Player.IsPlayer(noticed.gameObject))
        {
            EnsureIndicator();
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
            // The way it was going, as last seen: a search goes on from there, never from where it really is now.
            lastSeenTargetVelocity = attacks != null ? attacks.TargetVelocity : Vector3.zero;
            senses.Awareness.Alert(lastKnownTargetPosition);
            ScanForNearerTarget();
            // A raid's hunter has found its prey: from now on it can be shaken off.
            huntFound |= Relentless && ShakenOffAfterFound;
            return;
        }

        // A raid's hunter that found the player and then lost them long enough is an ordinary hunter again: hidden from, it gives up.
        if (Relentless && ShakenOffAfterFound && huntFound && Time.time - lastSeenTargetAt > PerceptionSettings.Current.HuntShakenOffSeconds)
        {
            Relentless = false;
            huntFound = false;
            LoseTrack();
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
    public bool IsUnaware => IsRemote ? IsRemoteUnaware
        : senses != null && CurrentState != State.Dead && CurrentState != State.Staggered
            && !CanTarget(target) && Stage == AwarenessStage.Unaware;

    // How aware it counts as for an ambush. Searching (its "?" up) is at least suspicious, though awareness drains while it sees
    // nothing: unaware a second after a sound, it took the full ambush from the front with its question mark showing.
    private AwarenessStage Stage => CurrentState == State.Investigating && senses.Stage == AwarenessStage.Unaware
        ? AwarenessStage.Suspicious : senses.Stage;

    /// <summary>
    /// Caught unaware by a blow from <paramref name="from"/>: unaware, or (Perception Settings › Ambush From Behind When Suspicious) only
    /// suspicious, hunting nobody, and struck from outside its sight cone (Sekiro's rule: its back shows from above). A friend's copy
    /// goes by its state alone; the host's game judges the blow itself.
    /// </summary>
    public bool IsUnawareOf(Vector3 from)
    {
        if (IsUnaware)
        {
            return true;
        }

        if (IsRemote || senses == null || definition == null || !PerceptionSettings.Current.AmbushFromBehindWhenSuspicious
            || CurrentState == State.Dead || CurrentState == State.Staggered || CanTarget(target) || Stage != AwarenessStage.Suspicious)
        {
            return false;
        }

        Vector3 toward = from - transform.position;
        toward.y = 0f;
        return toward.sqrMagnitude > 1e-4f && Vector3.Angle(transform.forward, toward) > SenseProfile.Of(definition).HalfAngle;
    }

    /// <summary>Sets what the next blow does to it for being caught unaware, judged from where it comes from (Guard asks before every
    /// blow; the host's game before a friend's).</summary>
    public void JudgeAmbushFrom(Vector3 from)
    {
        if (IsRemote || senses == null || CurrentState == State.Staggered || CurrentState == State.Dead)
        {
            return;
        }

        damageable.DamageTakenMultiplier = IsUnawareOf(from) ? PerceptionSettings.Current.AmbushDamageTaken : 1f;
    }

    // Where a blow was struck from: its striker, or back along its way.
    private static Vector3 BlowOrigin(in DamageInfo damage) =>
        damage.Source != null ? damage.Source.transform.position : damage.HitPoint - damage.Direction * 2f;

    /// <summary>Whether the last blow it took found it unaware (read after the blow, when it already knows).</summary>
    public bool LastHitAmbushed { get; private set; }

    // Unaware monsters take extra damage, a hit from the shadows (PerceptionSettings › Ambush).
    private void RefreshAmbush()
    {
        if (senses == null || CurrentState == State.Staggered || CurrentState == State.Dead)
        {
            return;
        }

        bool unaware = IsRemote ? IsRemoteUnaware : !CanTarget(target) && Stage == AwarenessStage.Unaware;
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

    // The alarm (Docs/EnemyAIReview_2026-11-19.md §8): which of its calls it raises on spotting the player, if one is ready and its group
    // hasn't called in the last 20 s. Its group then waits for the call instead of coming at once.
    private int alarmAction = -1;
    private static readonly System.Collections.Generic.Dictionary<Vector2Int, float> alarmsRaised = new System.Collections.Generic.Dictionary<Vector2Int, float>();
    private const float AlarmGroupSeconds = 20f;

    private Vector2Int AlarmGroup => pack != null
        ? new Vector2Int(pack.GetHashCode(), 0)
        : new Vector2Int(Mathf.FloorToInt(transform.position.x / 20f), Mathf.FloorToInt(transform.position.z / 20f) + 100000);

    private bool spottedWithOwnEyes;

    private void PlanAlarm()
    {
        alarmAction = -1;
        if (!spottedWithOwnEyes || IsRemote || definition == null || (pack != null && !pack.IsLeader(this) && PackHasCaller()))
        {
            return;
        }

        Vector2Int group = AlarmGroup;
        if (alarmsRaised.TryGetValue(group, out float at) && Time.time - at < AlarmGroupSeconds)
        {
            return;
        }

        for (int i = 0; i < definition.Attacks.Count; i++)
        {
            MonsterAttack action = definition.Attacks[i];
            if (action != null && action.onSpotted && action.kind == MonsterAttackKind.Howl && attacks.CanUse(i, IsHowler))
            {
                alarmAction = i;
                alarmsRaised[group] = Time.time;
                return;
            }
        }
    }

    // A pack whose leader can raise the alarm leaves it to the leader.
    private bool PackHasCaller()
    {
        foreach (MonsterAttack action in definition.Attacks)
        {
            if (action != null && action.onSpotted && action.packLeaderOnly)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True while it is about to raise the alarm, or raising it: its pack waits for the call.</summary>
    private bool RaisesAlarm => alarmAction >= 0;

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
            NoteSpottedPlayer();
            PlanAlarm();
        }

        if (global::Player.IsPlayer(targetComponent.gameObject) && Time.time >= nextSpottedSoundAt)
        {
            nextSpottedSoundAt = Time.time + SpottedSoundSpacing;
            AudioManager.Play(rules.SpottedSound, transform.position);
        }

        WorldNoise.Emit(transform.position, rules.SpottedCallRadius);
    }
}
