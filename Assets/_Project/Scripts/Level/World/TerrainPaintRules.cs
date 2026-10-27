using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paints a terrain by its shape (Create › Safe Haven › World › Terrain Paint Rules; applied from the World Terrain
/// Inspector): each rule lays its layer where the ground is steep enough, high or low enough, in a hollow or on a
/// ridge, broken up by noise. Rules apply in order, later ones over earlier ones, so start with the ground that covers
/// everything. Paths, rivers and cliffs paint over the result; hand painting afterwards is kept.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/World/Terrain Paint Rules", fileName = "Terrain Paint Rules")]
public sealed class TerrainPaintRules : ScriptableObject
{
    [Serializable]
    public sealed class Rule
    {
        public TerrainLayer layer;
        [Tooltip("How strongly it paints where every condition is met (0-1).")]
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("Steepness it paints on (degrees: 0 flat, 90 a wall).")]
        public Vector2 slope = new Vector2(0f, 90f);
        [Tooltip("Hollow (+) or ridge (-): 1 paints only in hollows, -1 only on ridges and humps, 0 anywhere.")]
        [Range(-1f, 1f)] public float hollows;
        public bool limitHeight;
        [Tooltip("World heights it paints between (metres).")]
        public Vector2 height = new Vector2(-1000f, 1000f);
        [Tooltip("How soft the edges of each condition are (degrees for steepness, metres for height).")]
        [Min(0.01f)] public float softness = 6f;
        [Tooltip("Breaks the paint up into patches (0 = even, 1 = patchy).")]
        [Range(0f, 1f)] public float patchiness = 0.3f;
        [Tooltip("Size of the patches (metres).")]
        [Min(0.5f)] public float patchSize = 8f;
    }

    [SerializeField] private List<Rule> rules = new List<Rule>();
    [SerializeField] private int seed = 7;

    public List<Rule> Rules => rules;
    public int Seed => seed;
}
