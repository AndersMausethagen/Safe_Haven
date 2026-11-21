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

    /// <summary>Shows <paramref name="title"/> with the kit icon <paramref name="icon"/> over the cards; hidden when there are none.
    /// Returns the height it takes (0 when hidden).</summary>
    public float Show(string title, string icon, Color tint, IReadOnlyList<KitCardFace> faces)
    {
        if (faces == null || faces.Count == 0)
        {
            Hide();
            return 0f;
        }

        root.gameObject.SetActive(true);
        heading.text = (icon != null ? HearthKit.InlineIcon(icon, tint) + "  " : "") + title;
        while (cards.Count < faces.Count)
        {
            cards.Add(new KitPictureCard(root, "Card " + cards.Count, cardWidth, pictureHeight, 2, 15f, null, null));
        }

        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < faces.Count;
            cards[i].SetActive(used);
            if (!used)
            {
                continue;
            }

            KitCardFace face = faces[i];
            cards[i].ShowPicture(face.Picture, face.Icon);
            cards[i].SetName(face.Name);
            cards[i].SetBadge(face.Badge);
            cards[i].SetLook(false, face.Rim);
        }

        float width = Mathf.Max(cardWidth, root.rect.width);
        return HeadingHeight + KitPictureCard.Lay(cards, faces.Count, 0f, HeadingHeight, width, Gap);
    }
}
