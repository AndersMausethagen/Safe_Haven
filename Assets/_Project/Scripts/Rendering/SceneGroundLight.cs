using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The ground light field for a painted scene (the Haven; Docs/LookAndFeelResearch_2026-11-18.md, the Haven's overhaul): what a generated
/// raid's map builds as it loads (GroundLightMap), worked out here behind the loading screen from the scene's own terrain and water: the
/// land's shade in hollows, the canopy's shade on the ground, wet shores at every pond, tarn and river, rocks sitting in the ground and
/// sun through the crowns. The scene's hours go on turning, so nothing in it is tied to one hour: no light thrown back from sunlit ground
/// (that follows the sun's place), and the night's lift follows the clock (GroundLightMap › living hours).
/// <para>Where it belongs: one per painted scene with a single terrain. Its look is set here (Relief, Sunflecks; Bounce stays 0).</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneGroundLight : MonoBehaviour, ISceneSetup
{
    [Tooltip("The scene's terrain. Empty: the active one.")]
    [SerializeField] private Terrain terrain;
    [Tooltip("The field's look. Bounce is ignored: a scene whose hours turn throws back no light from one hour's sun.")]
    [SerializeField] private RaidGroundLight look = new RaidGroundLight(true, 0.25f, 0f, Color.white, 0.6f);

    /// <summary>After the scene's content (buildings from the save, resource nodes) and before the field's own late redraw.</summary>
    public int SetupOrder => 50;

    public RaidGroundLight Look => look;

    public void Configure(Terrain on, RaidGroundLight with)
    {
        terrain = on;
        look = with;
    }

    public IEnumerator Setup()
    {
        GroundLightMap map = GroundLightMap.Current;
        Terrain ground = terrain != null ? terrain : Terrain.activeTerrain;
        if (map == null || ground == null || ground.terrainData == null || look == null || !look.Enabled)
        {
            yield break;
        }

        // The water, read on the main thread (transforms and renderers), laid out off it.
        var still = new List<ShoreGrid.Still>();
        var rivers = new List<Vector4[]>();
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water == null || !water.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (water.HasCentreline)
            {
                rivers.Add(water.WorldCentreline());
            }
            else if (water.TryGetComponent(out Renderer drawn))
            {
                Bounds bounds = drawn.bounds;
                still.Add(new ShoreGrid.Still(new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z), bounds.max.y));
            }
        }

        TerrainData data = ground.terrainData;
        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        Vector3 size = data.size, origin = ground.GetPosition();
        int cells = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Max(size.x, size.z))), 256, 1024);
        Task<ShoreGrid> laying = Task.Run(() => ShoreGrid.Build(heights, size, origin, cells, still, rivers, GroundLightMap.ShoreReach));
        while (!laying.IsCompleted)
        {
            yield return null;
        }

        if (laying.IsFaulted)
        {
            Debug.LogError($"Scene ground light: the shores could not be laid out ({laying.Exception?.GetBaseException().Message}); none are wet.", this);
        }

        ShoreGrid shores = laying.IsFaulted ? null : laying.Result;
        var scene = new RaidGroundLight(true, look.Relief, 0f, Color.white, look.Sunflecks);
        yield return map.Build(ground, null, null, shores != null ? shores.At : (System.Func<Vector2, Vector2>)null, scene, true);
    }
}
