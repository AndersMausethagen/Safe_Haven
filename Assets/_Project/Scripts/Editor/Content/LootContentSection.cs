using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The Content Editor's Loot Tables: every table in Data/Loot, grouped by kind (creatures, caches, raids, elites). Each one shows its
/// rows on one line apiece, what it gives at each raid tier (and at home), and who uses it: monsters, prefabs, raids.
/// </summary>
public sealed class LootTableSection : ContentSection
{
    public override string Title => "Loot Tables";
    public override string Noun => "Loot Table";
    public override string SidebarGroup => "Loot";
    public override Type AssetType => typeof(LootTable);
    public override string Folder => "Assets/_Project/Data/Loot";
    public override string Help =>
        "What creatures, caches and raids give. One table per creature (its definition and prefab share it), per kind of cache and per " +
        "raid (Regional Loot: what only that raid holds, rolled with every cache searched there; Elite Loot: what its elites give). " +
        "Each row drops on its own chance. From/To Tier limit a row to raid tiers (0: any; a row with a From Tier never drops outside a " +
        "raid). Rows scale with the raid's loot multiplier (each tier's Loot, its modifiers) unless they keep a Fixed Amount. A raid's " +
        "Loot & Events tab shows everything that drops there, tier by tier; Safe Haven › Loot › Write Loot Overview writes it all to " +
        "Docs/LootTables.md.";

    public override string ListGroup(Object asset) => ((LootTable)asset).Kind switch
    {
        LootTableKind.Creature => "Creatures",
        LootTableKind.Cache => "Caches",
        LootTableKind.Raid => "Raids",
        LootTableKind.Elite => "Elites",
        _ => "Other",
    };

    public override string Label(Object asset) => ((LootTable)asset).DisplayName;

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var table = (LootTable)asset;
        foreach (ItemDropEntry entry in table.Drops.Entries)
        {
            if (entry == null || entry.Drop == null)
            {
                problems.Add("A row drops nothing: set an item or a resource.");
            }
            else if (entry.Probability <= 0f)
            {
                problems.Add($"{entry.DropName} has no chance to drop.");
            }
        }

        if (LootOverview.UsersOf(table).Count == 0)
        {
            problems.Add("Nothing uses this table yet: no monster, cache, breakable or raid points at it.");
        }

        return problems;
    }

    public override void DrawExtras(Object asset)
    {
        var table = (LootTable)asset;
        ContentUtility.BeginPanel("By tier", "The average a single roll gives, chance included, before the raid's loot multiplier. " +
                                             "Home: killed in the Haven, where raid-only parts and Cursed Essence stay on the body.");
        LootOverview.DrawTierGrid(table);
        ContentUtility.EndPanel();

        List<(Object owner, string text)> users = LootOverview.UsersOf(table);
        ContentUtility.BeginPanel("Used by", "Click to select it.");
        if (users.Count == 0)
        {
            EditorGUILayout.LabelField("Nothing yet.", ContentUtility.WrappedMiniLabel);
        }

        foreach ((Object owner, string text) in users)
        {
            if (owner != null && GUILayout.Button(text, EditorStyles.linkLabel))
            {
                ContentUtility.Reveal(owner);
            }
        }

        if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
        {
            LootOverview.Invalidate();
        }

        ContentUtility.EndPanel();
    }
}

/// <summary>
/// Reads the game's loot as players meet it: who uses a table, what a table gives at each tier, and what drops in a raid tier by
/// tier (its creatures, its caches, its own regional finds, its elites). Drawn in the Content Editor and written to Docs/LootTables.md.
/// </summary>
public static class LootOverview
{
    private const string DocPath = "Docs/LootTables.md";

    // Who points at which table, and each raid's lines by tier: built once and dropped whenever the project changes (2026-11-17 review:
    // scanning every prefab per table on each list rebuild, and the raid lines on every repaint, stalled the Content Editor).
    private static Dictionary<LootTable, List<(Object owner, string text)>> users;
    private static readonly Dictionary<(RaidDefinition, int), List<string>> RaidLineCache = new Dictionary<(RaidDefinition, int), List<string>>();

    [InitializeOnLoadMethod]
    private static void WatchProject() => EditorApplication.projectChanged += Invalidate;

    public static void Invalidate()
    {
        users = null;
        RaidLineCache.Clear();
    }

    /// <summary>Monsters, prefabs and raids that point at the table.</summary>
    public static List<(Object owner, string text)> UsersOf(LootTable table)
    {
        users ??= BuildUsers();
        return users.TryGetValue(table, out List<(Object, string)> found) ? found : new List<(Object, string)>();
    }

    private static Dictionary<LootTable, List<(Object owner, string text)>> BuildUsers()
    {
        var index = new Dictionary<LootTable, List<(Object, string)>>();
        void Add(LootTable table, Object owner, string text)
        {
            if (table == null)
            {
                return;
            }

            if (!index.TryGetValue(table, out List<(Object, string)> list))
            {
                index[table] = list = new List<(Object, string)>();
            }

            if (!list.Exists(entry => entry.Item1 == owner && entry.Item2 == text))
            {
                list.Add((owner, text));
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (monster != null)
            {
                Add(monster.LootTable, monster, "Monster: " + ContentUtility.DisplayName(monster));
            }
        }

        foreach (RaidDefinition raid in Raids())
        {
            Add(raid.RegionalLoot, raid, $"Raid: {raid.DisplayName}'s regional loot");
            Add(raid.EliteLoot, raid, $"Raid: {raid.DisplayName}'s elites");
        }

        // Only the prefabs that point at a loot table are loaded (their direct dependencies say so), not every prefab with its meshes.
        var tablePaths = new HashSet<string>(AssetDatabase.FindAssets("t:LootTable").Select(AssetDatabase.GUIDToAssetPath));
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs", "Assets/_Project/Data" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.GetDependencies(path, true).Any(tablePaths.Contains))
            {
                continue;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            foreach (LootCache cache in prefab.GetComponentsInChildren<LootCache>(true))
            {
                Add(cache.Table, prefab, "Cache: " + prefab.name);
            }

            foreach (LootOnDeath body in prefab.GetComponentsInChildren<LootOnDeath>(true))
            {
                Add(body.Table, prefab, "Prefab: " + prefab.name);
            }
        }

        return index;
    }

    /// <summary>A grid: one row per entry, one column for home and each tier, each cell the average one roll gives.</summary>
    public static void DrawTierGrid(LootTable table)
    {
        const float NameWidth = 170f;
        const float CellWidth = 46f;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("", GUILayout.Width(NameWidth));
            GUILayout.Label("Home", EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));
            for (int tier = 1; tier <= Tiers.Max; tier++)
            {
                GUILayout.Label(Tiers.ToRoman(tier), EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));
            }
        }

        foreach (ItemDropEntry entry in table.Drops.Entries)
        {
            if (entry == null || entry.Drop == null)
            {
                continue;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                string name = entry.DropName + (entry.Item != null && entry.Item.RaidOnly ? " ◆" : "") + (entry.FixedAmount ? " (fixed)" : "");
                GUILayout.Label(new GUIContent(name, entry.Item != null && entry.Item.RaidOnly ? "Raid only: never dropped at home." : null),
                    EditorStyles.miniLabel, GUILayout.Width(NameWidth));
                GUILayout.Label(Cell(entry, 0), EditorStyles.miniLabel, GUILayout.Width(CellWidth));
                for (int tier = 1; tier <= Tiers.Max; tier++)
                {
                    GUILayout.Label(Cell(entry, tier), EditorStyles.miniLabel, GUILayout.Width(CellWidth));
                }
            }
        }
    }

    private static string Cell(ItemDropEntry entry, int tier)
    {
        bool home = tier == 0;
        bool held = home && ((entry.Item != null && entry.Item.RaidOnly) || IsCursedEssence(entry.Resource));
        return !entry.AppearsAt(tier) || held ? "–" : entry.AverageAmount.ToString("0.##");
    }

    private static bool IsCursedEssence(ResourceDefinition resource) =>
        resource != null && SettlementSettings.Current != null && resource == SettlementSettings.Current.CursedEssence;

    /// <summary>The raid's loot tier by tier, as foldouts in its Loot & Events tab.</summary>
    public static void DrawRaid(RaidDefinition raid)
    {
        foreach (RaidDifficulty difficulty in raid.Difficulties)
        {
            string key = $"SafeHaven.Loot.{raid.name}.{difficulty.tier}";
            bool open = SessionState.GetBool(key, false);
            bool now = EditorGUILayout.Foldout(open, $"Tier {Tiers.ToRoman(difficulty.tier)}: loot ×{difficulty.loot:0.##}, drops up to tier {Tiers.ToRoman(difficulty.DropCap)}", true);
            if (now != open)
            {
                SessionState.SetBool(key, now);
            }

            if (!now)
            {
                continue;
            }

            EditorGUI.indentLevel++;
            foreach (string line in RaidLines(raid, difficulty))
            {
                EditorGUILayout.LabelField(line, ContentUtility.WrappedMiniLabel);
            }

            EditorGUI.indentLevel--;
        }
    }

    /// <summary>What drops in a raid at one tier: its creatures, caches, regional finds and elites, each with its rows there.</summary>
    public static List<string> RaidLines(RaidDefinition raid, RaidDifficulty difficulty)
    {
        if (!RaidLineCache.TryGetValue((raid, difficulty.tier), out List<string> lines))
        {
            RaidLineCache[(raid, difficulty.tier)] = lines = BuildRaidLines(raid, difficulty);
        }

        return lines;
    }

    private static List<string> BuildRaidLines(RaidDefinition raid, RaidDifficulty difficulty)
    {
        int tier = difficulty.tier;
        var lines = new List<string>();
        var creatures = new Dictionary<string, List<string>>();
        var caches = new Dictionary<string, List<string>>();
        var rows = new Dictionary<string, ItemDropTable>();
        void Take(IEnumerable<SpawnTable.Entry> entries)
        {
            foreach (SpawnTable.Entry entry in entries)
            {
                if (entry.prefab == null || entry.weight <= 0f || !entry.AppearsAt(tier))
                {
                    continue;
                }

                foreach (LootOnDeath body in entry.prefab.GetComponentsInChildren<LootOnDeath>(true))
                {
                    if (body.GetComponentInParent<MonsterAI>(true) != null)
                    {
                        Add(creatures, rows, body.Table != null ? body.Table.DisplayName : body.name, body.Drops, entry.prefab.name);
                    }
                }

                foreach (LootCache cache in entry.prefab.GetComponentsInChildren<LootCache>(true))
                {
                    Add(caches, rows, cache.Table != null ? cache.Table.DisplayName : entry.prefab.name, cache.Drops, entry.prefab.name);
                }
            }
        }

        foreach (RaidSpawnList list in raid.SpawnLists)
        {
            Take(list.entries);
        }

        // Roaming creatures: the raid's own spawn tables. The scene's spawn groups are all bound to the lists above, so their own
        // tables never spawn; what the scene places by hand (an apex) is added at every tier.
        foreach (SpawnTable table in SpawnTablesOf(raid))
        {
            Take(table.Entries);
        }

        foreach (GameObject placed in PlacedIn(raid))
        {
            foreach (LootOnDeath body in placed.GetComponentsInChildren<LootOnDeath>(true))
            {
                Add(creatures, rows, (body.Table != null ? body.Table.DisplayName : body.name) + ", placed", body.Drops, placed.name);
            }
        }

        lines.Add("Creatures:");
        foreach (KeyValuePair<string, List<string>> pair in creatures.OrderBy(p => p.Key))
        {
            lines.Add($"  {pair.Key} ({string.Join(", ", pair.Value.Distinct())}): {Describe(rows[pair.Key], tier, difficulty)}");
        }

        lines.Add("Caches:");
        foreach (KeyValuePair<string, List<string>> pair in caches.OrderBy(p => p.Key))
        {
            lines.Add($"  {pair.Key} ({string.Join(", ", pair.Value.Distinct())}): {Describe(rows[pair.Key], tier, difficulty)}");
        }

        lines.Add(raid.RegionalLoot != null
            ? $"Only here, with every cache ({raid.RegionalLoot.DisplayName}): {Describe(raid.RegionalLoot.Drops, tier, difficulty)}"
            : "Only here: no regional loot table.");
        ItemDropTable elites = raid.EliteLoot != null ? raid.EliteLoot.Drops : difficulty.eliteLoot;
        lines.Add($"Elites ({difficulty.eliteCount.x}-{difficulty.eliteCount.y} a run, relic {difficulty.eliteRelicChance:0%}): {Describe(elites, tier, difficulty)}");
        return lines;
    }

    // Spawn tables the raid asset points at (its roamers).
    private static IEnumerable<SpawnTable> SpawnTablesOf(RaidDefinition raid)
    {
        var found = new HashSet<SpawnTable>();
        SerializedProperty property = new SerializedObject(raid).GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is SpawnTable table)
            {
                found.Add(table);
            }
        }

        return found;
    }

    // Creature prefabs the raid scene places by hand (its scene's direct dependencies that are monsters).
    private static IEnumerable<GameObject> PlacedIn(RaidDefinition raid)
    {
        string scene = AssetDatabase.FindAssets(raid.SceneName + " t:Scene").Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == raid.SceneName);
        if (scene == null)
        {
            yield break;
        }

        foreach (string path in AssetDatabase.GetDependencies(scene, false))
        {
            if (path.EndsWith(".prefab") && AssetDatabase.LoadAssetAtPath<GameObject>(path) is GameObject prefab
                && prefab.GetComponentInChildren<MonsterAI>(true) != null && prefab.GetComponentInChildren<LootOnDeath>(true) != null)
            {
                yield return prefab;
            }
        }
    }

    private static void Add(Dictionary<string, List<string>> into, Dictionary<string, ItemDropTable> rows, string name, ItemDropTable table, string from)
    {
        if (!into.TryGetValue(name, out List<string> sources))
        {
            into[name] = sources = new List<string>();
            rows[name] = table;
        }

        sources.Add(from);
    }

    /// <summary>A table's rows at a tier, as "Wolf Fang 1 (25%), Rough Hide 1-2"; capped rows the tier can't give are left out.</summary>
    public static string Describe(ItemDropTable table, int tier, RaidDifficulty difficulty = null)
    {
        var parts = new List<string>();
        foreach (ItemDropEntry entry in table.Entries)
        {
            if (entry == null || entry.Drop == null || !entry.AppearsAt(tier) || (difficulty != null && entry.Item != null && !difficulty.Allows(entry.Item)))
            {
                continue;
            }

            string amount = entry.MinQuantity == entry.MaxQuantity ? entry.MinQuantity.ToString() : $"{entry.MinQuantity}-{entry.MaxQuantity}";
            string chance = entry.Probability >= 1f ? "" : $" ({Mathf.RoundToInt(entry.Probability * 100f)}%)";
            parts.Add($"{entry.DropName} {amount}{chance}");
        }

        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    private static IEnumerable<RaidDefinition> Raids()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (raid != null)
            {
                yield return raid;
            }
        }
    }

    /// <summary>Everything above as Markdown: each raid tier by tier, then every table with its rows by tier.</summary>
    public static string Markdown()
    {
        Invalidate();
        var text = new StringBuilder();
        text.AppendLine("# Loot tables");
        text.AppendLine();
        text.AppendLine("Written from the game's data by Safe Haven › Loot › Write Loot Overview; edit the tables in the Content Editor's Loot " +
                        "Tables (Data/Loot), not here. Amounts are before the tier's loot multiplier. ◆ marks a raid-only part.");
        foreach (RaidDefinition raid in Raids().OrderBy(r => r.Tier))
        {
            text.AppendLine();
            text.AppendLine($"## {raid.DisplayName}");
            foreach (RaidDifficulty difficulty in raid.Difficulties)
            {
                text.AppendLine();
                text.AppendLine($"### Tier {Tiers.ToRoman(difficulty.tier)} (loot ×{difficulty.loot:0.##}, drops up to tier {Tiers.ToRoman(difficulty.DropCap)})");
                text.AppendLine();
                foreach (string line in RaidLines(raid, difficulty))
                {
                    if (line.EndsWith(":"))
                    {
                        text.AppendLine();
                        text.AppendLine($"**{line}**");
                        text.AppendLine();
                    }
                    else
                    {
                        text.AppendLine("- " + line.Trim());
                    }
                }
            }
        }

        var tables = AssetDatabase.FindAssets("t:LootTable").Select(g => AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(t => t != null).OrderBy(t => t.Kind).ThenBy(t => t.DisplayName);
        LootTableKind? kind = null;
        foreach (LootTable table in tables)
        {
            if (kind != table.Kind)
            {
                kind = table.Kind;
                text.AppendLine();
                text.AppendLine($"## {table.Kind} tables");
            }

            text.AppendLine();
            text.AppendLine($"### {table.DisplayName}");
            if (!string.IsNullOrWhiteSpace(table.Notes))
            {
                text.AppendLine();
                text.AppendLine(table.Notes.Trim());
            }

            text.AppendLine();
            text.AppendLine("| Drop | Amount | Chance | Tiers | Home |");
            text.AppendLine("|---|---|---|---|---|");
            foreach (ItemDropEntry entry in table.Drops.Entries)
            {
                if (entry == null || entry.Drop == null)
                {
                    continue;
                }

                bool raidOnly = entry.Item != null && entry.Item.RaidOnly;
                string tiers = entry.FromTier <= 0 && entry.ToTier <= 0 ? "any"
                    : $"{(entry.FromTier > 0 ? Tiers.ToRoman(entry.FromTier) : "I")}-{(entry.ToTier > 0 ? Tiers.ToRoman(entry.ToTier) : Tiers.ToRoman(Tiers.Max))}";
                string amount = (entry.MinQuantity == entry.MaxQuantity ? entry.MinQuantity.ToString() : $"{entry.MinQuantity}-{entry.MaxQuantity}") + (entry.FixedAmount ? " (fixed)" : "");
                string home = Cell(entry, 0) == "–" ? "no" : "yes";
                text.AppendLine($"| {entry.DropName}{(raidOnly ? " ◆" : "")} | {amount} | {Mathf.RoundToInt(entry.Probability * 100f)}% | {tiers} | {home} |");
            }
        }

        return text.ToString();
    }

    [MenuItem("Safe Haven/Loot/Write Loot Overview")]
    private static void WriteOverview()
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DocPath);
        File.WriteAllText(path, Markdown());
        Debug.Log("Loot overview written to " + DocPath);
    }
}

/// <summary>
/// A loot row on one line: what drops (an item or a resource: drag either in), how many, the chance, the tiers it drops at and
/// whether the raid's multiplier leaves it alone. Two lines when the inspector is narrow.
/// </summary>
[CustomPropertyDrawer(typeof(ItemDropEntry))]
public sealed class ItemDropEntryDrawer : PropertyDrawer
{
    private const float Numbers = 268f;

    private static bool Narrow => EditorGUIUtility.currentViewWidth < 560f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        EditorGUIUtility.singleLineHeight * (Narrow ? 2 : 1) + EditorGUIUtility.standardVerticalSpacing * (Narrow ? 2 : 1);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty item = property.FindPropertyRelative("item");
        SerializedProperty resource = property.FindPropertyRelative("resource");
        SerializedProperty min = property.FindPropertyRelative("minQuantity");
        SerializedProperty max = property.FindPropertyRelative("maxQuantity");
        SerializedProperty chance = property.FindPropertyRelative("probability");
        SerializedProperty from = property.FindPropertyRelative("fromTier");
        SerializedProperty to = property.FindPropertyRelative("toTier");
        SerializedProperty fixedAmount = property.FindPropertyRelative("fixedAmount");

        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float line = EditorGUIUtility.singleLineHeight;
        var row = new Rect(position.x, position.y + 1f, position.width, line);
        Rect dropRect = Narrow ? row : new Rect(row.x, row.y, row.width - Numbers - 4f, line);
        Object current = item.objectReferenceValue != null ? item.objectReferenceValue : resource.objectReferenceValue;
        EditorGUI.BeginChangeCheck();
        Object picked = EditorGUI.ObjectField(dropRect, current, typeof(ScriptableObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (picked == null || picked is ItemDefinition)
            {
                item.objectReferenceValue = picked;
                resource.objectReferenceValue = null;
            }
            else if (picked is ResourceDefinition)
            {
                resource.objectReferenceValue = picked;
                item.objectReferenceValue = null;
            }
        }

        GUI.Label(dropRect, new GUIContent("", "What drops: an item, or a resource such as Cursed Essence. Drag either in."));
        float x = Narrow ? row.x : dropRect.xMax + 4f;
        float y = Narrow ? row.y + line + EditorGUIUtility.standardVerticalSpacing : row.y;
        Rect Next(float width)
        {
            var rect = new Rect(x, y, width, line);
            x += width + 2f;
            return rect;
        }

        Int(Next(30f), min, 0, 999, "The least that drops.");
        GUI.Label(Next(8f), "–", EditorStyles.miniLabel);
        Int(Next(30f), max, 0, 999, "The most that drops.");
        x += 4f;
        Rect chanceRect = Next(34f);
        int percent = Mathf.RoundToInt(chance.floatValue * 100f);
        int newPercent = Mathf.Clamp(EditorGUI.IntField(chanceRect, percent), 0, 100);
        if (newPercent != percent)
        {
            chance.floatValue = newPercent / 100f;
        }

        GUI.Label(chanceRect, new GUIContent("", "Chance in percent that this row drops at all."));
        GUI.Label(Next(14f), "%", EditorStyles.miniLabel);
        x += 4f;
        GUI.Label(Next(22f), new GUIContent("tier", "Raid tiers it drops at: from – to. 0: any (and outside raids, for From)."), EditorStyles.miniLabel);
        Int(Next(22f), from, 0, Tiers.Max, "The lowest raid tier it drops at. 0: any tier, and outside raids too.");
        GUI.Label(Next(8f), "–", EditorStyles.miniLabel);
        Int(Next(22f), to, 0, Tiers.Max, "The highest raid tier it drops at. 0: no limit.");
        x += 4f;
        fixedAmount.boolValue = EditorGUI.ToggleLeft(Next(52f), new GUIContent("fixed", "Never scaled by the raid's loot multiplier: a trophy stays one."), fixedAmount.boolValue, EditorStyles.miniLabel);
        if (max.intValue < min.intValue)
        {
            max.intValue = min.intValue;
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    private static void Int(Rect rect, SerializedProperty property, int least, int most, string tooltip)
    {
        int value = EditorGUI.IntField(rect, property.intValue);
        property.intValue = Mathf.Clamp(value, least, most);
        GUI.Label(rect, new GUIContent("", tooltip));
    }
}
