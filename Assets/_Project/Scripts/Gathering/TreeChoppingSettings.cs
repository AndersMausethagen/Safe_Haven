using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What felling a tree gives (ChoppableTree): every tree in the world can be chopped with an axe (hatchet). The number of
/// logs grows with the axe's tier and the tree's size, and each log may come out as a better wood: the chance rises with
/// a better axe and a bigger tree. A wood more than one tier above the axe never drops.
/// <para>
/// Logs = (Base Logs + Logs Per Axe Tier × (axe tier − 1)) × size, at least one. Size is the tree's height over
/// Reference Height, within Min Size and Max Size. The chance of a wood of tier w (2 or more), with axe tier t, is
/// Base Chance + Chance Per Axe Tier × (t − (w − 1)) + Chance Per Size × (size − 1), within 0 and Max Chance; each log
/// tries the best wood first.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "TreeChoppingSettings", menuName = "Safe Haven/Settings/Tree Chopping Settings")]
public sealed class TreeChoppingSettings : GameSettings<TreeChoppingSettings>
{
    [Tooltip("The woods, by tier: Wood (I), Hardwood (II), Elderwood (III)...")]
    [SerializeField] private List<ItemDefinition> woodByTier = new List<ItemDefinition>();

    [Header("Logs")]
    [SerializeField, Min(1)] private int baseLogs = 2;
    [SerializeField, Min(0)] private int logsPerAxeTier = 1;
    [Tooltip("A tree this tall (metres) counts as size 1.")]
    [SerializeField, Min(0.5f)] private float referenceHeight = 8f;
    [SerializeField, Min(0.1f)] private float minSize = 0.5f;
    [SerializeField, Min(0.1f)] private float maxSize = 2.5f;

    [Header("Better wood")]
    [Tooltip("Chance of a wood one tier above the axe's, on a tree of size 1.")]
    [SerializeField, Range(0f, 1f)] private float baseChance = 0.08f;
    [SerializeField, Range(0f, 1f)] private float chancePerAxeTier = 0.2f;
    [SerializeField, Range(0f, 1f)] private float chancePerSize = 0.12f;
    [SerializeField, Range(0f, 1f)] private float maxChance = 0.75f;

    [Header("Terrain trees")]
    [Tooltip("The invisible stand-in a painted terrain tree gets near the player (TerrainTreeFelling): the Tree node.")]
    [SerializeField] private GameObject terrainTreeProxy;
    [Tooltip("Seconds until a felled terrain tree grows back. 0: never (until the scene loads again).")]
    [SerializeField, Min(0f)] private float terrainTreeRegrowSeconds = 900f;

    public IReadOnlyList<ItemDefinition> WoodByTier => woodByTier;
    public GameObject TerrainTreeProxy => terrainTreeProxy;
    public float TerrainTreeRegrowSeconds => terrainTreeRegrowSeconds;

    /// <summary>A tree's size from its height in metres.</summary>
    public float SizeOf(float height) => Mathf.Clamp(height / referenceHeight, minSize, maxSize);

    public int Logs(int axeTier, float size) =>
        Mathf.Max(1, Mathf.RoundToInt((baseLogs + logsPerAxeTier * Mathf.Max(0, axeTier - 1)) * size));

    /// <summary>The chance one log comes out as the wood of <paramref name="woodTier"/> (1 is the plain wood).</summary>
    public float ChanceOf(int woodTier, int axeTier, float size)
    {
        if (woodTier <= 1)
        {
            return 1f;
        }

        int above = axeTier - (woodTier - 1);
        if (above < 0)
        {
            return 0f;
        }

        return Mathf.Clamp(baseChance + chancePerAxeTier * above + chancePerSize * (size - 1f), 0f, maxChance);
    }

    /// <summary>The wood one log comes out as, given a roll from 0 to 1 per wood tier tried (best first).</summary>
    public ItemDefinition RollLog(int axeTier, float size, System.Func<float> roll)
    {
        for (int tier = woodByTier.Count; tier >= 2; tier--)
        {
            ItemDefinition wood = woodByTier[tier - 1];
            if (wood != null && roll() < ChanceOf(tier, axeTier, size))
            {
                return wood;
            }
        }

        return woodByTier.Count > 0 ? woodByTier[0] : null;
    }
}
