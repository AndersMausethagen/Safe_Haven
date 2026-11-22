using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The strip along the top left of the Keeper's view (the way Frostpunk, Against the Storm and Gord keep the stores in sight): the
/// Haven's rank and renown, the day and the hour, and the stores that matter for building and work (KeeperViewSettings › Stores),
/// each its picture and how many the chests hold; the name on hover. A plain helper; KeeperHud owns it and refreshes it twice a second.
/// </summary>
public sealed class KeeperStores
{
    private const float Height = 56f;
    private const float Pad = 14f;
    private const float IconSize = 30f;

    private sealed class Chip
    {
        public ItemDefinition Item;
        public RectTransform Rect;
        public TMP_Text Count;
        public int Shown = -1;
    }

    private readonly RectTransform root;
    private readonly KeeperTip tip;
    private readonly TMP_Text rank;
    private readonly TMP_Text renown;
    private readonly RectTransform clock;
    private readonly Image clockIcon;
    private readonly TMP_Text clockLabel;
    private readonly List<Chip> chips = new List<Chip>();
    private readonly float headWidth;
    private string shownClock = "-";
    private string shownRank = "-";

    public KeeperStores(RectTransform canvas, KeeperTip tip)
    {
        UIStyle style = UIStyle.Current;
        this.tip = tip;
        root = HearthKit.Card(canvas, "Keeper Stores", 0f, 0f, 400f, Height, 0.5f, style.Panel);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(20f, -20f);

        // The Haven's standing.
        HearthKit.Icon(root, "House", Pad, (Height - 30f) * 0.5f, 30f);
        rank = CodeUI.Label("Rank", root, Pad + 38f, 6f, 150f, 26f, 20f, style.AccentBright);
        rank.overflowMode = TextOverflowModes.Overflow;
        if (style.TitleFont != null)
        {
            rank.font = style.TitleFont;
        }

        renown = CodeUI.Label("Renown", root, Pad + 38f, 31f, 150f, 18f, 13f, style.Muted);
        renown.overflowMode = TextOverflowModes.Overflow;
        headWidth = Pad + 38f + 150f;

        // The day and the hour.
        clock = CodeUI.At("Clock", root, headWidth, 0f, 140f, Height);
        clockIcon = HearthKit.Icon(clock, "Sun", 8f, (Height - 26f) * 0.5f, 26f);
        clockLabel = CodeUI.Label("Time", clock, 40f, 0f, 100f, Height, 16f, style.Text, TextAlignmentOptions.MidlineLeft);
        clockLabel.overflowMode = TextOverflowModes.Overflow;

        foreach (ItemDefinition item in KeeperViewSettings.Current.Stores)
        {
            if (item != null)
            {
                chips.Add(MakeChip(item));
            }
        }

        Refresh();
    }

    /// <summary>The strip itself (for the overlap audit).</summary>
    public RectTransform Root => root;

    private Chip MakeChip(ItemDefinition item)
    {
        UIStyle style = UIStyle.Current;
        var chip = new Chip { Item = item };
        chip.Rect = CodeUI.At(item.DisplayName, root, 0f, 0f, 80f, Height);
        // Something for the pointer to land on.
        chip.Rect.gameObject.AddComponent<Image>().color = Color.clear;
        Image picture = CodeUI.At("Picture", chip.Rect, 6f, (Height - IconSize) * 0.5f, IconSize, IconSize).gameObject.AddComponent<Image>();
        picture.sprite = item.Icon;
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        chip.Count = CodeUI.Label("Count", chip.Rect, IconSize + 10f, 0f, 60f, Height, 17f, style.AccentBright, TextAlignmentOptions.MidlineLeft);
        chip.Count.fontStyle = FontStyles.Bold;
        chip.Count.overflowMode = TextOverflowModes.Overflow;
        var hover = chip.Rect.gameObject.AddComponent<UIHover>();
        hover.Entered = () => tip.Show(item.DisplayName, $"{GameSession.Stash.Items.Count(item)} in the chests.", chip.Rect, below: true);
        hover.Exited = tip.Hide;
        return chip;
    }

    /// <summary>The counts and the hour; the chips laid out again when a count's width changes.</summary>
    public void Refresh()
    {
        HavenSettings settings = HavenSettings.Current;
        int points = HavenProgress.Current != null ? HavenProgress.Current.Renown : 0;
        int index = settings.Ranks.Count > 0 ? Mathf.Clamp(Mathf.Max(GameSession.Here.HavenRank, settings.RankFor(points)), 0, settings.Ranks.Count - 1) : 0;
        bool last = index >= settings.Ranks.Count - 1;
        string standing = settings.Ranks.Count == 0 ? "" : last ? $"{points} renown" : $"{points} / {settings.Ranks[index + 1].renown} renown";
        string rankName = settings.Ranks.Count == 0 ? "The Haven" : settings.Ranks[index].name;
        if (rankName + standing != shownRank)
        {
            shownRank = rankName + standing;
            rank.text = rankName;
            renown.text = standing;
        }

        float hour = Mathf.Repeat(AtmosphereConditions.Hour, 24f);
        bool night = NightSettings.Darkness > 0.5f;
        string time = $"Day {GameSession.Here.DaysPassed + 1} · {Mathf.FloorToInt(hour):00}:{Mathf.FloorToInt(hour % 1f * 60f):00}";
        if (time != shownClock)
        {
            shownClock = time;
            clockLabel.text = time;
            clockIcon.sprite = UIStyle.Current.Icon(night ? "Lantern" : "Sun");
        }

        bool moved = false;
        foreach (Chip chip in chips)
        {
            int count = GameSession.Stash.Items.Count(chip.Item);
            if (count != chip.Shown)
            {
                moved |= chip.Shown < 0 || count.ToString().Length != chip.Shown.ToString().Length;
                chip.Shown = count;
                chip.Count.text = count.ToString();
            }
        }

        if (moved)
        {
            Lay();
        }
    }

    // Side by side after the clock, each as wide as its count; the strip as long as they are.
    private void Lay()
    {
        float x = headWidth + 140f + 6f;
        foreach (Chip chip in chips)
        {
            float width = IconSize + 10f + Mathf.Ceil(chip.Count.GetPreferredValues(chip.Count.text).x) + 14f;
            chip.Rect.anchoredPosition = new Vector2(x, 0f);
            chip.Rect.sizeDelta = new Vector2(width, Height);
            chip.Count.rectTransform.sizeDelta = new Vector2(width - IconSize - 10f, Height);
            x += width;
        }

        root.sizeDelta = new Vector2(x + Pad - 6f, Height);
    }
}
