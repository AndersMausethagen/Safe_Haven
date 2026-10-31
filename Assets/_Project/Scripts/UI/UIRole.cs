using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What a piece of UI is, so its colours come from UIStyle instead of being set on each prefab. Colours it once when it wakes;
/// scripts can tint it after that. Put on by Safe Haven › UI › Apply Hearthline.
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

    public Kind Role => kind;

    /// <summary>Sets the role, from the tool.</summary>
    public void Set(Kind role)
    {
        kind = role;
        Apply();
    }

    private void Awake() => Apply();

    /// <summary>Colours this from the current style.</summary>
    public void Apply()
    {
        UIStyle style = UIStyle.Current;
        var image = GetComponent<Image>();
        var line = GetComponent<Outline>();
        switch (kind)
        {
            case Kind.Window:
                Paint(image, style.Panel);
                Paint(line, style.Rim);
                break;
            case Kind.Inset:
                Paint(image, style.Inset);
                break;
            case Kind.Slot:
                Paint(image, style.Inset);
                Paint(line, style.Hairline);
                break;
            case Kind.Button:
                PaintButton(image, style.ControlLit, style.Control, style.Text);
                break;
            case Kind.PrimaryButton:
                PaintButton(image, style.Accent, style.Accent * new Color(0.85f, 0.85f, 0.85f, 1f), style.OnAccent);
                break;
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
    private void PaintButton(Image image, Color lit, Color rest, Color label)
    {
        Paint(image, lit);
        if (TryGetComponent(out Button button))
        {
            ColorBlock colors = button.colors;
            float share = lit.grayscale > 0f ? Mathf.Clamp01(rest.grayscale / lit.grayscale) : 1f;
            colors.normalColor = new Color(share, share, share, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(share, share, share, 0.45f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = label;
        }
    }
}
