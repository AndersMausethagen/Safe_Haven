using System.Collections;
using UnityEngine;

/// <summary>
/// Runs a boss encounter in its arena scene (Docs/BossEncounterResearch_2026-11-17.md, option C): the arrival stone is a way home
/// that costs nothing until the gate; the boss waits beyond the gate, asleep (BossEncounter.Hold); stepping through the gate
/// (EncounterGate) seals the mist behind the player, shuts the arrival stone and wakes the boss, and starts the fight's clock, after
/// which the boss enrages (the user: "a shown timer, a DPS check"); the way home opens where the boss falls, and nothing else opens it
/// (the user: "the only way out is by winning or dying"). A character who has reached the gate before starts at the rest stone.
/// <para>
/// Co-op: the boss is known by its BossDefinition, not by the one placed here (<see cref="Adopt"/>): on a friend's game that one is
/// removed as it wakes and the host's arrives later as a copy, which this arena takes in its place. A friend on the host's run steps
/// through free (<see cref="AsksPrice"/>).
/// </para>
/// <para>
/// Where it belongs: one per encounter arena scene, with its parts assigned. The raid it plays is a RaidDefinition with an Encounter.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "a friend's game runs its own gate and clock beside the host's; the boss is the host's (its copy is taken in " +
                               "by its definition, waits for this game's gate or the host's fight, and takes this clock's rage)")]
public sealed class EncounterArena : MonoBehaviour, ISceneSetup
{
    [Tooltip("The boss, placed beyond the gate. It sleeps until the gate seals. The arena knows it by its Boss Definition: on a friend's " +
             "game (co-op) the host's copy stands in for it.")]
    [SerializeField] private BossEncounter boss;
    [Tooltip("The gate of mist between the approach and the arena.")]
    [SerializeField] private EncounterGate gate;
    [Tooltip("The way home before the gate (the arrival stone): open until the gate seals, so leaving before it costs nothing.")]
    [SerializeField] private ExtractionPoint arrivalWay;
    [Tooltip("The way home after the fight, where the boss falls: shut until it does.")]
    [SerializeField] private ExtractionPoint wayHome;
    [Tooltip("Shown when the gate seals: the wall of mist behind the player, with its collider. Hidden until then.")]
    [SerializeField] private GameObject seal;
    [Tooltip("Where later tries start, by the gate, once this character has reached it. Empty: always at the map's Player Start.")]
    [SerializeField] private Transform restStone;
    [Tooltip("Where the player stands once through the gate: the far side of its wall of mist, which stays solid behind them. Empty: " +
             "where they are (then Seal must close the way back).")]
    [SerializeField] private Transform beyond;

    private float sealedAt = -1f;
    private float nextRampAt;
    // The boss this arena waits for, read from the one placed here as the scene wakes (on a friend's game it is then removed).
    private BossDefinition bossDefinition;
    // The boss fought here: the one placed, or on a friend's game the host's copy (Adopt).
    private BossEncounter adopted;
    // The damage the rage has added so far, given again to a boss taken in after it (a friend's copy, made late).
    private float rageAdded;

    /// <summary>The encounter running in the loaded scene, or null.</summary>
    public static EncounterArena Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>
    /// True on a friend's game on the host's run (co-op: a guest who followed the host in, and still so if the host has since left):
    /// the run, and the gate's offering, are the host's.
    /// </summary>
    public static bool FollowsAHost => HavenVisit.IsVisiting && GameSession.ActiveRaid != null;

    /// <summary>The encounter's rules, from the raid being played; null in a scene played without one.</summary>
    public BossEncounterDefinition Definition { get; private set; }

    /// <summary>The boss fought here: the one placed, or on a friend's game the host's copy once it has come (null until then).</summary>
    public BossEncounter Boss => adopted;
    public EncounterGate Gate => gate;
    public bool IsSealed => sealedAt >= 0f;
    public bool BossFallen { get; private set; }
    public bool IsEnraged { get; private set; }

    /// <summary>True when stepping through costs this player the gate's price: it has one, and they aren't following a host (<see cref="FollowsAHost"/>).</summary>
    public bool AsksPrice => Definition != null && Definition.HasGateCost && !FollowsAHost;

    /// <summary>Seconds until the boss enrages, while the fight is on; -1 before the gate, after the fall, and once it has enraged.</summary>
    public float EnrageIn => IsSealed && !BossFallen && !IsEnraged && Definition != null
        ? Mathf.Max(0f, sealedAt + Definition.EnrageSeconds - Time.time) : -1f;

    /// <summary>Seconds since the gate sealed (the fight's length so far), or 0 before.</summary>
    public float FightSeconds => IsSealed ? Time.time - sealedAt : 0f;

    // Before RaidMap (-100): the start is moved to the rest stone before the map puts the player there.
    public int SetupOrder => -110;

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogError("Only one EncounterArena can run at a time.", this);
            enabled = false;
            return;
        }

        Current = this;
        Definition = RaidDirector.Current != null ? RaidDirector.Current.Session.Definition.Encounter : null;
        bossDefinition = BossEncounter.DefinitionOf(boss);
        if (bossDefinition == null && Definition != null)
        {
            bossDefinition = Definition.Boss;
        }

        // The boss is taken in as it starts (before it decides whether it waits), whichever it turns out to be.
        BossEncounter.Appeared += Adopt;
        if (seal != null)
        {
            seal.SetActive(false);
        }

        if (wayHome != null)
        {
            wayHome.HoldShut($"The way home opens when {BossName} falls.");
        }
    }

    private void OnDestroy()
    {
        BossEncounter.Appeared -= Adopt;
        if (adopted != null)
        {
            adopted.Fell -= OnBossFell;
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    public IEnumerator Setup()
    {
        RaidDirector director = RaidDirector.Current;
        if (restStone != null && director != null && GameSession.Progress.HasReachedGate(director.Session.Definition.Id))
        {
            RaidMap map = FindAnyObjectByType<RaidMap>();
            if (map != null)
            {
                map.StartAt(restStone);
            }
        }

        yield break;
    }

    public string BossName => Definition != null ? Definition.BossName
        : bossDefinition != null ? bossDefinition.DisplayName : "the boss";

    /// <summary>
    /// Takes a boss that has appeared (BossEncounter.Appeared, in its Start) as this encounter's when it is the one this arena waits
    /// for, by its BossDefinition: the one placed here, or on a friend's game (co-op) the host's copy that stands in for it, made later.
    /// Before the gate it is held; once the gate has sealed it wakes, with the rage the clock has added so far. Taking the same boss
    /// again does nothing; another of the same kind takes the place of the last.
    /// </summary>
    public void Adopt(BossEncounter appeared)
    {
        if (appeared == null || appeared == adopted || BossFallen || bossDefinition == null || appeared.Definition != bossDefinition)
        {
            return;
        }

        if (adopted != null)
        {
            adopted.Fell -= OnBossFell;
        }

        adopted = appeared;
        adopted.Fell += OnBossFell;
        if (!IsSealed)
        {
            adopted.Hold();
            return;
        }

        adopted.WakeNow();
        adopted.Enrage(rageAdded);
    }

    /// <summary>
    /// Steps through the gate: pays its price (<see cref="AsksPrice"/>), seals the mist behind, shuts the arrival stone, wakes the boss
    /// and starts the clock. False, with a reason, when it can't (already sealed, the raid over, the price not in the bag).
    /// </summary>
    public bool Enter(GameObject user, out string problem)
    {
        problem = null;
        RaidDirector director = RaidDirector.Current;
        bool pays = AsksPrice;
        if (IsSealed)
        {
            problem = "The mist is sealed behind you.";
        }
        else if (director == null || !director.IsRunning)
        {
            problem = "Not now.";
        }
        else if (user == null || user != director.Player)
        {
            problem = "Only you can step through for yourself.";
        }
        else if (pays && !MaterialPool.For(user).CanAfford(Definition.GateResources, Definition.GateItems, out string missing))
        {
            problem = missing;
        }

        if (problem != null)
        {
            return false;
        }

        if (pays && !MaterialPool.For(user).TrySpend(Definition.GateResources, Definition.GateItems))
        {
            problem = "You no longer have what the gate asks.";
            return false;
        }

        sealedAt = Time.time;
        nextRampAt = float.MaxValue;
        if (seal != null)
        {
            seal.SetActive(true);
        }

        if (beyond != null)
        {
            ArenaFall.SetDown(user.transform, beyond);
        }

        if (arrivalWay != null)
        {
            arrivalWay.Close();
        }

        GameSession.Progress.MarkGateReached(director.Session.Definition.Id);
        // Co-op: once the host is through, friends can no longer follow into the run.
        if (CoopSession.Current != null && CoopSession.Current.Raids != null)
        {
            CoopSession.Current.Raids.CloseFollowing();
        }

        director.Session.RecordEvent($"Stepped through the mist to {BossName}");
        CameraShake.Add(0.4f);
        GameMessages.Post("The mist closes behind you. There is no way back but through.", MessageKind.Warning);
        if (adopted != null)
        {
            adopted.WakeNow();
        }

        return true;
    }

    private void Update()
    {
        if (!IsSealed || BossFallen || Definition == null || RaidDirector.Current == null || !RaidDirector.Current.IsRunning)
        {
            return;
        }

        if (!IsEnraged && Time.time >= sealedAt + Definition.EnrageSeconds)
        {
            IsEnraged = true;
            nextRampAt = Time.time + Definition.EnrageInterval;
            AddRage(Definition.EnrageDamage);
            GameMessages.Post(Definition.EnrageLine, MessageKind.Warning);
            RaidDirector.Current.Session.RecordEvent($"{BossName} enraged");
        }
        else if (IsEnraged && Time.time >= nextRampAt)
        {
            nextRampAt = Time.time + Definition.EnrageInterval;
            AddRage(Definition.EnrageRamp);
        }
    }

    // The rage goes to the boss here now, and is kept for one taken in later (a friend's copy of the host's).
    private void AddRage(float damage)
    {
        rageAdded += damage;
        if (adopted != null)
        {
            adopted.Enrage(damage);
        }
    }

    private void OnBossFell(BossEncounter fallen)
    {
        if (BossFallen)
        {
            return;
        }

        BossFallen = true;
        RaidDirector director = RaidDirector.Current;
        if (director != null && !director.Session.IsOver)
        {
            GameSession.Progress.RecordKill(director.Session.Definition.Id, System.DateTime.UtcNow);
            director.Session.RecordEvent($"Brought down {BossName} in {RaidStats.FormatTime(FightSeconds)}");
        }

        if (wayHome != null)
        {
            wayHome.Release();
        }
    }
}
