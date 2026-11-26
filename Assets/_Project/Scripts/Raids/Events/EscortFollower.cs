using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Someone (or something) to bring to a way out (Docs/RaidEventsPlan_2026-11-26.md E3 Escort: the dairymaid of the seter): follows the
/// player at their own pace, can be hurt and killed like anyone (a Combatant the creatures go for), and is delivered once it stands in an
/// open way out's circle: On Delivered is carried out then, On Lost if it falls. A companion's warp keeps it from being left far behind.
/// On the root of the follower's prefab, beside a NavMeshAgent, an Animator with a float "Speed" and a Damageable. Made by EscortOutcome.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EscortFollower : RaidEventTask, IInteractionName
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");

    [Tooltip("What it is called: \"Kari\", \"the dairymaid\".")]
    [SerializeField] private string called = "the traveller";
    [SerializeField, Min(0.5f)] private float followDistance = 2.5f;
    [Tooltip("Left this far behind, it is brought up behind the player (out of a river, round a cliff).")]
    [SerializeField, Min(10f)] private float catchUpBeyond = 35f;
    [Tooltip("Heard this far every few seconds while it follows (0: quiet).")]
    [SerializeField, Min(0f)] private float noiseRadius;
    [SerializeField, Min(0.5f)] private float noiseEvery = 4f;

    private NavMeshAgent agent;
    private Animator animator;
    private Damageable health;
    private bool hasSpeed;
    private RaidEventContext context;
    private string label;
    private List<RaidEventOutcome> onDelivered = new List<RaidEventOutcome>();
    private List<RaidEventOutcome> onLost = new List<RaidEventOutcome>();
    private float nextNoise;

    public bool IsDone { get; private set; }
    public string Called => called;
    public string InteractionName => string.IsNullOrEmpty(called) ? null : char.ToUpperInvariant(called[0]) + called.Substring(1);

    public override string HudLine
    {
        get
        {
            Vector3 from = transform.position;
            ExtractionPoint exit = CrossOutcome.Pick(from, CrossOutcome.Toward.NearestExit);
            string way = exit != null && exit.IsOpen
                ? $"{Mathf.RoundToInt(FlatDistance(exit.transform.position, from))} m to {(string.IsNullOrEmpty(exit.DisplayName) ? exit.name : exit.DisplayName)}"
                : "no way out open";
            return $"<color={UIStyle.Current.AccentHex}>{label}</color>: {way}";
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Damageable>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                hasSpeed |= parameter.nameHash == SpeedId && parameter.type == AnimatorControllerParameterType.Float;
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (health != null)
        {
            health.Died += OnFell;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (health != null)
        {
            health.Died -= OnFell;
        }
    }

    public void Begin(RaidEventContext eventContext, string what, List<RaidEventOutcome> delivered, List<RaidEventOutcome> lost)
    {
        context = eventContext;
        label = what;
        onDelivered = delivered ?? new List<RaidEventOutcome>();
        onLost = lost ?? new List<RaidEventOutcome>();
        nextNoise = Time.time + noiseEvery;
    }

    /// <summary>Delivered when it stands in an open way out's circle. Public for tests.</summary>
    public bool TryDeliver()
    {
        if (IsDone || (health != null && !health.IsAlive))
        {
            return false;
        }

        foreach (ExtractionPoint exit in ExtractionPoint.Active)
        {
            if (exit != null && exit.IsOpen && FlatDistance(exit.transform.position, transform.position) <= exit.HoldRadius)
            {
                IsDone = true;
                GameMessages.Post($"{InteractionName} is safe at {(string.IsNullOrEmpty(exit.DisplayName) ? "the way out" : exit.DisplayName)}.", MessageKind.Gain);
                Apply(onDelivered, context, transform.position);
                context?.Session?.RecordEvent($"Brought {called} out");
                Finish(true);
                return true;
            }
        }

        return false;
    }

    private void OnFell(Damageable fallen, DamageInfo blow)
    {
        if (IsDone)
        {
            return;
        }

        IsDone = true;
        GameMessages.Post($"{InteractionName} has fallen.", MessageKind.Warning);
        Apply(onLost, context, transform.position);
        context?.Session?.RecordEvent($"Lost {called}");
        Finish(false);
    }

    private void Update()
    {
        float speed = 0f;
        if (!IsDone && context?.Player != null && agent.isActiveAndEnabled)
        {
            if (context.Session != null && context.Session.IsOver)
            {
                Finish(false);
                return;
            }

            Vector3 player = context.Player.transform.position;
            float distance = FlatDistance(player, transform.position);
            // Left far behind, or off the NavMesh (made beside a cliff, knocked off it): brought up behind the player.
            if ((distance > catchUpBeyond || !agent.isOnNavMesh)
                && NavMesh.SamplePosition(player - context.Player.transform.forward * 3f, out NavMeshHit behind, 6f, agent.areaMask))
            {
                agent.Warp(behind.position);
            }
            else if (!agent.isOnNavMesh)
            {
                // Nowhere to put it yet: try again next frame.
            }
            else if (distance > followDistance)
            {
                agent.SetDestination(player);
            }
            else if (agent.hasPath)
            {
                agent.ResetPath();
            }

            speed = agent.velocity.magnitude;
            if (noiseRadius > 0f && Time.time >= nextNoise && Player.Current != null && Player.Current.Noise != null)
            {
                nextNoise = Time.time + noiseEvery;
                Player.Current.Noise.Make(transform.position, noiseRadius);
            }

            TryDeliver();
        }

        if (hasSpeed)
        {
            animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);
        }
    }

    // Delivered: goes on through the way out (gone). Fallen or the raid over: stays where it is, its line off the HUD.
    private void Finish(bool delivered)
    {
        IsDone = true;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        if (!Application.isPlaying)
        {
            enabled = false;
            return;
        }

        // Delivered: through the way out. Fallen: gone, not left standing (no death pose of their own).
        if (delivered || (health != null && !health.IsAlive))
        {
            Destroy(gameObject, delivered ? 1.5f : 1f);
        }

        enabled = false;
    }
}
