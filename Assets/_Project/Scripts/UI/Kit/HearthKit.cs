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

    // A number standing alone: 16, 2.5, 40%, x1.5 (or with the times sign), 3/8, 12:30; not inside a word.
    private static readonly System.Text.RegularExpressions.Regex Numbers = new System.Text.RegularExpressions.Regex(
        @"(?<![\p{L}\d#])(?:\u00d7|x)?\d+(?:[.,:]\d+)?(?:\s?/\s?\d+(?:[.,]\d+)?)?%?(?![\p{L}\d])",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// The numbers in a line made to stand out (Hearthline 3: "more colour on the important words and numbers"): each
    /// lone number gold and bold. Text already coloured (a cost in red, a bonus in green) is left as it is, and so are
    /// tags; safe to run twice.
    /// </summary>
    public static string ToneNumbers(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string gold = ColorUtility.ToHtmlStringRGB(UIStyle.Current.AccentBright);
        var result = new System.Text.StringBuilder(text.Length + 32);
        int coloured = 0;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                int end = text.IndexOf('>', i);
                if (end < 0)
                {
                    result.Append(text, i, text.Length - i);
                    break;
                }

                string tag = text.Substring(i, end - i + 1);
                if (tag.StartsWith("<color", StringComparison.OrdinalIgnoreCase)) coloured++;
                else if (tag.StartsWith("</color", StringComparison.OrdinalIgnoreCase)) coloured = Mathf.Max(0, coloured - 1);
                result.Append(tag);
                i = end + 1;
                continue;
            }

            int next = text.IndexOf('<', i);
            string plain = next < 0 ? text.Substring(i) : text.Substring(i, next - i);
            result.Append(coloured > 0 ? plain : Numbers.Replace(plain, match => $"<color=#{gold}><b>{match.Value}</b></color>"));
            i = next < 0 ? text.Length : next;
        }

        return result.ToString();
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

    // Every window's headings and the icon each carries, in one place so the same words always get the same picture.
    private static readonly System.Collections.Generic.Dictionary<string, string> HeadingIcons =
        new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Bag", "Bags" }, { "Your bag", "Bags" }, { "Cargo", "Bags" }, { "Likely finds", "Bags" }, { "Safe Pocket", "Lock" },
            { "Essences", "Spark" }, { "What essence buys", "Spark" }, { "Crafting", "Hammer" }, { "The queue", "Hourglass" },
            { "Needs", "Wheat" }, { "Cost", "Coin" }, { "Size", "Weight" }, { "Gear", "Sword" }, { "Companion", "Heart" },
            { "What next", "Star" }, { "Renown", "Star" }, { "Milestones", "Star" }, { "Levels", "Stats" }, { "Expedition", "Lead" },
            { "Raid modifiers", "Crit" }, { "Difficulty", "Crit" }, { "Known dangers", "Skull" }, { "This run", "Hourglass" },
            { "Extra time", "Hourglass" }, { "What it is", "Info" }, { "How it works", "Gear" }, { "Gathering", "Axe" },
            { "Combat", "Sword" }, { "Stealth", "Eye" }, { "Riding", "HorseHead" }, { "Skill Bar", "Spark" }, { "Known Skills", "Book" },
            { "Attributes", "Star" }, { "Offense", "Sword" }, { "Resistances", "Barding" },
        };

    // What an action's first word suggests, for menus whose actions don't name an icon (ActionRowView).
    private static readonly (string word, string icon)[] ActionIcons =
    {
        ("enter", "Lead"), ("travel", "Lead"), ("go", "Lead"), ("leave", "Lead"), ("follow", "Lead"), ("lead", "Lead"),
        ("repair", "Hammer"), ("build", "Hammer"), ("fix", "Hammer"), ("craft", "Hammer"), ("make", "Hammer"), ("upgrade", "Star"),
        ("trade", "Coin"), ("buy", "Coin"), ("sell", "Coin"), ("pay", "Coin"), ("offer", "Coin"),
        ("listen", "Book"), ("talk", "Book"), ("ask", "Book"), ("read", "Book"), ("learn", "Book"), ("study", "Book"),
        ("purify", "Sun"), ("bless", "Sun"), ("pray", "Sun"), ("light", "Flame"), ("burn", "Flame"),
        ("rest", "Hourglass"), ("sleep", "Hourglass"), ("wait", "Hourglass"),
        ("open", "Bags"), ("store", "Bags"), ("take", "Bags"), ("put", "Bags"), ("loot", "Bags"), ("load", "Bags"), ("unload", "Bags"),
        ("ride", "HorseHead"), ("mount", "HorseHead"), ("stable", "Stable"), ("tie", "Stay"), ("untie", "Stay"), ("stay", "Stay"),
        ("feed", "Wheat"), ("eat", "Wheat"), ("cook", "Wheat"), ("harvest", "Wheat"), ("plant", "Graze"),
        ("fight", "Sword"), ("attack", "Sword"), ("challenge", "Sword"), ("hunt", "Sword"),
        ("hitch", "Wheel"), ("unhitch", "Wheel"), ("push", "Wheel"), ("pull", "Wheel"),
        ("rename", "Pencil"), ("name", "Pencil"), ("give", "Heart"), ("heal", "Heart"), ("mend", "Heart"),
        ("look", "Eye"), ("search", "Eye"), ("inspect", "Eye"), ("claim", "Star"), ("raise", "Star"),
    };

    /// <summary>The icon an action's label suggests by its first word ("Repair a stone": Hammer), or null.</summary>
    public static string IconForAction(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        string text = label.TrimStart();
        int end = 0;
        while (end < text.Length && char.IsLetter(text[end]))
        {
            end++;
        }

        string first = text.Substring(0, end);
        foreach ((string word, string icon) in ActionIcons)
        {
            if (string.Equals(first, word, StringComparison.OrdinalIgnoreCase))
            {
                return UIStyle.Current.Icon(icon) != null ? icon : null;
            }
        }

        return null;
    }

    /// <summary>The icon a heading with these words carries, or null.</summary>
    public static string IconForHeading(string text) =>
        text != null && HeadingIcons.TryGetValue(text.Trim(), out string icon) && UIStyle.Current.Icon(icon) != null ? icon : null;

    /// <summary>Puts each heading's icon before it, for every label under <paramref name="root"/> that says one of the
    /// headings the kit knows (IconForHeading). Labels on buttons and tabs are left alone, and each is done once.</summary>
    public static void DecorateHeadings(Transform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string icon = IconForHeading(label.text);
            if (icon != null && label.GetComponentInParent<Selectable>(true) == null && !label.name.EndsWith(SectionSuffix))
            {
                DecorateHeading(label, icon);
            }
        }
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

    private const string SectionSuffix = " Section";

    /// <summary>A section's heading: its icon, then its name in the title face, bold.</summary>
    public static TMP_Text Section(Transform parent, string text, string icon, float x, float y, float width = 400f)
    {
        UIStyle style = UIStyle.Current;
        bool hasIcon = style.Icon(icon) != null;
        if (hasIcon)
        {
            Icon(parent, icon, x, y + 3f, 24f);
        }

        // Named a section, so DecorateHeadings leaves it: its icon is beside it already.
        TMP_Text heading = CodeUI.Label(text + SectionSuffix, parent, hasIcon ? x + 34f : x, y - 2f, width, 32f, 23f, style.Text);
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
        Ground(panel);
    }

    private const string GroundName = "Hearthline Ground";

    /// <summary>A window's ground under its contents: rugged brown leather, tiled (its own colours, over the panel colour),
    /// and a vignette dark round the edges; once. Behind everything on the panel, and outside its layout.</summary>
    public static void Ground(RectTransform panel)
    {
        UIStyle style = UIStyle.Current;
        if (panel == null || style.Pattern == null || panel.Find(GroundName) != null)
        {
            return;
        }

        RectTransform ground = CodeUI.Stretch(GroundName, panel);
        ground.SetAsFirstSibling();
        ground.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var pattern = CodeUI.Stretch("Pattern", ground).gameObject.AddComponent<Image>();
        pattern.sprite = style.Pattern;
        pattern.type = Image.Type.Tiled;
        pattern.pixelsPerUnitMultiplier = 1f;
        pattern.color = new Color(1f, 1f, 1f, style.PatternStrength);
        pattern.raycastTarget = false;
        if (style.Vignette != null)
        {
            var vignette = CodeUI.Stretch("Vignette", ground).gameObject.AddComponent<Image>();
            vignette.sprite = style.Vignette;
            vignette.color = new Color(1f, 1f, 1f, 0.6f);
            vignette.raycastTarget = false;
        }
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
        UIHighlight.On(rect.gameObject);
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
