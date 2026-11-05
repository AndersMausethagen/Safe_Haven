using UnityEngine;
using UnityEngine.UI;

/// <summary>Places the shared carved artwork without intercepting input or changing a window's layout.</summary>
public static class HearthlineArt
{
    private const string FrameName = "Hearthline Corners";
    private const float CornerSize = 36f;

    public static GameObject AddFrame(RectTransform panel, UIStyle style, bool onRaid)
    {
        Transform existing = panel.Find(FrameName);
        Color ink = style.AccentFor(onRaid);
        ink.a = 0.55f;
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            foreach (Image image in existing.GetComponentsInChildren<Image>(true))
            {
                image.color = ink;
            }

            return existing.gameObject;
        }

        if (style.CarvedCorner == null)
        {
            Debug.LogError("UI Style needs its Hearthline carved corner artwork.", panel);
            return null;
        }

        RectTransform frame = CodeUI.Stretch(FrameName, panel);
        frame.SetAsFirstSibling();
        frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var anchor = new Vector2(x, y);
                var position = new Vector2(x == 0 ? CornerSize * 0.5f : -CornerSize * 0.5f,
                    y == 0 ? CornerSize * 0.5f : -CornerSize * 0.5f);
                Image corner = AddIcon(frame, "Corner", style.CarvedCorner, anchor, Vector2.one * CornerSize, position, ink);
                corner.rectTransform.localScale = new Vector3(x == 0 ? 1f : -1f, y == 1 ? 1f : -1f, 1f);
            }
        }

        return frame.gameObject;
    }

    public static Image AddIcon(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size,
        Vector2 position, Color colour, bool onWorld = false)
    {
        if (sprite == null)
        {
            Debug.LogError($"UI Style is missing the artwork for {name}.", parent);
            return null;
        }

        RectTransform rect = CodeUI.Box(name, parent, anchor, size, position);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = colour;
        image.raycastTarget = false;
        if (onWorld)
        {
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        return image;
    }
}
