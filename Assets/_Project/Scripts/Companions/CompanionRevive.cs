using UnityEngine;

/// <summary>
/// The "[E] Help up" on a knocked-down companion (GDD §8.4 revive): the player holds E beside them for the definition's
/// Revive Seconds and they get up with Revive Health. Made by the Companion on a child named after the person (so the
/// prompt shows their name) and active only while they are down, so it never mixes with talking to them.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "helping a companion up")]
public sealed class CompanionRevive : MonoBehaviour, IInteractionVerb
{
    private const float Reach = 1.8f;

    private Companion companion;
    private Interactable interactable;

    public string Verb => "Help up";

    /// <summary>Adds the revive spot to a companion, switched off until they are knocked down.</summary>
    public static CompanionRevive Create(Companion owner, float seconds)
    {
        var spot = new GameObject(owner.DisplayName);
        spot.SetActive(false);
        spot.transform.SetParent(owner.transform, false);
        var interactable = spot.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.GatherLow, seconds, Reach, false);
        CompanionRevive revive = spot.AddComponent<CompanionRevive>();
        revive.companion = owner;
        revive.interactable = interactable;
        return revive;
    }

    public void SetAvailable(bool available) => gameObject.SetActive(available);

    private void OnEnable() => interactable.Completed.AddListener(OnHelped);

    private void OnDisable() => interactable.Completed.RemoveListener(OnHelped);

    private void OnHelped() => companion.Revive();
}
