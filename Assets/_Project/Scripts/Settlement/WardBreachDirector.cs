using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Plays ward breaches in Safe Haven (GDD §6.6–6.9): warning → preparation → defense → recovery. Coming home from enough
/// successful raids turns the next breach (WardBreachSettings) Pending: the player is warned and its lanes appear. They
/// prepare and start the defense at the portal, or the ward gives way by itself at a nightfall once it has been put off too
/// long.
/// <para>
/// The first breach is scripted and forgiving: enemies come along one marked path, hunt only the player, buildings are
/// shielded and falling just ends it. Later ones (Threatens Home) come in waves down several lanes and go for the home:
/// half for the ward, half for the nearest building, turning on anyone who gets close and giving up on anyone who runs
/// too far. Buildings can be broken but never lost (Damageable.HealthFloor). The player falling doesn't end it, as they
/// wake at the portal and can go on. It ends when every attacker is dead, or when the ward falls: the attackers leave,
/// the ward is strained for a while and the breach can be tried again.
/// </para>
/// <para>
/// Where it belongs: one per Safe Haven scene, with a spawn point at the start of the path and a path marker object, whose
/// look each lane copies. The numbers and texts live in WardBreachDefinition and WardBreachSettings; status in SettlementState.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class WardBreachDirector : MonoBehaviour
{
    private const float CheckSeconds = 1f;
    private const float NextWaveAfter = 35f;
    private const int FewLeft = 1;
    private const float MarkerHeight = 0.15f;
    private const float MarkerStep = 2f;

    [Tooltip("The breach played when WardBreachSettings lists none.")]
    [SerializeField] private WardBreachDefinition definition;
    [Tooltip("Where the enemies appear: the outer end of the marked path.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Shown while the first breach is pending or under way, so the player sees where the enemies will come from. Later breaches copy its look for each lane.")]
    [SerializeField] private GameObject pathMarker;
    [Tooltip("Seconds after arriving in Safe Haven before the warning appears, so it isn't lost among other messages.")]
    [SerializeField, Min(0f)] private float warningDelay = 4f;
    [Tooltip("Enemies spread this far around the spawn point, in meters.")]
    [SerializeField, Min(0f)] private float spawnSpread = 2f;

    private readonly List<Damageable> attackers = new List<Damageable>();
    private readonly List<MonsterAI> hunters = new List<MonsterAI>();
    private readonly List<Damageable> shielded = new List<Damageable>();
    private readonly List<BreachLane> lanes = new List<BreachLane>();
    private readonly List<GameObject> laneMarkers = new List<GameObject>();
    private NavMeshPath road;
    private Damageable playerHealth;
    private Damageable ward;
    private DayNightCycle cycle;
    private WardBreachDefinition running;
    private bool spawning;
    private bool givingWay;
    private float startedAt;
    private float nextCheck;
    private int nextLane;
    private int lanesFor = -1;
    private (int ward, int buildings) lanesSeen = (-1, -1);

    /// <summary>The breach director in the loaded scene, or null.</summary>
    public static WardBreachDirector Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>The breach under way, else the one due next.</summary>
    public WardBreachDefinition Definition => running != null ? running
        : WardBreachSettings.Current.For(GameSession.Here.BreachesWon) ?? definition;

    public bool IsActive { get; private set; }

    /// <summary>True from the ward giving way (its countdown) to the breach's end: the Haven is under attack.</summary>
    public bool IsUnderWay => IsActive || givingWay;
    public bool IsPending => Definition != null && !IsActive && GameSession.Here.Breach == BreachStatus.Pending;

    /// <summary>The ways in, for the breach pending or under way. Empty for the first, which keeps to its marked path.</summary>
    public IReadOnlyList<BreachLane> Lanes => lanes;

    /// <summary>Who is attacking now.</summary>
    public IReadOnlyList<Damageable> Attackers => attackers;

    /// <summary>The wave under way, from 1.</summary>
    public int Wave { get; private set; }
    public int WaveCount => Definition != null ? Definition.WaveCount : 0;

    /// <summary>True when this breach can bring the ward down: the first can't.</summary>
    public bool WardAtRisk => ward != null;

    /// <summary>The ward's strength in this fight, 0..1; 1 when it isn't in danger.</summary>
    public float WardHealth => ward != null ? ward.HealthFraction : 1f;

    private int fallsThisBreach;

    /// <summary>Seconds before a fallen player wakes in this breach: longer with each fall (the death rules).</summary>
    public float WakeSeconds
    {
        get
        {
            DeathRuleSet rules = DeathSettings.Now;
            return Mathf.Min(Mathf.Max(rules.wakeSeconds, rules.wakeMost), rules.wakeSeconds + rules.wakePerFall * Mathf.Max(0, fallsThisBreach - 1));
        }
    }

    /// <summary>True when the warned breach has been put off too long: it comes at the next nightfall.</summary>
    public bool IsOverdue
    {
        get
        {
            IHavenView settlement = GameSession.Here;
            return IsPending && settlement.BreachesWon > 0
                && settlement.SuccessfulExtractions >= settlement.BreachPendingSince + WardBreachSettings.Current.RaidsPutOff;
        }
    }

    /// <summary>How the coming breach is described at the portal: what comes and from where.</summary>
    public string Hint
    {
        get
        {
            string hint = Definition != null ? Definition.StartHint : "";
            return lanes.Count > 0 ? $"{hint}, from {BreachLanes.Describe(lanes)}" : hint;
        }
    }

    public int AttackersLeft
    {
        get
        {
            int left = 0;
            foreach (Damageable attacker in attackers)
            {
                if (attacker != null && attacker.IsAlive)
                {
                    left++;
                }
            }

            return left;
        }
    }

    private void Awake()
    {
        Current = this;
        road = new NavMeshPath();
        // The Haven's look under attack (WardBreachSettings › Breach Look).
        gameObject.AddComponent<BreachMood>();
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }
    }

    private void OnEnable()
    {
        GameSession.Here.Changed += UpdateMarker;
    }

    private void OnDisable()
    {
        GameSession.Here.Changed -= UpdateMarker;
    }

    private IEnumerator Start()
    {
        cycle = FindAnyObjectByType<DayNightCycle>();
        if (Player.Current != null)
        {
            playerHealth = Player.Current.Health;
            playerHealth.Died += OnPlayerDied;
        }

        UpdateMarker();
        yield return new WaitForSeconds(warningDelay);
        Announce();
    }

    // A breach only ever comes due on the way home from a successful raid, never after a failed one (GDD §6.6).
    private void Announce()
    {
        IHavenView settlement = GameSession.Here;
        WardBreachDefinition next = Definition;
        if (next == null)
        {
            return;
        }

        if (settlement.Breach != BreachStatus.Pending)
        {
            int due = settlement.BreachesWon == 0 ? next.AvailableAfterExtractions : settlement.NextBreachAt; // as SettlementState.NextBreachDueAt
            if (settlement.SuccessfulExtractions < due)
            {
                return;
            }

            // The host's game warns of its own breaches; a guest's copy hears of it with the settlement.
            if (HavenVisit.IsVisiting)
            {
                return;
            }

            // Past the visit guard: one's own Haven is the one here.
            GameSession.Home.WarnOfBreach();
            GameMessages.Post(next.WarningText, MessageKind.Warning);
            AudioManager.Play(AudioManager.Sounds?.BreachWarning, transform.position);
        }

        if (IsOverdue)
        {
            GameMessages.Post("The portal can't hold much longer. At nightfall the ward will give way.", MessageKind.Warning);
            AudioManager.Play(AudioManager.Sounds?.BreachWarning, transform.position);
        }
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + CheckSeconds;
        if (IsActive)
        {
            DirectAttackers();
            if (Time.time >= startedAt + WardBreachSettings.Current.LongestDefense)
            {
                Win();
            }
        }
        else if (IsOverdue && !givingWay && cycle != null && cycle.IsNight && playerHealth != null && playerHealth.IsAlive && !HavenVisit.IsVisiting)
        {
            StartCoroutine(GiveWay());
        }
    }

    private IEnumerator GiveWay()
    {
        givingWay = true;
        GameMessages.Post($"{Definition.DisplayName}: the ward is giving way. Get to the clearing!", MessageKind.Warning);
        AudioManager.Play(AudioManager.Sounds?.BreachWarning, transform.position);
        yield return new WaitForSeconds(WardBreachSettings.Current.NightfallWarning);
        givingWay = false;
        Begin();
    }

    /// <summary>Starts the defense. Called from the portal's menu, or at nightfall when it has been put off too long.</summary>
    public void Begin()
    {
        WardBreachDefinition breach = Definition;
        // A guest's copy of a friend's Haven doesn't hold its own breaches; the host's game does.
        // Never on a corrupted night: the two would come at once.
        if (!IsPending || breach == null || !breach.HasEnemies || playerHealth == null || !playerHealth.IsAlive || HavenVisit.IsVisiting
            || (CorruptionNights.Current != null && CorruptionNights.Current.IsActive))
        {
            return;
        }

        running = breach;
        IsActive = true;
        startedAt = Time.time;
        Wave = 1;
        fallsThisBreach = 0;
        if (breach.ThreatensHome)
        {
            RaiseWard();
        }
        else
        {
            ProtectBuildings(true);
        }

        UpdateMarker();
        BreachHud.Create();
        GameMessages.Post($"{breach.DisplayName}: they are coming. Hold the clearing!", MessageKind.Warning);
        AudioManager.Play(AudioManager.Sounds?.BreachBegins, transform.position);
        StartCoroutine(SpawnAttackers());
    }

    // The ward as something to wear down: it only stands while a breach is on.
    private void RaiseWard()
    {
        Damageable prefab = WardBreachSettings.Current.WardAnchor;
        if (prefab == null || HomePortal.Current == null)
        {
            Debug.LogWarning("WardBreachSettings has no ward anchor, so this breach can't be lost.", this);
            return;
        }

        ward = Instantiate(prefab, HomePortal.Current.transform.position, Quaternion.identity);
        float stronger = GameSession.Here.WardLevel * WardBreachSettings.Current.WardHealthPerLevel
                         + GameSession.Here.JobBonus(job => job.WardHealth);
        ward.GetComponent<Combatant>().Stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, stronger, this));
        ward.Heal(ward.MaxHealth);
        ward.Died += OnWardFell;
    }

    private IEnumerator SpawnAttackers()
    {
        spawning = true;
        for (int wave = 0; wave < running.WaveCount && IsActive; wave++)
        {
            Wave = wave + 1;
            if (wave > 0)
            {
                GameMessages.Post($"Wave {Wave} of {running.WaveCount}!", MessageKind.Warning);
                AudioManager.Play(AudioManager.Sounds?.BreachBegins, transform.position);
            }

            // What was done to the breach's source in a raid leaves it short of fighters, though never of all of them.
            List<GameObject> roster = running.Roster(wave, GameSession.Here.BreachesWon);
            roster.RemoveAll(prefab => prefab == null);
            // A wave with nobody in it (every group gated by breaches won, or without a prefab) is skipped: roster[0] threw and
            // left the breach spawning for good, ending only at its longest.
            if (roster.Count == 0)
            {
                continue;
            }

            // More of them, and more again for each of the Haven's people who fight (everyone turns out now): the roster comes round.
            int fielded = WardBreachSettings.Current.Fielded(roster.Count, GuardPosts.BreachFighters(GameSession.Here, DefenceSettings.Current),
                GameSession.Here.BreachWeakening);
            List<GameObject> fillers = WardBreachSettings.RankAndFile(roster);
            for (int i = 0; i < fielded && IsActive; i++)
            {
                Spawn(i < roster.Count ? roster[i] : fillers[(i - roster.Count) % fillers.Count]);
                if (i < fielded - 1)
                {
                    yield return new WaitForSeconds(running.SecondsBetweenEnemies);
                }
            }

            // The next wave comes when this one is nearly done, or when it has had its time.
            float waitUntil = Time.time + NextWaveAfter;
            while (wave < running.WaveCount - 1 && IsActive && AttackersLeft > FewLeft && Time.time < waitUntil)
            {
                yield return null;
            }
        }

        spawning = false;
        CheckWon();
    }

    private Vector3 NextSpawn(out Quaternion facing)
    {
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        facing = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        if (lanes.Count > 0)
        {
            BreachLane lane = lanes[nextLane++ % lanes.Count];
            origin = lane.Start;
            if (HomePortal.Current != null)
            {
                facing = Quaternion.LookRotation(HomePortal.Current.transform.position - origin);
            }
        }

        Vector2 offset = Random.insideUnitCircle * spawnSpread;
        Vector3 position = origin + new Vector3(offset.x, 0f, offset.y);
        return NavMesh.SamplePosition(position, out NavMeshHit hit, spawnSpread + 2f, NavMesh.AllAreas) ? hit.position : origin;
    }

    private void Spawn(GameObject prefab)
    {
        Vector3 position = NextSpawn(out Quaternion facing);
        GameObject enemy = Instantiate(prefab, position, facing);
        // Breach enemies carry no loot: the breach has its own reward.
        foreach (LootOnDeath loot in enemy.GetComponentsInChildren<LootOnDeath>(true))
        {
            loot.enabled = false;
        }

        float stronger = WardBreachSettings.Current.Strength(GameSession.Here.BreachesWon) * (1f - GameSession.Here.BreachWeakening);
        if (enemy.TryGetComponent(out Combatant combatant))
        {
            StatSheet stats = combatant.Stats;
            stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, running.EnemyHealthMultiplier * stronger - 1f, this));
            stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, running.EnemyPowerMultiplier * stronger - 1f, this));
        }

        if (enemy.TryGetComponent(out Damageable damageable))
        {
            attackers.Add(damageable);
            damageable.Died += OnAttackerDied;
        }

        if (enemy.TryGetComponent(out MonsterAI ai))
        {
            ai.PlayerOnly = !running.ThreatensHome;
            ai.Relentless = true;
            ai.KeepAwake = true;
            hunters.Add(ai);
            if (running.ThreatensHome)
            {
                Direct(ai, hunters.Count - 1);
            }
            else
            {
                ai.Engage(playerHealth);
            }
        }
    }

    private void DirectAttackers()
    {
        if (!running.ThreatensHome)
        {
            return;
        }

        for (int i = 0; i < hunters.Count; i++)
        {
            MonsterAI ai = hunters[i];
            if (ai != null && ai.CurrentState != MonsterAI.State.Dead)
            {
                Direct(ai, i);
            }
        }
    }

    // A fight with someone close is left alone, and so is a building or the ward still standing (HomeAssault). Otherwise: on to the next.
    private void Direct(MonsterAI ai, int index)
    {
        if (HomeAssault.StaysOnFight(ai, WardBreachSettings.Current.ChaseLimit))
        {
            return;
        }

        Damageable objective = Objective(ai, index);
        if (objective != null)
        {
            ai.Engage(objective);
        }
    }

    // Every other attacker makes for the ward as long as a way to it is open. The rest, and any walled off, take the nearest
    // building. A wall-breaker goes for the walls first, and for the ward only when nothing is left standing.
    private Damageable Objective(MonsterAI ai, int index)
    {
        Vector3 from = ai.transform.position;
        if (ward != null && index % 2 == 0 && !HomeAssault.BreaksWalls(ai) && CanWalkTo(from, ward.transform.position))
        {
            return ward;
        }

        Damageable nearest = HomeAssault.Objective(ai, from);
        return nearest != null ? nearest : ward;
    }

    private bool CanWalkTo(Vector3 from, Vector3 to)
    {
        // The portal stands on a hole in the NavMesh: anywhere beside it will do.
        return NavMesh.SamplePosition(to, out NavMeshHit near, 5f, NavMesh.AllAreas)
            && NavMesh.CalculatePath(from, near.position, NavMesh.AllAreas, road) && road.status == NavMeshPathStatus.PathComplete;
    }

    private void OnAttackerDied(Damageable victim, DamageInfo killingBlow)
    {
        victim.Died -= OnAttackerDied;
        CheckWon();
    }

    private void CheckWon()
    {
        if (IsActive && !spawning && AttackersLeft == 0)
        {
            Win();
        }
    }

    private void Win()
    {
        WardBreachDefinition breach = running;
        SendAway();
        End();
        GameSession.Home.WinBreach(WardBreachSettings.Current.RaidsBetween);
        GameObject player = Player.Current != null ? Player.Current.gameObject : null;
        MaterialPool pool = MaterialPool.For(player);
        foreach (ResourceAmount reward in breach.Reward)
        {
            pool.Add(reward.Resource, reward.Amount);
        }

        string rewards = breach.Reward.Count > 0 ? $" Reward: {AmountText.Describe(breach.Reward)}." : "";
        // What holding it off has taught the defences (8.7): the upgrades that waited for this many breaches won.
        PlayerCrafting crafting = player != null ? player.GetComponent<PlayerCrafting>() : null;
        List<string> opened = crafting != null ? BuildingUnlocks.OpenedByBreaches(GameSession.Here.BreachesWon, crafting.Buildings) : new List<string>();
        if (opened.Count > 0)
        {
            rewards += $" Now you can raise {string.Join(" and ", opened)}.";
        }

        GameMessages.Post(breach.WonText + rewards, MessageKind.Gain);
        // Everyone at home fought (DefenceSettings › Everyone Fights): a breach won may leave its mark (Defended the Ward).
        foreach (ResidentRecord defender in GameSession.Home.Residents)
        {
            if (!defender.IsAway)
            {
                foreach (MarkDefinition mark in ResidentMarks.AfterEvent(defender, MarkCause.BreachWon, breach.name, () => Random.value))
                {
                    PeopleNews.Tell($"{defender.Resident.DisplayName} held the ward: {mark.DisplayName}.", MessageKind.Gain);
                }
            }
        }

        AudioManager.Play(AudioManager.Sounds?.BreachWon, transform.position);
        QuestDeeds.Raise(GameEventKind.BreachRepelled, breach, player);
        if (breach.WonEvent != null)
        {
            GameEvents.Raise(breach.WonEvent, player);
        }

        if (breach.ThreatensHome)
        {
            ShowRecovery(breach.DisplayName, breach.WonText + rewards);
        }
    }

    private void OnWardFell(Damageable victim, DamageInfo killingBlow)
    {
        WardBreachDefinition breach = running;
        SendAway();
        End();
        WardBreachSettings settings = WardBreachSettings.Current;
        GameSession.Home.LoseBreach(settings.StrainRaids);
        GameMessages.Post(breach.FailedText, MessageKind.Warning);
        AudioManager.Play(AudioManager.Sounds?.BreachLost, transform.position);
        string strained = settings.StrainRaids > 0
            ? $"\n\n<color={UIStyle.Hex(UIStyle.Current.Warning)}>The ward draws in by {settings.StrainRadius:0} m until you have come home from {settings.StrainRaids} more raids.</color>"
            : "";
        ShowRecovery(breach.DisplayName, breach.FailedText + strained + Plundered(breach));
    }

    // A lost breach under the death rules: the band carries off some of the stores, its leader walks the next raid with them, and those
    // who fought come home bruised. Nothing under Gentle.
    private string Plundered(WardBreachDefinition breach)
    {
        DeathRuleSet rules = DeathSettings.Now;
        if (breach == null || HomeFalls.Spared(DeathSettings.Chosen) || HavenVisit.IsVisiting)
        {
            return "";
        }

        if (rules.fightersBruised)
        {
            foreach (ResidentRecord record in GameSession.Home.Residents)
            {
                if (GuardPosts.FightsAs(record, true, DefenceSettings.Current) != null)
                {
                    record.Bruised = true;
                }
            }
        }

        // Who carries it first: nothing is taken without a leader to win it back from.
        MonsterDefinition leader = LeaderOf(breach);
        if (leader == null)
        {
            return "";
        }

        List<ItemAmount> taken = Plunder.Take(GameSession.Stash.Items, rules.plunderShare, rules.plunderStacks, rules.plunderMost);
        if (taken.Count == 0)
        {
            return "";
        }

        // Added to what an earlier band still carries, under its leader.
        PlunderRecord plunder = GameSession.Home.Plunder ?? new PlunderRecord { Leader = leader.Id };
        foreach (ItemAmount item in taken)
        {
            int at = plunder.Items.FindIndex(carried => carried.Item == item.Item);
            if (at >= 0)
            {
                plunder.Items[at] = new ItemAmount(item.Item, plunder.Items[at].Amount + item.Amount);
            }
            else
            {
                plunder.Items.Add(item);
            }
        }

        GameSession.Home.SetPlunder(plunder);
        string what = string.Join(", ", taken.ConvertAll(item => $"{item.Amount} {item.Item.DisplayName}"));
        PlaytestLogger.Note(PlaytestLog.Plundered, "Taken", taken.Sum(item => item.Amount), what);
        MonsterDefinition named = JournalSettings.Current.FindCreature(plunder.Leader);
        MonsterDefinition carrier = named != null ? named : leader;
        return $"\n\n<color={UIStyle.Hex(UIStyle.Current.Danger)}>They carried off {what} from your stores.</color> Their leader, a {carrier.DisplayName}, " +
               "walks your next raid with it: lay it low and bring home what it drops." +
               (rules.fightersBruised ? " Those who fought are bruised until the next dawn." : "");
    }

    // The band's leader: the strongest of the breach's last wave (by what it is worth).
    private static MonsterDefinition LeaderOf(WardBreachDefinition breach)
    {
        MonsterDefinition best = null;
        foreach (GameObject prefab in breach.Roster(breach.WaveCount - 1, GameSession.Here.BreachesWon))
        {
            MonsterAI ai = prefab != null ? prefab.GetComponent<MonsterAI>() : null;
            MonsterDefinition definition = ai != null ? ai.Definition : null;
            if (definition != null && (best == null || definition.Experience > best.Experience))
            {
                best = definition;
            }
        }

        return best;
    }

    private void OnPlayerDied(Damageable victim, DamageInfo killingBlow)
    {
        if (!IsActive)
        {
            return;
        }

        // At a real breach the player wakes at the portal and goes on (HomeRevival); the fight is the ward's to lose. The waking costs it
        // (the death rules: a share of its strength a fall, never below its floor: only the attackers can bring it down).
        if (running.ThreatensHome)
        {
            fallsThisBreach++;
            DeathRuleSet rules = DeathSettings.Now;
            if (ward != null && rules.wardPerFall > 0f && !HomeFalls.Spared(DeathSettings.Chosen))
            {
                float now = ward.HealthFraction;
                float after = Mathf.Max(Mathf.Min(now, rules.wardFloor), now - rules.wardPerFall);
                if (after < now)
                {
                    ward.SetHealthFraction(after);
                    GameMessages.Post($"The portal pulls you back, and the ward pays for it: {Mathf.RoundToInt((now - after) * 100f)}% of its strength.", MessageKind.Warning);
                }
            }

            PlaytestLogger.Note(PlaytestLog.BreachFall, running.name, fallsThisBreach, $"ward {WardHealth:P0}");
            return;
        }

        string failed = running.FailedText;
        firstBreachFellAt = Time.frameCount;
        SendAway();
        End();
        GameMessages.Post(failed, MessageKind.Warning);
    }

    private int firstBreachFellAt = -1;

    /// <summary>True while a fall now is spared by the breach: the first, scripted one teaches (the death rules). Also in the frame its fall
    /// ended it, whichever of the director and HomeRevival heard of the fall first.</summary>
    public bool SparesAFall => (IsActive && running != null && !running.ThreatensHome) || firstBreachFellAt == Time.frameCount;

    // Recovery (GDD §6.7): what the home took, and the way to put it right.
    private void ShowRecovery(string title, string outcome)
    {
        GameObject player = Player.Current != null ? Player.Current.gameObject : null;
        if (player == null || playerHealth == null || ActionMenuWindow.Current == null)
        {
            return;
        }

        // Down when the ward fell (the wake in a breach is long now): shown once they are up, so what was taken isn't missed.
        if (!playerHealth.IsAlive)
        {
            StartCoroutine(ShowRecoveryOnWaking(title, outcome));
            return;
        }

        ActionMenuWindow.Current.Open(() => Recovery(title, outcome, player), player);
    }

    private IEnumerator ShowRecoveryOnWaking(string title, string outcome)
    {
        float giveUpAt = Time.time + 60f;
        while (playerHealth != null && !playerHealth.IsAlive && Time.time < giveUpAt)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
        if (playerHealth != null && playerHealth.IsAlive)
        {
            ShowRecovery(title, outcome);
        }
    }

    private static MenuPage Recovery(string title, string outcome, GameObject player)
    {
        List<Damageable> damaged = BuildingRepairs.Damaged();
        int broken = damaged.FindAll(building => building.IsBroken).Count;
        string state = damaged.Count == 0 ? "No building was harmed."
            : $"{damaged.Count} building{(damaged.Count == 1 ? " was" : "s were")} damaged{(broken > 0 ? $", {broken} of them broken and out of use until mended" : "")}.";
        var page = new MenuPage { Title = title, Body = $"{outcome}\n\n{state}" };
        MenuAction mend = BuildingRepairs.MendAction(player);
        if (mend != null)
        {
            page.Add(mend);
        }

        return page;
    }

    // Those still standing go back into the forest. The fallen keep their death.
    private void SendAway()
    {
        foreach (Damageable attacker in attackers)
        {
            if (attacker != null && attacker.IsAlive)
            {
                Destroy(attacker.gameObject);
            }
        }
    }

    private void End()
    {
        IsActive = false;
        spawning = false;
        running = null;
        Wave = 0;
        StopAllCoroutines();
        ProtectBuildings(false);
        if (ward != null)
        {
            ward.Died -= OnWardFell;
            Destroy(ward.gameObject);
            ward = null;
        }

        foreach (Damageable attacker in attackers)
        {
            if (attacker != null)
            {
                attacker.Died -= OnAttackerDied;
            }
        }

        attackers.Clear();
        hunters.Clear();
        nextLane = 0;
        UpdateMarker();
    }

    // The first breach is forgiving: its enemies attack only the player, so buildings can't be harmed while it lasts.
    private void ProtectBuildings(bool protect)
    {
        if (!protect)
        {
            foreach (Damageable building in shielded)
            {
                if (building != null)
                {
                    building.RemoveInvulnerability(this);
                }
            }

            shielded.Clear();
            return;
        }

        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building.TryGetComponent(out Damageable damageable))
            {
                damageable.AddInvulnerability(this);
                shielded.Add(damageable);
            }
        }
    }

    private void UpdateMarker()
    {
        bool shown = IsActive || IsPending;
        WardBreachDefinition breach = shown ? Definition : null;
        bool useLanes = breach != null && breach.LaneCount > 0;
        if (pathMarker != null)
        {
            pathMarker.SetActive(breach != null && !useLanes);
        }

        if (!useLanes)
        {
            ClearLanes();
            return;
        }

        // Found again whenever what they run round changes within a stay too: the ward raised (it widens) or a building put up
        // where a lane started, not only at the next breach.
        int breachNumber = GameSession.Here.BreachesWon;
        var key = (GameSession.Here.WardLevel, BuildingObject.Active.Count);
        if (lanesFor != breachNumber || key != lanesSeen)
        {
            lanesSeen = key;
            FindLanes(breach, breachNumber);
        }
    }

    private void ClearLanes()
    {
        lanes.Clear();
        lanesFor = -1;
        foreach (GameObject marker in laneMarkers)
        {
            Destroy(marker);
        }

        laneMarkers.Clear();
    }

    // How far from the portal the farthest of the player's buildings stands (on the ground plane).
    private static float OutermostBuilding(Vector3 centre)
    {
        float farthest = 0f;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building == null || building.Definition == null || building.Definition.Faction != Faction.Player || building.Definition.IsPath
                || Construction.IsPlanned(building))
            {
                continue;
            }

            Vector3 offset = building.transform.position - centre;
            offset.y = 0f;
            farthest = Mathf.Max(farthest, offset.magnitude + building.Footprint.HalfSize.magnitude);
        }

        return farthest;
    }

    private void FindLanes(WardBreachDefinition breach, int breachNumber)
    {
        HomePortal tree = HomePortal.Current;
        if (tree == null)
        {
            return;
        }

        ClearLanes();
        lanesFor = breachNumber;
        // Outside what the player has built, not only outside the ward: the whole meadow can be built on (2026-11-12), and an
        // attack must come from beyond the walls and the outermost house (the design review's 8.2, 2026-11-14).
        float distance = Mathf.Max(tree.WardRadius, OutermostBuilding(tree.transform.position)) + WardBreachSettings.Current.LaneDistance;
        lanes.AddRange(BreachLanes.Find(tree.transform.position, distance, breach.LaneCount, breachNumber * 7919 + 1));
        if (pathMarker == null)
        {
            return;
        }

        foreach (BreachLane lane in lanes)
        {
            GameObject marker = Instantiate(pathMarker, transform);
            marker.name = $"Breach Lane {lane.Direction}";
            marker.SetActive(true);
            Draw(marker.GetComponent<LineRenderer>(), lane.Road);
            laneMarkers.Add(marker);
        }
    }

    // The road's corners can be far apart: stepped along it and laid on the ground, so the line doesn't cut through a hill.
    private static void Draw(LineRenderer line, Vector3[] road)
    {
        if (line == null || road.Length < 2)
        {
            return;
        }

        var points = new List<Vector3>();
        Terrain terrain = Terrain.activeTerrain;
        for (int i = 0; i < road.Length - 1; i++)
        {
            float length = Vector3.Distance(road[i], road[i + 1]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / MarkerStep));
            for (int step = 0; step < steps; step++)
            {
                points.Add(OnGround(Vector3.Lerp(road[i], road[i + 1], step / (float)steps), terrain));
            }
        }

        points.Add(OnGround(road[road.Length - 1], terrain));
        line.useWorldSpace = true;
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }

    private static Vector3 OnGround(Vector3 point, Terrain terrain)
    {
        if (terrain != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        return point + Vector3.up * MarkerHeight;
    }
}
