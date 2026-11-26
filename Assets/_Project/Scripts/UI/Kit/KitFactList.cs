using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Facts as rows (Hearthline: pictures with a few words, never a wall of text): each a picture or kit icon in a small framed card, a
/// short title in its tone's colour and at most a short muted line under it, under a heading for each group. For the results screen's
/// right-hand column (RaidReportFacts). Built once, laid out again on every Show; rows are reused.
/// </summary>
public sealed class KitFactList
{
    private const float Picture = 48f;
    private const float RowGap = 10f;
    private const float GroupGap = 14f;
    private const float HeadingHeight = 28f;
    private const float TitleSize = 18f;
    private const float LineSize = 15f;
    private const float TextLeft = Picture + 12f;

    private readonly RectTransform root;
    private readonly List<(RectTransform row, Image picture, Image icon, TMP_Text title, TMP_Text line)> rows =
        new List<(RectTransform, Image, Image, TMP_Text, TMP_Text)>();
    private readonly List<TMP_Text> headings = new List<TMP_Text>();

    /// <param name="parent">What it is laid in; it fills the parent's width from the top.</param>
    public KitFactList(RectTransform parent, string name)
    {
        root = CodeUI.At(name, parent, 0f, 0f, 0f, 0f);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.sizeDelta = Vector2.zero;
    }

    public RectTransform Root => root;

    public float Top
    {
        get => -root.anchoredPosition.y;
        set => root.anchoredPosition = new Vector2(0f, -value);
    }

    /// <summary>Lays the facts out across <paramref name="width"/>, a heading over each group; returns the height they take.</summary>
    public float Show(IReadOnlyList<ReportFact> facts, float width)
    {
        UIStyle style = UIStyle.Current;
        float y = 0f;
        int row = 0;
        int heading = 0;
        string group = null;
        foreach (ReportFact fact in facts)
        {
            if (fact.Group != group)
            {
                group = fact.Group;
                if (y > 0f)
                {
                    y += GroupGap;
                }

                TMP_Text label = HeadingAt(heading++);
                label.text = HearthKit.InlineIcon(GroupIcon(group), style.Accent) + "  " + (group ?? "").ToUpperInvariant();
                label.rectTransform.anchoredPosition = new Vector2(0f, -y);
                label.rectTransform.sizeDelta = new Vector2(width, HeadingHeight);
                y += HeadingHeight + 4f;
            }

            var (rect, picture, icon, title, line) = RowAt(row++);
            rect.anchoredPosition = new Vector2(0f, -y);
            picture.sprite = fact.Picture;
            picture.enabled = fact.Picture != null;
            icon.sprite = fact.Picture == null ? style.Icon(fact.Icon) : null;
            icon.enabled = icon.sprite != null;
            icon.color = fact.Tone == FactTone.Bad ? style.Danger : fact.Tone == FactTone.Good ? style.Good : HearthKit.IconColour(fact.Icon);

            float textWidth = Mathf.Max(80f, width - TextLeft);
            title.text = fact.Title ?? "";
            // Bad news in red; good news reads in the plain colour (pale green on the leather was hard to read), its icon green.
            title.color = fact.Tone == FactTone.Bad ? style.Danger : style.Text;
            float titleHeight = Mathf.Min(title.GetPreferredValues(title.text, textWidth, 0f).y, TitleSize * 2.6f);
            title.rectTransform.sizeDelta = new Vector2(textWidth, titleHeight);

            bool hasLine = !string.IsNullOrEmpty(fact.Line);
            line.gameObject.SetActive(hasLine);
            float lineHeight = 0f;
            if (hasLine)
            {
                line.text = fact.Line;
                lineHeight = Mathf.Min(line.GetPreferredValues(line.text, textWidth, 0f).y, LineSize * 2.6f);
                line.rectTransform.anchoredPosition = new Vector2(TextLeft, -(titleHeight + 2f));
                line.rectTransform.sizeDelta = new Vector2(textWidth, lineHeight);
            }

            float words = titleHeight + (hasLine ? 2f + lineHeight : 0f);
            // A short title sits level with the middle of its picture.
            title.rectTransform.anchoredPosition = new Vector2(TextLeft, words < Picture ? -(Picture - words) * 0.5f : 0f);
            if (hasLine && words < Picture)
            {
                line.rectTransform.anchoredPosition = new Vector2(TextLeft, -((Picture - words) * 0.5f + titleHeight + 2f));
            }

            float height = Mathf.Max(Picture, words);
            rect.sizeDelta = new Vector2(width, height);
            y += height + RowGap;
        }

        for (int i = row; i < rows.Count; i++)
        {
            rows[i].row.gameObject.SetActive(false);
        }

        for (int i = heading; i < headings.Count; i++)
        {
            headings[i].gameObject.SetActive(false);
        }

        float total = row > 0 ? y - RowGap : 0f;
        root.sizeDelta = new Vector2(0f, total);
        return total;
    }

    private static string GroupIcon(string group) =>
        group == RaidReportFacts.People ? "Bond" : group == RaidReportFacts.AtHome ? "House" : group == RaidReportFacts.OnTheWay ? "Path" : "Info";

    private TMP_Text HeadingAt(int index)
    {
        while (headings.Count <= index)
        {
            UIStyle style = UIStyle.Current;
            TMP_Text label = CodeUI.Label("Heading " + headings.Count, root, 0f, 0f, 400f, HeadingHeight, 17f, style.Accent);
            label.richText = true;
            label.characterSpacing = 4f;
            headings.Add(label);
        }

        headings[index].gameObject.SetActive(true);
        return headings[index];
    }

    private (RectTransform, Image, Image, TMP_Text, TMP_Text) RowAt(int index)
    {
        while (rows.Count <= index)
        {
            UIStyle style = UIStyle.Current;
            RectTransform row = CodeUI.At("Fact " + rows.Count, root, 0f, 0f, 400f, Picture);
            RectTransform frame = HearthKit.Card(row, "Frame", 0f, 0f, Picture, Picture, 0.5f);
            RectTransform paint = CodeUI.Stretch("Picture", frame);
            paint.offsetMin = new Vector2(3f, 3f);
            paint.offsetMax = new Vector2(-3f, -3f);
            var picture = paint.gameObject.AddComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            RectTransform mark = CodeUI.Stretch("Icon", frame);
            mark.offsetMin = new Vector2(Picture * 0.2f, Picture * 0.2f);
            mark.offsetMax = new Vector2(-Picture * 0.2f, -Picture * 0.2f);
            var icon = mark.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Transform rim = frame.Find("Rim");
            if (rim != null)
            {
                rim.SetAsLastSibling();
            }

            TMP_Text title = CodeUI.Paragraph("Title", row, TextLeft, 0f, 300f, TitleSize * 1.3f, TitleSize, style.Text);
            title.richText = true;
            title.overflowMode = TextOverflowModes.Truncate;
            TMP_Text line = CodeUI.Paragraph("Line", row, TextLeft, TitleSize * 1.3f, 300f, LineSize * 1.3f, LineSize, style.Muted);
            line.overflowMode = TextOverflowModes.Truncate;
            rows.Add((row, picture, icon, title, line));
        }

        rows[index].row.gameObject.SetActive(true);
        return rows[index];
    }
}
