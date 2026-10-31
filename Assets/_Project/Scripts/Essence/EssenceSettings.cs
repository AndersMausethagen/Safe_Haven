using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What Essence buys besides the ward (GDD §13.5, §16.4): empowering gear, the enchantments there are and deepening skills.
/// Every upgrade is deterministic: it costs what it says and never fails. The essences themselves are named in
/// SettlementSettings. Lives at Resources/EssenceSettings, edited on the Essence page of the Systems Editor; built-in
/// defaults apply when the asset is missing.
/// </summary>
[CreateAssetMenu(fileName = "EssenceSettings", menuName = "Safe Haven/Settings/Essence Settings")]
public sealed class EssenceSettings : GameSettings<EssenceSettings>
{
    [Serializable]
    public sealed class EmpowerLevel
    {
        [Tooltip("Purified Essence for this level on a tier I item.")]
        [Min(0)] public int purified = 6;
        [Tooltip("Share of the item's own recipe also needed in materials. Essence augments gathering, it doesn't replace it.")]
        [Min(0f)] public float materialShare = 0.25f;
    }

    [Serializable]
    public sealed class SkillRank
    {
        [Min(0)] public int purified = 10;
        [Tooltip("Share more damage, healing and buff strength in all, at this rank.")]
        [Min(0f)] public float power = 0.15f;
        [Tooltip("Share off the cooldown in all, at this rank.")]
        [Range(0f, 0.9f)] public float cooldownCut = 0.08f;
    }

    [Header("Empowering gear")]
    [SerializeField] private List<EmpowerLevel> empowerLevels = new List<EmpowerLevel>
    {
        new EmpowerLevel { purified = 6, materialShare = 0.25f },
        new EmpowerLevel { purified = 12, materialShare = 0.5f },
        new EmpowerLevel { purified = 24, materialShare = 1f },
    };
    [Tooltip("More Purified Essence per level for each tier of the item above the first.")]
    [SerializeField, Min(0)] private int purifiedPerTier = 2;
    [Tooltip("Each level adds this share of the item's own stat bonuses.")]
    [SerializeField, Min(0f)] private float statShare = 0.2f;
    [Tooltip("Each level of a weapon adds this much Damage. 0.06 is +6%.")]
    [SerializeField, Min(0f)] private float weaponDamage = 0.06f;
    [Tooltip("Each level of a tool adds this Extra Yield Chance.")]
    [SerializeField, Min(0f)] private float toolYield = 0.03f;
    [Tooltip("Each level of a tool adds this much Less Tool Wear.")]
    [SerializeField, Min(0f)] private float toolCare = 0.1f;

    [Header("Enchantments")]
    [SerializeField] private List<EnchantmentDefinition> enchantments = new List<EnchantmentDefinition>();

    [Header("Skills")]
    [Tooltip("Ranks a known skill can be deepened through, in order. Each states its totals, not what it adds.")]
    [SerializeField] private List<SkillRank> skillRanks = new List<SkillRank>
    {
        new SkillRank { purified = 10, power = 0.15f, cooldownCut = 0.08f },
        new SkillRank { purified = 20, power = 0.3f, cooldownCut = 0.16f },
        new SkillRank { purified = 35, power = 0.5f, cooldownCut = 0.25f },
    };

    public IReadOnlyList<EmpowerLevel> EmpowerLevels => empowerLevels;
    public int PurifiedPerTier => purifiedPerTier;
    public float StatShare => statShare;
    public float WeaponDamage => weaponDamage;
    public float ToolYield => toolYield;
    public float ToolCare => toolCare;
    public IReadOnlyList<EnchantmentDefinition> Enchantments => enchantments;
    public IReadOnlyList<SkillRank> SkillRanks => skillRanks;

    public EnchantmentDefinition FindEnchantment(string id)
    {
        foreach (EnchantmentDefinition enchantment in enchantments)
        {
            if (enchantment != null && enchantment.Id == id)
            {
                return enchantment;
            }
        }

        return null;
    }

    /// <summary>What a skill's rank gives; rank 0 is the skill as it was learned.</summary>
    public float PowerAt(int rank) => rank <= 0 || skillRanks.Count == 0 ? 0f : skillRanks[Mathf.Min(rank, skillRanks.Count) - 1].power;

    public float CooldownCutAt(int rank) => rank <= 0 || skillRanks.Count == 0 ? 0f : skillRanks[Mathf.Min(rank, skillRanks.Count) - 1].cooldownCut;
}
