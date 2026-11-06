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
    [Tooltip("Metres along the path over which its height evens out. 4 follows the ground; 20-30 grades it like a made track, cutting into "
             + "rises and filling dips, with banks that widen as the cut deepens.")]
    [SerializeField, Min(1f)] private float gradeSmoothing = 4f;
    [Tooltip("Per knot: width multiplier (the handles beside each knot).")]
    [SerializeField] private KnotValues widthAtKnots = new KnotValues();

    [Header("Natural Look")]
    [Tooltip("How much the width swells and pinches along the path, as a trodden track does (0 = even).")]
    [SerializeField, Range(0f, 1f)] private float widthWander;
    [Tooltip("The ground showing through the path in patches, least in the most worn middle (0 = painted evenly).")]
    [SerializeField, Range(0f, 1f)] private float patchiness;
    [Tooltip("How far the edges stand above the worn middle (metres): a soft lip that catches the light from above.")]
    [SerializeField, Range(0f, 0.3f)] private float shoulder;
    [Tooltip("How ragged the grass edge is, with tufts left near the edges (0 = cut cleanly).")]
    [SerializeField, Range(0f, 1f)] private float raggedGrass;

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
    public float GradeSmoothing => gradeSmoothing;
    /// <summary>A graded path's banks: this many metres out for every metre of cut or fill (about 32 degrees).</summary>
    public const float BankPerMetre = 1.6f;
    /// <summary>The furthest a graded path's banks reach past its soft edge (metres); deeper cuts steepen instead.</summary>
    public const float MaxBank = 7f;
    public KnotValues WidthAtKnots => widthAtKnots;
    public float WidthWander => widthWander;
    public float Patchiness => patchiness;
    public float Shoulder => shoulder;
    public float RaggedGrass => raggedGrass;
    public TerrainLayer Layer => layer;
    public float PaintStrength => paintStrength;
    public bool ClearGrass => clearGrass;
    public bool RemoveTrees => removeTrees;
    public ScatterPalette EdgeScatter => edgeScatter;
    public float EdgeScatterPer10m => edgeScatterPer10m;

    public override float Reach => width * RiverFeature.MaxOf(widthAtKnots) * 0.5f * (1f + widthWander * 0.45f) + softEdge * 1.6f + 1f
                                    + (gradeSmoothing > 4.01f ? MaxBank : 0f);

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

    /// <summary>Sets the width and the natural look in one go (the Haven world generator).</summary>
    public void ConfigureLook(float pathWidth, float wander, float patches, float lip, float ragged, float edgesPer10m, float grade = 4f)
    {
        gradeSmoothing = Mathf.Max(1f, grade);
        width = Mathf.Max(0.3f, pathWidth);
        widthWander = Mathf.Clamp01(wander);
        patchiness = Mathf.Clamp01(patches);
        shoulder = Mathf.Clamp(lip, 0f, 0.3f);
        raggedGrass = Mathf.Clamp01(ragged);
        edgeScatterPer10m = Mathf.Max(0f, edgesPer10m);
    }
}
