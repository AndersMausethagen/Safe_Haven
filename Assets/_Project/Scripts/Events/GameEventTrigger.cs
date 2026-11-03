using UnityEngine;
using UnityEngine.Serialization;

/// <summary>When a GameEventTrigger raises its event.</summary>
public enum TriggerWhen
{
    /// <summary>The player walks into this object's trigger collider.</summary>
    PlayerEntersZone = 0,
    /// <summary>The player finishes interacting with this object (its Interactable completes).</summary>
    PlayerInteracts = 1,
    /// <summary>Only when something calls Raise, e.g. a button or another component's event in the Inspector.</summary>
    Manually = 2,
    /// <summary>The player is inside the trigger's Area, such as the portal's ward (it grows with the ward level). No collider needed.</summary>
    PlayerInsideArea = 3,
}

/// <summary>How often a GameEventTrigger may raise its event. Values match the old "Only Once" tick (0 off, 1 on).</summary>
public enum TriggerRepeat
{
    /// <summary>Every time it happens.</summary>
    EveryTime = 0,
    /// <summary>Once while the scene is loaded; again after the scene is reloaded.</summary>
    OncePerSceneLoad = 1,
    /// <summary>Once in the whole game: skipped if the event was ever raised before, even in an earlier session (saved).</summary>
    OncePerGame = 2,
}

/// <summary>
/// Raises a Quest Event when something happens to this object: the player walks into its zone, interacts with it, or
/// steps inside an area (any <see cref="IArea"/>, such as the portal's ward). For a zone, give the object a collider with Is Trigger ticked. Use it for "Reach
/// the Old Tower", "Inspect the shrine", "The moss blooms when you first come home". Its Raise method can also be chosen
/// from any event in the Inspector. Pair it with a Quest Event Listener to make something happen in the scene. Listed in
/// Add Component as Safe Haven › Quests › Quest Event Trigger; quick setups under GameObject › Safe Haven.
/// </summary>
[AddComponentMenu("Safe Haven/Quests/Quest Event Trigger")]
public sealed class GameEventTrigger : MonoBehaviour
{
    private const float AreaCheckInterval = 0.25f;

    [SerializeField, RaisedByGame] private GameEventDefinition gameEvent;
    [SerializeField] private TriggerWhen when = TriggerWhen.PlayerEntersZone;
    [Tooltip("For Player Inside Area: the area to watch, a component that is one (the portal, for its ward).")]
    [SerializeField] private Component area;
    [Tooltip("Once Per Game is remembered in the save, for story moments that must never repeat.")]
    [FormerlySerializedAs("onlyOnce")]
    [SerializeField] private TriggerRepeat repeat = TriggerRepeat.OncePerGame;
    [Tooltip("Optional. Only raises while this quest is active, so the zone does nothing before or after it matters.")]
    [SerializeField] private QuestDefinition onlyDuringQuest;
    [Tooltip("Optional. Only raises once this quest has started or is done, e.g. \"after the first raid\".")]
    [SerializeField] private QuestCondition requires;

    private Interactable interactable;
    private bool raised;
    private float nextAreaCheck;
    private bool wasInside;

    public GameEventDefinition Event => gameEvent;
    public TriggerWhen When => when;
    public TriggerRepeat Repeat => repeat;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (when == TriggerWhen.PlayerEntersZone && !HasTriggerCollider())
        {
            Debug.LogWarning($"{name}: Quest Event Trigger is set to Player Enters Zone but has no collider with Is Trigger ticked.", this);
        }

        if (when == TriggerWhen.PlayerInsideArea && !(area is IArea))
        {
            Debug.LogWarning($"{name}: Quest Event Trigger is set to Player Inside Area but has no area.", this);
        }
    }

    // A GameObject dropped on the field arrives as its first component; take the one that is an area.
    private void OnValidate()
    {
        if (area != null && !(area is IArea))
        {
            area = area.GetComponent<IArea>() as Component;
        }
    }

    private void OnEnable()
    {
        if (when == TriggerWhen.PlayerInteracts && interactable != null)
        {
            interactable.Completed.AddListener(OnInteracted);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(OnInteracted);
        }
    }

    private void Update()
    {
        if (when != TriggerWhen.PlayerInsideArea || Time.time < nextAreaCheck)
        {
            return;
        }

        nextAreaCheck = Time.time + AreaCheckInterval;
        Player player = Player.Current;
        bool inside = player != null && area is IArea watched && watched.Contains(player.transform.position);
        // Every Time raises on stepping in; one-time triggers keep trying while inside, so a condition that comes true
        // while the player is already in the area (finishing a quest at the portal) still counts.
        if (inside && (repeat != TriggerRepeat.EveryTime || !wasInside))
        {
            Raise(player.gameObject);
        }

        wasInside = inside;
    }

    /// <summary>True when every condition is met and the event would be raised right now.</summary>
    public bool CanRaise
    {
        get
        {
            if (gameEvent == null || (repeat != TriggerRepeat.EveryTime && raised))
            {
                return false;
            }

            if (repeat == TriggerRepeat.OncePerGame && GameSession.Quests.HasHappened(gameEvent))
            {
                return false;
            }

            if (onlyDuringQuest != null && GameSession.Quests.StatusOf(onlyDuringQuest) != QuestStatus.Active)
            {
                return false;
            }

            return requires.IsMet;
        }
    }

    /// <summary>Raises the event now (respecting Repeat and the quest conditions). Can be chosen from any Inspector event.</summary>
    public void Raise()
    {
        Raise(null);
    }

    private bool Raise(GameObject actor)
    {
        if (!CanRaise)
        {
            return false;
        }

        raised = true;
        GameEvents.Raise(gameEvent, actor);
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryZone(other);
    }

    // A player already standing in the zone when its quest condition becomes true (e.g. arriving home after a raid)
    // still counts, so one-time zones also check while the player stays inside.
    private void OnTriggerStay(Collider other)
    {
        if (repeat != TriggerRepeat.EveryTime && !raised)
        {
            TryZone(other);
        }
    }

    private void TryZone(Collider other)
    {
        if (when != TriggerWhen.PlayerEntersZone)
        {
            return;
        }

        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            Raise(player.gameObject);
        }
    }

    private void OnInteracted()
    {
        Raise(interactable.Interactor);
    }

    private bool HasTriggerCollider()
    {
        foreach (Collider found in GetComponentsInChildren<Collider>())
        {
            if (found.isTrigger)
            {
                return true;
            }
        }

        return false;
    }
}
