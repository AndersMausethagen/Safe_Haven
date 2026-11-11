using System.Text;
using UnityEngine;

/// <summary>
/// A region's boundary stone (Territory): the pact marked its borders with them. Touching it opens its menu: what the region is,
/// how the Haven stands there, and what can be done: light it (claim the region), feed it a Pact Rune (a firmer hold), or stand
/// watch when the forest is pushing in (StoneDefence). Its runes glow while the region is the Haven's, red while it is pushed.
/// Spawned at the atlas's stone points by TerritorySpawner; the prefab (BuildTerritory) carries the Interactable and collider.
/// </summary>
[RequireComponent(typeof(Interactable))]
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

    public string Region => region;
    public string InteractionName => $"Boundary Stone of {region}";

    public void Configure(string regionName)
    {
        region = regionName;
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

    private bool IsPushed => GameSession.Settlement.Territory.Incursion != null && GameSession.Settlement.Territory.Incursion.Region == region;

    private void Show()
    {
        bool claimed = GameSession.Settlement.Territory.IsClaimed(region);
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

    private MenuPage Page(GameObject user)
    {
        TerritorySettings settings = TerritorySettings.Current;
        TerritoryState state = GameSession.Settlement.Territory;
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
            bool free = !state.ClaimNeedsRune(settings);
            bool can = Territory.CanClaim(region, user, out string why);
            page.Add(new MenuAction("Light the boundary stone", can ? (free ? "The first stone answers to the hearth: no rune needed." : $"A Pact Rune (you have {runes}).") : why,
                can, () => Territory.Claim(region, user)));
        }
        else
        {
            bool can = Territory.CanOffer(region, user, out string why);
            page.Add(new MenuAction("Offer a Pact Rune", can ? $"Hold +{settings.RuneHold:0} (you have {runes})." : why, can, () => Territory.Offer(region, user)));
        }

        if (IsPushed)
        {
            bool watching = StoneDefence.Current != null;
            page.Add(new MenuAction("Stand watch at the stone", watching ? "A watch is already being kept."
                    : "What the forest sends comes here. Beat it all and stay near the stone: the push is broken.",
                !watching, () => StoneDefence.Begin(this, user), true));
        }

        return page;
    }
}
