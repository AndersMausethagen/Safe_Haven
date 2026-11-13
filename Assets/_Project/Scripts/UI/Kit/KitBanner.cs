using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A painting across the top of a window (Hearthline 3, the user: "more artwork at the top: under Workshop a picture
/// tab could be shown"), from Resources/WindowArt/&lt;key&gt; (Tools/LocalSpawn/window_art.json, AgentScripts/BuildUIArt.cs
/// Windows). The window grows by the banner's height: what sits in its top band (the title, its close button) stays on
/// the painting, and the rest moves down under it, so no layout is redrawn. The painting fills the band (cropped to
/// it), darkened behind the title, fading into the panel at its foot over a gold rule, and slowly drifts so it lives.
/// Changing the key (another station) changes the painting; a key with no painting takes the band away again.
/// </summary>
public sealed class KitBanner : MonoBehaviour
{
    private const string Name = "Hearthline Banner";
    private const float DriftSeconds = 24f;

    private static Sprite fadeDown;
    private static Sprite fadeRight;

    private RectTransform band;
    private Image picture;
    private AspectRatioFitter fit;
    private readonly List<RectTransform> moved = new List<RectTransform>();
    private float height;
    private float keep;
    private float shownHeight;
    private string key;
    private float drift;

    /// <summary>Puts (or changes) the painting <paramref name="art"/> at the top of <paramref name="panel"/>,
    /// <paramref name="bandHeight"/> tall; what starts within <paramref name="keepTop"/> of the top stays on it.</summary>
    public static KitBanner Show(RectTransform panel, string art, float bandHeight, float keepTop)
    {
        if (panel == null)
        {
            return null;
        }

        KitBanner banner = panel.GetComponent<KitBanner>();
        if (banner == null)
        {
            banner = panel.gameObject.AddComponent<KitBanner>();
            banner.Build(panel, bandHeight, keepTop);
        }

        banner.Set(art, string.IsNullOrEmpty(art) ? null : Resources.Load<Sprite>("WindowArt/" + art));
        return banner;
    }

    /// <summary>As Show with a name, for a painting held elsewhere (a raid's own results artwork); null hides the band.</summary>
    public static KitBanner Show(RectTransform panel, Sprite art, float bandHeight, float keepTop)
    {
        if (panel == null)
        {
            return null;
        }

        KitBanner banner = panel.GetComponent<KitBanner>();
        if (banner == null)
        {
            banner = panel.gameObject.AddComponent<KitBanner>();
            banner.Build(panel, bandHeight, keepTop);
        }

        banner.Set(art != null ? "sprite:" + art.name : null, art);
        return banner;
    }

    /// <summary>How much taller it makes the panel now (0 when it shows no painting).</summary>
    public float Growth => shownHeight;

    private void Build(RectTransform panel, float bandHeight, float keepTop)
    {
        height = bandHeight;
        keep = keepTop;
        // What moves down: the panel's direct children below its top band, but not the full overlays (ground, corners).
        foreach (Transform child in panel)
        {
            var rect = (RectTransform)child;
            bool overlay = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;
            bool topAnchored = Mathf.Approximately(rect.anchorMin.y, 1f) && Mathf.Approximately(rect.anchorMax.y, 1f);
            if (overlay || !topAnchored)
            {
                continue;
            }

            float top = -(rect.anchoredPosition.y + rect.rect.height * (1f - rect.pivot.y));
            if (top >= keepTop)
            {
                moved.Add(rect);
            }
        }

        band = CodeUI.At(Name, panel, 0f, 0f, panel.rect.width, bandHeight);
        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = new Vector2(1f, 1f);
        band.offsetMin = new Vector2(0f, -bandHeight);
        band.offsetMax = Vector2.zero;
        band.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        // Over the ground, under everything else.
        Transform ground = panel.Find("Hearthline Ground");
        band.SetSiblingIndex(ground != null ? ground.GetSiblingIndex() + 1 : 0);
        band.gameObject.AddComponent<RectMask2D>();

        RectTransform art = CodeUI.Box("Painting", band, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        picture = art.gameObject.AddComponent<Image>();
        picture.raycastTarget = false;
        fit = art.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        UIStyle style = UIStyle.Current;
        // Dark behind the title (top left), fading into the panel at the foot, and a gold rule under it.
        var shade = CodeUI.Stretch("Shade", band).gameObject.AddComponent<Image>();
        shade.sprite = FadeRight();
        shade.color = new Color(0f, 0f, 0f, 0.62f);
        shade.raycastTarget = false;
        var foot = CodeUI.Stretch("Foot", band).gameObject.AddComponent<Image>();
        foot.sprite = FadeDown();
        foot.color = style.Panel;
        foot.raycastTarget = false;
        var rule = CodeUI.Box("Rule", band, new Vector2(0.5f, 0f), new Vector2(0f, 1.5f), new Vector2(0f, 0.75f)).gameObject.AddComponent<Image>();
        rule.rectTransform.anchorMin = new Vector2(0.03f, 0f);
        rule.rectTransform.anchorMax = new Vector2(0.97f, 0f);
        rule.color = HearthKit.Faded(style.Accent, 0.6f);
        rule.raycastTarget = false;
    }

    private void Set(string art, Sprite sprite)
    {
        if (art == key && picture.sprite != null)
        {
            return;
        }

        key = art;
        picture.sprite = sprite;
        if (sprite != null)
        {
            fit.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        }

        // The band covers the old top band too (the title's), so the window grows only by what it adds below that.
        Grow(sprite != null ? Mathf.Max(0f, height - keep) : 0f);
        band.gameObject.SetActive(sprite != null);
    }

    private Image dark;

    /// <summary>An even dark veil over the whole painting (for a title centred on it, where the left-hand shade is not).</summary>
    public void Darken(float alpha)
    {
        if (band == null)
        {
            return;
        }

        if (dark == null)
        {
            dark = CodeUI.Stretch("Veil", band).gameObject.AddComponent<Image>();
            dark.raycastTarget = false;
            dark.transform.SetSiblingIndex(picture.transform.GetSiblingIndex() + 1);
        }

        dark.color = new Color(0f, 0f, 0f, alpha);
    }

    // The panel grows by the band (both edges, staying centred) and what is under the band moves down by it.
    private void Grow(float to)
    {
        float by = to - shownHeight;
        if (Mathf.Approximately(by, 0f))
        {
            return;
        }

        shownHeight = to;
        var panel = (RectTransform)transform;
        panel.sizeDelta += new Vector2(0f, by);
        foreach (RectTransform rect in moved)
        {
            if (rect != null)
            {
                rect.anchoredPosition -= new Vector2(0f, by);
            }
        }
    }

    // A slow drift across the painting, as if the eye wandered over it.
    private void Update()
    {
        if (picture == null || picture.sprite == null)
        {
            return;
        }

        drift += Time.unscaledDeltaTime / DriftSeconds;
        float t = Mathf.Sin(drift * Mathf.PI * 2f);
        picture.rectTransform.localScale = Vector3.one * (1.06f + 0.02f * t);
        picture.rectTransform.anchoredPosition = new Vector2(t * 14f, Mathf.Cos(drift * Mathf.PI * 2f) * 4f);
    }

    // Transparent at the top to opaque at the foot (its lower third), and dark at the left to clear at the right.
    private static Sprite FadeDown()
    {
        if (fadeDown != null)
        {
            return fadeDown;
        }

        var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < 64; y++)
        {
            float t = 1f - y / 63f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, a));
        }

        texture.Apply();
        fadeDown = Sprite.Create(texture, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        fadeDown.hideFlags = HideFlags.HideAndDontSave;
        return fadeDown;
    }

    private static Sprite FadeRight()
    {
        if (fadeRight != null)
        {
            return fadeRight;
        }

        var texture = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int x = 0; x < 64; x++)
        {
            float t = x / 63f;
            texture.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Pow(1f - t, 1.6f)));
        }

        texture.Apply();
        fadeRight = Sprite.Create(texture, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
        fadeRight.hideFlags = HideFlags.HideAndDontSave;
        return fadeRight;
    }
}
