using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Creatures that belong to a set-piece.</summary>
[Serializable]
public sealed class RaidSetPieceGuard
{
    public GameObject prefab;
    [Min(1)] public int count = 1;
    [Tooltip("Only in raids of this tier and above, so a first raid doesn't meet what a fourth does.")]
    [Min(0)] public int fromTier;

    public bool ComesAt(int tier) => tier >= fromTier;
}

/// <summary>
/// An authored place in a generated raid (GDD §9.2): a ruined watchtower, a wolf den, a ring of standing stones. RaidLayoutPlanner picks
/// them for the map's rumour sites and a few of its camps, and RaidMapGenerator stands them there, so a run has landmarks to remember and
/// a rumour's reward stands somewhere, with something guarding it. Keep the middle of the prefab clear: what a rumour brings stands there.
/// A new one is this asset and a prefab, added to Layout Rules › Set Pieces. No code.
/// </summary>
[CreateAssetMenu(fileName = "RaidSetPiece", menuName = "Safe Haven/Raids/Set Piece")]
public sealed class RaidSetPieceDefinition : ScriptableObject
{
    [Tooltip("Stable unique identifier.")]
    [SerializeField] private string id;
    [Tooltip("What the place is called.")]
    [SerializeField] private string displayName;
    [Tooltip("What stands there, built round its middle. Each direct child is set on the ground by itself.")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Radius of open, level ground it needs, in metres.")]
    [SerializeField, Min(3f)] private float radius = 9f;
    [Tooltip("Relative chance against other set-pieces.")]
    [SerializeField, Min(0f)] private float weight = 1f;
    [Tooltip("A rumour's reward can stand in it. Off: only a place for a camp.")]
    [SerializeField] private bool holdsRumours = true;
    [Tooltip("Always on the map, on the first rumour site, as a rumour depends on it.")]
    [SerializeField] private bool alwaysThere;
    [Tooltip("Only on maps of these moods, by name. Empty: any.")]
    [SerializeField] private List<string> moods = new List<string>();
    [Tooltip("Walk here and these are here: they stay about it until something rouses them.")]
    [SerializeField] private List<RaidSetPieceGuard> guards = new List<RaidSetPieceGuard>();

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public GameObject Prefab => prefab;
    public float Radius => radius;
    public float Weight => weight;
    public bool HoldsRumours => holdsRumours;
    public bool AlwaysThere => alwaysThere;
    public IReadOnlyList<RaidSetPieceGuard> Guards => guards;

    /// <summary>Whether it belongs on a map of this mood.</summary>
    public bool Suits(string mood) => moods.Count == 0 || moods.Contains(mood);
}

/// <summary>A set-piece chosen for a generated map, in metres from the map's south-west corner.</summary>
public sealed class RaidLayoutPiece
{
    public RaidSetPieceDefinition Definition;
    public Vector2 Position;
    public float Turn;
}
