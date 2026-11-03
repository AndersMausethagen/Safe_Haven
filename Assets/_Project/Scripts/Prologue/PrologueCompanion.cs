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
    [Tooltip("Raised when the player asks it, for a story to remember. Optional.")]
    public GameEventDefinition raises;
}

/// <summary>
/// The story side of an expedition member; walking and fighting are its Companion's. Its resident definition is reused
/// when the person is rescued later. In their conversation (their Talkable) they say their line for the part of the night
/// it is; around the camp the player can also ask their topics, one after another, to get to know them. The director's rally points become
/// Companion orders (stay at the camp, at the portal; otherwise follow), and the companion stands still until the
/// expedition sets out. An alchemist also heals the player now and then during a fight.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Talkable))]
public sealed class PrologueCompanion : MonoBehaviour, IConversationTopics
{
    [SerializeField] private ResidentDefinition resident;
    [Tooltip("Their line by the morning fire in the meadow, and on the road to the forest.")]
    [SerializeField, TextArea] private string meadowLine;
    [SerializeField, TextArea] private string forestLine;
    [SerializeField, TextArea] private string campLine;
    [SerializeField, TextArea] private string portalLine;
    [Tooltip("Asked around the camp (from gathering fuel until the wolves come), each once per visit to the scene.")]
    [SerializeField] private CompanionTopic[] campTopics = new CompanionTopic[0];
    [Tooltip("Asked by the morning fire: who they are and what they make of the mission, each once per visit to the scene.")]
    [SerializeField] private CompanionTopic[] morningTopics = new CompanionTopic[0];
    [Tooltip("Heals the player by Support Amount every Support Interval during a fight, when within 10 m.")]
    [SerializeField] private bool alchemist;
    [SerializeField, Min(1f)] private float supportInterval = 6f;
    [SerializeField, Min(0f)] private float supportAmount = 8f;

    private NavMeshAgent agent;
    private Companion companion;
    private float nextSupport;
    private readonly HashSet<CompanionTopic> asked = new HashSet<CompanionTopic>();

    public ResidentDefinition Resident => resident;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        companion = GetComponent<Companion>();
        if (companion == null) Debug.LogError("An expedition member needs a Companion to walk and fight.", this);
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
        if (companion != null)
        {
            companion.Paused = expedition == null || expedition.CompanionsHeld;
            companion.HoldFire = expedition != null && expedition.CompanionsHoldFire;
        }

        if (!alchemist || Player.Current == null || expedition == null || !expedition.IsFighting || Time.time < nextSupport) return;
        nextSupport = Time.time + supportInterval;
        if ((companion == null || !companion.IsDowned) &&
            Vector3.Distance(transform.position, Player.Current.transform.position) < 10f)
            Player.Current.Health.Heal(supportAmount);
    }

    /// <summary>Their line for this part of the night (when nothing before it spoke) and the camp topics not asked yet.</summary>
    public void AddTopics(MenuPage page, DialogueSpeaker speaker, Action afterAnswer)
    {
        PrologueDirector expedition = PrologueDirector.Current;
        PrologueSite talk = expedition != null ? expedition.TalkSite : PrologueSite.Portal;
        if (string.IsNullOrWhiteSpace(page.Body))
        {
            page.Body = talk == PrologueSite.Portal ? portalLine : talk == PrologueSite.Camp ? campLine :
                talk == PrologueSite.Meadow ? meadowLine : forestLine;
        }
        if (expedition == null || !expedition.AtRest) return;
        foreach (CompanionTopic topic in talk == PrologueSite.Meadow ? morningTopics : campTopics)
        {
            if (asked.Contains(topic) || string.IsNullOrWhiteSpace(topic.question)) continue;
            CompanionTopic chosen = topic;
            page.Add(new MenuAction(chosen.question, null, true, () =>
            {
                asked.Add(chosen);
                if (chosen.raises != null) GameEvents.Raise(chosen.raises);
                DialogueBox.Current.Say(speaker, chosen.answer, afterAnswer);
            }));
        }
    }
}
