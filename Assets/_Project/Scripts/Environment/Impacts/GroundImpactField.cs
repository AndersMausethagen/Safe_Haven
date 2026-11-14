using UnityEngine;

/// <summary>
/// The marks blows leave on the ground near the player: one GPU field (a window of Mark Area metres that follows where
/// the marks are pressed), healing by itself over each mark's own time. The world terrain shader sinks the ground by it
/// and raises the lips (WorldTerrainDisplacement.hlsl), and the grass shader lowers and cuts the grass in it; both read it
/// as globals (_GroundImpactField, _GroundImpactArea = origin x, z, 1 / metres, on). Plain C#, owned by GroundImpacts.
/// Nothing is saved and no terrain asset is ever changed.
/// </summary>
public sealed class GroundImpactField
{
    private static readonly int FieldId = Shader.PropertyToID("_GroundImpactField");
    private static readonly int AreaId = Shader.PropertyToID("_GroundImpactArea");

    private RenderTexture field;
    private RenderTexture scratch;
    private Material stamp;
    private Vector2 origin;
    private float area;
    // Seconds until every mark has healed; the field rests (no work each frame) after that.
    private float healing;

    public bool IsActive => field != null && healing > 0f;
    public Vector2 Origin => origin;
    public float Area => area;

    /// <summary>Clears the globals so no shader reads a field that is gone (a new scene, Play starting).</summary>
    public static void ClearGlobals()
    {
        Shader.SetGlobalTexture(FieldId, Texture2D.blackTexture);
        Shader.SetGlobalVector(AreaId, Vector4.zero);
    }

    /// <summary>Presses one mark in, centred at <paramref name="at"/>, lasting <paramref name="seconds"/>.</summary>
    public void Press(GroundMarkShape shape, Vector3 at, Vector3 forward, float radius, float depth, float rim, float length,
        float flatten, float seconds, float arcDegrees)
    {
        if (shape == GroundMarkShape.None || !Ensure())
        {
            return;
        }

        Vector2 centre = new Vector2(at.x, at.z);
        Vector2 uv = (centre - origin) / area;
        Vector2 shift = Vector2.zero;
        if (healing <= 0f || uv.x < 0.25f || uv.y < 0.25f || uv.x > 0.75f || uv.y > 0.75f)
        {
            // Recentred on whole texels, so the marks already in it don't blur as the window moves.
            float texel = area / field.width;
            Vector2 next = centre - Vector2.one * (area * 0.5f);
            next = new Vector2(Mathf.Round(next.x / texel), Mathf.Round(next.y / texel)) * texel;
            shift = healing > 0f ? (next - origin) / area : new Vector2(2f, 2f); // nothing to keep: start clean
            origin = next;
            uv = (centre - origin) / area;
        }

        Heal(0f, shift);
        Vector2 direction = new Vector2(forward.x, forward.z);
        direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector2.up;
        stamp.SetVector("_Centre", uv);
        stamp.SetFloat("_Area", area);
        stamp.SetVector("_Direction", direction);
        stamp.SetFloat("_Shape", (float)shape);
        stamp.SetFloat("_Radius", radius);
        stamp.SetFloat("_Depth", depth);
        stamp.SetFloat("_Rim", rim);
        stamp.SetFloat("_Length", length);
        stamp.SetFloat("_Flatten", flatten);
        stamp.SetFloat("_Seconds", seconds);
        stamp.SetFloat("_ArcCos", Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad));
        stamp.SetFloat("_Seed", Random.Range(0f, 100f));
        Graphics.Blit(field, scratch, stamp, 1);
        Swap();
        healing = Mathf.Max(healing, seconds);
        Bind();
    }

    /// <summary>Copies the marks' ground offsets into every terrain footprint field's second channel (the terrain shader has
    /// no room for another texture, so it reads them there). Called each frame while marks heal, and once after.</summary>
    public void Mirror()
    {
        if (field == null)
        {
            return;
        }

        stamp.SetTexture("_ImpactSource", field);
        stamp.SetVector("_ImpactWindow", new Vector4(origin.x, origin.y, 1f / area, 0f));
        foreach (TerrainFootprints prints in TerrainFootprints.Fields)
        {
            if (prints == null || prints.Field == null)
            {
                continue;
            }

            stamp.SetVector("_FootprintWindow", new Vector4(prints.Origin.x, prints.Origin.y, prints.Area, 0f));
            prints.Overlay(stamp, 2);
        }
    }

    /// <summary>Heals every mark by <paramref name="seconds"/> (each at its own pace).</summary>
    public void Advance(float seconds)
    {
        if (!IsActive || seconds <= 0f)
        {
            return;
        }

        Heal(seconds, Vector2.zero);
        healing -= seconds;
        Bind();
    }

    private void Heal(float seconds, Vector2 shift)
    {
        stamp.SetFloat("_Delta", seconds);
        stamp.SetVector("_Shift", shift);
        Graphics.Blit(field, scratch, stamp, 0);
        Swap();
    }

    private void Swap()
    {
        (field, scratch) = (scratch, field);
    }

    private void Bind()
    {
        Shader.SetGlobalTexture(FieldId, field);
        Shader.SetGlobalVector(AreaId, new Vector4(origin.x, origin.y, 1f / area, healing > 0f ? 1f : 0f));
    }

    private bool Ensure()
    {
        if (field != null)
        {
            return true;
        }

        GroundImpactSettings settings = GroundImpactSettings.Current;
        Shader shader = settings.StampShader;
        if (shader == null || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
        {
            return false;
        }

        stamp = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        area = settings.MarkArea;
        int size = settings.MarkResolution;
        field = Make(size);
        scratch = Make(size);
        return true;
    }

    private static RenderTexture Make(int size)
    {
        var texture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
        {
            name = "Ground Impact Field",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.Create();
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = active;
        return texture;
    }

    /// <summary>Frees the field's textures and clears the globals.</summary>
    public void Release()
    {
        if (field != null) { field.Release(); Object.Destroy(field); }
        if (scratch != null) { scratch.Release(); Object.Destroy(scratch); }
        if (stamp != null) Object.Destroy(stamp);
        field = scratch = null;
        stamp = null;
        healing = 0f;
        ClearGlobals();
    }
}
