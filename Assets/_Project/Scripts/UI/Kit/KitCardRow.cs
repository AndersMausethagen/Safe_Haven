using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>What one KitPictureCard in a KitCardRow shows.</summary>
public struct KitCardFace
{
    public string Name;
    /// <summary>A picture laid whole on the card (a skill's or item's icon); a kit icon (<see cref="Icon"/>) when there is none.</summary>
    public Sprite Picture;
    public string Icon;
    /// <summary>A count or change in the picture's corner ("+120").</summary>
    public string Badge;
    /// <summary>The rim's colour; null for the kit's gold.</summary>
    public Color? Rim;
}

/// <summary>
/// A heading with its sign, then things as picture cards with their names (KitPictureCard), row after row, laid into a window
/// made in code or a prefab like KitHaul, laid out again on every Show. For lists that were lines of text (the user, 2026-11-21:
/// "UI windows should normally not have a wall of text"): what a raid taught, one card a skill.
/// </summary>
public sealed class KitCardRow
{
    private const float HeadingHeight = 30f;
    private const float Gap = 10f;

    private readonly RectTransform root;
    private readonly TMP_Text heading;
    private readonly float cardWidth;
    private readonly float pictureHeight;
    private readonly List<KitPictureCard> cards = new List<KitPictureCard>();

    /// <param name="parent">What it is laid in; it fills the parent's width.</param>
    public KitCardRow(RectTransform parent, string name, float cardWidth, float pictureHeight)
    {
        UIStyle style = UIStyle.Current;
        this.cardWidth = cardWidth;
        this.pictureHeight = pictureHeight;
        root = CodeUI.At(name, parent, 0f, 0f, 0f, 0f);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.sizeDelta = Vector2.zero;
        heading = CodeUI.Label("Heading", root, 0f, 0f, 600f, HeadingHeight, 19f, style.Text);
        heading.rectTransform.anchorMax = new Vector2(1f, 1f);
        heading.rectTransform.sizeDelta = new Vector2(0f, HeadingHeight);
        heading.richText = true;
        style.Title(heading);
        heading.fontStyle = FontStyles.Bold;
    }

    /// <summary>Where it starts, from the parent's top.</summary>
    public float Top
    {
        get => -root.anchoredPosition.y;
        set => root.anchoredPosition = new Vector2(0f, -value);
    }

    public void Hide() => root.gameObject.SetActive(false);

    /// <summary>Whether <paramref name="count"/> cards fit in <paramref name="rows"/> rows without a "more" card.</summary>
    public bool Fits(int count, int rows = 1) =>
        count <= Mathf.Max(1, Mathf.FloorToInt((Mathf.Max(cardWidth, root.rect.width) + Gap) / (cardWidth + Gap))) * Mathf.Max(1, rows);

    /// <summary>How tall it is with <paramref name="count"/> cards in at most <paramref name="rows"/> rows (0 for none).</summary>
    public float HeightFor(int count, int rows = 1)
    {
        if (count <= 0)
        {
            return 0f;
        }

        int perRow = Mathf.Max(1, Mathf.FloorToInt((Mathf.Max(cardWidth, root.rect.width) + Gap) / (cardWidth + Gap)));
        int used = Mathf.Min(Mathf.Max(1, rows), (count + perRow - 1) / perRow);
        float card = cards.Count > 0 ? cards[0].Height : pictureHeight + 4f + Mathf.Ceil(15f * 1.25f * 2f);
        return HeadingHeight + used * card + (used - 1) * Gap;
    }

    /// <summary>Shows <paramref name="title"/> with the kit icon <paramref name="icon"/> over the cards, in at most <paramref name="rows"/>
    /// rows (the last card then says how many more); hidden when there are none. Returns the height it takes (0 when hidden).</summary>
    public float Show(string title, string icon, Color tint, IReadOnlyList<KitCardFace> faces, int rows = 1)
    {
        if (faces == null || faces.Count == 0)
        {
            Hide();
            return 0f;
        }

        root.gameObject.SetActive(true);
        heading.text = (icon != null ? HearthKit.InlineIcon(icon, tint) + "  " : "") + title;
        float width = Mathf.Max(cardWidth, root.rect.width);
        int perRow = Mathf.Max(1, Mathf.FloorToInt((width + Gap) / (cardWidth + Gap)));
        int room = perRow * Mathf.Max(1, rows);
        // Past the room, the last card counts the rest (as KitHaul does): the row never grows past what the window has.
        int shown = faces.Count <= room ? faces.Count : room;
        int rest = faces.Count - (faces.Count <= room ? faces.Count : room - 1);
        while (cards.Count < shown)
        {
            cards.Add(new KitPictureCard(root, "Card " + cards.Count, cardWidth, pictureHeight, 2, 15f, null, null));
        }

        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < shown;
            cards[i].SetActive(used);
            if (!used)
            {
                continue;
            }

            bool more = faces.Count > room && i == shown - 1;
            KitCardFace face = more ? new KitCardFace { Name = $"{rest} more", Icon = icon } : faces[i];
            cards[i].ShowPicture(face.Picture, face.Icon);
            cards[i].SetName(face.Name);
            cards[i].SetBadge(face.Badge);
            cards[i].SetLook(false, face.Rim);
        }

        return HeadingHeight + KitPictureCard.Lay(cards, shown, 0f, HeadingHeight, width, Gap);
    }
}
