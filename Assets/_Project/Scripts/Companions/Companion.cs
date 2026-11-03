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
/// It keeps a place in formation turned with the way the leader goes, leaves enemies another has, steps out of blows it sees
/// coming, keeps low while the player sneaks, catches up if it falls behind, and speaks up in a fight (CompanionBarks).
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
    [Tooltip("Their place while following, turned with the way the leader is heading: (2, 0, -3) walks right of and behind them.")]
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
    private float ownSpeed = -1f;
    private float walkingPace;
    private readonly CompanionBarks barks = new CompanionBarks();
    // The way the leader is heading, for its place in the formation. It keeps the last while they stand.
    private Vector3 heading = Vector3.forward;
    private Vector3 leaderWasAt;
    private float hurry = 1f;
    private float stuckSince = -1f;
    // Stepping out of a blow until then, and whose blow it last looked at, so it doesn't keep rolling for the same one.
    private float dodgingUntil = -1f;
    private MonsterAI watching;
    private float watchAgainAt;
    private bool shotBlocked;
    private float archerSide = 1f;
    private float nextArcherSideAt;
    private bool bleedsOut;
    private float bleedOutAt;

    private const float LeaderMovesFaster = 0.8f;
    private const float DodgeSeconds = 0.7f;
    private const float DodgeSpeed = 1.6f;
    private const float ChargeArc = 40f;
    private const float StuckLimit = 5f;
    private const float BehindArc = 110f;
    private const float WarnWithin = 7f;

    /// <summary>Every enabled companion, for systems that give orders or show them.</summary>
    public static IReadOnlyList<Companion> Active => active;

    public CompanionDefinition Definition => definition;
    public CompanionOrder Order { get; private set; }
    public bool IsDowned => getUpAt >= 0f;
    public string DisplayName => resident != null ? resident.DisplayName : name;
    public ResidentDefinition Resident => resident;

    /// <summary>What this raid has been for them, kept for when they get home.</summary>
    public int TimesHelpedUp { get; private set; }
    public int Kills { get; private set; }

    /// <summary>On a raid: they were down too long and the forest has them (SettlementState.LoseResident).</summary>
    public bool IsLost { get; private set; }

    /// <summary>Seconds left to help them up, while they are down on a raid.</summary>
    public float BleedOutLeft => bleedsOut && IsDowned && !IsLost ? Mathf.Max(0f, bleedOutAt - Time.time) : 0f;

    public float HealthShare => health != null ? health.HealthFraction : 0f;

    /// <summary>
    /// Brings someone along on a raid, as their ResidentDefinition says. The copy wakes outside an inactive holder, so that it wakes
    /// as this person, with their model, not as the prefab. On a raid they don't get up by themselves.
    /// </summary>
    public static Companion SpawnForRaid(Companion prefab, ResidentDefinition who, Vector3 position) =>
        SpawnAs(prefab, who, who.RaidCompanion, position, true, true);

    /// <summary>Brings someone out as a companion who fights as <paramref name="how"/> says. Those who bleed out don't get up by themselves.</summary>
    public static Companion SpawnAs(Companion prefab, ResidentDefinition who, CompanionDefinition how, Vector3 position, bool bleedsOut, bool takesOrders)
    {
        var holder = new GameObject("Companion");
        holder.SetActive(false);
        Companion made = Instantiate(prefab, position, Quaternion.identity, holder.transform);
        made.resident = who;
        made.definition = how;
        made.bleedsOut = bleedsOut;
        made.ordersInConversation = takesOrders;
        made.name = who.DisplayName;
        if (who.Model != null)
        {
            Instantiate(who.Model, made.transform, false);
        }

        made.transform.SetParent(null, true);
        Destroy(holder);
        return made;
    }

    /// <summary>Stands still and stays out of fights while set (a story moment: before the expedition sets out).</summary>
    public bool Paused { get; set; }

    /// <summary>Someone lost, standing where a rumour said: they wait, nothing touches them, and there is no talking to them until they are found.</summary>
    public void StandLost()
    {
        Paused = true;
        health.AddInvulnerability(this);
        interactable.enabled = false;
    }

    /// <summary>They follow, as anyone would on a raid: they can fall, and can be helped up.</summary>
    public void FollowWhoFoundThem()
    {
        Paused = false;
        health.RemoveInvulnerability(this);
        interactable.enabled = true;
    }

    /// <summary>They go where they are told but leave enemies to the player (a lesson: nobody kills the practice target).</summary>
    public bool HoldFire { get; set; }

    /// <summary>True while the player sneaks and nothing has noticed yet: they keep low with them, start nothing, and don't give them away.</summary>
    public bool IsKeepingLow { get; private set; }

    public Vector3 FormationOffset
    {
        get => formationOffset;
        set => formationOffset = value;
    }

    public event Action<CompanionOrder> OrderChanged;

    /// <summary>Walks at this many metres a second (a party keeping one pace), or their own speed again when 0 or less.</summary>
    public void SetPace(float metresPerSecond)
    {
        walkingPace = Mathf.Max(0f, metresPerSecond);
        ApplySpeed();
    }

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
        ownSpeed = agent.speed;
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
        Damageable.AnyDamaged += CountKill;
    }

    private void OnDisable()
    {
        active.Remove(this);
        health.Died -= OnKnockedDown;
        Damageable.AnyDamaged -= CountKill;
        leader.Follow(null);
        body?.SetSheathed(false);
    }

    private void Update()
    {
        leader.Follow(Player.Current);
        body.Arm(definition);
        bool talking = !IsDowned && ((talkable != null && talkable.IsTalking) || DialogueBox.IsTalkingTo(transform)) &&
            !MonsterAI.AnyHuntingPlayerSide();
        body.SetSheathed(talking);
        if (talking)
        {
            weapon.Cancel();
            Halt();
            body.SetSpeed(0f);
            return;
        }

        if (IsDowned)
        {
            if (bleedsOut && Time.time >= bleedOutAt)
            {
                Lose();
            }
            else if (Time.time >= getUpAt)
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

        weapon.Fight(engaged, Time.time < dodgingUntil || shotBlocked);
        // Stops to listen while the player talks to them, unless there is fighting to do.
        agent.isStopped = threats.Count == 0 && talkable != null && talkable.IsTalking;
        body.SetSpeed(agent.isStopped ? 0f : agent.velocity.magnitude, engaged == null ? walkingPace : 0f);
    }

    // ----- Deciding -----

    private void Think()
    {
        WatchLeader();
        PlayerStealth stealth = leader.Player.Stealth;
        IsKeepingLow = stealth != null && stealth.IsSneaking && !MonsterAI.AnyHuntingPlayerSide();
        GatherThreats();
        if (HoldFire || IsKeepingLow)
        {
            threats.Clear();
            threatMonsters.Clear();
        }

        Speak();
        if (GetsOutOfTheWay())
        {
            return;
        }

        MonsterAI leaderTarget = leader.Target;
        var situation = new CompanionSituation
        {
            Order = Order,
            Self = transform.position,
            Leader = leader.Player.transform.position,
            FollowPoint = CompanionTactics.FormationPoint(leader.Player.transform.position, heading, formationOffset),
            HoldPoint = holdPoint,
            LeaderTarget = leaderTarget != null ? threatMonsters.IndexOf(leaderTarget) : -1,
            Threats = threats,
        };

        intent = CompanionTactics.Decide(situation, definition.Ranges);
        engaged = intent.Action == CompanionAction.Attack ? threatMonsters[intent.Target] : null;
        shotBlocked = false;
        // An archer keeps its eyes on what it shoots while it moves about.
        agent.updateRotation = !(definition.IsRanged && engaged != null);
        KeepUp();
        switch (intent.Action)
        {
            case CompanionAction.Move:
                MoveTo(intent.Point);
                break;
            case CompanionAction.Attack:
                if (definition.IsRanged)
                {
                    Shoot();
                }
                else if (Flat(engaged.transform.position - transform.position) > definition.ReachFor(0.5f))
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

    private void Shoot()
    {
        shotBlocked = !weapon.HasClearShot(engaged);
        if (Time.time >= nextArcherSideAt)
        {
            nextArcherSideAt = Time.time + 3f;
            archerSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        Vector3? step = CompanionTactics.ArcherStep(transform.position, engaged.transform.position, definition.KeepAway, definition.ReachFor(0.5f), !shotBlocked, archerSide);
        if (step.HasValue)
        {
            MoveTo(step.Value);
        }
        else
        {
            Halt();
        }
    }

    // Which way the leader is going. Standing or turning on the spot, the formation stays as it is: it doesn't spin round them.
    private void WatchLeader()
    {
        Vector3 at = leader.Player.transform.position;
        Vector3 moved = (at - leaderWasAt) / ThinkInterval;
        moved.y = 0f;
        leaderWasAt = at;
        if (moved.magnitude >= LeaderMovesFaster && moved.magnitude < 30f)
        {
            heading = Vector3.Slerp(heading, moved.normalized, 0.5f);
        }
    }

    private void ApplySpeed()
    {
        float speed = walkingPace > 0f ? walkingPace : ownSpeed * (IsKeepingLow ? definition.SneakSpeed : 1f) * hurry;
        agent.speed = Time.time < dodgingUntil ? speed * DodgeSpeed : speed;
    }

    // Falling behind: it hurries, and if that won't do, it comes up from behind the leader by other ways, where nobody sees it arrive. Only when
    // it is meant to be with them, and not on a walk or at a post.
    private void KeepUp()
    {
        hurry = 1f;
        bool following = walkingPace <= 0f && Order != CompanionOrder.Stay && threats.Count == 0;
        if (following)
        {
            float distance = Flat(leader.Player.transform.position - transform.position);
            bool moving = agent.velocity.sqrMagnitude > 0.04f || distance <= definition.Ranges.Slack * 2f;
            stuckSince = moving ? -1f : stuckSince >= 0f ? stuckSince : Time.time;
            CompanionTactics.CatchUp need = CompanionTactics.HowToCatchUp(distance, definition.HurryFrom, definition.WarpFrom,
                stuckSince >= 0f ? Time.time - stuckSince : 0f, StuckLimit);
            if (need == CompanionTactics.CatchUp.Hurrying)
            {
                hurry = definition.HurrySpeed;
            }
            else if (need == CompanionTactics.CatchUp.Warp)
            {
                Vector3 behind = leader.Player.transform.position - heading * (definition.HurryFrom + 4f);
                if (NavMesh.SamplePosition(behind, out NavMeshHit hit, 8f, agent.areaMask))
                {
                    agent.Warp(hit.position);
                    stuckSince = -1f;
                }
            }
        }

        ApplySpeed();
    }

    // A blow is coming that would reach it, and it may see it in time. True while it gets out of the way, which comes before anything else.
    private bool GetsOutOfTheWay()
    {
        if (Time.time < dodgingUntil)
        {
            return true;
        }

        bool wasDodging = dodgingUntil >= 0f;
        dodgingUntil = -1f;
        if (wasDodging)
        {
            ApplySpeed();
        }

        foreach (MonsterAI monster in threatMonsters)
        {
            AttackDefinition attack = monster.CurrentAttack;
            if (attack == null || !monster.IsClosingIn)
            {
                continue;
            }

            MonsterAttack action = monster.ActiveAction;
            bool charge = action != null && action.kind == MonsterAttackKind.Charge;
            float reach = charge ? action.chargeDistance : attack.Range + 0.4f;
            if (!CompanionTactics.InTheWay(transform.position, monster.transform.position, monster.AttackDirection, reach, charge ? ChargeArc : attack.Arc))
            {
                continue;
            }

            if (monster == watching && Time.time < watchAgainAt)
            {
                continue;
            }

            watching = monster;
            watchAgainAt = Time.time + attack.HitDelay + 0.5f;
            if (UnityEngine.Random.value >= definition.ReadsAttacks)
            {
                continue;
            }

            dodgingUntil = Time.time + DodgeSeconds;
            ApplySpeed();
            MoveTo(CompanionTactics.StepOutOfTheWay(transform.position, monster.transform.position, monster.AttackDirection));
            return true;
        }

        return false;
    }

    private void Speak()
    {
        bool hunted = false;
        foreach (CompanionThreat threat in threats)
        {
            hunted |= threat.HuntsLeader || threat.HuntsSelf;
        }

        CompanionBarks.Moment moment = barks.Think(hunted, BlowFromBehindTheLeader(), health.HealthFraction, Time.time);
        if (DialogueBox.Current == null || !CompanionBarks.MaySpeak(moment, Time.time, DialogueBox.IsSpeaking))
        {
            return;
        }

        string[] lines = CompanionBarks.LinesFor(moment, resident != null ? resident.FightLines : null, definition.FightLines);
        if (lines == null || lines.Length == 0)
        {
            return;
        }

        CompanionBarks.Spoke(moment, Time.time);
        DialogueBox.Current.Aside(talkable != null ? talkable.Speaker : new DialogueSpeaker(DisplayName, transform), lines[UnityEngine.Random.Range(0, lines.Length)]);
    }

    private bool BlowFromBehindTheLeader()
    {
        Transform player = leader.Player.transform;
        foreach (MonsterAI monster in threatMonsters)
        {
            if (!monster.IsClosingIn || !monster.IsEngaging(leader.Player.Health))
            {
                continue;
            }

            Vector3 to = monster.transform.position - player.position;
            to.y = 0f;
            if (to.sqrMagnitude <= WarnWithin * WarnWithin && Vector3.Angle(player.forward, to) >= BehindArc)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The monsters' senses pass over this one while it keeps low.</summary>
    public static bool IsHidden(Component candidate) => candidate.TryGetComponent(out Companion companion) && companion.IsKeepingLow;

    private int TakenBy(MonsterAI monster)
    {
        int taken = 0;
        foreach (Companion other in active)
        {
            if (other != this && other.engaged == monster)
            {
                taken++;
            }
        }

        return taken;
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
            threats.Add(new CompanionThreat(position, monster.IsEngaging(leaderBody), monster.IsEngaging(health), TakenBy(monster)));
        }
    }

    private bool IsFoe(MonsterAI monster)
    {
        return monster != null && monster.CurrentState != MonsterAI.State.Dead
            && monster.TryGetComponent(out Combatant other) && combatant.IsHostileTo(other.Faction)
            && monster.TryGetComponent(out Damageable body) && body.IsAlive;
    }

    // ----- Knocked down -----

    private void CountKill(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (result.Killed && damage.Source == combatant)
        {
            Kills++;
        }
    }

    private void OnKnockedDown(Damageable victim, DamageInfo hit)
    {
        getUpAt = bleedsOut ? float.PositiveInfinity : Time.time + definition.DownedSeconds;
        bleedOutAt = Time.time + definition.BleedOutSeconds;
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
        GameMessages.Post(bleedsOut ? $"{DisplayName} is down! Hold E beside them within {definition.BleedOutSeconds:0} seconds, or they are lost."
            : $"{DisplayName} is down! Hold E beside them to help them up.", MessageKind.Warning);
    }

    private void Lose()
    {
        IsLost = true;
        revive.SetAvailable(false);
        GameMessages.Post($"{DisplayName} is gone.", MessageKind.Warning);
        if (resident != null && !string.IsNullOrWhiteSpace(resident.FallenLine))
        {
            GameMessages.Post($"{DisplayName}: \"{resident.FallenLine}\"", MessageKind.Warning);
        }

        if (agent != null)
        {
            agent.enabled = false;
        }

        enabled = false;
    }

    /// <summary>The player helped them up (CompanionRevive): sooner, and with more health than getting up alone.</summary>
    public void Revive()
    {
        if (!IsDowned)
        {
            return;
        }

        GetUp(definition.ReviveHealth);
        TimesHelpedUp++;
        GameMessages.Post($"{DisplayName} is back on their feet.", MessageKind.Gain);
        string[] thanks = resident != null && resident.FightLines.helpedUp.Length > 0 ? resident.FightLines.helpedUp : definition.FightLines?.helpedUp;
        if (thanks != null && thanks.Length > 0 && DialogueBox.Current != null)
        {
            DialogueBox.Current.Aside(talkable != null ? talkable.Speaker : new DialogueSpeaker(DisplayName, transform), thanks[UnityEngine.Random.Range(0, thanks.Length)]);
        }
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
