using TMPro;
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
        ink.a = style.ScrollCorner != null ? 0.85f : 0.55f;
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            foreach (Image image in existing.GetComponentsInChildren<Image>(true))
            {
                image.color = ink;
            }

            return existing.gameObject;
        }

        if (style.CarvedCorner == null && style.ScrollCorner == null)
        {
            Debug.LogError("UI Style needs its Hearthline corner artwork.", panel);
            return null;
        }

        // Hearthline 3: the kit's gilded scrollwork, in proportion to the window (a tooltip's small, a big window's large),
        // a little brighter than the carved corners it replaces; the carved ones where the kit art isn't installed.
        Sprite art = style.ScrollCorner != null ? style.ScrollCorner : style.CarvedCorner;
        float size = CornerSize;
        if (style.ScrollCorner != null)
        {
            Rect box = panel.rect;
            float shortest = Mathf.Min(box.width, box.height);
            // Smaller than the kit's own windows' (their layouts leave room for 150), and over the edge: windows laid out
            // for the carved corners have tight margins, and a corner over a title reads as clutter.
            size = shortest > 1f ? Mathf.Clamp(shortest * 0.13f, 40f, 104f) : 80f;
            ink.a = 0.85f;
        }

        RectTransform frame = CodeUI.Stretch(FrameName, panel);
        frame.SetAsFirstSibling();
        frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        float inset = style.ScrollCorner != null ? -Mathf.Round(size * 0.12f) : 0f;
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var anchor = new Vector2(x, y);
                var position = new Vector2(x == 0 ? size * 0.5f + inset : -size * 0.5f - inset,
                    y == 0 ? size * 0.5f + inset : -size * 0.5f - inset);
                Image corner = AddIcon(frame, "Corner", art, anchor, Vector2.one * size, position, ink);
                corner.rectTransform.localScale = new Vector3(x == 0 ? 1f : -1f, y == 1 ? 1f : -1f, 1f);
            }
        }

        foreach (Image image in frame.GetComponentsInChildren<Image>(true))
        {
            image.color = ink;
        }

        return frame.gameObject;
    }

    private const string RuleName = "Hearthline Rule";
    private const string GlowName = "Hearthline Glow";

    /// <summary>The carved rule under a window's title: a child of the title, below its text, outside any layout.</summary>
    public static void AddTitleRule(TMP_Text title, UIStyle style)
    {
        if (title == null || style.Divider == null || title.transform.Find(RuleName) != null)
        {
            return;
        }

        var rect = (RectTransform)new GameObject(RuleName, typeof(RectTransform)).transform;
        rect.SetParent(title.transform, false);
        // Just under the letters themselves, not the label's box, which may reach down to the line beneath.
        title.ForceMeshUpdate();
        Bounds letters = title.textBounds;
        Rect box = title.rectTransform.rect;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        float width = letters.size.x > 1f ? letters.size.x + 220f : box.width * 0.6f;
        rect.sizeDelta = new Vector2(Mathf.Clamp(width, 240f, 460f), 16f);
        // From the last line's baseline: the text bounds take in the shadow's padding, which reaches well below the letters.
        TMP_TextInfo info = title.textInfo;
        float under = info.lineCount > 0 ? info.lineInfo[info.lineCount - 1].baseline - title.fontSize * 0.16f
            : letters.size.y > 1f ? letters.min.y : box.yMin;
        rect.anchoredPosition = new Vector2(letters.size.x > 1f ? letters.center.x - box.center.x : 0f, under - box.center.y - 2f);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style.Divider;
        image.preserveAspect = true;
        image.raycastTarget = false;
        Color ink = style.Accent;
        ink.a = 0.7f;
        image.color = ink;
    }

    /// <summary>A soft glow behind a control, kept on its place by UIGlowFollow; it sits just before the control, outside
    /// any layout, so the control draws over it.</summary>
    public static Image AddGlow(RectTransform target, UIStyle style, Color colour, float reach)
    {
        if (target == null || target.parent == null || style.Glow == null)
        {
            return null;
        }

        Transform existing = target.parent.Find(target.name + " " + GlowName);
        if (existing != null)
        {
            return existing.GetComponent<Image>();
        }

        var rect = (RectTransform)new GameObject(target.name + " " + GlowName, typeof(RectTransform)).transform;
        rect.SetParent(target.parent, false);
        rect.SetSiblingIndex(target.GetSiblingIndex());
        rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style.Glow;
        image.raycastTarget = false;
        image.color = colour;
        rect.gameObject.AddComponent<UIGlowFollow>().Follow(target, reach);
        return image;
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
