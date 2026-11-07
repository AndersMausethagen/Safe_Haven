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

    private void OnEnable()
    {
        GameEvents.Raised += OnGameEvent;
        GameSession.Settlement.Changed += GoToWork;
    }

    private void OnDisable()
    {
        GameEvents.Raised -= OnGameEvent;
        GameSession.Settlement.Changed -= GoToWork;
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

        life.Settle(homePosition, homeRotation, GameSession.Settlement.Find(who) is ResidentRecord record ? IndexOf(record) : 0);
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
        foreach (ResidentRecord other in GameSession.Settlement.Residents)
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
        IReadOnlyList<ResidentRecord> residents = GameSession.Settlement.Residents;
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

    /// <summary>Where they stand to work, when their job has a workplace that has been built.</summary>
    public bool TryGetWorkplace(out Vector3 spot, out Quaternion facing)
    {
        spot = homePosition;
        facing = homeRotation;
        JobDefinition job = resident != null ? GameSession.Settlement.Find(resident)?.Job : null;
        BuildingObject building = job != null && job.Workplace != null ? BuildingObject.Find(job.Workplace) : null;
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
    public JobDefinition Job => resident != null ? GameSession.Settlement.Find(resident)?.Job : null;

    /// <summary>Who they are and what they do (when nothing before it spoke), and the jobs they can take as answers.</summary>
    public void AddTopics(MenuPage page, DialogueSpeaker speaker, Action afterAnswer)
    {
        if (resident == null)
        {
            return;
        }

        JobDefinition current = GameSession.Settlement.Find(resident)?.Job;
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

        foreach (JobDefinition job in GameSession.Settlement.JobsOpenTo(resident))
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
        ResidentRecord record = GameSession.Settlement.Find(resident);
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
        FallenRecord loss = GameSession.Settlement.RecentLoss(settings.GriefRaids);
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
        SettlementState settlement = GameSession.Settlement;
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

    /// <summary>Gives someone a job, in conversation or from the Haven window: records it and teaches the player what the job knows.</summary>
    public static void GiveJob(ResidentDefinition resident, JobDefinition job, GameObject user)
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        GameSession.Settlement.AssignJob(resident, job);
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
}
