using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Haven's people down the right of the Keeper's view (Gord's column of its folk): a card for each with their portrait, name, rank
/// and work, and an icon for what they are about now (building, felling, quarrying, fetching, on guard, asleep...), a heart while a
/// wish for their home waits. Clicking a card flies the view to them and follows; hovering says what they are doing in words. The cards
/// shrink to fit when there are many. A plain helper; KeeperHud owns it and refreshes it twice a second.
/// </summary>
public sealed class KeeperPeople
{
    private const float Width = 250f;
    private const float Gap = 6f;
    private const float Top = 24f;
    private const float Bottom = 150f;
    private const float Tallest = 62f;
    private const float Shortest = 40f;

    private sealed class Card
    {
        public RectTransform Rect;
        public Image Rim;
        public Image Portrait;
        public TMP_Text Initial;
        public TMP_Text Name;
        public TMP_Text Work;
        public Image Doing;
        public Image Wish;
        public ResidentRecord Record;
        public string Shown = "-";
    }

    private readonly RectTransform canvas;
    private readonly RectTransform root;
    private readonly KeeperTip tip;
    private readonly List<Card> cards = new List<Card>();
    private Card pointed;
    private float cardHeight = Tallest;

    public KeeperPeople(RectTransform canvas, KeeperTip tip)
    {
        this.canvas = canvas;
        this.tip = tip;
        root = CodeUI.Box("Keeper People", canvas, new Vector2(1f, 1f), new Vector2(Width, 10f), new Vector2(-20f, -Top));
        root.pivot = new Vector2(1f, 1f);
    }

    /// <summary>The column itself (for the overlap audit).</summary>
    public RectTransform Root => root;

    /// <summary>One card a resident of the Haven being looked at, in the order they live there.</summary>
    public void Refresh()
    {
        IReadOnlyList<ResidentRecord> residents = GameSession.Here.Residents;
        float room = canvas.rect.height - Top - Bottom;
        float height = Mathf.Clamp((room + Gap) / Mathf.Max(1, residents.Count) - Gap, Shortest, Tallest);
        if (!Mathf.Approximately(height, cardHeight))
        {
            cardHeight = height;
            foreach (Card card in cards)
            {
                Object.Destroy(card.Rect.gameObject);
            }

            cards.Clear();
        }

        while (cards.Count < residents.Count)
        {
            cards.Add(Make(cards.Count));
        }

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            bool used = i < residents.Count;
            if (card.Rect.gameObject.activeSelf != used)
            {
                card.Rect.gameObject.SetActive(used);
            }

            if (used)
            {
                Fill(card, residents[i]);
            }
        }

        root.sizeDelta = new Vector2(Width, residents.Count * (cardHeight + Gap));
        if (pointed != null && pointed.Rect.gameObject.activeSelf)
        {
            ShowTip(pointed);
        }
    }

    private Card Make(int index)
    {
        UIStyle style = UIStyle.Current;
        bool tall = cardHeight >= 54f;
        float portrait = cardHeight - 10f;
        var card = new Card();
        card.Rect = HearthKit.Card(root, "Person", 0f, index * (cardHeight + Gap), Width, cardHeight, 0.35f, style.Panel);
        card.Rim = card.Rect.Find("Rim").GetComponent<Image>();
        // The card catches the pointer: clicked, the view follows them.
        Image hit = card.Rect.GetComponent<Image>();
        hit.raycastTarget = true;
        var button = card.Rect.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        HearthKit.Tint(button);
        UIHighlight.On(card.Rect.gameObject);
        button.onClick.AddListener(() => FollowThem(card));
        var hover = card.Rect.gameObject.AddComponent<UIHover>();
        hover.Entered = () =>
        {
            pointed = card;
            ShowTip(card);
        };
        hover.Exited = () =>
        {
            if (pointed == card)
            {
                pointed = null;
                tip.Hide();
            }
        };

        RectTransform frame = HearthKit.Card(card.Rect, "Frame", 5f, 5f, portrait, portrait, 0.55f, style.Inset);
        card.Portrait = CodeUI.At("Portrait", frame, 2f, 2f, portrait - 4f, portrait - 4f).gameObject.AddComponent<Image>();
        card.Portrait.preserveAspect = true;
        card.Portrait.raycastTarget = false;
        frame.Find("Rim").SetAsLastSibling();
        card.Initial = CodeUI.Label("Initial", frame, 0f, 0f, portrait, portrait, portrait * 0.5f, style.Accent, TextAlignmentOptions.Center);
        card.Initial.overflowMode = TextOverflowModes.Overflow;
        if (style.TitleFont != null)
        {
            card.Initial.font = style.TitleFont;
        }

        float textX = portrait + 14f;
        float textWidth = Width - textX - 40f;
        card.Name = CodeUI.Label("Name", card.Rect, textX, tall ? 7f : (cardHeight - 24f) * 0.5f, textWidth, 24f, tall ? 18f : 16f, style.Text);
        card.Name.overflowMode = TextOverflowModes.Overflow;
        card.Work = CodeUI.Label("Work", card.Rect, textX, 32f, textWidth, 20f, 14f, style.Muted);
        card.Work.overflowMode = TextOverflowModes.Overflow;
        card.Work.gameObject.SetActive(tall);
        float iconSize = Mathf.Min(28f, cardHeight - 16f);
        card.Doing = HearthKit.Icon(card.Rect, "Doing", Width - iconSize - 10f, (cardHeight - iconSize) * 0.5f, iconSize);
        card.Wish = HearthKit.Icon(frame, "Heart", portrait - 18f, -4f, 18f);
        card.Wish.enabled = false;
        return card;
    }

    private void Fill(Card card, ResidentRecord record)
    {
        UIStyle style = UIStyle.Current;
        card.Record = record;
        ResidentDefinition who = record.Resident;
        ResidentLife life = ResidentLife.Of(who);
        string doing = DoingIcon(life, out string doingWords);
        bool wish = HomeWishes.WishOf(record) != null;
        bool followed = KeeperView.Current != null && life != null && KeeperView.Current.Following == life.transform;
        string state = $"{who.name}|{(record.Job != null ? record.Job.name : "")}|{record.WorkDone}|{doing}|{wish}|{followed}";
        if (state == card.Shown)
        {
            return;
        }

        card.Shown = state;
        card.Portrait.sprite = who.Portrait;
        card.Portrait.enabled = who.Portrait != null;
        card.Initial.text = who.Portrait != null ? "" : who.DisplayName.Substring(0, 1);
        card.Name.text = who.DisplayName;
        string rank = record.Job != null ? SettlementSettings.Current.RankFor(record.WorkDone).name : "";
        card.Work.text = record.Job != null ? (string.IsNullOrEmpty(rank) ? record.Job.DisplayName : $"{rank} {record.Job.DisplayName}") : "No work yet";
        card.Doing.sprite = style.Icon(doing);
        card.Doing.enabled = card.Doing.sprite != null;
        card.Doing.color = doing == "Sword" ? Color.Lerp(style.Danger, style.Accent, 0.4f) : doing == "Bed" ? style.Muted : HearthKit.IconColour(doing);
        card.Wish.enabled = wish;
        card.Rim.color = HearthKit.Faded(followed ? style.AccentBright : style.Accent, followed ? 0.95f : 0.35f);
    }

    // What they are about, as an icon and in words.
    private static string DoingIcon(ResidentLife life, out string words)
    {
        words = "away";
        if (life == null)
        {
            return "Path";
        }

        if (life.Guard != null)
        {
            words = "on guard";
            return "Sword";
        }

        switch (life.Activity)
        {
            case ResidentActivity.Work:
                if (life.IsBuilding) { words = "building"; return "Hammer"; }
                if (life.IsQuarrying) { words = "quarrying stone"; return "Pickaxe"; }
                if (life.IsFelling) { words = "felling the ring"; return "Axe"; }
                if (life.IsFetchingWater) { words = "fetching water"; return "Drop"; }
                if (life.IsHauling || life.IsTakingWorkBack) { words = "carrying for work"; return "Bags"; }
                words = "at work";
                return "Gear";
            case ResidentActivity.Meal:
                words = "eating";
                return "Pot";
            case ResidentActivity.Sleep:
                words = life.IsIndoors ? "asleep at home" : "going to bed";
                return "Bed";
            case ResidentActivity.Shelter:
                words = "sheltering at the portal";
                return "House";
            case ResidentActivity.Outing:
                words = "out for a walk";
                return "Path";
            default:
                words = life.IsInScene ? "with a friend" : "by the fire";
                return life.IsInScene ? "Spark" : "Bench";
        }
    }

    private void ShowTip(Card card)
    {
        if (card.Record == null || card.Record.Resident == null)
        {
            return;
        }

        ResidentLife life = ResidentLife.Of(card.Record.Resident);
        DoingIcon(life, out string doing);
        string wish = HomeWishes.WishOf(card.Record);
        string words = char.ToUpperInvariant(doing[0]) + doing.Substring(1) + "." + (wish != null ? $" Wishes for a home with: {wish}." : "")
            + (life != null ? " Click to follow; click them in the Haven for their page." : "");
        tip.Show(card.Record.Resident.DisplayName, words, card.Rect, beside: -1);
    }

    private static void FollowThem(Card card)
    {
        ResidentLife life = card.Record != null ? ResidentLife.Of(card.Record.Resident) : null;
        if (life != null && KeeperView.Current != null)
        {
            KeeperView.Current.FollowPerson(life.transform);
        }
    }
}
