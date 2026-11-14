#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Raids with a reason from home (`unity command script run --file AgentScripts/BuildHavenRequests.cs --entry
/// BuildHavenRequests.Apply`): what the Haven's people ask to be brought back from the raids, and what they give for it
/// (Data/Settlement/Requests, Resources/HavenRequestSettings), and the crafting orders they place once the player's hands are
/// up to it (a Fine or Masterwork piece, paid with a lesson in the craft and renown); the board that hands it in (SafeHaven › 05 Gameplay);
/// and loot that grows with each danger stage, so staying on is a choice (the raids' Escalation › Loot Bonus).
/// Re-runnable. Saves the scene only if it was clean when this began.
/// </summary>
public static class BuildHavenRequests
{
    private const string Folder = "Assets/_Project/Data/Settlement/Requests";

    private sealed class Ask
    {
        public string Id, Asker, Wanted, Says, Thanks, Reward;
        public int Count, RewardCount, Coins, Experience;
        public float Blight;
        public string[] Seasons = new string[0];

        // A crafting order: the make wanted, the level of the skill that makes it from which it is asked, its lesson in
        // levels' worth of experience at that level, and the renown it brings.
        public ItemQuality Quality;
        public int From;
        public float LessonLevels;
        public int Renown;
    }

    private static readonly Ask[] Asks =
    {
        new Ask { Id = "forge-iron", Asker = "Bjarne", Wanted = "IronOre", Count = 10, Says = "The forge is cold without iron.",
            Thanks = "Now we'll hear the hammer again.", Reward = "FieldRepairKit", RewardCount = 1, Coins = 6, Experience = 80 },
        new Ask { Id = "venom-draughts", Asker = "Ingrid", Wanted = "BogVenom", Count = 4, Says = "Bog venom, carried carefully. It makes the strongest draughts.",
            Thanks = "Careful hands. These will save someone.", Reward = "HealingDraught", RewardCount = 2, Experience = 70 },
        new Ask { Id = "wolf-fangs", Asker = "Sigrid", Wanted = "WolfFang", Count = 6, Says = "The wolves grow bold. Bring me their fangs and the children will sleep.",
            Thanks = "One for every door. Let them see.", Coins = 10, Experience = 60, Blight = 0.05f },
        new Ask { Id = "winter-hides", Asker = "Eirik", Wanted = "ThickHide", Count = 3, Says = "Winter's coming. Thick hides, for coats.",
            Thanks = "Warm backs this winter.", Coins = 8, Experience = 70, Seasons = new[] { "Summer", "Autumn", "Late Autumn" } },
        new Ask { Id = "wisp-embers", Asker = "Runa", Wanted = "WispEmber", Count = 3, Says = "Wisp embers. They remember the way through the mist.",
            Thanks = "Listen to them hum.", Reward = "WayfinderRune", RewardCount = 1, Experience = 90 },
        new Ask { Id = "trollstone", Asker = "Tor", Wanted = "Trollstone", Count = 2, Says = "Trollstone, for the ward's footing.",
            Thanks = "Old stone, older than the trolls. Good.", Reward = "RunestoneFragment", RewardCount = 1, Experience = 100 },
        new Ask { Id = "huldra-silk", Asker = "Liv", Wanted = "HuldraSilk", Count = 3, Says = "Huldra silk. Don't ask what for.",
            Thanks = "You didn't ask. Thank you.", Coins = 12, Experience = 80 },
        new Ask { Id = "boar-tusks", Asker = "Gunnar", Wanted = "BoarTusk", Count = 4, Says = "Boar tusks make good salves, ground fine.",
            Thanks = "That's a week of grinding. Here.", Reward = "HealingSalve", RewardCount = 2, Experience = 60 },
        new Ask { Id = "rootling-bark", Asker = "Hild", Wanted = "RootlingBark", Count = 6, Says = "Rootling bark. Burnt here, it takes the blight with it.",
            Thanks = "Smell that? That's the woods getting better.", Coins = 6, Experience = 50, Blight = 0.1f },
        new Ask { Id = "bones", Asker = "Asa", Wanted = "Bone", Count = 8, Says = "Bones, for needles and pins.",
            Thanks = "I'll stitch you something.", Reward = "Bandage", RewardCount = 4, Experience = 40 },
        new Ask { Id = "copper", Asker = "Yrsa", Wanted = "CopperOre", Count = 8, Says = "Copper ore. The Haven needs pots more than swords.",
            Thanks = "Pots, pans, and a kettle for Sigrid.", Coins = 6, Experience = 50 },
        new Ask { Id = "grave-iron", Asker = null, Wanted = "GraveIron", Count = 3, Says = "Grave iron from the barrows, to bind the ward.",
            Thanks = "The ward drinks it in.", Reward = "RuneOfPlenty", RewardCount = 1, Experience = 120, Blight = 0.1f },
        new Ask { Id = "winter-cold", Asker = null, Wanted = "RoughHide", Count = 6, Says = "The cold's in every bed. Hides, any hides.",
            Thanks = "Nobody freezes tonight.", Coins = 8, Experience = 50, Seasons = new[] { "Winter" } },

        // Crafting orders: a piece of good make, from the player's own hands. One stands at a time.
        new Ask { Id = "order-leather-jack", Asker = "Asa", Wanted = "CuredLeatherJack", Count = 1, Quality = ItemQuality.Fine, From = 10,
            Says = "Show me your stitching. A Fine leather jack, every seam tight.", Thanks = "Tight seams. Here, let me show you a lock stitch.",
            Coins = 8, LessonLevels = 1f, Renown = 8 },
        new Ask { Id = "order-hatchet", Asker = "Tor", Wanted = "IronHatchet", Count = 1, Quality = ItemQuality.Fine, From = 12,
            Says = "My hatchet's chewed to the haft. A Fine iron one would bite clean.", Thanks = "Balanced right. Hold the haft lower, like this.",
            Coins = 10, LessonLevels = 1f, Renown = 10 },
        new Ask { Id = "order-pickaxe", Asker = "Hild", Wanted = "IronPickaxe", Count = 1, Quality = ItemQuality.Fine, From = 12,
            Says = "The quarry eats picks. A Fine iron one, tempered hard.", Thanks = "Hard as the stone it's for. Watch how I set the head.",
            Coins = 10, LessonLevels = 1f, Renown = 10 },
        new Ask { Id = "order-sword", Asker = "Bjarne", Wanted = "IronSword", Count = 1, Quality = ItemQuality.Fine, From = 15,
            Says = "A Fine iron sword, for the ward. By your hand, not mine: I want to see what you've learned.",
            Thanks = "Good steel. Now let me show you the fold.", Coins = 12, LessonLevels = 1f, Renown = 12 },
        new Ask { Id = "order-bow", Asker = "Gunnar", Wanted = "HuntersBow", Count = 1, Quality = ItemQuality.Fine, From = 15,
            Says = "A Fine hunter's bow. Mine pulls left.", Thanks = "Straight as a sunbeam. Feel where the draw sits.",
            Coins = 12, LessonLevels = 1f, Renown = 12 },
        new Ask { Id = "order-hauberk", Asker = "Eirik", Wanted = "IronMailHauberk", Count = 1, Quality = ItemQuality.Fine, From = 20,
            Says = "The wardens need mail that holds. A Fine iron hauberk.", Thanks = "It'll turn a blade. The rings, though: close them this way.",
            Coins = 16, LessonLevels = 1f, Renown = 15 },
        new Ask { Id = "order-masterwork-spear", Asker = "Bjarne", Wanted = "IronSpear", Count = 1, Quality = ItemQuality.Masterwork, From = 30,
            Says = "A Masterwork spear. The kind they'll sing about at the Haven fires.", Thanks = "They will sing of this one. Both of us.",
            Coins = 25, LessonLevels = 1.5f, Renown = 25 },
        new Ask { Id = "order-masterwork-mail", Asker = "Sigrid", Wanted = "CopperScaleHauberk", Count = 1, Quality = ItemQuality.Masterwork, From = 30,
            Says = "For the children's first raid, one day: a Masterwork scale hauberk. Nothing less.", Thanks = "Nothing less. Thank you.",
            Coins = 25, LessonLevels = 1.5f, Renown = 25 },
    };

    public static string Apply()
    {
        var report = new List<string>();
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Data/Settlement", "Requests");
        }

        var made = new List<HavenRequestDefinition>();
        var missing = new List<string>();
        foreach (Ask ask in Asks)
        {
            ItemDefinition wanted = Item(ask.Wanted);
            ResidentDefinition asker = ask.Asker != null ? Resident(ask.Asker) : null;
            if (wanted == null || (ask.Asker != null && asker == null))
            {
                missing.Add(ask.Id);
                continue;
            }

            string path = $"{Folder}/{ask.Id}.asset";
            var request = AssetDatabase.LoadAssetAtPath<HavenRequestDefinition>(path);
            if (request == null)
            {
                request = ScriptableObject.CreateInstance<HavenRequestDefinition>();
                AssetDatabase.CreateAsset(request, path);
            }

            request.Configure(ask.Id, asker, ask.Says, ask.Thanks, wanted, ask.Count, ask.Reward != null ? Item(ask.Reward) : null,
                ask.RewardCount, ask.Coins, ask.Experience, ask.Blight, ask.Seasons.ToList());
            if (ask.Quality != ItemQuality.Plain)
            {
                if (request.Craft == null)
                {
                    missing.Add(ask.Id + " (no skill makes it)");
                }

                int lesson = Mathf.RoundToInt(LevelingSettings.Current.ExperienceToNext(ask.From) * ask.LessonLevels);
                request.ConfigureOrder(ask.Quality, ask.From, lesson, ask.Renown);
            }

            EditorUtility.SetDirty(request);
            made.Add(request);
        }

        const string settingsPath = "Assets/_Project/Resources/HavenRequestSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<HavenRequestSettings>(settingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<HavenRequestSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);
        }

        settings.Configure(made, 3);
        EditorUtility.SetDirty(settings);
        report.Add($"{made.Count} requests{(missing.Count > 0 ? " (missing: " + string.Join(", ", missing) + ")" : "")}");
        report.Add(StageLoot());
        report.Add(Scene());
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    // Each danger stage: stronger creatures, and the woods give more for staying on.
    private static string StageLoot()
    {
        float[] bonuses = { 0.25f, 0.5f, 0.8f };
        int set = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition", new[] { "Assets/_Project/Data/Raids" }))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            var data = new SerializedObject(raid);
            SerializedProperty stages = FindStages(data);
            if (stages == null)
            {
                continue;
            }

            for (int i = 0; i < stages.arraySize; i++)
            {
                SerializedProperty stage = stages.GetArrayElementAtIndex(i);
                float bonus = bonuses[Mathf.Min(i, bonuses.Length - 1)];
                stage.FindPropertyRelative("lootBonus").floatValue = bonus;
                SerializedProperty message = stage.FindPropertyRelative("message");
                string tail = $" The woods give more to those who stay: loot +{bonus:P0}.";
                int cut = message.stringValue.IndexOf(" The woods give more", System.StringComparison.Ordinal);
                message.stringValue = (cut >= 0 ? message.stringValue.Substring(0, cut) : message.stringValue) + tail;
                set++;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(raid);
        }

        return $"{set} danger stages pay for staying";
    }

    private static SerializedProperty FindStages(SerializedObject data)
    {
        SerializedProperty it = data.GetIterator();
        while (it.NextVisible(true))
        {
            if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.arraySize > 0
                && it.GetArrayElementAtIndex(0).FindPropertyRelative("startsAtSecondsLeft") != null)
            {
                return it.Copy();
            }
        }

        return null;
    }

    private static string Scene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SafeHaven")
        {
            return "scene skipped (open SafeHaven)";
        }

        bool wasClean = !scene.isDirty;
        GameObject gameplay = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "05 Gameplay");
        if (gameplay == null)
        {
            return "no 05 Gameplay";
        }

        Transform holder = gameplay.transform.Find("Haven Requests");
        if (holder == null)
        {
            holder = new GameObject("Haven Requests").transform;
            holder.SetParent(gameplay.transform, false);
        }

        if (!holder.TryGetComponent(out HavenRequestBoard _))
        {
            holder.gameObject.AddComponent<HavenRequestBoard>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (wasClean)
        {
            EditorSceneManager.SaveScene(scene);
        }

        return wasClean ? "scene: request board (saved)" : "scene: request board (not saved: it had other changes)";
    }

    private static ItemDefinition Item(string name) => Find<ItemDefinition>(name, "Assets/_Project/Data/Items");

    private static ResidentDefinition Resident(string name) => Find<ResidentDefinition>(name, "Assets/_Project/Data");

    private static T Find<T>(string name, string folder) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}", new[] { folder }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == name)
            {
                return asset;
            }
        }

        return null;
    }
}
