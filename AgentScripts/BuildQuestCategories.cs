using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Step 1 of Docs/QuestRedesign.md: quest categories and the open door
/// (`unity command script run --file AgentScripts/BuildQuestCategories.cs --entry BuildQuestCategories.Apply`).
/// Every quest gets its kind (Main Story, Rescue, Haven, Tale, Errand). At home the main story is short: listen to the portal,
/// light a fire, free the three stones, wake the door, go and find Ingrid. Tools, food, the workshop, the chest and the club are
/// Haven quests beside it and gate nothing. Gifts of the Clearing and They Are Still Out There are retired.
/// It replaces the order BuildOpeningOrder set: don't run that one, BuildHomeStory or BuildFoundation after this.
/// Safe to run again.
/// </summary>
public static class BuildQuestCategories
{
    private const string Quests = "Assets/_Project/Data/Quests/";
    private const string PrologueQuests = "Assets/_Project/Data/Prologue/Quests/";
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string Story = "The Hearth and the Door";
    private const string FirstDays = "The First Days";
    private const string HandsAtHome = "Hands at Home";

    private static readonly string[] Rescues =
    {
        "AStrangerInTheMist", "BringBjarneHome", "BringTorHome", "BringEirikHome", "BringGunnarHome", "BringAsaHome", "BringYrsaHome",
        "BringRunaHome", "BringLivHome", "BringHildHome", "BringSigridHome",
    };

    private static readonly string[] HavenQuests =
    {
        "PrimitiveTools", "FoodAndWater", "ARoofForTheWork", "SafeKeeping", "FirstWeapon", "RaiseTheWalls", "AGrowingHome", "HeavyLoads",
        "WoodcuttersLodge", "TheAlchemist", "TheCook", "TheFarmer", "TheHunter", "TheLeader", "TheMason", "TheRuneScholar", "TheSmith",
        "TheTracker", "TheWarden", "ALightTheyCanSee",
    };

    // The story at home, in order.
    private static readonly string[] MainLine = { "Awakening", "FirstCamp", "RootboundStones", "AwakeningThePortal", "ThroughThePortal", "TheWeakeningWard" };

    public static string Apply()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty) return "the open scene " + open.name + " has unsaved changes";
        }

        var report = new List<string>();
        MainStory();
        Haven();
        Ranks();
        report.Add(Retire());

        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        database.CollectAll();
        EditorUtility.SetDirty(database);
        Milestones();
        AssetDatabase.SaveAssets();

        report.Add(Home());
        report.Add(Problems());
        return string.Join(" | ", report);
    }

    // ----- The main story at home: fire, stones, door, Ingrid -----

    private static void MainStory()
    {
        Edit(Quest("Awakening"), so => so.FindProperty("displayName").stringValue = "The One It Caught");

        Edit(Quest("FirstCamp"), so =>
        {
            so.FindProperty("description").stringValue = "The forest cannot forget a hearth that someone tends. Pick up the fallen sticks and loose stones around the portal and light a fire inside its ward (build menu: {key:Build}). Your people will see it through the mist.";
            so.FindProperty("journalNotes").stringValue = "This is the old bargain, though you are only starting to understand it. A fire inside the ward feeds the ward. It also shows, a long way off, to anyone who is looking for it.\n\nSticks and small rocks lie on the ground around the portal: walk up to them and pick them up (E). The build menu (G) shows what the fire costs.";
            After(so, Quest("Awakening"));
            SerializedProperty objectives = so.FindProperty("objectives");
            for (int i = objectives.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty objective = objectives.GetArrayElementAtIndex(i);
                Object built = objective.FindPropertyRelative("building").objectReferenceValue;
                bool theFire = objective.FindPropertyRelative("type").intValue == (int)ObjectiveType.BuildStructure && built != null && built.name == "Campfire";
                if (!theFire) objectives.DeleteArrayElementAtIndex(i);
                else objective.FindPropertyRelative("description").stringValue = "Light a Campfire inside the ward (build menu: {key:Build})";
            }

            so.FindProperty("objectivesInOrder").boolValue = false;
        });

        Edit(Quest("RootboundStones"), so => After(so, Quest("FirstCamp")));

        Edit(Quest("AwakeningThePortal"), so =>
        {
            so.FindProperty("displayName").stringValue = "The Door Wakes";
            After(so, Quest("RootboundStones"));
        });

        ResidentDefinition ingrid = Quest("AStrangerInTheMist").Objectives[0].Person;
        if (ingrid == null) throw new InvalidOperationException("Bring Ingrid Home no longer names Ingrid first");
        Edit(Quest("ThroughThePortal"), so =>
        {
            so.FindProperty("description").stringValue = "Sigrid's voice came through the stones: Ingrid is hurt, and close. Step through the portal, find her, and reach an extraction point before the portal closes. What you wear comes home with you whatever happens. What you carry is lost if you fall.";
            so.FindProperty("journalNotes").stringValue = "Your first raid. Put anything precious in your safe pocket, and turn back earlier than feels necessary. A hatchet and a pick let you bring hardwood and ore home as well as people.\n\nFinding Ingrid counts even when the raid goes wrong: she waits where you found her.";
            After(so, Quest("AwakeningThePortal"));
            SerializedProperty objectives = so.FindProperty("objectives");
            for (int i = objectives.arraySize - 1; i >= 0; i--)
            {
                int type = objectives.GetArrayElementAtIndex(i).FindPropertyRelative("type").intValue;
                if (type != (int)ObjectiveType.CompleteRaids) objectives.DeleteArrayElementAtIndex(i);
            }

            if (objectives.arraySize != 1) throw new InvalidOperationException("Into the Misty Woods should keep exactly its raid objective");
            objectives.InsertArrayElementAtIndex(0);
            SerializedProperty find = objectives.GetArrayElementAtIndex(0);
            find.boxedValue = QuestObjective.Deed(ObjectiveType.RescuePerson, ingrid);
            find.FindPropertyRelative("description").stringValue = "Find Ingrid in the Misty Woods";
            find.FindPropertyRelative("countAlreadyOwned").boolValue = true;
            so.FindProperty("objectivesInOrder").boolValue = false;
        });

        foreach (string file in MainLine)
        {
            Edit(Quest(file), so => so.FindProperty("chain").stringValue = Story);
        }
    }

    // ----- Haven quests: optional, beside the story -----

    private static void Haven()
    {
        Edit(Quest("PrimitiveTools"), so =>
        {
            so.FindProperty("description").stringValue = "Bare hands bring berries home. A hatchet and a pick bring hardwood and ore. Bind a sharp rock to a stick for each: both are made from your bag (B), from sticks and small rocks off the clearing floor.";
            so.FindProperty("journalNotes").stringValue = "Stone tools are made from your bag (B), with no station needed, and go on your belt by themselves. Without them a raid still brings people and loose finds home, but no wood and no ore.";
            After(so, Quest("FirstCamp"));
        });

        Edit(Quest("FoodAndWater"), so => After(so, Quest("FirstCamp")));

        QuestDefinition workshop = Workshop();

        Edit(Quest("SafeKeeping"), so => After(so, workshop));
        Edit(Quest("FirstWeapon"), so =>
        {
            so.FindProperty("description").stringValue = "The weapon you came through with won't last for ever, and it is one way to fight. Make a Knotted Club at the Workshop, from oak and nettle fibre: it stuns what it hits. Wear it in your weapon slot (C), or keep it in the chest for the day yours breaks.";
            so.FindProperty("journalNotes").stringValue = "Nettles grow at the clearing's edge: pick them by hand for the fibre. Z swaps between the weapon in your hand and the one you held before it.";
            After(so, workshop);
        });

        foreach (string file in new[] { "PrimitiveTools", "FoodAndWater", "SafeKeeping", "FirstWeapon" })
        {
            Edit(Quest(file), so => so.FindProperty("chain").stringValue = FirstDays);
        }

        foreach (string file in new[] { "RaiseTheWalls", "AGrowingHome", "ALightTheyCanSee" })
        {
            Edit(Quest(file), so => so.FindProperty("chain").stringValue = HandsAtHome);
        }

        Edit(Quest("KnowYourEnemy"), so => so.FindProperty("chain").stringValue = "Errands");
        Edit(Quest("SmithsRequest"), so => so.FindProperty("chain").stringValue = "Errands");
        Edit(Quest("EchoesInTheWilds"), so => so.FindProperty("chain").stringValue = "Tales");
    }

    // The workshop used to be half of A Light in the Wood. Now it is its own Haven quest, open once the door is awake.
    private static QuestDefinition Workshop()
    {
        string path = Quests + "ARoofForTheWork.asset";
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (quest == null)
        {
            if (!AssetDatabase.CopyAsset(Quests + "SafeKeeping.asset", path)) throw new InvalidOperationException("Could not make " + path);
            quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        }

        Edit(quest, so =>
        {
            so.FindProperty("id").stringValue = "aroofforthework";
            so.FindProperty("displayName").stringValue = "A Roof for the Work";
            so.FindProperty("description").stringValue = "What the woods give you is only wood and ore until there is somewhere to work it. Raise a Workshop beside the fire (build menu: {key:Build}). It mends what a raid wears down, and the chest, the club and the bandages all start at its bench.";
            so.FindProperty("journalNotes").stringValue = "The Workshop wants oak, grey stone and sticks: fell the oaks at the clearing's edge with a hatchet and break the grey boulders with a pick. Repairs are made at its bench.";
            so.FindProperty("chain").stringValue = FirstDays;
            so.FindProperty("start").intValue = (int)QuestStart.Automatically;
            so.FindProperty("startEvent").objectReferenceValue = null;
            After(so, Quest("AwakeningThePortal"));
            SerializedProperty objectives = so.FindProperty("objectives");
            objectives.arraySize = 1;
            SerializedProperty build = objectives.GetArrayElementAtIndex(0);
            build.boxedValue = QuestObjective.Create(ObjectiveType.BuildStructure, 1, Find<BuildingDefinition>("ConstructionTable"));
            build.FindPropertyRelative("description").stringValue = "Build a Workshop (build menu: {key:Build})";
            build.FindPropertyRelative("countAlreadyOwned").boolValue = true;
            so.FindProperty("objectivesInOrder").boolValue = false;
            so.FindProperty("rewardItems").arraySize = 0;
            so.FindProperty("rewardResources").arraySize = 0;
            so.FindProperty("rewardRecipes").arraySize = 0;
        });
        return quest;
    }

    // ----- Kinds -----

    private static void Ranks()
    {
        foreach (string file in MainLine) Rank(file, QuestRank.MainStory);
        foreach (string file in Rescues) Rank(file, QuestRank.Rescue);
        foreach (string file in HavenQuests) Rank(file, QuestRank.Haven);
        Rank("KnowYourEnemy", QuestRank.Optional);
        Rank("SmithsRequest", QuestRank.Optional);
        Rank("EchoesInTheWilds", QuestRank.SideQuest);
    }

    private static void Rank(string file, QuestRank rank) => Edit(Quest(file), so => so.FindProperty("rank").intValue = (int)rank);

    // ----- Retired quests -----

    private static string Retire()
    {
        var gone = new List<string>();
        foreach (string path in new[] { Quests + "SettlingIn.asset", PrologueQuests + "TheyAreStillOutThere.asset" })
        {
            if (AssetDatabase.LoadAssetAtPath<QuestDefinition>(path) != null && AssetDatabase.DeleteAsset(path)) gone.Add(System.IO.Path.GetFileNameWithoutExtension(path));
        }

        return "retired: " + (gone.Count > 0 ? string.Join(", ", gone) : "none left");
    }

    // ----- What a run is measured against (Run Comparison) -----

    private static void Milestones()
    {
        var targets = new (string label, string eventName, string subject, string detail, float minutes)[]
        {
            ("Awake", "QuestCompleted", "Awakening", "", 16f),
            ("First fire", "QuestCompleted", "FirstCamp", "", 19f),
            ("Portal awakened", "QuestCompleted", "AwakeningThePortal", "", 24f),
            ("First raid over", "RaidFinished", "", "", 42f),
            ("First extraction", "RaidFinished", "", "Extracted", 42f),
            ("Growing home", "QuestCompleted", "AGrowingHome", "", 120f),
            ("First death", "PlayerDied", "", "", 0f),
        };
        Edit(PlaytestSettings.Current, so =>
        {
            SerializedProperty milestones = so.FindProperty("milestones");
            milestones.arraySize = targets.Length;
            for (int i = 0; i < targets.Length; i++)
            {
                SerializedProperty milestone = milestones.GetArrayElementAtIndex(i);
                milestone.FindPropertyRelative("label").stringValue = targets[i].label;
                milestone.FindPropertyRelative("eventName").stringValue = targets[i].eventName;
                milestone.FindPropertyRelative("subject").stringValue = targets[i].subject;
                milestone.FindPropertyRelative("detail").stringValue = targets[i].detail;
                milestone.FindPropertyRelative("targetMinutes").floatValue = targets[i].minutes;
            }
        });
    }

    // ----- Home: what the portal says, and what pointed at the retired quest -----

    private static string Home()
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScene);
        HomePortal portal = Object.FindAnyObjectByType<HomePortal>(FindObjectsInactive.Include);
        if (portal == null) return "no portal in " + HomeScene;

        var lines = new Dictionary<string, string>
        {
            ["FirstCamp"] = "<i>A fire first, little one. Fallen branches and loose stones lie all around me: pick them up, open the build menu (G) and light it inside my ward. The sooner it burns, the sooner they can see it.</i>",
            ["ThroughThePortal"] = "<i>Go, little one. She is just beyond me, off the path. Come back through one of my doors before I close. What you wear I can always bring home. What you carry I cannot.</i>",
            ["PrimitiveTools"] = "<i>Bare hands bring little home, little one. A hatchet in your belt and a pick beside it bring wood and ore. Sticks and sharp stones: make them in your bag (B).</i>",
            ["ARoofForTheWork"] = "<i>You have a fire. Now a roof to work under: a workshop beside it (G). What the woods break, it mends.</i>",
            ["SafeKeeping"] = "<i>What you carry through me, the woods can take from you. What you leave here, I keep. Build a chest beside your workshop (G), and put in it what you cannot bear to lose.</i>",
            ["FirstWeapon"] = "<i>One weapon is one answer, and it will not last for ever. Make a second at your workshop: a club stuns what a blade only cuts.</i>",
        };

        var so = new SerializedObject(portal);
        SerializedProperty whispers = so.FindProperty("questWhispers");
        var kept = new List<(QuestDefinition quest, string text, Object moment)>();
        for (int i = 0; i < whispers.arraySize; i++)
        {
            SerializedProperty whisper = whispers.GetArrayElementAtIndex(i);
            var quest = whisper.FindPropertyRelative("quest").objectReferenceValue as QuestDefinition;
            // A retired quest leaves an empty entry behind.
            if (quest != null) kept.Add((quest, whisper.FindPropertyRelative("text").stringValue, whisper.FindPropertyRelative("moment").objectReferenceValue));
        }

        foreach (KeyValuePair<string, string> line in lines)
        {
            QuestDefinition quest = Quest(line.Key);
            int at = kept.FindIndex(entry => entry.quest == quest);
            if (at >= 0) kept[at] = (quest, line.Value, kept[at].moment);
            else kept.Add((quest, line.Value, null));
        }

        // The last active whisper speaks first: errands and the haven early in the list, rescues after them, the story last.
        // Within the story its own order decides, so a later step speaks before an earlier one still open.
        List<(QuestDefinition quest, string text, Object moment)> ordered = kept
            .OrderByDescending(entry => QuestMarkerSettings.RankOrder(entry.quest.Rank))
            .ThenBy(entry => Array.IndexOf(MainLine, entry.quest.name))
            .ToList();
        whispers.arraySize = ordered.Count;
        for (int i = 0; i < ordered.Count; i++)
        {
            SerializedProperty whisper = whispers.GetArrayElementAtIndex(i);
            whisper.FindPropertyRelative("quest").objectReferenceValue = ordered[i].quest;
            whisper.FindPropertyRelative("text").stringValue = ordered[i].text;
            whisper.FindPropertyRelative("moment").objectReferenceValue = ordered[i].moment;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // The retired quest's arrow now leads to the portal for the quest that asks the player to listen to it; its
        // "inspected the portal" trigger has nothing left to count for.
        int waypoints = 0, triggers = 0;
        QuestDefinition awakening = Quest("Awakening");
        foreach (QuestWaypoint waypoint in portal.GetComponentsInChildren<QuestWaypoint>(true))
        {
            if (waypoint.Quest != null && waypoint.Quest != awakening) continue;
            waypoint.Configure(awakening, 0, "The Portal");
            waypoint.gameObject.name = "Quest Waypoint - The One It Caught";
            EditorUtility.SetDirty(waypoint);
            waypoints++;
        }

        foreach (GameEventTrigger trigger in portal.GetComponents<GameEventTrigger>())
        {
            var data = new SerializedObject(trigger);
            bool mine = data.FindProperty("gameEvent").objectReferenceValue == null && data.FindProperty("onlyDuringQuest").objectReferenceValue == null
                && data.FindProperty("target").objectReferenceValue == portal.QuestTarget;
            if (!mine) continue;
            Object.DestroyImmediate(trigger);
            triggers++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"home: {ordered.Count} whispers, {waypoints} waypoint retargeted, {triggers} trigger removed, dirty={scene.isDirty}";
    }

    private static string Problems()
    {
        var problems = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition", new[] { "Assets/_Project/Data" }))
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string problem in quest.FindProblems()) problems.Add(quest.name + ": " + problem);
            if (quest.Prerequisites.Contains(null)) problems.Add(quest.name + ": an empty prerequisite");
        }

        return problems.Count == 0 ? "no quest problems" : string.Join("; ", problems);
    }

    // ----- Helpers -----

    private static void After(SerializedObject so, QuestDefinition before)
    {
        SerializedProperty prerequisites = so.FindProperty("prerequisites");
        prerequisites.arraySize = 1;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = before;
    }

    private static QuestDefinition Quest(string file)
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(Quests + file + ".asset");
        if (quest == null) throw new InvalidOperationException("Missing " + Quests + file + ".asset");
        return quest;
    }

    private static T Find<T>(string name) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == name) return asset;
        }

        throw new InvalidOperationException($"No {typeof(T).Name} named {name}");
    }

    private static void Edit(Object target, Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
