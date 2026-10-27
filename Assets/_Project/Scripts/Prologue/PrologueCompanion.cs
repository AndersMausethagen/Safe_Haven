using UnityEngine;
using UnityEngine.AI;

/// <summary>Lightweight story escort. Its resident definition is reused when the person is rescued later.</summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Interactable))]
public sealed class PrologueCompanion : MonoBehaviour
{
    [SerializeField] private ResidentDefinition resident;
    [SerializeField] private Vector3 formationOffset;
    [SerializeField, TextArea] private string forestLine;
    [SerializeField, TextArea] private string campLine;
    [SerializeField, TextArea] private string portalLine;
    [SerializeField, RaisedByGame] private GameEventDefinition talkedEvent;
    [SerializeField] private bool soldier;
    [SerializeField] private bool alchemist;
    [SerializeField, Min(1f)] private float supportInterval = 6f;
    [SerializeField, Min(0f)] private float supportAmount = 8f;

    private NavMeshAgent agent;
    private Interactable interactable;
    private Animator animator;
    private Transform rally;
    private float nextPath;
    private float nextSupport;
    private bool hasSpeed;
    private PlayerCameraController dialogueCamera;
    private bool talking;
    private static readonly int Speed = Animator.StringToHash("Speed");

    public ResidentDefinition Resident => resident;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        interactable = GetComponent<Interactable>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                hasSpeed |= parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float;
    }

    private void OnEnable()
    {
        // A standard giver owns its interaction, including confirmation and camera framing.
        if (GetComponent<QuestGiver>() == null) GetComponent<Interactable>().Completed.AddListener(Talk);
    }
    private void OnDisable()
    {
        if (interactable != null) interactable.Completed.RemoveListener(Talk);
        if (talking) ActionMenuWindow.Current?.Close();
        dialogueCamera?.EndDialogue(transform);
    }

    public void RallyAt(Transform point, bool restore)
    {
        rally = point;
        if (restore && agent != null && point != null &&
            NavMesh.SamplePosition(point.position + formationOffset, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    private void Update()
    {
        if (Player.Current == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        var expedition = PrologueDirector.Current;
        if (expedition == null || !expedition.ExpeditionStarted)
        {
            agent.isStopped = true;
            if (hasSpeed) animator.SetFloat(Speed, 0f);
            return;
        }
        agent.isStopped = ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpen;
        if (Time.time >= nextPath)
        {
            nextPath = Time.time + 0.3f;
            Vector3 destination = (rally != null ? rally.position : Player.Current.transform.position) + formationOffset;
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }
        if (hasSpeed) animator.SetFloat(Speed, agent.velocity.magnitude, 0.15f, Time.deltaTime);
        PrologueDirector director = PrologueDirector.Current;
        if (director == null || !director.IsFighting || Time.time < nextSupport) return;
        nextSupport = Time.time + supportInterval;
        if (alchemist && Vector3.Distance(transform.position, Player.Current.transform.position) < 10f)
            Player.Current.Health.Heal(supportAmount);
        if (!soldier) return;
        foreach (Damageable enemy in director.Enemies)
        {
            if (enemy == null || !enemy.IsAlive || Vector3.Distance(transform.position, enemy.transform.position) > 3.5f) continue;
            transform.LookAt(new Vector3(enemy.transform.position.x, transform.position.y, enemy.transform.position.z));
            enemy.ApplyDamage(new DamageInfo(null, null, supportAmount, DamageType.Physical, false,
                enemy.transform.position, enemy.transform.position - transform.position));
            break;
        }
    }

    private void Talk()
    {
        if (resident == null || PrologueDirector.Current == null) return;
        var director = PrologueDirector.Current;
        PrologueStep step = PrologueDirector.Current.Step;
        string line = step >= PrologueStep.ReachPortal ? portalLine
            : step >= PrologueStep.GatherFuel ? campLine : forestLine;
        GameEvents.Raise(talkedEvent, interactable.Interactor);
        if (ActionMenuWindow.Current == null)
        {
            Debug.LogError("Expedition conversations require an ActionMenuWindow.", this);
            return;
        }
        dialogueCamera = FindAnyObjectByType<PlayerCameraController>();
        ActionMenuWindow.Current.Open(() => {
            return new MenuPage { Title = resident.DisplayName, Body = line };
        }, interactable.Interactor, transform, 4f, () => {
            talking = false;
            dialogueCamera?.EndDialogue(transform);
        });
        talking = true;
        dialogueCamera?.BeginDialogue(transform, director.Definition.DialogueCameraDistance);
    }
}
