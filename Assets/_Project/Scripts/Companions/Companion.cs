using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Someone who goes along with the player and fights at their side (GDD §15.5): the expedition in the prologue now,
/// raid companions later. It carries out its current <see cref="CompanionOrder"/> through
/// <see cref="CompanionTactics"/> (which enemy, where to stand), walks on the NavMesh, swings through AttackResolver or
/// shoots pooled arrows like the player, and at no health is knocked down for a while instead of dying.
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
    private const float LeaderTargetMemory = 15f;
    private const float TurnSpeed = 540f;
    private const float ShotHeight = 1.4f;

    private static readonly List<Companion> active = new List<Companion>();
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

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
    private NavMeshAgent agent;
    private Combatant combatant;
    private Damageable health;
    private Animator animator;
    private Interactable interactable;
    private bool hasSpeed;
    private bool hasAttack;
    private bool hasDead;

    private CompanionIntent intent = CompanionIntent.Idle;
    private MonsterAI engaged;
    private Vector3 holdPoint;
    private float nextThink;
    private float nextAttack;
    private float strikeAt = -1f;
    private MonsterAI striking;
    private float getUpAt = -1f;
    private Player leader;
    private MonsterAI leaderTarget;
    private float leaderTargetAt;
    private CompanionRevive revive;
    private bool armed;

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
        animator = GetComponentInChildren<Animator>();
        if (animator != null && definition != null && definition.Animations != null)
        {
            animator.runtimeAnimatorController = definition.Animations;
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                hasSpeed |= parameter.nameHash == SpeedHash && parameter.type == AnimatorControllerParameterType.Float;
                hasAttack |= parameter.nameHash == AttackHash && parameter.type == AnimatorControllerParameterType.Trigger;
                hasDead |= parameter.nameHash == DeadHash && parameter.type == AnimatorControllerParameterType.Bool;
            }
        }

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
        Follow(null);
    }

    private void Update()
    {
        Follow(Player.Current);
        if (!armed && leader != null)
        {
            Arm();
        }

        if (IsDowned)
        {
            if (Time.time >= getUpAt)
            {
                GetUp(definition.GetUpHealth);
            }

            return;
        }

        if (strikeAt >= 0f && Time.time >= strikeAt)
        {
            Strike();
        }

        if (Paused || leader == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Halt();
            SetSpeed(0f);
            return;
        }

        if (Time.time >= nextThink)
        {
            nextThink = Time.time + ThinkInterval;
            Think();
        }

        Fight();
        // Stops to listen while the player is in a conversation, unless there is fighting to do.
        agent.isStopped = threats.Count == 0 && InConversation();
        SetSpeed(agent.isStopped ? 0f : agent.velocity.magnitude);
    }

    // ----- Deciding -----

    private void Think()
    {
        GatherThreats();
        int target = leaderTarget != null && Time.time - leaderTargetAt <= LeaderTargetMemory
            ? threatMonsters.IndexOf(leaderTarget)
            : -1;
        var situation = new CompanionSituation
        {
            Order = Order,
            Self = transform.position,
            Leader = leader.transform.position,
            FollowPoint = leader.transform.position + formationOffset,
            HoldPoint = holdPoint,
            LeaderTarget = target,
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
        IDamageable leaderBody = leader.Health;
        float awareness = definition.Awareness;
        Vector3 self = transform.position;
        Vector3 leaderPosition = leader.transform.position;
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

    // ----- Fighting -----

    private void Fight()
    {
        if (engaged == null || !IsFoe(engaged))
        {
            return;
        }

        Vector3 toTarget = engaged.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > definition.ReachFor(0.5f))
        {
            return;
        }

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget),
                TurnSpeed * Time.deltaTime);
        }

        if (Time.time >= nextAttack && strikeAt < 0f)
        {
            nextAttack = Time.time + definition.AttackInterval;
            strikeAt = Time.time + definition.StrikeDelay;
            striking = engaged;
            if (hasAttack)
            {
                animator.SetTrigger(AttackHash);
            }
        }
    }

    // The swing lands (or the arrow leaves) at the attack's hit delay.
    private void Strike()
    {
        strikeAt = -1f;
        MonsterAI target = striking;
        striking = null;
        if (target == null || !IsFoe(target))
        {
            return;
        }

        if (definition.IsRanged)
        {
            Shoot(target);
        }
        else
        {
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            AttackResolver.Execute(combatant, definition.Attack, definition.WeaponDamage, transform.position, direction, ~0);
        }

        AudioManager.Play(definition.AttackSound, transform.position);
    }

    private void Shoot(MonsterAI target)
    {
        ItemDefinition arrowItem = definition.Arrow;
        ArrowDefinition arrow = arrowItem.Arrow;
        if (arrow.Projectile == null)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * ShotHeight + transform.forward * 0.4f;
        Vector3 aim = target.TryGetComponent(out Collider body) ? body.bounds.center : target.transform.position + Vector3.up * 0.5f;
        float speed = definition.ArrowSpeed * arrow.SpeedMultiplier;
        Vector3 direction = ArcheryRules.LaunchDirection(origin, aim, speed, Physics.gravity.magnitude * arrow.GravityScale);
        ArrowProjectile projectile = PrefabPool.Spawn(arrow.Projectile, origin, Quaternion.LookRotation(direction));
        projectile.Launch(combatant, definition.Attack, arrowItem, arrow.Damage * definition.ArrowDamageShare, direction * speed);
    }

    // The definition's weapon in the hand, held the way the player holds it (the player's grip; a bow in the left hand).
    private void Arm()
    {
        armed = true;
        ItemDefinition weapon = definition.Weapon;
        if (weapon == null || animator == null || !animator.isHuman)
        {
            return;
        }

        bool bow = HeldItemView.IsBow(weapon);
        Transform hand = animator.GetBoneTransform(bow ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        if (hand == null)
        {
            return;
        }

        HeldItemView reference = leader.GetComponent<HeldItemView>();
        Pose pose = reference == null ? Pose.identity : bow ? reference.OffHandGripPose : reference.GripPose;
        var grip = new GameObject("Weapon Grip").transform;
        grip.SetParent(hand, false);
        grip.SetLocalPositionAndRotation(pose.position, pose.rotation);
        HeldItemView.CreateModel(weapon, grip);
    }

    // ----- Knocked down -----

    private void OnKnockedDown(Damageable victim, DamageInfo hit)
    {
        getUpAt = Time.time + definition.DownedSeconds;
        strikeAt = -1f;
        striking = null;
        engaged = null;
        Halt();
        SetSpeed(0f);
        if (hasDead)
        {
            animator.SetBool(DeadHash, true);
        }

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
        if (hasDead)
        {
            animator.SetBool(DeadHash, false);
        }

        if (interactable != null)
        {
            interactable.enabled = true;
        }

        nextThink = 0f;
    }

    // ----- Leader -----

    // Listens to what the leader hits, so "attack my target" knows what that is.
    private void Follow(Player player)
    {
        if (player == leader)
        {
            return;
        }

        if (leader != null && leader.Combatant != null)
        {
            leader.Combatant.DamageDealt -= OnLeaderHit;
        }

        leader = player;
        leaderTarget = null;
        if (leader != null && leader.Combatant != null)
        {
            leader.Combatant.DamageDealt += OnLeaderHit;
        }
    }

    private void OnLeaderHit(IDamageable target, DamageInfo damage, DamageResult result)
    {
        if (target is Component hit && hit.TryGetComponent(out MonsterAI monster))
        {
            leaderTarget = monster;
            leaderTargetAt = Time.time;
        }
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

    private static bool InConversation() =>
        DialogueBox.IsOpen || (ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpen);

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

    private void SetSpeed(float speed)
    {
        if (hasSpeed)
        {
            animator.SetFloat(SpeedHash, speed, 0.15f, Time.deltaTime);
        }
    }

    private static float Flat(Vector3 offset)
    {
        offset.y = 0f;
        return offset.magnitude;
    }
}
