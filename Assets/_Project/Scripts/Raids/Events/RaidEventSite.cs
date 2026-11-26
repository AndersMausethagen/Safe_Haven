using UnityEngine;

/// <summary>
/// The place a raid event happens: on the root of its site prefab, with an Interactable (Use, stays active) when the event
/// has choices. Using it opens the choices in the ActionMenuWindow: the clock keeps running while the player thinks.
/// Choosing pays the price, carries out the outcomes and closes the site. A site without choices counts as found when
/// the player gets close: what stands there (a cache, a fog bank) does the rest.
/// </summary>
[DisallowMultipleComponent]
[InAFriendsHaven(HavenUse.NotInAHaven, "a raid's event: its co-op rule is the raid's (the host's game)")]
public sealed class RaidEventSite : MonoBehaviour, IInteractionName, IInteractionVerb
{
    [Tooltip("What marks the site while it waits: a pillar of light, a glow. Hidden once it is done.")]
    [SerializeField] private GameObject beacon;
    [Tooltip("What sets it apart from the woods around it, close to: a glowing ring on the ground, slow motes and a soft " +
             "light. Unlike the beacon it shows from the start (it is only seen once the place is in view), and goes out once done.")]
    [SerializeField] private GameObject mark;
    [Tooltip("Metres from the site at which a site without choices counts as found.")]
    [SerializeField, Min(1f)] private float findRadius = 8f;

    private RaidEventDefinition definition;
    private RaidEventContext context;
    private Interactable interactable;
    private float beaconShowsWithin = -1f;
    private bool found;

    /// <summary>The place it stands in, when a rumour put it in a set-piece. Empty otherwise.</summary>
    public string PlaceName { get; set; } = "";

    /// <summary>The beacon stays out until the player is this close: what a rumour points to has to be found.</summary>
    public void HideBeaconBeyond(float metres) => beaconShowsWithin = metres;

    /// <summary>True when the player here has found it: at once, unless a rumour hid its beacon. Once found it stays found
    /// (review 2026-11-16): walking away from a rescue site already seen doesn't turn its map mark back into a search area.</summary>
    public bool IsFoundFrom(Vector3 position)
    {
        if (found || beaconShowsWithin < 0f)
        {
            return true;
        }

        Vector3 offset = transform.position - position;
        offset.y = 0f;
        found = offset.sqrMagnitude <= beaconShowsWithin * beaconShowsWithin;
        return found;
    }

    /// <summary>Where a map marks it from there: roughly, until it has been found.</summary>
    public Vector3 MarkFrom(Vector3 position) => IsFoundFrom(position) ? transform.position : RumourWay.RoughSpot(transform.position);

    public bool IsResolved { get; private set; }
    public string DisplayName => definition != null ? definition.DisplayName : name;
    public string InteractionName => DisplayName;
    public string Verb => "Look closer";

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.Completed.AddListener(OnUsed);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(OnUsed);
        }
    }

    public void Bind(RaidEventDefinition eventDefinition, RaidEventContext eventContext)
    {
        definition = eventDefinition;
        context = eventContext;
        BegunAt = context?.Session != null ? context.Session.Elapsed : 0f;
        if (context?.Player != null)
        {
            Vector3 offset = transform.position - context.Player.transform.position;
            offset.y = 0f;
            BegunMetres = offset.magnitude;
        }
    }

    /// <summary>Lights the site's ring again while a task runs there (hold the bell): the ring says where to stand.</summary>
    public void ShowMark(bool shown)
    {
        if (mark != null)
        {
            mark.SetActive(shown);
        }
    }

    /// <summary>The event this site is for.</summary>
    public RaidEventDefinition Definition => definition;

    /// <summary>The raid's seconds when it began, and how far off the player was (the playtest log's EventSkipped).</summary>
    public float BegunAt { get; private set; }
    public float BegunMetres { get; private set; }

    /// <summary>True once the player has come within <see cref="ReachedWithin"/> metres (the log's EventReached).</summary>
    public bool WasReached { get; private set; }

    public const float ReachedWithin = 10f;

    private void NoteReached()
    {
        if (WasReached || definition == null || context?.Player == null)
        {
            return;
        }

        Vector3 offset = transform.position - context.Player.transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= ReachedWithin * ReachedWithin)
        {
            WasReached = true;
            PlaytestLogger.Note(PlaytestLog.EventReached, definition.name, Mathf.RoundToInt(context.Session != null ? context.Session.Elapsed : 0f));
        }
    }

    private void ShowBeaconByDistance()
    {
        if (beacon == null || beaconShowsWithin < 0f || IsResolved || context?.Player == null)
        {
            return;
        }

        bool close = (context.Player.transform.position - transform.position).sqrMagnitude <= beaconShowsWithin * beaconShowsWithin;
        found |= close;
        if (beacon.activeSelf != close)
        {
            beacon.SetActive(close);
        }
    }

    private void Update()
    {
        ShowBeaconByDistance();
        NoteReached();
        if (IsResolved || definition == null || definition.Choices.Count > 0 || context.Player == null)
        {
            return;
        }

        if ((context.Player.transform.position - transform.position).sqrMagnitude <= findRadius * findRadius)
        {
            if (!string.IsNullOrWhiteSpace(definition.SiteText))
            {
                GameMessages.Post(definition.SiteText);
            }

            Resolve(1);
        }
    }

    private void OnUsed()
    {
        if (IsResolved || definition == null || definition.Choices.Count == 0)
        {
            return;
        }

        GameObject user = interactable.Interactor;
        ActionMenuWindow menu = ActionMenuWindow.Current;
        if (menu == null)
        {
            Debug.LogError("A raid event needs an ActionMenuWindow in the scene.", this);
            return;
        }

        menu.Open(() => BuildPage(user), user, transform);
    }

    private MenuPage BuildPage(GameObject user)
    {
        if (IsResolved)
        {
            return null;
        }

        var page = new MenuPage { Title = definition.DisplayName, Body = definition.SiteText, Picture = definition.Artwork };
        MaterialPool pool = MaterialPool.For(user);
        for (int i = 0; i < definition.Choices.Count; i++)
        {
            int index = i;
            RaidEventChoice choice = definition.Choices[i];
            bool canPay = choice.cost.Count == 0 || (pool != null && pool.CanAfford(null, choice.cost));
            string price = choice.cost.Count > 0 ? AmountText.Describe(choice.cost) : "";
            string detail = string.IsNullOrEmpty(price) ? choice.detail : string.IsNullOrEmpty(choice.detail) ? price : $"{price}. {choice.detail}";
            // A need not met: shown, greyed, with what it needs first (Docs/RaidEventsPlan_2026-11-26.md E2).
            ChoiceRequirement unmet = choice.Unmet(user, context?.Session);
            if (unmet != null)
            {
                detail = string.IsNullOrEmpty(detail) ? unmet.Reason : $"{unmet.Reason}. {detail}";
            }

            page.Add(new MenuAction(choice.label, detail, canPay && unmet == null, () => Choose(index, user), closesMenu: true));
        }

        return page;
    }

    private void Choose(int index, GameObject user)
    {
        RaidEventChoice choice = definition.Choices[index];
        MaterialPool pool = MaterialPool.For(user);
        if (IsResolved || choice.Unmet(user, context?.Session) != null || (choice.cost.Count > 0 && (pool == null || !pool.TrySpend(null, choice.cost))))
        {
            return;
        }

        Resolve(index + 1);
        if (!string.IsNullOrWhiteSpace(choice.result))
        {
            GameMessages.Post(choice.result, MessageKind.Gain);
        }

        foreach (RaidEventOutcome outcome in choice.outcomes)
        {
            outcome?.Apply(context);
        }

        // After the outcomes, so a quest that counts it sees what they did (a creature come through, a thing given).
        if (definition.RaisesWhenChosen != null)
        {
            GameEvents.Raise(new GameEvent(GameEventKind.Custom, definition.RaisesWhenChosen, 1, user));
        }

        context.Session.RecordEvent($"{definition.DisplayName}: {choice.label}", definition.DisplayName);
        PlaytestLogger.Note(PlaytestLog.EventChoice, definition.name, index + 1, choice.label);
    }

    // Amount in the game event says which choice it was: 1 for the first; for sites without choices, 1 for finding it.
    private void Resolve(int choice)
    {
        IsResolved = true;
        if (beacon != null)
        {
            beacon.SetActive(false);
        }

        if (mark != null)
        {
            mark.SetActive(false);
        }

        if (interactable != null)
        {
            interactable.enabled = false;
        }

        GameEvents.Raise(new GameEvent(GameEventKind.RaidEvent, definition, choice, context.Player));
    }
}
