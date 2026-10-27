using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A river or stream along a spline (GameObject › Safe Haven › World › River). Draw it from source to mouth: the
/// water runs from the first knot to the last and never flows uphill. The World Builder digs the bed with sloping
/// banks, lays the water surface (its ripples follow the bends), paints the bed and banks, clears grass and trees in
/// the way, sets stones in the water and plants on the banks, and turns steep drops into waterfalls with a plunge pool.
/// </summary>
public sealed class RiverFeature : WorldFeature
{
    public enum SurfaceMode
    {
        [Tooltip("The water lies a little below the ground along the line and only ever runs downhill.")]
        FollowGround = 0,
        [Tooltip("The water lies at the height of the spline's knots (move them up and down).")]
        SplineHeight = 1,
    }

    [Header("Shape")]
    [Tooltip("Width of the water (metres). Drag the width handles at each knot to make it narrower or wider there.")]
    [SerializeField, Min(0.5f)] private float width = 4f;
    [Tooltip("Depth in the middle (metres).")]
    [SerializeField, Min(0.1f)] private float depth = 1.2f;
    [Tooltip("How far the banks slope up to the surrounding ground (metres).")]
    [SerializeField, Min(0.5f)] private float bankWidth = 3f;
    [SerializeField] private SurfaceMode surface = SurfaceMode.FollowGround;
    [Tooltip("Follow Ground: how far the water lies below the ground along the line (metres).")]
    [SerializeField, Min(0f)] private float surfaceBelowGround = 0.35f;
    [Tooltip("Per knot: width multiplier (the handles beside each knot).")]
    [SerializeField] private KnotValues widthAtKnots = new KnotValues();
    [Tooltip("Per knot: depth multiplier.")]
    [SerializeField] private KnotValues depthAtKnots = new KnotValues();

    [Header("Water")]
    [Tooltip("Water material (Safe Haven/Water with Flow Follows the Mesh). Empty: the World Builder's river water.")]
    [SerializeField] private Material water;
    [Tooltip("Flow speed (metres per second). Narrow stretches run faster.")]
    [SerializeField, Range(0f, 6f)] private float flowSpeed = 1.4f;
    [Tooltip("A drop at least this high over a short stretch becomes a waterfall with foam, mist and spray at its foot.")]
    [SerializeField, Min(0.5f)] private float waterfallDrop = 1.5f;

    [Header("Ground")]
    [Tooltip("Terrain layer for the river bed (gravel). Empty: the bed is not painted.")]
    [SerializeField] private TerrainLayer bedLayer;
    [Tooltip("Terrain layer for the banks (mud, soil). Empty: the banks are not painted.")]
    [SerializeField] private TerrainLayer bankLayer;
    [Tooltip("How far beyond the water the bank paint reaches (metres).")]
    [SerializeField, Min(0f)] private float bankPaint = 2f;
    [SerializeField] private bool clearGrass = true;
    [SerializeField] private bool removeTrees = true;

    [Header("Stones and plants")]
    [Tooltip("Stones in the water (they make foam where they break the surface).")]
    [SerializeField] private ScatterPalette stones;
    [Tooltip("Stones per 10 metres of river.")]
    [SerializeField, Min(0f)] private float stonesPer10m = 2f;
    [Tooltip("Rocks and plants along the banks.")]
    [SerializeField] private ScatterPalette bankScatter;
    [Tooltip("Bank objects per 10 metres of river (both banks together).")]
    [SerializeField, Min(0f)] private float bankScatterPer10m = 3f;

    public float Width => width;
    public float Depth => depth;
    public float BankWidth => bankWidth;
    public SurfaceMode Surface => surface;
    public float SurfaceBelowGround => surfaceBelowGround;
    public KnotValues WidthAtKnots => widthAtKnots;
    public KnotValues DepthAtKnots => depthAtKnots;
    public Material Water => water;
    public float FlowSpeed => flowSpeed;
    public float WaterfallDrop => waterfallDrop;
    public TerrainLayer BedLayer => bedLayer;
    public TerrainLayer BankLayer => bankLayer;
    public float BankPaint => bankPaint;
    public bool ClearGrass => clearGrass;
    public bool RemoveTrees => removeTrees;
    public ScatterPalette Stones => stones;
    public float StonesPer10m => stonesPer10m;
    public ScatterPalette BankScatter => bankScatter;
    public float BankScatterPer10m => bankScatterPer10m;

    public override float Reach => width * MaxOf(widthAtKnots) * 0.5f + bankWidth + bankPaint;

    public override IEnumerable<KnotValues> PerKnot
    {
        get
        {
            yield return widthAtKnots;
            yield return depthAtKnots;
        }
    }

    protected override int DefaultOrder => 20;

    /// <summary>Defaults filled in by the World Builder when the river is made.</summary>
    public void Configure(Material waterMaterial, TerrainLayer bed, TerrainLayer bank, ScatterPalette waterStones, ScatterPalette banks)
    {
        water = waterMaterial;
        bedLayer = bed;
        bankLayer = bank;
        stones = waterStones;
        bankScatter = banks;
    }

    internal static float MaxOf(KnotValues values)
    {
        float max = 1f;
        for (int i = 0; i < values.Count; i++)
        {
            max = Mathf.Max(max, values[i]);
        }

        return max;
    }
}
