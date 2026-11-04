using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// A tab of the Haven window: headed shelves of rows on the left and, on the right, what the chosen row is, in detail, with its
/// button. It only draws the HavenShelf lists it is given. Rows are kept and reused, so reading again every second neither
/// allocates new objects nor loses the player's place in the list.
/// </summary>
public sealed class HavenListPage
{
    private const float Margin = 44f;
    private const float TitleTop = 20f;
    private const float ListTop = 88f;
    private const float DefaultDetailWidth = 400f;
    private const float DetailGap = 40f;
    private const float RowHeight = 62f;
    private const float RowGap = 8f;
    private const float ColumnGap = 10f;
    private const float HeadingHeight = 34f;
    private const float ShelfGap = 14f;
    private const float IconSize = 46f;
    private const float DefaultIconSize = 84f;
    private const float ActionHeight = 46f;
    private const float ActionGap = 8f;
    private const int MostActions = 4;

    private sealed class Row
    {
        public GameObject Root;
        public Image Frame;
        public Outline Rim;
        public Image Icon;
        public TMP_Text Glyph;
        public TMP_Text Name;
        public TMP_Text Line;
        public TMP_Text Badge;
        public HavenEntry Entry;
    }

    private sealed class ActionButton
    {
        public GameObject Root;
        public Button Button;
        public UIRole Role;
        public TMP_Text Label;
        public HavenAction Action;
    }

    private readonly RectTransform root;
    private readonly float listWidth;
    private readonly int columns;
    private readonly float height;
    private readonly Action<HavenAction> runAction;
    private readonly List<Row> rows = new List<Row>();
    private readonly List<TMP_Text> headings = new List<TMP_Text>();
    private ScrollRect scroll;
    private RectTransform content;
    private TMP_Text note;
    private TMP_Text emptyNote;
    private GameObject detail;
    private Image detailIcon;
    private TMP_Text detailGlyph;
    private TMP_Text detailName;
    private TMP_Text detailStatus;
    private TMP_Text detailBody;
    private ScrollRect detailScroll;
    private float detailHeight;
    private readonly List<ActionButton> actions = new List<ActionButton>();
    private HavenEntry selectedEntry;
    private object selectedKey;
    private readonly float detailWidth;
    private readonly float detailIconSize;
    private readonly float detailBodyTop;

    /// <param name="runAction">Runs an entry's button; the window decides what else that means (closing, reading again).</param>
    public HavenListPage(RectTransform parent, string title, float width, float height, int columns, string emptyText, Action<HavenAction> runAction,
        float detailWidth = DefaultDetailWidth, float detailIconSize = DefaultIconSize)
    {
        this.detailWidth = detailWidth;
        this.detailIconSize = detailIconSize;
        detailBodyTop = detailIconSize + 16f;
        this.columns = Mathf.Max(1, columns);
        this.height = height;
        this.runAction = runAction;
        listWidth = width - Margin * 2f - detailWidth - DetailGap;
        UIStyle style = UIStyle.Current;
        root = At(title, parent, 0f, 0f, width, height);
        TMP_Text heading = Label("Title", root, Margin, TitleTop, listWidth, 48f, 34f, style.Text);
        style.Title(heading);
        heading.text = title;
        note = Label("Note", root, Margin, TitleTop + 42f, listWidth, 24f, 17f, style.Muted);
        BuildList(style, emptyText);
        Hairline(root, Margin + listWidth + DetailGap * 0.5f, TitleTop, 1f, height - TitleTop - 10f, style);
        BuildDetail(style, Margin + listWidth + DetailGap);
    }

    /// <summary>Raised when an entry is shown in the detail pane, by choosing it or by being the one a draw falls on.</summary>
    public event Action<HavenEntry> Chosen;

    public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

    public void ScrollToTop() => scroll.verticalNormalizedPosition = 1f;

    /// <summary>Draws the shelves, keeping the chosen entry when it is still there and otherwise choosing the first.</summary>
    public void Draw(IReadOnlyList<HavenShelf> shelves, string summary)
    {
        note.text = summary;
        UIStyle style = UIStyle.Current;
        float rowWidth = (listWidth - ColumnGap * (columns - 1)) / columns;
        float y = 0f;
        int usedRows = 0;
        int usedHeadings = 0;
        HavenEntry first = null;
        selectedEntry = null;
        foreach (HavenShelf shelf in shelves)
        {
            if (shelf.Entries.Count == 0)
            {
                continue;
            }

            TMP_Text heading = HeadingAt(usedHeadings++, style);
            heading.gameObject.SetActive(true);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -y);
            heading.text = $"{shelf.Title.ToUpperInvariant()}  <color={UIStyle.Hex(style.Muted)}>{shelf.Entries.Count}</color>";
            y += HeadingHeight;

            for (int i = 0; i < shelf.Entries.Count; i++)
            {
                HavenEntry entry = shelf.Entries[i];
                first ??= entry;
                if (entry.Key != null && Equals(entry.Key, selectedKey))
                {
                    selectedEntry = entry;
                }

                Row row = RowAt(usedRows++, style);
                row.Root.SetActive(true);
                ((RectTransform)row.Root.transform).anchoredPosition = new Vector2((i % columns) * (rowWidth + ColumnGap), -(y + (i / columns) * (RowHeight + RowGap)));
                ((RectTransform)row.Root.transform).sizeDelta = new Vector2(rowWidth, RowHeight);
                DrawRow(row, entry, rowWidth, style);
            }

            y += Mathf.CeilToInt(shelf.Entries.Count / (float)columns) * (RowHeight + RowGap) + ShelfGap;
        }

        for (int i = usedRows; i < rows.Count; i++)
        {
            rows[i].Root.SetActive(false);
            rows[i].Entry = null;
        }

        for (int i = usedHeadings; i < headings.Count; i++)
        {
            headings[i].gameObject.SetActive(false);
        }

        content.sizeDelta = new Vector2(listWidth, y);
        emptyNote.gameObject.SetActive(usedRows == 0);
        selectedEntry ??= first;
        selectedKey = selectedEntry?.Key;
        foreach (Row row in rows)
        {
            if (row.Entry != null)
            {
                DrawChosen(row, row.Entry == selectedEntry, style);
            }
        }

        DrawDetail(style);
        RaiseChosen();
    }

    private void RaiseChosen()
    {
        if (selectedEntry == null)
        {
            return;
        }

        Chosen?.Invoke(selectedEntry);
        foreach (Row row in rows)
        {
            if (row.Entry == selectedEntry)
            {
                row.Badge.text = selectedEntry.Badge;
            }
        }
    }

    private void DrawRow(Row row, HavenEntry entry, float width, UIStyle style)
    {
        row.Entry = entry;
        bool dimmed = entry.State == HavenEntryState.Locked;
        row.Icon.gameObject.SetActive(entry.Icon != null);
        row.Icon.sprite = entry.Icon;
        row.Icon.color = dimmed ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        row.Glyph.gameObject.SetActive(entry.Icon == null);
        row.Glyph.text = string.IsNullOrEmpty(entry.Name) ? "" : entry.Name.Substring(0, 1).ToUpperInvariant();
        row.Glyph.color = dimmed ? style.Muted : style.Accent;
        row.Name.text = entry.Name;
        row.Name.color = dimmed ? style.Muted : style.Text;
        row.Line.text = entry.Line;
        row.Line.color = LineColour(entry.State, style);
        bool hasBadge = !string.IsNullOrEmpty(entry.Badge);
        row.Badge.text = entry.Badge;
        row.Badge.color = entry.State == HavenEntryState.Ready ? style.Accent : style.Text;
        float textWidth = width - IconSize - 28f - (hasBadge ? 62f : 8f);
        row.Name.rectTransform.sizeDelta = new Vector2(textWidth, 28f);
        row.Line.rectTransform.sizeDelta = new Vector2(textWidth, 22f);
        row.Badge.rectTransform.anchoredPosition = new Vector2(width - 62f, -8f);
    }

    private static Color LineColour(HavenEntryState state, UIStyle style)
    {
        switch (state)
        {
            case HavenEntryState.Working:
                return style.Accent;
            case HavenEntryState.Attention:
                return style.Warning;
            default:
                return style.Muted;
        }
    }

    private static void DrawChosen(Row row, bool on, UIStyle style)
    {
        row.Frame.color = on ? style.ControlLit : style.Inset;
        row.Rim.effectColor = on ? style.Accent : style.Hairline;
        row.Rim.effectDistance = on ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
    }

    private void Choose(Row row)
    {
        if (row.Entry == null)
        {
            return;
        }

        selectedEntry = row.Entry;
        selectedKey = row.Entry.Key;
        UIStyle style = UIStyle.Current;
        foreach (Row other in rows)
        {
            if (other.Entry != null)
            {
                DrawChosen(other, other == row, style);
            }
        }

        DrawDetail(style);
        detailScroll.verticalNormalizedPosition = 1f;
        RaiseChosen();
    }

    // A stick moving down a long list has to be able to see where it has got to.
    private void Reveal(Row row)
    {
        var rect = (RectTransform)row.Root.transform;
        float top = -rect.anchoredPosition.y;
        float bottom = top + RowHeight;
        float viewHeight = ((RectTransform)scroll.transform).rect.height;
        Vector2 at = content.anchoredPosition;
        if (top < at.y)
        {
            at.y = top;
        }
        else if (bottom > at.y + viewHeight)
        {
            at.y = bottom - viewHeight;
        }

        content.anchoredPosition = at;
    }

    private void DrawDetail(UIStyle style)
    {
        HavenEntry entry = selectedEntry;
        detail.SetActive(entry != null);
        if (entry == null)
        {
            return;
        }

        bool dimmed = entry.State == HavenEntryState.Locked;
        detailIcon.gameObject.SetActive(entry.Icon != null);
        detailIcon.sprite = entry.Icon;
        detailIcon.color = dimmed ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        detailGlyph.gameObject.SetActive(entry.Icon == null);
        detailGlyph.text = string.IsNullOrEmpty(entry.Name) ? "" : entry.Name.Substring(0, 1).ToUpperInvariant();
        detailName.text = entry.Name;
        detailStatus.text = entry.Status;
        detailStatus.color = LineColour(entry.State, style);
        detailBody.text = DetailText(entry, style);
        DrawActions(entry);
    }

    // Buttons stack up from the bottom of the pane, the first on top; the text above gives way to them.
    private void DrawActions(HavenEntry entry)
    {
        int shown = Mathf.Min(entry.Actions.Count, MostActions);
        for (int i = 0; i < actions.Count; i++)
        {
            ActionButton button = actions[i];
            bool used = i < shown;
            button.Root.SetActive(used);
            if (!used)
            {
                continue;
            }

            HavenAction action = entry.Actions[i];
            button.Action = action;
            button.Label.text = action.Label;
            button.Button.interactable = action.Enabled;
            button.Role.Set(action.Primary ? UIRole.Kind.PrimaryButton : UIRole.Kind.Button);
            button.Label.fontStyle = action.Primary ? FontStyles.Bold : FontStyles.Normal;
            float y = detailHeight - (shown - i) * (ActionHeight + ActionGap) + ActionGap;
            ((RectTransform)button.Root.transform).anchoredPosition = new Vector2(0f, -y);
        }

        float bodyHeight = detailHeight - detailBodyTop - shown * (ActionHeight + ActionGap) - 4f;
        ((RectTransform)detailScroll.transform).sizeDelta = new Vector2(detailWidth, bodyHeight);
    }

    private static string DetailText(HavenEntry entry, UIStyle style)
    {
        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(entry.Description))
        {
            text.Append(entry.Description).Append("\n\n");
        }

        string accent = UIStyle.Hex(style.Accent);
        string good = UIStyle.Hex(style.Good);
        string danger = UIStyle.Hex(style.Danger);
        string muted = UIStyle.Hex(style.Muted);
        foreach (HavenDetail part in entry.Details)
        {
            text.Append("<size=15><color=").Append(accent).Append("><cspace=4>").Append(part.Heading.ToUpperInvariant()).Append("</cspace></color></size>\n");
            if (!string.IsNullOrEmpty(part.Body))
            {
                text.Append(part.Body).Append('\n');
            }

            foreach (HavenCost cost in part.Costs)
            {
                text.Append(cost.Name).Append("<pos=68%><color=").Append(cost.Have >= cost.Need ? good : danger).Append('>').Append(cost.Have)
                    .Append("</color><color=").Append(muted).Append("> / ").Append(cost.Need).Append("</color>\n");
            }

            text.Append('\n');
        }

        return text.ToString().TrimEnd();
    }


    private TMP_Text HeadingAt(int index, UIStyle style)
    {
        while (headings.Count <= index)
        {
            TMP_Text heading = Label($"Heading {headings.Count}", content, 0f, 0f, listWidth, HeadingHeight, 16f, style.Accent);
            heading.characterSpacing = 6f;
            headings.Add(heading);
        }

        return headings[index];
    }

    private Row RowAt(int index, UIStyle style)
    {
        while (rows.Count <= index)
        {
            rows.Add(BuildRow(rows.Count, style));
        }

        return rows[index];
    }

    private Row BuildRow(int index, UIStyle style)
    {
        RectTransform rect = At($"Row {index}", content, 0f, 0f, listWidth, RowHeight);
        var row = new Row { Root = rect.gameObject };
        row.Frame = rect.gameObject.AddComponent<Image>();
        row.Rim = rect.gameObject.AddComponent<Outline>();
        rect.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = row.Frame;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Choose(row));
        rect.gameObject.AddComponent<UISelectHook>().Selected = () =>
        {
            Choose(row);
            Reveal(row);
        };

        float iconTop = (RowHeight - IconSize) * 0.5f;
        row.Icon = At("Icon", rect, 10f, iconTop, IconSize, IconSize).gameObject.AddComponent<Image>();
        row.Icon.preserveAspect = true;
        row.Icon.raycastTarget = false;
        row.Glyph = Label("Glyph", rect, 10f, iconTop, IconSize, IconSize, 28f, style.Accent, TextAlignmentOptions.Center);
        row.Name = Label("Name", rect, IconSize + 20f, 7f, 100f, 28f, 19f, style.Text);
        row.Line = Label("Line", rect, IconSize + 20f, 33f, 100f, 22f, 15f, style.Muted);
        row.Badge = Label("Badge", rect, 0f, 8f, 52f, 46f, 22f, style.Text, TextAlignmentOptions.Right);
        return row;
    }

    private void BuildList(UIStyle style, string emptyText)
    {
        RectTransform area = At("List", root, Margin, ListTop, listWidth, height - ListTop - 10f);
        scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        area.gameObject.AddComponent<RectMask2D>();
        // A transparent graphic lets the wheel scroll over the gaps between rows.
        area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        content = At("Rows", area, 0f, 0f, listWidth, 0f);
        scroll.content = content;
        scroll.viewport = area;
        emptyNote = Paragraph("Empty", area, 0f, 10f, listWidth, 120f, 19f, style.Muted);
        emptyNote.text = emptyText;
    }

    private void BuildDetail(UIStyle style, float x)
    {
        RectTransform rect = At("Detail", root, x, TitleTop, detailWidth, height - TitleTop - 10f);
        detail = rect.gameObject;
        detailIcon = At("Icon", rect, 0f, 0f, detailIconSize, detailIconSize).gameObject.AddComponent<Image>();
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;
        detailGlyph = Label("Glyph", rect, 0f, 0f, detailIconSize, detailIconSize, 52f, style.Accent, TextAlignmentOptions.Center);
        float textLeft = detailIconSize + 16f;
        detailName = Label("Name", rect, textLeft, 2f, detailWidth - textLeft, 44f, 30f, style.Text);
        style.Title(detailName);
        detailName.fontSizeMax = detailName.fontSize;
        detailName.fontSizeMin = 18f;
        detailName.enableAutoSizing = true;
        detailStatus = Paragraph("Status", rect, textLeft, 46f, detailWidth - textLeft, 44f, 17f, style.Muted);

        detailHeight = rect.sizeDelta.y;
        RectTransform area = At("Body", rect, 0f, detailBodyTop, detailWidth, detailHeight - detailBodyTop);
        detailScroll = area.gameObject.AddComponent<ScrollRect>();
        detailScroll.horizontal = false;
        detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.scrollSensitivity = 40f;
        area.gameObject.AddComponent<RectMask2D>();
        area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        detailBody = Paragraph("Text", area, 0f, 0f, detailWidth, 0f, 18f, style.Text);
        detailBody.overflowMode = TextOverflowModes.Overflow;
        detailBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        detailScroll.content = detailBody.rectTransform;
        detailScroll.viewport = area;

        for (int i = 0; i < MostActions; i++)
        {
            var slot = new ActionButton();
            slot.Label = AddButton($"Action {i}", rect, 0f, 0f, detailWidth, UIRole.Kind.Button, () => Run(slot.Action), out slot.Button);
            slot.Root = slot.Button.gameObject;
            slot.Role = slot.Root.GetComponent<UIRole>();
            slot.Root.SetActive(false);
            actions.Add(slot);
        }
    }

    private void Run(HavenAction action)
    {
        if (action != null && action.Enabled)
        {
            runAction(action);
        }
    }
}
