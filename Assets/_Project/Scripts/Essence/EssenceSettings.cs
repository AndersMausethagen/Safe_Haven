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
        [Tooltip("Mastery (Docs/SkillsImprovementResearch_2026-11-24.md 12): cast this many times, the rank costs no essence. 0: never.")]
        [Min(0)] public int practisedUses;
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

    [Header("Learning enchantments (Docs/EnchantingResearch.md)")]
    [Tooltip("The skill enchanting trains and asks for (Runecraft).")]
    [SerializeField] private ProficiencyDefinition enchantingSkill;
    [Tooltip("The parchment that carries an enchantment until it is read at home (its stack's Enchantment is what it teaches).")]
    [SerializeField] private ItemDefinition inscription;
    [Tooltip("A blank one, carried to a carved stone to take a rubbing.")]
    [SerializeField] private ItemDefinition blankParchment;
    [Tooltip("Runecraft practice for carving an enchantment, times the item's tier experience (the crafting skills' Craft Work).")]
    [SerializeField, Min(0f)] private float enchantWork = 3f;
    [Tooltip("Runecraft practice for empowering a level, times the item's tier experience.")]
    [SerializeField, Min(0f)] private float empowerWork = 2f;
    [Tooltip("Runecraft practice for reading an inscription already known, times its tier experience (or handing it to Liv).")]
    [SerializeField, Min(0f)] private float studyWork = 1.5f;
    [Tooltip("Runecraft practice for reading a carved stone, times the raid tier's experience.")]
    [SerializeField, Min(0f)] private float stoneWork = 2f;
    [Tooltip("Chance an elite gives an inscription, by raid tier (I, II, III...).")]
    [SerializeField] private float[] eliteInscriptionChance = { 0.1f, 0.2f, 0.25f, 0.3f, 0.3f };
    [Tooltip("Once a great creature's own are all known, the chance it still gives one (for Liv, or practice).")]
    [SerializeField, Range(0f, 1f)] private float bossRepeatChance = 0.25f;
    [Tooltip("Chance a raid's supply cache holds an inscription besides its loot, before the raid's loot multiplier.")]
    [SerializeField, Range(0f, 1f)] private float cacheInscriptionChance = 0.03f;
    [Tooltip("Liv's rank grows by this much work for each inscription handed to her.")]
    [SerializeField, Min(0)] private int workPerInscriptionGiven = 5;
    [Tooltip("And by this much for each enchantment carved at the altar while she is home.")]
    [SerializeField, Min(0)] private int workPerEnchanting = 1;

    [Header("How worked gear looks in the hand")]
    [Tooltip("A weapon with a Purified enchantment.")]
    [SerializeField, ColorUsage(false, true)] private Color purifiedGlow = new Color(0.25f, 0.75f, 0.45f);
    [Tooltip("A weapon with a Cursed enchantment.")]
    [SerializeField, ColorUsage(false, true)] private Color cursedGlow = new Color(0.8f, 0.1f, 0.14f);
    [Tooltip("A weapon empowered but not enchanted.")]
    [SerializeField, ColorUsage(false, true)] private Color empowerGlow = new Color(0.45f, 0.3f, 0.1f);
    [Tooltip("How much stronger the glow is for each level of empowering.")]
    [SerializeField, Min(0f)] private float glowPerLevel = 0.35f;
    [Tooltip("The motes that drift off worked gear.")]
    [SerializeField] private Material glowMaterial;

    [Header("Skills")]
    [Tooltip("Ranks a known skill can be deepened through, in order. Each states its totals, not what it adds.")]
    [SerializeField] private List<SkillRank> skillRanks = new List<SkillRank>
    {
        new SkillRank { purified = 10, power = 0.15f, cooldownCut = 0.08f, practisedUses = 30 },
        new SkillRank { purified = 20, power = 0.3f, cooldownCut = 0.16f, practisedUses = 90 },
        new SkillRank { purified = 35, power = 0.5f, cooldownCut = 0.25f, practisedUses = 200 },
    };

    public IReadOnlyList<EmpowerLevel> EmpowerLevels => empowerLevels;
    public int PurifiedPerTier => purifiedPerTier;
    public float StatShare => statShare;
    public float WeaponDamage => weaponDamage;
    public float ToolYield => toolYield;
    public float ToolCare => toolCare;
    public IReadOnlyList<EnchantmentDefinition> Enchantments => enchantments;
    public ProficiencyDefinition EnchantingSkill => enchantingSkill;
    public ItemDefinition Inscription => inscription;
    public ItemDefinition BlankParchment => blankParchment;
    public float EnchantWork => enchantWork;
    public float EmpowerWork => empowerWork;
    public float StudyWork => studyWork;
    public float StoneWork => stoneWork;
    public float BossRepeatChance => bossRepeatChance;
    public float CacheInscriptionChance => cacheInscriptionChance;
    public int WorkPerInscriptionGiven => workPerInscriptionGiven;
    public int WorkPerEnchanting => workPerEnchanting;

    /// <summary>Chance an elite of a raid at <paramref name="tier"/> gives an inscription.</summary>
    public float EliteInscriptionChance(int tier) =>
        eliteInscriptionChance == null || eliteInscriptionChance.Length == 0 ? 0f : eliteInscriptionChance[Mathf.Clamp(tier, 1, eliteInscriptionChance.Length) - 1];
    public Color PurifiedGlow => purifiedGlow;
    public Color CursedGlow => cursedGlow;
    public Color EmpowerGlow => empowerGlow;
    public float GlowPerLevel => glowPerLevel;
    public Material GlowMaterial => glowMaterial;
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

        return RelicSettings.Current.FindPower(id);
    }

    /// <summary>What a skill's rank gives; rank 0 is the skill as it was learned.</summary>
    public float PowerAt(int rank) => rank <= 0 || skillRanks.Count == 0 ? 0f : skillRanks[Mathf.Min(rank, skillRanks.Count) - 1].power;

    public float CooldownCutAt(int rank) => rank <= 0 || skillRanks.Count == 0 ? 0f : skillRanks[Mathf.Min(rank, skillRanks.Count) - 1].cooldownCut;
}
