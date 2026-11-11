using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the map knows of a scene's land beyond its picture: its named regions (a grid saying which region each cell belongs to,
/// so borders can follow any shape) and its named places, each found by walking up to it (PlaceDiscovery). Baked from the world's
/// plan by AgentScripts/BuildHavenAtlas.cs; lives at Resources/Atlases/&lt;scene name&gt;, so a scene without one simply has none.
/// </summary>
[CreateAssetMenu(fileName = "World Atlas", menuName = "Safe Haven/World/World Atlas")]
public sealed class WorldAtlas : ScriptableObject
{
    [Serializable]
    public sealed class Region
    {
        public string name;
        [Tooltip("Where its name is written on the map (world x and z): as far from its borders as it gets.")]
        public Vector2 label;
        [Tooltip("How much ground it covers, as a share of the grid: names of small regions are written smaller.")]
        public float share;
        [Tooltip("Where its boundary stone stands (world x and z): beside a path through it, near its middle. Zero: none (home).")]
        public Vector2 stoneAt;
    }

    [Serializable]
    public sealed class Place
    {
        [Tooltip("Stable, for saves: what was found is remembered by this.")]
        public string id;
        public string name;
        [Tooltip("What kind of place it is (Wayfarer's Stones, Old Quarry...).")]
        public string kind;
        public Vector2 at;
        [Tooltip("Metres from it at which it counts as found.")]
        public float radius = 25f;
    }

    [Tooltip("The ground it covers, world x and z: the scene's terrain.")]
    [SerializeField] private Rect area;
    [SerializeField] private int gridSize;
    [Tooltip("One byte per cell, row by row from the south-west corner: the region's index, or 255 for none.")]
    [SerializeField, HideInInspector] private byte[] grid = Array.Empty<byte>();
    [SerializeField] private List<Region> regions = new List<Region>();
    [SerializeField] private List<Place> places = new List<Place>();

    public const byte NoRegion = 255;

    public Rect Area => area;
    public int GridSize => gridSize;
    public IReadOnlyList<Region> Regions => regions;
    public IReadOnlyList<Place> Places => places;

    /// <summary>The atlas of a scene, or null when it has none.</summary>
    public static WorldAtlas For(string sceneName) => string.IsNullOrEmpty(sceneName) ? null : Resources.Load<WorldAtlas>("Atlases/" + sceneName);

    /// <summary>The region's index at a cell, or NoRegion.</summary>
    public byte RegionAtCell(int x, int y) =>
        x < 0 || y < 0 || x >= gridSize || y >= gridSize || grid.Length != gridSize * gridSize ? NoRegion : grid[y * gridSize + x];

    /// <summary>The region at a point in the world, or null outside it.</summary>
    public Region RegionAt(Vector3 world)
    {
        if (gridSize <= 0 || area.width <= 0f)
        {
            return null;
        }

        int x = Mathf.FloorToInt((world.x - area.xMin) / area.width * gridSize);
        int y = Mathf.FloorToInt((world.z - area.yMin) / area.height * gridSize);
        byte index = RegionAtCell(x, y);
        return index < regions.Count ? regions[index] : null;
    }

    /// <summary>Sets everything at once (BuildHavenAtlas).</summary>
    public void Set(Rect covered, int size, byte[] cells, List<Region> named, List<Place> found)
    {
        area = covered;
        gridSize = size;
        grid = cells;
        regions = named;
        places = found;
    }
}
