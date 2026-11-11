using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// How a horse moves and looks while it does. Wild, it grazes and ambles about where it was met and bolts from what
/// frightens it, as Wildlife does: the player is heard (PlayerNoise), so a crouched walk gets close and a run sends it off;
/// a creature on the hunt nearby sends it off too. Tamed, it stays near where it was left. Hurt, it runs from what hurt it
/// for a while. Ridden, it goes where the rider steers (<see cref="Ride"/>) at a trot, a gallop on its stamina or a walk.
/// <para>
/// One gait at a time (walk, trot, canter, gallop, sprint: the Horse AnimSet Pro clips), the one whose own speed is nearest,
/// played as fast as the speed needs, rather than a blend of two that matches neither. Its Animator has a float "Gait"
/// (0 standing, 1 walk ... 5 sprint), bools "Eating" and "Dead", and a trigger "Rear".
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Horse), typeof(NavMeshAgent))]
public sealed class HorseMotion : MonoBehaviour
{
    private static readonly int GaitId = Animator.StringToHash("Gait");
    private static readonly int EatingId = Animator.StringToHash("Eating");
    private static readonly int DeadId = Animator.StringToHash("Dead");
    private static readonly int RearId = Animator.StringToHash("Rear");

    private enum Idle { Graze, Wander, Flee, Held, Ridden, Dead }

    [Tooltip("Metres a second each gait's clip shows at the body's size, walk to sprint (measured from the clips' root motion by BuildHorses).")]
    [SerializeField] private float[] gaitSpeeds = { 1.42f, 3.35f, 5.18f, 7.83f, 10.94f };

    private Horse horse;
    private NavMeshAgent agent;
    private Animator animator;
    private Idle mode = Idle.Graze;
    private Vector3 home;
    private float until;
    private float nextLook;
    private bool galloping;
    private Vector3 rideDirection;
    private int rideGait;

    /// <summary>Its speed over the ground, for the rider and a hitched cart.</summary>
    public Vector3 Velocity { get; private set; }
    public bool IsGalloping => galloping;
    public bool IsFleeing => mode == Idle.Flee;

    private void Awake()
    {
        horse = GetComponent<Horse>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        home = transform.position;
    }

    private void Start()
    {
        home = transform.position;
        agent.speed = horse.Definition.WalkSpeed;
        Graze();
    }

    /// <summary>Runs from <paramref name="from"/> for some seconds.</summary>
    public void Flee(Vector3 from, float seconds)
    {
        if (mode == Idle.Dead || mode == Idle.Ridden || mode == Idle.Held || !agent.isOnNavMesh)
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

    public void PlayRear()
    {
        if (animator != null)
        {
            animator.SetTrigger(RearId);
        }
    }

    /// <summary>A rider on its back: from now on it goes where <see cref="Ride"/> says.</summary>
    public void BeginRidden()
    {
        mode = Idle.Ridden;
        agent.ResetPath();
        agent.isStopped = true;
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
        Graze();
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
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        agent.enabled = false;
        if (animator != null)
        {
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

        if (mode == Idle.Ridden)
        {
            UpdateRidden();
        }
        else
        {
            UpdateOnFoot();
            Velocity = agent.velocity;
        }

        // Stamina comes back while it doesn't gallop.
        if (!galloping)
        {
            horse.Stamina = Mathf.Min(horse.Stats.maxStamina, horse.Stamina + horse.Definition.StaminaRegen * Time.deltaTime);
        }

        Animate(Velocity.magnitude);
    }

    private void UpdateRidden()
    {
        HorseDefinition kind = horse.Definition;
        float wanted = rideDirection.magnitude;
        galloping = rideGait > 0 && wanted > 0.1f && horse.Stamina > 0.5f && (horse.Taming == null || !horse.Taming.IsBreaking);
        float pace = rideGait < 0 ? kind.WalkSpeed * 1.4f : galloping ? horse.Stats.gallopSpeed : kind.TrotSpeed;
        pace *= horse.SpeedScale * HorseHitch.SpeedScaleFor(horse);
        if (galloping)
        {
            horse.Stamina = Mathf.Max(0f, horse.Stamina - kind.GallopCost * Time.deltaTime);
        }

        Vector3 forward = transform.forward;
        if (wanted > 0.1f)
        {
            // It turns toward the rein before it goes there: no strafing sideways.
            Quaternion facing = Quaternion.LookRotation(rideDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, kind.TurnSpeed * Time.deltaTime);
            forward = transform.forward;
        }

        float alignment = wanted > 0.1f ? Mathf.Clamp01(Vector3.Dot(forward, rideDirection.normalized) * 1.3f) : 0f;
        Vector3 target = forward * (pace * wanted * alignment);
        Velocity = Vector3.MoveTowards(Velocity, target, (target.sqrMagnitude > Velocity.sqrMagnitude ? 9f : 14f) * Time.deltaTime);
        if (agent.isOnNavMesh && Velocity.sqrMagnitude > 0.0001f)
        {
            // Move keeps it on the NavMesh: it stops at walls and water as anything walking does.
            agent.Move(Velocity * Time.deltaTime);
        }
    }

    private void UpdateOnFoot()
    {
        if (!agent.isOnNavMesh || mode == Idle.Held)
        {
            galloping = false;
            return;
        }

        galloping = mode == Idle.Flee;
        if (mode == Idle.Flee)
        {
            if (Time.time > until || (!agent.pathPending && agent.remainingDistance < 1f))
            {
                home = transform.position;
                Graze();
            }

            return;
        }

        if (Time.time >= nextLook)
        {
            nextLook = Time.time + 0.3f;
            if (horse.IsWild && Frightened(out Vector3 from))
            {
                Flee(from, horse.Definition.FleeSeconds);
                if (horse.Definition.Whinny != null)
                {
                    AudioManager.Play(horse.Definition.Whinny, transform.position);
                }

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

    // The player heard as the birds hear them, and anything hunting the player's side nearby.
    private bool Frightened(out Vector3 from)
    {
        from = transform.position;
        Player player = Player.Current;
        if (player != null && (horse.Taming == null || !horse.Taming.IsHeld))
        {
            float heard = player.Noise != null ? player.Noise.Level : 0f;
            float startle = horse.Definition.ShyDistance + heard * horse.Definition.PerHeardMetre;
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
        if (agent.isOnNavMesh)
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
        if (animator == null)
        {
            return;
        }

        int gait = GaitFor(speed, gaitSpeeds);
        animator.SetFloat(GaitId, gait, 0.1f, Time.deltaTime);
        animator.speed = gait == 0 ? 1f : Mathf.Clamp(speed / gaitSpeeds[gait - 1], 0.6f, 1.5f);
        animator.SetBool(EatingId, mode == Idle.Graze && speed < 0.15f && Time.time < until - 1.5f && !horse.IsTamed || mode == Idle.Graze && horse.IsTamed && speed < 0.15f);
    }
}
