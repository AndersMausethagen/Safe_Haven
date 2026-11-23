using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Loose ends in the game's content, the kind nothing complains about until a playtest: a reference to an asset or object
/// that is gone (the Drinking Spot), a recipe or building nobody can learn, an item something asks for that nothing gives,
/// a monster named somewhere that nothing spawns. ContentAuditTests asks for none, and so does the build (SafeHavenBuild).
/// <para>
/// It reads every data asset and prefab once (<see cref="Index"/>) and sorts each field that points at an item, recipe,
/// building or monster by what it means: a source (a recipe's output, a drop, a reward), a use (a cost, an ingredient), a
/// teacher, or neither. A field it doesn't know fails <c>EveryContentField_IsSorted</c> until it is added below, so a new
/// system can't hide from the audit.
/// </para>
/// </summary>
public static class ContentAudit
{
    // ---- What each field means. "Type.path" with arrays as [] ; a prefix match.

    private static readonly string[] ItemSources =
    {
        "RecipeDefinition.output", "LootCache.drops", "LootOnDeath.drops", "MonsterDefinition.drops", "ResourceSource.drops", "LootTable.drops", "ItemDefinition.breaksInto",
        "GatheringNodeDefinition.yields", "GatheringNodeDefinition.burstYields", "ChoppableTree.wood", "TreeChoppingSettings.woodByTier",
        "TreeChoppingSettings.limbingItem", "FishingSettings.fish", "FireflyProfile.catchable", "HavenRequestDefinition.reward",
        "QuestDefinition.rewardItems", "RaidDefinition.difficulties[].eliteLoot", "RaidEventDefinition.choices[].outcomes",
        "RelicSettings.gear", "TerritorySettings.regions[].tithe", "TradeSettings.offers", "TransportDefinition.kit",
        "WaterSource.fillItem", "SurvivalSettings.openWaterFillItem", "PrologueDefinition.startingArmor", "PrologueDefinition.startingBackpack", "PrologueDefinition.sticks",
        "PrologueDefinition.stones", "ItemWorldObject.definition", "RaidLayoutRules.pondFillItem",
        // What is left to take once at a Haven place (PlaceFind).
        "HavenPlaceVerbs.finds[].item",
        // Inscriptions are made by drops, caches and rubbings from the one item (Inscriptions.Make).
        "EssenceSettings.inscription",
        // What someone at home made while the player was away, put in the chests on coming home (AwayGifts).
        "HomecomingSettings.gifts[].item",
        // What a party to a held land brings home from a raid (LandParties), and a rock a mason broke while the player was away
        // (RingWork's quarry rings).
        "TerritorySettings.parties[].yields[].item", "WorkerSettings.awayRockYield[].item",
        // What an outpost at a Secure land sends home each raid (Outposts).
        "TerritorySettings.regions[].outpost[].item",
        // What a foray may bring home (Forays): residents raiding on their own.
        "ForaySettings.destinations[].loot[].item",
    };

    private static readonly string[] ItemUses =
    {
        "RecipeDefinition.itemCost", "BuildingDefinition.constructionItems", "BuildingDefinition.upgrades[].cost",
        "RaidEventDefinition.choices[].cost", "HavenRequestDefinition.wanted", "SettlementSettings.portalLevels",
        "QuestDefinition.objectives", "FishingSettings.bait", "LanternDefinition.refill", "JobDefinition.workTool", "TerritorySettings.pactRune",
        // Raising an outpost (Outposts).
        "TerritorySettings.outpostCost",
        "HorseDefinition.lasso", "HorseDefinition.barley",
        // A carved stone's rubbing uses one up (CarvedStone).
        "EssenceSettings.blankParchment",
        // A hard mark healed by an item given in talk, used up (ResidentMarks: Old Wound and a Mending Draught).
        "MarkDefinition.healedBy",
        // A comfort bought for a house from a resident's window (HomeLife.Furnish).
        "HomeLifeSettings.comforts[].cost[].item",
        // A boss encounter's gate takes its price (the Hearth Ember into Guardian's Hollow).
        "BossEncounterDefinition.gateItems[].item",
        // A torch burns its fuel each night (TorchFuel: Charcoal).
        "TorchFuel.fuel",
        // A wall piece's torch (WallFittings).
        "BuildingDefinition.wallTorchCost",
        // A building module's price (BuildingModules.Fit).
        "BuildingModuleDefinition.cost",
    };

    private static readonly string[] ItemNeither =
    {
        "ContentDatabase.items", "CompanionDefinition.arrow", "CompanionDefinition.weapon", "RaidDefinition.likelyItems", "TradeSettings.coin",
        "TradeSettings.sales", "RaidRumourDefinition.teaches",
        // Only shown in a resident's hand: the woodcutter's axe at a ring, the Farmer's water on the way back (nothing made or spent).
        "WorkerSettings.fellingTool", "FarmSettings.waterCarried",
        // Only shown in a mason's hand at a quarry ring; the stores counted along the top of the Keeper's view.
        "WorkerSettings.quarryTool", "KeeperViewSettings.stores",
        // The herbs a module makes one fewer of (the Herb Rack: no recipe of its own); the raw meat and fish residents leave for the cook.
        "BuildingModuleDefinition.oneFewer", "HomeLifeSettings.uneaten",
    };

    private static readonly string[] RecipeTeachers =
    {
        "BuildingDefinition.teachesRecipes", "BuildingDefinition.upgrades[].teachesRecipes", "ItemDefinition.teachesRecipes",
        "JobDefinition.teachesRecipes", "LootOnDeath.unlocksRecipes", "MonsterDefinition.unlocksRecipes", "QuestDefinition.rewardRecipes",
    };

    // A thrifty variant names the recipe it beats where its module is fitted (RecipeVariants): it neither teaches nor asks for it.
    // A crop's look on the farm bed (FarmPlotLook) only names the recipe that sows it.
    private static readonly string[] RecipeNeither = { "RecipeBook.recipes", "JobDefinition.workOrders", "RecipeDefinition.replaces", "FarmSettings.cropLooks[].recipe" };

    // A quest's Unlocks Buildings teaches them as it starts (BuildingGate, the quest flow review's C2).
    private static readonly string[] BuildingTeachers = { "ItemDefinition.teachesBuildings", "JobDefinition.teachesBuildings", "QuestDefinition.unlocksBuildings" };

    private static readonly string[] BuildingNeither =
    {
        "BuildingDefinition.requires", "BuildingDefinition.upgrades[].requires", "BuildingObject.definition", "JobDefinition.workplace",
        "ProficiencyDefinition.mentor", "PrologueDefinition.campfire", "QuestDefinition.objectives", "RaidRumourDefinition.needsBuilding",
        "RecipeBook.buildings", "SettlementSettings.residentGatheringPlace", "SettlementSettings.wardLevels[].requires",
        // Where the Haven's asks are posted (the Notice Board): needs it standing, teaches nothing.
        "HavenRequestSettings.postedAt",
        // The Haven as a town (2026-11-15): the station an extension serves, and what a yard's role needs standing inside it.
        "BuildingDefinition.extends", "YardRoleDefinition.needs[].anyOf[]",
        // The gate a wall piece can become (WallUpgrades): taught as a building on its own.
        "BuildingDefinition.gateVersion",
        // The buildings a module fits (BuildingModules): it needs one standing, and teaches none.
        "BuildingModuleDefinition.buildings",
        // What standing near a home makes it better (HomeLifeSettings › Features).
        "HomeLifeSettings.features[].near[]",
        // Once it stands the Haven's people eat from the chests (ResidentMeals): needs it standing, teaches nothing.
        "HomeLifeSettings.mealsFrom",
    };

    private static readonly string[] MonsterAsks = { "QuestDefinition.objectives", "JournalSettings.", "RaidDefinition.knownEnemies" };
    // Wildlife.senses: scenery game borrows a huntable kind's senses (2026-11-16), which neither asks for that creature nor makes it.
    // LostOneSettings: the bodies the Lost Ones walk in (by rank, and the fallback), which neither asks for a creature nor makes one
    // anywhere but in a raid a Lost One walks.
    private static readonly string[] MonsterNeither = { "MonsterAI.definition", "CheatSettings.creatures", "Wildlife.senses", "LostOneSettings.fallbackBody", "LostOneSettings.bodyGrades[].bodies[]" };

    private static readonly string[] Scenes = { "Assets/_Project/Scenes/SafeHaven.unity", "Assets/_Project/Scenes/Prologue_MistyWoods.unity" };

    /// <summary>One reference: what it points at (kind and asset path), by which field, from which asset.</summary>
    public readonly struct Reference
    {
        public Reference(string kind, string target, string field, string from)
        {
            Kind = kind;
            Target = target;
            Field = field;
            From = from;
        }

        public string Kind { get; }
        public string Target { get; }
        public string Field { get; }
        public string From { get; }
    }

    private static List<Reference> index;
    private static string everything;

    /// <summary>Forgets what was read (the next question reads everything again).</summary>
    public static void Invalidate()
    {
        index = null;
        everything = null;
    }

    /// <summary>Every reference from a data asset or prefab to an item, recipe, building or monster.</summary>
    public static List<Reference> Index()
    {
        if (index != null)
        {
            return index;
        }

        index = new List<Reference>();
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is ScriptableObject)
                {
                    Read(asset, path);
                }
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component != null) Read(component, path);
            }
        }

        return index;
    }

    private static void Read(Object owner, string path)
    {
        var so = new SerializedObject(owner);
        SerializedProperty it = so.GetIterator();
        bool enter = true;
        while (it.Next(enter))
        {
            enter = it.propertyType == SerializedPropertyType.Generic || it.propertyType == SerializedPropertyType.ManagedReference;
            if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null || it.objectReferenceValue == owner)
            {
                continue;
            }

            Object target = it.objectReferenceValue;
            string kind = target is RecipeDefinition ? "Recipe" : target is BuildingDefinition ? "Building" : target is ItemDefinition ? "Item" : target is MonsterDefinition ? "Monster" : null;
            if (kind != null)
            {
                string field = owner.GetType().Name + "." + Regex.Replace(it.propertyPath, @"\.Array\.data\[\d+\]", "[]");
                index.Add(new Reference(kind, AssetDatabase.GetAssetPath(target), field, path));
            }
        }
    }

    private static bool Is(string field, IEnumerable<string> prefixes) => prefixes.Any(field.StartsWith);

    /// <summary>Fields pointing at content that the audit doesn't know the meaning of (add them to the lists above).</summary>
    public static List<string> UnsortedFields() => Index()
        .Where(r => r.Kind == "Item" ? !Is(r.Field, ItemSources) && !Is(r.Field, ItemUses) && !Is(r.Field, ItemNeither)
            : r.Kind == "Recipe" ? !Is(r.Field, RecipeTeachers) && !Is(r.Field, RecipeNeither)
            : r.Kind == "Building" ? !Is(r.Field, BuildingTeachers) && !Is(r.Field, BuildingNeither)
            : !Is(r.Field, MonsterAsks) && !Is(r.Field, MonsterNeither))
        .Select(r => $"{r.Kind} <- {r.Field} (in {Path.GetFileName(r.From)})").Distinct().ToList();

    /// <summary>Recipes not known from the start that nothing teaches.</summary>
    public static List<string> UnlearnableRecipes()
    {
        var taught = new HashSet<string>(Index().Where(r => r.Kind == "Recipe" && Is(r.Field, RecipeTeachers)).Select(r => r.Target));
        return AssetDatabase.FindAssets("t:RecipeDefinition", new[] { "Assets/_Project" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path).KnownFromStart && !taught.Contains(path))
            .Select(path => $"{Path.GetFileNameWithoutExtension(path)}: not known from the start, and no building, job, item, creature or quest teaches it").ToList();
    }

    /// <summary>Buildings not known from the start that no job or item teaches.</summary>
    public static List<string> UnlearnableBuildings()
    {
        var taught = new HashSet<string>(Index().Where(r => r.Kind == "Building" && Is(r.Field, BuildingTeachers)).Select(r => r.Target));
        return AssetDatabase.FindAssets("t:BuildingDefinition", new[] { "Assets/_Project" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path).KnownFromStart && !taught.Contains(path))
            .Select(path => $"{Path.GetFileNameWithoutExtension(path)}: not known from the start, and no job or item teaches it").ToList();
    }

    /// <summary>Items a cost, ingredient, request or quest asks for that nothing gives: no learnable recipe makes it, nothing drops it.</summary>
    public static List<string> ItemsThatNeverCome()
    {
        var unlearnable = new HashSet<string>(UnlearnableRecipes().Select(line => line.Substring(0, line.IndexOf(':'))));
        var sources = new HashSet<string>(Index().Where(r => r.Kind == "Item" && Is(r.Field, ItemSources)
            && !(r.Field == "RecipeDefinition.output" && unlearnable.Contains(Path.GetFileNameWithoutExtension(r.From)))).Select(r => r.Target));
        string scenes = string.Concat(Scenes.Where(File.Exists).Select(File.ReadAllText));
        return Index().Where(r => r.Kind == "Item" && Is(r.Field, ItemUses)).GroupBy(r => r.Target)
            .Where(asked => !sources.Contains(asked.Key) && !scenes.Contains(AssetDatabase.AssetPathToGUID(asked.Key)) && !IsWaterMeasure(asked.Key))
            .Select(asked => $"{Path.GetFileNameWithoutExtension(asked.Key)}: asked for by {string.Join(", ", asked.Select(r => Path.GetFileNameWithoutExtension(r.From)).Distinct().Take(4))}, given by nothing")
            .ToList();
    }

    // Water as a recipe cost is had in waterskins (filled at water), not given as an item.
    private static bool IsWaterMeasure(string path)
    {
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        return item != null && item.IsWaterMeasure;
    }

    /// <summary>Monsters a quest, the journal or a raid names that have no body (prefab), or whose body nothing spawns.</summary>
    public static List<string> MonstersNothingSpawns()
    {
        Dictionary<string, string> bodies = Index().Where(r => r.Kind == "Monster" && r.Field == "MonsterAI.definition")
            .GroupBy(r => r.Target).ToDictionary(g => g.Key, g => g.First().From);
        var found = new List<string>();
        foreach (string monster in Index().Where(r => r.Kind == "Monster" && Is(r.Field, MonsterAsks)).Select(r => r.Target).Distinct())
        {
            if (!bodies.TryGetValue(monster, out string body))
            {
                found.Add($"{Path.GetFileNameWithoutExtension(monster)}: named, but no prefab's MonsterAI is it");
            }
            else if (!Everything().Contains(AssetDatabase.AssetPathToGUID(body)))
            {
                found.Add($"{Path.GetFileNameWithoutExtension(monster)}: nothing spawns its body ({Path.GetFileName(body)}): no raid, zone, event or scene names it");
            }
        }

        return found;
    }

    // Every data asset, prefab and scene as text, to ask whether anything names a GUID.
    private static string Everything()
    {
        if (everything != null)
        {
            return everything;
        }

        var text = new System.Text.StringBuilder();
        foreach (string folder in new[] { "Assets/_Project/Data", "Assets/_Project/Prefabs", "Assets/_Project/Resources" })
        {
            foreach (string path in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories).Where(p => p.EndsWith(".asset") || p.EndsWith(".prefab")))
            {
                text.Append(File.ReadAllText(path));
            }
        }

        foreach (string scene in Scenes.Where(File.Exists)) text.Append(File.ReadAllText(scene));
        return everything = text.ToString();
    }

    // ---- References to what is gone

    private static readonly Regex External = new Regex(@"\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d\}");
    private static readonly Regex Local = new Regex(@"\{fileID: (-?\d+)\}");
    private static readonly Regex Document = new Regex(@"^--- !u!\d+ &(-?\d+)", RegexOptions.Multiline);
    private static readonly HashSet<string> BuiltIn = new HashSet<string> { "0000000000000000e000000000000000", "0000000000000000f000000000000000", "0000000000000000d000000000000000" };

    /// <summary>
    /// References in the game's own scenes, prefabs and data to an asset that no longer exists, or to an object gone from the
    /// same file (what the Inspector shows as Missing).
    /// </summary>
    public static List<string> BrokenReferences()
    {
        var found = new List<string>();
        var known = new Dictionary<string, bool>();
        bool Exists(string guid)
        {
            if (!known.TryGetValue(guid, out bool there))
            {
                known[guid] = there = BuiltIn.Contains(guid) || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));
            }

            return there;
        }

        string[] kinds = { ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController" };
        foreach (string path in Directory.GetFiles("Assets/_Project", "*.*", SearchOption.AllDirectories).Where(p => kinds.Any(p.EndsWith)))
        {
            string text = File.ReadAllText(path);
            if (!text.StartsWith("%YAML", StringComparison.Ordinal))
            {
                continue;
            }

            var gone = new List<string>();
            foreach (Match match in External.Matches(text))
            {
                if (!Exists(match.Groups[2].Value)) gone.Add("asset " + match.Groups[2].Value);
            }

            if (path.EndsWith(".unity") || path.EndsWith(".prefab"))
            {
                var ids = new HashSet<string>(Document.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value));
                foreach (Match match in Local.Matches(text))
                {
                    if (match.Groups[1].Value != "0" && !ids.Contains(match.Groups[1].Value)) gone.Add("object " + match.Groups[1].Value);
                }
            }

            if (gone.Count > 0)
            {
                found.Add($"{path.Replace('\\', '/')}: {gone.Count} reference(s) to what is gone ({string.Join(", ", gone.Distinct().Take(3))})");
            }
        }

        return found;
    }

    /// <summary>Everything above in one list, for the build.</summary>
    /// <summary>
    /// Content saved by its id that has none of its own, so its asset's name stands in: renaming the asset would lose it from every
    /// save (Docs/BugPatterns.md, second round; PortalBloomed and StoneSilenced were found so). Every definition with an Id and an
    /// id field to set says it.
    /// </summary>
    public static List<string> ImplicitIds()
    {
        var found = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null || asset.GetType().Assembly != typeof(SettlementState).Assembly || asset.GetType().GetProperty("Id") == null)
            {
                continue;
            }

            SerializedProperty id = new SerializedObject(asset).FindProperty("id");
            if (id != null && id.propertyType == SerializedPropertyType.String && string.IsNullOrWhiteSpace(id.stringValue))
            {
                found.Add($"{asset.GetType().Name} {path} has no id of its own: its name stands in, and a rename would lose it from saves");
            }
        }

        return found;
    }

    public static List<string> All() =>
        BrokenReferences().Concat(UnlearnableRecipes()).Concat(UnlearnableBuildings()).Concat(ItemsThatNeverCome()).Concat(MonstersNothingSpawns())
            .Concat(UnsortedFields().Select(f => "the content audit doesn't know what this field means: " + f)).Concat(ImplicitIds()).ToList();
}
