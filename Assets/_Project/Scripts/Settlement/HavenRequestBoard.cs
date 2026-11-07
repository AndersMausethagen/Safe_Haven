using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Haven's asks, at home: keeps HavenRequestSettings › At Once asks standing (SettlementState.Requests, saved), and
/// as soon as what one asks for is home (in the bag or the stash) hands it over: the asker takes it, thanks the player
/// and gives its reward (items to the bag or the stash, coin, experience, the blight eased). A new ask takes its place
/// the next time the player comes home from a raid, so each run can be planned round what is wanted.
/// <para>Where it belongs: once in the Haven scene (set up by AgentScripts/BuildHavenRequests.cs).</para>
/// </summary>
public sealed class HavenRequestBoard : MonoBehaviour
{
    [Tooltip("Seconds between looking whether an ask can be answered.")]
    [SerializeField, Min(0.5f)] private float checkEvery = 2f;

    private float nextCheck;
    private int askedAtHomecoming = -1;

    /// <summary>The asks standing now.</summary>
    public static List<HavenRequestDefinition> Standing()
    {
        var standing = new List<HavenRequestDefinition>();
        HavenRequestSettings settings = HavenRequestSettings.Current;
        foreach (string id in GameSession.Settlement.Requests)
        {
            HavenRequestDefinition request = settings.Find(id);
            if (request != null)
            {
                standing.Add(request);
            }
        }

        return standing;
    }

    /// <summary>What a resident has asked for, if anything.</summary>
    public static HavenRequestDefinition AskedBy(ResidentDefinition resident)
    {
        if (resident == null)
        {
            return null;
        }

        foreach (HavenRequestDefinition request in Standing())
        {
            if (request.Asker == resident)
            {
                return request;
            }
        }

        return null;
    }

    /// <summary>The raid planner's lines for what is asked at home, with how much is home already.</summary>
    public static List<string> BriefingLines(GameObject player)
    {
        var lines = new List<string>();
        ItemInventory bag = player != null ? player.GetComponent<ItemInventory>() : null;
        foreach (HavenRequestDefinition request in Standing())
        {
            lines.Add(request.BriefingLine(AtHome(request.Wanted, bag)));
        }

        return lines;
    }

    private static int AtHome(ItemDefinition item, ItemInventory bag)
    {
        int count = bag != null ? bag.Container.Count(item) : 0;
        if (GameSession.Stash != null)
        {
            count += GameSession.Stash.Items.Count(item);
        }

        return count;
    }

    private void Start()
    {
        // A fresh game, or asks gone from the settings: fill up at once.
        Refill(force: GameSession.Settlement.Requests.Count == 0);
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + checkEvery;
        Player player = Player.Current;
        if (player == null || RaidDirector.Current != null || HavenVisit.IsVisiting)
        {
            return;
        }

        ItemInventory bag = player.GetComponent<ItemInventory>();
        foreach (HavenRequestDefinition request in Standing())
        {
            if (AtHome(request.Wanted, bag) >= request.Count)
            {
                Answer(request, player, bag);
            }
        }

        Refill(force: false);
    }

    // Takes what was asked for (from the bag first, then the stash) and gives the reward.
    private static void Answer(HavenRequestDefinition request, Player player, ItemInventory bag)
    {
        int left = request.Count;
        if (bag != null)
        {
            left -= bag.Container.Remove(request.Wanted, left);
        }

        if (left > 0 && GameSession.Stash != null)
        {
            left -= GameSession.Stash.Items.Remove(request.Wanted, left);
        }

        SettlementState settlement = GameSession.Settlement;
        settlement.RemoveRequest(request.Id);
        settlement.RecordRequestAnswered();
        var given = new List<string>();
        Give(request.Reward, request.RewardCount, bag, given);
        Give(TradeSettings.Current.Coin, request.Coins, bag, given);
        if (request.Experience > 0)
        {
            ExperienceTracker.GrantInsight(request.Experience);
            given.Add($"{request.Experience} experience");
        }

        if (request.EasesBlight > 0f)
        {
            settlement.ChangeBlight(-request.EasesBlight);
            given.Add("the blight eases");
        }

        string thanks = string.IsNullOrEmpty(request.Thanks) ? "Thank you." : request.Thanks;
        GameMessages.Post($"{request.AskerName} takes the {request.Count} {request.Wanted.DisplayName}. \"{thanks}\"", MessageKind.Gain);
        if (given.Count > 0)
        {
            GameMessages.Post("For it: " + string.Join(", ", given) + ".", MessageKind.Gain);
        }

        AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.QuestCompleted : null, player.transform.position);
    }

    private static void Give(ItemDefinition item, int count, ItemInventory bag, List<string> given)
    {
        if (item == null || count <= 0)
        {
            return;
        }

        int placed = bag != null ? bag.Give(item, count) : 0;
        if (placed < count && GameSession.Stash != null)
        {
            placed += GameSession.Stash.Items.Add(item, count - placed);
        }

        if (placed > 0)
        {
            given.Add($"{placed} {item.DisplayName}");
        }
    }

    // New asks come with each homecoming (or at once when there are none), not the moment one is answered: the next
    // raid is planned round what is wanted then.
    private void Refill(bool force)
    {
        SettlementState settlement = GameSession.Settlement;
        int homecoming = settlement.SuccessfulExtractions;
        if (!force && askedAtHomecoming == homecoming)
        {
            return;
        }

        askedAtHomecoming = homecoming;
        HavenRequestSettings settings = HavenRequestSettings.Current;
        List<HavenRequestDefinition> standing = Standing();
        foreach (string id in new List<string>(settlement.Requests))
        {
            if (settings.Find(id) == null)
            {
                settlement.RemoveRequest(id); // gone from the game
            }
        }

        int places = settings.AtOnce - standing.Count;
        foreach (HavenRequestDefinition request in HavenRequestSettings.Choose(settings.Requests, standing, places, settlement,
                     Seasons.Today, settlement.DaysPassed + homecoming * 31))
        {
            settlement.AddRequest(request.Id);
        }
    }
}
