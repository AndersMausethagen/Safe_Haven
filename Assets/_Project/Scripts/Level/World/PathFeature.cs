using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A path, track or road along a spline (GameObject › Safe Haven › World › Path). The World Builder paints its ground
/// layer with a frayed edge, evens out the ground across it, clears grass and trees from it and can line it with
/// stones or plants. Move a knot and the old path grows back over.
/// </summary>
public sealed class PathFeature : WorldFeature
{
    [Header("Shape")]
    [Tooltip("Width of the fully painted middle (metres). Drag the width handles at each knot to vary it.")]
    [SerializeField, Min(0.3f)] private float width = 2.2f;
    [Tooltip("How far the paint fades out beyond the middle, frayed so it does not look ruled (metres).")]
    [SerializeField, Min(0f)] private float softEdge = 1.2f;
    [Tooltip("How much the ground is evened out across the path (0 = keeps every bump, 1 = level from side to side).")]
    [SerializeField, Range(0f, 1f)] private float flatten = 0.7f;
    [Tooltip("Sinks the path into the ground a little, like a trodden track (metres).")]
    [SerializeField, Range(0f, 0.5f)] private float sink = 0.06f;
    [Tooltip("Per knot: width multiplier (the handles beside each knot).")]
    [SerializeField] private KnotValues widthAtKnots = new KnotValues();

    [Header("Ground")]
    [Tooltip("Terrain layer of the path (trodden soil, gravel).")]
    [SerializeField] private TerrainLayer layer;
    [Tooltip("How strongly the layer is painted (lower lets the ground show through).")]
    [SerializeField, Range(0f, 1f)] private float paintStrength = 1f;
    [SerializeField] private bool clearGrass = true;
    [SerializeField] private bool removeTrees = true;

    [Header("Edges")]
    [Tooltip("Stones, stumps or plants along the edges. Empty: none.")]
    [SerializeField] private ScatterPalette edgeScatter;
    [Tooltip("Edge objects per 10 metres of path (both sides together).")]
    [SerializeField, Min(0f)] private float edgeScatterPer10m = 1.5f;

    public float Width => width;
    public float SoftEdge => softEdge;
    public float Flatten => flatten;
    public float Sink => sink;
    public KnotValues WidthAtKnots => widthAtKnots;
    public TerrainLayer Layer => layer;
    public float PaintStrength => paintStrength;
    public bool ClearGrass => clearGrass;
    public bool RemoveTrees => removeTrees;
    public ScatterPalette EdgeScatter => edgeScatter;
    public float EdgeScatterPer10m => edgeScatterPer10m;

    public override float Reach => width * RiverFeature.MaxOf(widthAtKnots) * 0.5f + softEdge + 1f;

    public override IEnumerable<KnotValues> PerKnot
    {
        get { yield return widthAtKnots; }
    }

    protected override int DefaultOrder => 10;

    public void Configure(TerrainLayer pathLayer, ScatterPalette edges)
    {
        layer = pathLayer;
        edgeScatter = edges;
    }
}
