using UnityEngine;

/// <summary>
/// Wishes met (Docs/ColonyModeResearch_2026-11-21.md §4.9, Dark Cloud's wishes): what someone likes in a home (HomeLifeSettings ›
/// Likings) is their wish, shown in the Keeper's view while unmet. The first time their home has it, they say so (their Liking's
/// Thanks, as a word in passing) and leave a small gift in the chests, the one their trade makes (HomecomingSettings › Gifts), once
/// (ResidentRecord.WishMet, saved). Positive only: nothing is lost while a wish waits. On the portal, added by it; only in the
/// game that keeps the Haven, one wish told a look.
/// </summary>
[DisallowMultipleComponent]
public sealed class HomeWishes : MonoBehaviour
{
    private const float LookSeconds = 5f;
    private float nextLook;

    private void Update()
    {
        if (Time.unscaledTime < nextLook)
        {
            return;
        }

        nextLook = Time.unscaledTime + LookSeconds;
        if (!Place.AtHome)
        {
            return;
        }

        foreach (ResidentRecord record in GameSession.Home.Residents)
        {
            if (!record.WishMet && HomeLife.IsContent(record))
            {
                Meet(record);
                return;
            }
        }
    }

    private static void Meet(ResidentRecord record)
    {
        record.WishMet = true;
        HomeLifeSettings.Liking liking = HomeLifeSettings.Current.LikingOf(record.Resident);
        string name = record.Resident.DisplayName;
        string thanks = liking != null && !string.IsNullOrWhiteSpace(liking.thanks) ? liking.thanks : "\"That's what a home wants. Thank you.\"";
        HomecomingSettings.Gift gift = GiftFor(record);
        int given = gift != null && gift.item != null ? GameSession.Stash.Items.Add(gift.item, gift.amount) : 0;
        string what = liking != null ? $" ({liking.likes})" : "";
        GameMessages.Post(given > 0
            ? $"{name}'s wish is met{what}: {given} {gift.item.DisplayName} in the chests, with thanks."
            : $"{name}'s wish is met{what}.", MessageKind.Gain);
        // Said aloud where they are, as a word in passing.
        ResidentLife life = ResidentLife.Of(record.Resident);
        if (life != null && DialogueBox.Current != null && !DialogueBox.IsOpen)
        {
            DialogueBox.Current.Bark(new DialogueSpeaker(name, life.transform, null, 0f, null, life.transform), thanks);
        }
    }

    // What their trade makes (the homecoming gifts), if their job has one.
    private static HomecomingSettings.Gift GiftFor(ResidentRecord record)
    {
        foreach (HomecomingSettings.Gift gift in HomecomingSettings.Current.Gifts)
        {
            if (gift.job != null && gift.job == record.Job)
            {
                return gift;
            }
        }

        return null;
    }

    /// <summary>What <paramref name="record"/> still wishes for in a home, or null (met, or none).</summary>
    public static string WishOf(ResidentRecord record)
    {
        if (record == null || record.WishMet)
        {
            return null;
        }

        HomeLifeSettings.Liking liking = HomeLifeSettings.Current.LikingOf(record.Resident);
        return liking != null && !string.IsNullOrWhiteSpace(liking.likes) && !HomeLife.IsContent(record) ? liking.likes : null;
    }
}
