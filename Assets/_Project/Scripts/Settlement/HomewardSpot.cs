using UnityEngine;

/// <summary>
/// The "Send home" prompt on a freed person standing in an open extraction stone's circle (RescuableTraveler.SendHome): they go home
/// ahead through the stone, and the player stays on (the user, 2026-11-22: "allow a rescued NPC to be sent home from an extraction
/// point... we can send the NPC through, but we can choose to stay"). A rescue no longer has to end the raid.
/// </summary>
[InAFriendsHaven(HavenUse.NotInAHaven, "a raid's way out; they join the guest's own Haven (GameSession.Home)")]
public sealed class HomewardSpot : MonoBehaviour, IInteractionVerb, IInteractionCondition
{
    private const float Reach = 2.5f;
    private const float Seconds = 1.5f;

    private RescuableTraveler traveler;
    private Interactable interactable;

    public static HomewardSpot Create(RescuableTraveler traveler, string name)
    {
        var holder = new GameObject(name);
        holder.SetActive(false);
        holder.transform.SetParent(traveler.transform, false);
        var interactable = holder.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.None, Seconds, Reach, false);
        HomewardSpot spot = holder.AddComponent<HomewardSpot>();
        spot.traveler = traveler;
        spot.interactable = interactable;
        return spot;
    }

    public string Verb => traveler != null && traveler.StoneAround() != null && traveler.StoneAround().IsPortal ? "Send through the portal" : "Send home";

    public void SetAvailable(bool available)
    {
        if (gameObject.activeSelf != available)
        {
            gameObject.SetActive(available);
        }
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        if (traveler == null || traveler.StoneAround() == null)
        {
            reason = "Bring them into an open stone's circle.";
            return false;
        }

        reason = null;
        return true;
    }

    private void OnEnable() => interactable.Completed.AddListener(OnSent);

    private void OnDisable() => interactable.Completed.RemoveListener(OnSent);

    private void OnSent()
    {
        if (traveler != null)
        {
            traveler.SendHome(interactable.Interactor);
        }
    }
}
