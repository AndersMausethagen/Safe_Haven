using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What came home or was lost, as pictures (Hearthline 3): a heading with its sign, then each thing as a card with its
/// picture, its rarity's rim and its count (+ green, - red), row after row up to a limit, the last card saying how many more
/// there are. Built once into a window made in code or a prefab, laid out again on every Show.
/// </summary>
public sealed class KitHaul
{
    private const float Gap = 8f;
    private const float HeadingHeight = 30f;
    private const int MostPerRow = 8;

    private readonly RectTransform root;
    private readonly TMP_Text heading;
    private readonly TMP_Text empty;
    private readonly float cardSize;
    private readonly List<(GameObject root, Image icon, Image rim, TMP_Text count, TMP_Text more)> cards =
        new List<(GameObject, Image, Image, TMP_Text, TMP_Text)>();

    /// <param name="parent">What it is laid in; it fills the parent's width from <paramref name="y"/> down.</param>
    public KitHaul(RectTransform parent, string name, float y, float cardSize)
    {
        UIStyle style = UIStyle.Current;
        this.cardSize = cardSize;
        root = CodeUI.At(name, parent, 0f, y, 0f, 0f);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(0f, 0f);
        heading = CodeUI.Label("Heading", root, 0f, 0f, 600f, HeadingHeight, 19f, style.Text);
        heading.rectTransform.anchorMax = new Vector2(1f, 1f);
        heading.rectTransform.sizeDelta = new Vector2(0f, HeadingHeight);
        style.Title(heading);
        heading.fontStyle = FontStyles.Bold;
        empty = CodeUI.Label("Empty", root, 0f, HeadingHeight, 600f, 26f, 17f, style.Muted);
        empty.rectTransform.anchorMax = new Vector2(1f, 1f);
        empty.rectTransform.sizeDelta = new Vector2(0f, 26f);
    }

    /// <summary>Where it starts, from the parent's top.</summary>
    public float Top
    {
        get => -root.anchoredPosition.y;
        set => root.anchoredPosition = new Vector2(0f, -value);
    }

    /// <summary>Hides it (it then takes no room: Show returns 0 for it).</summary>
    public void Hide() => root.gameObject.SetActive(false);

    /// <summary>
    /// Shows <paramref name="title"/> with <paramref name="icon"/> over the things, each counted with <paramref name="sign"/>
    /// in <paramref name="tint"/>, in at most <paramref name="rows"/> rows; <paramref name="none"/> when there are none.
    /// Returns the height it takes.
    /// </summary>
    public float Show(string title, string icon, Color tint, string sign, IReadOnlyList<ItemAmount> items,
        IReadOnlyList<ResourceAmount> resources, string none, int rows = 2)
    {
        UIStyle style = UIStyle.Current;
        root.gameObject.SetActive(true);
        heading.text = (icon != null ? HearthKit.InlineIcon(icon, tint) + "  " : "") + title;

        var shown = new List<(Sprite picture, Color rim, int amount)>();
        foreach (ItemAmount item in items)
        {
            if (item.Item != null && item.Amount > 0)
            {
                shown.Add((item.Item.Icon, item.Item.Rarity > ItemRarity.Common ? Rarity.ColorOf(item.Item.Rarity) : HearthKit.Faded(style.Accent, 0.45f), item.Amount));
            }
        }

        foreach (ResourceAmount resource in resources)
        {
            if (resource.Resource != null && resource.Amount > 0)
            {
                shown.Add((resource.Resource.Icon, HearthKit.Faded(style.Accent, 0.45f), resource.Amount));
            }
        }

        empty.gameObject.SetActive(shown.Count == 0);
        empty.text = none ?? "";
        if (shown.Count == 0)
        {
            HideCards(0);
            return HeadingHeight + (string.IsNullOrEmpty(none) ? 0f : 28f);
        }

        float width = Mathf.Max(cardSize, root.rect.width);
        // Never more than eight to a row (the user, 2026-11-27: "Start a new line when items on the left side are over 8").
        int perRow = Mathf.Clamp(Mathf.FloorToInt((width + Gap) / (cardSize + Gap)), 1, MostPerRow);
        int room = perRow * Mathf.Max(1, rows);
        // Past the room, the last card counts the rest.
        int cardsShown = shown.Count <= room ? shown.Count : room;
        int rest = shown.Count - (shown.Count <= room ? shown.Count : room - 1);
        for (int i = 0; i < cardsShown; i++)
        {
            if (i >= cards.Count)
            {
                cards.Add(MakeCard(i));
            }

            var card = cards[i];
            card.root.SetActive(true);
            ((RectTransform)card.root.transform).anchoredPosition = new Vector2(i % perRow * (cardSize + Gap), -(HeadingHeight + 4f + i / perRow * (cardSize + Gap)));
            bool last = rest > 0 && i == cardsShown - 1;
            card.icon.enabled = !last && shown[i].picture != null;
            card.icon.sprite = last ? null : shown[i].picture;
            card.rim.color = last ? HearthKit.Faded(style.Accent, 0.3f) : shown[i].rim;
            card.count.transform.parent.gameObject.SetActive(!last);
            card.count.text = last ? "" : $"<color=#{ColorUtility.ToHtmlStringRGB(tint)}>{sign}{Short(shown[i].amount)}</color>";
            card.more.gameObject.SetActive(last);
            card.more.text = last ? $"+{rest}\n<size=60%>more</size>" : "";
        }

        HideCards(cardsShown);
        int rowsUsed = (cardsShown + perRow - 1) / perRow;
        return HeadingHeight + 4f + rowsUsed * (cardSize + Gap);
    }

    private void HideCards(int from)
    {
        for (int i = from; i < cards.Count; i++)
        {
            cards[i].root.SetActive(false);
        }
    }

    private (GameObject, Image, Image, TMP_Text, TMP_Text) MakeCard(int index)
    {
        UIStyle style = UIStyle.Current;
        RectTransform card = HearthKit.Card(root, "Card " + index, 0f, 0f, cardSize, cardSize, 0.5f);
        Image rim = card.Find("Rim").GetComponent<Image>();
        RectTransform inner = CodeUI.Stretch("Icon", card);
        inner.offsetMin = new Vector2(cardSize * 0.12f, cardSize * 0.12f);
        inner.offsetMax = new Vector2(-cardSize * 0.12f, -cardSize * 0.12f);
        var icon = inner.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        // The count on a dark chip in the corner, over the picture.
        RectTransform chip = CodeUI.At("Count", card, cardSize * 0.3f, cardSize - 22f, cardSize * 0.7f - 3f, 19f);
        Image back = HearthKit.Sliced(chip.gameObject.AddComponent<Image>(), style.Card, 3f);
        back.color = HearthKit.Faded(style.Panel, 0.85f);
        back.raycastTarget = false;
        TMP_Text count = CodeUI.Label("Text", chip, 0f, 0f, cardSize * 0.7f - 7f, 19f, 14f, style.Text, TextAlignmentOptions.Right);
        count.fontStyle = FontStyles.Bold;
        TMP_Text more = CodeUI.Label("More", card, 0f, 0f, cardSize, cardSize, 20f, style.Accent, TextAlignmentOptions.Center);
        more.fontStyle = FontStyles.Bold;
        more.textWrappingMode = TextWrappingModes.Normal;
        rim.transform.SetAsLastSibling();
        return (card.gameObject, icon, rim, count, more);
    }

    private static string Short(int amount) => amount >= 10000 ? $"{amount / 1000}k" : amount >= 1000 ? $"{amount / 1000f:0.#}k" : amount.ToString();
}
