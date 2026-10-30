using UnityEngine;

/// <summary>
/// The place a raid event happens: on the root of its site prefab, with an Interactable (Use, stays active) when the event
/// has choices. Using it opens the choices in the ActionMenuWindow: the clock keeps running while the player thinks.
/// Choosing pays the price, carries out the outcomes and closes the site. A site without choices counts as found when
/// the player gets close: what stands there (a cache, a fog bank) does the rest.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidEventSite : MonoBehaviour, IInteractionName, IInteractionVerb
{
    [Tooltip("What marks the site while it waits: a pillar of light, a glow. Hidden once it is done.")]
    [SerializeField] private GameObject beacon;
    [Tooltip("Metres from the site at which a site without choices counts as found.")]
    [SerializeField, Min(1f)] private float findRadius = 8f;

    private RaidEventDefinition definition;
    private RaidEventContext context;
    private Interactable interactable;

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
    }

    private void Update()
    {
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

        var page = new MenuPage { Title = definition.DisplayName, Body = definition.SiteText };
        MaterialPool pool = MaterialPool.For(user);
        for (int i = 0; i < definition.Choices.Count; i++)
        {
            int index = i;
            RaidEventChoice choice = definition.Choices[i];
            bool canPay = choice.cost.Count == 0 || (pool != null && pool.CanAfford(null, choice.cost));
            string price = choice.cost.Count > 0 ? AmountText.Describe(choice.cost) : "";
            string detail = string.IsNullOrEmpty(price) ? choice.detail : string.IsNullOrEmpty(choice.detail) ? price : $"{price}. {choice.detail}";
            page.Add(new MenuAction(choice.label, detail, canPay, () => Choose(index, user), closesMenu: true));
        }

        return page;
    }

    private void Choose(int index, GameObject user)
    {
        RaidEventChoice choice = definition.Choices[index];
        MaterialPool pool = MaterialPool.For(user);
        if (IsResolved || (choice.cost.Count > 0 && (pool == null || !pool.TrySpend(null, choice.cost))))
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

        context.Session.RecordEvent($"{definition.DisplayName}: {choice.label}", definition.DisplayName);
    }

    // Amount in the game event says which choice it was: 1 for the first; for sites without choices, 1 for finding it.
    private void Resolve(int choice)
    {
        IsResolved = true;
        if (beacon != null)
        {
            beacon.SetActive(false);
        }

        if (interactable != null)
        {
            interactable.enabled = false;
        }

        GameEvents.Raise(new GameEvent(GameEventKind.RaidEvent, definition, choice, context.Player));
    }
}
