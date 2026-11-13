using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The UI kit's parts that keep state (Hearthline 3, see HearthKit): each is built once into a window made in code and
// rebound when the window refreshes. All colours come from the UI Style.

/// <summary>A number with its icon: its name, its value and, for some, a bar (under it, or beside it when inline).</summary>
public sealed class KitStatRow
{
    private readonly TMP_Text value;
    private readonly RectTransform fill;
    private readonly Image fillImage;
    private readonly bool hasBar;

    public KitStatRow(Transform parent, float x, float y, float width, string name, string icon, bool bar, float size = 18f, bool inline = false,
        float labelWidth = 160f, float valueWidth = 110f)
    {
        UIStyle style = UIStyle.Current;
        hasBar = bar;
        HearthKit.Icon(parent, icon, x, y + 2f, size + 6f);
        float labelX = x + size + 18f;
        TMP_Text label = CodeUI.Label(name, parent, labelX, y, labelWidth, size + 10f, size, style.Text);
        label.text = name;
        float valueX = inline ? labelX + labelWidth : x + width - 120f;
        value = CodeUI.Label("Value", parent, valueX, y, inline ? valueWidth : 120f, size + 10f, size, style.Text,
            inline ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
        if (!bar)
        {
            return;
        }

        float barX = inline ? valueX + valueWidth + 2f : labelX;
        float barY = inline ? y + size * 0.5f + 1f : y + size + 12f;
        float barWidth = inline ? x + width - barX : x + width - labelX;
        fill = KitBar.Make(parent, barX, barY, barWidth, 9f, out fillImage);
    }

    /// <summary>Shows <paramref name="shown"/>; the bar at <paramref name="fraction"/>, in <paramref name="low"/> under a quarter.</summary>
    public void Set(string shown, float fraction, Color colour, Color low)
    {
        value.text = HearthKit.ToneNumbers(shown);
        if (hasBar)
        {
            KitBar.Set(fill, fillImage, fraction, fraction < 0.25f ? low : colour);
        }
    }
}

/// <summary>A thin rounded bar: a dark track and a coloured fill.</summary>
public static class KitBar
{
    public static RectTransform Make(Transform parent, float x, float y, float width, float height, out Image fillImage)
    {
        UIStyle style = UIStyle.Current;
        float corners = Mathf.Max(1f, 60f / Mathf.Max(4f, height));
        RectTransform track = CodeUI.At("Track", parent, x, y, width, height);
        Image trackImage = HearthKit.Sliced(track.gameObject.AddComponent<Image>(), style.Card, corners);
        trackImage.color = style.Track;
        trackImage.raycastTarget = false;
        RectTransform fill = CodeUI.Stretch("Fill", track);
        fillImage = HearthKit.Sliced(fill.gameObject.AddComponent<Image>(), style.Card, corners);
        fillImage.raycastTarget = false;
        // Hearthline 3: the fill glows, shines and glides (the bar shader), where the style has it.
        if (style.BarMaterial != null)
        {
            KitBarFill.On(fillImage);
        }

        return fill;
    }

    /// <summary>Sets a bar: through its KitBarFill (gliding, pulsing, the bar shader) where it has one.</summary>
    public static void Set(RectTransform fill, Image fillImage, float fraction, Color colour)
    {
        KitBarFill alive = fillImage.GetComponent<KitBarFill>();
        if (alive != null)
        {
            alive.Show(fraction, colour);
            return;
        }

        fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        fillImage.color = colour;
    }
}

/// <summary>A row of framed tabs: the one shown lit gold, the rest dark with a faint rim.</summary>
public sealed class KitTabs
{
    private readonly List<(RectTransform rect, Image fill, Image rim, TMP_Text label)> tabs = new List<(RectTransform, Image, Image, TMP_Text)>();

    public KitTabs(Transform parent, float x, float y, float tabWidth, float height, IReadOnlyList<string> names, Action<int> chosen, float gap = 10f)
    {
        UIStyle style = UIStyle.Current;
        for (int i = 0; i < names.Count; i++)
        {
            int index = i;
            RectTransform rect = CodeUI.At(names[i] + " Tab", parent, x + i * (tabWidth + gap), y, tabWidth, height);
            Image fill = HearthKit.Sliced(rect.gameObject.AddComponent<Image>(), style.Card, 1.4f);
            Image rim = HearthKit.Rim(rect, 0.3f, 1.4f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            HearthKit.Tint(button);
            UIHighlight.On(rect.gameObject);
            button.onClick.AddListener(() => chosen(index));
            TMP_Text label = CodeUI.Label("Label", rect, 0f, 0f, tabWidth, height, 21f, style.Text, TextAlignmentOptions.Center);
            style.Title(label);
            label.text = names[i];
            tabs.Add((rect, fill, rim, label));
        }
    }

    /// <summary>The tabs' rects, for PadTabs (where the bumpers' glyphs go).</summary>
    public List<RectTransform> Rects => tabs.ConvertAll(tab => tab.rect);

    public int Count => tabs.Count;

    /// <summary>Lights the tab shown.</summary>
    public void Show(int shown)
    {
        UIStyle style = UIStyle.Current;
        for (int i = 0; i < tabs.Count; i++)
        {
            bool on = i == shown;
            tabs[i].fill.color = on ? Color.Lerp(style.Inset, style.AccentDeep, 0.55f) : style.Inset;
            tabs[i].rim.color = on ? style.Accent : HearthKit.Faded(style.Accent, 0.3f);
            tabs[i].label.color = on ? style.AccentBright : style.Text;
            tabs[i].label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}

/// <summary>A row of rounded chips (a trait, a state), each tinted by its colour, laid out left to right.</summary>
public sealed class KitChips
{
    private readonly RectTransform row;
    private readonly List<(GameObject root, RectTransform rect, Image fill, Image rim, TMP_Text label)> pool =
        new List<(GameObject, RectTransform, Image, Image, TMP_Text)>();

    public KitChips(Transform parent, float x, float y, float width)
    {
        row = CodeUI.At("Chips", parent, x, y, width, 36f);
    }

    public void Show(IReadOnlyList<(string text, Color colour)> shown)
    {
        UIStyle style = UIStyle.Current;
        while (pool.Count < shown.Count)
        {
            RectTransform rect = CodeUI.At("Chip", row, 0f, 0f, 100f, 36f);
            Image fill = HearthKit.Sliced(rect.gameObject.AddComponent<Image>(), style.Card, 1.1f);
            fill.raycastTarget = false;
            Image rim = HearthKit.Rim(rect, 1f, 1.1f);
            TMP_Text label = CodeUI.Label("Label", rect, 0f, 0f, 100f, 36f, 17f, style.Text, TextAlignmentOptions.Center);
            pool.Add((rect.gameObject, rect, fill, rim, label));
        }

        float x = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            bool used = i < shown.Count;
            pool[i].root.SetActive(used);
            if (!used)
            {
                continue;
            }

            pool[i].label.text = shown[i].text;
            pool[i].label.ForceMeshUpdate();
            float width = pool[i].label.preferredWidth + 36f;
            pool[i].rect.sizeDelta = new Vector2(width, 36f);
            pool[i].rect.anchoredPosition = new Vector2(x, 0f);
            pool[i].label.rectTransform.sizeDelta = new Vector2(width, 36f);
            Color colour = shown[i].colour;
            pool[i].fill.color = Color.Lerp(style.Inset, colour, 0.28f);
            pool[i].rim.color = HearthKit.Faded(colour, 0.85f);
            pool[i].label.color = Color.Lerp(colour, Color.white, 0.55f);
            x += width + 10f;
        }
    }
}

/// <summary>How a kit button is laid out.</summary>
public enum KitButtonShape
{
    /// <summary>A square-ish tile: the icon on top, its name under it.</summary>
    Tile = 0,
    /// <summary>A small pill: the icon left, its name beside it.</summary>
    Compact = 1,
    /// <summary>A list row: the icon left, the name and a line of detail beside it.</summary>
    Row = 2,
}

/// <summary>A button for one MenuAction: its icon (MenuAction.Icon) and name, and in a row its detail; dim when it can't
/// be chosen.</summary>
public sealed class KitButton
{
    private readonly GameObject root;
    private readonly Button button;
    private readonly Image fill;
    private readonly Image rim;
    private readonly Image icon;
    private readonly TMP_Text label;
    private readonly TMP_Text detail;
    private readonly TMP_Text status;
    private readonly KitButtonShape shape;
    private readonly float width;
    private MenuAction action;

    /// <summary>Where it is, for windows that lay their buttons out as they refresh.</summary>
    public RectTransform Rect => (RectTransform)root.transform;

    public KitButton(Transform parent, float x, float y, float width, float height, Action<MenuAction> chosen, KitButtonShape shape = KitButtonShape.Tile)
    {
        UIStyle style = UIStyle.Current;
        this.shape = shape;
        this.width = width;
        float corners = shape == KitButtonShape.Compact ? 1.6f : 1.3f;
        RectTransform rect = CodeUI.At("Action", parent, x, y, width, height);
        root = rect.gameObject;
        fill = HearthKit.Sliced(root.AddComponent<Image>(), style.Card, corners);
        rim = HearthKit.Rim(rect, 0.6f, corners);
        button = root.AddComponent<Button>();
        button.targetGraphic = fill;
        HearthKit.Tint(button);
        UIHighlight.On(root);
        button.onClick.AddListener(() => chosen(action));
        switch (shape)
        {
            case KitButtonShape.Row:
                icon = CodeUI.At("Icon", rect, 18f, (height - 34f) * 0.5f, 34f, 34f).gameObject.AddComponent<Image>();
                label = CodeUI.Label("Label", rect, 68f, 8f, width - 90f, 28f, 20f, style.Text);
                detail = CodeUI.Label("Detail", rect, 68f, 36f, width - 90f, 24f, 16f, style.Muted);
                status = CodeUI.Label("Status", rect, width - 190f, 0f, 172f, height, 16f, style.Accent, TextAlignmentOptions.MidlineRight);
                break;
            case KitButtonShape.Compact:
                icon = CodeUI.At("Icon", rect, 12f, (height - 22f) * 0.5f, 22f, 22f).gameObject.AddComponent<Image>();
                label = CodeUI.Label("Label", rect, 40f, 0f, width - 48f, height, 18f, style.Text, TextAlignmentOptions.Center);
                break;
            default:
                icon = CodeUI.At("Icon", rect, (width - 34f) * 0.5f, 12f, 34f, 34f).gameObject.AddComponent<Image>();
                label = CodeUI.Label("Label", rect, 4f, height - 34f, width - 8f, 26f, 17f, style.Text, TextAlignmentOptions.Center);
                break;
        }

        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    /// <summary>Shows this action (none hides the button); <paramref name="name"/> in place of its label when given.</summary>
    public void Bind(MenuAction shown, string name = null)
    {
        action = shown;
        root.SetActive(shown != null);
        if (shown == null)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        button.interactable = shown.Enabled;
        label.text = name ?? shown.Label;
        label.color = shown.Enabled ? style.Text : style.Muted;
        if (detail != null)
        {
            detail.text = HearthKit.ToneNumbers(GameInput.WithRebinds(shown.Detail ?? ""));
        }

        if (status != null)
        {
            bool hasStatus = !string.IsNullOrEmpty(shown.Status);
            status.text = shown.Status ?? "";
            status.color = shown.Enabled ? style.AccentBright : style.Danger;
            float room = width - 90f - (hasStatus ? 180f : 0f);
            label.rectTransform.sizeDelta = new Vector2(room, label.rectTransform.sizeDelta.y);
            detail.rectTransform.sizeDelta = new Vector2(room, detail.rectTransform.sizeDelta.y);
        }

        icon.sprite = style.Icon(shown.Icon);
        icon.enabled = icon.sprite != null;
        icon.color = shown.Enabled ? style.Accent : HearthKit.Faded(style.Muted, 0.7f);
        fill.color = shape == KitButtonShape.Row ? style.Panel : Color.Lerp(style.Inset, style.Control, 0.6f);
        rim.color = HearthKit.Faded(style.Accent, shown.Enabled ? (shape == KitButtonShape.Row ? 0.3f : 0.6f) : 0.18f);
    }
}

/// <summary>A list of MenuActions as row buttons, in columns; a line when there is nothing to do.</summary>
public sealed class KitActionList
{
    private readonly List<KitButton> rows = new List<KitButton>();
    private readonly Transform parent;
    private readonly float x;
    private readonly float y;
    private readonly float width;
    private readonly float rowHeight;
    private readonly int columns;
    private readonly Action<MenuAction> chosen;
    private readonly TMP_Text empty;

    public KitActionList(Transform parent, float x, float y, float width, Action<MenuAction> chosen, int columns = 2, float rowHeight = 68f,
        string nothing = "Nothing to do here just now.")
    {
        this.parent = parent;
        this.x = x;
        this.y = y;
        this.width = width;
        this.chosen = chosen;
        this.columns = Mathf.Max(1, columns);
        this.rowHeight = rowHeight;
        empty = CodeUI.Label("Nothing", parent, x + 4f, y + 8f, width, 26f, 18f, UIStyle.Current.Muted);
        empty.text = nothing;
    }

    public void Show(IReadOnlyList<MenuAction> actions)
    {
        float column = (width - 12f * (columns - 1)) / columns;
        while (rows.Count < actions.Count)
        {
            int i = rows.Count;
            rows.Add(new KitButton(parent, x + (i % columns) * (column + 12f), y + (i / columns) * (rowHeight + 10f), column, rowHeight, chosen,
                KitButtonShape.Row));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Bind(i < actions.Count ? actions[i] : null);
        }

        empty.gameObject.SetActive(actions.Count == 0);
    }
}

/// <summary>A container's slots as a grid of rounded cells: each item's icon and how many, empty cells dim. A click on
/// any calls <c>open</c> (the container's own window).</summary>
public sealed class KitSlotGrid
{
    private const float Gap = 8f;
    private readonly Transform parent;
    private readonly float x;
    private readonly float y;
    private readonly float cell;
    private readonly int perRow;
    private readonly Action open;
    private readonly List<(GameObject root, Image icon, TMP_Text count, Image rim)> cells = new List<(GameObject, Image, TMP_Text, Image)>();

    public KitSlotGrid(Transform parent, float x, float y, float width, Action open, float cell = 58f)
    {
        this.parent = parent;
        this.x = x;
        this.y = y;
        this.cell = cell;
        this.open = open;
        perRow = Mathf.Max(1, Mathf.FloorToInt((width + Gap) / (cell + Gap)));
    }

    /// <summary>How far down the grid reaches for this many slots.</summary>
    public float Bottom(int slots) => y + Mathf.CeilToInt(slots / (float)perRow) * (cell + Gap);

    /// <summary>Shows the first <paramref name="slots"/> of <paramref name="container"/> (none for null); returns how many hold something.</summary>
    public int Show(ItemContainer container, int slots)
    {
        UIStyle style = UIStyle.Current;
        int count = container != null ? Mathf.Min(slots, container.SlotCount) : 0;
        while (cells.Count < count)
        {
            int i = cells.Count;
            RectTransform rect = CodeUI.At("Slot", parent, x + (i % perRow) * (cell + Gap), y + (i / perRow) * (cell + Gap), cell, cell);
            Image fill = HearthKit.Sliced(rect.gameObject.AddComponent<Image>(), style.Card, 1.8f);
            fill.color = style.Panel;
            Image rim = HearthKit.Rim(rect, 0.18f, 1.8f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            HearthKit.Tint(button);
            UIHighlight.On(rect.gameObject);
            button.onClick.AddListener(() => open?.Invoke());
            Image icon = CodeUI.At("Icon", rect, 7f, 7f, cell - 14f, cell - 14f).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TMP_Text amount = CodeUI.Label("Count", rect, 0f, cell - 22f, cell - 6f, 20f, 15f, style.Text, TextAlignmentOptions.Right);
            cells.Add((rect.gameObject, icon, amount, rim));
        }

        int used = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            bool shown = i < count;
            cells[i].root.SetActive(shown);
            if (!shown)
            {
                continue;
            }

            ItemStack stack = container.GetSlot(i);
            bool full = !stack.IsEmpty;
            used += full ? 1 : 0;
            cells[i].icon.sprite = full ? stack.Item.Icon : null;
            cells[i].icon.enabled = full && stack.Item.Icon != null;
            cells[i].count.text = full && stack.Count > 1 ? stack.Count.ToString() : "";
            cells[i].rim.color = HearthKit.Faded(style.Accent, full ? 0.55f : 0.18f);
        }

        return used;
    }
}

/// <summary>One labelled line in a list of facts: a gold label, its value, a hairline under it.</summary>
public sealed class KitFactRow
{
    private readonly TMP_Text label;
    private readonly TMP_Text value;

    public KitFactRow(Transform parent, float x, float y, float width, float labelWidth = 300f)
    {
        UIStyle style = UIStyle.Current;
        label = CodeUI.Label("Label", parent, x + 4f, y, labelWidth, 30f, 19f, style.Accent);
        value = CodeUI.Label("Value", parent, x + labelWidth, y, width - labelWidth - 10f, 30f, 20f, style.Text);
        CodeUI.Hairline(parent, x, y + 42f, width, 1f, style);
    }

    public void Set(string name, string shown)
    {
        label.text = name;
        value.text = HearthKit.ToneNumbers(shown);
    }
}

/// <summary>What fills one place (a saddle, a weapon, an armour slot): an icon, a small capital label, what is there, a
/// line, and optionally a bar for how full.</summary>
public sealed class KitSlotCard
{
    private readonly Image icon;
    private readonly TMP_Text value;
    private readonly TMP_Text line;
    private readonly RectTransform fill;
    private readonly Image fillImage;
    private readonly GameObject track;

    public KitSlotCard(Transform parent, float x, float y, float width, string name, string iconName, float height = 92f)
    {
        UIStyle style = UIStyle.Current;
        RectTransform card = HearthKit.Card(parent, name + " Slot", x, y, width, height, 0.35f, style.Panel, 1.3f);
        icon = HearthKit.Icon(card, iconName, 16f, (height - 46f) * 0.5f, 46f);
        TMP_Text label = CodeUI.Label("Name", card, 76f, 10f, width - 90f, 22f, 15f, style.Accent);
        label.text = name.ToUpperInvariant();
        label.characterSpacing = 6f;
        value = CodeUI.Label("Value", card, 76f, 32f, width - 90f, 28f, 20f, style.Text);
        line = CodeUI.Label("Line", card, 76f, 58f, width - 90f, 20f, 15f, style.Muted);
        fill = KitBar.Make(card, 76f, height - 11f, width - 96f, 5f, out fillImage);
        track = fill.parent.gameObject;
    }

    /// <summary>What is there (<paramref name="worn"/>: something is), a line, and a bar when <paramref name="full"/> is 0 or more.</summary>
    public void Set(string shown, string detail, bool worn, float full = -1f)
    {
        UIStyle style = UIStyle.Current;
        value.text = shown;
        value.color = worn ? style.Text : style.Muted;
        line.text = detail;
        icon.color = worn ? style.Accent : HearthKit.Faded(style.Muted, 0.6f);
        track.SetActive(full >= 0f);
        if (full >= 0f)
        {
            KitBar.Set(fill, fillImage, full, full > 1f ? style.Danger : full > 0.6f ? style.Warning : style.Accent);
        }
    }
}

/// <summary>Something being worked towards (a trait, a skill, a deed): an icon, its name, a line, a state on the right and
/// a bar under it, lit when done.</summary>
public sealed class KitProgressRow
{
    private readonly Image icon;
    private readonly TMP_Text name;
    private readonly TMP_Text line;
    private readonly TMP_Text state;
    private readonly RectTransform fill;
    private readonly Image fillImage;

    public KitProgressRow(Transform parent, float x, float y, float width, string iconName = "Star", float height = 74f)
    {
        UIStyle style = UIStyle.Current;
        RectTransform card = HearthKit.Card(parent, "Progress", x, y, width, height, 0.25f, style.Panel, 1.3f);
        icon = HearthKit.Icon(card, iconName, 18f, (height - 34f) * 0.5f, 34f);
        name = CodeUI.Label("Name", card, 68f, 10f, 260f, 28f, 21f, style.Text);
        style.Title(name);
        line = CodeUI.Label("Line", card, 68f, 40f, width - 420f, 24f, 16f, style.Muted);
        state = CodeUI.Label("State", card, width - 320f, 10f, 300f, 26f, 16f, style.Muted, TextAlignmentOptions.Right);
        fill = KitBar.Make(card, width - 320f, 44f, 300f, 9f, out fillImage);
    }

    public void Set(string title, string what, bool done, float progress, string doneText = "Learned")
    {
        UIStyle style = UIStyle.Current;
        name.text = title;
        name.color = done ? style.AccentBright : style.Text;
        line.text = what;
        state.text = done ? doneText : $"{progress * 100f:0}% of the way";
        state.color = done ? style.Good : style.Muted;
        icon.color = done ? style.Accent : HearthKit.Faded(style.Muted, 0.6f);
        KitBar.Set(fill, fillImage, progress, done ? style.Good : style.Info);
    }
}
