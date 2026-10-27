using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cliff, crag or rock wall along a spline (GameObject › Safe Haven › World › Cliff). Draw the line where the face
/// should stand: the World Builder raises the ground on the high side (or cuts it away on the low side) into a steep
/// face, stands rock-face pieces from a <see cref="CliffKit"/> along it, overlapping like a real crag, drops rubble at
/// its foot and paints rock under it. A closed spline makes an outcrop (high inside) or a pit (high outside).
/// </summary>
public sealed class CliffFeature : WorldFeature
{
    public enum HighSide
    {
        [Tooltip("Left of the line, looking from the first knot to the next. Closed splines: inside.")]
        Left = 0,
        [Tooltip("Right of the line, looking from the first knot to the next. Closed splines: outside.")]
        Right = 1,
    }

    public enum Shaping
    {
        [Tooltip("Lift the high side up by the height (a ledge, plateau or outcrop).")]
        Raise = 0,
        [Tooltip("Cut the low side down by the height (a ravine or quarry wall).")]
        Cut = 1,
        [Tooltip("Leave the terrain alone: only stand rocks along the line.")]
        RocksOnly = 2,
    }

    [Header("Shape")]
    [Tooltip("Height of the face (metres). Drag the height handles at each knot to vary it.")]
    [SerializeField, Min(0.5f)] private float height = 6f;
    [SerializeField] private HighSide highSide = HighSide.Left;
    [SerializeField] private Shaping shaping = Shaping.Raise;
    [Tooltip("How far the raised or cut ground reaches from the face before blending back (metres).")]
    [SerializeField, Min(1f)] private float plateau = 10f;
    [Tooltip("Horizontal depth of the face: smaller is steeper (metres).")]
    [SerializeField, Min(0.3f)] private float faceDepth = 1.5f;
    [Tooltip("How ragged the edge is along its length (0 = straight, 1 = broken).")]
    [SerializeField, Range(0f, 1f)] private float raggedness = 0.5f;
    [Tooltip("Per knot: height multiplier (the handles above each knot).")]
    [SerializeField] private KnotValues heightAtKnots = new KnotValues();

    [Header("Rocks")]
    [Tooltip("Rock-face pieces to stand along the face. Empty: the terrain alone (rock paint and the terrain's cliff shading).")]
    [SerializeField] private CliffKit kit;
    [Tooltip("How much neighbouring pieces overlap (0 = edge to edge, 0.6 = heavily).")]
    [SerializeField, Range(0f, 0.8f)] private float overlap = 0.45f;
    [Tooltip("Random size variation of the pieces (their tops always sit on the rim; taller ones go deeper into the ground).")]
    [SerializeField] private Vector2 sizeVariation = new Vector2(0.95f, 1.15f);
    [Tooltip("Where the pieces' finished side stands: 0 at the foot of the face, 1 at its top edge. Their backs reach " +
             "into the high ground, out of sight.")]
    [SerializeField, Range(0f, 1f)] private float inset = 0.1f;
    [Tooltip("Boulders and rubble at the foot of the face. Empty: none.")]
    [SerializeField] private ScatterPalette rubble;
    [Tooltip("Rubble per 10 metres of cliff.")]
    [SerializeField, Min(0f)] private float rubblePer10m = 2f;

    [Header("Ground")]
    [Tooltip("Terrain layer painted on and around the face.")]
    [SerializeField] private TerrainLayer rockLayer;
    [SerializeField] private bool clearGrass = true;
    [SerializeField] private bool removeTrees = true;

    public float Height => height;
    public HighSide High => highSide;
    public Shaping Shape => shaping;
    public float Plateau => plateau;
    public float FaceDepth => faceDepth;
    public float Raggedness => raggedness;
    public KnotValues HeightAtKnots => heightAtKnots;
    public CliffKit Kit => kit;
    public float Overlap => overlap;
    public Vector2 SizeVariation => sizeVariation;
    public float Inset => inset;
    public ScatterPalette Rubble => rubble;
    public float RubblePer10m => rubblePer10m;
    public TerrainLayer RockLayer => rockLayer;
    public bool ClearGrass => clearGrass;
    public bool RemoveTrees => removeTrees;

    public override float Reach => plateau + faceDepth + 4f;

    public override IEnumerable<KnotValues> PerKnot
    {
        get { yield return heightAtKnots; }
    }

    protected override int DefaultOrder => 0;

    public void Configure(CliffKit pieces, ScatterPalette footRubble, TerrainLayer rock)
    {
        kit = pieces;
        rubble = footRubble;
        rockLayer = rock;
    }
}
