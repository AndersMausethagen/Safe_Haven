using UnityEngine;

/// <summary>
/// The Purification Altar (GDD §13.5): where essence is put to work, through the AltarWindow. The player queues batches of
/// Cursed Essence here (paid when queued, from what is carried and stored); SettlementSystem then works through the queue
/// while a resident holds the purifier job (the Alchemist), even when the player is away, and puts the Purified Essence in
/// storage. The rest of the window empowers and enchants gear and deepens skills (AltarWork). The altar's upgrades make it
/// work faster and give more for the same essence: it tells SettlementState, which keeps going during raids when this object is not loaded.
/// <para>
/// Where it belongs: on the Purification Altar building prefab, next to an Interactable that doesn't deactivate.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class PurificationAltar : MonoBehaviour
{
    [SerializeField, Min(1f)] private float menuRange = 6f;

    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
        Construction.Finished += OnBuilt;
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        Construction.Finished -= OnBuilt;
    }

    private void Start() => TellSpeed();

    private void OnBuilt(Construction _) => TellSpeed();

    private void TellSpeed()
    {
        // A friend's altar is told of in their game (co-op).
        if (!HavenVisit.IsVisiting && TryGetComponent(out Construction construction))
        {
            GameSession.Settlement.SetPurificationBonus(construction.Sum(level => level.craftSpeed));
            GameSession.Settlement.SetPurificationYield(construction.Sum(level => level.purifyYield));
        }
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        AltarWindow.Create().Open(user, transform, menuRange);
    }
}
