using UnityEngine;

/// <summary>
/// Soft shapes for the interface's moments, made once in code so no texture asset is needed: a vignette (clear in the
/// middle, solid at the edges) for a screen-edge glow, and a band that fades out at its ends and edges, to lay under
/// banner type over a bright world. White, to be tinted by the Image's colour.
/// </summary>
public static class UISprites
{
    private static Sprite vignette;
    private static Sprite band;
    private static Sprite glow;
    private static Sprite ray;
    private static Sprite disc;

    /// <summary>A soft round glow: a bright core fading to nothing. Sparks, motes, the light behind an icon.</summary>
    public static Sprite Glow => glow != null ? glow : glow = Make(64, 64, (x, y) =>
    {
        float r = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
        float core = Mathf.Clamp01(1f - r / 0.35f);
        return Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f) * 0.75f + core * core);
    });

    /// <summary>A thin shaft of light, brightest at its root (the bottom), for rays thrown out from a moment.</summary>
    public static Sprite Ray => ray != null ? ray : ray = Make(16, 128, (x, y) =>
    {
        float across = Mathf.Clamp01(1f - Mathf.Abs(x - 0.5f) * 2f);
        return across * across * Mathf.Pow(1f - y, 1.5f) * Mathf.SmoothStep(0f, 1f, y / 0.08f);
    });

    /// <summary>A soft-edged disc: what an icon sits on.</summary>
    public static Sprite Disc => disc != null ? disc : disc = Make(128, 128, (x, y) =>
    {
        float r = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
        return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.8f, 1f, r));
    });

    public static Sprite Vignette => vignette != null ? vignette : vignette = Make(128, 128, (x, y) =>
    {
        float dx = x * 2f - 1f;
        float dy = y * 2f - 1f;
        float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, r));
    });

    public static Sprite Band => band != null ? band : band = Make(128, 32, (x, y) =>
    {
        float ends = Mathf.SmoothStep(0f, 1f, Mathf.Min(x, 1f - x) / 0.3f);
        float edges = Mathf.SmoothStep(0f, 1f, Mathf.Min(y, 1f - y) / 0.4f);
        return ends * edges;
    });

    private static Sprite Make(int width, int height, System.Func<float, float, float> alpha)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha((x + 0.5f) / width, (y + 0.5f) / height)) * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}
