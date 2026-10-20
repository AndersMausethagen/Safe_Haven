using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A way out of a raid. The player holds Interact on it for the raid's extraction time (scaled by this point's
/// <see cref="timeMultiplier"/>); moving cancels it like any other interaction. When it completes, the RaidDirector ends
/// the raid as extracted. The Interactable beside it should have no duration (this component completes it) and must not
/// deactivate on completion. HUDs list the points through <see cref="Active"/>.
/// Exits are choices, not just destinations (GDD §7.2 "Extraction"): a point can open late or go dark at set times on
/// the raid clock, be offered only in some raids (RaidGenerator rolls <see cref="AvailabilityChance"/>), ask for an
/// item to pay its way, extract faster or slower, and sound an alarm that sends a spawn group hunting the player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class ExtractionPoint : MonoBehaviour, IInteractionCondition
{
    private static readonly List<ExtractionPoint> active = new List<ExtractionPoint>();

    [SerializeField] private string displayName = "Extraction";
    [Tooltip("Seconds to extract when no raid is running, such as a scene without a RaidDirector.")]
    [SerializeField, Min(RaidParameters.MinimumExtractionTime)] private float fallbackSeconds = 5f;

    [Header("Window (seconds left on the raid clock)")]
    [Tooltip("Opens when this many seconds are left. 0 is open from the start.")]
    [SerializeField, Min(0f)] private float opensAtSecondsLeft;
    [Tooltip("Goes dark when this many seconds are left. 0 never closes.")]
    [SerializeField, Min(0f)] private float closesAtSecondsLeft;

    [Header("Choice")]
    [Tooltip("Chance (0-1) this exit is offered in a raid, rolled from the raid's seed. At least one exit always is.")]
    [SerializeField, Range(0f, 1f)] private float availabilityChance = 1f;
    [Tooltip("Multiplies the raid's extraction time here: below 1 is a quick exit, above 1 a slow one.")]
    [SerializeField, Range(0.2f, 4f)] private float timeMultiplier = 1f;
    [Tooltip("Optional item spent to leave through here, such as a rune that wakes the way home.")]
    [SerializeField] private ItemDefinition costItem;
    [SerializeField, Min(1)] private int costAmount = 1;
    [Tooltip("Optional spawn group (set to Alarm) woken the first time anyone starts extracting here.")]
    [SerializeField] private SpawnGroup alarm;
    [Tooltip("Shown when the alarm goes off.")]
    [SerializeField] private string alarmMessage = "The beacon's light draws the forest's attention!";

    private Interactable interactable;
    private float elapsed;
    private ExtractionStatus lastStatus;
    private bool alarmRaised;

    /// <summary>Every enabled extraction point in the scene.</summary>
    public static IReadOnlyList<ExtractionPoint> Active => active;

    public string DisplayName => displayName;
    public float AvailabilityChance => availabilityChance;
    public ItemDefinition CostItem => costItem;
    public int CostAmount => costItem != null ? costAmount : 0;
    public bool HasAlarm => alarm != null;
    public SpawnGroup Alarm => alarm;
    public bool IsExtracting => interactable != null && interactable.IsInUse;
    public float Duration => Mathf.Max(RaidParameters.MinimumExtractionTime,
        (RaidDirector.Current != null ? RaidDirector.Current.Parameters.ExtractionTime : fallbackSeconds) * timeMultiplier);
    public float TimeLeft => IsExtracting ? Mathf.Max(0f, Duration - elapsed) : Duration;

    /// <summary>Open, not yet open or closed, by the raid clock. Always open outside a raid.</summary>
    public ExtractionStatus Status => ExtractionWindow.StatusAt(opensAtSecondsLeft, closesAtSecondsLeft, SecondsLeft);
    public bool IsOpen => Status == ExtractionStatus.Open;
    /// <summary>Seconds until this point opens or closes, or -1 when it won't change again.</summary>
    public float SecondsUntilChange => ExtractionWindow.SecondsUntilChange(opensAtSecondsLeft, closesAtSecondsLeft, SecondsLeft);

    private static float SecondsLeft => RaidDirector.Current != null ? RaidDirector.Current.Session.TimeRemaining : float.MaxValue;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (alarm != null && alarm.Trigger != SpawnTrigger.Alarm)
        {
            Debug.LogWarning($"Extraction point '{name}' uses '{alarm.name}' as its alarm, but that group isn't set to Alarm, so it also spawns on its own.", this);
        }
    }

    private void OnEnable()
    {
        active.Add(this);
        lastStatus = Status;
        interactable.Started.AddListener(OnStarted);
        interactable.Completed.AddListener(OnCompleted);
        interactable.Cancelled.AddListener(OnCancelled);
    }

    private void OnDisable()
    {
        active.Remove(this);
        interactable.Started.RemoveListener(OnStarted);
        interactable.Completed.RemoveListener(OnCompleted);
        interactable.Cancelled.RemoveListener(OnCancelled);
    }

    private void Update()
    {
        ExtractionStatus status = Status;
        if (status != lastStatus)
        {
            lastStatus = status;
            AnnounceChange(status);
        }

        if (!IsExtracting)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= Duration)
        {
            interactable.Complete();
        }
    }

    /// <summary>Blocks extracting while closed, and when the player can't pay the cost. Asked by the Interactable.</summary>
    public bool CanInteract(GameObject user, out string reason)
    {
        switch (Status)
        {
            case ExtractionStatus.NotYetOpen:
                reason = $"{displayName} opens in {RaidStats.FormatTime(SecondsUntilChange)}.";
                return false;
            case ExtractionStatus.Closed:
                reason = $"{displayName} has gone dark.";
                return false;
        }

        if (costItem != null && MaterialPool.For(user).Have(costItem) < costAmount)
        {
            reason = $"Needs {CostText} to open the way.";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>The cost as shown to the player, such as "2× Wayfinder Rune", or empty when free.</summary>
    public string CostText => costItem == null ? "" : costAmount > 1 ? $"{costAmount}× {costItem.DisplayName}" : costItem.DisplayName;

    /// <summary>The closest open extraction point to a position, measured to its nearest edge, or null when there is none.</summary>
    public static ExtractionPoint FindNearest(Vector3 position, out float distance)
    {
        return FindNearest(position, out distance, true);
    }

    /// <summary>The closest extraction point to a position; with openOnly, only those that can be used right now.</summary>
    public static ExtractionPoint FindNearest(Vector3 position, out float distance, bool openOnly)
    {
        ExtractionPoint nearest = null;
        distance = float.MaxValue;
        foreach (ExtractionPoint point in active)
        {
            if (openOnly && !point.IsOpen)
            {
                continue;
            }

            float candidate = point.DistanceFrom(position);
            if (candidate < distance)
            {
                nearest = point;
                distance = candidate;
            }
        }

        return nearest;
    }

    /// <summary>Horizontal distance from a position to this point's nearest edge.</summary>
    public float DistanceFrom(Vector3 position)
    {
        return interactable.DistanceFrom(position);
    }

    private void AnnounceChange(ExtractionStatus status)
    {
        if (RaidDirector.Current == null || RaidDirector.Current.Session.IsOver)
        {
            return;
        }

        if (status == ExtractionStatus.Open)
        {
            GameMessages.Post($"{displayName} has opened. A new way home!", MessageKind.Gain);
        }
        else if (status == ExtractionStatus.Closed)
        {
            GameMessages.Post($"{displayName} has gone dark.", MessageKind.Warning);
        }
    }

    private void OnStarted()
    {
        elapsed = 0f;
        GameMessages.Post($"Extracting... hold still for {Duration:0.#} seconds.");
        RaiseAlarm();
    }

    private void RaiseAlarm()
    {
        if (alarm == null || alarmRaised || RaidDirector.Current == null)
        {
            return;
        }

        alarmRaised = true;
        if (RaidDirector.Current.Wake(alarm) > 0 && !string.IsNullOrEmpty(alarmMessage))
        {
            GameMessages.Post(alarmMessage, MessageKind.Warning);
        }
    }

    private void OnCompleted()
    {
        elapsed = 0f;
        RaidDirector director = RaidDirector.Current;
        if (director == null)
        {
            GameMessages.Post("There is no raid to leave.", MessageKind.Warning);
            return;
        }

        // The way may have closed, or the cost been dropped, while the player held still.
        GameObject user = interactable.Interactor;
        if (!CanInteract(user, out string reason))
        {
            GameMessages.Post(reason, MessageKind.Warning);
            return;
        }

        if (costItem != null && !MaterialPool.For(user).TrySpend(null, new[] { new ItemAmount(costItem, costAmount) }))
        {
            GameMessages.Post($"Needs {CostText} to open the way.", MessageKind.Warning);
            return;
        }

        director.Extract(user);
    }

    private void OnCancelled()
    {
        elapsed = 0f;
        RaidDirector director = RaidDirector.Current;
        if (director == null || !director.Session.IsOver)
        {
            GameMessages.Post("Extraction interrupted.", MessageKind.Warning);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var lines = new List<string> { displayName };
        if (opensAtSecondsLeft > 0f)
        {
            lines.Add($"opens at {RaidStats.FormatTime(opensAtSecondsLeft)} left");
        }

        if (closesAtSecondsLeft > 0f)
        {
            lines.Add($"closes at {RaidStats.FormatTime(closesAtSecondsLeft)} left");
        }

        if (availabilityChance < 1f)
        {
            lines.Add($"{availabilityChance:P0} of raids");
        }

        if (costItem != null)
        {
            lines.Add("costs " + CostText);
        }

        if (alarm != null)
        {
            lines.Add("alarm: " + alarm.name);
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.6f);
            Gizmos.DrawLine(transform.position + Vector3.up, alarm.transform.position + Vector3.up);
        }

        UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, string.Join("\n", lines));
    }
#endif
}