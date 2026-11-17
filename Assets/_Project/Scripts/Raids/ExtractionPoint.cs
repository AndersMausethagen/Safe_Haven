using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A way out of a raid, held rather than waited at (GDD §8.1). The player lights it once with Interact (paying its cost
/// and sounding its alarm), then has to hold its circle: the way home fills only while the player stands inside and no
/// hostile creature does (<see cref="ExtractionHold"/>). Leaving or a creature in the circle pauses it; nothing resets
/// it but the way going dark. A lit beacon calls out every few seconds (WorldNoise), so the forest comes to see. When
/// it is full, the RaidDirector ends the raid as extracted. The Interactable beside it should have no duration and must
/// not deactivate on completion. ExtractionZoneView draws the circle. HUDs list the points through <see cref="Active"/>.
/// Exits are choices, not just destinations (GDD §7.2 "Extraction"): a point can open late or go dark at set times on
/// the raid clock, be offered only in some raids (RaidGenerator rolls <see cref="AvailabilityChance"/>), ask for an
/// item to light it, take more or less time to hold, and sound an alarm that sends a spawn group hunting the player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.NotInAHaven, "a raid's way out")]
public sealed class ExtractionPoint : MonoBehaviour, IInteractionCondition, IInteractionNote, IInteractionVerb
{
    private static readonly List<ExtractionPoint> active = new List<ExtractionPoint>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    [SerializeField] private string displayName = "Extraction";
    [Tooltip("Seconds to hold when no raid is running, such as a scene without a RaidDirector.")]
    [SerializeField, Min(RaidParameters.MinimumExtractionTime)] private float fallbackSeconds = 5f;

    [Header("Window (seconds left on the raid clock)")]
    [Tooltip("Opens when this many seconds are left. 0 is open from the start.")]
    [SerializeField, Min(0f)] private float opensAtSecondsLeft;
    [Tooltip("Goes dark when this many seconds are left. 0 never closes. A lit beacon that goes dark loses what was held.")]
    [SerializeField, Min(0f)] private float closesAtSecondsLeft;

    [Header("Choice")]
    [Tooltip("Chance (0-1) this exit is offered in a raid, rolled from the raid's seed. At least one exit always is.")]
    [SerializeField, Range(0f, 1f)] private float availabilityChance = 1f;
    [Tooltip("Multiplies the raid's extraction time here: below 1 is a quick hold, above 1 a long one.")]
    [SerializeField, Range(0.2f, 4f)] private float timeMultiplier = 1f;
    [Tooltip("Optional item spent to light the way home here, such as a rune.")]
    [SerializeField] private ItemDefinition costItem;
    [SerializeField, Min(1)] private int costAmount = 1;
    [Tooltip("Optional spawn group (set to Alarm) woken when the beacon is lit.")]
    [SerializeField] private SpawnGroup alarm;
    [Tooltip("Shown when the alarm goes off.")]
    [SerializeField] private string alarmMessage = "The beacon's light draws the forest's attention!";

    [Header("Holding")]
    [Tooltip("Radius of the circle the player has to hold, in metres from this point.")]
    [SerializeField, Min(1f)] private float holdRadius = 5f;
    [Tooltip("How fast the hold fills while a hostile creature is in the circle too (0.5 = half speed, 0 = it waits).")]
    [SerializeField, Range(0f, 1f)] private float contestedHoldRate = 0.5f;
    [Tooltip("While lit, the beacon calls out this far every Call Interval: idle creatures inside come to look. 0 = silent.")]
    [SerializeField, Min(0f)] private float callRadius = 35f;
    [SerializeField, Min(0.5f)] private float callInterval = 5f;
    [Tooltip("A boss encounter's arrival stone: leaving by it before the gate turns back (RaidDirector.TurnBack): nothing gained and " +
             "nothing lost, no raid's end, so it can't be farmed as an extraction.")]
    [SerializeField] private bool turnsBack;

    private readonly ExtractionHold hold = new ExtractionHold();
    private Interactable interactable;
    private ExtractionStatus lastStatus;
    private ExtractionHoldState lastHoldState = ExtractionHoldState.Unlit;
    private bool alarmRaised;
    private bool extracted;
    private float nextCallAt;
    // What the clock has done to this way out so far (see Status).
    private bool openedOnce;
    private bool closedOnce;
    // Shut by a boss encounter until its boss falls (EncounterArena), with what the player is told; null when the clock decides.
    private string heldShut;

    /// <summary>Every enabled extraction point in the scene.</summary>
    public static IReadOnlyList<ExtractionPoint> Active => active;

    public string DisplayName => displayName;
    public float AvailabilityChance => availabilityChance;
    public ItemDefinition CostItem => costItem;
    public int CostAmount => costItem != null ? costAmount : 0;
    /// <summary>Another way out like this one, under another name, for a larger map. An alarm belongs to one place, so it stays here.</summary>
    public ExtractionPoint CopyAs(string newName)
    {
        ExtractionPoint copy = Instantiate(this, transform.parent);
        copy.name = newName;
        copy.displayName = newName;
        copy.alarm = null;
        copy.gameObject.SetActive(true);
        return copy;
    }

    public bool HasAlarm => alarm != null;
    public SpawnGroup Alarm => alarm;
    public float HoldRadius => holdRadius;

    /// <summary>Seconds of holding this point needs.</summary>
    public float Duration => Mathf.Max(RaidParameters.MinimumExtractionTime,
        (RaidDirector.Current != null ? RaidDirector.Current.Parameters.ExtractionTime : fallbackSeconds) * timeMultiplier);

    /// <summary>True once lit, until the raid ends or the way goes dark.</summary>
    public bool IsLit => hold.IsLit;
    public ExtractionHoldState HoldState => hold.State;
    /// <summary>0–1 of the way home held so far.</summary>
    public float HoldFraction => hold.Fraction;
    /// <summary>Seconds still to hold: the full duration before it is lit.</summary>
    public float TimeLeft => IsLit ? hold.Remaining : Duration;

    /// <summary>Open, not yet open or closed, by the raid clock. Always open outside a raid. Once open it doesn't close
    /// again before its time, and once closed it stays closed, though a site's choice adds time to the clock.</summary>
    public ExtractionStatus Status => closedOnce ? ExtractionStatus.Closed
        : heldShut != null ? ExtractionStatus.NotYetOpen
        : ExtractionWindow.StatusAt(openedOnce ? 0f : opensAtSecondsLeft, closesAtSecondsLeft, SecondsLeft);
    public bool IsOpen => Status == ExtractionStatus.Open;
    /// <summary>Seconds until this point opens or closes, or -1 when it won't change again.</summary>
    public float SecondsUntilChange => closedOnce || heldShut != null ? -1f
        : ExtractionWindow.SecondsUntilChange(openedOnce ? 0f : opensAtSecondsLeft, closesAtSecondsLeft, SecondsLeft);

    public string Verb => "Light the way home";

    /// <summary>True while a boss encounter keeps this way shut (<see cref="HoldShut"/>).</summary>
    public bool IsHeldShut => heldShut != null;

    /// <summary>Keeps this way home shut whatever the clock says, telling the player why (a boss encounter's, until its boss falls).</summary>
    public void HoldShut(string reason) => heldShut = string.IsNullOrWhiteSpace(reason) ? "Not yet." : reason;

    /// <summary>Lets the clock decide again: the way opens now if its window is open.</summary>
    public void Release() => heldShut = null;

    /// <summary>Closes this way home for the rest of the raid (a boss encounter's arrival stone, once the gate seals).</summary>
    public void Close()
    {
        heldShut = null;
        closedOnce = true;
    }

    /// <summary>A lit beacon blocks lighting it again; that message is a reminder, not a problem.</summary>
    public bool IsNote => IsLit;

    private static float SecondsLeft => RaidDirector.Current != null ? RaidDirector.Current.Session.TimeRemaining : float.MaxValue;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (!TryGetComponent(out ExtractionZoneView _))
        {
            gameObject.AddComponent<ExtractionZoneView>();
        }

        if (alarm != null && alarm.Trigger != SpawnTrigger.Alarm)
        {
            Debug.LogWarning($"Extraction point '{name}' uses '{alarm.name}' as its alarm, but that group isn't set to Alarm, so it also spawns on its own.", this);
        }
    }

    private void OnEnable()
    {
        active.Add(this);
        lastStatus = Status;
        interactable.Completed.AddListener(OnCompleted);
    }

    private void OnDisable()
    {
        active.Remove(this);
        interactable.Completed.RemoveListener(OnCompleted);
    }

    private void Update()
    {
        ExtractionStatus status = Status;
        // Only in a raid: outside one every point reads open.
        if (RaidDirector.Current != null)
        {
            openedOnce |= status == ExtractionStatus.Open;
            closedOnce |= status == ExtractionStatus.Closed;
        }

        if (status != lastStatus)
        {
            lastStatus = status;
            AnnounceChange(status);
        }

        if (!IsLit || extracted)
        {
            return;
        }

        if (status != ExtractionStatus.Open)
        {
            hold.Douse();
            lastHoldState = ExtractionHoldState.Unlit;
            GameMessages.Post($"{displayName} gutters out. What you held is lost.", MessageKind.Warning);
            return;
        }

        GameObject player = HolderCandidate();
        bool inside = player != null && Inside(player.transform.position);
        ExtractionHoldState state = hold.Tick(Time.deltaTime, inside, IsContested(), contestedHoldRate);
        if (state != lastHoldState)
        {
            AnnounceHold(state);
            lastHoldState = state;
        }

        if (Time.time >= nextCallAt)
        {
            nextCallAt = Time.time + callInterval;
            // Shared: on a co-op friend's game the creatures are the host's, and the call must reach them there.
            WorldNoise.EmitShared(transform.position, callRadius);
        }

        if (state == ExtractionHoldState.Done)
        {
            Leave(player);
        }
    }

    /// <summary>
    /// Blocks lighting while closed, when the player can't pay the cost, and once lit (then it reminds them to hold the
    /// circle). Asked by the Interactable.
    /// </summary>
    public bool CanInteract(GameObject user, out string reason)
    {
        switch (Status)
        {
            case ExtractionStatus.NotYetOpen:
                reason = heldShut ?? $"{displayName} opens in {RaidStats.FormatTime(SecondsUntilChange)}.";
                return false;
            case ExtractionStatus.Closed:
                reason = $"{displayName} has gone dark.";
                return false;
        }

        if (IsLit)
        {
            reason = "Lit. Hold the circle until the way opens.";
            return false;
        }

        if (costItem != null && MaterialPool.For(user).Have(costItem) < costAmount)
        {
            reason = $"Needs {CostText} to light the way.";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>The cost as shown to the player, such as "2× Wayfinder Rune", or empty when free.</summary>
    public string CostText => costItem == null ? "" : costAmount > 1 ? $"{costAmount}× {costItem.DisplayName}" : costItem.DisplayName;

    /// <summary>True when a creature that would attack the player stands inside the circle.</summary>
    public bool IsContested()
    {
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster.CurrentState != MonsterAI.State.Dead && monster.Definition != null
                && monster.Definition.Faction.WillAttack(Faction.Player) && Inside(monster.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a position is inside the circle (measured on the ground plane).</summary>
    public bool Inside(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= holdRadius * holdRadius;
    }

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

    // The player is the one who holds it in a raid; outside one (a test scene), whoever is the player.
    private static GameObject HolderCandidate()
    {
        if (RaidDirector.Current != null)
        {
            return RaidDirector.Current.Player;
        }

        return Player.Current != null ? Player.Current.gameObject : null;
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

    private void AnnounceHold(ExtractionHoldState state)
    {
        switch (state)
        {
            case ExtractionHoldState.Contested:
                GameMessages.Post("Something is in the circle! The way opens slower until you drive it out.", MessageKind.Warning);
                break;
            case ExtractionHoldState.Waiting when lastHoldState == ExtractionHoldState.Holding
                                                  || lastHoldState == ExtractionHoldState.Contested:
                GameMessages.Post($"Back into the circle! {hold.Remaining:0} s still to hold.", MessageKind.Warning);
                break;
        }
    }

    private void OnCompleted()
    {
        GameObject user = interactable.Interactor;
        // The way may have closed, or the cost been dropped, in the moment of the press.
        if (!CanInteract(user, out string reason))
        {
            GameMessages.Post(reason, MessageKind.Warning);
            return;
        }

        if (costItem != null && !MaterialPool.For(user).TrySpend(null, new[] { new ItemAmount(costItem, costAmount) }))
        {
            GameMessages.Post($"Needs {CostText} to light the way.", MessageKind.Warning);
            return;
        }

        hold.Light(Duration);
        if (PlayerTransport.Current != null)
        {
            PlayerTransport.Current.WarnIfLeavingCart();
        }

        lastHoldState = hold.State;
        nextCallAt = Time.time;
        GameMessages.Post($"{displayName} is lit! Hold its circle for {Duration:0} seconds. The forest will come.", MessageKind.Warning);
        RaiseAlarm();
    }

    private void RaiseAlarm()
    {
        if (alarm == null || alarmRaised || RaidDirector.Current == null)
        {
            return;
        }

        alarmRaised = true;
        if (RaidDirector.Current.Creatures.Wake(alarm) > 0 && !string.IsNullOrEmpty(alarmMessage))
        {
            GameMessages.Post(alarmMessage, MessageKind.Warning);
        }
    }

    private void Leave(GameObject player)
    {
        extracted = true;
        RaidDirector director = RaidDirector.Current;
        if (director == null)
        {
            GameMessages.Post("There is no raid to leave.", MessageKind.Warning);
            return;
        }

        if (turnsBack && director.TurnBack(player))
        {
            return;
        }

        director.Extract(player);
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

        UnityEditor.Handles.color = new Color(0.55f, 0.92f, 0.55f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, holdRadius);
        if (callRadius > 0f)
        {
            UnityEditor.Handles.color = new Color(1f, 0.75f, 0.3f, 0.35f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, callRadius);
        }

        UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, string.Join("\n", lines));
    }
#endif
}
