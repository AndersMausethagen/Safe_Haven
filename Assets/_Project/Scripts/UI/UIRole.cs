using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What a piece of UI is, so its colours come from UIStyle instead of being set on each prefab. Colours it once when it wakes;
/// scripts can tint it after that. Put on by Safe Haven › Setup › UI › Apply Hearthline.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIRole : MonoBehaviour
{
    public enum Kind
    {
        /// <summary>A window's background, with its thin rim.</summary>
        Window = 0,
        /// <summary>A sunken area inside a window: a list or a row.</summary>
        Inset = 1,
        /// <summary>An item or skill slot, with a thin line around it.</summary>
        Slot = 2,
        /// <summary>An ordinary button.</summary>
        Button = 3,
        /// <summary>The one button to press: craft, enter, accept.</summary>
        PrimaryButton = 4,
    }

    [SerializeField] private Kind kind;

    private Button button;
    private Color labelColor;
    private bool labelsDimmed;
    private bool started;
    private GameObject frame;

    public Kind Role => kind;

    /// <summary>Sets the role, from the tool.</summary>
    public void Set(Kind role)
    {
        kind = role;
        Apply();
        if (started)
        {
            RefreshFrame();
        }
    }

    private void Awake()
    {
        // Only buttons have anything to watch; PaintButton switches them back on.
        enabled = false;
        Apply();
    }

    private void Start()
    {
        started = true;
        RefreshFrame();
        if (button == null)
        {
            enabled = false;
        }
    }

    // Selectable's tint reaches only the button's own graphic, so a disabled button's label is dimmed here. Without it
    // a dark button that can't be pressed looks just like one that can.
    private void LateUpdate()
    {
        bool dim = !button.IsInteractable();
        if (dim != labelsDimmed)
        {
            labelsDimmed = dim;
            PaintLabels(dim ? UIStyle.Current.Muted : labelColor);
        }
    }

    /// <summary>Colours this from the current style.</summary>
    public void Apply()
    {
        UIStyle style = UIStyle.Current;
        if (Application.isPlaying)
        {
            enabled = false;
            button = null;
        }

        var image = GetComponent<Image>();
        var line = GetComponent<Outline>();
        switch (kind)
        {
            case Kind.Window:
                Paint(image, style.Panel);
                Paint(line, style.RimFor(RaidDirector.Current != null));
                if (Application.isPlaying && !started)
                {
                    enabled = true;
                }
                break;
            case Kind.Inset:
                Paint(image, style.Inset);
                break;
            case Kind.Slot:
                Paint(image, style.Inset);
                Paint(line, style.Hairline);
                break;
            case Kind.Button:
                PaintButton(image, style.ControlLit, style.Control, style.Text, null);
                break;
            case Kind.PrimaryButton:
                PaintButton(image, style.Accent, style.Accent * new Color(0.85f, 0.85f, 0.85f, 1f), style.OnAccent, style.Control);
                break;
        }
    }

    private void RefreshFrame()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (kind == Kind.Window && transform is RectTransform panel)
        {
            UIStyle style = UIStyle.Current;
            bool onRaid = RaidDirector.Current != null;
            frame = HearthlineArt.AddFrame(panel, style, onRaid);
            Paint(GetComponent<Outline>(), style.RimFor(onRaid));
        }
        else if (frame != null)
        {
            frame.SetActive(false);
        }
    }

    private static void Paint(Graphic graphic, Color color)
    {
        if (graphic != null)
        {
            graphic.color = color;
        }
    }

    private static void Paint(Shadow line, Color color)
    {
        if (line != null)
        {
            line.effectColor = color;
        }
    }

    // A button's graphic is its lit colour, and Selectable's tint darkens it at rest, as a tint can only darken.
    // Disabled, a button fades into the panel, or sinks to the given colour: a half-faded amber still calls to be pressed.
    private void PaintButton(Image image, Color lit, Color rest, Color label, Color? disabled)
    {
        Paint(image, lit);
        if (TryGetComponent(out button))
        {
            ColorBlock colors = button.colors;
            float share = lit.grayscale > 0f ? Mathf.Clamp01(rest.grayscale / lit.grayscale) : 1f;
            colors.normalColor = new Color(share, share, share, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            float sunk = disabled.HasValue && lit.grayscale > 0f ? Mathf.Clamp01(disabled.Value.grayscale / lit.grayscale) : share;
            colors.disabledColor = new Color(sunk, sunk, sunk, disabled.HasValue ? 1f : 0.45f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            enabled = true;
        }

        labelColor = label;
        labelsDimmed = false;
        PaintLabels(label);
    }

    private void PaintLabels(Color color)
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = color;
        }
    }
}
