using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hearthline 3, the UI kit: the pieces windows built in code are made of, after the user's reference picture
/// (2026-11-13) and the horse window that first used them. A gilded window (solid panel, gold rim, scroll corners, a
/// round close button, fitted to the screen), rounded cards with a soft gold rim, section headings with an icon, icons
/// tinted by meaning. Measured like CodeUI, from the parent's top left. Art is on the UI Style (Card, CardRim,
/// ScrollCorner, Icon(name)); without it the pieces still work, square and plain. The parts with state (stat rows,
/// tabs, chips, buttons, lists, slot grids) are in KitParts.
/// </summary>
public static class HearthKit
{
    public const float CornerSize = 150f;

    public static Color Faded(Color colour, float alpha)
    {
        colour.a *= alpha;
        return colour;
    }

    /// <summary>An image drawn 9-sliced from <paramref name="sprite"/>; <paramref name="multiplier"/> shrinks its corners
    /// (more for small things: a bar, a chip).</summary>
    public static Image Sliced(Image image, Sprite sprite, float multiplier)
    {
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
        }

        return image;
    }

    /// <summary>A button's tint over its own coloured graphic: brighter pointed at, darker pressed, dim disabled.</summary>
    public static void Tint(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = new Color(1.1f, 1.08f, 1.04f, 1f);
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
        colors.colorMultiplier = 1.4f;
        button.colors = colors;
    }

    /// <summary>A rounded card: the inset colour and a soft gold rim. Its fill doesn't catch the pointer.</summary>
    public static RectTransform Card(Transform parent, string name, float x, float y, float width, float height, float rimAlpha = 0.45f,
        Color? fill = null, float corners = 1.2f)
    {
        UIStyle style = UIStyle.Current;
        RectTransform card = CodeUI.At(name, parent, x, y, width, height);
        Image surface = Sliced(card.gameObject.AddComponent<Image>(), style.Card, corners);
        surface.color = fill ?? style.Inset;
        surface.raycastTarget = false;
        Rim(card, rimAlpha, corners);
        return card;
    }

    /// <summary>A gold rim round a rect (a card, a picture frame), on top of what is in it.</summary>
    public static Image Rim(RectTransform around, float alpha, float corners = 1.2f)
    {
        UIStyle style = UIStyle.Current;
        Image rim = Sliced(CodeUI.Stretch("Rim", around).gameObject.AddComponent<Image>(), style.CardRim, corners);
        rim.color = Faded(style.Accent, alpha);
        rim.raycastTarget = false;
        return rim;
    }

    /// <summary>An icon from the kit, in the accent colour unless told otherwise; hidden when there is no such icon.</summary>
    public static Image Icon(Transform parent, string name, float x, float y, float size, Color? colour = null)
    {
        UIStyle style = UIStyle.Current;
        Image image = CodeUI.At(name + " Icon", parent, x, y, size, size).gameObject.AddComponent<Image>();
        image.sprite = style.Icon(name);
        image.color = colour ?? IconColour(name);
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = image.sprite != null;
        return image;
    }

    /// <summary>What an icon is tinted by its meaning: life red, breath yellow, fondness rose, everything else gold.</summary>
    public static Color IconColour(string icon)
    {
        UIStyle style = UIStyle.Current;
        switch (icon)
        {
            case "Heart": return Color.Lerp(style.Danger, style.Accent, 0.35f);
            case "Bolt": return Color.Lerp(style.Warning, style.Accent, 0.3f);
            case "Bond": return Color.Lerp(style.Danger, style.Accent, 0.5f);
            default: return style.Accent;
        }
    }

    /// <summary>An icon inside text (TextMesh Pro's sprite tag over the kit's icon atlas), tinted <paramref name="colour"/>
    /// (the accent by default, or by its meaning: IconColour).</summary>
    public static string InlineIcon(string name, Color? colour = null)
    {
        Color tint = colour ?? IconColour(name);
        return $"<color=#{ColorUtility.ToHtmlStringRGBA(tint)}><sprite name=\"{name}\" tint=1></color>";
    }

    /// <summary>Puts an icon before an existing label's text (a prefab window's heading), once.</summary>
    public static void DecorateHeading(TMP_Text label, string icon)
    {
        if (label == null || string.IsNullOrEmpty(label.text) || label.text.Contains("<sprite"))
        {
            return;
        }

        label.richText = true;
        label.text = InlineIcon(icon) + "  " + label.text;
    }

    /// <summary>Puts icons before a window's headings, found by their text (a prefab's labels): "Bag" gets Bags, and so
    /// on. Each once; labels that say something else are left alone.</summary>
    public static void DecorateHeadings(Transform root, params (string text, string icon)[] headings)
    {
        if (root == null)
        {
            return;
        }

        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string text = label.text != null ? label.text.Trim() : "";
            foreach ((string heading, string icon) in headings)
            {
                if (string.Equals(text, heading, StringComparison.OrdinalIgnoreCase))
                {
                    DecorateHeading(label, icon);
                    break;
                }
            }
        }
    }

    /// <summary>A section's heading: its icon, then its name in the title face, bold.</summary>
    public static TMP_Text Section(Transform parent, string text, string icon, float x, float y, float width = 400f)
    {
        UIStyle style = UIStyle.Current;
        bool hasIcon = style.Icon(icon) != null;
        if (hasIcon)
        {
            Icon(parent, icon, x, y + 3f, 24f);
        }

        TMP_Text heading = CodeUI.Label(text + " Heading", parent, hasIcon ? x + 34f : x, y - 2f, width, 32f, 23f, style.Text);
        style.Title(heading);
        heading.fontStyle = FontStyles.Bold;
        heading.text = text;
        return heading;
    }

    /// <summary>A thin gold line, for between a window's parts (a hairline is fainter).</summary>
    public static Image Rule(Transform parent, float x, float y, float width, float alpha = 0.35f)
    {
        Image line = CodeUI.At("Rule", parent, x, y, width, 1f).gameObject.AddComponent<Image>();
        line.color = Faded(UIStyle.Current.Accent, alpha);
        line.raycastTarget = false;
        return line;
    }

    /// <summary>
    /// A gilded window on <paramref name="root"/> (a screen-space canvas): the dimmed screen behind it (the window's
    /// content, which UIWindow shows and hides), and the panel itself: the panel colour and material, a gold rim, scroll
    /// corners, and a round × that calls <paramref name="close"/> (none when null). Returns the panel; <paramref name="content"/>
    /// is what to configure the UIWindow with.
    /// </summary>
    public static RectTransform Window(Transform root, float width, float height, Action close, out RectTransform content)
    {
        UIStyle style = UIStyle.Current;
        content = CodeUI.Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);
        RectTransform panel = CodeUI.Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(width, height), Vector2.zero);
        Dress(panel);
        if (close != null)
        {
            CloseButton(panel, width - 66f, 18f, close);
        }

        return panel;
    }

    /// <summary>Gives an existing panel the gilded look: panel colour and material, gold rim, scroll corners.</summary>
    public static void Dress(RectTransform panel)
    {
        UIStyle style = UIStyle.Current;
        Image surface = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
        surface.color = style.Panel;
        if (style.PanelMaterial != null && surface.sprite == null)
        {
            surface.material = style.PanelMaterial;
        }

        Outline rim = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
        rim.effectColor = Faded(style.Accent, 0.55f);
        rim.effectDistance = new Vector2(1.5f, -1.5f);
        Corners(panel);
    }

    private const string CornersName = "Scroll Corners";

    /// <summary>The four gilded scroll corners on a panel, behind its contents; once.</summary>
    public static void Corners(RectTransform panel, float size = CornerSize)
    {
        UIStyle style = UIStyle.Current;
        if (style.ScrollCorner == null || panel.Find(CornersName) != null)
        {
            return;
        }

        RectTransform holder = CodeUI.Stretch(CornersName, panel);
        holder.SetAsFirstSibling();
        holder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                RectTransform corner = CodeUI.Box("Scroll Corner", holder, new Vector2(x, y), Vector2.one * size,
                    new Vector2(x == 0 ? size * 0.5f + 6f : -size * 0.5f - 6f, y == 0 ? size * 0.5f + 6f : -size * 0.5f - 6f));
                var image = corner.gameObject.AddComponent<Image>();
                image.sprite = style.ScrollCorner;
                image.color = Faded(style.Accent, 0.9f);
                image.raycastTarget = false;
                corner.localScale = new Vector3(x == 0 ? 1f : -1f, y == 1 ? 1f : -1f, 1f);
            }
        }
    }

    /// <summary>A round × button.</summary>
    public static Button CloseButton(Transform parent, float x, float y, Action close)
    {
        UIStyle style = UIStyle.Current;
        RectTransform rect = CodeUI.At("Close", parent, x, y, 46f, 46f);
        Image fill = Sliced(rect.gameObject.AddComponent<Image>(), style.Card, 1f);
        fill.color = style.Inset;
        Rim(rect, 0.8f, 1f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        Tint(button);
        button.onClick.AddListener(() => close());
        TMP_Text cross = CodeUI.Label("X", rect, 0f, -2f, 46f, 46f, 30f, style.Text, TextAlignmentOptions.Center);
        cross.text = "×";
        return button;
    }

    /// <summary>Shrinks <paramref name="panel"/> (whole, in proportion) where its canvas leaves too little room for it.</summary>
    public static void FitToScreen(RectTransform panel, RectTransform canvas, float width, float height, float margin = 40f)
    {
        if (panel == null || canvas == null)
        {
            return;
        }

        Rect room = canvas.rect;
        float scale = Mathf.Min(1f, (room.height - margin) / height, (room.width - margin) / width);
        if (scale > 0.1f)
        {
            panel.localScale = Vector3.one * scale;
        }
    }

    /// <summary>A window's name in the display face. Overflowing, not cut: the face is taller than its size, and with an
    /// ellipsis a box shorter than its line drops the text whole.</summary>
    public static TMP_Text DisplayName(Transform parent, float x, float y, float width, float size = 52f)
    {
        UIStyle style = UIStyle.Current;
        TMP_Text label = CodeUI.Label("Name", parent, x, y, width, size * 1.8f, size, style.Text);
        style.Display(label);
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
}
