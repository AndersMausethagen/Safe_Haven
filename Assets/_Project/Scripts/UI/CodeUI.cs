using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The small parts windows built in code are put together from, measured from the parent's top left corner.</summary>
public static class CodeUI
{
    public static RectTransform At(string name, Transform parent, float x, float y, float width, float height)
    {
        RectTransform rect = Box(name, parent, new Vector2(0f, 1f), new Vector2(width, height), new Vector2(x, -y));
        rect.pivot = new Vector2(0f, 1f);
        return rect;
    }

    public static RectTransform Box(string name, Transform parent, Vector2 anchorAt, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchorAt;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        RectTransform rect = Box(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static TMP_Text Label(string name, Transform parent, float x, float y, float width, float height, float size, Color colour,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var text = At(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    /// <summary>Text that runs over several lines.</summary>
    public static TMP_Text Paragraph(string name, Transform parent, float x, float y, float width, float height, float size, Color colour)
    {
        TMP_Text text = Label(name, parent, x, y, width, height, size, colour, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    public static void Hairline(Transform parent, float x, float y, float width, float height, UIStyle style)
    {
        At("Line", parent, x, y, width, height).gameObject.AddComponent<Image>().color = style.Hairline;
    }

    /// <summary>A heading in spaced capitals, the accent colour.</summary>
    public static TMP_Text Heading(string text, Transform parent, float x, float y, float width, UIStyle style)
    {
        TMP_Text heading = Label(text + " Heading", parent, x, y, width, 26f, 17f, style.Accent);
        heading.text = text.ToUpperInvariant();
        heading.characterSpacing = 8f;
        return heading;
    }

    public static TMP_Text AddButton(string name, Transform parent, float x, float y, float width, UIRole.Kind role,
        UnityEngine.Events.UnityAction onClick, out Button button)
    {
        RectTransform rect = At(name, parent, x, y, width, 46f);
        var image = rect.gameObject.AddComponent<Image>();
        button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        TMP_Text label = Label("Label", rect, 0f, 0f, width, 46f, 20f, UIStyle.Current.Text, TextAlignmentOptions.Center);
        // Blending in Linear thins dark text on a light button.
        label.fontStyle = role == UIRole.Kind.PrimaryButton ? FontStyles.Bold : FontStyles.Normal;
        rect.gameObject.AddComponent<UIRole>().Set(role);
        return label;
    }
}
