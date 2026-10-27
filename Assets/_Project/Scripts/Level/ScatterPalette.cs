using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the Scatter Brush paints (Safe Haven > World > Place > Scatter Brush): weighted prefabs with their size range,
/// how much they follow the slope and how deep they sit, plus the spacing between them. Defaults live in
/// Data/Level/Palettes (forest trees, rocks and logs, wood and stone nodes, forage); make more with Create › Safe Haven ›
/// Level › Scatter Palette.</summary>
[CreateAssetMenu(menuName = "Safe Haven/Level/Scatter Palette", fileName = "Scatter Palette")]
public sealed class ScatterPalette : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Random uniform scale between these.")]
        public Vector2 scale = new Vector2(0.85f, 1.2f);
        [Tooltip("0 = upright, 1 = tilted fully with the ground's slope.")]
        [Range(0f, 1f)] public float alignToSlope = 0.3f;
        [Tooltip("Metres sunk into the ground so it does not hover on bumps.")]
        [Min(0f)] public float sink = 0.05f;
        public bool randomYaw = true;
    }

    public List<Entry> entries = new List<Entry>();
    [Tooltip("Smallest distance between two painted objects (metres).")]
    [Min(0.1f)] public float spacing = 2f;

    /// <summary>A random entry by weight (null when there is nothing to paint).</summary>
    public Entry Pick(System.Random random)
    {
        float total = 0f;
        foreach (Entry entry in entries)
        {
            total += entry.prefab != null ? entry.weight : 0f;
        }

        float roll = (float)random.NextDouble() * total;
        foreach (Entry entry in entries)
        {
            if (entry.prefab == null)
            {
                continue;
            }

            roll -= entry.weight;
            if (roll <= 0f)
            {
                return entry;
            }
        }

        return null;
    }

    public bool Contains(GameObject prefab) => entries.Exists(e => e.prefab == prefab);
}
