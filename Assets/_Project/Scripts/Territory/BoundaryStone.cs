using System.Text;
using UnityEngine;

/// <summary>
/// A region's boundary stone (Territory): the pact marked its borders with them. Touching it opens its menu: what the region is,
/// how the Haven stands there, and what can be done: light it (claim the region), feed it a Pact Rune (a firmer hold), or stand
/// watch when the forest is pushing in (StoneDefence). Its runes glow while the region is the Haven's, red while it is pushed.
/// Spawned at the atlas's stone points by TerritorySpawner; the prefab (BuildTerritory) carries the Interactable and collider.
/// </summary>
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.ItsChoicesDecide, "claiming and the watches are the host's (Territory.ForHost)")]
public sealed class BoundaryStone : MonoBehaviour, IInteractionName
{
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color LitColour = new Color(0.35f, 0.75f, 1.4f);
    private static readonly Color PushedColour = new Color(1.6f, 0.35f, 0.2f);

    [SerializeField] private string region;

    private Interactable interactable;
    private Renderer[] renderers;
    private Light glow;
    private MaterialPropertyBlock block;
    // While the quest that opens the stones asks for one to be lit, every unlit stone is a waypoint: the tracker points at the nearest.
    private QuestWaypoint waypoint;

    public string Region => region;
    public string InteractionName => $"Boundary Stone of {region}";

    public void Configure(string regionName)
    {
        region = regionName;
        TerritorySettings settings = TerritorySettings.Current;
        if (settings.UnlockedBy != null && settings.StonesObjective > 0)
        {
            waypoint = TryGetComponent(out QuestWaypoint existing) ? existing : gameObject.AddComponent<QuestWaypoint>();
            waypoint.Configure(settings.UnlockedBy, settings.StonesObjective, InteractionName);
        }

        if (block != null)
        {
            Show();
        }
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        renderers = GetComponentsInChildren<Renderer>();
        glow = GetComponentInChildren<Light>(true);
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
        Territory.Changed += Show;
        Show();
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        Territory.Changed -= Show;
    }

    private void Update()
    {
        // A pushed stone breathes red, so it can be found by its light at night.
        if (IsPushed && glow != null)
        {
            glow.intensity = Mathf.Lerp(0.6f, 2.2f, UIMotion.Breath(0.5f));
        }
    }

    private bool IsPushed => GameSession.Here.Territory.Incursion != null && GameSession.Here.Territory.Incursion.Region == region;

    private void Show()
    {
        bool claimed = GameSession.Here.Territory.IsClaimed(region);
        // A visitor's quest wants their own stones lit, not the host's.
        if (waypoint != null)
        {
            waypoint.enabled = !claimed && !HavenVisit.IsVisiting;
        }

        Color colour = IsPushed ? PushedColour : claimed ? LitColour : Color.black;
        foreach (Renderer part in renderers)
        {
            if (part == null)
            {
                continue;
            }

            part.GetPropertyBlock(block);
            block.SetColor(EmissionId, colour);
            part.SetPropertyBlock(block);
        }

        if (glow != null)
        {
            glow.enabled = claimed || IsPushed;
            glow.color = IsPushed ? new Color(1f, 0.35f, 0.25f) : new Color(0.5f, 0.75f, 1f);
            glow.intensity = 1.4f;
        }
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (user == null || ActionMenuWindow.Current == null)
        {
            return;
        }

        ActionMenuWindow.Current.Open(() => Page(user), user, transform);
    }

    /// <summary>The stones of remembered lands in this scene, in their regions' names' order.</summary>
    public static System.Collections.Generic.List<BoundaryStone> Lit()
    {
        TerritoryState state = GameSession.Here.Territory;
        var lit = new System.Collections.Generic.List<BoundaryStone>();
        foreach (BoundaryStone stone in FindObjectsByType<BoundaryStone>(FindObjectsInactive.Exclude))
        {
            if (state.IsClaimed(stone.region))
            {
                lit.Add(stone);
            }
        }

        lit.Sort((a, b) => string.CompareOrdinal(a.region, b.region));
        return lit;
    }

    private MenuPage Page(GameObject user)
    {
        TerritorySettings settings = TerritorySettings.Current;
        TerritoryState state = GameSession.Here.Territory;
        TerritorySettings.RegionInfo info = settings.Info(region);
        UIStyle style = UIStyle.Current;
        var body = new StringBuilder();
        if (info != null)
        {
            body.Append(info.description).Append('\n');
            body.Append($"<color={style.MutedHex}>{info.danger} · {info.gives}</color>\n\n");
        }

        body.Append($"<color={style.AccentHex}>{Territory.Describe(region)}</color>");
        body.Append($"\nPlaces found: {Territory.PlacesFound(region)} of {Territory.PlacesIn(region).Count}");
        string task = Territory.TaskText(region);
        if (task != null)
        {
            body.Append('\n').Append(task);
        }

        var page = new MenuPage { Title = $"The Boundary Stone of {region}", Body = body.ToString() };
        int runes = Territory.RunesHeld(user);
        if (!state.IsClaimed(region))
        {
            bool first = !state.ClaimNeedsRune(settings);
            bool asked = !first && Territory.AnArcAsks(region);
            bool can = Territory.CanClaim(region, user, out string why);
            page.Add(new MenuAction("Light the stone: remember the land", !can ? why
                    : first ? "The first stone answers to the hearth: no rune needed."
                    : asked ? "What you came out here for is enough: no rune needed." : $"A Pact Rune (you have {runes}).",
                can, () => Territory.Claim(region, user)));
        }
        else
        {
            bool can = Territory.CanOffer(region, user, out string why);
            page.Add(new MenuAction("Offer a Pact Rune", can ? $"Hold +{settings.RuneHold:0} (you have {runes})." : why, can, () => Territory.Offer(region, user)));
        }

        bool open = HavenTravel.IsOpen && !GameStateManager.InCombat;
        string shut = GameStateManager.InCombat ? "Not with something at your heels." : "Not during a breach.";
        // A lit stone knows the way home from the first one lit, and the portal knows the way back out to it (HomePortalMenu): a land
        // remembered isn't walked again (the user, 2026-11-27: "walking for several minutes is not fun").
        if (state.IsClaimed(region) && Place.InOwnHaven)
        {
            page.Add(new MenuAction("Walk the old way home", open ? "The hearth's own path, back to the portal." : shut,
                open, () => HavenTravel.GoTo(HomePortal.Current.transform), true) { Icon = "Lead" });
        }

        // The ways between the lit stones (Westwood remembered): to any other stone of a remembered land, in their names' order; not
        // with something at your heels (no running from the Old Grey) nor during a breach.
        if (state.IsClaimed(region) && HavenRegionSettings.Current.StoneTravelOpen)
        {
            var others = Lit();
            others.Remove(this);
            foreach (BoundaryStone other in others)
            {
                BoundaryStone to = other;
                page.Add(new MenuAction($"Walk the old way to {to.region}'s stone", open ? "The cairn-builders' marks lead there." : shut,
                    open, () => HavenTravel.GoTo(to.transform), true));
            }
        }

        if (IsPushed)
        {
            bool watching = StoneDefence.Current != null;
            bool may = Territory.CanStandWatch(out string notYours);
            page.Add(new MenuAction("Stand watch at the stone", !may ? notYours : watching ? "A watch is already being kept."
                    : "What the forest sends comes here. Beat it all and stay near the stone: the push is broken.",
                may && !watching, () => StoneDefence.Begin(this, user), true));
        }

        return page;
    }
}
