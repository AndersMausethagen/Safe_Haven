using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The sky-occlusion map (Shaders/SkyOcclusion.hlsl): a top-down orthographic camera draws the world height of whatever stands
/// highest at each spot (roofs, canopies, bushes, people) through the Sky Occlusion Renderer, whose Render Objects pass draws
/// everything with the Sky Occlusion Height material. Shaders then dim the sky's ambient light and reflections under cover and
/// keep rain, puddles and snow out from under roofs, with buildings put up in play and the day's light as it is now (a bake could
/// have neither). At home it follows the player, redrawn every few seconds round them. On a generated raid map it is drawn once
/// over the whole map while the loading screen is up (<see cref="Pin"/>, by GroundLightMap) and from then on only where something
/// changed. Made by WorldRendering; numbers in RenderingSettings.
/// </summary>
public sealed class SkyOcclusionMap : MonoBehaviour
{
    private static readonly int MapId = Shader.PropertyToID("_SkyOcclusionMap");
    private static readonly int RectId = Shader.PropertyToID("_SkyOcclusionRect");
    private static readonly Quaternion Down = Quaternion.LookRotation(Vector3.down, Vector3.forward);

    private RenderingSettings settings;
    private Camera above;
    private RenderTexture map;
    private bool active;
    private float nextRender;
    private float lastRender = -1000f;
    private Vector3 renderedAt = new Vector3(float.MaxValue, 0f, 0f);
    private int renderedBuildings = -1;
    private float strength = 1f;

    // Pinned: the whole raid map, drawn once.
    private RenderTexture pinned;
    private Rect pinnedArea;
    private float pinnedFloor;
    private float pinnedTop;
    private readonly List<Renderer> hidden = new List<Renderer>();
    private readonly List<Renderer> parts = new List<Renderer>();
    // What moves, as the scans in FindMoving last found it: a burst of patches (a building moved: its ground then and now; a row of
    // trees felled) scans the scene once, not six times a patch. Found anew for the whole map's draws and after MovingKeptSeconds.
    private const float MovingKeptSeconds = 2f;
    private readonly List<Renderer> moving = new List<Renderer>();
    private float movingFoundAt = float.NegativeInfinity;

    /// <summary>How much cover dims indirect light in this scene (0-1), as bound.</summary>
    public float Strength => strength;

    /// <summary>True when the map can be drawn at all (switched on, with its renderer on the pipeline's list).</summary>
    public bool CanDraw => settings != null && settings.SkyOcclusion && settings.SkyOcclusionRenderer.HasValue && above != null;

    /// <summary>True while it covers a whole raid map and no longer follows the player.</summary>
    public bool Pinned => pinned != null;

    /// <summary>The square the pinned map covers (x, z in metres), and its texels a side.</summary>
    public Rect PinnedArea => pinnedArea;
    public int PinnedResolution => pinned != null ? pinned.width : 0;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
        int size = Mathf.ClosestPowerOfTwo(Mathf.Clamp(settings.SkyOcclusionResolution, 128, 2048));
        map = new RenderTexture(size, size, GraphicsFormat.R16_SFloat, GraphicsFormat.D24_UNorm_S8_UInt)
        {
            name = "Sky Occlusion", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
        };
        var go = new GameObject("Sky Occlusion Camera") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(transform, false);
        above = go.AddComponent<Camera>();
        above.enabled = false;
        above.orthographic = true;
        above.clearFlags = CameraClearFlags.SolidColor;
        above.backgroundColor = new Color(-1000f, 0f, 0f, 1f);
        above.cullingMask = settings.SkyOcclusionLayers;
        above.allowHDR = true;
        above.allowMSAA = false;
        above.useOcclusionCulling = false;
        FollowView();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        // -1 is URP's "the default renderer": the camera stays off then anyway (Bind).
        data.SetRenderer(settings.SkyOcclusionRenderer ?? -1);
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.antialiasing = AntialiasingMode.None;
        Shader.SetGlobalTexture(MapId, map);
        Off();
    }

    /// <summary>A new scene: how much cover dims the indirect light here (0 off, 1 fully; rain stays out from under cover whenever it is on).
    /// Back to following the player until a raid map pins it.</summary>
    public void Bind(float dimming)
    {
        Unpin();
        strength = Mathf.Clamp01(dimming);
        active = strength > 0f && settings.SkyOcclusion && settings.SkyOcclusionRenderer.HasValue;
        nextRender = 0f;
        renderedAt = new Vector3(float.MaxValue, 0f, 0f);
        if (!active)
        {
            Off();
        }
    }

    /// <summary>
    /// Draws what stands over a whole square (a raid map and a margin round it, so the map's border never fades) once, at
    /// <paramref name="resolution"/> texels a side, and stops following the player: shaders read this until the next scene. Returns
    /// what was drawn as a temporary texture for a readback (the caller releases it with RenderTexture.ReleaseTemporary), or null
    /// when the map can't be drawn. <paramref name="floor"/> and <paramref name="top"/> bound the heights drawn.
    /// </summary>
    public RenderTexture Pin(Rect area, int resolution, float floor, float top)
    {
        if (!CanDraw)
        {
            return null;
        }

        Unpin();
        resolution = Mathf.Clamp(resolution, 64, 2048);
        pinned = new RenderTexture(resolution, resolution, GraphicsFormat.R16_SFloat, GraphicsFormat.None)
        {
            name = "Sky Occlusion (whole map)", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
        };
        pinned.Create();
        pinnedArea = area;
        pinnedFloor = floor;
        pinnedTop = top;
        RenderTexture drawn = Redraw(new RectInt(0, 0, resolution, resolution), true);
        Shader.SetGlobalTexture(MapId, pinned);
        Shader.SetGlobalVector(RectId, new Vector4(area.xMin, area.yMin, 1f / area.width, active ? strength : 0f));
        return drawn;
    }

    /// <summary>
    /// Draws a block of the pinned map's texels again (something there changed: a felled tree, a building) and returns what was
    /// drawn as a temporary texture of the block's size, for a readback (release it with RenderTexture.ReleaseTemporary). What moves is
    /// left out as it was last found, unless <paramref name="rescan"/> (the whole map's draws, once things have been placed) or it was
    /// found more than a moment ago.
    /// </summary>
    public RenderTexture Redraw(RectInt texels, bool rescan = false)
    {
        if (pinned == null || !CanDraw)
        {
            return null;
        }

        texels.ClampToBounds(new RectInt(0, 0, pinned.width, pinned.height));
        if (texels.width <= 0 || texels.height <= 0)
        {
            return null;
        }

        float texel = pinnedArea.width / pinned.width;
        var block = RenderTexture.GetTemporary(new RenderTextureDescriptor(texels.width, texels.height, GraphicsFormat.R16_SFloat,
            GraphicsFormat.D24_UNorm_S8_UInt) { msaaSamples = 1 });
        block.filterMode = FilterMode.Point;
        float halfHeight = texels.height * texel * 0.5f;
        var centre = new Vector3(pinnedArea.xMin + (texels.x + texels.width * 0.5f) * texel, pinnedTop,
                                 pinnedArea.yMin + (texels.y + texels.height * 0.5f) * texel);
        above.targetTexture = block;
        above.orthographicSize = halfHeight;
        above.aspect = texels.width / (float)texels.height;
        above.nearClipPlane = 0.5f;
        above.farClipPlane = Mathf.Max(10f, pinnedTop - pinnedFloor + 5f);
        above.transform.SetPositionAndRotation(centre, Down);
        float reach = Mathf.Sqrt(texels.width * texels.width + texels.height * texels.height) * texel * 0.5f;
        if (rescan || Time.unscaledTime - movingFoundAt > MovingKeptSeconds)
        {
            FindMoving();
        }

        DrawOnce(halfHeight, Mathf.Sqrt(reach * reach + (pinnedTop - pinnedFloor) * (pinnedTop - pinnedFloor)));
        FollowView();
        Graphics.CopyTexture(block, 0, 0, 0, 0, texels.width, texels.height, pinned, 0, 0, texels.x, texels.y);
        return block;
    }

    /// <summary>The pinned map's texels over a square of ground (x, z in metres), clamped to the map.</summary>
    public RectInt TexelsOver(Rect ground)
    {
        if (pinned == null)
        {
            return new RectInt();
        }

        float perMetre = pinned.width / pinnedArea.width;
        int x0 = Mathf.FloorToInt((ground.xMin - pinnedArea.xMin) * perMetre);
        int z0 = Mathf.FloorToInt((ground.yMin - pinnedArea.yMin) * perMetre);
        int x1 = Mathf.CeilToInt((ground.xMax - pinnedArea.xMin) * perMetre);
        int z1 = Mathf.CeilToInt((ground.yMax - pinnedArea.yMin) * perMetre);
        var texels = new RectInt(x0, z0, x1 - x0, z1 - z0);
        texels.ClampToBounds(new RectInt(0, 0, pinned.width, pinned.height));
        return texels;
    }

    private void LateUpdate()
    {
        // Pinned over a raid map: drawn once at load, and again only where something changes (GroundLightMap).
        if (!active || pinned != null || Time.unscaledTime < nextRender)
        {
            return;
        }

        Transform centre = Player.Current != null ? Player.Current.transform : Camera.main != null ? Camera.main.transform : null;
        if (centre == null)
        {
            return;
        }

        Vector3 here = centre.position;
        float move = settings.SkyOcclusionMove;
        bool moved = (new Vector2(here.x - renderedAt.x, here.z - renderedAt.z)).sqrMagnitude > move * move;
        bool built = BuildingObject.Active.Count != renderedBuildings;
        if (!moved && !built && Time.unscaledTime < lastRender + settings.SkyOcclusionRefresh)
        {
            return;
        }

        nextRender = Time.unscaledTime + settings.SkyOcclusionInterval;
        lastRender = Time.unscaledTime;
        renderedAt = here;
        renderedBuildings = BuildingObject.Active.Count;
        // Snapped to whole texels, so the map doesn't shimmer as it follows.
        float area = settings.SkyOcclusionArea;
        float texel = area / map.width;
        Vector3 at = centre.position;
        at.x = Mathf.Round(at.x / texel) * texel;
        at.z = Mathf.Round(at.z / texel) * texel;
        above.transform.SetPositionAndRotation(new Vector3(at.x, at.y + 400f, at.z), Down);
        KeepTerrainResources();
        // Smaller trees stay in the map (Sky Occlusion Lod Bias): their last level's cut culled them from straight above.
        float lodBias = QualitySettings.lodBias;
        try
        {
            QualitySettings.lodBias = lodBias * settings.SkyOcclusionLodBias;
            above.Render();
        }
        finally
        {
            QualitySettings.lodBias = lodBias;
        }
        Shader.SetGlobalTexture(MapId, map);
        Shader.SetGlobalVector(RectId, new Vector4(at.x - area * 0.5f, at.z - area * 0.5f, 1f / area, strength));
    }

    // The camera as the follow map uses it.
    private void FollowView()
    {
        above.orthographicSize = settings.SkyOcclusionArea * 0.5f;
        above.aspect = 1f;
        above.nearClipPlane = 1f;
        above.farClipPlane = 800f;
        above.targetTexture = map;
    }

    // This camera draws only now and then. A terrain throws away what it built for a camera that has not drawn for a few
    // frames, and builds it again on the next draw: with 130,000 trees that was 160 ms, a stutter every 12 m walked. Kept,
    // the same draw takes 6 ms (measured in the Haven, 2026-11-10).
    private static void KeepTerrainResources()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (!terrain.keepUnusedRenderingResources)
            {
                terrain.keepUnusedRenderingResources = true;
            }
        }
    }

    // One draw of a block of the pinned map: level of detail chosen as the follow map would (an orthographic camera picks it by
    // its view's size), every painted tree in it drawn however far the camera stands, and whatever moves or doesn't last left
    // out (HideMoving): it is not cover, and one standing there at load would leave a dark spot for the whole run.
    private void DrawOnce(float halfHeight, float farthest)
    {
        KeepTerrainResources();
        float lodBias = QualitySettings.lodBias;
        Terrain[] terrains = Terrain.activeTerrains;
        var treeDistances = new float[terrains.Length];
        hidden.Clear();
        try
        {
            QualitySettings.lodBias = lodBias * halfHeight / Mathf.Max(1f, settings.SkyOcclusionArea * 0.5f) * settings.SkyOcclusionLodBias;
            for (int i = 0; i < terrains.Length; i++)
            {
                treeDistances[i] = terrains[i].treeDistance;
                terrains[i].treeDistance = Mathf.Max(treeDistances[i], farthest + 20f);
            }

            foreach (Renderer part in moving)
            {
                Hide(part);
            }

            above.Render();
        }
        finally
        {
            QualitySettings.lodBias = lodBias;
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] != null)
                {
                    terrains[i].treeDistance = treeDistances[i];
                }
            }

            foreach (Renderer part in hidden)
            {
                if (part != null)
                {
                    part.forceRenderingOff = false;
                }
            }

            hidden.Clear();
        }
    }

    // What is switched off for a pinned draw: people and creatures (skinned meshes), and the whole of anything that walks (a character
    // controller, a navmesh agent, an animated body: with the weapon, the hat or the pack it carries), anything with a body (a felled
    // tree falling or lying, a cart, a boat, thrown, broken and chopped pieces, a ragdoll) and things dropped on the ground. An Animator
    // counts only when it animates something (a controller) or a body (skinned meshes): a still prop that carries one (a door, a
    // sign) is cover like any other.
    private void FindMoving()
    {
        movingFoundAt = Time.unscaledTime;
        moving.Clear();
        foreach (SkinnedMeshRenderer skinned in FindObjectsByType<SkinnedMeshRenderer>())
        {
            moving.Add(skinned);
        }

        AddUnder(FindObjectsByType<Rigidbody>());
        AddUnder(FindObjectsByType<CharacterController>());
        AddUnder(FindObjectsByType<NavMeshAgent>());
        foreach (Animator animator in FindObjectsByType<Animator>())
        {
            if (animator != null && (animator.runtimeAnimatorController != null || animator.GetComponentInChildren<SkinnedMeshRenderer>(true) != null))
            {
                AddUnder(animator);
            }
        }

        AddUnder(FindObjectsByType<DroppedItem>());
    }

    private void AddUnder<T>(T[] roots) where T : Component
    {
        foreach (T root in roots)
        {
            AddUnder(root);
        }
    }

    private void AddUnder(Component root)
    {
        if (root == null)
        {
            return;
        }

        root.GetComponentsInChildren(false, parts);
        moving.AddRange(parts);
        parts.Clear();
    }

    // Only what is drawn now, so what something else switched off stays off afterwards.
    private void Hide(Renderer part)
    {
        if (part != null && !part.forceRenderingOff)
        {
            part.forceRenderingOff = true;
            hidden.Add(part);
        }
    }

    private void Unpin()
    {
        moving.Clear();
        movingFoundAt = float.NegativeInfinity;
        if (pinned == null)
        {
            return;
        }

        pinned.Release();
        Destroy(pinned);
        pinned = null;
        Shader.SetGlobalTexture(MapId, map);
        Off();
    }

    private static void Off() => Shader.SetGlobalVector(RectId, Vector4.zero);

    private void OnDestroy()
    {
        Off();
        if (pinned != null)
        {
            pinned.Release();
            Destroy(pinned);
        }

        if (map != null)
        {
            map.Release();
            Destroy(map);
        }
    }
}
