using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What belongs together in one kind of place: a ground layer, terrain details (grass, ferns, flowers) with their own
/// density, slopes and patchiness, and prop palettes (stones, logs, debris). E.g. the Haven's meadow, the Misty Woods'
/// floor, a riverbank. Painted by hand with the Biome Brush (Safe Haven › World › Paint › Biome Brush), or over a whole
/// loop by a World Builder Biome Area that uses it (rebuilt whenever the preset changes). Level design data only: the
/// game reads nothing from it.
/// </summary>
[CreateAssetMenu(fileName = "Biome", menuName = "Safe Haven/Level/Biome Preset")]
public sealed class BiomePreset : ScriptableObject
{
    [Serializable]
    public sealed class Detail
    {
        [Tooltip("A plant prefab (the looks' Details folders): added to the terrain as a detail when missing.")]
        public GameObject prefab;
        [Tooltip("Share of ground cells it grows in at full brush strength.")]
        [Range(0f, 1f)] public float density = 0.4f;
        [Tooltip("Slopes it grows on, in degrees.")]
        public Vector2 slope = new Vector2(0f, 35f);
        [Tooltip("Metres across its patches: small for scattered flowers, large for meadows.")]
        [Min(0.5f)] public float patchSize = 4f;
        [Tooltip("0 grows evenly; 1 only in patches, with bare ground between.")]
        [Range(0f, 1f)] public float patchiness = 0.4f;
    }

    [Serializable]
    public sealed class Props
    {
        [Tooltip("Prefabs to scatter, with their own spacing, sizes and sink (Scatter Brush palettes).")]
        public ScatterPalette palette;
        [Tooltip("How many tries per dab, at full brush strength.")]
        [Range(0, 20)] public int tries = 2;
        [Tooltip("Tries per 100 m² when a World Builder Biome Area paints with this biome.")]
        [Min(0f)] public float perHundredSquareMetres = 1f;
        [Tooltip("Slopes they may stand on, in degrees.")]
        public Vector2 slope = new Vector2(0f, 40f);
    }

    [TextArea] public string description;
    [Tooltip("The world look it belongs to, as World Tools groups it: Forest Lake (the Haven), Mossy Forest (the dark " +
             "forest of the raids), or empty for any.")]
    public string look = "";

    [Header("Ground")]
    [Tooltip("Ground painted under the brush. Empty: the ground is left as it is.")]
    public TerrainLayer ground;
    [Range(0f, 1f)] public float groundStrength = 0.5f;
    [Tooltip("Slopes the ground is painted on, in degrees (keep it off cliffs).")]
    public Vector2 groundSlope = new Vector2(0f, 40f);

    [Header("Plants")]
    public List<Detail> details = new List<Detail>();

    [Header("Props")]
    public List<Props> props = new List<Props>();

    [Header("Where")]
    [Tooltip("Paints only within this many metres of water (rivers, lakes, ponds); 0 paints anywhere. For banks.")]
    [Min(0f)] public float nearWater;
}
