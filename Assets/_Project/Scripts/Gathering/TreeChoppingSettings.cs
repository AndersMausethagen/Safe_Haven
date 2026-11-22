using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What felling a tree gives (ChoppableTree): every tree in the world can be chopped with an axe (hatchet). The number of
/// logs grows with the axe's tier and the tree's size, and each log may come out as a better wood: the chance rises with
/// a better axe and a bigger tree. A wood more than one tier above the axe never drops.
/// <para>
/// A felled tree pays in stages. Bringing it down gives Felling Share of those logs when it lands; the rest of the wood is
/// in what lies on the ground: limbing it gives sticks, and each section cut from the trunk gives more logs (FelledLog).
/// </para>
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
    [Tooltip("Away from a raid (the Haven, the prologue), logs come out no better than this wood tier: 2, Hardwood. Elderwood is a " +
             "raid wood (the user, 2026-11-22, on Docs/EconomyAndPhases_2026-11-22.md: \"Elderwood can be raid exclusive\"). A tree " +
             "of its own wood is still that wood.")]
    [SerializeField, Min(1)] private int homeMaxWoodTier = 2;

    [Header("Terrain trees")]
    [Tooltip("The invisible stand-in a painted terrain tree gets near the player (TerrainTreeFelling): the Tree node.")]
    [SerializeField] private GameObject terrainTreeProxy;
    [Tooltip("Seconds until a felled terrain tree grows back. 0: never (until the scene loads again).")]
    [SerializeField, Min(0f)] private float terrainTreeRegrowSeconds = 900f;

    [Header("Small trees")]
    [Tooltip("Trees shorter than this (metres, as they stand) aren't felled with an axe: they are broken down by hand for " +
             "sticks (and a sapling), through Small Tree Proxy.")]
    [SerializeField, Min(0f)] private float smallTreeHeight = 6f;
    [Tooltip("The invisible stand-in for a small painted tree: a node gathered by hand (Young Tree) instead of the felling one.")]
    [SerializeField] private GameObject smallTreeProxy;
    [Tooltip("Played where a small tree is broken down (a burst of leaves).")]
    [SerializeField] private GameObject smallTreeBreak;

    [Header("Working the fallen tree")]
    [Tooltip("The share of the logs above that bringing the tree down gives. The rest of the reward is in cutting it up.")]
    [SerializeField, Range(0.1f, 1f)] private float fellingShare = 0.6f;
    [Tooltip("Sections a tree of size 1 can be cut into.")]
    [SerializeField, Min(0.1f)] private float sectionsPerSize = 2f;
    [Tooltip("At least this many, or a small tree pays no more for being cut up than for being felled.")]
    [SerializeField, Min(1)] private int minSections = 2;
    [SerializeField, Min(1)] private int maxSections = 6;
    [Tooltip("Logs from each section cut, with a tier 1 axe.")]
    [SerializeField, Min(1)] private int logsPerSection = 1;
    [Tooltip("Extra logs per section for each axe tier above 1. Rounded down.")]
    [SerializeField, Min(0f)] private float sectionLogsPerAxeTier = 0.5f;
    [Tooltip("What limbing gives: the branches trimmed off.")]
    [SerializeField] private ItemDefinition limbingItem;
    [Tooltip("How many of it a tree of size 1 gives.")]
    [SerializeField, Min(0f)] private float limbingItemsPerSize = 1.5f;
    [Tooltip("Seconds a fallen tree stays once nobody is working it.")]
    [SerializeField, Min(10f)] private float fallenTreeSeconds = 600f;

    [Header("Falling")]
    [Tooltip("The fallen tree: FelledTree, FelledLog and the node the player cuts it up through.")]
    [SerializeField] private GameObject felledTreePrefab;
    [Tooltip("How far from straight away from the player a tree may turn to find open ground.")]
    [SerializeField, Range(0f, 180f)] private float fallSpreadDegrees = 75f;
    [Tooltip("How far monsters hear a tree come down, in metres.")]
    [SerializeField, Min(0f)] private float landingNoiseRadius = 30f;
    [Tooltip("Camera shake right beside a landing tree. It fades out over Landing Felt Distance.")]
    [SerializeField, Min(0f)] private float landingShake = 0.6f;
    [SerializeField, Min(1f)] private float landingFeltDistance = 30f;
    [Tooltip("Surface of falling timber: how it grips and how little it bounces.")]
    [SerializeField] private PhysicsMaterial timberSurface;

    [Header("What it hits on the way down")]
    [Tooltip("Damage of the whole weight of a tree of size 1, moving at Crush Full Speed.")]
    [SerializeField, Min(0f)] private float crushDamage = 90f;
    [Tooltip("Metres a second a part of the tree must be moving to hurt anyone. Below it, a tree only tips.")]
    [SerializeField, Min(0f)] private float crushFromSpeed = 3f;
    [Tooltip("Metres a second at which a blow is at its worst, and a standing tree is knocked the furthest.")]
    [SerializeField, Min(0.1f)] private float crushFullSpeed = 12f;
    [Tooltip("How far beyond the trunk it reaches, in metres: the branches around it.")]
    [SerializeField, Min(0f)] private float crushReach = 0.7f;
    [Tooltip("The share of that damage the player takes under a falling tree.")]
    [SerializeField, Range(0f, 1f)] private float playerCrushShare = 0.5f;
    [Tooltip("The most a falling tree can take from the player, as a share of their full health: it hurts, but a mistake needn't end a raid.")]
    [SerializeField, Range(0f, 1f)] private float playerCrushMost = 0.6f;
    [Tooltip("The share allies take: companions and rescued people. 0: they always get clear.")]
    [SerializeField, Range(0f, 1f)] private float allyCrushShare;
    [Tooltip("The most degrees a standing tree is knocked over by one that hits it.")]
    [SerializeField, Range(0f, 30f)] private float mostKnockDegrees = 11f;
    [Tooltip("The share of a knock a standing tree keeps: the lean it stands at from then on.")]
    [SerializeField, Range(0f, 0.5f)] private float knockKept = 0.2f;

    [Header("Looks")]
    [Tooltip("Which materials are foliage: words of their names, separated by commas.")]
    [SerializeField] private string foliageWords = "needle, leaf, leaves, vine";
    [Tooltip("Bark for logs of trees whose own bark is made for their model alone.")]
    [SerializeField] private Material logBark;
    [Tooltip("The sawn face of a log.")]
    [SerializeField] private Material cutFace;
    [Tooltip("Leaves coming down from a crown. Sized and coloured to each tree when played.")]
    [SerializeField] private GameObject leafFall;
    [Tooltip("The dust thrown up along a landing tree.")]
    [SerializeField] private GameObject landingDust;
    [Tooltip("Its colour. Keep the alpha low: it builds up where the puffs overlap.")]
    [SerializeField] private Color dustTint = new Color(0.46f, 0.39f, 0.29f, 0.34f);

    [Header("Sounds")]
    [Tooltip("The trunk starting to give, in the last swings before it goes.")]
    [SerializeField] private SoundDefinition creakSound;
    [Tooltip("The crown rushing through the air.")]
    [SerializeField] private SoundDefinition rushSound;
    [Tooltip("The tree hitting the ground.")]
    [SerializeField] private SoundDefinition landingSound;
    [Tooltip("One branch breaking.")]
    [SerializeField] private SoundDefinition branchSnapSound;
    [Tooltip("The branches being cleared off a fallen tree.")]
    [SerializeField] private SoundDefinition limbingSound;
    [Tooltip("Cut timber knocking on the ground as it rolls.")]
    [SerializeField] private SoundDefinition logKnockSound;
    [Tooltip("A falling tree meeting a standing one.")]
    [SerializeField] private SoundDefinition strikeSound;
    [Tooltip("A falling tree coming down on someone.")]
    [SerializeField] private SoundDefinition crushSound;

    public GameObject FelledTreePrefab => felledTreePrefab;
    public float FallSpreadDegrees => fallSpreadDegrees;
    public float LandingNoiseRadius => landingNoiseRadius;
    public float LandingShake => landingShake;
    public float LandingFeltDistance => landingFeltDistance;
    public PhysicsMaterial TimberSurface => timberSurface;
    public float FallenTreeSeconds => fallenTreeSeconds;
    public ItemDefinition LimbingItem => limbingItem;
    public Material LogBark => logBark;
    public Material CutFace => cutFace;
    public GameObject LeafFall => leafFall;
    public GameObject LandingDust => landingDust;
    public Color DustTint => dustTint;
    public SoundDefinition CreakSound => creakSound;
    public SoundDefinition RushSound => rushSound;
    public SoundDefinition LandingSound => landingSound;
    public SoundDefinition BranchSnapSound => branchSnapSound;
    public SoundDefinition LimbingSound => limbingSound;
    public SoundDefinition LogKnockSound => logKnockSound;
    public SoundDefinition StrikeSound => strikeSound;
    public SoundDefinition CrushSound => crushSound;
    public float CrushDamage => crushDamage;
    public float CrushFromSpeed => crushFromSpeed;
    public float CrushFullSpeed => crushFullSpeed;
    public float CrushReach => crushReach;
    public float MostKnockDegrees => mostKnockDegrees;
    public float KnockKept => knockKept;
    public float PlayerCrushMost => playerCrushMost;

    /// <summary>What a tree of this size, moving this fast, does to someone on this side with this much full health.</summary>
    public float CrushDamageTo(Faction faction, float size, float speed, float fullHealth)
    {
        float damage = TreeContact.CrushDamage(crushDamage, size, speed, crushFromSpeed, crushFullSpeed) * CrushShare(faction);
        return faction == Faction.Player ? Mathf.Min(damage, fullHealth * playerCrushMost) : damage;
    }

    /// <summary>How much of a falling tree's damage this side takes.</summary>
    public float CrushShare(Faction faction)
    {
        switch (faction)
        {
            case Faction.Player: return playerCrushShare;
            case Faction.Ally: return allyCrushShare;
            default: return 1f;
        }
    }

    public IReadOnlyList<ItemDefinition> WoodByTier => woodByTier;
    public GameObject TerrainTreeProxy => terrainTreeProxy;
    public float TerrainTreeRegrowSeconds => terrainTreeRegrowSeconds;
    public float SmallTreeHeight => smallTreeHeight;
    public GameObject SmallTreeProxy => smallTreeProxy;
    public GameObject SmallTreeBreak => smallTreeBreak;

    /// <summary>A tree's size from its height in metres.</summary>
    public float SizeOf(float height) => Mathf.Clamp(height / referenceHeight, minSize, maxSize);

    public int Logs(int axeTier, float size) =>
        Mathf.Max(1, Mathf.RoundToInt((baseLogs + logsPerAxeTier * Mathf.Max(0, axeTier - 1)) * size));

    /// <summary>The logs bringing a tree down gives, when it lands.</summary>
    public int FellingLogs(int axeTier, float size) => Mathf.Max(1, Mathf.RoundToInt(Logs(axeTier, size) * fellingShare));

    /// <summary>How many sections a fallen tree of this size can be cut into.</summary>
    public int Sections(float size) => Mathf.Clamp(Mathf.RoundToInt(size * sectionsPerSize), minSections, Mathf.Max(minSections, maxSections));

    /// <summary>The logs one cut section gives to an axe of this tier.</summary>
    public int SectionLogs(int axeTier) => logsPerSection + Mathf.FloorToInt(sectionLogsPerAxeTier * Mathf.Max(0, axeTier - 1));

    /// <summary>The logs of a tree worked to the end: felled, and every section cut.</summary>
    public int AllLogs(int axeTier, float size) => FellingLogs(axeTier, size) + Sections(size) * SectionLogs(axeTier);

    /// <summary>What limbing a tree of this size gives.</summary>
    public int LimbingItems(float size) => limbingItem != null ? Mathf.Max(1, Mathf.RoundToInt(size * limbingItemsPerSize)) : 0;

    /// <summary>Whether a material is a tree's foliage, by its name.</summary>
    public bool IsFoliage(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
        {
            return false;
        }

        foreach (string word in foliageWords.Split(','))
        {
            string trimmed = word.Trim();
            if (trimmed.Length > 0 && materialName.IndexOf(trimmed, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

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

    /// <summary>The best wood tier a log can come out as away from a raid.</summary>
    public int HomeMaxWoodTier => homeMaxWoodTier;

    /// <summary>The wood one log comes out as, given a roll from 0 to 1 per wood tier tried (best first), no better than
    /// <paramref name="maxTier"/>.</summary>
    public ItemDefinition RollLog(int axeTier, float size, System.Func<float> roll, int maxTier = int.MaxValue)
    {
        for (int tier = Mathf.Min(woodByTier.Count, maxTier); tier >= 2; tier--)
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
