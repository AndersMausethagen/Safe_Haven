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

    /// <summary>Sets up a window's title.</summary>
    public void Title(TMP_Text label)
    {
        if (titleFont != null)
        {
            label.font = titleFont;
        }

        label.color = text;
    }
}
