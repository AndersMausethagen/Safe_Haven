using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Trading Post (GDD §17.3, Docs/TradeResearch_2026-11-28.md): where the traders stop. Its menu names who is here now (the
/// Pedlar, the Tinker, the Hidden Folk after dark), each opening the barter window (TradeWindow), and keeps the caravan's horse trade
/// (HorseTrader). The post rolls each homecoming's visits once it stands (Caravans.EnsureRolled) and says who has come.
/// <para>
/// Where it belongs: on the Trading Post building prefab, next to an Interactable that doesn't deactivate.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.ItsChoicesDecide, "trades take the host's stores: the host's (HavenVisit.LeftToHost)")]
public sealed class TradingPost : MonoBehaviour
{
    [SerializeField, Min(1f)] private float menuRange = 6f;
    [Tooltip("Seconds between looking whether a homecoming's traders have been rolled.")]
    [SerializeField, Min(0.5f)] private float checkEvery = 2f;

    private Interactable interactable;
    private Construction construction;
    private float nextCheck;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        construction = GetComponentInParent<Construction>();
    }

    private void OnEnable() => interactable.Completed.AddListener(OnUsed);

    private void OnDisable() => interactable.Completed.RemoveListener(OnUsed);

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + checkEvery;
        // A friend's Haven is the host's: their traders, rolled in their game.
        // A plan or a scaffold is no post yet: nobody comes to it, and the Pedlar's first visit waits until it stands.
        if (HavenVisit.IsVisiting || !Place.AtHome || GameSession.Home == null || construction != null && (construction.IsPlan || !construction.IsStanding))
        {
            return;
        }

        List<TraderDefinition> arrived = Caravans.EnsureRolled(GameSession.Home.Trade, GameSession.Home, TradeSettings.Current);
        foreach (TraderDefinition trader in arrived)
        {
            GameMessages.Post(trader.OnlyAfterDark ? $"{trader.DisplayName} have come to the Trading Post. They deal after dark."
                : $"{trader.DisplayName} has come to the Trading Post.", MessageKind.Info);
        }
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => BuildPage(user), user, transform, menuRange);
        }
    }

    private MenuPage BuildPage(GameObject user)
    {
        TradeSettings settings = TradeSettings.Current;
        var page = new MenuPage { Title = "The Trading Post" };
        List<TraderDefinition> here = HavenVisit.IsVisiting ? new List<TraderDefinition>() : Caravans.HereNow(GameSession.Home.Trade, GameSession.Home, settings);
        page.Body = here.Count == 0 ? "Nobody is at the post now. Traders come with your homecomings." : "Who is at the post now:";
        foreach (TraderDefinition trader in here)
        {
            TraderDefinition chosen = trader;
            TradeState.Trader record = GameSession.Home.Trade.Of(trader.Id);
            bool deals = Caravans.DealsNow(trader, NightSettings.IsDark, out string why);
            string standing = $"{settings.TierName(TradeRules.Tier(record.Standing, settings))}, standing {record.Standing:0}";
            page.Add(new MenuAction($"Trade with {trader.DisplayName}", deals ? standing : why, deals,
                () => TradeWindow.Show(chosen, user, transform), closesMenu: true) { HostOnly = true });
        }

        // The caravan's horse trade: the player's horses standing near the post, and one of its own each time it comes.
        HorseTrader.AddTo(page, user, transform);
        return page;
    }
}
