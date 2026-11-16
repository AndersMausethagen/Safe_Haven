using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A place worth a second look (a raid event site, a rune stone): the first time the player comes near, the view eases in a
/// little and the player thinks aloud about it, how it looks and what it might be for, as a subtitle that leaves the controls
/// and the camera to them (DialogueBox.Aside). Not in a fight, not over a conversation. With an Interactable beside it (the
/// world's stones) the player can stop and look again ("Look closer"), which brings the next thought. A raid site has its
/// own use (its choices), so its CuriousPlace sits on its Mark, with no Interactable.
/// Which places have been noticed: the world's stones (those with an Interactable) in the character's journal, so they keep
/// it across loads (JournalRecord.Noticed); a raid's sites, new each raid, only for the session.
/// </summary>
[DisallowMultipleComponent]
[InAFriendsHaven(HavenUse.Anyone, "a look, the visitor's own")]
public sealed class CuriousPlace : MonoBehaviour, IInteractionName, IInteractionVerb
{
    private static readonly HashSet<string> Noticed = new HashSet<string>();

    // Entering play mode doesn't reload scripts in this project, so the session's notices are forgotten here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Noticed.Clear();

    [Tooltip("What it is called on the prompt.")]
    [SerializeField] private string placeName = "";
    [Tooltip("What the player thinks on seeing it, one at a time: the first on noticing it, the next on each closer look.")]
    [SerializeField, TextArea(2, 4)] private string[] thoughts = System.Array.Empty<string>();
    [Tooltip("Metres from it at which the player notices it.")]
    [SerializeField, Min(1f)] private float noticeRadius = 7f;
    [Tooltip("How far in the view comes while the thought lasts: its distance times this.")]
    [SerializeField, Range(0.3f, 1f)] private float closeness = 0.7f;
    [SerializeField, Min(0.1f)] private float easeSeconds = 1.2f;

    private int next;
    private float holdUntil = -1f;
    private float weight;
    private Interactable interactable;

    public string InteractionName => placeName;
    public string Verb => "Look closer";

    /// <summary>What it says, for the builder and tests.</summary>
    public IReadOnlyList<string> Thoughts => thoughts;

    // Made once: built anew it was two strings a frame for every such place.
    private string key;
    private string Key => key ??= $"{gameObject.scene.name}/{name}/{Mathf.RoundToInt(transform.position.x)},{Mathf.RoundToInt(transform.position.z)}";

    private void Awake()
    {
        if (TryGetComponent(out interactable))
        {
            interactable.Completed.AddListener(LookCloser);
        }
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(LookCloser);
        }
    }

    /// <summary>Stops to look at it again: the next thought, the view brought in.</summary>
    public void LookCloser() => Muse();

    private void Update()
    {
        Player player = Player.Current;
        if (player != null && thoughts.Length > 0 && (player.transform.position - transform.position).sqrMagnitude <= noticeRadius * noticeRadius
            && !WasNoticed() && MayThink())
        {
            NoteNoticed();
            Muse();
        }

        float wanted = Time.time < holdUntil ? 1f : 0f;
        if (Mathf.Approximately(weight, wanted) && wanted == 0f)
        {
            return;
        }

        weight = Mathf.MoveTowards(weight, wanted, Time.deltaTime / easeSeconds);
        float eased = weight * weight * (3f - 2f * weight);
        PlayerCameraController view = PlayerCameraController.Current;
        if (view != null)
        {
            view.SetCloseness(this, Mathf.Lerp(1f, closeness, eased));
        }
    }

    private void OnDisable()
    {
        weight = 0f;
        holdUntil = -1f;
        PlayerCameraController view = PlayerCameraController.Current;
        if (view != null)
        {
            view.SetCloseness(this, 1f);
        }
    }

    // The world's stones are the character's to remember; a raid site only this session.
    private bool Remembered => interactable != null && GameSession.Progress != null;

    private bool WasNoticed() => Remembered ? GameSession.Progress.Journal.HasNoticed(Key) : Noticed.Contains(Key);

    private void NoteNoticed()
    {
        if (Remembered)
        {
            GameSession.Progress.Journal.NoteNoticed(Key);
        }
        else
        {
            Noticed.Add(Key);
        }
    }

    private static bool MayThink() => DialogueBox.Current != null && !DialogueBox.IsSpeaking && !GameStateManager.InCombat;

    private void Muse()
    {
        if (thoughts.Length == 0 || !MayThink())
        {
            return;
        }

        string line = thoughts[next % thoughts.Length];
        next++;
        // Held while it is read: about the time a subtitle stays up, at least a few seconds.
        holdUntil = Time.time + Mathf.Clamp(2.5f + line.Length * 0.045f, 4f, 9f);
        DialogueBox.Current.Bark(DialogueSpeaker.Player(), $"<i>{line}</i>", () => holdUntil = Mathf.Min(holdUntil, Time.time + 0.5f));
    }
}
