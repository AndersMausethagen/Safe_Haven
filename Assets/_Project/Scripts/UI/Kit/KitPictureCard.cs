using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A picture with its name under it (Hearthline 3): a painting cropped to fill the frame (an event, a place) or a picture laid
/// whole on the card (a creature's portrait, an item's icon), gold-rimmed, lit when pointed at, and what it is about on hover
/// through the window's tooltip.
/// <para>
/// The rule it is for (the user, 2026-11-21: "UI windows should normally not have a wall of text, unless it makes logical
/// sense"): a window shows things as pictures with names, and the words about them come up on hover.
/// </para>
/// </summary>
public sealed class KitPictureCard
{
    private const float NameGap = 4f;

    private readonly RectTransform root;
    private readonly RectTransform frame;
    private readonly RawImage painting;
    private readonly Image picture;
    private readonly Image rim;
    private readonly Image badge;
    private readonly TMP_Text badgeLabel;
    private readonly TMP_Text label;
    private readonly float width;
    private readonly float pictureHeight;
    private Color pictureTint = Color.white;

    public RectTransform Root => root;
    public float Width => width;
    /// <summary>The card's full height: the picture and the name under it.</summary>
    public float Height { get; }

    /// <param name="nameLines">How many lines the name may take under the picture (longer names end in "…").</param>
    /// <param name="entered">What to show while the pointer is over it (the window's tooltip); <paramref name="exited"/> hides it.</param>
    public KitPictureCard(Transform parent, string name, float width, float pictureHeight, int nameLines, float nameSize, Action entered, Action exited)
    {
        UIStyle style = UIStyle.Current;
        this.width = width;
        this.pictureHeight = pictureHeight;
        float nameHeight = nameLines > 0 ? Mathf.Ceil(nameSize * 1.25f * nameLines) : 0f;
        Height = pictureHeight + (nameLines > 0 ? NameGap + nameHeight : 0f);

        root = CodeUI.At(name, parent, 0f, 0f, width, Height);
        // Something for the pointer to land on, over the picture and its name alike.
        root.gameObject.AddComponent<Image>().color = Color.clear;
        var hover = root.gameObject.AddComponent<UIHover>();
        hover.Entered = entered;
        hover.Exited = exited;

        frame = HearthKit.Card(root, "Frame", 0f, 0f, width, pictureHeight, 0.4f);
        UIHighlight.On(frame.gameObject);
        painting = CodeUI.At("Painting", frame, 3f, 3f, width - 6f, pictureHeight - 6f).gameObject.AddComponent<RawImage>();
        painting.raycastTarget = false;
        painting.enabled = false;
        picture = CodeUI.At("Picture", frame, 6f, 6f, width - 12f, pictureHeight - 12f).gameObject.AddComponent<Image>();
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        picture.enabled = false;
        // The rim drawn last, over the painting's edges.
        rim = frame.Find("Rim").GetComponent<Image>();
        rim.transform.SetAsLastSibling();

        // A small count or mark in the picture's lower right corner (×6), when there is one.
        RectTransform badgeRect = CodeUI.At("Badge", frame, width - 44f, pictureHeight - 24f, 40f, 20f);
        badge = HearthKit.Sliced(badgeRect.gameObject.AddComponent<Image>(), style.Card, 2f);
        badge.color = HearthKit.Faded(style.Panel, 0.9f);
        badge.raycastTarget = false;
        badgeLabel = CodeUI.Label("Count", badgeRect, 0f, 0f, 40f, 20f, 14f, style.AccentBright, TextAlignmentOptions.Center);
        badgeLabel.fontStyle = FontStyles.Bold;
        // A count is never cut short: the plate grows to it (SetBadge).
        badgeLabel.overflowMode = TextOverflowModes.Overflow;
        badge.gameObject.SetActive(false);

        label = CodeUI.Paragraph("Name", root, 0f, pictureHeight + NameGap, width, nameHeight, nameSize, style.Text);
        label.alignment = TextAlignmentOptions.Top;
        label.maxVisibleLines = Mathf.Max(1, nameLines);
        label.gameObject.SetActive(nameLines > 0);
    }

    /// <summary>A painting, cropped to fill the frame from its middle (an event's scene, a place).</summary>
    public void ShowPainting(Sprite art)
    {
        picture.enabled = false;
        painting.enabled = art != null;
        if (art == null)
        {
            return;
        }

        Texture texture = art.texture;
        Rect rect = art.textureRect;
        float frameAspect = (width - 6f) / Mathf.Max(1f, pictureHeight - 6f);
        float artAspect = rect.width / Mathf.Max(1f, rect.height);
        // Cover: the side that is too long is cut evenly at both ends.
        Rect uv = artAspect > frameAspect
            ? new Rect(rect.x + rect.width * (1f - frameAspect / artAspect) * 0.5f, rect.y, rect.width * frameAspect / artAspect, rect.height)
            : new Rect(rect.x, rect.y + rect.height * (1f - artAspect / frameAspect) * 0.5f, rect.width, rect.height * artAspect / frameAspect);
        painting.texture = texture;
        painting.uvRect = new Rect(uv.x / texture.width, uv.y / texture.height, uv.width / texture.width, uv.height / texture.height);
        painting.color = Color.white;
    }

    /// <summary>A picture laid whole on the card (a portrait, an item's icon), or a kit icon when there is no picture.</summary>
    public void ShowPicture(Sprite sprite, string fallbackIcon = null, Color? iconTint = null)
    {
        painting.enabled = false;
        Sprite shown = sprite != null ? sprite : fallbackIcon != null ? UIStyle.Current.Icon(fallbackIcon) : null;
        picture.sprite = shown;
        picture.enabled = shown != null;
        pictureTint = sprite != null ? Color.white : iconTint ?? HearthKit.IconColour(fallbackIcon);
        picture.color = pictureTint;
        // A kit icon is a sign, not a picture: smaller, in the middle.
        float inset = sprite != null ? 6f : pictureHeight * 0.24f;
        picture.rectTransform.anchoredPosition = new Vector2(inset, -inset);
        picture.rectTransform.sizeDelta = new Vector2(width - inset * 2f, pictureHeight - inset * 2f);
    }

    public void SetName(string text) => label.text = text;

    /// <summary>A count or mark in the picture's corner; null or empty hides it.</summary>
    public void SetBadge(string text)
    {
        badge.gameObject.SetActive(!string.IsNullOrEmpty(text));
        badgeLabel.text = text;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Sized to its words (hidden text measures nothing: then the plate keeps its last size).
        float words = badgeLabel.GetPreferredValues(text).x;
        float plate = words > 0f ? Mathf.Clamp(Mathf.Ceil(words) + 12f, 24f, width - 8f) : badge.rectTransform.sizeDelta.x;
        badge.rectTransform.sizeDelta = new Vector2(plate, 20f);
        badge.rectTransform.anchoredPosition = new Vector2(width - 4f - plate, badge.rectTransform.anchoredPosition.y);
        badgeLabel.rectTransform.sizeDelta = new Vector2(plate, 20f);
    }

    /// <summary>
    /// Greyed (something not yet met or not yet open), as the kit greys what isn't available (HearthKit.GreyTint), or in full colour;
    /// <paramref name="rimColour"/> tints the rim (a rarity, a harm), null for the kit's gold.
    /// </summary>
    public void SetLook(bool dim, Color? rimColour = null)
    {
        UIStyle style = UIStyle.Current;
        painting.color = dim ? Dimmed : Color.white;
        picture.color = dim ? pictureTint * Dimmed : pictureTint;
        rim.color = HearthKit.Faded(rimColour ?? style.Accent, dim ? 0.2f : rimColour.HasValue ? 0.8f : 0.4f);
        label.color = dim ? style.Muted : style.Text;
    }

    // A picture not yet open: darker and nearly without colour, as the kit's greyed faces.
    private static readonly Color Dimmed = new Color(0.66f, 0.64f, 0.62f, 1f);

    public void SetActive(bool shown) => root.gameObject.SetActive(shown);

    /// <summary>Moves it to a place in its parent, from the parent's top left.</summary>
    public void Place(float x, float y) => root.anchoredPosition = new Vector2(x, -y);

    /// <summary>
    /// Lays <paramref name="count"/> cards out in rows across <paramref name="width"/>, from <paramref name="y"/> down, with
    /// <paramref name="gap"/> between them. Returns the height the rows take.
    /// </summary>
    public static float Lay(System.Collections.Generic.IReadOnlyList<KitPictureCard> cards, int count, float x, float y, float width, float gap)
    {
        if (count <= 0 || cards.Count == 0)
        {
            return 0f;
        }

        float step = cards[0].Width + gap;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((width + gap) / step));
        int rows = (count + perRow - 1) / perRow;
        for (int i = 0; i < count && i < cards.Count; i++)
        {
            cards[i].Place(x + i % perRow * step, y + i / perRow * (cards[0].Height + gap));
        }

        return rows * cards[0].Height + (rows - 1) * gap;
    }
}
