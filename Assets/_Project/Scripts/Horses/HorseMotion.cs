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

    // Seconds between hoofbeats at each gait's own speed, walk to sprint: four beats to a walk's stride, two to a trot's,
    // three to a canter's, four quick ones to a gallop's.
    private static readonly float[] HoofBeats = { 0.28f, 0.34f, 0.2f, 0.12f, 0.1f };

    private enum Idle { Graze, Wander, Flee, Held, Ridden, Come, Strike, Stall, Lure, Approach, Dead }

    // A wild horse notices barley left on the ground this far off, and comes to a crouched, still player with food this far.
    private const float LureReach = 28f;
    private const float HandReach = 18f;

    [Tooltip("Metres a second each gait's clip shows at the body's size, walk to sprint (measured from the clips' root motion by BuildHorses).")]
    [SerializeField] private float[] gaitSpeeds = { 1.42f, 3.35f, 5.18f, 7.83f, 10.94f };
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
    private float nextLure;
    private float nextHand;
    private static readonly Collider[] trampled = new Collider[16];

    /// <summary>Its speed over the ground, for the rider and a hitched cart.</summary>
    public Vector3 Velocity { get; private set; }
    public bool IsGalloping => galloping;
    public bool IsFleeing => mode == Idle.Flee;
    public bool IsSwimming => swimming;
    public bool IsComing => mode == Idle.Come;
    /// <summary>The gait it shows now: 0 standing, 1 walk ... 5 sprint (the rider's animation follows it).</summary>
    public int Gait { get; private set; }
    /// <summary>Its Animator, for the rider's animation to keep step with.</summary>
    public Animator Animator => animator;
    /// <summary>Raised with what it did and which one (a fidget's or a flinch's number), for its rider.</summary>
    public event Action<HorseAct, int> Acted;

    private void Awake()
    {
        horse = GetComponent<Horse>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        home = transform.position;
        lastYaw = transform.eulerAngles.y;
        nextFidget = Time.time + Random.Range(6f, 20f);
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
            if (mode != Idle.Stall)
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
        if (animator != null && mode != Idle.Ridden)
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
    /// 1 gallop while its stamina lasts).</summary>
    public void Ride(Vector3 direction, int gait)
    {
        rideDirection = Vector3.ClampMagnitude(new Vector3(direction.x, 0f, direction.z), 1f);
        rideGait = gait;
    }

    public void Die()
    {
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

        if (swimming)
        {
            UpdateSwimming();
        }
        else if (mode == Idle.Ridden)
        {
            UpdateRidden();
        }
        else
        {
            UpdateOnFoot();
            Velocity = agent.enabled ? agent.velocity : Vector3.zero;
        }

        // Stamina comes back while it neither gallops nor swims.
        if (!galloping && !swimming)
        {
            horse.Stamina = Mathf.Min(horse.MaxStamina, horse.Stamina + horse.Definition.StaminaRegen * Time.deltaTime);
        }

        Animate(Velocity.magnitude);
        Sound(Velocity.magnitude);
    }

    private void UpdateRidden()
    {
        HorseDefinition kind = horse.Definition;
        Vector3 wish = rideDirection;
        // Under a rider it doesn't know yet, it veers off this way and that (HorseTaming); the rider steers against it.
        // Balking or rearing, it goes nowhere.
        if (horse.Taming != null && horse.Taming.IsBreaking)
        {
            wish = horse.Taming.IsBalking || horse.Taming.IsRearing ? Vector3.zero : Vector3.ClampMagnitude(wish + horse.Taming.Veer, 1f);
        }

        float wanted = wish.magnitude;
        galloping = rideGait > 0 && wanted > 0.1f && horse.Stamina > 0.5f && (horse.Taming == null || !horse.Taming.IsBreaking);
        float speed = rideGait < 0 ? kind.WalkSpeed * 1.4f : galloping ? horse.Stats.gallopSpeed : kind.TrotSpeed;
        speed *= horse.SpeedScale * HorseHitch.SpeedScaleFor(horse);
        if (galloping)
        {
            horse.Stamina = Mathf.Max(0f, horse.Stamina - kind.GallopCost * PlayerHorsemanship.GallopCostScale() * Time.deltaTime);
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

        // Deep water ahead: it wades in and swims. Otherwise Move keeps it on the NavMesh, stopping at walls as anything
        // walking does.
        if (wanted > 0.1f && WaterAhead(out float surface))
        {
            BeginSwimming(surface);
            return;
        }

        agent.Move(Velocity * Time.deltaTime);
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
        horse.Stamina = Mathf.Max(0f, horse.Stamina - settings.SwimCost * Time.deltaTime);
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
            lure.Hold(new ItemStack(food.Item, food.Count - 1));
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
        if (Gait > 0 && !swimming && kind.Hooves != null)
        {
            nextHoof -= Time.deltaTime * (animator != null ? animator.speed : 1f);
            if (nextHoof <= 0f)
            {
                nextHoof = HoofBeats[Mathf.Clamp(Gait - 1, 0, HoofBeats.Length - 1)];
                AudioManager.Play(kind.Hooves, transform.position);
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
