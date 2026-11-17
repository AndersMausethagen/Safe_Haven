using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lily pads on a generated map's still water (RaidMapDefinition › Lily Pads, Docs/WorldLookPlan_2026-11-17.md B9): clumps of pads,
/// a white water lily on one now and then and a closed bud beside another, where the bed lies a little under the surface near a shore.
/// Ponds and a fen's pools only, never running water, trails, clearings or crossings. RaidLilyPads lays them out and builds them into a
/// mesh per stretch of map: no collider, no shadow. An empty Pads list places none, so a map without the rule is as it was.
/// </summary>
[Serializable]
public sealed class RaidLilyPadRule
{
    [Serializable]
    public sealed class Pad
    {
        [Tooltip("A pad's prefab: a MeshFilter whose mesh (Read/Write on) has its pivot at the waterline. Only the mesh is used.")]
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Smallest and largest size, picked at random for each pad.")]
        public Vector2 scale = new Vector2(0.85f, 1.2f);
    }

    [Tooltip("The pads, picked by weight. Empty: no lily pads on this map.")]
    public List<Pad> pads = new List<Pad>();
    [Tooltip("The flower set on a pad now and then (pivot at the waterline). Empty: none.")]
    public GameObject flower;
    [Tooltip("The closed bud standing beside a pad now and then (pivot at the waterline). Empty: none.")]
    public GameObject bud;
    [Tooltip("What every piece is drawn with. Empty: the first pad prefab's own material.")]
    public Material material;

    [Header("Where")]
    [Tooltip("How far under the surface the bed lies where pads float, least and most, in metres.")]
    public Vector2 depth = new Vector2(0.2f, 0.9f);
    [Tooltip("Pads float only this many metres or less from a shore (a bank, a hummock, a string of the fen).")]
    [Min(0.5f)] public float shoreReach = 4f;
    [Tooltip("Metres pads keep from the edge of a trail or clearing, so none lies where the player walks or wades across.")]
    [Min(0f)] public float trailClearance = 1.5f;

    [Header("How many")]
    [Tooltip("Metres between the spots where a clump could start.")]
    [Min(1f)] public float clumpSpacing = 8f;
    [Tooltip("Chance of a clump at each spot that suits, in the patches of water the noise favours.")]
    [Range(0f, 1f)] public float clumpChance = 0.5f;
    [Tooltip("Metres across the patches of water that carry clumps; between them the water stays open.")]
    [Min(2f)] public float patchSize = 30f;
    [Tooltip("How many pads a clump holds, least and most.")]
    public Vector2Int clumpPads = new Vector2Int(5, 20);

    [Header("Flowers")]
    [Tooltip("Chance that a pad carries a flower.")]
    [Range(0f, 1f)] public float flowerChance = 1f / 12f;
    [Tooltip("Chance that a bud stands beside a pad.")]
    [Range(0f, 1f)] public float budChance = 1f / 12f;

    [Tooltip("Metres the pads float above the water's surface, so the water never draws over them.")]
    [Range(0f, 0.1f)] public float aboveWater = 0.015f;

    /// <summary>True when there is at least one pad with a prefab and weight: otherwise nothing is placed.</summary>
    public bool Enabled
    {
        get
        {
            if (pads == null)
            {
                return false;
            }

            foreach (Pad pad in pads)
            {
                if (pad != null && pad.prefab != null && pad.weight > 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
