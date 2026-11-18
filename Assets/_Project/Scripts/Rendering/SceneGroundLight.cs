using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The ground light field for a painted scene (the Haven; Docs/LookAndFeelResearch_2026-11-18.md, the Haven's overhaul): what a generated
/// raid's map builds as it loads (GroundLightMap), worked out here behind the loading screen from the scene's own terrain and water: the
/// land's shade in hollows, the canopy's shade on the ground, wet shores at every pond, tarn and river, rocks sitting in the ground and
/// sun through the crowns. The scene's hours go on turning, so nothing in it is tied to one hour: the light thrown back from sunlit ground
/// is the ground's colour (its colour map, GrassTerrainColour) times how much of the day's sun reaches it, lit by the sun as it is now
/// (Graphics › Haven Sun Bounce), and the night's lift follows the clock (GroundLightMap › living hours).
/// <para>Where it belongs: one per painted scene. Over several terrains (the prologue's meadow and forest) the field lies over all of
/// them, their heights laid on one grid (FieldGround.Across), without bounce (the colour maps are one terrain's). Its look is set here.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneGroundLight : MonoBehaviour, ISceneSetup
{
    [Tooltip("The scene's terrain. Empty: every terrain in this scene, the field over all of them.")]
    [SerializeField] private Terrain terrain;
    [Tooltip("The field's look. Bounce is the day's: what sunlit ground throws back as the sun goes over (one terrain with a colour map).")]
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
        List<Terrain> terrains = Terrains();
        if (map == null || terrains.Count == 0 || look == null || !look.Enabled)
        {
            yield break;
        }

        // The largest is the field's own terrain (its paint, its heightmap for the rocks' feet until the player stands elsewhere).
        Terrain ground = terrains[0];

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

        // Heights read here (the terrains' own), laid out off the main thread: over several terrains, on one grid first.
        FieldGround one = terrains.Count == 1 ? FieldGround.Of(ground) : null;
        var pieces = new List<FieldGround.Piece>();
        if (one == null)
        {
            foreach (Terrain each in terrains)
            {
                pieces.Add(FieldGround.Piece.Of(each));
            }
        }

        Task<(FieldGround, ShoreGrid)> laying = Task.Run(() =>
        {
            FieldGround field = one ?? FieldGround.Across(pieces, AcrossResolution);
            int cells = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Max(field.Size.x, field.Size.z))), 256, 1024);
            return (field, ShoreGrid.Build(field.Heights, field.Size, field.Origin, cells, still, rivers, GroundLightMap.ShoreReach));
        });
        while (!laying.IsCompleted)
        {
            yield return null;
        }

        if (laying.IsFaulted)
        {
            Debug.LogError($"Scene ground light: the ground could not be laid out ({laying.Exception?.GetBaseException().Message}); no field.", this);
            yield break;
        }

        (FieldGround laid, ShoreGrid shores) = laying.Result;
        // The ground's colours for the light it throws back: the terrain's colour map, read back once (one terrain only).
        Color32[] colours = null;
        Vector2Int colourSize = default;
        if (terrains.Count == 1 && look.Bounce > 0f && ground.TryGetComponent(out GrassTerrainColour paint) && paint.ColourMap != null)
        {
            colours = ReadBack(paint.ColourMap, ColourSide, out colourSize);
        }

        var scene = new RaidGroundLight(true, look.Relief, colours != null ? look.Bounce : 0f, look.BounceTint, look.Sunflecks);
        System.Func<Vector2, bool> underWater = null;
        if (shores != null)
        {
            underWater = point =>
            {
                Vector2 shore = shores.At(point);
                return !float.IsNaN(shore.x) && shore.y <= 0f;
            };
        }

        yield return map.Build(ground, colours, underWater, shores != null ? shores.At : (System.Func<Vector2, Vector2>)null, scene, true,
            terrains.Count > 1 ? laid : null, terrains, colourSize);
    }

    /// <summary>The side the colour map is read back at: finer than the field's bounce needs (it spreads over metres).</summary>
    public const int ColourSide = 512;

    // A colour map, readable or not, copied down to side x side through the GPU and read back (sRGB, as a raid's paint colours are).
    private static Color32[] ReadBack(Texture source, int side, out Vector2Int size)
    {
        RenderTexture target = RenderTexture.GetTemporary(side, side, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture before = RenderTexture.active;
        var copy = new Texture2D(side, side, TextureFormat.RGBA32, false, false);
        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            copy.ReadPixels(new Rect(0, 0, side, side), 0, 0, false);
            copy.Apply(false);
            size = new Vector2Int(side, side);
            return copy.GetPixels32();
        }
        finally
        {
            RenderTexture.active = before;
            RenderTexture.ReleaseTemporary(target);
            Destroy(copy);
        }
    }

    /// <summary>Samples a side of the grid that several terrains are laid on (0.5 m apart over the prologue's 500 m).</summary>
    public const int AcrossResolution = 1025;

    /// <summary>The terrains the field lies over: the one set, or every terrain in this scene, the largest first.</summary>
    public List<Terrain> Terrains()
    {
        var found = new List<Terrain>();
        if (terrain != null)
        {
            if (terrain.terrainData != null)
            {
                found.Add(terrain);
            }

            return found;
        }

        foreach (Terrain each in Terrain.activeTerrains)
        {
            if (each != null && each.terrainData != null && each.gameObject.scene == gameObject.scene)
            {
                found.Add(each);
            }
        }

        found.Sort((a, b) => (b.terrainData.size.x * b.terrainData.size.z).CompareTo(a.terrainData.size.x * a.terrainData.size.z));
        return found;
    }
}
