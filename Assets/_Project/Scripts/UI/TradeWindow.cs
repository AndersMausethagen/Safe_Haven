using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The barter at the Trading Post (Docs/TradeResearch_2026-11-28.md §3), Hearthline 3: the trader's goods on the left and the Haven's
/// (bag and stash) on the right as picture cards with their names, a price under each and the words on hover; click to add one to
/// the deal (Shift: five), right-click to take one back. Between them the deal as chips and the scales: what is given, what is taken,
/// the coin that makes up the rest or comes back, and a gift. A haggle once a visit, its odds shown first, the trader's ask with its
/// reward, and Accept. Built in code the first time it opens (as the loot window); several traders here show as tabs.
/// </summary>
public sealed class TradeWindow : MonoBehaviour
{
    private const float Width = 1180f;
    private const float Height = 770f;
    private const float Margin = 56f;
    // The header and the foot's left card stand clear of the scroll corners (they cover ~70 px of each corner).
    private const float HeadX = 84f;
    private const float ColumnWidth = 520f;
    private const float RightX = Margin + ColumnWidth + 28f;
    private const float GridTop = 226f;
    private const float GridHeight = 300f;
    private const float CardWidth = 82f;
    private const float PictureHeight = 70f;
    private const float CardGap = 8f;
    private const float PriceHeight = 18f;
    private const float ChipSize = 44f;
    private const float Reach = 9f;

    private UIWindow window;
    private RectTransform panel;
    private TMP_Text nameLabel;
    private TMP_Text greeting;
    private TMP_Text standingLabel;
    private RectTransform standingFill;
    private Image standingFillImage;
    private TMP_Text packLabel;
    private TMP_Text coinLabel;
    private RectTransform theirGrid;
    private RectTransform yourGrid;
    private TMP_Text theirEmpty;
    private TMP_Text yourEmpty;
    private RectTransform giveChips;
    private RectTransform takeChips;
    private TMP_Text balanceLabel;
    private TMP_Text problemLabel;
    private Button accept;
    private Button haggle;
    private TMP_Text haggleLabel;
    private RectTransform askCard;
    private Image askIcon;
    private TMP_Text askLabel;
    private Button askButton;
    private RectTransform tabs;
    private ItemTooltipView tooltip;

    private TraderDefinition trader;
    private TradeDeal deal;
    private GameObject user;
    private Transform post;
    private List<TradeDeal.Good> goods = new List<TradeDeal.Good>();
    private bool dirty;
    private bool shownDark;
    // The bag and the stash, watched while open: residents haul and carts unload, and the goods shown follow.
    private ItemContainer watchedBag;
    private ItemContainer watchedStash;

    public static TradeWindow Current { get; private set; }

    public bool IsOpen => window != null && window.IsOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Opens the barter with <paramref name="with"/> for <paramref name="who"/> at <paramref name="at"/> (the post).</summary>
    public static void Show(TraderDefinition with, GameObject who, Transform at)
    {
        if (with == null || who == null || HavenVisit.LeftToHost())
        {
            return;
        }

        (Current != null ? Current : Create()).Open(with, who, at);
    }

    private static TradeWindow Create()
    {
        var root = new GameObject("Trade Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<TradeWindow>();
        Current = view;
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private static TradeSettings Settings => TradeSettings.Current;
    private static ContentDatabase Content => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
    private static TradeState State => GameSession.Home.Trade;

    private void Open(TraderDefinition with, GameObject who, Transform at)
    {
        user = who;
        post = at;
        Choose(with);
        ItemInventory inventory = who.GetComponent<ItemInventory>();
        Watch(inventory != null ? inventory.Container : null, GameSession.Stash != null ? GameSession.Stash.Items : null);
        window.Open();
        HearthKit.FitToScreen(panel, (RectTransform)transform, Width, Height);
        if (Application.isPlaying)
        {
            UIMotion.Pop(panel, 0.97f, 0.2f);
        }
    }

    private void Choose(TraderDefinition with)
    {
        shownDark = NightSettings.IsDark;
        trader = with;
        deal = new TradeDeal(with, State, Settings);
        nameLabel.text = with.DisplayName;
        greeting.text = with.Greeting;
        BuildTabs();
        dirty = true;
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // Walked away, gone home to a friend's, or the trader left (a new homecoming): the window closes.
        if (user == null || post == null || (user.transform.position - post.position).sqrMagnitude > Reach * Reach
            || !Caravans.IsHere(trader, State, GameSession.Home) || !Caravans.DealsNow(trader, NightSettings.IsDark, out _))
        {
            window.Close();
            return;
        }

        // Dusk or dawn: the Hidden Folk's tab opens or closes.
        if (NightSettings.IsDark != shownDark)
        {
            shownDark = NightSettings.IsDark;
            BuildTabs();
        }

        if (dirty)
        {
            dirty = false;
            Redraw();
        }

        tooltip.Follow(GameInput.PointerPosition);
    }

    // ----- Drawing -----

    private void Redraw()
    {
        UIStyle style = UIStyle.Current;
        TradeSettings settings = Settings;
        TradeState.Trader record = deal.Record;
        int tier = TradeRules.Tier(record.Standing, settings);
        float next = tier + 1 < settings.TierCount ? settings.TierFrom(tier + 1) : 100f;
        float from = settings.TierFrom(tier);
        standingLabel.text = HearthKit.ToneNumbers($"{settings.TierName(tier)}  ·  standing {record.Standing:0}");
        KitBar.Set(standingFill, standingFillImage, Mathf.Clamp01((record.Standing - from) / Mathf.Max(1f, next - from)), style.Accent);
        packLabel.text = HearthKit.ToneNumbers($"{HearthKit.InlineIcon("Bags")} Their pack takes {Coins(TradeRules.PackLeft(trader, record, settings))} more");
        MaterialPool pool = MaterialPool.For(user);
        int coinHeld = settings.Coin != null ? pool.Have(settings.Coin) : 0;
        coinLabel.text = trader.DealsInCoin ? HearthKit.ToneNumbers($"{HearthKit.InlineIcon("Coin")} Your coin {coinHeld}") : "They take no coin";

        ItemInventory bag = user.GetComponent<ItemInventory>();
        // Goods are read again on every redraw: what is offered keeps its place by kind or slot, so the deal follows the same things.
        List<TradeDeal.Good> fresh = TradeDeal.GoodsOf(bag != null ? bag.Container : null, GameSession.Stash.Items, trader, record, settings);
        KeepOffers(fresh);
        goods = fresh;
        DrawTheirs(style);
        DrawYours(style);
        DrawDeal(style);

        TradeDeal.Balance balance = deal.Weigh(Content, coinHeld);
        string line = $"You give {Coins(balance.Give)}   ·   You get {Coins(balance.Take)}";
        if (balance.CoinFromYou > 0)
        {
            line += $"   ·   {HearthKit.InlineIcon("Coin")} you add {balance.CoinFromYou} coin";
        }

        if (balance.CoinToYou > 0)
        {
            line += $"   ·   {HearthKit.InlineIcon("Coin")} they add {balance.CoinToYou} coin";
        }

        if (balance.Gift > 0.05f)
        {
            line += $"   ·   the rest a gift";
        }

        if (balance.Standing >= 0.05f)
        {
            line += $"   ·   {HearthKit.InlineIcon("Star")} +{balance.Standing:0.#} standing";
        }

        balanceLabel.text = deal.IsEmpty ? "Click their goods to take them, yours to offer them." : HearthKit.ToneNumbers(line);
        problemLabel.text = deal.IsEmpty ? "" : balance.Problem ?? "";
        accept.interactable = balance.CanMake;

        haggleLabel.text = record.Haggle == HaggleResult.NotTried ? $"{trader.HaggleWord} {TradeRules.HaggleChance(record, settings):0%}"
            : record.Haggle == HaggleResult.Won ? "Talked down" : "No more haggling";
        haggle.interactable = record.Haggle == HaggleResult.NotTried;
        DrawAsk(style, bag);
    }

    // Keeps what was offered when the goods are read again: the same kind, or the same piece in the same slot.
    private void KeepOffers(List<TradeDeal.Good> fresh)
    {
        var kept = new List<(TradeDeal.Good good, int count)>();
        foreach (KeyValuePair<TradeDeal.Good, int> offered in deal.Given)
        {
            TradeDeal.Good same = fresh.Find(good => good.IsPiece == offered.Key.IsPiece
                && (good.IsPiece ? good.Container == offered.Key.Container && good.Slot == offered.Key.Slot && good.Sample.SameKind(offered.Key.Sample)
                    : good.Sample.Item == offered.Key.Sample.Item));
            if (same != null)
            {
                kept.Add((same, offered.Value));
            }
        }

        var taken = new List<KeyValuePair<string, int>>(deal.Taken);
        deal.Clear();
        foreach ((TradeDeal.Good good, int count) in kept)
        {
            deal.Give(good, count);
        }

        foreach (KeyValuePair<string, int> line in taken)
        {
            deal.Take(line.Key, line.Value);
        }
    }

    private void DrawTheirs(UIStyle style)
    {
        Clear(theirGrid);
        ContentDatabase content = Content;
        var cards = new List<KitPictureCard>();
        foreach (TradeState.StockLine line in deal.Record.Stock)
        {
            ItemDefinition item = content != null ? content.FindItem(line.Item) : null;
            if (item == null)
            {
                continue;
            }

            string id = line.Item;
            int left = line.Left - deal.TakenOf(id);
            float price = TradeRules.BuyPrice(item, trader, deal.Record, Settings);
            var stack = new ItemStack(item, 1);
            KitPictureCard card = Card(theirGrid, item.DisplayName, item.Icon, left > 0 ? $"×{left}" : "Taken", Coins(price), left <= 0,
                () => tooltip.Show(stack, $"{Coins(price)} coin each. {left} in their pack.\n{AddHint}"),
                () => deal.Take(id, Step()), () => deal.Take(id, -Step()));
            cards.Add(card);
        }

        Lay(theirGrid, cards);
        theirEmpty.gameObject.SetActive(cards.Count == 0);
    }

    private void DrawYours(UIStyle style)
    {
        Clear(yourGrid);
        var cards = new List<KitPictureCard>();
        foreach (TradeDeal.Good good in goods)
        {
            TradeDeal.Good chosen = good;
            int left = good.Count - deal.GivenOf(good);
            bool wanted = deal.Record.Wanted.Contains(good.Sample.Item.Id);
            float flood = TradeRules.Flooding(deal.Record, good.Sample.Item.Id, Settings);
            string note = wanted ? "They want this most this visit." : flood < 0.8f ? "They have been sold plenty of this lately." : "";
            string worked = good.Sample.IsWorked ? $"\n<color={style.WarningHex}>Its empowering and enchantment go with it.</color>" : "";
            float next = deal.NextPrice(good);
            KitPictureCard card = Card(yourGrid, good.Sample.DisplayName, good.Sample.Item.Icon, left > 0 ? $"×{left}" : "Offered",
                Coins(next), left <= 0,
                () => tooltip.Show(good.Sample, $"They pay {Coins(next)} coin for the next one, a little less for each after it. {note}{worked}\n{AddHint}"),
                () => deal.Give(chosen, Step()), () => deal.Give(chosen, -Step()));
            if (wanted)
            {
                card.SetLook(left <= 0, style.Good);
            }

            cards.Add(card);
        }

        Lay(yourGrid, cards);
        yourEmpty.gameObject.SetActive(cards.Count == 0);
        yourEmpty.text = string.IsNullOrEmpty(trader.RefusalLine) ? "Nothing at home they would take." : $"Nothing at home they would take. {trader.RefusalLine}";
    }

    private void DrawDeal(UIStyle style)
    {
        Clear(giveChips);
        Clear(takeChips);
        float x = 0f;
        foreach (KeyValuePair<TradeDeal.Good, int> line in deal.Given)
        {
            TradeDeal.Good good = line.Key;
            Chip(giveChips, ref x, good.Sample.Item.Icon, line.Value, () => deal.Give(good, -Step()));
        }

        x = 0f;
        ContentDatabase content = Content;
        foreach (KeyValuePair<string, int> line in deal.Taken)
        {
            ItemDefinition item = content != null ? content.FindItem(line.Key) : null;
            string id = line.Key;
            Chip(takeChips, ref x, item != null ? item.Icon : null, line.Value, () => deal.Take(id, -Step()));
        }
    }

    private void DrawAsk(UIStyle style, ItemInventory bag)
    {
        TraderDefinition.Ask ask = TradeFavours.AskOf(trader, deal.Record);
        askCard.gameObject.SetActive(ask != null);
        if (ask == null)
        {
            return;
        }

        int home = TradeFavours.AtHome(ask, bag != null ? bag.Container : null, GameSession.Stash.Items);
        askIcon.sprite = ask.wanted.Icon;
        askIcon.enabled = ask.wanted.Icon != null;
        string reward = ask.reward != null && ask.rewardCount > 0 ? $"{ask.rewardCount} {ask.reward.DisplayName}, " : "";
        askLabel.text = HearthKit.ToneNumbers($"They ask {ask.count} {ask.wanted.DisplayName}  ·  {home}/{ask.count} home\nFor it: {reward}+{ask.standing} standing");
        askButton.interactable = home >= ask.count;
    }

    private void Watch(ItemContainer bag, ItemContainer stash)
    {
        if (watchedBag != null)
        {
            watchedBag.SlotChanged -= OnStoresChanged;
        }

        if (watchedStash != null)
        {
            watchedStash.SlotChanged -= OnStoresChanged;
        }

        watchedBag = bag;
        watchedStash = stash;
        if (bag != null)
        {
            bag.SlotChanged += OnStoresChanged;
        }

        if (stash != null)
        {
            stash.SlotChanged += OnStoresChanged;
        }
    }

    private void OnStoresChanged(int slot) => dirty = true;

    // ----- Parts -----

    private const string AddHint = "Click: one more (Shift: five). Right-click: one less.";

    private static int Step()
    {
        UnityEngine.InputSystem.Keyboard keys = UnityEngine.InputSystem.Keyboard.current;
        return keys != null && keys.shiftKey.isPressed ? 5 : 1;
    }

    /// <summary>Worth in coin, as the window shows it: one decimal, two under one coin.</summary>
    private static string Coins(float worth)
    {
        float coins = worth / Settings.CoinWorth;
        return coins >= 10f ? coins.ToString("0") : coins >= 1f ? coins.ToString("0.#") : coins.ToString("0.##");
    }

    private KitPictureCard Card(RectTransform grid, string name, Sprite icon, string badge, string price, bool dim,
        System.Action hovered, System.Action add, System.Action remove)
    {
        UIStyle style = UIStyle.Current;
        var card = new KitPictureCard(grid, name, CardWidth, PictureHeight, 2, 13f, hovered, () => tooltip.Hide());
        card.ShowPicture(icon, "Bags");
        card.SetName(name);
        card.SetBadge(badge);
        card.SetLook(dim);
        TMP_Text priceLabel = Label("Price", card.Root, 0f, card.Height, CardWidth, PriceHeight, 13f, style.AccentBright, TextAlignmentOptions.Center);
        priceLabel.richText = true;
        priceLabel.text = $"{HearthKit.InlineIcon("Coin")} {price}";
        var button = card.Root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            add();
            // The card under the pointer is built anew: its old words would stay up.
            tooltip.Hide();
            dirty = true;
        });
        card.Root.gameObject.AddComponent<TradeCardClicks>().Right = () =>
        {
            remove();
            dirty = true;
        };
        return card;
    }

    private void Chip(RectTransform row, ref float x, Sprite icon, int count, System.Action remove)
    {
        UIStyle style = UIStyle.Current;
        if (x + ChipSize > ColumnWidth)
        {
            return; // more than fit: the scales still count them
        }

        RectTransform chip = HearthKit.Card(row, "Chip", x, 0f, ChipSize, ChipSize, 0.5f);
        if (icon != null)
        {
            Image picture = At("Icon", chip, 4f, 4f, ChipSize - 8f, ChipSize - 8f).gameObject.AddComponent<Image>();
            picture.sprite = icon;
            picture.preserveAspect = true;
            picture.raycastTarget = false;
        }

        TMP_Text amount = Label("Count", chip, 0f, ChipSize - 18f, ChipSize - 3f, 16f, 13f, style.AccentBright, TextAlignmentOptions.Right);
        amount.fontStyle = FontStyles.Bold;
        amount.text = $"{count}";
        // The card's own surface takes the pointer (HearthKit.Card leaves it off).
        Image hit = chip.GetComponent<Image>();
        hit.raycastTarget = true;
        var button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.onClick.AddListener(() =>
        {
            remove();
            dirty = true;
        });
        UIHighlight.On(chip.gameObject);
        x += ChipSize + 6f;
    }

    private static void Lay(RectTransform grid, List<KitPictureCard> cards)
    {
        if (cards.Count == 0)
        {
            grid.sizeDelta = new Vector2(grid.sizeDelta.x, 0f);
            return;
        }

        float step = cards[0].Height + PriceHeight + CardGap;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((ColumnWidth - 14f + CardGap) / (CardWidth + CardGap)));
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].Place(i % perRow * (CardWidth + CardGap), i / perRow * step);
        }

        grid.sizeDelta = new Vector2(grid.sizeDelta.x, Mathf.CeilToInt(cards.Count / (float)perRow) * step);
    }

    private static void Clear(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    // ----- Actions -----

    private void Accept()
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        ItemInventory bag = user.GetComponent<ItemInventory>();
        TradeDeal.Balance balance = deal.Weigh(Content, Settings.Coin != null ? MaterialPool.For(user).Have(Settings.Coin) : 0);
        string problem = deal.Make(bag, GameSession.Stash.Items, MaterialPool.For(user), Content);
        if (problem != null)
        {
            GameMessages.Post(problem, MessageKind.Warning);
        }
        else
        {
            GameMessages.Post(balance.CoinToYou > 0 ? $"A deal with {trader.DisplayName}: {balance.CoinToYou} coin back. What you took is in your bag or the stash."
                : $"A deal with {trader.DisplayName}. What you took is in your bag or the stash.", MessageKind.Gain);
            PlaytestLogger.Note(PlaytestLog.Trade, trader.Id, Mathf.RoundToInt(balance.Give), $"take {Mathf.RoundToInt(balance.Take)} coin -{balance.CoinFromYou}+{balance.CoinToYou}");
            AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.QuestCompleted : null, user.transform.position);
        }

        dirty = true;
    }

    private void Haggle()
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        HaggleResult result = TradeFavours.Haggle(State, deal.Record, Settings, Random.value);
        GameMessages.Post(result == HaggleResult.Won ? $"{trader.DisplayName} gives way: better prices until they come again."
            : $"{trader.DisplayName} takes offence. No more haggling this visit.", result == HaggleResult.Won ? MessageKind.Gain : MessageKind.Warning);
        PlaytestLogger.Note(PlaytestLog.Trade, trader.Id, 0, $"haggle {result}");
        tooltip.Hide();
        dirty = true;
    }

    private void AnswerAsk()
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        TraderDefinition.Ask ask = TradeFavours.AskOf(trader, deal.Record);
        string problem = TradeFavours.Answer(trader, State, user.GetComponent<ItemInventory>(), GameSession.Stash.Items, Settings);
        if (problem != null)
        {
            GameMessages.Post(problem, MessageKind.Warning);
        }
        else if (ask != null)
        {
            string thanks = string.IsNullOrEmpty(ask.thanks) ? "Just what was wanted." : ask.thanks;
            GameMessages.Post($"{trader.DisplayName} takes the {ask.wanted.DisplayName}. \"{thanks}\"", MessageKind.Gain);
            PlaytestLogger.Note(PlaytestLog.Trade, trader.Id, ask.count, $"ask {ask.id}");
            AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.QuestCompleted : null, user.transform.position);
        }

        dirty = true;
    }

    // ----- Building -----

    private void BuildTabs()
    {
        Clear(tabs);
        List<TraderDefinition> here = Caravans.HereNow(State, GameSession.Home, Settings);
        if (here.Count < 2)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        float x = 0f;
        foreach (TraderDefinition other in here)
        {
            TraderDefinition chosen = other;
            Button tab = AddTab(other.DisplayName, tabs, x, 0f, 160f, 40f, style, out TMP_Text label, out Image shown);
            label.text = other.DisplayName;
            label.fontSize = 16f;
            shown.enabled = other == trader;
            bool deals = Caravans.DealsNow(other, NightSettings.IsDark, out _);
            tab.interactable = deals && other != trader;
            tab.onClick.AddListener(() => Choose(chosen));
            x += 166f;
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        panel = HearthKit.Window(root, Width, Height, () => window.Close(), out RectTransform content);

        TMP_Text kicker = Label("Kicker", panel, HeadX, 30f, 300f, 16f, 12f, style.Accent);
        kicker.text = "TRADE";
        kicker.characterSpacing = 10f;
        nameLabel = HearthKit.DisplayName(panel, HeadX, 38f, 500f, 40f);
        greeting = Label("Greeting", panel, HeadX, 110f, 1000f, 22f, 16f, style.Muted);
        tabs = At("Tabs", panel, 600f, 34f, 500f, 44f);

        HearthKit.Icon(panel, "Star", Margin, 144f, 22f);
        standingLabel = Label("Standing", panel, Margin + 30f, 142f, 260f, 26f, 16f, style.Text);
        standingLabel.richText = true;
        standingFill = KitBar.Make(panel, Margin + 290f, 150f, 200f, 10f, out standingFillImage);
        packLabel = Label("Pack", panel, RightX, 142f, 300f, 26f, 15f, style.Muted);
        packLabel.richText = true;
        coinLabel = Label("Coin", panel, RightX + 330f, 142f, 210f, 26f, 15f, style.Text, TextAlignmentOptions.Right);
        coinLabel.richText = true;

        HearthKit.Section(panel, "Their goods", "Bags", Margin, 186f, 400f);
        HearthKit.Section(panel, "Your goods", "House", RightX, 186f, 400f);
        theirGrid = Scroll(panel, "Their Goods", Margin, out theirEmpty, "Their pack is empty.");
        yourGrid = Scroll(panel, "Your Goods", RightX, out yourEmpty, "Nothing at home they would take.");

        HearthKit.Rule(panel, Margin, GridTop + GridHeight + 12f, Width - Margin * 2f);
        float dealTop = GridTop + GridHeight + 22f;
        Label("Give", panel, Margin, dealTop, 200f, 20f, 14f, style.Muted).text = "YOU GIVE";
        Label("Take", panel, RightX, dealTop, 200f, 20f, 14f, style.Muted).text = "YOU GET";
        giveChips = At("Give Chips", panel, Margin, dealTop + 22f, ColumnWidth, ChipSize);
        takeChips = At("Take Chips", panel, RightX, dealTop + 22f, ColumnWidth, ChipSize);

        float scalesTop = dealTop + 22f + ChipSize + 12f;
        balanceLabel = Label("Scales", panel, Margin, scalesTop, Width - Margin * 2f, 28f, 18f, style.Text, TextAlignmentOptions.Center);
        balanceLabel.richText = true;
        problemLabel = Label("Problem", panel, Margin, scalesTop + 28f, Width - Margin * 2f, 22f, 15f, style.Warning, TextAlignmentOptions.Center);

        float footTop = Height - 92f;
        askCard = HearthKit.Card(panel, "Ask", HeadX, footTop, 540f, 64f, 0.5f);
        askIcon = At("Icon", askCard, 8f, 8f, 48f, 48f).gameObject.AddComponent<Image>();
        askIcon.preserveAspect = true;
        askIcon.raycastTarget = false;
        askLabel = Paragraph("Words", askCard, 66f, 9f, 320f, 48f, 15f, style.Text);
        askLabel.richText = true;
        TMP_Text answer = AddButton("Hand Over", askCard, 392f, 9f, 140f, UIRole.Kind.Button, AnswerAsk, out askButton);
        answer.text = "Hand over";
        answer.fontSize = 17f;

        haggleLabel = AddButton("Haggle", panel, 640f, footTop + 9f, 170f, UIRole.Kind.Button, Haggle, out haggle);
        haggleLabel.fontSize = 17f;
        var haggleHover = haggle.gameObject.AddComponent<UIHover>();
        haggleHover.Entered = () => tooltip.Show(trader.HaggleWord, style.Accent, TradeFavours.HaggleOdds(deal.Record, Settings));
        haggleHover.Exited = () => tooltip.Hide();
        TMP_Text clear = AddButton("Clear", panel, 820f, footTop + 9f, 110f, UIRole.Kind.Button, () =>
        {
            deal.Clear();
            dirty = true;
        }, out _);
        clear.text = "Clear";
        clear.fontSize = 17f;
        TMP_Text acceptLabel = AddButton("Accept", panel, Width - HeadX - 160f, footTop + 9f, 160f, UIRole.Kind.PrimaryButton, Accept, out accept);
        acceptLabel.text = "Accept";

        BuildTooltip(content, style);
        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Trade, content.gameObject);
        window.OpenChanged += open =>
        {
            if (!open)
            {
                tooltip.Hide();
                deal?.Clear();
                Watch(null, null);
            }
        };
    }

    // A scrolling area under a section's heading; returns the content the cards go in.
    private RectTransform Scroll(RectTransform parent, string name, float x, out TMP_Text empty, string emptyText)
    {
        UIStyle style = UIStyle.Current;
        RectTransform frame = HearthKit.Card(parent, name, x, GridTop, ColumnWidth, GridHeight, 0.25f);
        RectTransform viewport = At("Viewport", frame, 8f, 8f, ColumnWidth - 16f, GridHeight - 16f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        RectTransform cards = At("Cards", viewport, 0f, 0f, ColumnWidth - 16f, 0f);
        var scroll = frame.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = cards;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        empty = Paragraph("Empty", frame, 20f, 20f, ColumnWidth - 40f, 60f, 16f, style.Muted);
        empty.text = emptyText;
        empty.gameObject.SetActive(false);
        return cards;
    }

    // The game's item tooltip, over the content (as the loot window's).
    private void BuildTooltip(RectTransform content, UIStyle style)
    {
        var holder = new GameObject("Tooltip Holder", typeof(RectTransform));
        holder.SetActive(false);
        holder.transform.SetParent(content, false);
        RectTransform tip = Box("Tooltip", content, new Vector2(0.5f, 0.5f), new Vector2(380f, 100f), Vector2.zero);
        tip.pivot = new Vector2(0f, 1f);
        tip.gameObject.AddComponent<Image>().raycastTarget = false;
        tip.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        var layout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 12);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text title = Paragraph("Title", tip, 0f, 0f, 352f, 28f, 19f, style.Text);
        TMP_Text body = Paragraph("Body", tip, 0f, 0f, 352f, 28f, 16f, style.Text);
        tooltip = holder.AddComponent<ItemTooltipView>();
        tooltip.Configure(tip, title, body);
        holder.SetActive(true);
    }
}
