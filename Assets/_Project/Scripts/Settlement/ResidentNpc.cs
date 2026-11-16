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

    /// <summary>Sets who this is and where their home spot is. Called by ResidentSpawner.</summary>
    public void Bind(ResidentDefinition who)
    {
        resident = who;
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

        return null;
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
    public EmoteDefinition WorkGesture => Job?.WorkGesture;

    /// <summary>The job they hold now, or null.</summary>
    public JobDefinition Job => resident != null ? GameSession.Here.Find(resident)?.Job : null;

    /// <summary>Who they are and what they do (when nothing before it spoke), and the jobs they can take as answers.</summary>
    public void AddTopics(MenuPage page, DialogueSpeaker speaker, Action afterAnswer)
    {
        if (resident == null)
        {
            return;
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

        // A lesson in their job's skill, once between raids; in their own Haven only (as Liv's carving): a guest's "taught" mark
        // lived on the copy of the host's settlement, undone by the host's next word, and the lesson could be taken again and again.
        ResidentRecord record = GameSession.Here.Find(resident);
        if (current != null && current.Teaches != null && record != null && !HavenVisit.InHostsHaven)
        {
            ProficiencyDefinition skill = current.Teaches;
            string why = record.TaughtSinceRaid ? "Another, after your next raid survived." : $"What a {SettlementSettings.Current.RankFor(record.WorkDone).name} {current.DisplayName} knows";
            page.Add(new MenuAction($"Ask for a lesson in {skill.DisplayName}", why, !record.TaughtSinceRaid, () =>
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

        foreach (JobDefinition job in GameSession.Here.JobsOpenTo(resident))
        {
            if (job == null)
            {
                continue;
            }

            bool isCurrent = job == current;
            page.Add(new MenuAction(isCurrent ? $"Working as {job.DisplayName}" : $"Work as {job.DisplayName}", null, !isCurrent, () =>
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
    /// </summary>
    public static void TeachWhatTheyKnow(ResidentDefinition resident, GameObject user, bool announce = true)
    {
        var crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (crafting == null || resident == null)
        {
            return;
        }

        foreach (JobDefinition job in resident.Jobs)
        {
            if (job != null)
            {
                crafting.UnlockBuildings(job.TeachesBuildings, announce);
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

        foreach (ResidentRecord record in GameSession.Here.Residents)
        {
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
            crafting.UnlockBuildings(job.TeachesBuildings);
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
