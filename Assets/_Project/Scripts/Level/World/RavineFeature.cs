using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ravine, gorge or gully along a spline (GameObject › Safe Haven › World › Ravine). Draw the line along its floor:
/// the World Builder carves the ground down into a floor the depth below the ground along the line, with steep walls
/// rising on both sides to wherever the ground is (so a ravine cut into a hillside has one wall taller than the other).
/// The walls are ragged and noisy, can be broken into ledges like layered rock, are painted rock and lined with crag
/// pieces; rubble lies on the floor. Depth and width can change per knot: narrow slots, wide gorges. It only ever cuts
/// down. Put a River on the same line for a stream at the bottom.
/// </summary>
public sealed class RavineFeature : WorldFeature
{
    [Header("Shape")]
    [Tooltip("Metres from the ground along the line down to the floor.")]
    [SerializeField, Min(1f)] private float depth = 12f;
    [Tooltip("Width of the flat floor, in metres.")]
    [SerializeField, Min(0.5f)] private float floorWidth = 5f;
    [Tooltip("Metres across from the floor's edge to the top of a wall. Small is sheer (1-2 m for a slot canyon); large is a steep valley.")]
    [SerializeField, Min(0.3f)] private float wallRun = 2.5f;
    [Tooltip("How much the walls wander in and out along the ravine, so it doesn't look drawn with a ruler.")]
    [SerializeField, Range(0f, 1f)] private float raggedness = 0.6f;
    [Tooltip("Bumps and hollows on the walls themselves.")]
    [SerializeField, Range(0f, 1f)] private float wallNoise = 0.4f;
    [Tooltip("Ledges in the walls, like layered rock. 0: one smooth face.")]
    [SerializeField, Range(0, 6)] private int ledges = 2;
    [Tooltip("How flat the ledges are: 0 barely shows them, 1 makes clear steps.")]
    [SerializeField, Range(0f, 1f)] private float ledgeStrength = 0.5f;
    [Tooltip("Depth along the line, per knot (1 = the Depth above).")]
    [SerializeField] private KnotValues depthAtKnots = new KnotValues();
    [Tooltip("Floor width along the line, per knot (1 = the Floor Width above).")]
    [SerializeField] private KnotValues widthAtKnots = new KnotValues();

    [Header("Rock")]
    [Tooltip("Crag pieces lined along both walls. Empty: bare terrain walls.")]
    [SerializeField] private CliffKit kit;
    [Tooltip("How much neighbouring crag pieces overlap.")]
    [SerializeField, Range(0f, 0.8f)] private float overlap = 0.45f;
    [SerializeField] private Vector2 sizeVariation = new Vector2(0.95f, 1.15f);
    [Tooltip("Share of crag pieces with a block leaning out over the rim above them (overhangs).")]
    [SerializeField, Range(0f, 1f)] private float overhangs = 0.25f;
    [Tooltip("How far into the wall the crag pieces stand: 0 covers the whole face, 1 leaves it mostly showing.")]
    [SerializeField, Range(0f, 1f)] private float inset = 0.35f;
    [Tooltip("Stones and boulders on the floor.")]
    [SerializeField] private ScatterPalette rubble;
    [SerializeField, Min(0f)] private float rubblePer10m = 3f;

    [Header("Ground")]
    [SerializeField] private TerrainLayer wallLayer;
    [Tooltip("Moss on the walls that face north (shaded). Empty: none.")]
    [SerializeField] private TerrainLayer mossLayer;
    [SerializeField, Range(0f, 1f)] private float northMoss = 0.6f;
    [Tooltip("The floor's ground (gravel, a stream bed). Empty: left as it is.")]
    [SerializeField] private TerrainLayer floorLayer;
    [SerializeField] private bool clearGrass = true;
    [SerializeField] private bool removeTrees = true;

    public float Depth => depth;
    public float FloorWidth => floorWidth;
    public float WallRun => wallRun;
    public float Raggedness => raggedness;
    public float WallNoise => wallNoise;
    public int Ledges => ledges;
    public float LedgeStrength => ledgeStrength;
    public KnotValues DepthAtKnots => depthAtKnots;
    public KnotValues WidthAtKnots => widthAtKnots;
    public CliffKit Kit => kit;
    public float Overlap => overlap;
    public Vector2 SizeVariation => sizeVariation;
    public float Overhangs => overhangs;
    public TerrainLayer MossLayer => mossLayer;
    public float NorthMoss => northMoss;
    public float Inset => inset;
    public ScatterPalette Rubble => rubble;
    public float RubblePer10m => rubblePer10m;
    public TerrainLayer WallLayer => wallLayer;
    public TerrainLayer FloorLayer => floorLayer;
    public bool ClearGrass => clearGrass;
    public bool RemoveTrees => removeTrees;

    public override float Reach => floorWidth * 2f + wallRun + 8f;

    public override IEnumerable<KnotValues> PerKnot
    {
        get
        {
            yield return depthAtKnots;
            yield return widthAtKnots;
        }
    }

    // After cliffs and ground paint (its rock walls paint over them), before paths and rivers (a river laid in it follows its floor).
    protected override int DefaultOrder => 8;

    public void Configure(CliffKit crags, ScatterPalette floorRubble, TerrainLayer rock, TerrainLayer floor, TerrainLayer moss = null)
    {
        kit = crags;
        rubble = floorRubble;
        wallLayer = rock;
        floorLayer = floor;
        mossLayer = moss;
    }
}
