using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blood as decals on the ground (BloodEffects' stains and pools when RenderingSettings has a blood decal): one of four
/// splats from the atlas, turned at random, in the creature's own blood colour. URP's decal shader takes no tint, so the
/// atlas is tinted once per colour into a copy (half size) and kept for the rest of the run.
/// </summary>
public static class BloodDecals
{
    private static readonly int BaseMapId = Shader.PropertyToID("Base_Map");
    private static readonly Dictionary<Color32, Material> Tinted = new Dictionary<Color32, Material>();
    // Colours asked for ahead of the first blow (Prepare), tinted one a frame by WarmOne.
    private static readonly Queue<Color32> Waiting = new Queue<Color32>();

    /// <summary>Tints <paramref name="color"/>'s copy soon, one colour a frame, so the first blow on a new kind of creature doesn't pay
    /// for it: the tint is a pass over the atlas' pixels, 9 ms in the Editor (Docs/QualityLevers_2026-11-24.md §2), landing on the frame
    /// of the hit. Asked by a bleeding creature as it appears.</summary>
    public static void Prepare(Color color)
    {
        Color32 key = color;
        if (!Tinted.ContainsKey(key) && !Waiting.Contains(key))
        {
            Waiting.Enqueue(key);
        }
    }

    /// <summary>Tints the oldest colour waiting, if any (DecalPool, once a frame).</summary>
    public static void WarmOne()
    {
        if (Waiting.Count > 0)
        {
            Color32 next = Waiting.Dequeue();
            if (!Tinted.ContainsKey(next))
            {
                MaterialFor(next);
            }
        }
    }

    /// <summary>Whether <paramref name="color"/>'s copy is made already.</summary>
    public static bool IsTinted(Color color) => Tinted.TryGetValue(color, out Material made) && made != null;

    /// <summary>Whether blood can be drawn as decals now.</summary>
    public static bool Available
    {
        get
        {
            RenderingSettings settings = RenderingSettings.Current;
            return DecalPool.Current != null && settings.BloodDecal != null && settings.BloodAtlas != null && settings.BloodAtlas.isReadable;
        }
    }

    /// <summary>A splat <paramref name="size"/> metres across lying on the ground, for <paramref name="seconds"/>,
    /// spreading to <paramref name="grow"/> times its size over <paramref name="growSeconds"/> (a pool).</summary>
    public static void Splat(Vector3 ground, Vector3 normal, float size, Color color, float seconds, float grow = 1f, float growSeconds = 1f)
    {
        int tile = Random.Range(0, 4);
        var uv = new Vector4(0.5f, 0.5f, (tile % 2) * 0.5f, (tile / 2) * 0.5f);
        Vector3 along = Quaternion.AngleAxis(Random.Range(0f, 360f), normal) * Vector3.ProjectOnPlane(Vector3.forward, normal);
        DecalPool.Current.Press(For(color), ground, normal, along, new Vector2(size, size), uv, seconds, grow, growSeconds);
    }

    /// <summary>The blood decal material in <paramref name="color"/> (its 2 x 2 atlas tinted), or null when blood decals are
    /// not set up. Shared, cached per colour: never destroy it.</summary>
    public static Material MaterialFor(Color color)
    {
        RenderingSettings settings = RenderingSettings.Current;
        return settings.BloodDecal != null && settings.BloodAtlas != null && settings.BloodAtlas.isReadable ? For(color) : null;
    }

    private static Material For(Color color)
    {
        Color32 key = color;
        if (Tinted.TryGetValue(key, out Material material) && material != null)
        {
            return material;
        }

        RenderingSettings settings = RenderingSettings.Current;
        Texture2D atlas = settings.BloodAtlas;
        int mip = atlas.mipmapCount > 1 ? 1 : 0;
        int width = Mathf.Max(1, atlas.width >> mip), height = Mathf.Max(1, atlas.height >> mip);
        Color32[] pixels = atlas.GetPixels32(mip);
        // The atlas is near white; the creature's colour sets the hue, the atlas the shading. Thin blood shows less.
        float r = color.r * 0.85f, g = color.g * 0.85f, b = color.b * 0.85f, a = Mathf.Clamp01(color.a + 0.05f);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            float shade = p.r / 255f;
            pixels[i] = new Color32((byte)Mathf.Min(255f, r * shade * 255f), (byte)Mathf.Min(255f, g * shade * 255f),
                (byte)Mathf.Min(255f, b * shade * 255f), (byte)(p.a * a));
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = "Blood " + ColorUtility.ToHtmlStringRGB(color), wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        material = new Material(settings.BloodDecal) { name = texture.name, hideFlags = HideFlags.DontSave };
        material.SetTexture(BaseMapId, texture);
        Tinted[key] = material;
        return material;
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    /// <summary>Frees every tint and forgets what waits (an Edit Mode test: out of Play nothing else resets them).</summary>
    internal static void ForgetForTests() => ResetForNewPlaySession();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        // DontSave: nothing else frees them, so each Play session left a texture and a material per blood colour behind.
        foreach (Material tinted in Tinted.Values)
        {
            if (tinted != null)
            {
                Object.DestroyImmediate(tinted.GetTexture(BaseMapId));
                Object.DestroyImmediate(tinted);
            }
        }

        Tinted.Clear();
        Waiting.Clear();
    }
}
