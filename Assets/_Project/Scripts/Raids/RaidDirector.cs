using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs a raid scene. It starts the clock with the rules the raid was launched with, strengthens the enemies when
/// modifiers say so, warns as time runs low, and ends the raid when the player extracts (see ExtractionPoint), dies or
/// runs out of time. Extracting takes everything the player carries back home. Failing (dying or running out of time)
/// keeps knowledge, the safe pocket and the deployed equipment with durability damage, and loses everything else (GDD
/// §8.3, PlayerSnapshot.AfterFailedRaid); the player then wakes at the portal. After a short pause the player is taken
/// to the full-screen results (RaidResultsScreen) and from there home. The clock only starts once the scene is generated
/// and the loading screen lifts (SceneTransition.WhenReady). A scene played straight from the
/// editor runs a test raid from the fallback definition. Systems in the raid read its rules through <see cref="Current"/>.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class RaidDirector : MonoBehaviour
{
    [Tooltip("Played when this scene is started without going through a raid portal, for testing.")]
    [SerializeField] private RaidDefinition fallbackRaid;
    [Tooltip("Where a test raid sends the player when it ends.")]
    [SerializeField] private string fallbackReturnScene = "SafeHaven";
    [Tooltip("Seconds left at which the player is warned that time is running out.")]
    [SerializeField] private List<float> warningTimes = new List<float> { 120f, 60f, 30f, 10f };
    [Tooltip("Seconds the outcome is shown before the player is sent back.")]
    [SerializeField, Min(0f)] private float returnDelay = 4f;
    [Tooltip("The full-screen raid report shown between the raid and home. When it isn't in Build Settings, the player " +
             "goes straight home and sees the report there.")]
    [SerializeField] private string resultsScene = "RaidResults";

    private readonly List<float> pendingWarnings = new List<float>();
    private GameObject player;
    private Damageable playerHealth;
    private RaidGenerator generator;
    private bool running;

    /// <summary>The raid running in the loaded scene, or null outside raids.</summary>
    public static RaidDirector Current { get; private set; }

    public RaidSession Session { get; private set; }
    public RaidParameters Parameters => Session.Parameters;
    public GameObject Player => player;

    /// <summary>True from when the clock starts until the raid ends.</summary>
    public bool IsRunning => running && Session != null && !Session.IsOver;

    /// <summary>The report of the raid once it has ended (also handed to GameSession); null while it runs.</summary>
    public RaidReport Report { get; private set; }

    /// <summary>Raised once when the raid ends, after the outcome has been recorded.</summary>
    public event Action<RaidSession> Ended;

    /// <summary>How many danger stages have begun (RaidDefinition.Stages): 0 is the calm before the first.</summary>
    public int StageIndex { get; private set; }

    /// <summary>The danger stage the raid is in, or null before the first.</summary>
    public RaidStage CurrentStage => RaidStages.Get(Session?.Definition.Stages, StageIndex);

    /// <summary>How much more the woods give now for staying on (the danger stage's Loot Bonus): 1 in the calm.</summary>
    public float StageLoot => 1f + (CurrentStage != null ? CurrentStage.LootBonus : 0f);

    /// <summary>The stage loot of the scene's raid, or 1 outside a raid.</summary>
    public static float StageLootNow => Current != null ? Current.StageLoot : 1f;

    /// <summary>Raised when a new danger stage begins, after enemies are strengthened and its spawn groups woken.</summary>
    public event Action<int, RaidStage> StageChanged;

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogError("Only one RaidDirector can run at a time.", this);
            enabled = false;
            Destroy(this);
            return;
        }

        // "global::" because this class has its own Player property (the player's GameObject).
        player = global::Player.Current != null ? global::Player.Current.gameObject : null;
        Session = GameSession.ActiveRaid ?? StartTestRaid();
        if (Session == null)
        {
            Debug.LogError("RaidDirector has no raid to run. Enter through a raid portal, or set a fallback raid to test the scene.", this);
            enabled = false;
            return;
        }

        Current = this;

        // The raid's own systems (roaming spawns and the like, set up in the Systems Editor) are added here; every raid
        // scene also sleeps far idle creatures (global raid rules).
        foreach (RaidSystem system in Session.Definition.Systems)
        {
            if (system != null && system.Enabled)
            {
                system.Install(this);
            }
        }

        if (!TryGetComponent(out CreatureSleep _))
        {
            gameObject.AddComponent<CreatureSleep>();
        }

        if (!TryGetComponent(out GatheringNoise _))
        {
            gameObject.AddComponent<GatheringNoise>();
        }
    }

    private void Start()
    {
        if (Session == null)
        {
            return;
        }

        if (player != null && player.TryGetComponent(out playerHealth))
        {
            playerHealth.Died += OnPlayerDied;
        }
        else
        {
            Debug.LogWarning("RaidDirector found no Player with a Damageable, so dying won't end the raid.", this);
        }

        PlayerGathering.Swung += OnSwung;
        // Thresholds the raid starts below (a short test raid) would all fire at once, so they are skipped.
        foreach (float time in warningTimes)
        {
            if (time > 0f && time < Session.TimeRemaining)
            {
                pendingWarnings.Add(time);
            }
        }

        pendingWarnings.Sort((a, b) => b.CompareTo(a));
        // Like the warnings, stages a short test raid starts inside are already under way, without fanfare or waves.
        StageIndex = RaidStages.IndexAt(Session.Definition.Stages, Session.TimeRemaining);
        generator = FindAnyObjectByType<RaidGenerator>();
        // The clock starts once the loading screen lifts and the layout is generated, which may spawn more enemies.
        SceneTransition.WhenReady(BeginRaid);
    }

    private void BeginRaid()
    {
        if (this == null || Session.IsOver)
        {
            return;
        }

        running = true;
        StrengthenEnemies(true);
        BringCompanion();
        var pieceRandom = new System.Random(RaidLayout.SeedFor(Session.Seed, "Set Piece Guards"));
        foreach (RaidSetPieceSite site in FindObjectsByType<RaidSetPieceSite>(FindObjectsInactive.Exclude))
        {
            site.BringGuards(this, pieceRandom);
        }

        Transform start = FindAnyObjectByType<RaidMap>()?.PlayerStart;
        RaidElites.PromoteAtStart(Session, start != null ? start.position : (Vector3?)null, HealthMultiplier, PowerMultiplier);
        if (NightSettings.IsDark)
        {
            GameMessages.Post(NightSettings.Current.NightRaidText, MessageKind.Warning);
        }

        GameMessages.Post($"{Session.Definition.DisplayName}: grab what you can and reach an extraction within {RaidStats.FormatTime(Session.TimeRemaining)}.");
    }

    // They walk in beside the player. Fights grow with the raid's tier and so do they, so they stay as much help as they are in the first.
    private void BringCompanion()
    {
        Companion prefab = SettlementSettings.Current.RaidCompanionPrefab;
        if (Session.Companion == null || Session.Companion.RaidCompanion == null || prefab == null || player == null)
        {
            return;
        }

        Vector3 beside = player.transform.position - player.transform.forward * 2f + player.transform.right * 1.5f;
        if (!UnityEngine.AI.NavMesh.SamplePosition(beside, out UnityEngine.AI.NavMeshHit hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
        {
            Debug.LogWarning($"Nowhere for {Session.Companion.DisplayName} to stand; they stayed at home.", this);
            return;
        }

        Companion = Companion.SpawnForRaid(prefab, Session.Companion, hit.position);
        Companion.FormationOffset = new Vector3(2f, 0f, -3f);
        ResidentRecord record = GameSession.Settlement.Find(Session.Companion);
        Toughen(Companion, record != null ? CompanionBond.RankFor(record.RaidsAlong, SettlementSettings.Current.BondRanks) : null);
    }

    private void Toughen(Companion who, CompanionBondRank rank)
    {
        StatSheet stats = who.GetComponent<Combatant>().Stats;
        float tougher = rank != null ? rank.healthBonus : 0f;
        float harder = rank != null ? rank.damageBonus : 0f;
        stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, PowerMultiplier * (1f + tougher) - 1f, this));
        stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, HealthMultiplier * (1f + harder) - 1f, this));
        Damageable body = who.GetComponent<Damageable>();
        body.Heal(body.MaxHealth);
    }

    /// <summary>Whoever is lost longest stands here, waiting, as a rumour said they would.</summary>
    public void StandLostAt(Vector3 position)
    {
        FallenRecord lost = GameSession.Settlement.LostToFind;
        Companion prefab = SettlementSettings.Current.RaidCompanionPrefab;
        if (lost == null || prefab == null || LostOne != null)
        {
            return;
        }

        Vector3 beside = position + Vector3.right * 2f;
        if (!UnityEngine.AI.NavMesh.SamplePosition(beside, out UnityEngine.AI.NavMeshHit hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
        {
            Debug.LogWarning($"Nowhere for {lost.Resident.DisplayName} to stand; they weren't found this raid.", this);
            return;
        }

        LostOne = Companion.SpawnForRaid(prefab, lost.Resident, hit.position);
        LostOne.FormationOffset = new Vector3(-2f, 0f, -3f);
        Toughen(LostOne, null);
        LostOne.StandLost();
    }

    /// <summary>The player has found them. They follow without knowing why.</summary>
    public void LeadLostHome()
    {
        if (LostOne != null)
        {
            LostOneFollows = true;
            LostOne.FollowWhoFoundThem();
            GameMessages.Post(string.Format(SettlementSettings.Current.FoundFollowLine, LostOne.DisplayName), MessageKind.Gain);
        }
    }

    // Home only with the player, alive and close by: they don't know the way. Never found, nothing has changed.
    private void SettleTheLost(bool extracted)
    {
        if (LostOne == null || !LostOneFollows)
        {
            return;
        }

        bool close = player != null && Vector3.Distance(player.transform.position, LostOne.transform.position) <= SettlementSettings.Current.FoundComesHomeWithin;
        if (extracted && close && !LostOne.IsLost && !LostOne.IsDowned && GameSession.Settlement.FindAgain(LostOne.Resident))
        {
            Report.FoundAgain = LostOne.Resident;
        }
        else
        {
            Report.LeftBehind = LostOne.Resident;
        }
    }

    private void OnDestroy()
    {
        SceneTransition.CancelWhenReady(BeginRaid);
        PlayerGathering.Swung -= OnSwung;
        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }

        if (Current == this)
        {
            Current = null;
        }

        // Still active here means the scene was left mid-raid; the menu must be able to load a save again.
        GameSession.AbandonRaid(Session);
    }

    private void Update()
    {
        if (!running || Session.IsOver)
        {
            return;
        }

        if (Session.Tick(Time.deltaTime))
        {
            Finish();
            return;
        }

        while (pendingWarnings.Count > 0 && Session.TimeRemaining <= pendingWarnings[0])
        {
            GameMessages.Post($"{RaidStats.FormatTime(pendingWarnings[0])} left! Get to an extraction.", MessageKind.Warning);
            pendingWarnings.RemoveAt(0);
        }

        int stage = RaidStages.IndexAt(Session.Definition.Stages, Session.TimeRemaining);
        while (StageIndex < stage)
        {
            EnterStage(StageIndex + 1);
        }
    }

    private void EnterStage(int index)
    {
        StageIndex = index;
        RaidStage stage = CurrentStage;
        StrengthenEnemies(false);
        if (stage != null)
        {
            GameMessages.Post(string.IsNullOrWhiteSpace(stage.Message) ? $"{stage.DisplayName}." : stage.Message, MessageKind.Warning);
        }

        if (generator != null)
        {
            foreach (SpawnGroup group in generator.SpawnStage(index))
            {
                Prepare(group);
            }
        }

        StageChanged?.Invoke(index, stage);
    }

    /// <summary>
    /// Fills a waiting spawn group now (an extraction alarm, a scripted event) with this raid's seed and rules: its
    /// creatures get the raid's and the current stage's strength, and hunt the player if the group says so. Returns how
    /// many were spawned; 0 when the group had already spawned or the scene has no RaidGenerator.
    /// </summary>
    public int Wake(SpawnGroup group)
    {
        if (group == null || group.HasSpawned || generator == null || Session.IsOver)
        {
            return 0;
        }

        // A group filled by a raid spawn list fills the whole list, possibly onto other groups' points too.
        IReadOnlyList<GameObject> spawned = generator.SpawnNow(group);
        if (group.Trigger == SpawnTrigger.Alarm)
        {
            AlarmsRaised++;
        }

        int count = 0;
        foreach (GameObject root in spawned)
        {
            if (root != null)
            {
                count++;
                PrepareSpawned(root, group.HuntPlayer);
            }
        }

        return count;
    }

    /// <summary>How many alarm spawn groups have been woken this raid (extraction alarms). Roaming spawns grow with it.</summary>
    public int AlarmsRaised { get; private set; }

    private int Prepare(SpawnGroup group)
    {
        int count = 0;
        foreach (GameObject spawned in group.Spawned)
        {
            if (spawned == null)
            {
                continue;
            }

            count++;
            PrepareSpawned(spawned, group.HuntPlayer);
        }

        return count;
    }

    /// <summary>
    /// Gives creatures spawned mid-raid (a pack root and all its members included) the raid's and the current stage's
    /// strength at full health, and when <paramref name="huntPlayer"/> is set sends them straight at the player.
    /// </summary>
    /// <param name="mayBeElite">Off for what a creature summons: a swarm of elites would be no fun, and no reward.</param>
    public void PrepareSpawned(GameObject root, bool huntPlayer, bool mayBeElite = true)
    {
        if (root == null)
        {
            return;
        }

        foreach (Combatant combatant in root.GetComponentsInChildren<Combatant>(true))
        {
            if (combatant.Faction == Faction.Enemy)
            {
                Strengthen(combatant, true);
            }
        }

        if (huntPlayer && mayBeElite)
        {
            RaidElites.RollHunter(Session, root, HealthMultiplier, PowerMultiplier);
        }

        if (!huntPlayer || playerHealth == null)
        {
            return;
        }

        foreach (MonsterAI ai in root.GetComponentsInChildren<MonsterAI>())
        {
            ai.PlayerOnly = true;
            ai.Relentless = true;
            ai.Engage(playerHealth);
        }
    }

    /// <summary>Ends the raid as extracted, if it is still running and the user is the player. Called by extraction points.</summary>
    public bool Extract(GameObject user)
    {
        if (user == null || user != player || !Session.End(RaidOutcome.Extracted))
        {
            return false;
        }

        Finish();
        return true;
    }

    private void OnSwung(GatheringSwing swing)
    {
        if (!Session.IsOver)
        {
            Session.Gathering.Record(swing.Result);
        }
    }

    private void OnPlayerDied(Damageable victim, DamageInfo killingBlow)
    {
        if (Session.End(RaidOutcome.Died))
        {
            // Co-op: friends still standing on the run are watched first; the raid ends for this player afterwards.
            if (CoopSpectate.TryBegin(Finish))
            {
                return;
            }

            Finish();
        }
    }

    private void Finish()
    {
        PlayerSnapshot final = player != null ? PlayerSnapshot.Capture(player) : Session.Loadout;
        bool extracted = Session.Outcome == RaidOutcome.Extracted;
        // A failed raid keeps knowledge, the safe pocket and the deployed equipment, worn down (GDD §8.3).
        var wear = new List<EquipmentWear>();
        PlayerSnapshot keeps = extracted ? final : final?.AfterFailedRaid(Session.Loadout, DurabilitySettings.Current.LossOnFailedRaid, wear);
        Report = RaidReport.Create(Session, Session.Loadout, keeps, final, wear);
        if (PlayerTransport.Current != null)
        {
            PlayerTransport.Current.LeaveRaid(Report, extracted);
        }

        if (Session.Companion != null)
        {
            Report.Companion = Session.Companion;
            // Down when the raid ends is as lost as bleeding out: nobody went back for them.
            Report.CompanionLost = Companion != null && (Companion.IsLost || Companion.IsDowned);
            if (Report.CompanionLost)
            {
                GameSession.Settlement.LoseResident(Session.Companion, Session.Definition.DisplayName);
            }
            else if (Companion != null)
            {
                bool rose = GameSession.Settlement.RecordRaidAlong(Session.Companion, SettlementSettings.Current.BondRanks, extracted, Session.BossesBeaten,
                    Companion.TimesHelpedUp, Session.TimeRemaining, Companion.Kills, SettlementSettings.Current.MemoryReturnsEvery);
                ResidentRecord record = GameSession.Settlement.Find(Session.Companion);
                Report.CompanionNewRank = rose ? CompanionBond.RankFor(record.RaidsAlong, SettlementSettings.Current.BondRanks)?.name : null;
            }
        }

        SettleTheLost(extracted);
        GameSession.EndRaid(Report);
        SaveGameService.SaveOnceHome();
        GameEvents.Raise(new GameEvent(GameEventKind.RaidFinished, Session.Definition, 1, null, Session.Outcome, Session.Elapsed));
        GameSession.CarryPlayerState(keeps);
        // Coming home through the portal, or waking at the portal after a failure.
        GameSession.SetArrivalPoint(extracted ? PlayerArrivalPoint.Portal : PlayerArrivalPoint.HomePortal);

        StopPlayer();
        GameMessages.Post(extracted
                ? "Extracted! Heading back with everything you carry."
                : "The raid is lost. You'll wake at the portal with your gear, but everything else you carried is gone.",
            extracted ? MessageKind.Gain : MessageKind.Warning);
        // Started before listeners run, so a faulty one can't leave the player stranded in the raid.
        StartCoroutine(ReturnAfterDelay());
        Ended?.Invoke(Session);
    }

    // The raid is decided; the player only watches the outcome until the scene changes.
    private void StopPlayer()
    {
        if (player == null)
        {
            return;
        }

        if (player.TryGetComponent(out PlayerStateMachine stateMachine))
        {
            stateMachine.EndInteraction();
        }

        if (player.TryGetComponent(out PlayerInputHandler input))
        {
            input.enabled = false;
        }
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSecondsRealtime(returnDelay);

        // The results screen reads the report and sends the player on to the return scene from there.
        if (CanLoad(resultsScene))
        {
            SceneLoader.Load(resultsScene, Report != null && Report.Extracted ? "Counting the haul" : "The raid is lost");
            yield break;
        }

        // The raid always ends somewhere loadable, so a misconfigured return scene can't strand the player.
        string scene = Session.ReturnScene;
        if (!CanLoad(scene))
        {
            Debug.LogError($"The raid can't return to '{scene}' because it isn't in Build Settings; trying '{fallbackReturnScene}'.", this);
            scene = fallbackReturnScene;
        }

        if (CanLoad(scene))
        {
            SceneLoader.Load(scene);
        }
        else if (SceneManager.sceneCountInBuildSettings > 0)
        {
            Debug.LogError($"The fallback return scene '{fallbackReturnScene}' isn't in Build Settings either; loading the first scene in the build.", this);
            SceneLoader.Load(0);
        }
        else
        {
            Debug.LogError("The raid has nowhere to return to: Build Settings list no scenes.", this);
        }
    }

    private static bool CanLoad(string scene)
    {
        return !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);
    }

    // The raid's enemy multipliers times the current stage's bonus and what the hour adds.
    /// <summary>The raid's enemy health multiplier right now.</summary>
    /// <summary>Who came along on this raid, once they have walked in.</summary>
    public Companion Companion { get; private set; }

    /// <summary>Someone lost on an earlier raid, standing where a rumour said. Null when nobody is out there or they had nowhere to stand.</summary>
    public Companion LostOne { get; private set; }

    /// <summary>The player has found them, and they are coming along.</summary>
    public bool LostOneFollows { get; private set; }

    public float HealthMultiplier => Parameters.EnemyHealthMultiplier * (1f + (CurrentStage?.EnemyHealthBonus ?? 0f)) * NightSettings.Current.HealthAt(NightSettings.Darkness);
    /// <summary>The raid's enemy damage multiplier right now.</summary>
    public float PowerMultiplier => Parameters.EnemyPowerMultiplier * (1f + (CurrentStage?.EnemyPowerBonus ?? 0f)) * NightSettings.Current.PowerAt(NightSettings.Darkness);

    /// <param name="fillHealth">
    /// At the start, living enemies are topped up to their new maximum. When a stage strengthens them mid-fight, they only
    /// gain the extra maximum, so a wounded wolf stays wounded.
    /// </param>
    private void StrengthenEnemies(bool fillHealth)
    {
        if (Mathf.Approximately(HealthMultiplier, 1f) && Mathf.Approximately(PowerMultiplier, 1f) && StageIndex == 0)
        {
            return;
        }

        // Inactive enemies (waiting for a trigger, say) are included. Enemies spawned later by this raid get the same
        // treatment through Prepare; anything else that spawns enemies needs to apply it itself.
        foreach (Combatant combatant in FindObjectsByType<Combatant>(FindObjectsInactive.Include))
        {
            if (combatant.Faction == Faction.Enemy)
            {
                Strengthen(combatant, fillHealth);
            }
        }
    }

    private void Strengthen(Combatant combatant, bool fillHealth)
    {
        float health = HealthMultiplier;
        float power = PowerMultiplier;
        combatant.TryGetComponent(out Damageable damageable);
        float oldMax = damageable != null ? damageable.MaxHealth : 0f;

        // Percent bonuses on top of each creature's own stats, added by this raid and gone with the scene.
        StatSheet stats = combatant.Stats;
        stats.RemoveModifiersFrom(this);
        stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, health - 1f, this));
        stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, power - 1f, this));

        // Enemies that have not woken yet start at their new maximum when they do; the rest are healed.
        if (damageable != null && damageable.CurrentHealth > 0f)
        {
            float gain = fillHealth ? damageable.MaxHealth : damageable.MaxHealth - oldMax;
            if (gain > 0f)
            {
                damageable.Heal(gain);
            }
        }
    }

    private RaidSession StartTestRaid()
    {
        if (fallbackRaid == null)
        {
            return null;
        }

        RaidModifierDefinition[] none = Array.Empty<RaidModifierDefinition>();
        var session = new RaidSession(fallbackRaid, none, RaidParameters.Calculate(fallbackRaid, none),
            player != null ? PlayerSnapshot.Capture(player) : null, fallbackReturnScene);
        GameSession.BeginRaid(session);
        return session;
    }
}
