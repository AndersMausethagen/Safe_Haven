using UnityEngine;

/// <summary>
/// The "[E] Help up" on a friend who is down in a shared raid (<see cref="CoopDowned"/>), as the one on a knocked-down
/// companion: hold E beside them and they get up. Made on a child of the friend's body named after them (so the prompt
/// shows their name), switched on only while they are down. What helping does is the maker's: a friend gets up through
/// CoopDowned, a friend's companion through their game (RemoteCompanion).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "helping a friend up")]
public sealed class FriendRevive : MonoBehaviour, IInteractionVerb
{
    private const float Reach = 1.8f;

    private Interactable interactable;
    private float seconds;
    private System.Action<bool> helping;
    private System.Action helped;

    public string Verb => "Help up";

    /// <summary>The help-up for a friend who is down: their game hears who helps and gets them up (CoopDowned).</summary>
    public static FriendRevive Create(Transform body, string friendName, ulong clientId, float seconds) =>
        Create(body, friendName, seconds,
            helping =>
            {
                if (CoopSession.Current != null) CoopSession.Current.Downed.SendHelping(clientId, helping, seconds);
            },
            () =>
            {
                if (CoopSession.Current != null) CoopSession.Current.Downed.HelpUp(clientId);
            });

    /// <param name="helping">Called as someone starts (true) or stops (false) helping.</param>
    /// <param name="helped">Called when the help-up is done.</param>
    public static FriendRevive Create(Transform body, string friendName, float seconds, System.Action<bool> helping, System.Action helped)
    {
        var spot = new GameObject(friendName);
        spot.SetActive(false);
        spot.transform.SetParent(body, false);
        var interactable = spot.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.GatherLow, seconds, Reach, false);
        FriendRevive revive = spot.AddComponent<FriendRevive>();
        revive.interactable = interactable;
        revive.seconds = seconds;
        revive.helping = helping;
        revive.helped = helped;
        return revive;
    }

    /// <summary>On while the friend is down; named after them for the prompt.</summary>
    public void Show(bool down, string friendName)
    {
        if (gameObject.name != friendName && !string.IsNullOrEmpty(friendName))
        {
            gameObject.name = friendName;
        }

        if (gameObject.activeSelf != down)
        {
            gameObject.SetActive(down);
        }
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnHelped);
        interactable.Started.AddListener(OnStarted);
        interactable.Cancelled.AddListener(OnCancelled);
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnHelped);
        interactable.Started.RemoveListener(OnStarted);
        interactable.Cancelled.RemoveListener(OnCancelled);
    }

    private void OnStarted() => helping?.Invoke(true);

    private void OnCancelled() => helping?.Invoke(false);

    private void OnHelped() => helped?.Invoke();
}
