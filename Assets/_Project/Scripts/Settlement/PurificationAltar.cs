using System.Text;
using UnityEngine;

/// <summary>
/// The Purification Altar (GDD §13.5): turns extracted Cursed Essence into Purified Essence. The player queues batches
/// here (each costs SettlementSettings.CursedPerBatch Cursed Essence, paid when queued, from what is carried and stored);
/// SettlementSystem then works through the queue while a resident holds the purifier job (the Alchemist), even when the
/// player is away, and puts the Purified Essence in storage.
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
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => BuildPage(user), user, transform, menuRange);
        }
    }

    private static MenuPage BuildPage(GameObject user)
    {
        SettlementSettings settings = SettlementSettings.Current;
        SettlementState settlement = GameSession.Settlement;
        ResourceDefinition cursed = settings.CursedEssence;
        ResourceDefinition purified = settings.PurifiedEssence;
        var page = new MenuPage { Title = "Purification Altar" };
        if (cursed == null || purified == null)
        {
            page.Body = "The altar has no essences set (Systems Editor > Settlement).";
            return page;
        }

        MaterialPool pool = MaterialPool.For(user);
        int available = pool.Have(cursed);
        bool staffed = settlement.HasWorker(settings.PurifierJob);
        string worker = settings.PurifierJob != null ? settings.PurifierJob.DisplayName : "someone";
        var body = new StringBuilder();
        body.Append($"Turns {settings.CursedPerBatch} {cursed.DisplayName} into {settings.PurifiedPerBatch} {purified.DisplayName} every {settings.SecondsPerBatch:0} seconds. ")
            .Append("Purified Essence goes to storage.\n\n")
            .Append($"{cursed.DisplayName} you can use: {available}\n");
        if (settlement.PurificationQueued > 0)
        {
            float progress = settlement.PurificationProgress / settings.SecondsPerBatch;
            body.Append($"In the queue: {settlement.PurificationQueued} batch{(settlement.PurificationQueued == 1 ? "" : "es")} ({progress:P0} through the current one)\n");
        }

        if (!staffed)
        {
            body.Append($"\n<color=#FF9A6B>The altar is cold. It needs a resident working as the {worker}.</color>");
        }

        page.Body = body.ToString();
        int room = settings.MaxQueuedBatches - settlement.PurificationQueued;
        int affordable = available / settings.CursedPerBatch;
        page.Add(new MenuAction("Purify one batch", $"{settings.CursedPerBatch} {cursed.DisplayName}", staffed && affordable >= 1 && room >= 1,
            () => Queue(user, 1)));
        int all = Mathf.Min(affordable, room);
        page.Add(new MenuAction("Purify all", all > 0 ? $"{all * settings.CursedPerBatch} {cursed.DisplayName} → {all * settings.PurifiedPerBatch} {purified.DisplayName}" : null,
            staffed && all > 1, () => Queue(user, all)));
        return page;
    }

    private static void Queue(GameObject user, int batches)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int cost = batches * settings.CursedPerBatch;
        var price = new[] { new ResourceAmount(settings.CursedEssence, cost) };
        if (batches <= 0 || !MaterialPool.For(user).TrySpend(price))
        {
            return;
        }

        GameSession.Settlement.QueuePurification(batches, settings.MaxQueuedBatches);
        GameMessages.Post($"The altar takes {cost} {settings.CursedEssence.DisplayName}.");
    }
}
