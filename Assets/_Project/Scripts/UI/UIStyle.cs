using TMPro;
using UnityEngine;

/// <summary>
/// The look of all the game's UI, in one place (Resources/UIStyle, Systems Editor › Settings › UI Style): Hearthline, taken
/// from the main menu. The HUD is type and thin lines, held up by a soft shadow. Carved marks carry the same identity
/// from the hearth to the colder raid regions (GDD §18.7). Windows stay solid over bright grass and dark forest alike.
/// <para>
/// Prefabs and scenes carry a UIRole, which colours them from here when they wake; code-built UI asks for the colours
/// directly. So changing a colour here changes the game, with nothing to re-run. Safe Haven › Setup › UI › Apply Hearthline puts the
/// roles on new UI prefabs.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "UIStyle", menuName = "Safe Haven/Settings/UI Style")]
public sealed class UIStyle : GameSettings<UIStyle>
{
    [Header("Type")]
    [Tooltip("Everything read at a glance: labels, values, lists.")]
    [SerializeField] private TMP_FontAsset bodyFont;
    [Tooltip("Titles and the big numbers.")]
    [SerializeField] private TMP_FontAsset titleFont;
    [Tooltip("Body Font with a soft shadow, for text directly on the world.")]
    [SerializeField] private Material bodyOnWorld;
    [Tooltip("Title Font with a soft shadow, for text directly on the world.")]
    [SerializeField] private Material titleOnWorld;

    [Header("Surfaces")]
    [SerializeField] private Color panel = new Color(0.075f, 0.066f, 0.055f, 1f);
    [Tooltip("The thin line around a window.")]
    [SerializeField] private Color rim = new Color(0.85f, 0.65f, 0.35f, 0.5f);
    [Tooltip("Thin lines inside a window and around a slot.")]
    [SerializeField] private Color hairline = new Color(0.925f, 0.894f, 0.827f, 0.09f);
    [Tooltip("A sunken area inside a window: a list, an empty slot. Keep window colours solid: this project blends in Linear, where a faint overlay on a dark panel comes out far brighter than its alpha.")]
    [SerializeField] private Color inset = new Color(0.112f, 0.1f, 0.085f, 1f);
    [Tooltip("A slot with something in it.")]
    [SerializeField] private Color filled = new Color(0.15f, 0.133f, 0.11f, 1f);
    [Tooltip("An ordinary button at rest. Pointing at it lifts it to Control Lit.")]
    [SerializeField] private Color control = new Color(0.165f, 0.145f, 0.118f, 1f);
    [SerializeField] private Color controlLit = new Color(0.25f, 0.215f, 0.17f, 1f);
    [Tooltip("A button or tab that is switched on.")]
    [SerializeField] private Color controlOn = new Color(0.42f, 0.32f, 0.17f, 1f);
    [Tooltip("The empty part of a bar.")]
    [SerializeField] private Color track = new Color(0.925f, 0.894f, 0.827f, 0.24f);

    [Header("Text")]
    [SerializeField] private Color text = new Color(0.925f, 0.894f, 0.827f, 1f);
    [SerializeField] private Color muted = new Color(0.66f, 0.62f, 0.555f, 1f);
    [Tooltip("The one accent: headings, the thing to act on, a marker.")]
    [SerializeField] private Color accent = new Color(0.85f, 0.655f, 0.353f, 1f);
    [Tooltip("Text on an Accent button.")]
    [SerializeField] private Color onAccent = new Color(0.075f, 0.066f, 0.055f, 1f);
    [Tooltip("A colder decorative accent on raids. Action and warning colours do not change.")]
    [SerializeField] private Color raidAccent = new Color(0.49f, 0.75f, 0.77f, 1f);

    [Header("Carved artwork")]
    [SerializeField] private Sprite hearthmark;
    [SerializeField] private Sprite carvedCorner;
    [SerializeField] private Sprite exitMark;
    [SerializeField] private Sprite questMark;
    [SerializeField] private Sprite companionMark;
    [SerializeField] private Sprite eventMark;
    [SerializeField] private Sprite rumourMark;

    [Header("Signals (never the only sign: a word or a value goes with them)")]
    [SerializeField] private Color good = new Color(0.525f, 0.627f, 0.435f, 1f);
    [SerializeField] private Color warning = new Color(0.91f, 0.62f, 0.27f, 1f);
    [SerializeField] private Color danger = new Color(0.79f, 0.333f, 0.247f, 1f);
    [Tooltip("How much water a waterskin holds: in its slot and its tooltip.")]
    [SerializeField] private Color water = new Color(0.5f, 0.77f, 0.91f, 1f);

    [Header("Hearthline 2")]
    [Tooltip("The amber's lit end, for highlights and glows.")]
    [SerializeField] private Color accentBright = new Color(0.98f, 0.82f, 0.52f, 1f);
    [Tooltip("The amber's deep end, for rules and quiet marks.")]
    [SerializeField] private Color accentDeep = new Color(0.55f, 0.38f, 0.17f, 1f);
    [Tooltip("Information that isn't a call to act: hints, comparisons that are only facts.")]
    [SerializeField] private Color info = new Color(0.58f, 0.74f, 0.86f, 1f);
    [Tooltip("The third step of text: what can't be used now.")]
    [SerializeField] private Color faint = new Color(0.46f, 0.43f, 0.39f, 1f);
    [Tooltip("Window titles: Cinzel, carved capitals.")]
    [SerializeField] private TMP_FontAsset displayFont;
    [Tooltip("The display font's material: a soft shadow under the letters.")]
    [SerializeField] private Material displayMaterial;
    [Tooltip("Shaders/UI/HearthlinePanel: a window's surface (lift, inner shadow, grain).")]
    [SerializeField] private Material panelMaterial;
    [Tooltip("The rule under a window's title.")]
    [SerializeField] private Sprite divider;
    [Tooltip("A soft round glow: behind the one button to press, under lit item icons.")]
    [SerializeField] private Sprite glow;
    [Tooltip("The pointer.")]
    [SerializeField] private Texture2D cursor;

    [Header("Hearthline 3 (the kit: Tools/UI/hearthline_kit_art.py, Safe Haven › Setup › UI › Install Hearthline Kit Art)")]
    [Tooltip("The gilded scrollwork for a window's corners (top left; mirrored for the others).")]
    [SerializeField] private Sprite scrollCorner;
    [Tooltip("A rounded card, 9-sliced: cards, tabs, bars, chips, buttons.")]
    [SerializeField] private Sprite card;
    [Tooltip("The same card's rim alone, 9-sliced.")]
    [SerializeField] private Sprite cardRim;
    [Tooltip("White glyphs, tinted where they are used: Heart, Bolt, Weight, Star, Gear and the rest, by name.")]
    [SerializeField] private Sprite[] icons = System.Array.Empty<Sprite>();
    [Tooltip("A window's ground, tiled faint under its contents.")]
    [SerializeField] private Sprite pattern;
    [Tooltip("Dark round a window's edges, clear in the middle.")]
    [SerializeField] private Sprite vignette;
    [Tooltip("A control's hover light (UIHighlight), 9-sliced.")]
    [SerializeField] private Sprite sheen;
    [Tooltip("The bars' material (Safe Haven/UI Kit Bar): gradient, shine, glow. KitBarFill makes one of its own from it.")]
    [SerializeField] private Material barMaterial;

    [Header("Motion")]
    [Tooltip("Seconds a window takes to fade in when it opens. 0: it appears at once. Closing is always immediate.")]
    [SerializeField, Range(0f, 0.5f)] private float windowFadeSeconds = 0.12f;

    public TMP_FontAsset BodyFont => bodyFont;
    public TMP_FontAsset TitleFont => titleFont;
    public Color Panel => panel;
    public Color Rim => rim;
    public Color Hairline => hairline;
    public Color Inset => inset;
    public Color Filled => filled;
    public Color Control => control;
    public Color ControlLit => controlLit;
    public Color ControlOn => controlOn;
    public Color Track => track;
    public Color Text => text;
    public Color Muted => muted;
    public Color Accent => accent;
    public Color OnAccent => onAccent;
    public Color RaidAccent => raidAccent;
    public Color Good => good;
    public Color Warning => warning;
    public Color Danger => danger;
    public Color Water => water;
    public Color AccentBright => accentBright;
    public Color AccentDeep => accentDeep;
    public Color Info => info;
    public Color Faint => faint;
    public TMP_FontAsset DisplayFont => displayFont;
    public Material DisplayMaterial => displayMaterial;
    public Material PanelMaterial => panelMaterial;
    public Sprite Divider => divider;
    public Sprite ScrollCorner => scrollCorner;
    public Sprite Card => card;
    public Sprite CardRim => cardRim;
    public Material BarMaterial => barMaterial;
    public Sprite Pattern => pattern;
    public Sprite Vignette => vignette;
    public Sprite Sheen => sheen;

    /// <summary>The kit icon of this name (a white glyph), or null.</summary>
    public Sprite Icon(string name)
    {
        if (string.IsNullOrEmpty(name) || icons == null)
        {
            return null;
        }

        foreach (Sprite icon in icons)
        {
            if (icon != null && icon.name == name)
            {
                return icon;
            }
        }

        return null;
    }
    public Sprite Glow => glow;
    public Texture2D Cursor => cursor;
    public string InfoHex => infoHex ??= Hex(info);
    public string FaintHex => faintHex ??= Hex(faint);
    public float WindowFadeSeconds => windowFadeSeconds;
    public Sprite Hearthmark => hearthmark;
    public Sprite CarvedCorner => carvedCorner;
    public Sprite ExitMark => exitMark;
    public Sprite QuestMark => questMark;
    public Sprite CompanionMark => companionMark;
    public Sprite EventMark => eventMark;
    public Sprite RumourMark => rumourMark;

    /// <summary>Only decorative ink changes with the place, not the colours of actions or warnings.</summary>
    public Color AccentFor(bool onRaid) => onRaid ? raidAccent : accent;

    public Color RimFor(bool onRaid)
    {
        Color colour = onRaid ? raidAccent : rim;
        colour.a = rim.a;
        return colour;
    }

    /// <summary>A colour for rich text, with its #.</summary>
    public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

    // The tokens text built in code colours itself with, as rich-text colours ("#C9553F"); made once, again after an edit.
    private string textHex, mutedHex, accentHex, goodHex, warningHex, dangerHex, waterHex;

    public string TextHex => textHex ??= Hex(text);
    public string MutedHex => mutedHex ??= Hex(muted);
    public string AccentHex => accentHex ??= Hex(accent);
    public string GoodHex => goodHex ??= Hex(good);
    public string WarningHex => warningHex ??= Hex(warning);
    public string DangerHex => dangerHex ??= Hex(danger);
    public string WaterHex => waterHex ??= Hex(water);
    private string infoHex, faintHex;

    private void OnValidate() => textHex = mutedHex = accentHex = goodHex = warningHex = dangerHex = waterHex = null;

    /// <summary>Sets up text that sits directly on the world, with the shadow that keeps it readable there.</summary>
    public void OnWorld(TMP_Text label, bool title = false)
    {
        TMP_FontAsset font = title ? titleFont : bodyFont;
        if (font != null)
        {
            label.font = font;
        }

        Material shadow = title ? titleOnWorld : bodyOnWorld;
        if (shadow != null)
        {
            label.fontSharedMaterial = shadow;
        }
    }

    /// <summary>Sets up a serif title (a name, a heading inside a window). A window's own title is carved in the display
    /// face by its UIRole instead (Display).</summary>
    public void Title(TMP_Text label)
    {
        if (titleFont != null)
        {
            label.font = titleFont;
        }

        label.color = text;
    }

    /// <summary>
    /// A window's title in the display face: carved capitals spaced a little apart, a soft shadow under them, and a fall of
    /// light from the text colour at the top to a touch of amber at the foot.
    /// </summary>
    public void Display(TMP_Text label)
    {
        if (displayFont == null || label == null)
        {
            return;
        }

        label.font = displayFont;
        if (displayMaterial != null)
        {
            label.fontSharedMaterial = displayMaterial;
        }

        label.characterSpacing = 5f;
        label.color = Color.white;
        label.enableVertexGradient = true;
        Color foot = Color.Lerp(text, accentBright, 0.45f);
        label.colorGradient = new VertexGradient(text, text, foot, foot);
    }
}
