using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Someone stranded in a raid who can be brought home (GDD §15.2, §21.4): found on a side path, freed with a short
/// interaction, then follows the player. Enemies can attack them; if they fall they stay behind for this raid and wait at
/// the same place next time. Reaching extraction close to the player moves them into Safe Haven (SettlementState); a
/// person not extracted never arrives at home. Once they live in Safe Haven they no longer appear in raids.
/// <para>
/// Where it belongs: on a character in a raid scene with an Interactable (deactivates on completion), a NavMeshAgent, a
/// Combatant of the Player faction and a Damageable that isn't destroyed on death. The person is a ResidentDefinition.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable), typeof(NavMeshAgent))]
public sealed class RescuableTraveler : MonoBehaviour
{
    private enum State
    {
        Waiting,
        Following,
        Fallen,
    }

    [SerializeField] private ResidentDefinition resident;
    [Tooltip("Raised when the player frees them, for quests.")]
    [SerializeField, RaisedByGame] private GameEventDefinition freedEvent;
    [Tooltip("Raised when they come home with the player, for quests.")]
    [SerializeField, RaisedByGame] private GameEventDefinition rescuedEvent;
    [Tooltip("They stop this close to the player, in meters.")]
    [SerializeField, Min(0.5f)] private float followDistance = 2.5f;
    [Tooltip("They come along when the player extracts within this distance of them, in meters.")]
    [SerializeField, Min(1f)] private float extractionReach = 12f;
    [Tooltip("Float parameter on the Animator set to the walking speed.")]
    [SerializeField] private string speedParameter = "Speed";

    private Interactable interactable;
    private NavMeshAgent agent;
    private Damageable health;
    private Animator animator;
    private int speedHash;
    private bool hasSpeed;
    private State state;
    private RaidDirector raid;

    public ResidentDefinition Resident => resident;
    public bool IsFollowing => state == State.Following;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Damageable>();
        animator = GetComponentInChildren<Animator>();
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
        }
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnFreed);
        if (health != null)
        {
            health.Died += OnFell;
        }
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

    private void Start()
    {
        raid = RaidDirector.Current;
        if (raid != null)
        {
            raid.Ended += OnRaidEnded;
        }
    }

    private void OnFreed()
    {
        state = State.Following;
        GameMessages.Post($"{resident.DisplayName}: \"{resident.RescueLine}\"", MessageKind.Gain);
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
            Vector3 target = Player.Current.transform.position;
            if (Vector3.Distance(transform.position, target) > followDistance)
            {
                agent.SetDestination(target);
            }
            else if (agent.hasPath)
            {
                agent.ResetPath();
            }

            speed = agent.velocity.magnitude;
        }

        if (hasSpeed)
        {
            animator.SetFloat(speedHash, speed, 0.1f, Time.deltaTime);
        }
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
        if (player == null || Vector3.Distance(player.transform.position, transform.position) > extractionReach)
        {
            GameMessages.Post($"{resident.DisplayName} was too far behind and is left in the forest.", MessageKind.Warning);
            return;
        }

        if (GameSession.Settlement.AddResident(resident))
        {
            raid.Report?.Rescued.Add(resident);
            GameMessages.Post($"{resident.DisplayName} steps through the portal with you.", MessageKind.Gain);
            if (rescuedEvent != null)
            {
                GameEvents.Raise(rescuedEvent, player);
            }
        }
    }
}
