using System;
using System.Text;
using UnityEngine;

/// <summary>
/// A resident living in Safe Haven (GDD §15.3–15.4). In their conversation (their Talkable) they tell who they are, and
/// the jobs they can take are answers; giving a job records it in SettlementState and teaches the player what the job knows (the Alchemist teaches the
/// Purification Altar). When their job has a workplace that has been built, they stand by it, so hiring someone visibly
/// changes the settlement.
/// <para>
/// Created by ResidentSpawner for every resident in SettlementState; it needs a Talkable whose Interactable doesn't
/// deactivate.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Talkable))]
public sealed class ResidentNpc : MonoBehaviour, IConversationTopics
{
    private const string DefaultAcceptLine = "I'll get to it.";

    private ResidentDefinition resident;
    private Vector3 homePosition;
    private Quaternion homeRotation;

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
        GoToWork();
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.Built)
        {
            GoToWork();
        }
    }

    // Stands next to their workplace if one has been built; otherwise at home.
    private void GoToWork()
    {
        JobDefinition job = resident != null ? GameSession.Settlement.Find(resident)?.Job : null;
        if (job != null && job.Workplace != null)
        {
            BuildingObject building = BuildingObject.Find(job.Workplace);
            if (building != null)
            {
                Vector3 offset = building.transform.right * 1.8f;
                transform.position = building.transform.position + offset;
                transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
                return;
            }
        }

        transform.SetPositionAndRotation(homePosition, homeRotation);
    }

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
                ? $"<color=#E8C46A>Works as the {current.DisplayName}.</color> {current.Description}"
                : "<color=#E8C46A>Has no job yet.</color>");
            page.Body = body.ToString();
        }

        foreach (JobDefinition job in resident.Jobs)
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

    private void Assign(JobDefinition job, GameObject user) => GiveJob(resident, job, user);

    /// <summary>Gives someone a job, in conversation or from the Haven window: records it and teaches the player what the job knows.</summary>
    public static void GiveJob(ResidentDefinition resident, JobDefinition job, GameObject user)
    {
        GameSession.Settlement.AssignJob(resident, job);
        var crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (crafting != null)
        {
            crafting.UnlockBuildings(job.TeachesBuildings);
            crafting.Unlock(job.TeachesRecipes);
        }

        if (job.AssignedEvent != null)
        {
            GameEvents.Raise(job.AssignedEvent, user);
        }
    }
}
