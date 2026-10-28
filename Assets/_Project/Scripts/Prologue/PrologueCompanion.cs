using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Something to ask a companion by the fire, and what they answer.</summary>
[Serializable]
public sealed class CompanionTopic
{
    public string question;
    [TextArea(2, 5)] public string answer;
}

/// <summary>
/// Lightweight story escort. Its resident definition is reused when the person is rescued later. Talking to them says
/// their line for the part of the night it is; around the camp the player can also ask their topics, one after another,
/// to get to know them.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Interactable))]
public sealed class PrologueCompanion : MonoBehaviour, IConversationTopics
{
    [SerializeField] private ResidentDefinition resident;
    [SerializeField] private Vector3 formationOffset;
    [SerializeField, TextArea] private string forestLine;
    [SerializeField, TextArea] private string campLine;
    [SerializeField, TextArea] private string portalLine;
    [Tooltip("Asked around the camp (from gathering fuel until the wolves come), each once per visit to the scene.")]
    [SerializeField] private CompanionTopic[] campTopics = new CompanionTopic[0];
    [SerializeField, RaisedByGame] private GameEventDefinition talkedEvent;
    [SerializeField] private bool soldier;
    [SerializeField] private bool alchemist;
    [Tooltip("Shoots the nearest enemy within Archer Range during a fight (Runa).")]
    [SerializeField] private bool archer;
    [SerializeField, Min(1f)] private float archerRange = 15f;
    [SerializeField] private SoundDefinition shotSound;
    [SerializeField, Min(1f)] private float supportInterval = 6f;
    [SerializeField, Min(0f)] private float supportAmount = 8f;

    private NavMeshAgent agent;
    private Interactable interactable;
    private Animator animator;
    private Transform rally;
    private float nextPath;
    private float nextSupport;
    private bool hasSpeed;
    private bool talking;
    private readonly HashSet<int> asked = new HashSet<int>();
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
        if (talking && DialogueBox.IsOpen) DialogueBox.Current.Close();
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
        agent.isStopped = ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpen || DialogueBox.IsOpen;
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
        if (archer && Shoot(director)) return;
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

    // The nearest enemy in range takes an arrow.
    private bool Shoot(PrologueDirector director)
    {
        Damageable target = null;
        float best = archerRange * archerRange;
        foreach (Damageable enemy in director.Enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                target = enemy;
            }
        }

        if (target == null) return false;
        transform.LookAt(new Vector3(target.transform.position.x, transform.position.y, target.transform.position.z));
        AudioManager.Play(shotSound, transform.position);
        target.ApplyDamage(new DamageInfo(null, null, supportAmount, DamageType.Physical, false,
            target.transform.position + Vector3.up, target.transform.position - transform.position));
        return true;
    }

    private void Talk()
    {
        if (resident == null || PrologueDirector.Current == null) return;
        PrologueStep step = PrologueDirector.Current.Step;
        string line = step >= PrologueStep.ReachPortal ? portalLine
            : step >= PrologueStep.GatherFuel ? campLine : forestLine;
        GameEvents.Raise(talkedEvent, interactable.Interactor);
        Converse(line, true);
    }

    // The line, then the topics not asked yet as answers; each answer is said and the rest offered again.
    private void Converse(string line, bool withTopics)
    {
        talking = true;
        var page = new MenuPage { Title = resident.DisplayName, Body = line };
        if (withTopics) AddTopics(page, Speaker, () => Converse(AfterTopicLine, true));
        DialogueBox.Current.Talk(page, Speaker, () => talking = false);
    }

    private const string AfterTopicLine = "Anything else?";

    /// <summary>Adds the camp topics not asked yet to a conversation (also a quest giver's, such as Sigrid's).</summary>
    public void AddTopics(MenuPage page, DialogueSpeaker speaker, Action afterAnswer)
    {
        PrologueStep step = PrologueDirector.Current != null ? PrologueDirector.Current.Step : PrologueStep.Finished;
        if (step < PrologueStep.GatherFuel || step > PrologueStep.CatchBreath) return;
        for (int i = 0; i < campTopics.Length; i++)
        {
            int topic = i;
            if (asked.Contains(topic) || string.IsNullOrWhiteSpace(campTopics[topic].question)) continue;
            page.Add(new MenuAction(campTopics[topic].question, null, true, () =>
            {
                asked.Add(topic);
                DialogueBox.Current.Say(speaker, campTopics[topic].answer, afterAnswer);
            }));
        }
    }

    private DialogueSpeaker Speaker =>
        new DialogueSpeaker(resident.DisplayName, transform, null, PrologueDirector.Current.Definition.DialogueCameraDistance);
}
