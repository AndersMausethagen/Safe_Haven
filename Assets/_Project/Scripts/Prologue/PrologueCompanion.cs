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
/// expedition sets out. An alchemist (Ingrid, Sigrid) heals the player when they are badly hurt and close by, with a word.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Talkable))]
public sealed class PrologueCompanion : MonoBehaviour, IConversationTopics, IQuestPerson
{
    [SerializeField, QuestIdentity] private ResidentDefinition resident;
    [Tooltip("Their line by the morning fire in the meadow, and on the road to the forest.")]
    [SerializeField, TextArea] private string meadowLine;
    [SerializeField, TextArea] private string forestLine;
    [SerializeField, TextArea] private string campLine;
    [SerializeField, TextArea] private string portalLine;
    [Tooltip("Asked around the camp (from gathering fuel until the wolves come), each once per visit to the scene.")]
    [SerializeField] private CompanionTopic[] campTopics = new CompanionTopic[0];
    [Tooltip("Asked by the morning fire: who they are and what they make of the mission, each once per visit to the scene.")]
    [SerializeField] private CompanionTopic[] morningTopics = new CompanionTopic[0];
    [Tooltip("Heals the player when their health is down to Low Health, within Support Reach, at most every Support Interval.")]
    [SerializeField] private bool alchemist;
    [Tooltip("Share of the player's health at or below which an alchemist heals them.")]
    [SerializeField, Range(0.1f, 0.9f)] private float lowHealth = 0.5f;
    [Tooltip("Share of the player's most health given back each time.")]
    [SerializeField, Range(0.05f, 1f)] private float healShare = 0.35f;
    [SerializeField, Min(1f)] private float supportInterval = 10f;
    [SerializeField, Min(1f)] private float supportReach = 12f;
    [Tooltip("What they say as they heal, one at random.")]
    [SerializeField, TextArea] private string[] healLines =
    {
        "Hold still. Drink this.",
        "You're bleeding. Here.",
        "Not yet, you don't. Drink.",
    };

    private NavMeshAgent agent;
    private Companion companion;
    private float nextSupport;
    private readonly HashSet<CompanionTopic> asked = new HashSet<CompanionTopic>();

    public ResidentDefinition Resident => resident;

    private void Awake()
    {
        WalkerFootprints.AddTo(gameObject);
        agent = GetComponent<NavMeshAgent>();
        companion = GetComponent<Companion>();
        if (companion == null) Debug.LogError("An expedition member needs a Companion to walk and fight.", this);
    }

    /// <summary>The director's rally point: stay there (in formation), or follow the player when there is none. Restoring
    /// a follow chapter (a load, a revival), they are put beside <paramref name="followFrom"/>, where the player is put,
    /// rather than walking the road from where the scene left them.</summary>
    public void RallyAt(Transform point, bool restore, Transform followFrom = null)
    {
        if (companion == null) return;
        if (point == null)
        {
            if (restore && followFrom != null && agent != null &&
                NavMesh.SamplePosition(followFrom.position + companion.FormationOffset, out NavMeshHit near, 5f, NavMesh.AllAreas))
                agent.Warp(near.position);
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

        if (alchemist)
        {
            Support();
        }
    }

    /// <summary>Heals the player when they are badly hurt and near, with a word; then not again for a while.</summary>
    private void Support()
    {
        Player player = Player.Current;
        if (player == null || Time.time < nextSupport || !gameObject.activeInHierarchy || companion != null && companion.IsDowned)
        {
            return;
        }

        Damageable health = player.Health;
        if (!health.IsAlive || health.HealthFraction > lowHealth ||
            Vector3.Distance(transform.position, player.transform.position) > supportReach)
        {
            return;
        }

        nextSupport = Time.time + supportInterval;
        health.Heal(health.MaxHealth * healShare);
        if (healLines.Length > 0 && DialogueBox.Current != null && !DialogueBox.IsOpen && TryGetComponent(out Talkable talkable))
        {
            DialogueBox.Current.Aside(talkable.Speaker, healLines[UnityEngine.Random.Range(0, healLines.Length)]);
        }
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
