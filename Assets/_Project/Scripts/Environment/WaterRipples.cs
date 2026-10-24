using System.Collections.Generic;
using UnityEngine;

/// <summary>The ripple height field "Safe Haven/Water" reads: a square of <see cref="Size"/> metres that follows the
/// player, simulated as a damped wave equation <see cref="StepRate"/> times a second on the GPU (Hidden/Safe Haven/Water
/// Ripples). <see cref="WaterInteractor"/>s push the water down with stamps; the waves then spread and fade on their own.
/// Globals: _WaterRippleTex, _WaterRippleRect (min X, min Z, 1 / size, one texel in UV), _WaterRippleOn.</summary>
public static class WaterRipples
{
    public const int Resolution = 256;
    public const float Size = 24f;
    public const float StepRate = 30f;
    public const int MaxStamps = 16;
    private const float WaveSpeed = 0.15f; // stable below 0.5; about 1 m/s at this size and rate
    private const float Damping = 0.985f;

    private static readonly int TextureId = Shader.PropertyToID("_WaterRippleTex");
    private static readonly int RectId = Shader.PropertyToID("_WaterRippleRect");
    private static readonly int OnId = Shader.PropertyToID("_WaterRippleOn");
    private static readonly int TexelId = Shader.PropertyToID("_RippleTexel");
    private static readonly int ShiftId = Shader.PropertyToID("_RippleShift");
    private static readonly int DampingId = Shader.PropertyToID("_RippleDamping");
    private static readonly int WaveSpeedId = Shader.PropertyToID("_RippleWaveSpeed");
    private static readonly int StampsId = Shader.PropertyToID("_RippleStamps");
    private static readonly int StampCountId = Shader.PropertyToID("_RippleStampCount");
    private static readonly List<Vector4> Pending = new List<Vector4>();
    private static readonly Vector4[] Stamps = new Vector4[MaxStamps];
    private static RenderTexture current, next;
    private static Material material;
    private static Vector2 origin;
    private static bool placed;
    private static float accumulator;

    /// <summary>The current height field (R = height), or null before the first step.</summary>
    public static RenderTexture Texture => current;

    /// <summary>World X / Z of the field's lower corner.</summary>
    public static Vector2 Origin => origin;

    public static int PendingStamps => Pending.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Release();

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void ReleaseBeforeReload() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif

    /// <summary>Push the water down by <paramref name="depth"/> in a circle (world position). Applied on the next step;
    /// beyond <see cref="MaxStamps"/> per step the weakest are dropped.</summary>
    public static void AddStamp(Vector3 point, float radius, float depth)
    {
        if (depth <= 0f || radius <= 0f)
        {
            return;
        }

        Pending.Add(new Vector4(point.x, point.z, radius, depth));
    }

    /// <summary>Creates the textures and loads the shader now (at scene load) instead of on the first ripple, which
    /// would otherwise hitch the frame the player first steps into water.</summary>
    public static void Prewarm() => Ensure();

    /// <summary>Adds the frame time and returns how many simulation steps are due (at most 3 a frame).</summary>
    public static int Advance(float deltaTime)
    {
        float step = 1f / StepRate;
        accumulator = Mathf.Min(accumulator + Mathf.Max(deltaTime, 0f), 3f * step);
        int steps = Mathf.FloorToInt(accumulator / step);
        accumulator -= steps * step;
        return steps;
    }

    /// <summary>Runs <paramref name="steps"/> steps with the area centred on <paramref name="centre"/>; pending
    /// stamps go into the first.</summary>
    public static void Run(int steps, Vector3 centre)
    {
        if (steps <= 0 || !Ensure())
        {
            return;
        }

        float texel = Size / Resolution;
        Vector2 wanted = new Vector2(Mathf.Round((centre.x - Size * 0.5f) / texel) * texel,
                                     Mathf.Round((centre.z - Size * 0.5f) / texel) * texel);
        Vector2Int shift = Vector2Int.zero;
        if (!placed)
        {
            origin = wanted;
            placed = true;
        }
        else
        {
            shift = new Vector2Int(Mathf.RoundToInt((wanted.x - origin.x) / texel), Mathf.RoundToInt((wanted.y - origin.y) / texel));
            if (Mathf.Abs(shift.x) >= Resolution || Mathf.Abs(shift.y) >= Resolution)
            {
                Clear();
                shift = Vector2Int.zero;
            }

            origin = wanted;
        }

        for (int i = 0; i < steps; i++)
        {
            Step(i == 0 ? shift : Vector2Int.zero, i == 0);
        }

        Pending.Clear();
        Shader.SetGlobalTexture(TextureId, current);
        Shader.SetGlobalVector(RectId, new Vector4(origin.x, origin.y, 1f / Size, 1f / Resolution));
        Shader.SetGlobalFloat(OnId, 1f);
    }

    /// <summary>Frees the textures and turns the ripples off in the shader.</summary>
    public static void Release()
    {
        foreach (RenderTexture texture in new[] { current, next })
        {
            if (texture != null)
            {
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        if (material != null)
        {
            Object.DestroyImmediate(material);
        }

        current = next = null;
        material = null;
        placed = false;
        accumulator = 0f;
        Pending.Clear();
        Shader.SetGlobalFloat(OnId, 0f);
    }

    private static bool Ensure()
    {
        if (material == null)
        {
            Shader shader = Resources.Load<Shader>("WaterRipples");
            if (shader == null || !shader.isSupported)
            {
                return false;
            }

            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        if (current == null || !current.IsCreated())
        {
            current = Create("Water Ripples A");
            next = Create("Water Ripples B");
            placed = false;
            Clear();
        }

        return true;
    }

    private static RenderTexture Create(string name)
    {
        var texture = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false
        };
        texture.Create();
        return texture;
    }

    private static void Clear()
    {
        RenderTexture active = RenderTexture.active;
        foreach (RenderTexture texture in new[] { current, next })
        {
            RenderTexture.active = texture;
            GL.Clear(false, true, Color.clear);
        }

        RenderTexture.active = active;
    }

    private static void Step(Vector2Int shift, bool withStamps)
    {
        int count = 0;
        if (withStamps && Pending.Count > 0)
        {
            if (Pending.Count > MaxStamps)
            {
                Pending.Sort((a, b) => b.w.CompareTo(a.w));
            }

            count = Mathf.Min(Pending.Count, MaxStamps);
            for (int i = 0; i < count; i++)
            {
                Vector4 stamp = Pending[i];
                Stamps[i] = new Vector4((stamp.x - origin.x) / Size, (stamp.y - origin.y) / Size, stamp.z / Size, stamp.w);
            }
        }

        material.SetVector(TexelId, new Vector4(1f / Resolution, 1f / Resolution, Resolution, Resolution));
        material.SetVector(ShiftId, new Vector4(shift.x, shift.y, 0f, 0f));
        material.SetFloat(DampingId, Damping);
        material.SetFloat(WaveSpeedId, WaveSpeed);
        material.SetVectorArray(StampsId, Stamps);
        material.SetFloat(StampCountId, count);
        Graphics.Blit(current, next, material);
        (current, next) = (next, current);
    }
}
