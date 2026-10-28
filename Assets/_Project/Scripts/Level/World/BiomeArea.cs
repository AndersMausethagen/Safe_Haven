using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An area filled by rules (GameObject › Safe Haven › World › Forest Area): draw a closed spline around it and list what
/// grows there. Each layer places a palette at a density, only where its rules allow (steepness, height, the ground
/// layer underneath, distance from water, paths and cliffs), in clumps if wanted, thinning out towards the edge. The
/// same seed always gives the same forest; it grows again around a river or path that moves through it. Objects
/// already standing there (buildings, nodes, rocks with colliders) are left room.
/// </summary>
public sealed class BiomeArea : WorldFeature
{
    [Serializable]
    public sealed class Layer
    {
        [Tooltip("Shown in the Inspector only.")]
        public string name = "Trees";
        public ScatterPalette palette;
        [Tooltip("Objects per 100 m² where every rule is met (the palette's spacing still applies).")]
        [Min(0f)] public float density = 3f;

        [Header("Where")]
        [Tooltip("Ground steepness it grows on (degrees: 0 flat, 90 a wall).")]
        public Vector2 slope = new Vector2(0f, 30f);
        [Tooltip("Only between these heights (world metres).")]
        public bool limitHeight;
        public Vector2 height = new Vector2(-1000f, 1000f);
        [Tooltip("Only on these terrain layers (empty: any ground).")]
        public TerrainLayer[] onGround = Array.Empty<TerrainLayer>();
        [Tooltip("How much of the ground must be one of those layers (0-1).")]
        [Range(0f, 1f)] public float groundAmount = 0.4f;
        [Tooltip("Keep this far from river water (metres). Negative: only within that distance of water (reeds).")]
        public float awayFromWater = 1.5f;
        [Tooltip("Keep this far from paths (metres).")]
        [Min(0f)] public float awayFromPaths = 1.5f;
        [Tooltip("Keep this far from cliff faces (metres).")]
        [Min(0f)] public float awayFromCliffs = 2f;

        [Header("Look")]
        [Tooltip("0 = evenly spread, 1 = in clumps with clearings between.")]
        [Range(0f, 1f)] public float clumping = 0.5f;
        [Tooltip("Size of the clumps and clearings (metres).")]
        [Min(2f)] public float clumpSize = 18f;
        [Tooltip("Thins out over this distance inside the area's edge (metres).")]
        [Min(0f)] public float edgeFade = 6f;
    }

    [Tooltip("What grows here, placed in this order (big trees first, then what fits between them).")]
    [SerializeField] private List<Layer> layers = new List<Layer>();
    [Tooltip("Room left around objects already standing there with a collider (metres).")]
    [SerializeField, Min(0f)] private float roomAroundObjects = 1f;

    public IReadOnlyList<Layer> Layers => layers;
    public float RoomAroundObjects => roomAroundObjects;

    public override float Reach => 0f;
    public override bool ShapesTerrain => false;
    public override bool IsArea => true;
    protected override int DefaultOrder => 30;

    public void Configure(IEnumerable<Layer> defaults)
    {
        layers = new List<Layer>(defaults);
    }
}
