using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Someone stranded in a raid who can be brought home (GDD §15.2, §21.4): found on a side path, freed with a short
/// interaction, then follows the player. Enemies can attack them; if they fall they stay behind for this raid and wait at
/// the same place next time. Still following when the player extracts, however far behind (the user, 2026-11-15: Ingrid
/// was left in the forest for not standing in the circle), they move into Safe Haven (SettlementState); a person not
/// extracted never arrives at home. Once they live in Safe Haven they no longer appear in raids. A Quest Waypoint under
/// them marks them on the map while they wait to be found, and is put away while they follow.
/// <para>
/// Where it belongs: on a character in a raid scene with an Interactable (deactivates on completion), a NavMeshAgent, a
/// Combatant of the Ally faction and a Damageable that isn't destroyed on death. The person is a ResidentDefinition.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable), typeof(NavMeshAgent))]
[InAFriendsHaven(HavenUse.NotInAHaven, "found on a raid; they join the guest's own Haven (HavenVisit.RescuedHome)")]
public sealed class RescuableTraveler : MonoBehaviour, IInteractionName
{
    private enum State
    {
        Waiting,
        Following,
        Fallen,
    }

    [SerializeField, QuestIdentity] private ResidentDefinition resident;
    [Tooltip("Raised when the player frees them, for quests.")]
    [SerializeField, RaisedByGame] private GameEventDefinition freedEvent;
    [Tooltip("Raised when they come home with the player, for quests.")]
    [SerializeField, RaisedByGame] private GameEventDefinition rescuedEvent;
    [Tooltip("They stop this close to the player, in meters.")]
    [SerializeField, Min(0.5f)] private float followDistance = 2.5f;
    [Tooltip("Float parameter on the Animator set to the walking speed.")]
    [SerializeField] private string speedParameter = "Speed";

    [Tooltip("The player's answer to the rescue line. Empty: the freed person speaks alone.")]
    [SerializeField, TextArea] private string playerReply = "Stay close to me. We're getting out of here.";

    private Interactable interactable;
    private NavMeshAgent agent;
    private Damageable health;
    private Animator animator;
    private int speedHash;
    private bool hasSpeed;
    private State state;
    private RaidDirector raid;
    private WoundTending tending;
    private float baseSpeed;
    private float dodgingUntil;
    private MonsterAI watching;
    private float watchAgainAt;
    private float nextSpotAt;
    private Vector3 spot;

    private const float ChargeArc = 40f;
    private const float DodgeSeconds = 0.6f;
    private const float DodgeSpeed = 1.7f;
    private const float SpotEvery = 0.5f;

    public ResidentDefinition Resident => resident;
    public bool IsFollowing => state == State.Following;
    // Hidden until freed: the player doesn't know who it is yet.
    public string InteractionName => resident == null ? null : IsFollowing ? resident.DisplayName : "Someone hiding";

    /// <summary>
    /// Brings someone out of a raid event already following the player, whoever the prefab was made for. The copy starts
    /// under an inactive parent so that it doesn't wake as the prefab's own person.
    /// </summary>
    public static RescuableTraveler Spawn(RescuableTraveler prefab, ResidentDefinition who, Vector3 position, Transform parent)
    {
        var holder = new GameObject("Traveler");
        holder.SetActive(false);
        RescuableTraveler traveler = Instantiate(prefab, position, Quaternion.identity, holder.transform);
        traveler.resident = who;
        traveler.transform.SetParent(parent, true);
        Destroy(holder);
        traveler.OnFreed();
        traveler.interactable.enabled = false;
        return traveler;
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Damageable>();
        animator = GetComponentInChildren<Animator>();
        baseSpeed = agent != null ? agent.speed : 3.5f;
        speedHash = Animator.StringToHash(speedParameter);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                hasSpeed |= parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float;
            }
        }

        // Already living in Safe Haven: nobody to find here any more.
        if (resident == null || GameSession.Settlement.HasResident(resident))
        {
            gameObject.SetActive(false);
            return;
        }

        tending = WoundTending.For(gameObject, resident.DisplayName);
        tending.enabled = false;
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnFreed);
        if (health != null)
        {
            health.Died += OnFell;
        }

        ListenToRaid();
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnFreed);
        if (health != null)
        {
            health.Died -= OnFell;
        }

        if (raid != null)
        {
            raid.Ended -= OnRaidEnded;
            raid = null;
        }
    }

    // Start covers the scene's first frame, before the raid's director may be up; OnEnable covers being hidden and shown again.
    private void Start() => ListenToRaid();

    private void ListenToRaid()
    {
        if (raid == null && RaidDirector.Current != null)
        {
            raid = RaidDirector.Current;
            raid.Ended += OnRaidEnded;
        }
    }

    // A rescue is spoken, face to face, with the player's answer (DialogueBox.Say keeps it to a subtitle with the controls
    // left free while creatures are hunting). Only without a dialogue box does it fall back to a message.
    private void Thank()
    {
        if (DialogueBox.Current == null)
        {
            GameMessages.Post($"{resident.DisplayName}: \"{resident.RescueLine}\"", MessageKind.Gain);
            return;
        }

        var speaker = new DialogueSpeaker(resident.DisplayName, transform, resident.Portrait);
        DialogueBox.Current.Say(speaker, resident.RescueLine, () =>
        {
            if (!string.IsNullOrWhiteSpace(playerReply) && DialogueBox.Current != null)
            {
                DialogueBox.Current.Say(DialogueSpeaker.Player(), playerReply);
            }
        });
    }

    private void OnFreed()
    {
        state = State.Following;
        // At the player's side now: the map needn't point at them, and the way out is what to look for.
        foreach (QuestWaypoint mark in GetComponentsInChildren<QuestWaypoint>(true))
        {
            mark.enabled = false;
        }

        if (tending != null)
        {
            tending.enabled = true;
        }

        Thank();
        QuestDeeds.Raise(GameEventKind.PersonFreed, resident, interactable.Interactor);
        if (freedEvent != null)
        {
            GameEvents.Raise(freedEvent, interactable.Interactor);
        }
    }

    private void Update()
    {
        float speed = 0f;
        if (state == State.Following && Player.Current != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            if (tending != null && tending.IsBandaging)
            {
                agent.ResetPath();
            }
            else if (!SteppingAside())
            {
                agent.speed = baseSpeed;
                Vector3 target = WhereToStand(Player.Current.transform.position, out float slack);
                if (Vector3.Distance(transform.position, target) > slack)
                {
                    agent.SetDestination(target);
                }
                else if (agent.hasPath)
                {
                    agent.ResetPath();
                }
            }

            speed = agent.velocity.magnitude;
        }

        if (hasSpeed)
        {
            animator.SetFloat(speedHash, speed, 0.1f, Time.deltaTime);
        }
    }

    // Out of the fight (the user, 2026-11-14: she "just stands still and gets hit by all attacks"): with enemies about, keep
    // behind the player on the far side from the nearest one, out of the blows aimed at the player; otherwise close behind.
    private Vector3 WhereToStand(Vector3 player, out float slack)
    {
        AllyCareSettings care = AllyCareSettings.Current;
        MonsterAI nearest = NearestEnemy(player, care.FightRadius);
        if (nearest == null)
        {
            slack = followDistance;
            return player;
        }

        slack = 1f;
        if (Time.time >= nextSpotAt)
        {
            nextSpotAt = Time.time + SpotEvery;
            Vector3 away = player - nearest.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -Player.Current.transform.forward;
            Vector3 wanted = player + away * care.KeepClear;
            spot = NavMesh.SamplePosition(wanted, out NavMeshHit hit, 3f, agent.areaMask) ? hit.position : player;
        }

        return spot;
    }

    // A blow that would reach them, seen in time (Read Attacks): a quick step out of its line. True while stepping.
    private bool SteppingAside()
    {
        if (Time.time < dodgingUntil)
        {
            return true;
        }

        AllyCareSettings care = AllyCareSettings.Current;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            AttackDefinition attack = monster != null ? monster.CurrentAttack : null;
            if (attack == null || !monster.IsClosingIn || (monster.transform.position - transform.position).sqrMagnitude > 144f)
            {
                continue;
            }

            MonsterAttack action = monster.ActiveAction;
            bool charge = action != null && action.kind == MonsterAttackKind.Charge;
            float reach = charge ? action.chargeDistance : attack.Range + 0.6f;
            if (!CompanionTactics.InTheWay(transform.position, monster.transform.position, monster.AttackDirection, reach, charge ? ChargeArc : attack.Arc)
                || (monster == watching && Time.time < watchAgainAt))
            {
                continue;
            }

            watching = monster;
            watchAgainAt = Time.time + attack.HitDelay + 0.5f;
            if (Random.value >= care.ReadsAttacks)
            {
                continue;
            }

            // Sideways off the blow's line, to the side they already stand on.
            Vector3 aim = monster.AttackDirection;
            aim.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, aim.sqrMagnitude > 0.01f ? aim.normalized : transform.forward);
            if (Vector3.Dot(transform.position - monster.transform.position, side) < 0f)
            {
                side = -side;
            }

            if (NavMesh.SamplePosition(transform.position + side * care.Sidestep, out NavMeshHit hit, 1.5f, agent.areaMask))
            {
                agent.speed = baseSpeed * DodgeSpeed;
                agent.SetDestination(hit.position);
                dodgingUntil = Time.time + DodgeSeconds;
                return true;
            }
        }

        return false;
    }

    private static MonsterAI NearestEnemy(Vector3 around, float metres)
    {
        MonsterAI nearest = null;
        float best = metres * metres;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster == null || !monster.isActiveAndEnabled || monster.Definition == null || !monster.Definition.Faction.WillAttack(Faction.Player))
            {
                continue;
            }

            float distance = (monster.transform.position - around).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = monster;
            }
        }

        return nearest;
    }

    private void OnFell(Damageable victim, DamageInfo killingBlow)
    {
        state = State.Fallen;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        GameMessages.Post($"{resident.DisplayName}: \"{resident.FallenLine}\"", MessageKind.Warning);
        gameObject.SetActive(false);
    }

    private void OnRaidEnded(RaidSession session)
    {
        if (state != State.Following || session.Outcome != RaidOutcome.Extracted || (health != null && !health.IsAlive))
        {
            return;
        }

        GameObject player = raid != null ? raid.Player : null;
        if (player == null)
        {
            return;
        }

        // Rescued on a friend's raid (co-op): they make their home in this player's own Haven, not the friend's.
        if (HavenVisit.IsVisiting)
        {
            if (HavenVisit.RescuedHome(resident))
            {
                raid.Report?.Rescued.Add(resident);
                GameMessages.Post($"{resident.DisplayName} steps through the portal with you, and will be waiting in your own Haven.", MessageKind.Gain);
                ResidentNpc.TeachWhatTheyKnow(resident, player);
                QuestDeeds.Raise(GameEventKind.PersonRescued, resident, player);
                if (rescuedEvent != null)
                {
                    GameEvents.Raise(rescuedEvent, player);
                }
            }

            return;
        }

        if (GameSession.Settlement.AddResident(resident))
        {
            raid.Report?.Rescued.Add(resident);
            GameMessages.Post($"{resident.DisplayName} steps through the portal with you.", MessageKind.Gain);
            ResidentNpc.TeachWhatTheyKnow(resident, player);
            QuestDeeds.Raise(GameEventKind.PersonRescued, resident, player);
            if (rescuedEvent != null)
            {
                GameEvents.Raise(rescuedEvent, player);
            }
        }
    }
}
