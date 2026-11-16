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

    private static readonly System.Collections.Generic.List<PurificationAltar> standing = new System.Collections.Generic.List<PurificationAltar>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => standing.Clear();

    private void OnEnable()
    {
        standing.Add(this);
        interactable.Completed.AddListener(OnUsed);
        Construction.Finished += OnBuilt;
    }

    private void OnDisable()
    {
        standing.Remove(this);
        interactable.Completed.RemoveListener(OnUsed);
        Construction.Finished -= OnBuilt;
    }

    private void Start() => TellSpeed();

    private void OnBuilt(Construction _) => TellSpeed();

    private void TellSpeed() => NoteBest();

    /// <summary>
    /// Tells SettlementState the purifying speed and yield of the best altar standing whole now, or none (HavenProgress, with what
    /// else stands): whichever altar started or was rebuilt last set them, and a broken or demolished one kept its bonus.
    /// </summary>
    public static void NoteBest()
    {
        // A friend's altar is told of in their game (co-op).
        if (HavenVisit.IsVisiting)
        {
            return;
        }

        float speed = 0f;
        float yield = 0f;
        foreach (PurificationAltar altar in standing)
        {
            if (altar == null || !altar.TryGetComponent(out Construction construction) || !construction.IsStanding
                || (altar.TryGetComponent(out Damageable health) && health.IsBroken))
            {
                continue;
            }

            speed = Mathf.Max(speed, construction.Sum(level => level.craftSpeed));
            yield = Mathf.Max(yield, construction.Sum(level => level.purifyYield));
        }

        GameSession.Settlement.SetPurificationBonus(speed);
        GameSession.Settlement.SetPurificationYield(yield);
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        AltarWindow.Create().Open(user, transform, menuRange);
    }
}
