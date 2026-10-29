using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Someone who goes along with the player and fights at their side (GDD §15.5): the expedition in the prologue now,
/// raid companions later. It carries out its current <see cref="CompanionOrder"/> through
/// <see cref="CompanionTactics"/> (which enemy, where to stand), walks on the NavMesh, and at no health is knocked down
/// for a while instead of dying. Its parts are plain C#: <see cref="CompanionLeader"/> (whom it follows and their
/// target), <see cref="CompanionWeapon"/> (swings and arrows) and <see cref="CompanionBody"/> (animation and the weapon
/// in its hand).
/// <para>
/// Orders come from code (<see cref="Command(CompanionOrder)"/>: a story rally point, a future command wheel) or from the
/// player in conversation, as answers its CompanionDefinition lists. "Attack my target" is whatever the leader (the
/// player) last hit. Enemies see it as Player faction, but they go for the player first while they can reach them
/// (MonsterAI). How it fights is the CompanionDefinition; health and armour are its Combatant's stats.
/// </para>
/// <para>
/// Where it belongs: on a character with a NavMeshAgent, a Combatant of the Player faction, a Damageable that isn't
/// destroyed on death, a solid (non-trigger) collider so enemies can see and hit it, and an Animator with Speed,
/// Attack and Dead parameters (MonsterHumanoid has them).
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(Combatant), typeof(Damageable))]
public sealed class Companion : MonoBehaviour, IConversationTopics
{
    private const float ThinkInterval = 0.2f;

    private static readonly List<Companion> active = new List<Companion>();

    [SerializeField] private CompanionDefinition definition;
    [Tooltip("Who they are: the name used when they are knocked down.")]
    [SerializeField] private ResidentDefinition resident;
    [SerializeField] private CompanionOrder startingOrder = CompanionOrder.Follow;
    [Tooltip("Their place while following, from the leader (world axes): (2, 0, -3) walks right of and behind them.")]
    [SerializeField] private Vector3 formationOffset;
    [Tooltip("Offer the definition's orders when the player talks to them.")]
    [SerializeField] private bool ordersInConversation = true;

    private readonly List<CompanionThreat> threats = new List<CompanionThreat>();
    private readonly List<MonsterAI> threatMonsters = new List<MonsterAI>();
    private readonly CompanionLeader leader = new CompanionLeader();
    private NavMeshAgent agent;
    private Combatant combatant;
    private Damageable health;
    private Interactable interactable;
    private Talkable talkable;
    private CompanionBody body;
    private CompanionWeapon weapon;

    private CompanionIntent intent = CompanionIntent.Idle;
    private MonsterAI engaged;
    private Vector3 holdPoint;
    private float nextThink;
    private float getUpAt = -1f;
    private CompanionRevive revive;

    /// <summary>Every enabled companion, for systems that give orders or show them.</summary>
    public static IReadOnlyList<Companion> Active => active;

    public CompanionDefinition Definition => definition;
    public CompanionOrder Order { get; private set; }
    public bool IsDowned => getUpAt >= 0f;
    public string DisplayName => resident != null ? resident.DisplayName : name;

    /// <summary>Stands still and stays out of fights while set (a story moment: before the expedition sets out).</summary>
    public bool Paused { get; set; }

    public Vector3 FormationOffset
    {
        get => formationOffset;
        set => formationOffset = value;
    }

    public event Action<CompanionOrder> OrderChanged;

    /// <summary>Gives an order; Stay holds where they stand now.</summary>
    public void Command(CompanionOrder order) => Command(order, transform.position);

    /// <summary>Gives an order; <paramref name="spot"/> is where Stay holds.</summary>
    public void Command(CompanionOrder order, Vector3 spot)
    {
        holdPoint = spot;
        nextThink = 0f;
        if (order == Order)
        {
            return;
        }

        Order = order;
        OrderChanged?.Invoke(order);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Damageable>();
        interactable = GetComponent<Interactable>();
        talkable = GetComponent<Talkable>();
        body = new CompanionBody(GetComponentInChildren<Animator>(), definition);
        if (definition == null || definition.Attack == null)
        {
            Debug.LogError("A Companion needs a CompanionDefinition with an Attack.", this);
            enabled = false;
            return;
        }

        if (health.DestroysOnDeath)
        {
            Debug.LogWarning("A companion's Damageable should not be destroyed on death; it is knocked down instead.", this);
        }

        weapon = new CompanionWeapon(transform, combatant, definition, body, IsFoe);
        Order = startingOrder;
        holdPoint = transform.position;
        revive = CompanionRevive.Create(this, definition.ReviveSeconds);
    }

    private void OnEnable()
    {
        active.Add(this);
        health.Died += OnKnockedDown;
    }

    private void OnDisable()
    {
        active.Remove(this);
        health.Died -= OnKnockedDown;
        leader.Follow(null);
    }

    private void Update()
    {
        leader.Follow(Player.Current);
        body.Arm(definition);

        if (IsDowned)
        {
            if (Time.time >= getUpAt)
            {
                GetUp(definition.GetUpHealth);
            }

            return;
        }

        weapon.LandDue();
        if (Paused || leader.Player == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Halt();
            body.SetSpeed(0f);
            return;
        }

        if (Time.time >= nextThink)
        {
            nextThink = Time.time + ThinkInterval;
            Think();
        }

        weapon.Fight(engaged);
        // Stops to listen while the player talks to them, unless there is fighting to do.
        agent.isStopped = threats.Count == 0 && talkable != null && talkable.IsTalking;
        body.SetSpeed(agent.isStopped ? 0f : agent.velocity.magnitude);
    }

    // ----- Deciding -----

    private void Think()
    {
        GatherThreats();
        MonsterAI leaderTarget = leader.Target;
        var situation = new CompanionSituation
        {
            Order = Order,
            Self = transform.position,
            Leader = leader.Player.transform.position,
            FollowPoint = leader.Player.transform.position + formationOffset,
            HoldPoint = holdPoint,
            LeaderTarget = leaderTarget != null ? threatMonsters.IndexOf(leaderTarget) : -1,
            Threats = threats,
        };

        intent = CompanionTactics.Decide(situation, definition.Ranges);
        engaged = intent.Action == CompanionAction.Attack ? threatMonsters[intent.Target] : null;
        switch (intent.Action)
        {
            case CompanionAction.Move:
                MoveTo(intent.Point);
                break;
            case CompanionAction.Attack:
                if (Flat(engaged.transform.position - transform.position) > definition.ReachFor(0.5f))
                {
                    MoveTo(engaged.transform.position);
                }
                else
                {
                    Halt();
                }

                break;
            default:
                Halt();
                break;
        }
    }

    // Enemies near it or its leader, and the leader's own target wherever it is.
    private void GatherThreats()
    {
        threats.Clear();
        threatMonsters.Clear();
        IDamageable leaderBody = leader.Player.Health;
        MonsterAI leaderTarget = leader.Target;
        float awareness = definition.Awareness;
        Vector3 self = transform.position;
        Vector3 leaderPosition = leader.Player.transform.position;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (!IsFoe(monster))
            {
                continue;
            }

            Vector3 position = monster.transform.position;
            if (monster != leaderTarget && Flat(position - self) > awareness && Flat(position - leaderPosition) > awareness)
            {
                continue;
            }

            threatMonsters.Add(monster);
            threats.Add(new CompanionThreat(position, monster.IsEngaging(leaderBody), monster.IsEngaging(health)));
        }
    }

    private bool IsFoe(MonsterAI monster)
    {
        return monster != null && monster.CurrentState != MonsterAI.State.Dead
            && monster.TryGetComponent(out Combatant other) && combatant.IsHostileTo(other.Faction)
            && monster.TryGetComponent(out Damageable body) && body.IsAlive;
    }

    // ----- Knocked down -----

    private void OnKnockedDown(Damageable victim, DamageInfo hit)
    {
        getUpAt = Time.time + definition.DownedSeconds;
        weapon.Cancel();
        engaged = null;
        Halt();
        body.SetSpeed(0f);
        body.SetDown(true);

        if (interactable != null)
        {
            interactable.enabled = false;
        }

        revive.SetAvailable(true);
        GameMessages.Post($"{DisplayName} is down! Hold E beside them to help them up.", MessageKind.Warning);
    }

    /// <summary>The player helped them up (CompanionRevive): sooner, and with more health than getting up alone.</summary>
    public void Revive()
    {
        if (!IsDowned)
        {
            return;
        }

        GetUp(definition.ReviveHealth);
        GameMessages.Post($"{DisplayName} is back on their feet.", MessageKind.Gain);
    }

    private void GetUp(float healthShare)
    {
        getUpAt = -1f;
        health.Revive(healthShare);
        revive.SetAvailable(false);
        body.SetDown(false);

        if (interactable != null)
        {
            interactable.enabled = true;
        }

        nextThink = 0f;
    }

    // ----- Conversation -----

    /// <summary>The definition's orders, as answers in the player's conversation with them.</summary>
    public void AddTopics(MenuPage page, DialogueSpeaker speaker, Action afterAnswer)
    {
        if (!ordersInConversation || !enabled || IsDowned || Paused)
        {
            return;
        }

        foreach (CompanionOrderOption option in definition.Orders)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.ask) || (option.order == Order && option.order != CompanionOrder.Stay))
            {
                continue;
            }

            CompanionOrderOption chosen = option;
            page.Add(new MenuAction(chosen.ask, null, true, () =>
            {
                Command(chosen.order);
                DialogueBox.Current.Say(speaker, chosen.reply);
            }));
        }
    }

    // ----- Moving -----

    private void MoveTo(Vector3 point)
    {
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
    }

    private void Halt()
    {
        if (agent.isActiveAndEnabled && agent.isOnNavMesh && agent.hasPath)
        {
            agent.ResetPath();
        }
    }

    private static float Flat(Vector3 offset)
    {
        offset.y = 0f;
        return offset.magnitude;
    }
}
