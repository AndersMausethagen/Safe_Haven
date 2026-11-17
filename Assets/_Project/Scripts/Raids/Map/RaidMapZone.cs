using System;
using UnityEngine;

/// <summary>What the baker does with a painted zone of a raid map.</summary>
public enum RaidZoneRole
{
    /// <summary>Soil, no trees or rocks: trails through the forest.</summary>
    Path = 0,
    /// <summary>How dense the forest grows (full strength = trees at the definition's tree spacing).</summary>
    Forest = 1,
    /// <summary>Open ground: soil and no trees, but rocks and logs may lie there.</summary>
    Clearing = 2,
    /// <summary>Candidate spawn points for the spawn groups whose Zone has this zone's name (ore, herbs, wolves...).</summary>
    Spawn = 3,
    /// <summary>Roads: the two ways to the exit ahead on a generated map, painted with the map's Road Layer (G5). Never painted by hand.</summary>
    Road = 4,
}

/// <summary>
/// One painted layer of a raid map: a square grid of 0–255 strengths (row 0 = south, like the terrain), painted in the
/// Scene view with the RaidMap's painter, plus what the bake does with it. Editor data: a raid never reads it.
/// </summary>
[Serializable]
public sealed class RaidMapZone
{
    [Tooltip("Shown in the painter. Spawn zones are matched by this name from a Spawn Group's Zone.")]
    public string name = "New Zone";
    public RaidZoneRole role = RaidZoneRole.Spawn;
    [Tooltip("Colour of the zone in the painter's overlay.")]
    public Color color = new Color(1f, 0.6f, 0.1f, 1f);

    [Tooltip("Spawn zones: metres between candidate points where the paint is at full strength. Weaker paint gets fewer.")]
    [Min(0.5f)] public float pointSpacing = 8f;
    [Tooltip("Spawn zones: at most this many candidate points over the whole map. A raid fills only some of them " +
             "(the Spawn Group's count or the raid's spawn list, scaled by enemy density).")]
    [Min(0)] public int maxPoints = 40;
    [Tooltip("Spawn zones: no points on slopes steeper than this (degrees).")]
    [Range(0f, 90f)] public float maxSlope = 35f;
    [Tooltip("Spawn zones: trees and rocks keep this far (metres) from each point, so a node is never inside a trunk.")]
    [Min(0f)] public float clearTrees;

    [SerializeField, HideInInspector] private int resolution;
    [SerializeField, HideInInspector] private byte[] weights;

    public int Resolution => resolution;
    /// <summary>The raw strengths, resolution × resolution, row 0 = south. Edit through the painter, with Undo.</summary>
    public byte[] Weights => weights;

    public bool IsSpawnZone => role == RaidZoneRole.Spawn;

    /// <summary>Clears the zone to the given resolution, or resamples what is painted when only the size changes.</summary>
    public void EnsureResolution(int size)
    {
        size = Mathf.Max(2, size);
        if (weights != null && resolution == size && weights.Length == size * size)
        {
            return;
        }

        weights = weights != null && resolution > 1 && weights.Length == resolution * resolution
            ? RaidZoneGrid.Resample(weights, resolution, size)
            : new byte[size * size];
        resolution = size;
    }

    /// <summary>Replaces the whole grid (used by imports).</summary>
    public void SetWeights(byte[] values, int size)
    {
        if (values == null || values.Length != size * size)
        {
            throw new ArgumentException("A zone's weights must be size × size.");
        }

        weights = values;
        resolution = size;
    }

    /// <summary>Strength 0–1 at a map position (0–1 east, 0–1 north), smoothly between cells.</summary>
    public float Sample(float u, float v) => weights == null ? 0f : RaidZoneGrid.Sample(weights, resolution, u, v);
}

/// <summary>A point of interest on a raid map, dragged in the Scene view: where the player starts, an exit, a camp...</summary>
[Serializable]
public sealed class RaidMapMarker
{
    public RaidSiteKind kind = RaidSiteKind.EnemyCamp;
    [Tooltip("0–1 across the map: x east, y north (before the raid turns the map).")]
    public Vector2 position = new Vector2(0.5f, 0.5f);
    [Tooltip("A note for designers, shown next to the marker. Not used by the game.")]
    public string label = "";

    public RaidSite ToSite() => new RaidSite(kind, position);
}
