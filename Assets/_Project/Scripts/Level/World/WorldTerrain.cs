using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a terrain the World Builder shapes, with its settings (added automatically when a river, path or cliff is
/// made over it). The stash keeps the ground under the features; the built list records what was built, so the
/// builder knows when a feature changed or was deleted. Only data: nothing runs in the game.
/// </summary>
[RequireComponent(typeof(Terrain))]
[DisallowMultipleComponent]
public sealed class WorldTerrain : MonoBehaviour
{
    [Serializable]
    public struct Built
    {
        public WorldFeature feature;
        public string hash;
        public Bounds bounds;
        // It changed the terrain when built (an area painting a biome): if it stops, the terrain is rebuilt to undo that.
        public bool paints;
    }

    [Tooltip("The ground under the features (made next to the TerrainData by the World Builder).")]
    [SerializeField] private WorldTerrainStash stash;
    [Tooltip("Build as soon as a feature or the ground under one changes. Off: press Build in this Inspector.")]
    [SerializeField] private bool buildAutomatically = true;
    [Tooltip("Objects standing on the ground (resource nodes, props, buildings) move up or down with it when a feature " +
             "reshapes the ground under them.")]
    [SerializeField] private bool keepObjectsOnGround = true;
    [Tooltip("Rebake the NavMesh after every build (slow on big scenes; otherwise use Rebake NavMesh when done).")]
    [SerializeField] private bool rebakeNavMeshAfterBuild;
    [Tooltip("Rules for Auto-Paint: which layer goes on steep, flat, high, low, hollow and ridged ground.")]
    [SerializeField] private TerrainPaintRules paintRules;
    [SerializeField, HideInInspector] private List<Built> built = new List<Built>();
    [Tooltip("The heights as last built (WorldBuilder.HeightsFingerprint): a heightmap change that leaves them so is no hand edit.")]
    [SerializeField, HideInInspector] private string builtHeights;

    public WorldTerrainStash Stash
    {
        get => stash;
        set => stash = value;
    }

    public bool BuildAutomatically => buildAutomatically;
    public bool KeepObjectsOnGround => keepObjectsOnGround;
    public bool RebakeNavMeshAfterBuild => rebakeNavMeshAfterBuild;

    public TerrainPaintRules PaintRules
    {
        get => paintRules;
        set => paintRules = value;
    }

    public List<Built> BuiltFeatures => built;

    public string BuiltHeights
    {
        get => builtHeights;
        set => builtHeights = value;
    }
}
