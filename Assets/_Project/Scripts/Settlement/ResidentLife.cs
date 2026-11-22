using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A resident's day (GDD §15): they walk to their work and get on with it, eat, sit at the fire of an evening, sleep in their
/// house (walking in at night, its window lit while they are in; out of the door at dawn) or at their own spot by the portal
/// when no house is theirs (HavenHomes), and run for the portal when the haven is attacked. They stop to talk when spoken to and carry on after; asleep inside, a knock at the house's door
/// (HouseDoor) brings them out to talk, and they go back in after. What they do when is
/// ResidentRoutine and Settlement Settings › Daily Life; the gesture for a job is on its JobDefinition. Added by ResidentNpc. Where
/// the ground has no NavMesh they go straight to their place, as there is no way to walk there.
/// <para>The builders (HomeLifeSettings › Builders: the Carpenter and Mason) hammer the paid-for scaffolds at home in their working hours
/// (the Keeper's view, Docs/ColonyModeResearch_2026-11-21.md: walls and buildings rise while the player is home too, not only while
/// away), the nearest with room for one more, at <see cref="BuildRate"/>; work is added only in the game that keeps the Haven. They keep
/// out of deep water (DeepWater), and one who is stuck finds a fresh way before being put where they were going, and isn't put there in
/// plain sight of the Keeper's view (<see cref="Warps"/> counts those that were).</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ResidentNpc))]
public sealed class ResidentLife : MonoBehaviour, IHauler
{
    private const float ThinkSeconds = 0.5f;
    private const float ArriveDistance = 0.7f;
    private const float StuckSeconds = 4f;
    private const float TalkTurnSpeed = 240f;
    private const float RingRadius = 2.4f;
    private const float WorkmateGap = 1.2f;
    private const float GuardRadius = 6f;
    // How many builders hammer at one scaffold at once; the next goes to another.
    private const int BuildersPerSite = 3;
    private const float BuildSpotGap = 0.8f;
    // Woken by a knock: how long they stand at the door for the player, and how long they linger after a talk.
    private const float AwakeSeconds = 12f;
    private const float AfterTalkSeconds = 2.5f;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private ResidentNpc npc;
    private Talkable talkable;
    private NavMeshAgent agent;
    private Animator animator;
    private CharacterEmotes emotes;
    private DayNightCycle clock;
    private Vector3 home;
    private Quaternion homeFacing;
    private Vector3 place;
    private Quaternion facing;
    private ResidentActivity activity;
    private bool started;
    private bool arrived;
    private float nextThinkAt;
    private float stuckSince = -1f;
    private float nextGestureAt;
    private float ringAngle;
    private float workShift;
    private GameObject tool;
    private ItemDefinition toolItem;
    private float workSoundAt = float.PositiveInfinity;
    private ResidentDefender defender;
    // The house they are asleep in (hidden inside, its window lit), and what was switched off to hide them.
    private BuildingObject insideOf;
    // The scaffold a builder is hammering at home, or null.
    private Construction building;
    private bool repathed;
    private Renderer body;

    // A trip to a chest and back for their work (WorkerProduction): where they are on it, what they carry, a cart taken along.
    private enum Trip
    {
        None,
        /// <summary>To a free cart near the workplace first, to take its shafts beside it (as the player does).</summary>
        ToCart,
        ToChest,
        ToStation,
    }

    private const float TripLookSeconds = 1.5f;
    private const float CarriedScale = 0.8f;
    private Trip trip;
    private StashChest tripChest;
    private bool carryingBack;
    private readonly List<ItemAmount> carrying = new List<ItemAmount>();
    private Haulable cart;
    private Haulable cartWanted;
    private Collider[] ownColliders;
    private float nextTripLook;
    private Vector3 workPlace;
    private Quaternion workFacingThere = Quaternion.identity;

    // Felling a ring (RingWork): the ring, the tree they are at (claimed), the chopping left, the weight of the logs on their way to the
    // chests, and when their bench was last looked at.
    private const float BenchLookSeconds = 2f;
    private RingOrder fellingRing;
    private TerrainTreeFelling fellOn;
    private int fellIndex = -1;
    private Vector3 fellFoot;
    private float fellHeight;
    private float chopLeft;
    private bool logsToChest;
    private float carriedWeight;
    private float benchLookAt;
    private bool benchBusy;
    private BuildingObject sleepsIn;
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Collider> hiddenColliders = new List<Collider>();
    private readonly List<Behaviour> hiddenParts = new List<Behaviour>();
    // Woken by a knock: at the door until then, facing the player.
    private float awakeUntil = -1f;

    /// <summary>What they are about right now.</summary>
    public ResidentActivity Activity => activity;

    /// <summary>True once they are at the place for it and getting on with it.</summary>
    public bool HasArrived => arrived;

    /// <summary>Asleep inside their house: not seen, its window lit. Spoken to only by knocking at its door (HouseDoor).</summary>
    public bool IsIndoors => insideOf != null;

    /// <summary>The house they are asleep in, or null.</summary>
    public BuildingObject Inside => insideOf;

    /// <summary>In bed, in a house or at their spot by the portal: no lantern then.</summary>
    public bool IsResting => activity == ResidentActivity.Sleep && arrived;

    private void OnEnable()
    {
        Living.Add(this);
        HavenHomes.Changed += Refresh;
        HavenHomes.NoteIndoors(insideOf, true);
    }

    // Switched off (the scene going) while asleep inside: the house no longer counts them, and they stay as they are.
    private void OnDisable()
    {
        Living.Remove(this);
        if (npc != null && npc.Resident != null && ByWho.TryGetValue(npc.Resident, out ResidentLife known) && known == this)
        {
            ByWho.Remove(npc.Resident);
        }

        building = null;
        // Going with the scene: the trip ends where it stands, no walk back and no tool made while the scene closes.
        ClearTrip();
        StopFelling(true);
        fellingRing = null;
        HavenHomes.Changed -= Refresh;
        HavenHomes.NoteIndoors(insideOf, false);
    }

    private void Awake()
    {
        WalkerFootprints.AddTo(gameObject);
        ColdBreath.AddTo(gameObject);
        npc = GetComponent<ResidentNpc>();
        talkable = GetComponent<Talkable>();
    }

    /// <summary>Their own spot, where they sleep, and which side of the fire is theirs.</summary>
    public void Settle(Vector3 homePosition, Quaternion homeRotation, int seat)
    {
        home = homePosition;
        homeFacing = homeRotation;
        // The golden angle, so any number of them sit apart.
        ringAngle = seat * 137.5f;
        // Those who share a workplace stand side by side along it.
        workShift = (seat % 3 - 1) * WorkmateGap;
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null && !animator.TryGetComponent(out emotes))
        {
            emotes = animator.gameObject.AddComponent<CharacterEmotes>();
        }

        clock = FindAnyObjectByType<DayNightCycle>();
        SettlementSettings settings = SettlementSettings.Current;
        body = GetComponentInChildren<SkinnedMeshRenderer>();
        // People don't swim: deep water is left out of their way (DeepWater, as for creatures since the 2026-11-21 playtest).
        int walkable = DeepWater.MaskFor(NavMesh.AllAreas, false);
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, walkable))
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.areaMask = walkable;
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = settings.ResidentWalkSpeed;
            agent.angularSpeed = 300f;
            agent.acceleration = 6f;
            agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.Warp(hit.position);
        }

        started = true;
        // On arriving in the scene everyone is already where the hour has them: a whole haven doesn't set off at once on every load.
        Decide(true);
    }

    /// <summary>Thinks again now (a workplace was built, or a job given).</summary>
    public void Refresh()
    {
        if (started)
        {
            Decide(false, true);
        }
    }

    private void Update()
    {
        if (!started)
        {
            return;
        }

        if (KeepsGuard())
        {
            return;
        }

        if (insideOf != null)
        {
            // Asleep in the house: only the hour (or danger) brings them out.
            if (Time.time >= nextThinkAt)
            {
                nextThinkAt = Time.time + ThinkSeconds;
                Decide(false);
            }

            return;
        }

        if (talkable != null && talkable.IsTalking)
        {
            if (awakeUntil >= 0f)
            {
                awakeUntil = Time.time + AfterTalkSeconds;
            }

            Listen();
            return;
        }

        // Woken by a knock: at the door for the player a little while, then back to bed (or on to the day, if it came).
        if (awakeUntil >= 0f)
        {
            if (Time.time < awakeUntil)
            {
                arrived = true;
                Listen();
                return;
            }

            awakeUntil = -1f;
            Decide(false, true);
        }

        if (agent != null && agent.isStopped)
        {
            agent.isStopped = false;
            arrived = false;
        }

        if (Time.time >= nextThinkAt)
        {
            nextThinkAt = Time.time + ThinkSeconds;
            Decide(false);
            // On a drawn path they walk faster, as the player does (HavenPaths).
            if (agent != null && agent.enabled && !arrived)
            {
                SettlementSettings pace = SettlementSettings.Current;
                agent.speed = (activity == ResidentActivity.Shelter ? pace.ResidentRunSpeed : pace.ResidentWalkSpeed) * HavenPaths.SpeedAt(transform.position);
            }
        }

        if (arrived)
        {
            GetOnWithIt();
        }
        else
        {
            GetThere();
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f, 0.15f, Time.deltaTime);
        }
    }

    // A breach is on and their job defends the Haven: they are on guard until it is over, and the guard is who the player sees.
    private bool KeepsGuard()
    {
        bool breach = WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive;
        CompanionDefinition how = breach ? npc.Job?.DefendsAs : null;
        defender ??= new ResidentDefender(transform, animator, agent);
        if (how != null && !defender.IsOnGuard)
        {
            ComeOut();
            if (emotes != null && emotes.IsPlaying)
            {
                emotes.Stop();
            }

            defender.Stand(npc.Resident, how, GuardPost());
        }
        else if (how == null && defender.IsOnGuard)
        {
            defender.StandDown();
            Decide(false, true);
        }

        return defender.IsOnGuard;
    }

    // Up on the Watch Post if that is where their job is; otherwise a ring out from the portal, between it and what comes.
    private Vector3 GuardPost()
    {
        BuildingObject post = npc.Workplace;
        if (post != null && post.GetComponent<WatchPostArcher>() != null && npc.TryGetWorkplace(out Vector3 spot, out _))
        {
            return spot;
        }

        Vector3 round = Quaternion.Euler(0f, ringAngle, 0f) * Vector3.forward;
        return (HomePortal.Current != null ? HomePortal.Current.transform.position : home) + round * GuardRadius;
    }

    /// <summary>The guard they have become in a breach, or null.</summary>
    public Companion Guard => defender?.Guard;

    private void Listen()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        if (emotes != null && emotes.IsPlaying)
        {
            emotes.Stop();
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, 0f, 0.15f, Time.deltaTime);
        }

        Player player = Player.Current;
        if (player != null)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), TalkTurnSpeed * Time.deltaTime);
            }
        }
    }

    private void Decide(bool atOnce, bool again = false)
    {
        SettlementSettings settings = SettlementSettings.Current;
        bool hasWorkplace = npc.TryGetWorkplace(out Vector3 workSpot, out Quaternion workFacing);
        float hour = clock != null ? clock.Hour : 12f;
        bool danger = InDanger(settings);
        // Asleep in their house, a creature prowling past is no reason to run out into the dark: only a breach brings them out.
        if (danger && insideOf != null && (WardBreachDirector.Current == null || !WardBreachDirector.Current.IsActive))
        {
            danger = false;
        }
        ResidentActivity next = ResidentRoutine.InTheCold(ResidentRoutine.Decide(hour, settings.ResidentHours, hasWorkplace, danger), hour, PlayerWarmth.ColdNow());
        // A builder with a scaffold waiting builds in working hours rather than standing at their workplace, going out or, with no
        // workplace built yet, sitting at the fire.
        bool workTime = ResidentRoutine.InTheCold(ResidentRoutine.Decide(hour, settings.ResidentHours, true, danger), hour, PlayerWarmth.ColdNow())
            == ResidentActivity.Work;
        Construction scaffold = workTime && Festivals.Now == null ? ScaffoldToBuild() : null;
        if (scaffold != null)
        {
            next = ResidentActivity.Work;
        }

        // A woodcutter with a ring drawn fells it in working hours, after any scaffold (RingWork).
        RingOrder ring = workTime && scaffold == null && Festivals.Now == null ? RingToFell() : null;
        if (ring != null)
        {
            next = ResidentActivity.Work;
            scaffold = null;
        }

        bool freeToGo = scaffold == null && ring == null && (next == ResidentActivity.Work || (next == ResidentActivity.Fire && !hasWorkplace && hour >= settings.ResidentHours.WorkFrom));
        if (freeToGo && GoingOut(hour, out Vector3 outTo, out Quaternion outFacing))
        {
            next = ResidentActivity.Outing;
            workSpot = outTo;
            workFacing = outFacing;
        }

        if (Festivals.Now != null && !danger)
        {
            next = ResidentActivity.Fire; // everyone to the festival's bonfire
        }
        if (next != ResidentActivity.Work)
        {
            scaffold = null;
            ring = null;
        }

        if (next == activity && scaffold == building && ring == fellingRing && started && !atOnce && !again)
        {
            return;
        }

        building = scaffold;
        if (ring != fellingRing || ring == null)
        {
            StopFelling(false);
            fellingRing = ring;
        }
        repathed = false;
        // A new plan ends any trip: what they carried never left the chest or the station.
        trip = Trip.None;
        tripChest = null;
        cartWanted = null;
        carrying.Clear();
        carryingBack = false;
        LetGoOfCart();

        // Their house, if one is theirs: they sleep inside it, going in at its door.
        bool housed = npc.TryGetHome(out Vector3 door, out Quaternion outward, out BuildingObject house);
        if (insideOf != null && (next != ResidentActivity.Sleep || insideOf != house))
        {
            ComeOut();
        }

        activity = next;
        sleepsIn = next == ResidentActivity.Sleep && housed ? house : null;
        switch (next)
        {
            case ResidentActivity.Work when building != null:
                place = BuildSpot(building, out facing);
                break;
            case ResidentActivity.Work when fellingRing != null:
                place = fellIndex >= 0 ? TreeSpot(out facing) : fellingRing.Centre;
                facing = fellIndex >= 0 ? facing : transform.rotation;
                break;
            case ResidentActivity.Work:
                place = workSpot + workFacing * Vector3.right * workShift;
                facing = workFacing;
                workPlace = place;
                workFacingThere = facing;
                break;
            case ResidentActivity.Sleep:
                place = housed ? door : home;
                facing = housed ? outward * Quaternion.Euler(0f, 180f, 0f) : homeFacing;
                if (insideOf != null)
                {
                    // Already in bed in this house: nothing to walk to.
                    return;
                }

                break;
            case ResidentActivity.Outing:
                place = workSpot;
                facing = workFacing;
                break;
            default:
                Vector3 centre = Gathering(next, settings);
                Vector3 round = Quaternion.Euler(0f, ringAngle, 0f) * Vector3.forward;
                place = centre + round * RingRadius;
                facing = Quaternion.LookRotation(-round);
                break;
        }

        if (agent != null)
        {
            agent.speed = next == ResidentActivity.Shelter ? settings.ResidentRunSpeed : settings.ResidentWalkSpeed;
        }

        if (emotes != null && emotes.IsPlaying)
        {
            emotes.Stop();
        }

        arrived = false;
        stuckSince = -1f;
        workSoundAt = float.PositiveInfinity;
        ShowTool(null, 1f);
        if (atOnce || agent == null || !agent.isOnNavMesh)
        {
            PutThere();
        }
        else if (NavMesh.SamplePosition(place, out NavMeshHit hit, 3f, agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
    }

    // Where they gather: the fire nearest their home if there is one, else the portal. In danger, always the portal: it is the
    // heart of the place.
    private Vector3 Gathering(ResidentActivity next, SettlementSettings settings)
    {
        if (Festivals.Now != null && next != ResidentActivity.Shelter)
        {
            return Festivals.Place;
        }

        // A Feast Green's fire first (Yards), else the fire nearest home.
        Vector3 from = npc.TryGetHome(out Vector3 door, out _, out _) ? door : home;
        BuildingObject fire = next != ResidentActivity.Shelter && settings.ResidentGatheringPlace != null
            ? Yards.GatheringFire(from, settings.ResidentGatheringPlace) ?? BuildingObject.Nearest(settings.ResidentGatheringPlace, from)
              ?? BuildingObject.Find(settings.ResidentGatheringPlace) : null;
        if (fire != null)
        {
            return fire.transform.position;
        }

        return HomePortal.Current != null ? HomePortal.Current.transform.position : home;
    }

    // Places someone may walk out to on a day off work: the waystones and the lakeshore, near enough home and away
    // from where creatures gather. Found once per scene.
    private static readonly List<(Vector3 at, Vector3 lookAt)> OutingSpots = new List<(Vector3, Vector3)>();

    // Everyone living their day here, to share out the scaffolds; and by who they are (Of, asked by every station each look).
    private static readonly List<ResidentLife> Living = new List<ResidentLife>();
    private static readonly Dictionary<ResidentDefinition, ResidentLife> ByWho = new Dictionary<ResidentDefinition, ResidentLife>();

    /// <summary>How many times someone stuck was put where they were going, this session (probes: few is good).</summary>
    public static int Warps { get; private set; }

    /// <summary>True while hammering (or walking to) a scaffold at home.</summary>
    public bool IsBuilding => building != null && activity == ResidentActivity.Work;


    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        OutingSpots.Clear();
        outingSpotsScene = default;
        Living.Clear();
        ByWho.Clear();
        Warps = 0;
    }
    private static UnityEngine.SceneManagement.Scene outingSpotsScene;

    private bool GoingOut(float hour, out Vector3 to, out Quaternion facingThere)
    {
        to = default;
        facingThere = Quaternion.identity;
        string who = npc.Resident != null ? npc.Resident.DisplayName : name;
        int day = GameSession.Here.DaysPassed;
        if (!ResidentRoutine.Outing(who, day, out float start, out float hours) || hour < start || hour >= start + hours)
        {
            return false;
        }

        FindOutingSpots();
        if (OutingSpots.Count == 0)
        {
            return false;
        }

        (Vector3 at, Vector3 lookAt) = OutingSpots[Mathf.Abs((who.GetHashCode() + day) % OutingSpots.Count)];
        to = at;
        Vector3 look = lookAt - at;
        look.y = 0f;
        facingThere = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
        return true;
    }

    private static void FindOutingSpots()
    {
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (outingSpotsScene == scene)
        {
            return;
        }

        outingSpotsScene = scene;
        OutingSpots.Clear();
        Vector3 home = HomePortal.Current != null ? HomePortal.Current.transform.position : Vector3.zero;
        MobSpawnZone[] lairs = FindObjectsByType<MobSpawnZone>();
        void Add(Vector3 near, Vector3 lookAt)
        {
            if ((near - home).sqrMagnitude > 190f * 190f || !NavMesh.SamplePosition(near, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                return;
            }

            foreach (MobSpawnZone lair in lairs)
            {
                if (lair != null && (lair.transform.position - hit.position).sqrMagnitude < 45f * 45f)
                {
                    return;
                }
            }

            OutingSpots.Add((hit.position, lookAt));
        }

        foreach (OfferingStone stone in FindObjectsByType<OfferingStone>())
        {
            Vector3 stoneAt = stone.transform.position;
            Vector3 fromHome = (home - stoneAt).normalized;
            Add(stoneAt + fromHome * 2.2f, stoneAt);
        }

        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water != null && water.IsStill)
            {
                Vector3 shore = water.NearestPoint(home);
                Add(shore + (home - shore).normalized * 2.5f, shore);
            }
        }
    }

    private bool InDanger(SettlementSettings settings)
    {
        if (WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive)
        {
            return true;
        }

        float reach = settings.ResidentDangerRadius * settings.ResidentDangerRadius;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster != null && monster.CurrentState != MonsterAI.State.Dead && monster.IsHuntingPlayerSide
                && (monster.transform.position - transform.position).sqrMagnitude <= reach)
            {
                return true;
            }
        }

        return false;
    }

    private void GetThere()
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            PutThere();
            return;
        }

        if (agent.pathPending)
        {
            return;
        }

        if (!agent.hasPath || agent.remainingDistance <= ArriveDistance)
        {
            Reached();
            return;
        }

        // Boxed in by something built since, or no way through: they find their own way there rather than stand against a wall all day.
        bool getting = agent.velocity.sqrMagnitude > 0.04f && agent.pathStatus == NavMeshPathStatus.PathComplete;
        if (getting)
        {
            stuckSince = -1f;
            return;
        }

        if (stuckSince < 0f)
        {
            stuckSince = Time.time;
        }
        else if (Time.time - stuckSince >= StuckSeconds)
        {
            // First a fresh way there (something may have been built across the old one); only then put there, and not in plain
            // sight of the Keeper's view, where someone blinking across the Haven shows (Docs/ColonyModeResearch_2026-11-21.md 3.2).
            if (!repathed && agent.isOnNavMesh && NavMesh.SamplePosition(place, out NavMeshHit nearer, 6f, agent.areaMask))
            {
                repathed = true;
                stuckSince = Time.time;
                agent.SetDestination(nearer.position);
                return;
            }

            if (KeeperView.IsOpen && body != null && body.isVisible && Time.time - stuckSince < StuckSeconds * 4f)
            {
                return;
            }

            Warps++;
            LetGoOfCart();
            PutThere();
        }
    }

    private void PutThere()
    {
        if (agent != null && agent.isOnNavMesh && NavMesh.SamplePosition(place, out NavMeshHit hit, 3f, agent.areaMask))
        {
            agent.Warp(hit.position);
        }
        else if (agent == null)
        {
            transform.position = place;
        }

        Reached();
    }

    private void Reached()
    {
        arrived = true;
        if (agent != null && agent.isOnNavMesh && agent.hasPath)
        {
            agent.ResetPath();
        }

        if (trip != Trip.None)
        {
            ArrivedOnTrip();
            return;
        }

        if (activity == ResidentActivity.Sleep && sleepsIn != null)
        {
            GoIn(sleepsIn);
            return;
        }

        nextGestureAt = 0f;
        JobDefinition job = activity == ResidentActivity.Work ? npc.Job : null;
        if (job != null)
        {
            ShowTool(job.WorkTool, job.WorkToolScale);
        }
    }

    // Comes out when they get to work and goes away when they stop.
    private void ShowTool(ItemDefinition item, float scale)
    {
        if (item == toolItem)
        {
            return;
        }

        if (tool != null)
        {
            Destroy(tool);
        }

        toolItem = item;
        tool = null;
        Transform hand = item != null && animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null)
        {
            return;
        }

        tool = HeldItemView.CreateModel(item, hand);
        if (tool != null)
        {
            Pose grip = SettlementSettings.Current.ResidentToolGrip;
            tool.transform.SetLocalPositionAndRotation(grip.position + grip.rotation * item.HeldPose.position, grip.rotation * item.HeldPose.rotation);
            tool.transform.localScale *= scale;
        }
    }

    private void MakeWorkSound(JobDefinition job)
    {
        if (Time.time < workSoundAt)
        {
            return;
        }

        bool repeats = job.WorkGesture != null && job.WorkGesture.IsHeld;
        workSoundAt = repeats ? Time.time + Random.Range(job.WorkSoundEvery.x, job.WorkSoundEvery.y) : float.PositiveInfinity;
        AudioManager.Play(job.WorkSound, transform.position + Vector3.up);
    }

    private void GetOnWithIt()
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, TalkTurnSpeed * Time.deltaTime);
        if (IsBuilding && !Hammer())
        {
            return;
        }

        if (activity == ResidentActivity.Work && building == null && fellingRing != null)
        {
            if (!FellStep())
            {
                return;
            }
        }
        else if (activity == ResidentActivity.Work && building == null && Time.time >= nextTripLook && MaybeStartTrip())
        {
            return;
        }

        JobDefinition job = activity == ResidentActivity.Work ? npc.Job : null;
        if (job != null && job.WorkSound != null)
        {
            MakeWorkSound(job);
        }

        if (emotes == null || Time.time < nextGestureAt)
        {
            return;
        }

        SettlementSettings settings = SettlementSettings.Current;
        EmoteDefinition gesture = activity switch
        {
            ResidentActivity.Work => npc.WorkGesture,
            ResidentActivity.Meal => settings.ResidentMealGesture,
            ResidentActivity.Sleep => settings.ResidentSleepGesture,
            ResidentActivity.Fire => Pick(settings.ResidentFireGestures),
            ResidentActivity.Outing => Pick(settings.ResidentFireGestures),
            _ => null,
        };

        if (gesture == null)
        {
            nextGestureAt = float.PositiveInfinity;
            return;
        }

        // A pose lasts until something changes; a gesture comes round again.
        if (gesture.IsHeld)
        {
            if (emotes.Current != gesture)
            {
                emotes.Play(gesture);
                if (job != null && float.IsPositiveInfinity(workSoundAt))
                {
                    workSoundAt = Time.time + Random.Range(0.3f, job.WorkSoundEvery.y);
                }
            }

            nextGestureAt = activity == ResidentActivity.Fire ? Time.time + Random.Range(18f, 40f) : Time.time + 5f;
        }
        else if (!emotes.IsPlaying)
        {
            // Work keeps a rhythm, with a breath now and then; at the fire a gesture is only now and then.
            emotes.Play(gesture);
            if (job != null)
            {
                workSoundAt = Time.time + job.WorkSoundDelay;
            }

            nextGestureAt = activity != ResidentActivity.Work ? Time.time + Random.Range(18f, 40f)
                : Random.value < 0.2f ? Time.time + Random.Range(2f, 4f) : Time.time + Random.Range(0.1f, 0.5f);
        }
    }

    // ----- Fetching for their work (WorkerProduction; the user, 2026-11-21: "They have to get resources from storage") -----

    /// <summary>The living body of <paramref name="who"/> in this scene, or null.</summary>
    public static ResidentLife Of(ResidentDefinition who)
    {
        if (who == null)
        {
            return null;
        }

        if (ByWho.TryGetValue(who, out ResidentLife known) && known != null && known.npc != null && known.npc.Resident == who)
        {
            return known;
        }

        foreach (ResidentLife life in Living)
        {
            if (life != null && life.npc != null && life.npc.Resident == who)
            {
                ByWho[who] = life;
                return life;
            }
        }

        return null;
    }

    /// <summary>True while on a trip to a chest and back for their work.</summary>
    public bool IsHauling => trip != Trip.None;

    /// <summary>True while taking finished work to the chests (the first half of a trip that has it).</summary>
    public bool IsTakingWorkBack => trip == Trip.ToChest && carryingBack;

    /// <summary>
    /// Whether they fetch their work's materials on foot here (WorkerProduction.Hauling.OnFoot): in the game that keeps the Haven, on the
    /// NavMesh, with a workplace and a chest standing within WorkerSettings › Chest Reach of it. Otherwise it goes straight from the stash.
    /// </summary>
    public bool FetchesOnFoot
    {
        get
        {
            // Not whether they are on the NavMesh now: asleep indoors (agent off) they still fetch in the morning, and the work they
            // were fetched for mustn't switch to the stash meanwhile (that let crafts start short).
            if (!started || HavenVisit.IsVisiting || agent == null || npc == null || npc.Workplace == null)
            {
                return false;
            }

            return StashChest.Nearest(npc.Workplace.transform.position, WorkerSettings.Current.ChestReach) != null;
        }
    }

    /// <summary>True while at work at <paramref name="workplace"/>: there, not fetching, building or elsewhere (CraftingStation.WorkerIsHere).</summary>
    public bool WorksAt(BuildingObject workplace) =>
        workplace != null && activity == ResidentActivity.Work && building == null && fellingRing == null && trip == Trip.None && arrived
        && npc.Workplace == workplace;

    /// <summary>True while felling a ring (walking to a tree, chopping, or taking its logs to the chests).</summary>
    public bool IsFelling => fellingRing != null && activity == ResidentActivity.Work && building == null;

    // What they can carry now: their hands, a cart they took, a horse at their station.
    private CarryLimits Limits(StationQueue queue)
    {
        WorkerSettings settings = WorkerSettings.Current;
        float cartCapacity = cart != null && cart.TryGetComponent(out Transport carried) && carried.Definition != null ? carried.Definition.Capacity : 0f;
        return WorkerCarry.WithHelp(WorkerCarry.ByHand(npc.Record, settings), cartCapacity, queue != null && queue.HorseHelps, settings);
    }

    // At their station with nothing else to do: a trip when finished work waits to go back (the station full or done), or the next order
    // is short of what was fetched. True when they set off.
    private bool MaybeStartTrip()
    {
        nextTripLook = Time.time + TripLookSeconds;
        ResidentRecord record = npc.Record;
        if (HavenVisit.IsVisiting || record == null || record.WorkPaused || npc.Workplace == null || !FetchesOnFoot)
        {
            return false;
        }

        StationQueue queue = WorkerProduction.QueueOf(GameSession.Here, record);
        StashChest chest = queue != null ? StashChest.Nearest(npc.Workplace.transform.position, WorkerSettings.Current.ChestReach) : null;
        if (chest == null)
        {
            return false;
        }

        bool back = WorkerProduction.HasWorkToTakeBack(queue) && (queue.IsIdle || queue.Blocked) && CanTakeBack(queue);
        bool fetch = queue.IsIdle && WorkerProduction.PlanFetch(record.Job, queue, GameSession.Stash, Limits(queue)).Count > 0;
        if (!back && !fetch)
        {
            return false;
        }

        queue.TripMetres = Vector3.Distance(npc.Workplace.transform.position, chest.transform.position);
        tripChest = chest;
        carryingBack = back;
        carrying.Clear();
        // A free cart near the workplace goes along (WorkerSettings › Cart Reach): fetched first, its shafts taken beside it.
        cartWanted = cart == null ? Haulable.NearestFree(npc.Workplace.transform.position, WorkerSettings.Current.CartReach) : null;
        if (cartWanted != null)
        {
            trip = Trip.ToCart;
            GoTo(SpotBeside(cartWanted.transform, out facing));
            return true;
        }

        trip = Trip.ToChest;
        ShowTool(back && queue.Output is ItemDefinition made ? made : null, CarriedScale);
        GoTo(SpotBeside(chest.transform, out facing));
        return true;
    }


    private void LetGoOfCart()
    {
        Haulable held = cart;
        cart = null;
        if (held != null && held.IsHeldBy(gameObject))
        {
            held.LetGo();
        }
    }

    // At the chest: finished work goes into it, and what the next orders want is picked out (it leaves the stash only on arriving at the
    // station, so nothing is lost to a save or a change of plan on the way). At the station: what was picked out is put by it.
    private void ArrivedOnTrip()
    {
        ResidentRecord record = npc.Record;
        StationQueue queue = record != null ? WorkerProduction.QueueOf(GameSession.Here, record) : null;
        if (queue == null || HavenVisit.IsVisiting)
        {
            EndTrip();
            return;
        }

        if (trip == Trip.ToCart)
        {
            // Its shafts taken beside it, if nobody took it meanwhile: its capacity is theirs for the trip (WorkerSettings › Cart Slots).
            if (cartWanted != null && !cartWanted.InUse)
            {
                cart = cartWanted;
                cart.Take(this);
            }

            cartWanted = null;
            trip = Trip.ToChest;
            GoTo(tripChest != null ? SpotBeside(tripChest.transform, out facing) : workPlace);
            return;
        }

        if (trip == Trip.ToChest)
        {
            CarryLimits limits = Limits(queue);
            if (carryingBack)
            {
                WorkerProduction.TakeBack(record, queue, GameSession.Stash, limits);
            }

            carrying.Clear();
            carrying.AddRange(WorkerProduction.PlanFetch(record.Job, queue, GameSession.Stash, limits));
            carryingBack = false;
            trip = Trip.ToStation;
            ShowTool(cart == null && carrying.Count > 0 ? carrying[0].Item : null, CarriedScale);
            GoTo(workPlace);
            facing = workFacingThere;
            return;
        }

        float moved = WorkerProduction.Deliver(record, queue, GameSession.Stash, carrying);
        // A trip that brought nothing (the player took it meanwhile): a while before the next.
        if (moved <= 0f)
        {
            nextTripLook = Time.time + 20f;
        }

        EndTrip();
    }

    // Whether any of the finished work can go back: they can lift one, and the chests have room (else they'd pace there and back).
    private bool CanTakeBack(StationQueue queue)
    {
        if (!(queue.Output is ItemDefinition item))
        {
            return false;
        }

        return WorkerCarry.CanTake(item, queue.OutputAmount, Limits(queue)) > 0 && GameSession.Stash.Items.SpaceFor(item) > 0;
    }

    // The trip's state cleared, the cart let go, nothing else (the scene going).
    private void ClearTrip()
    {
        trip = Trip.None;
        tripChest = null;
        cartWanted = null;
        carrying.Clear();
        carryingBack = false;
        LetGoOfCart();
    }

    // Done, or the day moved on (a meal, the night, danger): nothing carried is lost, as nothing left its place before arriving.
    private void EndTrip()
    {
        bool was = trip != Trip.None;
        trip = Trip.None;
        tripChest = null;
        cartWanted = null;
        carrying.Clear();
        carryingBack = false;
        LetGoOfCart();
        if (was)
        {
            ShowTool(null, 1f);
            if (activity == ResidentActivity.Work && building == null)
            {
                GoTo(workPlace);
                facing = workFacingThere;
            }
        }
    }

    private void GoTo(Vector3 point)
    {
        place = point;
        arrived = false;
        stuckSince = -1f;
        repathed = false;
        if (agent != null && agent.isOnNavMesh && NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            PutThere();
        }
    }

    // Beside something on the side nearest them (out of its collider), and the way to face it.
    private Vector3 SpotBeside(Transform thing, out Quaternion facingIt)
    {
        Vector3 centre = thing.position;
        Collider box = thing.GetComponentInChildren<Collider>();
        Vector3 edge = box != null && box.enabled && !box.isTrigger ? box.ClosestPoint(transform.position) : centre;
        Vector3 outward = edge - centre;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f)
        {
            outward = transform.position - centre;
            outward.y = 0f;
        }

        outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
        facingIt = Quaternion.LookRotation(-outward);
        return edge + outward * BuildSpotGap;
    }

    // ----- Pulling a cart (Haulable): a resident takes the shafts as the player does -----

    Vector3 IHauler.Velocity => agent != null ? agent.velocity : Vector3.zero;

    float IHauler.PullScale => 1f;

    float IHauler.ExtraReach => 0.2f;

    IEnumerable<Collider> IHauler.HaulerColliders => ownColliders ??= GetComponentsInChildren<Collider>(true);

    void IHauler.Began(Transport transport)
    {
    }

    void IHauler.Ended()
    {
        cart = null;
    }

    // ----- Felling a ring (RingWork; Docs/ColonyModeResearch_2026-11-21.md §4.7) -----

    // The ring to fell now, for a woodcutter: the one under way while its logs are in their hands; else, while their own bench has
    // nothing to work on, the first ring with a grown tree nobody else is at. Never in a friend's Haven (theirs fell it).
    private RingOrder RingToFell()
    {
        if (HavenVisit.IsVisiting || !RingWork.Fells(npc.Record, WorkerSettings.Current))
        {
            return null;
        }

        IReadOnlyList<RingOrder> rings = GameSession.Here.Rings;
        if (fellingRing != null && logsToChest && Contains(rings, fellingRing))
        {
            return fellingRing;
        }

        if (BenchHasWork())
        {
            return null;
        }

        if (fellingRing != null && fellIndex >= 0 && Contains(rings, fellingRing))
        {
            return fellingRing;
        }

        foreach (RingOrder ring in rings)
        {
            if (RingWork.NextTree(ring, out _, out _))
            {
                return ring;
            }
        }

        return null;
    }

    private static bool Contains(IReadOnlyList<RingOrder> rings, RingOrder ring)
    {
        foreach (RingOrder one in rings)
        {
            if (one == ring)
            {
                return true;
            }
        }

        return false;
    }

    // Whether their own bench has work for them: a craft under way, or something to fetch or take back. Looked at every couple of
    // seconds (it plans a fetch), not every think.
    private bool BenchHasWork()
    {
        if (Time.time < benchLookAt)
        {
            return benchBusy;
        }

        benchLookAt = Time.time + BenchLookSeconds;
        ResidentRecord record = npc.Record;
        StationQueue queue = record != null && npc.Workplace != null ? WorkerProduction.QueueOf(GameSession.Here, record) : null;
        if (queue == null)
        {
            benchBusy = false;
        }
        else if (!queue.IsIdle)
        {
            benchBusy = !queue.Blocked || (WorkerProduction.HasWorkToTakeBack(queue) && CanTakeBack(queue));
        }
        else
        {
            benchBusy = FetchesOnFoot && ((WorkerProduction.HasWorkToTakeBack(queue) && CanTakeBack(queue))
                || WorkerProduction.PlanFetch(record.Job, queue, GameSession.Stash, Limits(queue)).Count > 0);
        }

        return benchBusy;
    }

    // One frame of felling: walking (false: nothing more this frame) or chopping (true: the work gesture plays).
    private bool FellStep()
    {
        if (logsToChest)
        {
            // At the chests: the logs went in as the tree fell (a save or a full chest on the way loses none); their weight counts to
            // the woodcutter's Strength.
            if (npc.Record != null && carriedWeight > 0f)
            {
                npc.Record.HaulDone += carriedWeight;
            }

            carriedWeight = 0f;
            logsToChest = false;
            ShowTool(null, 1f);
            return GoToNextTree();
        }

        // Gone meanwhile (the player felled it, or coming home laid it down): on to the next.
        if (fellIndex < 0 || fellOn == null || !fellOn.IsStanding(fellIndex))
        {
            return GoToNextTree();
        }

        chopLeft -= Time.deltaTime * BuildRate(npc.Record) * 2f;
        if (chopLeft > 0f)
        {
            return true;
        }

        // Down it comes: its logs into the chests (what won't fit at their feet), and a sapling where it stood.
        TerrainTreeFelling felled = fellOn;
        int index = fellIndex;
        Vector3 foot = fellFoot;
        ReleaseTree();
        if (!felled.FellByHand(index))
        {
            return GoToNextTree();
        }

        WorkerSettings settings = WorkerSettings.Current;
        carriedWeight = 0f;
        ItemDefinition shown = null;
        foreach (ItemAmount log in RingWork.Logs(fellHeight, settings, () => Random.value))
        {
            ItemDelivery.Give((ItemContainer)null, new ItemStack(log.Item, log.Amount), GameSession.Stash.Items, transform);
            carriedWeight += log.Amount * Mathf.Max(0f, log.Item.Weight);
            if (shown == null)
            {
                shown = log.Item;
            }
        }

        if (!RingWork.Replant(foot, settings) && fellingRing != null)
        {
            fellingRing.Unplanted.Add(foot);
        }

        // To the nearest chest with them: the walk paces the felling.
        StashChest chest = StashChest.Nearest(foot, settings.ChestReach);
        if (chest != null && shown != null)
        {
            logsToChest = true;
            ShowTool(shown, CarriedScale);
            GoTo(SpotBeside(chest.transform, out facing));
            return false;
        }

        carriedWeight = 0f;
        return GoToNextTree();
    }

    // The next tree in the ring; or back to the bench when it has work; or the ring is done.
    private bool GoToNextTree()
    {
        ReleaseTree();
        TerrainTreeFelling felling = null;
        TerrainTreeFelling.Standing tree = default;
        bool bench = BenchHasWork();
        if (fellingRing == null || bench || !RingWork.NextTree(fellingRing, out felling, out tree))
        {
            // Done when no grown tree stands in it (another woodcutter may still be at its last one).
            if (!bench && fellingRing != null && !HavenVisit.IsVisiting && Contains(GameSession.Home.Rings, fellingRing)
                && RingWork.Standing(fellingRing) == 0)
            {
                GameMessages.Post($"{npc.Resident.DisplayName} has felled the ring: its saplings are in.", MessageKind.Gain);
                RingWork.ReplantLeft(fellingRing, WorkerSettings.Current);
                GameSession.Home.RemoveRing(fellingRing);
            }

            StopFelling(false);
            fellingRing = null;
            Decide(false, true);
            return false;
        }

        fellOn = felling;
        fellIndex = tree.Index;
        fellFoot = tree.Foot;
        fellHeight = tree.Height;
        RingWork.Claim(felling, tree.Index);
        chopLeft = WorkerSettings.Current.ChopSeconds;
        ShowTool(WorkerSettings.Current.FellingTool, 1f);
        GoTo(TreeSpot(out facing));
        return false;
    }

    // Beside the tree, facing it.
    private Vector3 TreeSpot(out Quaternion facingIt)
    {
        Vector3 away = transform.position - fellFoot;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
        facingIt = Quaternion.LookRotation(-away);
        return fellFoot + away * 1.3f;
    }

    private void ReleaseTree()
    {
        RingWork.Release(fellOn, fellIndex);
        fellIndex = -1;
        fellOn = null;
    }

    // Felling given up (another plan, the ring gone): the tree let go and the axe or logs put away; quietly while the scene closes.
    private void StopFelling(bool quietly)
    {
        bool held = fellIndex >= 0 || logsToChest;
        ReleaseTree();
        logsToChest = false;
        carriedWeight = 0f;
        if (held && !quietly)
        {
            ShowTool(null, 1f);
        }
    }

    // ----- Building at home -----

    // The scaffold this builder should work on: the one they are on while it needs work, else the nearest paid-for one with room.
    private Construction ScaffoldToBuild()
    {
        // A friend's copy walks them to the scaffold too, to be seen at it; only the Haven's own game adds the work (Hammer).
        if (!ResidentBuilders.Builds(npc.Record))
        {
            return null;
        }

        if (building != null && NeedsHands(building))
        {
            return building;
        }

        Construction best = null;
        float bestDistance = float.MaxValue;
        foreach (BuildingObject standing in BuildingObject.Active)
        {
            if (standing == null || !standing.TryGetComponent(out Construction site) || !NeedsHands(site)
                || BuildersOn(site) >= BuildersFor(site.WorkLeft) || (site == unreachable && Time.time < unreachableUntil))
            {
                continue;
            }

            float distance = (standing.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = site;
            }
        }

        return best;
    }

    private static bool NeedsHands(Construction site) => site != null && site.Paid && site.WorkLeft > 0f;

    /// <summary>How many builders go to a scaffold with <paramref name="workLeft"/> seconds of hammering left: one to a flower bed, up to
    /// BuildersPerSite on a hall (three sent across the Haven to a few seconds' work only got in each other's way).</summary>
    public static int BuildersFor(float workLeft) => workLeft < 15f ? 1 : workLeft < 40f ? 2 : BuildersPerSite;

    // A scaffold they couldn't get to (no ground beside it), left alone a while.
    private Construction unreachable;
    private float unreachableUntil;
    private const float NearSiteMetres = 3f;

    private static int BuildersOn(Construction site)
    {
        int count = 0;
        foreach (ResidentLife life in Living)
        {
            count += life.building == site ? 1 : 0;
        }

        return count;
    }

    // Beside the scaffold on the side nearest them, facing it.
    private Vector3 BuildSpot(Construction site, out Quaternion facingIt)
    {
        Vector3 centre = site.transform.position;
        Vector3 edge = site.TryGetComponent(out Collider box) && box.enabled ? box.ClosestPoint(transform.position) : centre;
        Vector3 outward = edge - centre;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f)
        {
            outward = transform.position - centre;
            outward.y = 0f;
        }

        outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
        facingIt = Quaternion.LookRotation(-outward);
        // Builders coming from one side stand along it, apart (by their seat), not all on one spot.
        Vector3 along = Vector3.Cross(Vector3.up, outward);
        return edge + outward * BuildSpotGap + along * workShift;
    }

    // One frame of hammering: false (and a new plan) once the scaffold stands or the work was taken from them.
    private bool Hammer()
    {
        if (!NeedsHands(building))
        {
            building = null;
            Decide(false, true);
            return false;
        }

        // Put beside it where there was no way there (no ground by a dock, a ledge): not hammering it from across the Haven.
        Vector3 edge = building.TryGetComponent(out Collider box) && box.enabled ? box.ClosestPoint(transform.position) : building.transform.position;
        Vector3 apart = edge - transform.position;
        apart.y = 0f;
        if (apart.sqrMagnitude > NearSiteMetres * NearSiteMetres)
        {
            unreachable = building;
            unreachableUntil = Time.time + 60f;
            building = null;
            Decide(false, true);
            return false;
        }

        // Only the game that keeps the Haven counts the work (co-op: a friend's copy shows them hammering).
        if (!HavenVisit.IsVisiting)
        {
            building.AddWork(Time.deltaTime * BuildRate(npc.Record));
        }

        return true;
    }

    /// <summary>Seconds of hammering a builder puts in each second at home: HomeLifeSettings › Builder Work Per Second and their home's
    /// work bonus (HomeLife), so a good home makes a quicker builder.</summary>
    public static float BuildRate(ResidentRecord record) =>
        HomeLifeSettings.Current.BuilderWorkPerSecond * (1f + (record != null ? HomeLife.WorkBonus(record) : 0f));

    /// <summary>
    /// A knock at the door of the house they are asleep in (HouseDoor): they come out to the door and stand there a while for
    /// <paramref name="by"/>, talking to them at once when <paramref name="talk"/>; then they go back in. False when they
    /// aren't inside.
    /// </summary>
    public bool Wake(GameObject by, bool talk)
    {
        if (insideOf == null)
        {
            return false;
        }

        ComeOut();
        awakeUntil = Time.time + AwakeSeconds;
        arrived = true;
        if (talk && by != null && isActiveAndEnabled)
        {
            StartCoroutine(TalkTo(by));
        }

        return true;
    }

    // The next frame, once they stand at the door again: as if the player had spoken to them there.
    private System.Collections.IEnumerator TalkTo(GameObject by)
    {
        yield return null;
        Interactable speak = GetComponent<Interactable>();
        if (speak != null && insideOf == null && by != null && speak.TryBegin(by))
        {
            speak.End(InteractionResult.Completed);
        }
    }

    // In at the door: hidden and quiet until morning, the house's window lit (HearthSmoke reads HavenHomes.Indoors). The
    // quest marker over them stays, at the door: that is where to knock (HouseDoor).
    private void GoIn(BuildingObject house)
    {
        if (insideOf == house || house == null)
        {
            return;
        }

        ComeOut();
        insideOf = house;
        awakeUntil = -1f;
        HavenHomes.NoteIndoors(house, true);
        HouseDoor.For(house);
        ShowTool(null, 1f);
        if (emotes != null && emotes.IsPlaying)
        {
            emotes.Stop();
        }

        foreach (Renderer part in GetComponentsInChildren<Renderer>())
        {
            if (part.enabled && part.GetComponentInParent<QuestMarker>() == null)
            {
                part.enabled = false;
                hiddenRenderers.Add(part);
            }
        }

        foreach (Collider part in GetComponentsInChildren<Collider>())
        {
            if (part.enabled)
            {
                part.enabled = false;
                hiddenColliders.Add(part);
            }
        }

        foreach (Interactable part in GetComponents<Interactable>())
        {
            if (part.enabled)
            {
                part.enabled = false;
                hiddenParts.Add(part);
            }
        }

        if (animator != null && animator.enabled)
        {
            animator.enabled = false;
            hiddenParts.Add(animator);
        }

        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
            hiddenParts.Add(agent);
        }
    }

    // Out of the door again: seen, spoken to, walking.
    private void ComeOut()
    {
        if (insideOf == null)
        {
            return;
        }

        BuildingObject house = insideOf;
        insideOf = null;
        HavenHomes.NoteIndoors(house, false);
        Vector3 door = house != null ? house.Door(out _) : transform.position;
        foreach (Renderer part in hiddenRenderers)
        {
            if (part != null)
            {
                part.enabled = true;
            }
        }

        foreach (Collider part in hiddenColliders)
        {
            if (part != null)
            {
                part.enabled = true;
            }
        }

        foreach (Behaviour part in hiddenParts)
        {
            if (part != null)
            {
                part.enabled = true;
            }
        }

        hiddenRenderers.Clear();
        hiddenColliders.Clear();
        hiddenParts.Clear();
        if (agent != null && agent.enabled && NavMesh.SamplePosition(door, out NavMeshHit hit, 3f, agent.areaMask))
        {
            agent.Warp(hit.position);
        }
        else if (agent == null)
        {
            transform.position = door;
        }

        arrived = false;
    }

    private static EmoteDefinition Pick(EmoteDefinition[] gestures) =>
        gestures != null && gestures.Length > 0 ? gestures[Random.Range(0, gestures.Length)] : null;
}
