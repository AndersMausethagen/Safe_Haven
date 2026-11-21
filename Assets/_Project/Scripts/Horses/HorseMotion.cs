using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>What a horse just did that its rider's body answers (PlayerRidingAnimation plays the pack's rider clip for it).</summary>
public enum HorseAct
{
    Rear = 0,
    Buck = 1,
    Strike = 2,
    Fidget = 3,
    Hit = 4,
    Jump = 5,
}

/// <summary>
/// How a horse moves, sounds and looks while it does. Wild, it grazes and ambles about where it was met and bolts from what
/// frightens it, as Wildlife does: the player is heard (PlayerNoise), so a crouched walk gets close and a run sends it off
/// (how close, its temperament says); a creature on the hunt nearby sends it off too. Tamed, it stays near where it was left,
/// comes at its owner's whistle when its bond lets it hear it (<see cref="Come"/>), and in a stable stands in its stall and
/// sleeps at night. Hurt, it flinches to the side it was struck from and runs for a while; a bold one strikes out with its
/// forelegs instead (<see cref="Strike"/>). Ridden, it goes where the rider steers (<see cref="Ride"/>) at a trot, a gallop on
/// its stamina or a walk, and swims across deep water (off the NavMesh, on its stamina) until it finds ground again.
/// <para>
/// One gait at a time (walk, trot, canter, gallop, sprint: the Horse AnimSet Pro clips), the one whose own speed is nearest,
/// played as fast as the speed needs, rather than a blend of two that matches neither; turning, it steps round (the pack's
/// turning clips, by how fast it turns), and it never pivots faster than they show, so it doesn't slide round on the spot.
/// Its Animator has floats "Gait" (0 standing, 1 walk ... 5 sprint) and "Turn" (-1 left ... 1 right), bools "Eating", "Drinking", "Sleeping", "Swimming" and "Dead", ints "HitSide",
/// "Fidget" and "DeathKind", and triggers "Rear", "Buck", "Strike", "Hit" and "Fidgeting" (BuildHorses makes it). Hoofbeats
/// fall on every beat of its gait, and it breathes hard when it is blown.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Horse), typeof(NavMeshAgent))]
public sealed class HorseMotion : MonoBehaviour
{
    private static readonly int GaitId = Animator.StringToHash("Gait");
    private static readonly int TurnId = Animator.StringToHash("Turn");
    private static readonly int EatingId = Animator.StringToHash("Eating");
    private static readonly int DrinkingId = Animator.StringToHash("Drinking");
    private static readonly int SleepingId = Animator.StringToHash("Sleeping");
    private static readonly int SwimmingId = Animator.StringToHash("Swimming");
    private static readonly int DeadId = Animator.StringToHash("Dead");
    private static readonly int DeathKindId = Animator.StringToHash("DeathKind");
    private static readonly int RearId = Animator.StringToHash("Rear");
    private static readonly int BuckId = Animator.StringToHash("Buck");
    private static readonly int StrikeId = Animator.StringToHash("Strike");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int HitSideId = Animator.StringToHash("HitSide");
    private static readonly int FidgetId = Animator.StringToHash("Fidget");
    private static readonly int FidgetingId = Animator.StringToHash("Fidgeting");
    // Its controller's states (BuildHorses): the clips that play in place.
    private static readonly int MoveState = Animator.StringToHash("Move");
    private static readonly int[] FidgetStates = { Animator.StringToHash("Fidget 0"), Animator.StringToHash("Fidget 1"),
        Animator.StringToHash("Fidget 2"), Animator.StringToHash("Fidget 3"), Animator.StringToHash("Fidget 4") };
    private static readonly int[] RootedStates = { Animator.StringToHash("Rear"), Animator.StringToHash("Buck"), Animator.StringToHash("Strike") };
    private static readonly int JumpId = Animator.StringToHash("Jump");

    // Seconds between hoofbeats at each gait's own speed, walk to sprint: four beats to a walk's stride, two to a trot's,
    // three to a canter's, four quick ones to a gallop's.
    private static readonly float[] HoofBeats = { 0.28f, 0.34f, 0.2f, 0.12f, 0.1f };

    private enum Idle { Graze, Wander, Flee, Held, Ridden, Come, Strike, Stall, Lure, Approach, Dead }

    // A wild horse notices barley left on the ground this far off, and comes to a crouched, still player with food this far.
    private const float LureReach = 28f;
    private const float HandReach = 18f;

    [Tooltip("Metres a second each gait's clip shows at the body's size, walk to sprint (measured from the clips' root motion by BuildHorses).")]
    [SerializeField] private float[] gaitSpeeds = { 1.42f, 3.35f, 5.18f, 7.83f, 10.94f };
    [Tooltip("Metres creatures in a raid hear each hoofbeat at, walk to sprint: a walk about a sneaking step, a trot a run, a gallop a cart.")]
    [SerializeField] private float[] hoofNoise = { 2.5f, 6f, 8f, 11f, 13f };
    [Tooltip("Degrees a second its turn on the spot shows (measured from the pack's H_Turn clips by BuildHorses).")]
    [SerializeField, Min(10f)] private float turnInPlaceRate = 84f;

    private Horse horse;
    private NavMeshAgent agent;
    private Animator animator;
    private Idle mode = Idle.Graze;
    private Vector3 home;
    private Quaternion stallFacing = Quaternion.identity;
    private float until;
    private float nextLook;
    private bool galloping;
    private Vector3 rideDirection;
    private int rideGait;
    private bool swimming;
    private float nextHoof;
    private HorseGround.Surface underfoot;
    private float nextSurface;
    private float nextBreath;
    private float nextFidget;
    private bool drinking;
    private float lastYaw;
    private float turnRate;
    // Ridden or swimming: its speed along its facing. It goes where it faces, never sliding sideways through a turn.
    private float pace;
    private Combatant strikeAt;
    private float strikeLands;
    private float nextTrample;
    private DroppedItem lure;
    // Ground and slope under it (sampled a few times a second): its speed over them.
    private float groundScale = 1f;
    private bool roughGround;
    private float nextGround;
    private Vector3 lastPlace;
    // A jump: from, to, how far through, how long, how high.
    private bool jumping;
    private Vector3 jumpFrom;
    private Vector3 jumpTo;
    private float jumpT;
    private float jumpSeconds;
    private float jumpPeak;
    private float nextJumpLook;
    private float nextFollow;
    private float nextFright;
    private float frightenedUntil;
    // A kick on command from the saddle: when its forelegs land.
    private float kickLands = -1f;
    private LineRenderer lead;
    private static Material leadMaterial;

    // One rope material for every horse (each made its own, never destroyed).
    private static Material LeadMaterial
    {
        get
        {
            if (leadMaterial == null)
            {
                leadMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                leadMaterial.SetColor("_BaseColor", new Color(0.55f, 0.42f, 0.28f));
            }

            return leadMaterial;
        }
    }
    private bool puppet;
    private float nextLure;
    private float nextHand;
    private static readonly Collider[] trampled = new Collider[16];

    /// <summary>Its speed over the ground, for the rider and a hitched cart.</summary>
    public Vector3 Velocity { get; private set; }
    public bool IsGalloping => galloping;
    public bool IsFleeing => mode == Idle.Flee;
    public bool IsSwimming => swimming;
    public bool IsJumping => jumping;
    public bool IsComing => mode == Idle.Come;
    /// <summary>The gait it shows now: 0 standing, 1 walk ... 5 sprint (the rider's animation follows it).</summary>
    public int Gait { get; private set; }
    /// <summary>Its Animator, for the rider's animation to keep step with.</summary>
    public Animator Animator => animator;
    /// <summary>Raised with what it did and which one (a fidget's or a flinch's number), for its rider.</summary>
    public event Action<HorseAct, int> Acted;
    /// <summary>Co-op: drawn for a friend's game that runs it (<see cref="SetPuppet"/>).</summary>
    public bool IsPuppet => puppet;

    // Held still while its window is open (HorsePanelWindow), until something hurts it.
    private object inspectedBy;
    private float hurtAt = -99f;
    private bool heldStill;
    private Damageable body;

    /// <summary>The player studies it (its window is open): it stands still, unless something hurts it.</summary>
    public void SetInspected(object by, bool inspected)
    {
        if (inspected)
        {
            inspectedBy = by;
            hurtAt = -99f;
        }
        else if (inspectedBy == by)
        {
            inspectedBy = null;
        }
    }

    /// <summary>True while it stands still for the player studying it; false once it has been hurt meanwhile.</summary>
    public bool HeldForInspection => inspectedBy != null && !(inspectedBy is UnityEngine.Object gone && gone == null) && Time.time - hurtAt > 8f;

    private void OnHurt(DamageInfo damage, DamageResult result) => hurtAt = Time.time;

    private void OnDestroy()
    {
        if (body != null)
        {
            body.Damaged -= OnHurt;
        }
    }

    private void Awake()
    {
        horse = GetComponent<Horse>();
        body = GetComponent<Damageable>();
        if (body != null)
        {
            body.Damaged += OnHurt;
        }

        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        jumpRoot = animator != null ? FindDeep(animator.transform, JumpRootBone) : null;
        home = transform.position;
        lastYaw = transform.eulerAngles.y;
        lastPlace = transform.position;
        nextFidget = Time.time + Random.Range(6f, 20f);
        HoofPrints.AddTo(gameObject);
    }

    private void Start()
    {
        home = transform.position;
        agent.speed = horse.Definition.WalkSpeed;
        if (mode == Idle.Graze)
        {
            Graze();
        }
    }

    /// <summary>Runs from <paramref name="from"/> for some seconds.</summary>
    public void Flee(Vector3 from, float seconds)
    {
        if (mode == Idle.Dead || mode == Idle.Ridden || mode == Idle.Held || mode == Idle.Stall || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        mode = Idle.Flee;
        until = Time.time + seconds;
        Vector3 away = transform.position - from;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;
        away.y = 0f;
        Vector3 target = transform.position + Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * away.normalized * horse.Stats.gallopSpeed * seconds * 0.7f;
        agent.speed = horse.Stats.gallopSpeed * horse.SpeedScale;
        agent.isStopped = false;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 20f, agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
    }

    /// <summary>Its owner whistled and it heard: it comes at a canter to near <paramref name="to"/>. False when it can't now.</summary>
    public bool Come(Vector3 to)
    {
        if (mode == Idle.Dead || mode == Idle.Ridden || mode == Idle.Held || mode == Idle.Stall || !agent.enabled || !agent.isOnNavMesh)
        {
            return false;
        }

        Vector3 near = to + (transform.position - to).normalized * 3f;
        if (!NavMesh.SamplePosition(near, out NavMeshHit hit, 8f, agent.areaMask))
        {
            return false;
        }

        mode = Idle.Come;
        until = Time.time + 45f;
        agent.speed = Mathf.Lerp(horse.Definition.TrotSpeed, horse.Stats.gallopSpeed, 0.6f) * horse.SpeedScale;
        agent.isStopped = false;
        agent.SetDestination(hit.position);
        Neigh();
        return true;
    }

    /// <summary>Held on a rope (HorseTaming moves it with <see cref="Lunge"/>) or let go of it.</summary>
    public void Hold(bool held)
    {
        if (mode == Idle.Dead)
        {
            return;
        }

        mode = held ? Idle.Held : Idle.Graze;
        if (!held)
        {
            home = transform.position;
            Graze();
        }
    }

    /// <summary>On the rope: lunges toward a point at the given speed (0 stands still, head low).</summary>
    public void Lunge(Vector3 point, float speed)
    {
        if (mode != Idle.Held || !agent.isOnNavMesh)
        {
            return;
        }

        agent.speed = speed;
        agent.isStopped = speed <= 0.01f;
        if (speed > 0.01f)
        {
            agent.SetDestination(point);
        }
    }

    /// <summary>Kept in a stall (HorseStable): it stands at <paramref name="at"/>, facing out, and sleeps at night; the stall
    /// is inside the building's NavMesh hole, so its agent rests meanwhile. Let out (<paramref name="kept"/> false), it steps
    /// to <paramref name="at"/> (the door) and grazes there.</summary>
    public void Stall(bool kept, Vector3 at, Quaternion facing)
    {
        if (mode == Idle.Dead)
        {
            return;
        }

        if (!kept)
        {
            // Out of the stall: whatever the mode reads (fed there, it said Graze), the agent comes back on. Left off, the horse led
            // out stood frozen for good, deaf to the whistle and running in place under a rider.
            if (mode != Idle.Stall && agent.enabled)
            {
                return;
            }

            mode = Idle.Graze;
            agent.enabled = true;
            if (NavMesh.SamplePosition(at, out NavMeshHit door, 6f, NavMesh.AllAreas))
            {
                agent.Warp(door.position);
            }

            home = transform.position;
            Graze();
            return;
        }

        mode = Idle.Stall;
        galloping = false;
        swimming = false;
        Velocity = Vector3.zero;
        home = at;
        stallFacing = facing;
        agent.enabled = false;
        transform.SetPositionAndRotation(at, facing);
    }

    public bool IsStalled => mode == Idle.Stall;

    public void PlayRear()
    {
        SetTrigger(RearId);
        Acted?.Invoke(HorseAct.Rear, 0);
        Neigh();
    }

    /// <summary>A kick of the back legs: bucking on the rope or under a rider it doesn't know.</summary>
    public void PlayBuck()
    {
        SetTrigger(BuckId);
        Acted?.Invoke(HorseAct.Buck, 0);
    }

    public void PlayEat()
    {
        // Not out of the stall (or a hold or death) by eating: only the pose and its seconds.
        if (animator != null && mode != Idle.Ridden && mode != Idle.Stall && mode != Idle.Dead)
        {
            until = Mathf.Max(until, Time.time + 4f);
            mode = Idle.Graze;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }
    }

    /// <summary>A shake of the head, a look about or a step: idle life, and its answer to a brush.</summary>
    /// <summary>It throws its head up and round the way it is about to go (a tell on the rope).</summary>
    public void PlayHeadToss(Vector3 toward)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetInteger(FidgetId, 0);
        animator.SetTrigger(FidgetingId);
        Acted?.Invoke(HorseAct.Fidget, 0);
        if (horse.Definition.Snort != null)
        {
            AudioManager.Play(horse.Definition.Snort, transform.position);
        }
    }

    public void PlayFidget()
    {
        if (animator == null)
        {
            return;
        }

        int fidget = Random.Range(0, 5);
        animator.SetInteger(FidgetId, fidget);
        animator.SetTrigger(FidgetingId);
        Acted?.Invoke(HorseAct.Fidget, fidget);
        if (horse.Definition.Snort != null)
        {
            AudioManager.Play(horse.Definition.Snort, transform.position);
        }
    }

    /// <summary>Struck from <paramref name="from"/>: it flinches to that side (front left, front right, left, right, back left, back right).</summary>
    public void PlayHit(Vector3 from)
    {
        if (animator == null || mode == Idle.Dead)
        {
            return;
        }

        int side = HitSide(transform.forward, from - transform.position);
        animator.SetInteger(HitSideId, side);
        animator.SetTrigger(HitId);
        Acted?.Invoke(HorseAct.Hit, side);
    }

    /// <summary>Which side a blow came from: 0 front left, 1 front right, 2 left, 3 right, 4 back left, 5 back right.</summary>
    public static int HitSide(Vector3 forward, Vector3 toSource)
    {
        forward.y = 0f;
        toSource.y = 0f;
        float angle = toSource.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(forward, toSource, Vector3.up) : 0f;
        int side = angle >= 0f ? 1 : 0;
        float off = Mathf.Abs(angle);
        return off < 50f ? side : off < 125f ? 2 + side : 4 + side;
    }

    /// <summary>A bold horse turns on what hurt it and strikes with its forelegs.</summary>
    public void Strike(Combatant target)
    {
        if (target == null || mode == Idle.Dead || mode == Idle.Ridden || mode == Idle.Held || mode == Idle.Stall)
        {
            return;
        }

        mode = Idle.Strike;
        strikeAt = target;
        strikeLands = Time.time + 0.55f;
        until = Time.time + 1.6f;
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        Vector3 toward = target.transform.position - transform.position;
        toward.y = 0f;
        if (toward.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(toward.normalized, Vector3.up);
        }

        SetTrigger(StrikeId);
        Acted?.Invoke(HorseAct.Strike, 0);
        Neigh();
    }

    /// <summary>
    /// Co-op (Horse.SetPuppet): drawn for the friend's game that runs it: no thinking and no agent here, it is put where that
    /// game has it and its Animator is mirrored (CoopHorses); only its hoofbeats are its own, by how fast it is put along.
    /// Run here again, it grazes where it stands.
    /// </summary>
    public void SetPuppet(bool on)
    {
        puppet = on;
        lastPlace = transform.position;
        if (on)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.enabled = false;
            galloping = false;
            jumping = false;
            swimming = false;
            kickLands = -1f;
            strikeAt = null;
            lure = null;
            if (lead != null)
            {
                lead.enabled = false;
            }

            if (mode != Idle.Dead)
            {
                mode = Idle.Graze;
            }

            return;
        }

        if (mode == Idle.Dead)
        {
            return;
        }

        agent.enabled = true;
        if (agent.isOnNavMesh)
        {
            agent.Warp(transform.position);
        }

        agent.updateRotation = true;
        if (animator != null)
        {
            animator.speed = 1f;
        }

        home = transform.position;
        Graze();
    }

    /// <summary>A rider on its back: from now on it goes where <see cref="Ride"/> says.</summary>
    public void BeginRidden()
    {
        mode = Idle.Ridden;
        pace = 0f;
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        agent.updateRotation = false;
        rideDirection = Vector3.zero;
    }

    public void EndRidden()
    {
        if (mode != Idle.Ridden)
        {
            return;
        }

        mode = Idle.Graze;
        agent.updateRotation = true;
        galloping = false;
        home = transform.position;
        if (!swimming)
        {
            Graze();
        }
    }

    /// <summary>The rider's wish this frame: a direction on the ground (length up to 1) and a gait (-1 walk, 0 trot,
    /// 1 gallop while its stamina lasts, 2 canter: free).</summary>
    public void Ride(Vector3 direction, int gait)
    {
        rideDirection = Vector3.ClampMagnitude(new Vector3(direction.x, 0f, direction.z), 1f);
        rideGait = gait;
    }

    public void Die()
    {
        // Killed in the air: it comes down where it was going, not hanging there through its death (Update stops for the dead).
        if (jumping)
        {
            transform.position = jumpTo;
            jumping = false;
        }

        mode = Idle.Dead;
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        agent.enabled = false;
        if (animator != null)
        {
            // Killed at a gallop it tumbles forward; standing it falls to one side or the other.
            animator.SetInteger(DeathKindId, galloping ? 4 : Random.Range(0, 4));
            animator.SetBool(DeadId, true);
            animator.SetFloat(GaitId, 0f);
        }
    }

    private void Update()
    {
        if (mode == Idle.Dead)
        {
            return;
        }

        if (puppet)
        {
            // Put along by a friend's game: its gait and hoofbeats from how fast.
            Vector3 at = transform.position;
            Vector3 step = at - lastPlace;
            step.y = 0f;
            lastPlace = at;
            Velocity = Vector3.Lerp(Velocity, step / Mathf.Max(Time.deltaTime, 0.0001f), 0.25f);
            Gait = GaitFor(Velocity.magnitude, gaitSpeeds);
            Sound(Velocity.magnitude);
            return;
        }

        if (Time.time >= nextFright)
        {
            nextFright = Time.time + 0.5f;
            ShyFromMonsters();
        }

        if (kickLands > 0f && Time.time >= kickLands)
        {
            kickLands = -1f;
            KickLands();
        }

        if (jumping)
        {
            UpdateJump();
        }
        else if (swimming)
        {
            UpdateSwimming();
        }
        else if (mode == Idle.Ridden)
        {
            UpdateRidden();
        }
        else if (HeldForInspection)
        {
            // Stood still for the player to look it over: no grazing steps, no wandering off.
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            heldStill = true;
            Velocity = Vector3.zero;
        }
        else
        {
            if (heldStill)
            {
                heldStill = false;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }
            }

            UpdateOnFoot();
            Velocity = agent.enabled ? agent.velocity : Vector3.zero;
        }

        // Stamina comes back while it neither gallops nor swims (sooner for an enduring horse).
        if (!galloping && !swimming)
        {
            float regen = horse.Definition.StaminaRegen * (horse.Stats.Has(HorseTrait.Enduring) ? 1.25f : 1f);
            horse.Stamina = Mathf.Min(horse.MaxStamina, horse.Stamina + regen * Time.deltaTime);
        }

        // What it learns by going: rough ground, swims, the road.
        Vector3 place = transform.position;
        float moved = Vector3.Distance(new Vector3(place.x, 0f, place.z), new Vector3(lastPlace.x, 0f, lastPlace.z));
        lastPlace = place;
        if (moved < 5f)
        {
            horse.Travelled(moved, roughGround, swimming);
        }

        UpdateLead();
        Animate(Velocity.magnitude);
        Sound(Velocity.magnitude);
    }

    private void UpdateRidden()
    {
        // Up on its hind legs, bucking or striking out, it goes nowhere until it is down again: moving under a clip that
        // plays in place, it glided.
        if (Rooted())
        {
            pace = 0f;
            Velocity = Vector3.zero;
            return;
        }

        HorseDefinition kind = horse.Definition;
        Vector3 wish = rideDirection;
        // Under a rider it doesn't know yet, it veers off this way and that (HorseTaming); the rider steers against it.
        // Balking or rearing, it goes nowhere.
        if (horse.Taming != null && horse.Taming.IsBreaking)
        {
            wish = horse.Taming.IsBalking || horse.Taming.IsRearing ? Vector3.zero : Vector3.ClampMagnitude(wish + horse.Taming.Veer, 1f);
        }

        float wanted = wish.magnitude;
        bool breaking = horse.Taming != null && horse.Taming.IsBreaking;
        galloping = rideGait == 1 && wanted > 0.1f && horse.Stamina > 0.5f && !breaking;
        float speed = rideGait < 0 ? kind.WalkSpeed * 1.4f : galloping ? horse.Stats.gallopSpeed : rideGait == 2 && !breaking ? kind.CanterSpeed : kind.TrotSpeed;
        speed *= horse.SpeedScale * HorseHitch.SpeedScaleFor(horse) * GroundScale();
        if (galloping)
        {
            float enduring = horse.Stats.Has(HorseTrait.Enduring) ? 0.85f : 1f;
            horse.Stamina = Mathf.Max(0f, horse.Stamina - kind.GallopCost * PlayerHorsemanship.GallopCostScale() * enduring * Time.deltaTime);
            Trample();
        }

        Vector3 forward = Steer(wish, kind.TurnSpeed);
        // Into a sharp turn it slows (more the faster it goes), out of it it picks up again.
        float alignment = wanted > 0.1f ? Mathf.Clamp01(Vector3.Dot(forward, wish.normalized) * 1.3f) : 0f;
        float targetPace = speed * wanted * alignment;
        this.pace = Mathf.MoveTowards(this.pace, targetPace, (targetPace > this.pace ? 7f : 12f) * Time.deltaTime);
        Velocity = forward * this.pace;
        if (!agent.enabled || !agent.isOnNavMesh || Velocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // It jumps only when its rider asks (RequestJump, Space): jumping by itself whenever it met a stone or the edge of
        // the NavMesh, it leapt where the player hadn't meant to go.
        // Deep water ahead: it wades in and swims. Otherwise Move keeps it on the NavMesh, stopping at walls as anything
        // walking does.
        if (wanted > 0.1f && WaterAhead(out float surface))
        {
            BeginSwimming(surface);
            return;
        }

        agent.Move(Velocity * Time.deltaTime);
    }

    // ----- What frightens it, and a kick on command -----

    // A monster horses shy from (draugr, trolls) near: unless it is brave, very fond of its rider or barded, it panics:
    // ridden, it rears (and may throw a rider it hardly knows); otherwise it runs.
    private void ShyFromMonsters()
    {
        if (mode == Idle.Dead || mode == Idle.Stall || horse.Stats.Has(HorseTrait.Brave) || horse.Armour != null || horse.Definition.AlwaysArmoured
            || Time.time < frightenedUntil || horse.IsTamed && horse.CurrentBond() >= 70f)
        {
            return;
        }

        foreach (MonsterAI monster in MonsterAI.Active)
        {
            MonsterDefinition kind = monster != null ? monster.Definition : null;
            if (kind == null || kind.FrightensHorses <= 0f || !monster.TryGetComponent(out Damageable life) || !life.IsAlive
                || (monster.transform.position - transform.position).sqrMagnitude > kind.FrightensHorses * kind.FrightensHorses)
            {
                continue;
            }

            frightenedUntil = Time.time + 10f;
            if (horse.Rider != null)
            {
                PlayRear();
                GameMessages.Post($"{horse.HorseName} shies at the {kind.DisplayName.ToLowerInvariant()}!", MessageKind.Warning);
                if (HorseBondRules.Throws(horse.CurrentBond(), horse.Stats.temperament, HorseSettings.Current, Random.value))
                {
                    horse.Rider.Dismount(thrown: true);
                    Flee(monster.transform.position, horse.Definition.FleeSeconds * horse.Temperament.panic);
                }
            }
            else
            {
                Flee(monster.transform.position, horse.Definition.FleeSeconds * horse.Temperament.panic);
            }

            return;
        }
    }

    /// <summary>Its rider asks it to rear and strike out with its forelegs at what is ahead (R). Any horse will at bond
    /// 40; a bold one at once. False, when it won't.</summary>
    public bool KickOnCommand()
    {
        if (mode != Idle.Ridden || jumping || swimming || kickLands > 0f)
        {
            return false;
        }

        if (horse.Stats.temperament != HorseTemperament.Bold && horse.CurrentBond() < 40f)
        {
            return false;
        }

        SetTrigger(StrikeId);
        Acted?.Invoke(HorseAct.Strike, 0);
        Neigh();
        kickLands = Time.time + 0.5f;
        return true;
    }

    // Its forelegs come down on whatever enemy is close in front.
    private void KickLands()
    {
        Vector3 ahead = transform.position + transform.forward * 1.8f + Vector3.up;
        int found = Physics.OverlapSphereNonAlloc(ahead, 1.6f, trampled, ~0, QueryTriggerInteraction.Ignore);
        var struck = new System.Collections.Generic.HashSet<Damageable>();
        for (int i = 0; i < found; i++)
        {
            Combatant target = trampled[i].GetComponentInParent<Combatant>();
            if (target == null || target.Faction != Faction.Enemy || !target.TryGetComponent(out Damageable life) || !life.IsAlive || !struck.Add(life))
            {
                continue;
            }

            float damage = horse.Definition.StrikeDamage * 1.5f * (1f + PlayerHorsemanship.MountedDamage());
            var blow = new DamageInfo(horse.Combatant, null, damage, DamageType.Physical, false, target.transform.position + Vector3.up, transform.forward, 70f, HitTechnique.None);
            life.ApplyDamage(blow);
            PlayerHorsemanship.Practise(HorseWork.MountedHit);
        }

        if (struck.Count > 0)
        {
            CameraShake.Add(0.15f);
        }
    }

    // ----- Ground -----

    // Its speed over the ground under it (paths quicker, mud and rock slower) and up the slope ahead. A bog-strider
    // doesn't slow in mud; a sure-footed horse doesn't slow on rough ground.
    private float GroundScale()
    {
        if (Time.time >= nextGround)
        {
            nextGround = Time.time + 0.25f;
            HorseSettings settings = HorseSettings.Current;
            float ground = settings.GroundSpeedOf(HorseGround.LayerAt(transform.position), out bool soft);
            float grade = HorseGround.GradeAhead(transform.position, transform.forward, 1.5f);
            roughGround = ground < 1f && !soft || grade > 0.25f;
            if (soft && horse.Definition.Has(HorseAbility.BogStrider) || !soft && horse.Stats.Has(HorseTrait.SureFooted))
            {
                ground = Mathf.Max(1f, ground);
            }

            float climb = grade > 0f ? Mathf.Lerp(1f, settings.SteepClimb, Mathf.Clamp01(grade) * (horse.Stats.Has(HorseTrait.SureFooted) ? 0.5f : 1f)) : 1f;
            groundScale = ground * climb;
        }

        return groundScale;
    }

    // ----- Jumping -----

    private static readonly RaycastHit[] JumpHits = new RaycastHit[16];

    /// <summary>Its rider asks it to jump (Space, moving): over what is low ahead (a log, a fence, a ditch, a stream),
    /// or across open ground as far as its pace carries it. False when it can't: too slow, something too high ahead, or
    /// nowhere straight ahead to land.</summary>
    public bool RequestJump()
    {
        if (mode != Idle.Ridden || jumping || swimming || kickLands > 0f || !agent.enabled || !agent.isOnNavMesh
            || HorseHitch.Pulling(horse) != null || pace < 1f || Time.time < nextJumpLook)
        {
            return false;
        }

        nextJumpLook = Time.time + 0.4f;
        if (!FindJump(out Vector3 landing, out float over))
        {
            return false;
        }

        BeginJump(landing, over);
        return true;
    }

    // Whether anything but the horse, its rider and the ground stands between two points.
    private bool JumpBlocked(Vector3 from, Vector3 to, bool groundToo)
    {
        Vector3 line = to - from;
        float length = line.magnitude;
        if (length < 0.01f)
        {
            return false;
        }

        int count = Physics.RaycastNonAlloc(from, line / length, JumpHits, length, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = JumpHits[i].collider;
            if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<Player>() != null || (!groundToo && hit is TerrainCollider))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // Where a jump straight ahead lands, and how high it must clear. The landing is on the NavMesh, straight ahead (never
    // snapped to the side, which looked like a teleport), not much higher or lower, out of deep water, and with nothing
    // in the way along the arc; low things on the way (a log, a fence) are cleared, high ones refuse it.
    private bool FindJump(out Vector3 landing, out float over)
    {
        HorseSettings settings = HorseSettings.Current;
        landing = transform.position;
        over = 0f;
        Vector3 at = transform.position;
        Vector3 ahead = transform.forward;
        ahead.y = 0f;
        ahead.Normalize();
        float reach = Mathf.Clamp(pace * 0.55f + 1.5f, settings.JumpLength.x * 0.6f, settings.JumpLength.y);
        // What it has to clear on the way: the highest thing standing along the line (the ground itself isn't).
        if (Physics.Raycast(at + Vector3.up * 0.45f, ahead, out RaycastHit low, reach, ~0, QueryTriggerInteraction.Ignore)
            && !low.collider.transform.IsChildOf(transform) && !(low.collider is TerrainCollider) && low.collider.GetComponentInParent<Player>() == null)
        {
            Vector3 above = low.point + ahead * 0.2f + Vector3.up * (settings.JumpHeight + 0.6f);
            over = Physics.Raycast(above, Vector3.down, out RaycastHit top, settings.JumpHeight + 1.2f, ~0, QueryTriggerInteraction.Ignore)
                ? top.point.y - at.y : settings.JumpHeight + 1f;
            if (over > settings.JumpHeight)
            {
                return false;
            }

            reach = Mathf.Max(reach, low.distance + 2.2f);
        }

        float clear = over + 0.6f + 0.9f;
        for (float distance = reach; distance >= settings.JumpLength.x * 0.6f; distance -= 0.5f)
        {
            Vector3 target = at + ahead * distance;
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 0.8f, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 flat = hit.position - target;
            flat.y = 0f;
            if (flat.magnitude > 0.35f || Mathf.Abs(hit.position.y - at.y) > 1.2f
                || WaterSurface.OpenAt(hit.position + Vector3.up, 2f, out _, 0.4f) != null
                || JumpBlocked(at + Vector3.up * clear, hit.position + Vector3.up * clear, true)
                || JumpBlocked(hit.position + Vector3.up * clear, hit.position + Vector3.up * 0.3f, false))
            {
                continue;
            }

            landing = hit.position;
            return true;
        }

        return false;
    }

    // The jump clips (H_Jump_*) carry their forward travel on the rig's CG bone (3.6-9.9 m/s, not extracted as root motion),
    // while the jump itself is moved by code along its arc: the saddle ran up to 10 m ahead of the horse and snapped back as the
    // clip ended, the rider with it (the user's playtest, 2026-11-21: "It moves them back again quickly after a jump"). While a
    // jump clip plays, the CG bone is held where it was as the horse left the ground: the clip only moves the body and legs, and
    // the arc (UpdateJump) is the only travel and lift.
    private const string JumpRootBone = "CG";
    private const float JumpClipSeconds = 1.6f;
    private Transform jumpRoot;
    private Vector3 jumpRootRest;
    private float holdJumpRootUntil = -1f;

    private void LateUpdate()
    {
        if (jumpRoot == null)
        {
            return;
        }

        if (Time.time < holdJumpRootUntil)
        {
            jumpRoot.localPosition = jumpRootRest;
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void BeginJump(Vector3 landing, float over)
    {
        if (jumpRoot != null)
        {
            jumpRootRest = jumpRoot.localPosition;
            holdJumpRootUntil = Time.time + JumpClipSeconds;
        }

        jumping = true;
        jumpFrom = transform.position;
        jumpTo = landing;
        jumpT = 0f;
        float distance = Vector3.Distance(jumpFrom, jumpTo);
        jumpSeconds = Mathf.Clamp(distance / Mathf.Max(pace, 4f), 0.5f, 0.95f);
        jumpPeak = Mathf.Max(0f, over) + 0.6f;
        agent.enabled = false;
        SetTrigger(JumpId);
        Acted?.Invoke(HorseAct.Jump, 0);
    }

    private void UpdateJump()
    {
        jumpT = Mathf.Min(1f, jumpT + Time.deltaTime / jumpSeconds);
        Vector3 line = Vector3.Lerp(jumpFrom, jumpTo, jumpT);
        Vector3 next = line + Vector3.up * (4f * jumpPeak * jumpT * (1f - jumpT));
        Velocity = (jumpTo - jumpFrom) / jumpSeconds;
        transform.position = next;
        if (jumpT < 1f)
        {
            return;
        }

        // Down: on the NavMesh again, and a thud.
        jumping = false;
        agent.enabled = true;
        agent.Warp(jumpTo);
        if (horse.Rider != null)
        {
            CameraShake.Add(0.12f);
        }

        if (horse.Definition.Hooves != null)
        {
            AudioManager.Play(horse.Definition.Hooves, transform.position);
        }
    }

    // Trample (Horsemanship's technique): at a gallop, a creature in its path is run down and knocked off its feet.
    private void Trample()
    {
        if (Time.time < nextTrample || Velocity.sqrMagnitude < 36f || !Techniques.Has(PerkTechnique.Trample))
        {
            return;
        }

        nextTrample = Time.time + 0.25f;
        Vector3 ahead = transform.position + transform.forward * 1.7f + Vector3.up;
        int found = Physics.OverlapSphereNonAlloc(ahead, 1.2f, trampled, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < found; i++)
        {
            Combatant target = trampled[i].GetComponentInParent<Combatant>();
            if (target == null || target.Faction != Faction.Enemy || !target.TryGetComponent(out Damageable life) || !life.IsAlive)
            {
                continue;
            }

            float damage = 30f * (1f + PlayerHorsemanship.MountedDamage());
            var blow = new DamageInfo(horse.Combatant, null, damage, DamageType.Physical, false, target.transform.position + Vector3.up, transform.forward, 80f, HitTechnique.None);
            life.ApplyDamage(blow);
            Techniques.Used(PerkTechnique.Trample);
            PlayerHorsemanship.Practise(HorseWork.MountedHit);
            nextTrample = Time.time + 1f;
            return;
        }
    }

    // It turns toward the rein before it goes there: no strafing sideways. Standing it pivots no faster than its turning
    // clip steps round; moving, as fast as its kind turns.
    private Vector3 Steer(Vector3 wish, float turnSpeed)
    {
        if (wish.sqrMagnitude > 0.01f)
        {
            float moving = Mathf.Clamp01(Velocity.magnitude / Mathf.Max(0.1f, horse.Definition.WalkSpeed * 2f));
            float rate = Mathf.Lerp(Mathf.Min(turnSpeed, turnInPlaceRate * 1.3f), turnSpeed, moving);
            // At a gallop it turns wider: a horse can't wheel round on the spot at full tilt.
            rate *= Mathf.Lerp(1f, 0.65f, Mathf.InverseLerp(horse.Definition.TrotSpeed, horse.Stats.gallopSpeed, Velocity.magnitude));
            Quaternion facing = Quaternion.LookRotation(wish.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, rate * Time.deltaTime);
        }

        return transform.forward;
    }

    /// <summary>Turns on the spot toward a facing, at its turning clip's pace (a rider about to get on: it shows its side).
    /// True once it faces it.</summary>
    public bool TurnToward(Quaternion facing)
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnInPlaceRate * 1.3f * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, facing) < 3f;
    }

    // ----- Swimming -----

    private bool WaterAhead(out float surface)
    {
        HorseSettings settings = HorseSettings.Current;
        Vector3 ahead = transform.position + transform.forward * 1.6f;
        return WaterSurface.OpenAt(ahead, 2.5f, out surface, settings.SwimDepth) != null;
    }

    private void BeginSwimming(float surface)
    {
        swimming = true;
        agent.enabled = false;
        galloping = false;
        pace = Mathf.Min(pace, HorseSettings.Current.SwimSpeed);
        Vector3 at = transform.position + transform.forward * 0.8f;
        at.y = surface - HorseSettings.Current.SwimDepth;
        transform.position = at;
    }

    private void UpdateSwimming()
    {
        HorseSettings settings = HorseSettings.Current;
        bool ridden = mode == Idle.Ridden;
        Vector3 wish = ridden ? rideDirection : ShoreDirection();
        float tired = horse.Stamina > 0.5f ? 1f : 0.55f;
        bool swimmer = horse.Stats.Has(HorseTrait.Swimmer);
        tired *= swimmer ? 1.3f : 1f;
        horse.Stamina = Mathf.Max(0f, horse.Stamina - settings.SwimCost * (swimmer ? 0.5f : 1f) * Time.deltaTime);
        Vector3 forward = Steer(wish, horse.Definition.TurnSpeed * 0.6f);
        float wanted = wish.magnitude;
        float alignment = wanted > 0.1f ? Mathf.Clamp01(Vector3.Dot(forward, wish.normalized) * 1.3f) : 0f;
        pace = Mathf.MoveTowards(pace, settings.SwimSpeed * tired * wanted * alignment, 4f * Time.deltaTime);
        Velocity = forward * pace;
        Vector3 next = transform.position + Velocity * Time.deltaTime;
        if (WaterSurface.OpenAt(next + Vector3.up * settings.SwimDepth, 2.5f, out float surface, settings.SwimDepth * 0.7f) != null)
        {
            next.y = Mathf.MoveTowards(transform.position.y, surface - settings.SwimDepth, 2f * Time.deltaTime);
            transform.position = next;
            return;
        }

        // Ground ahead, shallow enough to stand on: it climbs out.
        if (NavMesh.SamplePosition(next + forward * 0.6f, out NavMeshHit hit, 1.8f, NavMesh.AllAreas))
        {
            swimming = false;
            agent.enabled = true;
            agent.Warp(hit.position);
            if (!ridden)
            {
                home = hit.position;
                Graze();
            }

            return;
        }

        // Anything else (a steep bank): it stops there.
        Velocity = Vector3.zero;
    }

    // Swimming with no rider: to the nearest ground.
    private Vector3 ShoreDirection()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 40f, NavMesh.AllAreas))
        {
            Vector3 to = hit.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
        }

        return transform.forward;
    }

    // ----- On foot -----

    private void UpdateOnFoot()
    {
        if (mode == Idle.Stall)
        {
            galloping = false;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, stallFacing, 90f * Time.deltaTime);
            return;
        }

        if (!agent.enabled || !agent.isOnNavMesh || mode == Idle.Held)
        {
            galloping = false;
            return;
        }

        galloping = mode == Idle.Flee;
        switch (mode)
        {
            case Idle.Flee:
                if (Time.time > until || (!agent.pathPending && agent.remainingDistance < 1f))
                {
                    home = transform.position;
                    Graze();
                }

                return;
            case Idle.Come:
                galloping = true;
                if (Time.time > until || (!agent.pathPending && agent.remainingDistance < 1.5f))
                {
                    home = transform.position;
                    Graze();
                    Neigh();
                }

                return;
            case Idle.Lure:
                UpdateLure();
                return;
            case Idle.Approach:
                UpdateApproach();
                return;
            case Idle.Strike:
                if (strikeAt != null && Time.time >= strikeLands)
                {
                    StrikeLands();
                }

                if (Time.time > until)
                {
                    Flee(transform.position - transform.forward, horse.Definition.FleeSeconds * 0.5f);
                }

                return;
        }

        if (Time.time >= nextLook)
        {
            nextLook = Time.time + 0.3f;
            if (Startled() || horse.IsWild && (LookForLure() || LookForHand()))
            {
                return;
            }
        }

        if (horse.IsTamed && horse.Order == Horse.Orders.Follow)
        {
            UpdateFollow();
            return;
        }

        if (horse.IsTamed && horse.Order == Horse.Orders.Work)
        {
            UpdateWork();
            return;
        }

        // Told to stay, it stands where it is (it still runs from a blow).
        if (horse.IsTamed && horse.Order == Horse.Orders.Stay)
        {
            if (mode != Idle.Graze)
            {
                Graze();
            }

            until = Time.time + 5f;
            return;
        }

        if (Time.time < until)
        {
            return;
        }

        if (mode == Idle.Graze)
        {
            Wander();
        }
        else if (!agent.pathPending && agent.remainingDistance < 0.6f)
        {
            Graze();
        }
    }

    // ----- Following, and a pack horse on a lead -----

    /// <summary>The player, or the horse they ride: who a following horse keeps behind.</summary>
    private Transform Leader(out bool led, out float leaderSpeed)
    {
        led = false;
        leaderSpeed = 0f;
        Player player = Player.Current;
        if (player == null)
        {
            return null;
        }

        if (player.TryGetComponent(out PlayerRiding riding) && riding.Riding != null && riding.Riding != horse)
        {
            led = true;
            leaderSpeed = riding.Riding.Motion.Velocity.magnitude;
            return riding.Riding.transform;
        }

        leaderSpeed = player.TryGetComponent(out CharacterController body) ? body.velocity.magnitude : 0f;
        return player.transform;
    }

    private void UpdateFollow()
    {
        if (Time.time < nextFollow)
        {
            return;
        }

        nextFollow = Time.time + 0.25f;
        HorseSettings settings = HorseSettings.Current;
        Transform leader = Leader(out bool led, out float leaderSpeed);
        if (leader == null)
        {
            return;
        }

        Vector3 behind = leader.position - leader.forward * settings.FollowDistance + leader.right * (led ? 1.1f : 0.6f);
        float distance = Vector3.Distance(transform.position, leader.position);
        if (distance < settings.FollowDistance * 0.85f && leaderSpeed < 0.5f)
        {
            agent.isStopped = true;
            mode = Idle.Graze;
            return;
        }

        if (!NavMesh.SamplePosition(behind, out NavMeshHit hit, 4f, agent.areaMask))
        {
            return;
        }

        mode = Idle.Wander;
        agent.speed = Mathf.Clamp(leaderSpeed * 1.15f + distance * 0.25f, horse.Definition.WalkSpeed, horse.Stats.gallopSpeed) * horse.SpeedScale;
        agent.isStopped = false;
        agent.SetDestination(hit.position);
    }

    // At work: a slow round beside the station, as a horse pulling a plough or a load goes, with rests between.
    private void UpdateWork()
    {
        if (Time.time < until || agent.pathPending || agent.remainingDistance > 0.6f && mode == Idle.Wander)
        {
            return;
        }

        if (mode == Idle.Wander)
        {
            // A rest at the end of each furrow.
            mode = Idle.Graze;
            until = Time.time + Random.Range(2f, 5f);
            agent.isStopped = true;
            return;
        }

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 target = horse.WorkAt + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(3f, 6f);
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, agent.areaMask))
        {
            mode = Idle.Wander;
            agent.speed = horse.Definition.WalkSpeed * 0.8f;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    // A horse led behind a ridden one is on a rope from the rider's hand to its head.
    private void UpdateLead()
    {
        Player player = Player.Current;
        bool shown = horse.IsTamed && horse.Order == Horse.Orders.Follow && mode != Idle.Ridden && player != null
            && player.TryGetComponent(out PlayerRiding riding) && riding.Riding != null && riding.Riding != horse
            && Vector3.Distance(riding.Riding.transform.position, transform.position) < HorseSettings.Current.LeadReach;
        if (shown && lead == null)
        {
            var holder = new GameObject("Lead Rope");
            holder.transform.SetParent(transform, false);
            lead = holder.AddComponent<LineRenderer>();
            lead.positionCount = 10;
            lead.widthMultiplier = 0.04f;
            lead.useWorldSpace = true;
            lead.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lead.sharedMaterial = LeadMaterial;
        }

        if (lead == null)
        {
            return;
        }

        lead.enabled = shown;
        if (!shown)
        {
            return;
        }

        Vector3 hand = player.transform.position + Vector3.up * 1.25f + player.transform.forward * 0.25f - player.transform.right * 0.2f;
        Vector3 head = horse.Seat.position + transform.forward * 0.9f + Vector3.up * 0.2f;
        for (int i = 0; i < lead.positionCount; i++)
        {
            float t = i / (lead.positionCount - 1f);
            lead.SetPosition(i, Vector3.Lerp(hand, head, t) + Vector3.down * (0.5f * 4f * t * (1f - t)));
        }
    }

    // A wild horse that hears the player too near, or sees something hunting, bolts.
    private bool Startled()
    {
        if (!horse.IsWild || !Frightened(out Vector3 from))
        {
            return false;
        }

        lure = null;
        Flee(from, horse.Definition.FleeSeconds * horse.Temperament.panic);
        if (horse.Definition.Whinny != null)
        {
            AudioManager.Play(horse.Definition.Whinny, transform.position);
        }

        return true;
    }

    // ----- Winning a wild horse's trust: barley on the ground, and food from the hand -----

    // Barley (or any food it eats) left on the ground within its reach, with no player close by it: it goes to it.
    private bool LookForLure()
    {
        if (Time.time < nextLure)
        {
            return false;
        }

        nextLure = Time.time + 1.5f;
        Player player = Player.Current;
        DroppedItem best = null;
        float bestDistance = LureReach * LureReach;
        foreach (DroppedItem dropped in DroppedItem.All)
        {
            if (dropped == null || dropped.Stack.IsEmpty || !dropped.Stack.Item.IsHorseTreat)
            {
                continue;
            }

            float distance = (dropped.transform.position - transform.position).sqrMagnitude;
            bool crowded = player != null && (player.transform.position - dropped.transform.position).sqrMagnitude < 5f * 5f;
            if (distance < bestDistance && !crowded)
            {
                best = dropped;
                bestDistance = distance;
            }
        }

        if (best == null || !NavMesh.SamplePosition(best.transform.position, out NavMeshHit hit, 2f, agent.areaMask))
        {
            return false;
        }

        lure = best;
        mode = Idle.Lure;
        until = Time.time + 30f;
        agent.speed = horse.Definition.WalkSpeed;
        agent.isStopped = false;
        agent.SetDestination(hit.position);
        return true;
    }

    private void UpdateLure()
    {
        if (Time.time >= nextLook)
        {
            nextLook = Time.time + 0.3f;
            if (Startled())
            {
                return;
            }
        }

        if (lure == null || lure.Stack.IsEmpty || Time.time > until)
        {
            lure = null;
            home = transform.position;
            Graze();
            return;
        }

        if ((lure.transform.position - transform.position).sqrMagnitude > 1.8f * 1.8f)
        {
            return;
        }

        // At it: head down, a mouthful, a little calmer; then the next, while any is left.
        ItemStack food = lure.Stack;
        if (food.Count > 1)
        {
            lure.Hold(food.WithCount(food.Count - 1));
        }
        else
        {
            lure.Vanish();
            lure = null;
        }

        horse.Calm();
        until = Time.time + 4f;
        mode = Idle.Graze;
        agent.isStopped = true;
        nextLure = Time.time + 5f;
    }

    // A crouched player who keeps still with food in their bag: a horse that has begun to trust them (or a calm one)
    // comes up to eat from their hand.
    private bool LookForHand()
    {
        Player player = Player.Current;
        if (Time.time < nextHand || player == null || (horse.Trust < 1 && horse.Stats.temperament != HorseTemperament.Calm) || !OffersFood(player))
        {
            return false;
        }

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude > HandReach)
        {
            return false;
        }

        Vector3 near = player.transform.position - offset.normalized * 1.7f;
        if (!NavMesh.SamplePosition(near, out NavMeshHit hit, 2f, agent.areaMask))
        {
            return false;
        }

        mode = Idle.Approach;
        until = Time.time + 20f;
        agent.speed = horse.Definition.WalkSpeed * 0.8f;
        agent.isStopped = false;
        agent.SetDestination(hit.position);
        return true;
    }

    private static bool OffersFood(Player player)
    {
        bool still = !player.TryGetComponent(out PlayerMovement feet) || feet.MovementInput.sqrMagnitude < 0.01f;
        return still && player.Noise != null && player.Noise.IsSneaking && Horse.TreatIn(player.GetComponent<ItemInventory>()) != null;
    }

    private void UpdateApproach()
    {
        if (Time.time >= nextLook)
        {
            nextLook = Time.time + 0.3f;
            if (Startled())
            {
                return;
            }
        }

        Player player = Player.Current;
        // Stood up, walked off or out of food: it thinks better of it, and keeps its distance a while.
        if (player == null || !OffersFood(player) || Time.time > until)
        {
            nextHand = Time.time + 15f;
            home = transform.position;
            Graze();
            return;
        }

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude > 2.3f)
        {
            return;
        }

        if (offset.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
        }

        horse.TakeFromHand(player);
        nextHand = Time.time + 12f;
        until = Time.time + 4f;
        mode = Idle.Graze;
        agent.isStopped = true;
    }

    private void StrikeLands()
    {
        Combatant target = strikeAt;
        strikeAt = null;
        if (target == null || !target.TryGetComponent(out Damageable life) || !life.IsAlive)
        {
            return;
        }

        Vector3 offset = target.transform.position - transform.position;
        if (offset.sqrMagnitude > 3.4f * 3.4f)
        {
            return;
        }

        var blow = new DamageInfo(horse.Combatant, null, horse.Definition.StrikeDamage, DamageType.Physical, false, target.transform.position + Vector3.up, offset.normalized, 20f, HitTechnique.None);
        life.ApplyDamage(blow);
    }

    // The player heard as the birds hear them (nearer for a calm horse, farther for a skittish one), and anything hunting
    // the player's side nearby.
    private bool Frightened(out Vector3 from)
    {
        from = transform.position;
        Player player = Player.Current;
        if (player != null && (horse.Taming == null || !horse.Taming.IsHeld))
        {
            float heard = player.Noise != null ? player.Noise.Level : 0f;
            float startle = (horse.Definition.ShyDistance + heard * horse.Definition.PerHeardMetre) * horse.Temperament.shyness * horse.ShyScale;
            // Coming to eat from the hand, it lets the crouched player be.
            if (mode == Idle.Approach)
            {
                startle *= 0.3f;
            }
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < startle * startle)
            {
                from = player.transform.position;
                return true;
            }
        }

        if (MonsterAI.AnyHuntingPlayerSideWithin(transform.position, 16f))
        {
            from = transform.position + transform.forward;
            return true;
        }

        return false;
    }

    private void Graze()
    {
        mode = Idle.Graze;
        until = Time.time + Random.Range(5f, 14f);
        drinking = WaterSurface.OpenAt(transform.position + transform.forward * 1.6f + Vector3.up, 2f, out _) != null;
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    private void Wander()
    {
        float radius = horse.IsTamed ? 4f : horse.Definition.WanderRadius;
        Vector2 step = Random.insideUnitCircle * radius;
        if (agent.isOnNavMesh && NavMesh.SamplePosition(home + new Vector3(step.x, 0f, step.y), out NavMeshHit hit, 4f, agent.areaMask))
        {
            mode = Idle.Wander;
            until = Time.time + 1f;
            agent.speed = horse.Definition.WalkSpeed;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
        else
        {
            Graze();
        }
    }

    /// <summary>The gait for a speed: 0 standing, else the gait (1 walk ... n) whose clip's own speed is nearest.</summary>
    public static int GaitFor(float speed, float[] gaits)
    {
        if (speed < 0.15f || gaits == null || gaits.Length == 0)
        {
            return 0;
        }

        int gait = 1;
        for (int i = 1; i < gaits.Length; i++)
        {
            if (speed > (gaits[i - 1] + gaits[i]) * 0.5f)
            {
                gait = i + 1;
            }
        }

        return gait;
    }

    private static bool Playing(AnimatorStateInfo state, int[] states, float before)
    {
        return System.Array.IndexOf(states, state.shortNameHash) >= 0 && state.normalizedTime < before;
    }

    // In a rear, a buck or a strike that hasn't finished (or is just beginning).
    private bool Rooted()
    {
        if (animator == null)
        {
            return false;
        }

        return Playing(animator.GetCurrentAnimatorStateInfo(0), RootedStates, 0.8f)
            || animator.IsInTransition(0) && Playing(animator.GetNextAnimatorStateInfo(0), RootedStates, 0.8f);
    }

    private void Animate(float speed)
    {
        Gait = swimming ? (speed > 0.3f ? 1 : 0) : GaitFor(speed, gaitSpeeds);
        // How fast it turns, from its own facing, whoever turned it (a rider, its agent, a rider getting on).
        float yaw = transform.eulerAngles.y;
        float rate = Time.deltaTime > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / Time.deltaTime : 0f;
        lastYaw = yaw;
        turnRate = Mathf.Lerp(turnRate, rate, 1f - Mathf.Exp(-12f * Time.deltaTime));
        if (animator == null)
        {
            return;
        }

        // A shake of the head or a look round gives way at once when it moves off, rather than playing on as it goes.
        if (speed > 0.25f && !animator.IsInTransition(0) && Playing(animator.GetCurrentAnimatorStateInfo(0), FidgetStates, 2f))
        {
            animator.CrossFadeInFixedTime(MoveState, 0.2f);
        }

        animator.SetBool(SwimmingId, swimming);
        animator.SetFloat(GaitId, Gait, 0.1f, Time.deltaTime);
        animator.SetFloat(TurnId, swimming ? 0f : Mathf.Clamp(turnRate / turnInPlaceRate, -1f, 1f), 0.08f, Time.deltaTime);
        animator.speed = swimming || Gait == 0 ? 1f : Mathf.Clamp(speed / gaitSpeeds[Gait - 1], 0.35f, 1.5f);
        bool still = speed < 0.15f && !swimming;
        bool grazing = mode == Idle.Graze && still && (horse.IsTamed || Time.time < until - 1.5f);
        animator.SetBool(EatingId, grazing && !drinking);
        animator.SetBool(DrinkingId, grazing && drinking);
        animator.SetBool(SleepingId, mode == Idle.Stall && still && IsNight());
        if (still && mode != Idle.Held && Time.time >= nextFidget)
        {
            nextFidget = Time.time + Random.Range(12f, 30f);
            if (mode == Idle.Ridden || mode == Idle.Stall || mode == Idle.Graze && !grazing)
            {
                PlayFidget();
            }
        }
    }

    private static bool IsNight()
    {
        float hour = AtmosphereConditions.Hour;
        return hour >= 21.5f || hour < 5f;
    }

    // Hoofbeats on every beat of the gait, hard breathing when blown, near the player only.
    private void Sound(float speed)
    {
        Player player = Player.Current;
        if (player == null || (player.transform.position - transform.position).sqrMagnitude > 40f * 40f)
        {
            return;
        }

        HorseDefinition kind = horse.Definition;
        if (Gait > 0 && !swimming && !jumping && kind.Hooves != null)
        {
            if (Time.time >= nextSurface)
            {
                nextSurface = Time.time + 0.3f;
                underfoot = HorseGround.SurfaceAt(transform.position);
            }

            nextHoof -= Time.deltaTime * (animator != null ? animator.speed : 1f);
            if (nextHoof <= 0f)
            {
                nextHoof = HoofBeats[Mathf.Clamp(Gait - 1, 0, HoofBeats.Length - 1)];
                AudioManager.Play(kind.HoovesOn(underfoot), transform.position);
                // In a raid the creatures hear it too: a gallop carries as far as a cart (Docs/SystemDesignReview_2026-11-17.md K10).
                if (RaidDirector.Current != null && hoofNoise.Length > 0)
                {
                    WorldNoise.Emit(transform.position, hoofNoise[Mathf.Clamp(Gait - 1, 0, hoofNoise.Length - 1)]);
                }
            }
        }

        if (kind.Breathing != null && !galloping && horse.Stamina < horse.MaxStamina * 0.4f && Time.time >= nextBreath)
        {
            nextBreath = Time.time + Random.Range(3f, 4.5f);
            AudioManager.Play(kind.Breathing, transform.position);
        }
    }

    private void Neigh()
    {
        if (horse.Definition.Neigh != null)
        {
            AudioManager.Play(horse.Definition.Neigh, transform.position);
        }
    }

    private void SetTrigger(int id)
    {
        if (animator != null && mode != Idle.Dead)
        {
            animator.SetTrigger(id);
        }
    }
}
