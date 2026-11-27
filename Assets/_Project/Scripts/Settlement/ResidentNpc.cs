using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A resident living in Safe Haven (GDD §15.3–15.4). In their conversation (their Talkable) they tell who they are, and
/// the jobs they can take are answers; giving a job records it in SettlementState and teaches the player what the job knows (the Alchemist teaches the
/// Purification Altar). Their day (ResidentLife) takes them to their workplace once it has been built, so hiring someone visibly
/// changes the settlement.
/// <para>
/// Created by ResidentSpawner for every resident in SettlementState; it needs a Talkable whose Interactable doesn't
/// deactivate.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Talkable))]
public sealed class ResidentNpc : MonoBehaviour, IConversationTopics, IQuestPerson
{
    private const string DefaultAcceptLine = "I'll get to it.";
    private const float WorkSpotClearance = 1f;

    private ResidentDefinition resident;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private ResidentLife life;

    public ResidentDefinition Resident => resident;

    private static readonly List<ResidentNpc> present = new List<ResidentNpc>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => present.Clear();

    /// <summary>Everyone standing in this scene.</summary>
    public static IReadOnlyList<ResidentNpc> Present => present;

    /// <summary>The one standing in this scene for <paramref name="who"/>, or null.</summary>
    public static ResidentNpc Of(ResidentDefinition who)
    {
        foreach (ResidentNpc npc in present)
        {
            if (npc != null && npc.resident == who)
            {
                return npc;
            }
        }

        return null;
    }

    /// <summary>What they are doing now: their day (ResidentLife), or null before it starts.</summary>
    public ResidentLife Life => life;

    private void OnEnable()
    {
        present.Add(this);
        GameEvents.Raised += OnGameEvent;
        GameSession.Here.Changed += GoToWork;
    }

    private void OnDisable()
    {
        present.Remove(this);
        GameEvents.Raised -= OnGameEvent;
        GameSession.Here.Changed -= GoToWork;
    }

    private QuestGiver questGiver;

    /// <summary>Sets who this is and where their home spot is. Called by ResidentSpawner.</summary>
    public void Bind(ResidentDefinition who)
    {
        resident = who;
        // Their quests at home, if they give any: a quest giver on them, speaking first in their conversation.
        bool sides = who.SideQuests != null && who.SideQuests.Count > 0;
        if ((who.QuestLine != null && who.QuestLine.Count > 0) || sides)
        {
            questGiver = TryGetComponent(out QuestGiver existing) ? existing : gameObject.AddComponent<QuestGiver>();
            // The quest that brought them home is handed in to them: their mark shows it (set before the line, which refreshes it).
            questGiver.AlsoHandedInHere = () => ResidentQuests.RescueToHandIn(resident);
            // Their side quests first: the line refreshes the mark, which reads them.
            questGiver.UseSideOffers(sides ? who.SideQuests : System.Array.Empty<QuestOffer>());
            questGiver.UseQuestLine(who.QuestLine ?? System.Array.Empty<QuestOffer>(), who.QuestLineDone, true);
        }

        homePosition = transform.position;
        homeRotation = transform.rotation;
        name = who.DisplayName;
        if (!TryGetComponent(out life))
        {
            life = gameObject.AddComponent<ResidentLife>();
        }

        life.Settle(homePosition, homeRotation, GameSession.Here.Find(who) is ResidentRecord record ? IndexOf(record) : 0);
        // A lantern after dark, and a word in passing: the village lived in.
        if (!TryGetComponent(out ResidentLantern _))
        {
            gameObject.AddComponent<ResidentLantern>();
        }

        if (!TryGetComponent(out ResidentAsides _))
        {
            gameObject.AddComponent<ResidentAsides>();
        }
    }

    /// <summary>What there is to say of late, in passing (a loss, a boss beaten, someone home who doesn't know us), or null.</summary>
    public string News()
    {
        if (resident == null)
        {
            return null;
        }

        string news = Grief() ?? BossTalk();
        if (news != null)
        {
            return news;
        }

        SettlementSettings settings = SettlementSettings.Current;
        foreach (ResidentRecord other in GameSession.Here.Residents)
        {
            if (other.Resident != resident && other.IsStranger && settings.HomecomingAsides.Length > 0)
            {
                return string.Format(settings.HomecomingAsides[CompanionBond.StableIndex(resident.Id, settings.HomecomingAsides.Length)], other.Resident.DisplayName);
            }
        }

        return RaidTalk();
    }

    // A word about the raid just come home from (Hades' homecomings): from the one who went along; to a player who came home
    // beaten; else about what the player carries that their work uses. Null when there was no raid or nothing to say.
    private string RaidTalk()
    {
        RaidOutcome last = Homecoming.LastOutcome;
        HomecomingSettings lines = HomecomingSettings.Current;
        if (last == RaidOutcome.InProgress || HavenVisit.IsVisiting)
        {
            return null;
        }

        string said = RaidLine(last, lines);
        // Once per raid each, however often the Haven is loaded meanwhile.
        return said != null && Homecoming.FirstWordOnTheRaid(resident) ? said : null;
    }

    private string RaidLine(RaidOutcome last, HomecomingSettings lines)
    {
        int pick = CompanionBond.StableIndex(resident.Id + Homecoming.Homecomings, 97);
        // Beaten, or pulled home by the portal: that comes first, the one who went along too.
        if ((last == RaidOutcome.Died || last == RaidOutcome.TimeExpired) && lines.FellLines.Length > 0)
        {
            return lines.FellLines[pick % lines.FellLines.Length];
        }

        if (Homecoming.LastAlong == resident && lines.AlongLines.Length > 0)
        {
            return lines.AlongLines[pick % lines.AlongLines.Length];
        }

        JobDefinition job = Job;
        Player player = Player.Current;
        if (job == null || job.Workplace == null || player == null || !player.TryGetComponent(out ItemInventory bag) || lines.BroughtLines.Length == 0)
        {
            return null;
        }

        // The input of their work the player carries most of, three at least.
        ItemDefinition most = null;
        int count = 2;
        foreach (WorkOrder order in job.WorkOrders)
        {
            if (order.recipe == null)
            {
                continue;
            }

            foreach (ItemAmount cost in order.recipe.ItemCost)
            {
                int carried = cost.Item != null ? bag.Container.Count(cost.Item) : 0;
                if (carried > count)
                {
                    most = cost.Item;
                    count = carried;
                }
            }
        }

        return most != null ? string.Format(lines.BroughtLines[pick % lines.BroughtLines.Length], count, most.DisplayName, job.Workplace.DisplayName) : null;
    }

    private static int IndexOf(ResidentRecord record)
    {
        IReadOnlyList<ResidentRecord> residents = GameSession.Here.Residents;
        for (int i = 0; i < residents.Count; i++)
        {
            if (residents[i] == record)
            {
                return i;
            }
        }

        return 0;
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.Built)
        {
            GoToWork();
        }
    }

    private void GoToWork()
    {
        if (life != null)
        {
            life.Refresh();
        }
    }

    /// <summary>Their record in the settlement, or null.</summary>
    public ResidentRecord Record => resident != null ? GameSession.Here.Find(resident) : null;

    /// <summary>The building they work at: the one of their job's kind nearest their home (HavenHomes), or null.</summary>
    public BuildingObject Workplace => HavenHomes.WorkplaceOf(Record, homePosition);

    /// <summary>The house they sleep in, or null (the home spot they were given by the portal).</summary>
    public BuildingObject Home => HavenHomes.HomeOf(Record);

    /// <summary>The spot by the portal they sleep at when no house is theirs.</summary>
    public Vector3 HomeSpot => homePosition;

    /// <summary>The door of the house they sleep in, and the way out of it. False when they have no house.</summary>
    public bool TryGetHome(out Vector3 door, out Quaternion outward, out BuildingObject house)
    {
        house = Home;
        if (house == null)
        {
            door = homePosition;
            outward = homeRotation;
            return false;
        }

        door = house.Door(out outward);
        return true;
    }

    /// <summary>Where they stand to work, when their job has a workplace that has been built: the one nearest their home.</summary>
    public bool TryGetWorkplace(out Vector3 spot, out Quaternion facing)
    {
        spot = homePosition;
        facing = homeRotation;
        BuildingObject building = Workplace;
        if (building == null)
        {
            return false;
        }

        // Outside the walls, which are carved out of the NavMesh: in its yard when it has one, else just off its side.
        Vector3 centre = building.transform.position;
        if (building.TryGetYard(out FootprintRect yard))
        {
            spot = yard.WorldCenter(centre.y);
        }
        else
        {
            spot = centre + building.transform.right * (building.Footprint.HalfSize.x + WorkSpotClearance);
        }

        Vector3 toBuilding = centre - spot;
        toBuilding.y = 0f;
        facing = toBuilding.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toBuilding) : homeRotation;
        return true;
    }

    /// <summary>What they do with their hands at work.</summary>
    public EmoteDefinition WorkGesture => Job != null ? Job.WorkGesture : null;

    /// <summary>The job they hold now, or null.</summary>
    public JobDefinition Job => resident != null ? GameSession.Here.Find(resident)?.Job : null;

    /// <summary>Who they are and what they do (when nothing before it spoke), and the jobs they can take as answers.</summary>
    // One answer for each kind of worker gear in the bag (the user, 2026-11-21: "train and upgrade the NPCs with equipment"): given, it
    // goes from the bag to them, and what they had comes back to the bag.
    private void AddGearTopics(MenuPage page, ResidentRecord record, DialogueSpeaker speaker, Action afterAnswer)
    {
        Player player = Player.Current;
        if (record == null || player == null || player.Bag == null || HavenVisit.InHostsHaven)
        {
            return;
        }

        var offered = new System.Collections.Generic.HashSet<ItemDefinition>();
        ItemContainer bag = player.Bag.Container;
        for (int i = 0; i < bag.SlotCount; i++)
        {
            ItemDefinition gear = bag.GetSlot(i).Item;
            if (gear == null || !gear.IsWorkerGear || gear == record.WorkGear || !offered.Add(gear))
            {
                continue;
            }

            page.Add(new MenuAction($"Give them your {gear.DisplayName}", GearLine(gear), true, () =>
            {
                if (bag.Remove(gear, 1) <= 0)
                {
                    afterAnswer?.Invoke();
                    return;
                }

                // What they had comes back: to the bag, or to the chests where the bag has no room (never lost).
                ItemDefinition had = record.WorkGear;
                record.WorkGear = gear;
                if (had != null && player.Bag.Give(had, 1, ArrivalCause.Moved) <= 0 && GameSession.Stash.Items.Add(had, 1) <= 0)
                {
                    ItemDelivery.Give(player.Bag, new ItemStack(had, 1), newFind: false, toStash: true, player.transform);
                }

                DialogueBox.Current.Say(speaker, $"The {gear.DisplayName}? That will lighten the work. Thank you.", afterAnswer);
            }));
        }
    }

    // Who they are, in talk (Docs/PeopleResearch_2026-11-23.md): a passion to grow once a bond rank is reached (the player chooses one of
    // two), the kit they fight with (a weapon or body piece from the bag that suits them, and taking theirs back to mend it), and a hard
    // mark healed by what heals it. In one's own Haven only: a guest's changes lived on the copy of the host's.
    private void AddPeopleTopics(MenuPage page, ResidentRecord record, DialogueSpeaker speaker, Action afterAnswer)
    {
        Player player = Player.Current;
        if (record == null || player == null || player.Bag == null || HavenVisit.InHostsHaven)
        {
            return;
        }

        if (record.Talents.PicksOwed > 0)
        {
            foreach ((ProficiencyDefinition skill, Passion to) in ResidentTalents.PickOffers(record))
            {
                page.Add(new MenuAction(to == Passion.Burning ? $"Help their love of {skill.DisplayName} burn" : $"Help them find a love of {skill.DisplayName}",
                    to == Passion.Burning ? "Keen to Burning: they learn it faster and shine at it" : "A new passion: they learn it faster and are happier at it",
                    true, () =>
                    {
                        bool picked = ResidentTalents.Pick(record, skill, to);
                        DialogueBox.Current.Say(speaker, picked
                            ? to == Passion.Burning ? $"{skill.DisplayName}. Yes. I think it was always that, under everything." : $"{skill.DisplayName}? I'd never thought of it. I will now."
                            : "Another time.", afterAnswer);
                    }));
            }
        }

        ItemContainer bag = player.Bag.Container;
        var offered = new HashSet<ItemDefinition>();
        int today = GameSession.Home.DaysPassed;
        for (int i = 0; i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (stack.IsEmpty || stack.IsBroken || !offered.Add(stack.Item))
            {
                continue;
            }

            // Something they love (PeopleGifts): a gift, once a day.
            if (PeopleGifts.Loves(record, stack.Item))
            {
                ItemDefinition loved = stack.Item;
                bool counts = PeopleGifts.CountsToday(record, today);
                page.Add(new MenuAction($"Give them your {loved.DisplayName}", counts ? "They love it" : "They had a gift today", counts, () =>
                {
                    bool glad = bag.Remove(loved, 1) > 0 && PeopleGifts.Give(record, loved, today);
                    DialogueBox.Current.Say(speaker, glad ? $"{loved.DisplayName}? For me? ...You remembered." : "Another day.", afterAnswer);
                }) { Icon = "Heart" });
            }

            int slot = i;
            bool weapon = CompanionKit.Fits(record, stack.Item, true);
            if (weapon || CompanionKit.Fits(record, stack.Item, false))
            {
                page.Add(new MenuAction($"Give them your {stack.DisplayName} to fight with", weapon ? "Their weapon beside you and at the ward" : "Their body piece beside you and at the ward",
                    true, () =>
                    {
                        bool given = CompanionKit.Give(record, bag, slot, GameSession.Stash != null ? GameSession.Stash.Items : null, player.transform);
                        DialogueBox.Current.Say(speaker, given ? $"A {stack.Item.DisplayName}. I'll bring it home, and you with it." : "That won't do for me.", afterAnswer);
                    }));
            }

            MarkDefinition healed = ResidentMarks.HealedBy(record, stack.Item);
            if (healed != null)
            {
                page.Add(new MenuAction($"Give them your {stack.Item.DisplayName}", $"For their {healed.DisplayName.ToLowerInvariant()}", true, () =>
                {
                    bool used = bag.Remove(stack.Item, 1) > 0 && ResidentMarks.Heal(record, healed);
                    if (used)
                    {
                        // The player tended them: the bond grows (a friend's evening easing a mark doesn't).
                        PeopleBond.Grow(record, PeopleSettings.Current.BondMarkHealed, "mark healed");
                    }
                    DialogueBox.Current.Say(speaker, used ? $"That's better. That's... better. Thank you." : "Not now.", afterAnswer);
                }));
            }
        }

        foreach (bool weapon in new[] { true, false })
        {
            ItemStack had = weapon ? record.KitWeapon : record.KitBody;
            if (had.IsEmpty)
            {
                continue;
            }

            string name = CompanionKit.NameOf(record, weapon);
            page.Add(new MenuAction($"Take back the {name}", had.IsBroken ? "It's broken: mend it and give it back" : $"{Mathf.RoundToInt(had.Durability * 100f)}% sound",
                true, () =>
                {
                    bool theirs = CompanionKit.IsTheirs(record, weapon);
                    CompanionKit.TakeBack(record, weapon, bag, GameSession.Stash != null ? GameSession.Stash.Items : null, player.transform);
                    DialogueBox.Current.Say(speaker, theirs ? "Mend it well. It knows my hand." : "Here.", afterAnswer);
                }));
        }
    }

    // What a piece of worker gear does, in a few words.
    private static string GearLine(ItemDefinition gear)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (gear.WorkerCarryWeight > 0f) parts.Add($"carries {gear.WorkerCarryWeight:0} more");
        if (gear.WorkerCarrySlots > 0) parts.Add($"{gear.WorkerCarrySlots} more kind{(gear.WorkerCarrySlots == 1 ? "" : "s")} at once");
        if (gear.WorkerSpeed > 0f) parts.Add($"works {gear.WorkerSpeed * 100f:0}% faster");
        return string.Join(", ", parts);
    }

    public void AddTopics(MenuPage page, DialogueSpeaker speaker, Action afterAnswer)
    {
        if (resident == null)
        {
            return;
        }

        // Brought home on a quest: it is handed in here, first and alone. Their words of arrival, then one answer that hands it in,
        // starts their own line and teaches their workplace; then they say what they can help with (the user, 2026-11-21).
        QuestDefinition rescue = ResidentQuests.RescueToHandIn(resident);
        if (rescue != null && !HavenVisit.InHostsHaven)
        {
            page.Body = !string.IsNullOrWhiteSpace(resident.WelcomeLine) ? resident.WelcomeLine : resident.Description;
            page.Add(new MenuAction("You're safe now. Welcome to the Haven.", rescue.DisplayName, true, () =>
            {
                if (!ResidentQuests.HandInRescue(resident, Player.Current != null ? Player.Current.gameObject : null))
                {
                    afterAnswer?.Invoke();
                    return;
                }

                string help = ResidentQuests.FirstOfferText(resident);
                if (string.IsNullOrWhiteSpace(help))
                {
                    afterAnswer?.Invoke();
                }
                else
                {
                    DialogueBox.Current.Say(speaker, help, afterAnswer);
                }
            }, quest: rescue, does: QuestAnswer.HandsIn));
            return;
        }

        // Their quest speaks first: what they ask, or what they say as it is handed in, before who they are.
        if (questGiver != null)
        {
            questGiver.AddTopicsForOwner(page, speaker, afterAnswer);
        }

        JobDefinition current = GameSession.Here.Find(resident)?.Job;
        if (string.IsNullOrWhiteSpace(page.Body))
        {
            var body = new StringBuilder(current == null && !string.IsNullOrWhiteSpace(resident.WelcomeLine) ? resident.WelcomeLine : resident.Description);
            body.Append("\n\n").Append(current != null
                ? $"<color={UIStyle.Current.AccentHex}>Works as the {current.DisplayName}.</color> {current.Description}"
                : $"<color={UIStyle.Current.AccentHex}>Has no job yet.</color>");
            string said = Remembered();
            foreach (string more in new[] { Grief(), said == null ? BossTalk() : null })
            {
                if (more != null)
                {
                    said = said != null ? said + "\n\n" + more : more;
                }
            }

            page.Body = said != null ? said + "\n\n" + body : body.ToString();
        }

        // What they asked the Haven for, handed to them once it is home.
        HavenRequestBoard.AddHandIn(page, resident, Player.Current != null ? Player.Current.gameObject : null, speaker, afterAnswer);

        // Worker gear (WorkerSettings): something from the player's bag to carry more or work faster. Given in person, in their own Haven.
        AddGearTopics(page, GameSession.Here.Find(resident), speaker, afterAnswer);
        AddPeopleTopics(page, GameSession.Here.Find(resident), speaker, afterAnswer);

        // A lesson in their job's skill, once between raids; in their own Haven only (as Liv's carving): a guest's "taught" mark
        // lived on the copy of the host's settlement, undone by the host's next word, and the lesson could be taken again and again.
        ResidentRecord record = GameSession.Here.Find(resident);
        if (current != null && current.Teaches != null && record != null && !HavenVisit.InHostsHaven)
        {
            ProficiencyDefinition skill = current.Teaches;
            bool more = record.TaughtSinceRaid && record.Inspiration == Inspiration.Lesson;
            string why = more ? "They have more to teach today." : record.TaughtSinceRaid ? "Another, after your next raid survived." : $"What a {ResidentTalents.RankName(record)} {current.DisplayName} knows ({skill.DisplayName} {ResidentTalents.LevelOf(record, skill)})";
            page.Add(new MenuAction($"Ask for a lesson in {skill.DisplayName}", why, !record.TaughtSinceRaid || more, () =>
            {
                int learned = ExperienceTracker.TakeLesson(record);
                DialogueBox.Current.Say(speaker, learned > 0
                    ? $"Watch my hands, not my face. There. (+{learned} {skill.DisplayName})"
                    : "There's nothing more I can show you. You know it better than I do now.", afterAnswer);
            }));
        }

        // Their page: home (and moving them), work and story (ResidentWindow).
        if (record != null)
        {
            page.Add(new MenuAction("Your home and work", "Their house, their work and their story", true, () => ResidentWindow.Show(resident, "Home")));
        }

        // Someone who carves enchantments (Liv): how it works, what she can carve, and inscriptions handed to her.
        if (current != null && current.Carves && !HavenVisit.InHostsHaven)
        {
            EnchanterTalk.AddTopics(page, speaker, record, afterAnswer);
        }

        // Their work now and their own trades, and the two others they are best at (any known trade with room is open: Who does what
        // shows them all).
        ResidentRecord them = GameSession.Here.Find(resident);
        List<JobDefinition> open = GameSession.Here.JobsOpenTo(resident);
        var offered = new List<JobDefinition>();
        foreach (JobDefinition job in open)
        {
            if (job != null && (job == current || System.Array.IndexOf(resident.Jobs, job) >= 0))
            {
                offered.Add(job);
            }
        }

        open.Sort((a, b) => ResidentTalents.LevelOf(them, b.Skill).CompareTo(ResidentTalents.LevelOf(them, a.Skill)));
        int others = 0;
        foreach (JobDefinition job in open)
        {
            if (job != null && !offered.Contains(job) && others < 2)
            {
                offered.Add(job);
                others++;
            }
        }

        foreach (JobDefinition job in offered)
        {
            if (job == null)
            {
                continue;
            }

            bool isCurrent = job == current;
            string level = job.Skill != null && them != null ? $"{job.Skill.DisplayName} {ResidentTalents.LevelOf(them, job.Skill)} {ResidentTalents.Flames(ResidentTalents.PassionOf(them, job.Skill))}" : null;
            page.Add(new MenuAction(isCurrent ? $"Working as {job.DisplayName}" : $"Work as {job.DisplayName}", level, !isCurrent, () =>
            {
                Assign(job, Player.Current != null ? Player.Current.gameObject : null);
                DialogueBox.Current.Say(speaker, string.IsNullOrWhiteSpace(job.AcceptLine) ? DefaultAcceptLine : job.AcceptLine, afterAnswer);
            }));
        }
    }

    // What they have to say of the last raid they went on, once.
    private string Remembered()
    {
        // A friend's people (co-op) remember the host's raids, not this visitor's: a guest heard and used up their lines on the copy,
        // which the host's next word undid, so they came again.
        if (HavenVisit.InHostsHaven)
        {
            return null;
        }

        ResidentRecord record = GameSession.Here.Find(resident);
        if (record == null)
        {
            return null;
        }

        SettlementSettings settings = SettlementSettings.Current;
        if (record.IsStranger)
        {
            record.IsStranger = false;
            return settings.StrangerLines.Length > 0 ? settings.StrangerLines[CompanionBond.StableIndex(resident.Id, settings.StrangerLines.Length)] : null;
        }

        if (record.ComingBack != CompanionMemory.None)
        {
            string back = CompanionBond.LineFor(record.ComingBack, resident.Id, resident.ReturningLines, settings.ReturningLines);
            if (record.AllCameBack && !string.IsNullOrWhiteSpace(settings.AllBackLine))
            {
                back = back != null ? back + "\n\n" + settings.AllBackLine : settings.AllBackLine;
            }

            record.ComingBack = CompanionMemory.None;
            record.AllCameBack = false;
            return back;
        }

        if (record.Memory == CompanionMemory.None)
        {
            return null;
        }

        string line = CompanionBond.LineFor(record.Memory, resident.Id, resident.MemoryLines, settings.MemoryLines);
        record.Memory = CompanionMemory.None;
        return line;
    }

    // They speak of whoever was lost for a while, each in the same words.
    private string Grief()
    {
        SettlementSettings settings = SettlementSettings.Current;
        FallenRecord loss = GameSession.Here.RecentLoss(settings.GriefRaids);
        if (loss == null || settings.GriefLines.Length == 0)
        {
            return null;
        }

        int line = CompanionBond.StableIndex(resident.Id, settings.GriefLines.Length);
        return string.Format(settings.GriefLines[line], loss.Resident.DisplayName);
    }

    // For a while after a boss falls, it is what people talk about.
    private string BossTalk()
    {
        IHavenView settlement = GameSession.Here;
        SettlementSettings settings = SettlementSettings.Current;
        if (string.IsNullOrEmpty(settlement.LastBossBeaten) || settlement.SuccessfulExtractions - settlement.LastBossBeatenAt > settings.GriefRaids)
        {
            return null;
        }

        foreach (BossDefinition boss in settings.Bosses)
        {
            if (boss != null && boss.Id == settlement.LastBossBeaten && boss.HomeLines.Length > 0)
            {
                return boss.HomeLines[CompanionBond.StableIndex(resident.Id, boss.HomeLines.Length)];
            }
        }

        return null;
    }

    private void Assign(JobDefinition job, GameObject user) => GiveJob(resident, job, user);

    /// <summary>
    /// Someone brought home shows the player how to build where they work: the buildings of every job they can do, so a rescue
    /// opens a workplace (Docs/RescueOrder.md). Giving them the job teaches the same again, for a co-op friend who wasn't there.
    /// Someone with a quest line teaches it only once its first quest has started (ResidentQuests.MayTeach, the user 2026-11-20).
    /// </summary>
    public static void TeachWhatTheyKnow(ResidentDefinition resident, GameObject user, bool announce = true)
    {
        var crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (crafting == null || resident == null || !ResidentQuests.MayTeach(resident))
        {
            return;
        }

        foreach (JobDefinition job in resident.Jobs)
        {
            if (job != null)
            {
                // A workplace a quest introduces waits for that quest (BuildingGate): Ingrid's Cauldron for her third.
                crafting.UnlockBuildings(BuildingGate.Open(job.TeachesBuildings), announce);
            }
        }
    }

    /// <summary>
    /// Everyone living in Safe Haven teaches the player their workplaces, quietly: called whenever the player appears in a scene.
    /// A rescue at the end of a raid teaches the raid's player after their state was taken home (RaidDirector), so this is what
    /// makes what the rescued know arrive home with the player. Not in a friend's Haven: their people are theirs to learn from.
    /// </summary>
    public static void TeachEveryoneHome(GameObject user)
    {
        if (HavenVisit.IsVisiting || user == null)
        {
            return;
        }

        // What started quests teach (a save from before a quest listed its building) comes quietly too.
        BuildingGate.TeachStarted(user);
        foreach (ResidentRecord record in GameSession.Here.Residents)
        {
            // Someone home whose quest line never started (an older save, or a rescue before 2026-11-20): it starts now, and what
            // they teach comes with it, announced; everyone else's is taught quietly, as it always was.
            QuestDefinition first = ResidentQuests.FirstQuest(record.Resident);
            if (first != null && !ResidentQuests.MayTeach(record.Resident))
            {
                ResidentQuests.StartFirst(record.Resident, user);
                continue;
            }

            TeachWhatTheyKnow(record.Resident, user, false);
        }
    }

    /// <summary>Gives someone a job, in conversation or from the Haven window: records it and teaches the player what the job knows.</summary>
    public static void GiveJob(ResidentDefinition resident, JobDefinition job, GameObject user)
    {
        // The job is the Haven's to give, made where it is kept (HavenDecisions): a visitor's word goes to their friend's game.
        // What the job teaches, and the quests it counts for, are the player's own, and only in their own Haven.
        // Refused (it says why), nothing else happens: the job's recipes and the quests counting it came even when it wasn't given.
        bool made = HavenDecisions.Do("resident.job", resident.Id + "|" + (job != null ? job.Id : ""));
        if (!made || HavenVisit.IsVisiting || job == null)
        {
            return;
        }

        var crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (crafting != null)
        {
            crafting.UnlockBuildings(BuildingGate.Open(job.TeachesBuildings));
            crafting.Unlock(job.TeachesRecipes);
        }

        QuestDeeds.Raise(GameEventKind.JobAssigned, job, user, resident);
        if (job.AssignedEvent != null)
        {
            GameEvents.Raise(job.AssignedEvent, user);
        }
    }

    [HavenDecision("resident.job")]
    private static HavenDecisions.Outcome AssignJob(string args)
    {
        // By id, among the Haven's own residents and the work each can do.
        string[] parts = (args ?? "").Split('|');
        ResidentRecord record = HavenPeople.RecordOf(parts[0]);
        ResidentDefinition resident = record != null ? record.Resident : null;
        // Their own work or a job left vacant (SettlementState.JobsOpenTo, what every job list offers): only their own was taken,
        // and a fallen Smith's work could be offered to anyone but given to no one.
        JobDefinition job = null;
        foreach (JobDefinition can in resident != null ? GameSession.Here.JobsOpenTo(resident) : new List<JobDefinition>())
        {
            job = can != null && parts.Length > 1 && can.Id == parts[1] ? can : job;
        }

        if (resident == null || (job == null && parts.Length > 1 && parts[1].Length > 0))
        {
            return HavenDecisions.Outcome.Refused("They don't live here now, or can't do that work.");
        }

        // Quietly, as ever: the Haven window shows it (and a visitor sees it there when the mirror comes).
        GameSession.Home.AssignJob(resident, job);
        return HavenDecisions.Outcome.Made();
    }
}
