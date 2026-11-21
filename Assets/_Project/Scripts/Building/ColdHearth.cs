using UnityEngine;

/// <summary>
/// The Haven's cold hearth by the portal (the quest flow review's A20, approved 2026-11-21, from PrologueAndQuestFlow_2026-11-18 §4.2,
/// "the fire you kept"): an unlit Campfire, a ruin of the Haven's own (Construction › Starts Ruined), that the player lights with the
/// ember they carried from the shelter instead of building a second fire. Using it ("[E] Set the shelter's ember") finishes the
/// campfire at once: no materials and no hammering. The Built event that raises counts for A Light in the Wood, so the campfire the
/// prologue taught isn't taught twice. It can still be repaired the ordinary way. Once A Light in the Wood is done (by this hearth or a
/// fire built elsewhere), a hearth still cold is taken away: an older save gets no extra ruin.
/// </summary>
[RequireComponent(typeof(Construction))]
[InAFriendsHaven(HavenUse.HostOnly, "the shelter's ember is the host's own first quest; a visitor doesn't see the cold hearth's prompt")]
public sealed class ColdHearth : MonoBehaviour
{
    [Tooltip("The quest it belongs to: A Light in the Wood. Lighting the hearth counts for its Build Campfire step.")]
    [SerializeField] private QuestDefinition quest;
    [Tooltip("What the prompt says.")]
    [SerializeField] private string verb = "Set the shelter's ember";
    [SerializeField, Min(0.5f)] private float reach = 2.2f;

    private Construction construction;
    private Interactable ember;
    // The fire's own parts (the flame, its light, sparks, heat haze: the model's children but its stones) and was each shown.
    private readonly System.Collections.Generic.List<(GameObject part, bool shown)> fire = new System.Collections.Generic.List<(GameObject, bool)>();
    private VisibilitySource glow;

    private void Awake()
    {
        construction = GetComponent<Construction>();
        var holder = new GameObject("Cold Hearth");
        holder.transform.SetParent(transform, false);
        var trigger = holder.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.2f;
        trigger.center = new Vector3(0f, 0.4f, 0f);
        holder.AddComponent<HearthWords>().Verb = verb;
        ember = holder.AddComponent<Interactable>();
        ember.Configure(InteractionAnimation.GatherLow, 0f, reach, true, InteractionKind.Use);
        ember.Completed.AddListener(Light);
        glow = GetComponent<VisibilitySource>();
        Transform model = transform.Find("Model");
        for (int i = 0; model != null && i < model.childCount; i++)
        {
            Transform part = model.GetChild(i);
            if (part.GetComponent<LODGroup>() == null)
            {
                fire.Add((part.gameObject, part.gameObject.activeSelf));
            }
        }
    }

    // Cold it is a ring of stones: no flame, no light, and no scaffold (the ruin's builders' frame would say it is being built).
    private void ShowCold(bool cold)
    {
        foreach ((GameObject part, bool shown) in fire)
        {
            if (part != null)
            {
                part.SetActive(!cold && shown);
            }
        }

        if (glow != null)
        {
            glow.enabled = !cold;
        }

        if (cold)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Scaffold")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }

    private void OnEnable() => GameEvents.Raised += OnGameEvent;

    private void OnDisable() => GameEvents.Raised -= OnGameEvent;

    private void Start() => Refresh();

    private void Update()
    {
        // Once a second is plenty: the hearth waits for the quest, which waits for the player.
        if (Time.frameCount % 60 == 0)
        {
            Refresh();
        }
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.QuestCompleted || gameEvent.Kind == GameEventKind.QuestStarted)
        {
            Refresh();
        }
    }

    private QuestStatus Status
    {
        get
        {
            QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
            return quest != null && log != null ? log.StatusOf(quest) : QuestStatus.NotStarted;
        }
    }

    // The ember can be set while the quest is under way and the hearth is cold; a cold hearth left after the quest is done goes.
    private void Refresh()
    {
        if (construction == null || ember == null)
        {
            return;
        }

        bool cold = !construction.IsStanding;
        ShowCold(cold);
        QuestStatus status = Status;
        if (cold && (status == QuestStatus.Completed || status == QuestStatus.ReadyToTurnIn) && Application.isPlaying && !HavenVisit.IsVisiting)
        {
            SaveableEntity.RemoveForGood(gameObject);
            return;
        }

        ember.gameObject.SetActive(cold && status == QuestStatus.Active && !HavenVisit.IsVisiting);
    }

    private void Light()
    {
        if (construction.IsStanding)
        {
            return;
        }

        GameMessages.Post("You tip the ember from the shelter into the cold hearth. It takes, as if it had been waiting.", MessageKind.Gain);
        construction.Finish(ember.Interactor);
        ember.gameObject.SetActive(false);
        ShowCold(false);
    }

    // The prompt's words for the hearth: its name and what pressing does.
    private sealed class HearthWords : MonoBehaviour, IInteractionVerb, IInteractionName
    {
        public string Verb { get; set; }
        public string InteractionName => "Cold hearth";
    }
}
