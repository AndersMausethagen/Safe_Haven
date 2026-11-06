using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>A pond or river drawn with "Safe Haven/Water". <see cref="WaterInteractor"/>s look it up to know when they
/// are in the water and where its surface is (the top of the mesh bounds, in the object's own space, so rotated and
/// scaled boxes or planes work). It also switches the water shader to its simple look for cameras that render without
/// the depth / opaque textures (SAFE_HAVEN_WATER_SIMPLE), such as the Mobile renderer.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
public sealed class WaterSurface : MonoBehaviour
{
    [Tooltip("Ponds and rivers: the player and stones make ripples and splashes on it. Off for waterfalls.")]
    [SerializeField] private bool takesRipples = true;
    [Tooltip("A pond or lake: other fish bite, and something old waits in the deep (fishing).")]
    [SerializeField] private bool still;
    [Tooltip("How far below the surface something still counts as in the water (metres), e.g. the feet of a wading player.")]
    [SerializeField, Min(0f)] private float depth = 2f;
    [Tooltip("Spline rivers: how fast the water runs in the middle, in m/s. It carries boats, slower towards the banks.")]
    [SerializeField, Min(0f)] private float flowSpeed = 1.4f;
    [Tooltip("Spline rivers (written by the World Builder): the surface's centre line in local space, xyz = a point on " +
             "the surface, w = half the width there. Empty: the top of the mesh bounds is the water.")]
    [SerializeField, HideInInspector] private Vector4[] centreline = System.Array.Empty<Vector4>();
    [Tooltip("An oval pond: its outline is the oval inside the mesh's bounds (the mesh is a disc), not the whole box.")]
    [SerializeField] private bool oval;
    [Header("Drinking")]
    [Tooltip("The player can drink anywhere along its edge, or wading in. A spline river always can.")]
    [SerializeField] private bool drinkable;
    [Tooltip("What the prompt calls it. Empty: River, or Lake for still water.")]
    [SerializeField] private string drinkName;
    [SerializeField, Min(0f)] private float drinkAmount = 40f;
    [Tooltip("Optional: a water container it fills too, such as a Waterskin.")]
    [SerializeField] private ItemDefinition fillItem;
    [SerializeField, Min(0)] private int fillUpTo = 3;

    private static readonly List<WaterSurface> All = new List<WaterSurface>();
    private const string SimpleKeyword = "SAFE_HAVEN_WATER_SIMPLE";
    private static bool hooked;
    private MeshFilter filter;

    private const float DrinkSeconds = 1.5f;
    private const float DrinkReach = 2.5f;

    public bool Oval
    {
        get => oval;
        set => oval = value;
    }

    // Rivers need no setting of their own: a new field on a river's feature would make every scene with one rebuild it on opening.
    public bool Drinkable
    {
        get => drinkable || HasCentreline;
        set => drinkable = value;
    }

    public bool TakesRipples
    {
        get => takesRipples;
        set => takesRipples = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        Unhook();
    }

    /// <summary>Frozen over (WinterIce, in a snowing season): it is walked on, not waded, swum or drunk from.</summary>
    public bool IsFrozen { get; private set; }

    public void SetFrozen(bool frozen) => IsFrozen = frozen;

    /// <summary>The surface height of the water at <paramref name="point"/>, if the point is over a ripple-taking
    /// water surface that isn't frozen, at most <paramref name="reach"/> above it and not deeper than that surface's depth.</summary>
    public static bool TryFind(Vector3 point, float reach, out float height)
    {
        foreach (WaterSurface surface in All)
        {
            if (surface != null && surface.takesRipples && !surface.IsFrozen && surface.Contains(point, reach, out height))
            {
                return true;
            }
        }

        height = 0f;
        return false;
    }

    /// <summary>The point of the surface nearest <paramref name="point"/> (on its top, inside its outline), for sounds.</summary>
    public Vector3 NearestPoint(Vector3 point)
    {
        if (filter == null)
        {
            filter = GetComponent<MeshFilter>();
        }

        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (centreline.Length >= 2)
        {
            Vector3 onLine = transform.InverseTransformPoint(point);
            NearestOnCentreline(centreline, new Vector2(onLine.x, onLine.z), out Vector4 at, out float away);
            Vector2 toPoint = new Vector2(onLine.x - at.x, onLine.z - at.z);
            Vector2 offset = away > at.w && away > 1e-4f ? toPoint * (at.w / away) : toPoint;
            return transform.TransformPoint(new Vector3(at.x + offset.x, at.y, at.z + offset.y));
        }

        if (mesh == null)
        {
            return transform.position;
        }

        Bounds bounds = mesh.bounds;
        Vector3 local = transform.InverseTransformPoint(point);
        if (oval)
        {
            // In the oval's own units, as its bounds are a circle in the mesh and the transform stretches it.
            var fromMiddle = new Vector2(local.x - bounds.center.x, local.z - bounds.center.z);
            fromMiddle = Vector2.ClampMagnitude(fromMiddle, bounds.extents.x);
            return transform.TransformPoint(new Vector3(bounds.center.x + fromMiddle.x, bounds.max.y, bounds.center.z + fromMiddle.y));
        }

        local = new Vector3(Mathf.Clamp(local.x, bounds.min.x, bounds.max.x), bounds.max.y, Mathf.Clamp(local.z, bounds.min.z, bounds.max.z));
        return transform.TransformPoint(local);
    }

    // Its own object, as a hovered Interactable outlines every renderer under it and the water should not light up.
    private void AddDrinkingPlace()
    {
        if (!Drinkable || !Application.isPlaying || transform.Find("Drinking Place") != null)
        {
            return;
        }

        var place = new GameObject("Drinking Place");
        place.transform.SetParent(transform, false);
        place.AddComponent<WaterShore>().Configure(this);
        var source = place.AddComponent<WaterSource>();
        source.Configure(string.IsNullOrWhiteSpace(drinkName) ? (still ? "Lake" : "River") : drinkName, drinkAmount, fillItem, fillUpTo);
        place.GetComponent<Interactable>().Configure(InteractionAnimation.GatherLow, DrinkSeconds, DrinkReach, false);
    }

    /// <summary>Every enabled water surface.</summary>
    public static IReadOnlyList<WaterSurface> Surfaces => All;

    /// <summary>Still water (a pond, a tarn) rather than a river. A river drawn into it joins it (RiverBuilder).</summary>
    public bool IsStill
    {
        get => still;
        set => still = value;
    }

    /// <summary>The open water at a point, if any (within reach of its edge), with its surface height. Ice isn't.</summary>
    public static WaterSurface At(Vector3 point, float reach, out float height)
    {
        height = 0f;
        foreach (WaterSurface surface in All)
        {
            if (surface != null && !surface.IsFrozen && surface.Contains(point, reach, out height))
            {
                return surface;
            }
        }

        return null;
    }

    public bool Contains(Vector3 point, float reach, out float height)
    {
        height = 0f;
        if (filter == null)
        {
            filter = GetComponent<MeshFilter>();
        }

        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null)
        {
            return false;
        }

        Bounds bounds = mesh.bounds;
        Vector3 local = transform.InverseTransformPoint(point);
        if (local.x < bounds.min.x || local.x > bounds.max.x || local.z < bounds.min.z || local.z > bounds.max.z)
        {
            return false;
        }

        float top = bounds.max.y;
        if (centreline.Length >= 2)
        {
            NearestOnCentreline(centreline, new Vector2(local.x, local.z), out Vector4 at, out float away);
            if (away > at.w)
            {
                return false;
            }

            top = at.y;
        }

        height = transform.TransformPoint(new Vector3(local.x, top, local.z)).y;
        return point.y <= height + reach && point.y >= height - depth;
    }

    /// <summary>Gives a spline river its shape (local space; see <see cref="centreline"/>). Null or fewer than two
    /// points goes back to the mesh bounds.</summary>
    public void SetCentreline(Vector4[] points) => centreline = points ?? System.Array.Empty<Vector4>();

    public bool HasCentreline => centreline.Length >= 2;

    /// <summary>A river's centreline in world space: each point (x, surface height, z) with its half width in w; empty for still water.</summary>
    public Vector4[] WorldCentreline()
    {
        var world = new Vector4[centreline.Length];
        float scale = Mathf.Abs(transform.lossyScale.x);
        for (int i = 0; i < centreline.Length; i++)
        {
            Vector3 point = transform.TransformPoint(new Vector3(centreline[i].x, centreline[i].y, centreline[i].z));
            world[i] = new Vector4(point.x, point.y, point.z, centreline[i].w * scale);
        }

        return world;
    }

    /// <summary>The centre line point nearest <paramref name="xz"/> (interpolated: xyz on the surface, w the half
    /// width there) and how far <paramref name="xz"/> is from it on the ground plane.</summary>
    /// <summary>How the water moves at a point: the way the river runs, as fast as it runs there. Nothing on still water.</summary>
    public static Vector3 CurrentAt(Vector3 point, float reach)
    {
        WaterSurface surface = At(point, reach, out _);
        return surface != null ? surface.Current(point) : Vector3.zero;
    }

    private Vector3 Current(Vector3 point)
    {
        if (centreline.Length < 2 || flowSpeed <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 local = transform.InverseTransformPoint(point);
        Vector2 flow = FlowOnCentreline(centreline, new Vector2(local.x, local.z));
        return transform.TransformDirection(new Vector3(flow.x, 0f, flow.y)).normalized * (flow.magnitude * flowSpeed);
    }

    /// <summary>
    /// Which way a spline river runs at a point, as a share of its full speed: the line's own direction, as it always runs
    /// downhill (RiverBuilder.Downhill), fading to a third at the banks where the bed slows it.
    /// </summary>
    public static Vector2 FlowOnCentreline(Vector4[] line, Vector2 xz)
    {
        Vector2 direction = Vector2.zero;
        float width = 1f;
        float distance = float.MaxValue;
        for (int i = 0; i + 1 < line.Length; i++)
        {
            Vector4 a = line[i];
            Vector4 b = line[i + 1];
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 1e-8f)
            {
                continue;
            }

            float t = Mathf.Clamp01(Vector2.Dot(xz - new Vector2(a.x, a.z), ab) / lengthSq);
            Vector4 p = Vector4.Lerp(a, b, t);
            float d = Vector2.Distance(xz, new Vector2(p.x, p.z));
            if (d < distance)
            {
                distance = d;
                direction = ab.normalized;
                width = Mathf.Max(0.01f, p.w);
            }
        }

        float across = Mathf.Clamp01(distance / width);
        return direction * Mathf.Lerp(1f, 1f / 3f, across * across);
    }

    public static void NearestOnCentreline(Vector4[] line, Vector2 xz, out Vector4 nearest, out float distance)
    {
        nearest = line[0];
        distance = float.MaxValue;
        for (int i = 0; i + 1 < line.Length; i++)
        {
            Vector4 a = line[i];
            Vector4 b = line[i + 1];
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(xz - new Vector2(a.x, a.z), ab) / lengthSq) : 0f;
            Vector4 p = Vector4.Lerp(a, b, t);
            float d = Vector2.Distance(xz, new Vector2(p.x, p.z));
            if (d < distance)
            {
                distance = d;
                nearest = p;
            }
        }
    }

    private void Start() => AddDrinkingPlace();

    private void OnEnable()
    {
        filter = GetComponent<MeshFilter>();
        if (!All.Contains(this))
        {
            All.Add(this);
        }

        if (!hooked)
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            hooked = true;
        }
    }

    private void OnDisable()
    {
        All.Remove(this);
        if (All.Count == 0)
        {
            Unhook();
        }
    }

    private static void Unhook()
    {
        if (hooked)
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            hooked = false;
        }

        Shader.DisableKeyword(SimpleKeyword);
    }

    private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        bool full;
        if (camera.TryGetComponent(out UniversalAdditionalCameraData data))
        {
            full = data.requiresDepthTexture && data.requiresColorTexture;
        }
        else
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            full = asset == null || (asset.supportsCameraDepthTexture && asset.supportsCameraOpaqueTexture);
        }

        if (full)
        {
            Shader.DisableKeyword(SimpleKeyword);
        }
        else
        {
            Shader.EnableKeyword(SimpleKeyword);
        }
    }
}
