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
/// The story side of an expedition member; walking and fighting are its Companion's. Its resident definition is reused
/// when the person is rescued later. Talking to them says their line for the part of the night it is; around the camp
/// the player can also ask their topics, one after another, to get to know them. The director's rally points become
/// Companion orders (stay at the camp, at the portal; otherwise follow), and the companion stands still until the
/// expedition sets out. An alchemist also heals the player now and then during a fight.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Interactable))]
public sealed class PrologueCompanion : MonoBehaviour, IConversationTopics
{
    [SerializeField] private ResidentDefinition resident;
    [SerializeField, TextArea] private string forestLine;
    [SerializeField, TextArea] private string campLine;
    [SerializeField, TextArea] private string portalLine;
    [Tooltip("Asked around the camp (from gathering fuel until the wolves come), each once per visit to the scene.")]
    [SerializeField] private CompanionTopic[] campTopics = new CompanionTopic[0];
    [SerializeField, RaisedByGame] private GameEventDefinition talkedEvent;
    [Tooltip("Heals the player by Support Amount every Support Interval during a fight, when within 10 m.")]
    [SerializeField] private bool alchemist;
    [SerializeField, Min(1f)] private float supportInterval = 6f;
    [SerializeField, Min(0f)] private float supportAmount = 8f;

    private NavMeshAgent agent;
    private Interactable interactable;
    private Companion companion;
    private float nextSupport;
    private bool talking;
    private readonly HashSet<int> asked = new HashSet<int>();

    public ResidentDefinition Resident => resident;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        interactable = GetComponent<Interactable>();
        // Talking opens at once: no work bar.
        interactable.Instant = true;
        companion = GetComponent<Companion>();
        if (companion == null) Debug.LogError("An expedition member needs a Companion to walk and fight.", this);
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

    /// <summary>The director's rally point: stay there (in formation), or follow the player when there is none.</summary>
    public void RallyAt(Transform point, bool restore)
    {
        if (companion == null) return;
        if (point == null)
        {
            companion.Command(CompanionOrder.Follow);
            return;
        }

        Vector3 spot = point.position + companion.FormationOffset;
        companion.Command(CompanionOrder.Stay, spot);
        if (restore && agent != null && NavMesh.SamplePosition(spot, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    private void Update()
    {
        PrologueDirector expedition = PrologueDirector.Current;
        if (companion != null) companion.Paused = expedition == null || !expedition.ExpeditionStarted;
        if (!alchemist || Player.Current == null || expedition == null || !expedition.IsFighting || Time.time < nextSupport) return;
        nextSupport = Time.time + supportInterval;
        if ((companion == null || !companion.IsDowned) &&
            Vector3.Distance(transform.position, Player.Current.transform.position) < 10f)
            Player.Current.Health.Heal(supportAmount);
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
        // Its own camp topics and anything else to talk about here (the Companion's orders).
        if (withTopics)
            foreach (IConversationTopics topics in GetComponents<IConversationTopics>())
                topics.AddTopics(page, Speaker, () => Converse(AfterTopicLine, true));
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
