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
    [Tooltip("How far below the surface something still counts as in the water (metres), e.g. the feet of a wading player.")]
    [SerializeField, Min(0f)] private float depth = 2f;

    private static readonly List<WaterSurface> All = new List<WaterSurface>();
    private const string SimpleKeyword = "SAFE_HAVEN_WATER_SIMPLE";
    private static bool hooked;
    private MeshFilter filter;

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

    /// <summary>The surface height of the water at <paramref name="point"/>, if the point is over a ripple-taking
    /// water surface, at most <paramref name="reach"/> above it and not deeper than that surface's depth.</summary>
    public static bool TryFind(Vector3 point, float reach, out float height)
    {
        foreach (WaterSurface surface in All)
        {
            if (surface != null && surface.takesRipples && surface.Contains(point, reach, out height))
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
        if (mesh == null)
        {
            return transform.position;
        }

        Bounds bounds = mesh.bounds;
        Vector3 local = transform.InverseTransformPoint(point);
        local = new Vector3(Mathf.Clamp(local.x, bounds.min.x, bounds.max.x), bounds.max.y, Mathf.Clamp(local.z, bounds.min.z, bounds.max.z));
        return transform.TransformPoint(local);
    }

    /// <summary>Every enabled water surface.</summary>
    public static IReadOnlyList<WaterSurface> Surfaces => All;

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

        height = transform.TransformPoint(new Vector3(local.x, bounds.max.y, local.z)).y;
        return point.y <= height + reach && point.y >= height - depth;
    }

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
