using System.Text;
using UnityEngine;

/// <summary>
/// A resident living in Safe Haven (GDD §15.3–15.4). Talking to them opens a menu with who they are and the jobs they can
/// take; giving a job records it in SettlementState and teaches the player what the job knows (the Alchemist teaches the
/// Purification Altar). When their job has a workplace that has been built, they stand by it, so hiring someone visibly
/// changes the settlement.
/// <para>
/// Created by ResidentSpawner for every resident in SettlementState; it needs an Interactable that doesn't deactivate.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class ResidentNpc : MonoBehaviour
{
    [Tooltip("The menu closes when the player walks further than this, in meters.")]
    [SerializeField, Min(1f)] private float menuRange = 6f;

    private Interactable interactable;
    private ResidentDefinition resident;
    private Vector3 homePosition;
    private Quaternion homeRotation;

    public ResidentDefinition Resident => resident;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        // Talking opens at once: no work bar.
        if (interactable != null) interactable.Instant = true;
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
        GameEvents.Raised += OnGameEvent;
        GameSession.Settlement.Changed += GoToWork;
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
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

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (resident == null || ActionMenuWindow.Current == null)
        {
            return;
        }

        ActionMenuWindow.Current.Open(() => BuildPage(user), user, transform, menuRange);
    }

    private MenuPage BuildPage(GameObject user)
    {
        JobDefinition current = GameSession.Settlement.Find(resident)?.Job;
        var body = new StringBuilder(resident.Description);
        body.Append("\n\n").Append(current != null
            ? $"<color=#E8C46A>Works as the {current.DisplayName}.</color> {current.Description}"
            : "<color=#E8C46A>Has no job yet.</color>");

        var page = new MenuPage { Title = resident.DisplayName, Body = body.ToString() };
        foreach (JobDefinition job in resident.Jobs)
        {
            if (job == null)
            {
                continue;
            }

            bool isCurrent = job == current;
            page.Add(new MenuAction(isCurrent ? $"Working as {job.DisplayName}" : $"Work as {job.DisplayName}",
                isCurrent ? null : job.Description, !isCurrent, () => Assign(job, user)));
        }

        return page;
    }

    private void Assign(JobDefinition job, GameObject user)
    {
        GameSession.Settlement.AssignJob(resident, job);
        var crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (crafting != null)
        {
            crafting.UnlockBuildings(job.TeachesBuildings);
            crafting.Unlock(job.TeachesRecipes);
        }

        if (!string.IsNullOrWhiteSpace(job.AcceptLine))
        {
            GameMessages.Post($"{resident.DisplayName}: \"{job.AcceptLine}\"", MessageKind.Gain);
        }

        if (job.AssignedEvent != null)
        {
            GameEvents.Raise(job.AssignedEvent, user);
        }
    }
}
