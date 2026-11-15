using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What a fight comes to at one raid tier, worked out from the real assets: the best kit of a tier, its gear score, its damage and how
/// much it can take, and from that the hits to kill and the hits to die against a reference creature. It is how a tier gets tuned to its
/// gear score: at the recommended kit those hits should stay about the same from tier to tier, and the kit before should feel it.
/// The kit's damage counts the Melee skill a player is expected to have at the tier (LevelingSettings.ExpectedLevel): none at tier I,
/// whose expected level is the baseline the game was tuned for, a little more each tier after.
/// Plain arithmetic: it ignores attack speed, perks and timing, so read the numbers against each other, not as a fight's length.
/// </summary>
public static class GearBalance
{
    /// <summary>The best of one tier in each slot, what it adds up to, and how it does in a fight.</summary>
    public sealed class Kit
    {
        public int Tier;
        public int Score;
        public float Damage;
        public float Armor;
        public float Health;
        public float Reduction;
        /// <summary>Share of a physical blow resistances take away (FightMath.Totals.Resist); 0 in the plain kit.</summary>
        public float Resist;
        /// <summary>The enchantment counted on each piece of the stacked kit, for the window.</summary>
        public readonly List<EnchantmentDefinition> Enchantments = new List<EnchantmentDefinition>();
        /// <summary>The Melee skill's damage bonus at the level expected for the tier, already in Damage.</summary>
        public float SkillDamage;
        public readonly List<ItemDefinition> Pieces = new List<ItemDefinition>();

        /// <summary>How much damage it takes to kill: health over what armor lets through.</summary>
        public float Toughness => Health / Mathf.Max(0.01f, (1f - Reduction) * (1f - Mathf.Clamp(Resist, 0f, 0.9f)));
    }

    private static readonly ArmorSlot[] WornSlots = { ArmorSlot.Head, ArmorSlot.Neck, ArmorSlot.Chest, ArmorSlot.Hands, ArmorSlot.Waist, ArmorSlot.Feet };

    public static List<ItemDefinition> AllGear() => AssetDatabase.FindAssets("t:ItemDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
        .Where(item => item != null && item.GearScoreOverride == 0 && GearScore.Counts(item)).ToList();

    /// <summary>The best kit that can be worn at a tier: this tier's pieces, and the tier before's where it has none yet.</summary>
    public static Kit KitOf(int tier, IReadOnlyList<ItemDefinition> gear)
    {
        var kit = new Kit { Tier = tier };
        ItemDefinition Best(Func<ItemDefinition, bool> fits)
        {
            for (int t = tier; t >= 0; t--)
            {
                ItemDefinition best = gear.Where(item => item.Tier == t && fits(item)).OrderByDescending(GearScore.Of).ThenBy(item => item.name, StringComparer.Ordinal).FirstOrDefault();
                if (best != null)
                {
                    return best;
                }
            }

            return null;
        }

        void Wear(ItemDefinition item)
        {
            if (item != null)
            {
                kit.Pieces.Add(item);
            }
        }

        // A one-handed weapon and a shield, the kit most players will carry. The heaviest two-handers trade the shield for damage.
        Wear(gear.Where(item => item.Tier <= tier && item.IsWeapon && !item.Weapon.IsShield && !item.Weapon.RequiresBothHands)
            .OrderByDescending(item => item.Tier).ThenByDescending(item => item.Weapon.Damage).ThenBy(item => item.name, StringComparer.Ordinal).FirstOrDefault());
        Wear(Best(item => item.IsWeapon && item.Weapon.IsShield));
        foreach (ArmorSlot slot in WornSlots)
        {
            Wear(Best(item => item.ArmorSlot == slot));
        }

        // One ring: the second finger is retired (GearSlots.IsRetired), so a kit can't wear two. Counted twice, the ring's
        // armor and crit made every tier's own kit look tougher and deadlier than the one a player can put on.
        Wear(Best(item => item.ArmorSlot == ArmorSlot.Finger));
        Measure(kit);
        LevelingSettings skills = LevelingSettings.Current;
        kit.SkillDamage = LevelingSettings.BonusAt(skills.SkillTrainedBy(ProficiencyTraining.Melee), StatType.MeleeDamage, skills.ExpectedLevel(tier), skills);
        kit.Damage *= 1f + kit.SkillDamage;
        return kit;
    }

    private static void Measure(Kit kit)
    {
        CombatRules rules = CombatRules.Default;
        float strength = 0f, crit = 0.05f;
        kit.Health = 100f;
        foreach (ItemDefinition piece in kit.Pieces)
        {
            kit.Score += GearScore.Of(piece);
            if (piece.IsWeapon && !piece.Weapon.IsShield)
            {
                kit.Damage = piece.Weapon.Damage;
            }

            foreach (StatBonus bonus in piece.StatBonuses)
            {
                switch (bonus.stat)
                {
                    case StatType.Armor:
                        kit.Armor += bonus.value;
                        break;
                    case StatType.MaxHealth:
                        kit.Health += bonus.value;
                        break;
                    case StatType.Strength:
                        strength += bonus.value;
                        break;
                    case StatType.PhysicalCritChance:
                        crit += bonus.value;
                        break;
                }
            }
        }

        kit.Damage *= (1f + strength * rules.PowerPerStrength * rules.DamagePerPower) * (1f + Mathf.Clamp01(crit) * 0.5f);
        kit.Reduction = rules.GetArmorReduction(kit.Armor);
    }

    /// <summary>
    /// The kit with everything a player can stack on it by the tier (Docs/PlayerStatsResearch.md #1): the stronger fight perk at each
    /// milestone reached by the level expected for the tier, in Melee and in the one crafting or gathering skill that adds most; the
    /// tier's two best meals (or as many as there are slots) and its best tonics (Tonic Slots); Well Rested. Counted with every fight stat
    /// (FightMath), where the plain kit counts gear armour, health, Strength and crit only. Empowering is left out: it raises the kit's gear
    /// score, so the planner already counts an empowered kit as the next tier's.
    /// </summary>
    public static Kit StackedKitOf(int tier, IReadOnlyList<ItemDefinition> gear) => StackedKitOf(tier, gear, true);

    /// <summary>
    /// <see cref="StackedKitOf(int, IReadOnlyList{ItemDefinition})"/>, with or without enchantments: they have a budget of their own on
    /// top of the rest (RaidTierTests: Enchantments Add At Most), so the two are measured apart.
    /// </summary>
    public static Kit StackedKitOf(int tier, IReadOnlyList<ItemDefinition> gear, bool enchanted) => StackedKitOf(tier, gear, enchanted, tier);

    /// <summary>
    /// The tier's gear with what can be stacked on it by <paramref name="reached"/>: the skill level expected then (perks, and the
    /// Runecraft an enchantment needs), the food and enchantments its raids hand out. With <paramref name="reached"/> one below the
    /// gear's tier it is the player arriving at the tier with what the last one gave them (the user, 2026-11-15: count what is within
    /// reach when; each tier hard at entry and mastered just before the next).
    /// </summary>
    public static Kit StackedKitOf(int tier, IReadOnlyList<ItemDefinition> gear, bool enchanted, int reached) =>
        StackedKitOf(tier, gear, enchanted, reached, true);

    /// <summary>
    /// The kit a player enters a tier with (the user, 2026-11-15: hard at entry, mastered just before the next): the tier's gear and what
    /// they keep from the tier before (perks, enchantments known), without what is eaten or slept for each raid. The tier's multipliers
    /// are tuned to this one (RaidTierTests); a player who eats and rests well finds it easier, and a mastered one is ready for the next.
    /// At tier I it is the plain kit: a new player has nothing yet.
    /// </summary>
    public static Kit EntryKitOf(int tier, IReadOnlyList<ItemDefinition> gear) =>
        tier <= 1 ? KitOf(tier, gear) : StackedKitOf(tier, gear, true, tier - 1, false);

    /// <summary>Everything within reach by the end of the tier, food and rest included: the tier mastered.</summary>
    public static Kit MasteredKitOf(int tier, IReadOnlyList<ItemDefinition> gear) => StackedKitOf(tier, gear, true, tier, true);

    public static Kit StackedKitOf(int tier, IReadOnlyList<ItemDefinition> gear, bool enchanted, int reached, bool consumables)
    {
        Kit plain = KitOf(tier, gear);
        var kit = new Kit { Tier = tier, Score = plain.Score, SkillDamage = plain.SkillDamage };
        kit.Pieces.AddRange(plain.Pieces);
        CombatRules rules = CombatRules.Default;
        var totals = new FightMath.Totals { MeleeDamage = plain.SkillDamage };
        float weapon = 0f;
        foreach (ItemDefinition piece in plain.Pieces)
        {
            if (piece.IsWeapon && !piece.Weapon.IsShield)
            {
                weapon = piece.Weapon.Damage;
            }

            foreach (StatBonus bonus in piece.StatBonuses)
            {
                // The kit's own resistances (Iron Mail's) are part of what the tier was tuned to, and the plain kit leaves them out too:
                // counted here they would read as stacking. Only what is stacked on top adds resistance.
                if (bonus.stat != StatType.PhysicalResistance && bonus.stat != StatType.BluntResistance && bonus.stat != StatType.SlashResistance
                    && bonus.stat != StatType.StabResistance)
                {
                    totals.Add(bonus);
                }
            }
        }

        float Worth(FightMath.Totals t) => t.Damage(weapon, rules) * t.Toughness(rules);
        void Take(System.Collections.Generic.IEnumerable<StatBonus> bonuses, FightMath.Totals into)
        {
            foreach (StatBonus bonus in bonuses)
            {
                into.Add(bonus);
            }
        }

        // Perks: Melee's, and the crafting or gathering skill that adds most, the stronger side at each milestone reached.
        LevelingSettings leveling = LevelingSettings.Current;
        int level = leveling.ExpectedLevel(Mathf.Max(1, reached));
        FightMath.Totals WithPerks(ProficiencyDefinition skill, FightMath.Totals from)
        {
            FightMath.Totals with = from.Copy();
            foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
            {
                if (milestone.level > level)
                {
                    continue;
                }

                FightMath.Totals first = with.Copy(), second = with.Copy();
                if (milestone.first != null) Take(milestone.first.Bonuses, first);
                if (milestone.second != null) Take(milestone.second.Bonuses, second);
                with = Worth(first) >= Worth(second) ? first : second;
            }

            return with;
        }

        var skills = AssetDatabase.FindAssets("t:ProficiencyDefinition").Select(id => AssetDatabase.LoadAssetAtPath<ProficiencyDefinition>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(skill => skill != null).ToList();
        ProficiencyDefinition melee = leveling.SkillTrainedBy(ProficiencyTraining.Melee);
        if (melee != null)
        {
            totals = WithPerks(melee, totals);
        }

        FightMath.Totals best = totals;
        foreach (ProficiencyDefinition skill in skills.Where(s => s.Group == ProficiencyGroup.Crafting || s.Group == ProficiencyGroup.Gathering))
        {
            FightMath.Totals with = WithPerks(skill, totals);
            if (Worth(with) > Worth(best))
            {
                best = with;
            }
        }

        totals = best;

        // Food: the best meals and tonics of the tier, as many as there are slots.
        List<ItemDefinition> food = AssetDatabase.FindAssets("t:ItemDefinition").Select(id => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(item => item != null && item.HasBoon && item.Tier <= Mathf.Max(1, reached)).ToList();
        SurvivalSettings survival = SurvivalSettings.Current;
        void Eat(bool tonics, int slots)
        {
            var left = food.Where(item => item.IsTonic == tonics).ToList();
            for (int i = 0; i < slots && left.Count > 0; i++)
            {
                ItemDefinition pick = left.OrderByDescending(item => { FightMath.Totals t = totals.Copy(); Take(item.BoonBonuses, t); return Worth(t); })
                    .ThenBy(item => item.name, StringComparer.Ordinal).First();
                Take(pick.BoonBonuses, totals);
                left.Remove(pick);
            }
        }

        if (consumables)
        {
            Eat(false, survival != null ? survival.MealSlots : 2);
            Eat(true, survival != null ? survival.TonicSlots : 1);
            // Well Rested (Docs/BaseProgression.md › Endurance): +10% health.
            totals.HealthShare += WellRestedHealth;
        }

        // Enchantments (Docs/EnchantingResearch.md #1): on each piece the one that fits and adds most, from those the tier's raids hand out
        // (or known from the start), each counted once however many pieces could carry it, as GearSet counts them. Their numbers only:
        // what a great enchantment does is a verb, kept out as attack speed and sustain are.
        var enchantments = !enchanted ? new List<EnchantmentDefinition>() : EssenceSettings.Current.Enchantments.Where(e => e != null
            && e.School != EssenceSchool.Relic && (e.KnownFromStart || e.Tier <= Mathf.Max(1, reached)) && e.Bonuses.Count > 0
            && e.RequiredRunecraft <= level).ToList();
        var used = new HashSet<EnchantmentDefinition>();
        foreach (ItemDefinition piece in plain.Pieces)
        {
            EnchantmentDefinition pick = null;
            float most = Worth(totals);
            foreach (EnchantmentDefinition enchantment in enchantments)
            {
                if (used.Contains(enchantment) || !enchantment.Fits(piece))
                {
                    continue;
                }

                FightMath.Totals with = totals.Copy();
                Take(enchantment.Bonuses, with);
                float worth = Worth(with);
                if (worth > most + 1e-4f)
                {
                    most = worth;
                    pick = enchantment;
                }
            }

            if (pick != null)
            {
                Take(pick.Bonuses, totals);
                used.Add(pick);
                kit.Enchantments.Add(pick);
            }
        }

        kit.Damage = totals.Damage(weapon, rules);
        kit.Armor = totals.Armor;
        kit.Health = totals.FullHealth;
        kit.Reduction = rules.GetArmorReduction(totals.Armor);
        kit.Resist = Mathf.Clamp(totals.Resist, 0f, 0.9f);
        return kit;
    }

    /// <summary>The health share Well Rested adds (Docs/BaseProgression.md › Endurance).</summary>
    public const float WellRestedHealth = 0.1f;

    /// <summary>Hits a kit needs to kill a creature of this health and armor.</summary>
    public static float HitsToKill(Kit kit, float health, float armor, float healthScale) =>
        health * healthScale / Mathf.Max(0.01f, kit.Damage * (1f - CombatRules.Default.GetArmorReduction(armor)));

    /// <summary>Hits of this strength a kit can take.</summary>
    public static float HitsToDie(Kit kit, float damage, float powerScale) => kit.Toughness / Mathf.Max(0.01f, damage * powerScale);
}

/// <summary>The raid editor's Difficulty Tiers panel (Rules tab): one row per tier, and what each comes to against the gear of its tier.</summary>
internal static class RaidTierEditor
{
    private const float ReferenceHealth = 60f;
    private const float ReferenceArmor = 5f;
    private const float ReferenceDamage = 15f;
    private const float ColumnWidth = 62f;

    private static readonly (string Label, string Field)[] Columns =
    {
        ("Tier", "tier"), ("Score", "recommendedGearScore"), ("At least", "minimumGearScore"), ("Health x", "enemyHealth"),
        ("Damage x", "enemyPower"), ("Enemies x", "enemyDensity"), ("Loot x", "loot"), ("Drops to", "dropsUpToTier"),
    };

    private static void Small(SerializedProperty row, string field, string label)
    {
        GUILayout.Label(new GUIContent(label, TooltipOf(field)), EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
        EditorGUILayout.PropertyField(row.FindPropertyRelative(field), GUIContent.none, GUILayout.Width(44f));
    }

    private static string TooltipOf(string field) =>
        typeof(RaidDifficulty).GetField(field)?.GetCustomAttributes(typeof(TooltipAttribute), false).Cast<TooltipAttribute>().FirstOrDefault()?.tooltip;

    public static void Draw(RaidDefinition raid, SerializedObject data)
    {
        ContentUtility.BeginPanel("Difficulty tiers", "The tiers the portal offers, lowest first. Each expects a gear score and scales the creatures to it. " +
            "Which creatures, resources and trees appear at each tier is set on the Enemies, Resources and World tabs (From / To).");
        SerializedProperty list = data.FindProperty("difficulties");
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach ((string label, string field) in Columns)
            {
                GUILayout.Label(new GUIContent(label, TooltipOf(field)), EditorStyles.miniBoldLabel, GUILayout.Width(ColumnWidth));
            }
        }

        int remove = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach ((string _, string field) in Columns)
                {
                    EditorGUILayout.PropertyField(row.FindPropertyRelative(field), GUIContent.none, GUILayout.Width(ColumnWidth));
                }

                if (GUILayout.Button(new GUIContent("×", "Remove this tier"), EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    remove = i;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Briefing", TooltipOf("briefing")), EditorStyles.miniLabel, GUILayout.Width(ColumnWidth));
                SerializedProperty briefing = row.FindPropertyRelative("briefing");
                briefing.stringValue = EditorGUILayout.TextField(briefing.stringValue);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Elites", TooltipOf("eliteCount")), EditorStyles.miniLabel, GUILayout.Width(ColumnWidth));
                SerializedProperty count = row.FindPropertyRelative("eliteCount");
                Vector2Int range = count.vector2IntValue;
                range.x = Mathf.Max(0, EditorGUILayout.IntField(range.x, GUILayout.Width(32f)));
                GUILayout.Label("to", EditorStyles.miniLabel, GUILayout.Width(14f));
                range.y = Mathf.Max(range.x, EditorGUILayout.IntField(range.y, GUILayout.Width(32f)));
                count.vector2IntValue = range;
                Small(row, "eliteAffixes", "Affixes each");
                Small(row, "hunterEliteChance", "Hunter chance");
                Small(row, "eliteRelicChance", "Relic chance");
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(row.FindPropertyRelative("eliteLoot"), new GUIContent("Elite loot", TooltipOf("eliteLoot")), true);
            }

            EditorGUILayout.Space(3f);
        }

        if (remove >= 0)
        {
            list.DeleteArrayElementAtIndex(remove);
        }

        if (GUILayout.Button("+ Add Tier", EditorStyles.miniButton, GUILayout.Width(90f)))
        {
            list.arraySize++;
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("tier").intValue = Mathf.Min(Tiers.Max, list.arraySize);
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(data.FindProperty("eliteAffixes"), new GUIContent("Elite affixes", "What can make a creature an elite here. Each has a From Tier of its own."), true);
        data.ApplyModifiedProperties();
        DrawBalance(raid);
        ContentUtility.EndPanel();
    }

    // A table rather than prose: it is what the multipliers above are tuned against.
    private static void DrawBalance(RaidDefinition raid)
    {
        if (raid.Difficulties.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("What it comes to", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"The best kit of each tier against a reference creature ({ReferenceHealth:0} health, {ReferenceArmor:0} armor, hits for {ReferenceDamage:0}). " +
                                   "'Own kit' should stay about the same down the table. 'Kit before' shows what skipping a tier costs. 'Fully stacked' adds every perk, meal, tonic and Well Rested a player can have by the tier " +
                                   "(RaidTierTests holds it within x1.5 toughness and x1.35 damage of the kit). Suggested: the scaling that keeps hits level with tier I.",
            ContentUtility.WrappedMiniLabel);
        List<ItemDefinition> gear = GearBalance.AllGear();
        Row(EditorStyles.miniBoldLabel, "Tier", "Kit score", "Damage", "Armor", "Health", "Own kit: hits to kill / die", "Kit before: kill / die", "Fully stacked: kill / die", "Suggested health / power");
        GearBalance.Kit first = GearBalance.KitOf(raid.Difficulties[0].tier, gear);
        foreach (RaidDifficulty tier in raid.Difficulties)
        {
            GearBalance.Kit kit = GearBalance.KitOf(tier.tier, gear);
            GearBalance.Kit before = GearBalance.KitOf(tier.tier - 1, gear);
            Row(EditorStyles.miniLabel, Tiers.ToRoman(tier.tier), kit.Score.ToString(), kit.Damage.ToString("0.#"), kit.Armor.ToString("0"), kit.Health.ToString("0"),
                Hits(kit, tier), Hits(before, tier), Hits(GearBalance.StackedKitOf(tier.tier, gear), tier), $"x{kit.Damage / first.Damage:0.##} / x{kit.Toughness / first.Toughness:0.##}");
        }
    }

    private static string Hits(GearBalance.Kit kit, RaidDifficulty tier) =>
        kit.Damage <= 0f ? "-" : $"{GearBalance.HitsToKill(kit, ReferenceHealth, ReferenceArmor, tier.enemyHealth):0.#} / {GearBalance.HitsToDie(kit, ReferenceDamage, tier.enemyPower):0.#}";

    private static void Row(GUIStyle style, params string[] cells)
    {
        float[] widths = { 34f, 58f, 52f, 46f, 46f, 130f, 110f, 130f };
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < cells.Length; i++)
            {
                GUILayout.Label(cells[i], style, GUILayout.Width(widths[i]));
            }
        }
    }
}
