using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Haven's asks, at home: keeps HavenRequestSettings › At Once asks standing (SettlementState.Requests, saved). Once what
/// one asks for is home (in the bag or the stash), an ask of someone living here is handed to them in talk (AddHandIn, the
/// player told once that it is ready), the Haven's own asks at once: the asker takes it (every bare piece before one the
/// player has worked on), thanks the player and gives its reward (items to the bag, the stash or the feet: ItemDelivery;
/// coin, experience, the blight eased). A new ask takes its place
/// the next time the player comes home from a raid, so each run can be planned round what is wanted.
/// <para>Where it belongs: once in the Haven scene (set up by AgentScripts/BuildHavenRequests.cs).</para>
/// </summary>
public sealed class HavenRequestBoard : MonoBehaviour
{
    [Tooltip("Seconds between looking whether an ask can be answered.")]
    [SerializeField, Min(0.5f)] private float checkEvery = 2f;

    private float nextCheck;

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
            lines.Add(request.BriefingLine(AtHome(request, bag)));
        }

        return lines;
    }

    /// <summary>How much of what a request wants is home, in the bag or the stash (an order's only of its make).</summary>
    public static int AtHome(HavenRequestDefinition request, ItemInventory bag)
    {
        int count = bag != null ? bag.Container.Count(request.Wanted, request.Quality) : 0;
        if (GameSession.Stash != null)
        {
            count += GameSession.Stash.Items.Count(request.Wanted, request.Quality);
        }

        return count;
    }

    private void Start()
    {
        // In a friend's Haven the asks are the host's (as in Update): this game's copy of their settlement isn't posted to.
        if (HavenVisit.IsVisiting)
        {
            return;
        }

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
            if (AtHome(request, bag) < request.Count)
            {
                told.Remove(request.Id);
                continue;
            }

            // An ask of someone living here is handed to them (AddHandIn: talk to them), so nothing worked on is taken unasked and
            // the giving is a moment the player sees; they are told once that it is ready. The Haven's own asks (no asker, or one
            // no longer here) are answered at once, as before.
            if (HandedIn(request))
            {
                if (told.Add(request.Id))
                {
                    string what = request.Count == 1 ? $"the {request.WantedName}" : $"{request.Count} {request.WantedName}";
                    GameMessages.Post($"{request.AskerName} can have {what} now: take it to them.", MessageKind.Info);
                }

                continue;
            }

            Answer(request, bag);
        }

        Refill(force: false);
    }

    private readonly HashSet<string> told = new HashSet<string>();

    // Asked by someone who lives here: handed over by talking to them.
    private static bool HandedIn(HavenRequestDefinition request) =>
        request.Asker != null && GameSession.Settlement.Find(request.Asker) != null;

    /// <summary>
    /// The asker's line for handing over what they asked for (ResidentNpc.AddTopics): ready once it is home, naming what goes, and
    /// warning when a piece the player has empowered or enchanted would have to go (every bare one goes first).
    /// </summary>
    public static void AddHandIn(MenuPage page, ResidentDefinition asker, GameObject player, DialogueSpeaker speaker, System.Action afterAnswer)
    {
        HavenRequestDefinition request = AskedBy(asker);
        if (request == null || RaidDirector.Current != null || HavenVisit.IsVisiting)
        {
            return;
        }

        ItemInventory bag = player != null ? player.GetComponent<ItemInventory>() : null;
        int home = AtHome(request, bag);
        string what = request.Count == 1 ? request.WantedName : $"{request.Count} {request.WantedName}";
        bool ready = home >= request.Count;
        string detail = !ready ? $"{home}/{request.Count} home (bag or stash)"
            : Bare(request, bag) < request.Count ? $"<color={UIStyle.Current.WarningHex}>A piece you have empowered or enchanted goes too</color>"
            : request.IsOrder ? "The plainest piece that will do, from your bag or the stash" : "From your bag or the stash";
        page.Add(new MenuAction($"Hand over {what}", detail, ready, () =>
        {
            Answer(request, bag, spoken: true);
            string thanks = string.IsNullOrEmpty(request.Thanks) ? "Thank you." : request.Thanks;
            if (DialogueBox.Current != null)
            {
                DialogueBox.Current.Say(speaker, thanks, afterAnswer);
            }
            else
            {
                afterAnswer?.Invoke();
            }
        }));
    }

    // How many bare pieces (not empowered or enchanted) of what is asked are home.
    private static int Bare(HavenRequestDefinition request, ItemInventory bag)
    {
        int count = 0;
        foreach (ItemContainer container in new[] { bag != null ? bag.Container : null, GameSession.Stash != null ? GameSession.Stash.Items : null })
        {
            for (int i = 0; container != null && i < container.SlotCount; i++)
            {
                ItemStack stack = container.GetSlot(i);
                if (stack.Item == request.Wanted && stack.Quality >= request.Quality && !stack.IsWorked)
                {
                    count += stack.Count;
                }
            }
        }

        return count;
    }

    /// <summary>Takes what was asked for (from the bag first, then the stash) and gives the reward. <paramref name="spoken"/>: handed
    /// over in talk, where the asker says their thanks (no message of it).</summary>
    public static void Answer(HavenRequestDefinition request, ItemInventory bag, bool spoken = false)
    {
        // Every bare piece, bag then stash, goes before any the player has empowered or enchanted: an order is answered on its
        // own, so it must not take a worked spare from the bag while a plain one waits in the stash.
        int left = request.Count;
        foreach (bool worked in new[] { false, true })
        {
            if (bag != null)
            {
                left -= bag.Container.Remove(request.Wanted, left, request.Quality, worked);
            }

            if (left > 0 && GameSession.Stash != null)
            {
                left -= GameSession.Stash.Items.Remove(request.Wanted, left, request.Quality, worked);
            }
        }

        SettlementState settlement = GameSession.Settlement;
        settlement.RemoveRequest(request.Id);
        settlement.RecordRequestAnswered(request.Renown);
        var given = new List<string>();
        Give(request.Reward, request.RewardCount, bag, given);
        Give(TradeSettings.Current.Coin, request.Coins, bag, given);
        if (request.Experience > 0)
        {
            ExperienceTracker.GrantInsight(request.Experience);
            given.Add($"{ExperienceTracker.InsightWorth(request.Experience)} Insight");
        }

        // An order's asker shows how they would have done it: a lesson in the craft, and the Haven is known for the work.
        ProficiencyDefinition craft = request.Craft;
        if (request.Lesson > 0 && craft != null)
        {
            ExperienceTracker.Teach(craft, request.Lesson);
            given.Add($"a lesson in {craft.DisplayName} (+{request.Lesson})");
        }

        if (request.Renown > 0)
        {
            given.Add($"{request.Renown} renown");
        }

        if (request.EasesBlight > 0f)
        {
            settlement.ChangeBlight(-request.EasesBlight);
            given.Add("the blight eases");
        }

        string thanks = string.IsNullOrEmpty(request.Thanks) ? "Thank you." : request.Thanks;
        string what = request.Count == 1 ? request.WantedName : $"{request.Count} {request.WantedName}";
        GameMessages.Post(spoken ? $"{request.AskerName} takes the {what}." : $"{request.AskerName} takes the {what}. \"{thanks}\"", MessageKind.Gain);
        if (given.Count > 0)
        {
            GameMessages.Post("For it: " + string.Join(", ", given) + ".", MessageKind.Gain);
        }

        Vector3 at = bag != null ? bag.transform.position : Vector3.zero;
        AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.QuestCompleted : null, at);
    }

    private static void Give(ItemDefinition item, int count, ItemInventory bag, List<string> given)
    {
        if (item == null || count <= 0)
        {
            return;
        }

        // Bag, then stash, then the player's feet: a reward is never lost for want of room.
        int placed = ItemDelivery.Give(bag, new ItemStack(item, count), newFind: true, toStash: true, cause: ArrivalCause.Rewarded).Total;
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
        HavenRequestSettings settings = HavenRequestSettings.Current;
        // Nothing new is asked until there is a board to post it on; the first asks come as soon as it stands.
        if (!force && settlement.RequestsAskedAt == homecoming || !settings.CanPost)
        {
            return;
        }

        settlement.NoteRequestsAsked(homecoming);
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
