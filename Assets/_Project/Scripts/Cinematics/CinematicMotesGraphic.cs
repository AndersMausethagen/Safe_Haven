using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>
/// Draws a cinematic layer of drifting specks (embers, snow, pollen, fireflies) as one mesh. Where each speck is comes
/// only from its number and the time it is given, so a shot plays the same every time and can be scrubbed to any second.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CinematicMotesGraphic : MaskableGraphic
{
    private CinematicMotes motes;
    private Sprite look;
    private float seconds;

    public override Texture mainTexture => look != null ? look.texture : s_WhiteTexture;

    public void Show(CinematicMotes settings, Sprite sprite)
    {
        motes = settings;
        look = sprite;
        raycastTarget = false;
        SetAllDirty();
    }

    /// <summary>Moves the specks to where they are this many seconds into the shot.</summary>
    public void SetTime(float time)
    {
        seconds = time;
        SetVerticesDirty();
    }

    /// <summary>Where speck number <paramref name="index"/> is inside its area (0..1 on both axes) and how visible (0..1).</summary>
    public static Vector2 Place(CinematicMotes motes, int index, float seconds, out float visibility, out float size)
    {
        float a = Hash(index, 1), b = Hash(index, 2), c = Hash(index, 3), d = Hash(index, 4);
        // Slower and faster specks, so they don't move as one sheet.
        float pace = 0.6f + 0.8f * d;
        Vector2 at = new Vector2(a, b) + motes.velocity * (seconds * pace);
        float turn = seconds / Mathf.Max(0.1f, motes.wanderSeconds);
        at += motes.wander * new Vector2(Mathf.Sin((turn + c) * Mathf.PI * 2f), Mathf.Cos((turn * 0.8f + d) * Mathf.PI * 2f));
        at = new Vector2(Mathf.Repeat(at.x, 1f), Mathf.Repeat(at.y, 1f));

        // Fades in and out at the two edges it travels between, so wrapping round is never seen.
        float along = Mathf.Abs(motes.velocity.y) >= Mathf.Abs(motes.velocity.x) ? at.y : at.x;
        float edge = motes.edgeFade > 0f ? Mathf.Clamp01(Mathf.Min(along, 1f - along) / motes.edgeFade) : 1f;
        float wink = 1f - motes.twinkle * (0.5f + 0.5f * Mathf.Sin((seconds * (0.3f + 0.9f * c) + b) * Mathf.PI * 2f));
        visibility = edge * wink;
        size = Mathf.Lerp(motes.size.x, motes.size.y, c);
        return at;
    }

    // A steady pseudo-random number in 0..1 for a speck and a purpose.
    private static float Hash(int index, int salt)
    {
        unchecked
        {
            uint n = (uint)(index * 374761393 + salt * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            n ^= n >> 16;
            return (n & 0xFFFFFF) / (float)0x1000000;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (motes == null)
        {
            return;
        }

        Rect rect = rectTransform.rect;
        Vector4 uv = look != null ? DataUtility.GetOuterUV(look) : new Vector4(0f, 0f, 1f, 1f);
        Color32 tint = color;
        for (int i = 0; i < motes.count; i++)
        {
            Vector2 at = Place(motes, i, seconds, out float visibility, out float size);
            if (visibility <= 0.003f)
            {
                continue;
            }

            Vector2 centre = rect.min + Vector2.Scale(motes.area.position + Vector2.Scale(at, motes.area.size), rect.size);
            float half = size * rect.height * 0.5f;
            var shade = new Color32(tint.r, tint.g, tint.b, (byte)(tint.a * visibility));
            int first = vh.currentVertCount;
            vh.AddVert(new Vector3(centre.x - half, centre.y - half), shade, new Vector2(uv.x, uv.y));
            vh.AddVert(new Vector3(centre.x - half, centre.y + half), shade, new Vector2(uv.x, uv.w));
            vh.AddVert(new Vector3(centre.x + half, centre.y + half), shade, new Vector2(uv.z, uv.w));
            vh.AddVert(new Vector3(centre.x + half, centre.y - half), shade, new Vector2(uv.z, uv.y));
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first + 2, first + 3, first);
        }
    }
}
