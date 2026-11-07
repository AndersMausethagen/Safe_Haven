using UnityEngine;

/// <summary>
/// The "[E] Help up" on a friend who is down in a shared raid (<see cref="CoopDowned"/>), as the one on a knocked-down
/// companion: hold E beside them and they get up. Made on a child of the friend's body named after them (so the prompt
/// shows their name), switched on only while they are down.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class FriendRevive : MonoBehaviour, IInteractionVerb
{
    private const float Reach = 1.8f;

    private ulong clientId;
    private Interactable interactable;

    public string Verb => "Help up";

    public static FriendRevive Create(Transform body, string friendName, ulong clientId, float seconds)
    {
        var spot = new GameObject(friendName);
        spot.SetActive(false);
        spot.transform.SetParent(body, false);
        var interactable = spot.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.GatherLow, seconds, Reach, false);
        FriendRevive revive = spot.AddComponent<FriendRevive>();
        revive.clientId = clientId;
        revive.interactable = interactable;
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

    private void OnEnable() => interactable.Completed.AddListener(OnHelped);

    private void OnDisable() => interactable.Completed.RemoveListener(OnHelped);

    private void OnHelped()
    {
        if (CoopSession.Current != null)
        {
            CoopSession.Current.Downed.HelpUp(clientId);
        }
    }
}
