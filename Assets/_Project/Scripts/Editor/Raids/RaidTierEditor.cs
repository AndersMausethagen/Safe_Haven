using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What a fight comes to at one raid tier, worked out from the real assets: the best kit of a tier, its gear score, its damage and how
/// much it can take, and from that the hits to kill and the hits to die against a reference creature. It is how a tier gets tuned to its
/// gear score: at the recommended kit those hits should stay about the same from tier to tier, and the kit before should feel it.
/// Plain arithmetic: it ignores attack speed, skills and timing, so read the numbers against each other, not as a fight's length.
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
        public readonly List<ItemDefinition> Pieces = new List<ItemDefinition>();

        /// <summary>How much damage it takes to kill: health over what armor lets through.</summary>
        public float Toughness => Health / Mathf.Max(0.01f, 1f - Reduction);
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

        ItemDefinition ring = Best(item => item.ArmorSlot == ArmorSlot.Finger);
        Wear(ring);
        Wear(ring);
        Measure(kit);
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
                                   "'Own kit' should stay about the same down the table. 'Kit before' shows what skipping a tier costs. Suggested: the scaling that keeps hits level with tier I.",
            ContentUtility.WrappedMiniLabel);
        List<ItemDefinition> gear = GearBalance.AllGear();
        Row(EditorStyles.miniBoldLabel, "Tier", "Kit score", "Damage", "Armor", "Health", "Own kit: hits to kill / die", "Kit before: kill / die", "Suggested health / power");
        GearBalance.Kit first = GearBalance.KitOf(raid.Difficulties[0].tier, gear);
        foreach (RaidDifficulty tier in raid.Difficulties)
        {
            GearBalance.Kit kit = GearBalance.KitOf(tier.tier, gear);
            GearBalance.Kit before = GearBalance.KitOf(tier.tier - 1, gear);
            Row(EditorStyles.miniLabel, Tiers.ToRoman(tier.tier), kit.Score.ToString(), kit.Damage.ToString("0.#"), kit.Armor.ToString("0"), kit.Health.ToString("0"),
                Hits(kit, tier), Hits(before, tier), $"x{kit.Damage / first.Damage:0.##} / x{kit.Toughness / first.Toughness:0.##}");
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
