using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abilities found rather than taught (Resources/SkillLoreSettings; the user, 2026-11-27: "we could also unlock abilities other ways.
/// through exploring or finding rare items"): the lore items that carry them, and how often the raids give one. Each source gives one the
/// player neither knows nor carries while there is any, as inscriptions do, and none once all are found. The Haven's hidden places hold
/// theirs as place finds (HavenPlaceVerbs › Finds).
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Combat/Skill Lore Settings", fileName = "SkillLoreSettings")]
public sealed class SkillLoreSettings : GameSettings<SkillLoreSettings>
{
    [Tooltip("Every lore item (ItemDefinition › Teaches Skill): what the raids may give.")]
    [SerializeField] private List<ItemDefinition> lore = new List<ItemDefinition>();

    [Header("Where the raids give one")]
    [Tooltip("A great creature or a boss laid low: the first one gives lore for sure while the player has found none, so the system shows " +
             "itself; after that, this chance.")]
    [SerializeField, Range(0f, 1f)] private float greatCreatureChance = 0.35f;
    [Tooltip("An elite slain, by the raid's tier (1 to 5): rare.")]
    [SerializeField] private float[] eliteChanceByTier = { 0.03f, 0.04f, 0.05f, 0.06f, 0.07f };
    [Tooltip("A cache that holds an inscription (the rare ones: stage hoards, strange places) holds lore as well at this chance.")]
    [SerializeField, Range(0f, 1f)] private float rareCacheChance = 0.15f;
    [Tooltip("A Lost One laid to rest: this chance, and this much more for each rank it rose.")]
    [SerializeField, Range(0f, 1f)] private float lostOneChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float lostOneChancePerRank = 0.15f;

    public IReadOnlyList<ItemDefinition> Lore => lore;
    public float GreatCreatureChance => greatCreatureChance;
    public float RareCacheChance => rareCacheChance;

    public float EliteChance(int tier) =>
        eliteChanceByTier == null || eliteChanceByTier.Length == 0 ? 0f : eliteChanceByTier[Mathf.Clamp(tier - 1, 0, eliteChanceByTier.Length - 1)];

    public float LostOneChance(int rank) => Mathf.Clamp01(lostOneChance + lostOneChancePerRank * Mathf.Max(0, rank - 1));
}
