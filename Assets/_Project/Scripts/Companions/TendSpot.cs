using UnityEngine;

/// <summary>The Bandage prompt on a hurt follower (WoundTending): a short piece of work for the player, with a healing item.</summary>
public sealed class TendSpot : MonoBehaviour, IInteractionVerb, IInteractionCondition
{
    private const float Reach = 2f;

    private WoundTending patient;
    private Interactable interactable;

    public string Verb => "Bandage";

    public static TendSpot Create(WoundTending patient, string name)
    {
        var holder = new GameObject(name);
        holder.SetActive(false);
        holder.transform.SetParent(patient.transform, false);
        var interactable = holder.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.GatherLow, AllyCareSettings.Current.TendSeconds, Reach, false);
        TendSpot spot = holder.AddComponent<TendSpot>();
        spot.patient = patient;
        spot.interactable = interactable;
        return spot;
    }

    public void SetAvailable(bool available)
    {
        if (gameObject.activeSelf != available)
        {
            gameObject.SetActive(available);
        }
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        reason = null;
        if (user == null || !user.TryGetComponent(out ItemInventory bag) || WoundTending.MildestHealing(bag.Container) == null)
        {
            reason = "You have nothing to bandage them with.";
            return false;
        }

        return true;
    }

    private void OnEnable() => interactable.Completed.AddListener(OnTended);

    private void OnDisable() => interactable.Completed.RemoveListener(OnTended);

    private void OnTended()
    {
        GameObject user = interactable.Interactor;
        if (user == null || !user.TryGetComponent(out ItemInventory bag))
        {
            return;
        }

        ItemDefinition item = WoundTending.MildestHealing(bag.Container);
        if (item == null || bag.Container.Remove(item, 1) < 1)
        {
            return;
        }

        float healed = patient.TendedWith(item, user.GetComponent<Damageable>());
        GameMessages.Post($"You bandage {name} with a {item.DisplayName} (+{Mathf.RoundToInt(healed)} health).", MessageKind.Gain);
    }
}
