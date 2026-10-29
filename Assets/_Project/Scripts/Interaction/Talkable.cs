using UnityEngine;

/// <summary>
/// Someone the player can talk to. Using its Interactable opens a conversation in the DialogueBox, which frames the camera
/// on them and ends when the player walks away. What is said comes from every <see cref="IConversationTopics"/> on the
/// object, in component order: the first with a line speaks it, and each adds its answers. After an answer has been said
/// the conversation opens again with the follow-up line, so the player can go on asking.
/// <para>
/// Where it belongs: beside an Interactable of the Use kind, with at least one IConversationTopics (QuestGiver,
/// PrologueCompanion, Companion, ResidentNpc).
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[AddComponentMenu("Safe Haven/Interaction/Talkable")]
public sealed class Talkable : MonoBehaviour, IInteractionName
{
    [Tooltip("The speaker's name in the dialogue box and above them. Empty: the object's name.")]
    [SerializeField] private string speakerName;
    [Tooltip("Shown in the dialogue box instead of a live portrait of this object. Optional.")]
    [SerializeField] private Sprite portrait;
    [SerializeField, Range(0.3f, 1f)] private float cameraDistance = 0.65f;
    [Tooltip("Said when the conversation opens again after an answer.")]
    [SerializeField] private string followUpLine = "Anything else?";
    [Tooltip("Raised each time the player starts talking to them, for quests. Optional.")]
    [SerializeField, RaisedByGame] private GameEventDefinition talkedEvent;

    private Interactable interactable;
    private DialogueBox box;

    public string SpeakerName => string.IsNullOrWhiteSpace(speakerName) ? gameObject.name : speakerName;
    public string InteractionName => SpeakerName;
    public DialogueSpeaker Speaker => new DialogueSpeaker(SpeakerName, transform, portrait, cameraDistance);

    /// <summary>True while a conversation with them is open.</summary>
    public bool IsTalking => box != null;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        if (box != null)
        {
            box.Close();
        }
    }

    private void OnUsed()
    {
        if (talkedEvent != null)
        {
            GameEvents.Raise(talkedEvent, interactable.Interactor);
        }

        Talk(null);
    }

    /// <summary>What they have to say now: <paramref name="line"/> (or the first part's line) and every part's answers.</summary>
    public MenuPage BuildPage(string line = null)
    {
        var page = new MenuPage { Title = SpeakerName, Body = line ?? "" };
        DialogueSpeaker speaker = Speaker;
        foreach (IConversationTopics topics in GetComponents<IConversationTopics>())
        {
            topics.AddTopics(page, speaker, FollowUp);
        }

        return page;
    }

    private void Talk(string line)
    {
        MenuPage page = BuildPage(line);
        if (string.IsNullOrWhiteSpace(page.Body) && page.Actions.Count == 0)
        {
            return;
        }

        if (box == null)
        {
            box = DialogueBox.Current;
            box.Closed += OnClosed;
        }

        box.Talk(page, Speaker);
    }

    private void FollowUp() => Talk(followUpLine);

    // The box stays open while lines follow each other; it closes when the conversation is over.
    private void OnClosed()
    {
        box.Closed -= OnClosed;
        box = null;
    }
}
