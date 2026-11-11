using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// The quest line that opens the Haven's Reach (Docs/Territory.md › The quest line)
/// (`unity command script run --file AgentScripts/BuildHavensReach.cs --entry BuildHavensReach.Apply`). Run after
/// BuildTerritory and BuildFirstAct. Safe to run again.
/// <para>
/// Data: three Haven quests (The Cold Stones, What the Land Asks, The Forest Answers), the quest events the land tells them
/// (TerritorySettings › The story), and the first quest as what opens the stones. Home: stories under Home Moments in
/// SafeHaven: the portal's call after the third raid, what the stones were (a conversation at the portal), and its words
/// when a stone is lit, when the forest first gathers, when a land is Secure and when a push is broken; and what the
/// portal whispers while each quest is open.
/// </para>
/// </summary>
public static class BuildHavensReach
{
    private const string Quests = "Assets/_Project/Data/Quests/";
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string SettingsPath = "Assets/_Project/Resources/TerritorySettings.asset";
    private const string RunePath = "Assets/_Project/Data/Items/PactRune.asset";
    private const string Chain = "The Haven's Reach";
    private const string Moments = "Home Moments";
    /// <summary>Raids done before the portal speaks of the stones.</summary>
    public const int RaidsBefore = 3;

    public static string Apply()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty) return "the open scene " + SceneManager.GetSceneAt(i).name + " has unsaved changes";
        }

        GameEventDefinition stoneLit = Event("LandStoneLit", "territory.stone_lit", "A boundary stone lit", "Light a boundary stone");
        GameEventDefinition taskDone = Event("LandTaskDone", "territory.task_done", "A held land's task done", "Do what a held land asks");
        GameEventDefinition runeOffered = Event("LandRuneOffered", "territory.rune_offered", "A Pact Rune offered", "Offer a Pact Rune at a lit stone");
        GameEventDefinition gathers = Event("LandForestGathers", "territory.forest_gathers", "The forest gathers against a held land", "The forest gathers");
        GameEventDefinition answered = Event("LandPushAnswered", "territory.push_answered", "The forest's push answered", "Answer the forest's push");
        GameEventDefinition secure = Event("LandSecure", "territory.land_secure", "A held land made Secure", "Make a land Secure");
        Event("ColdStonesMoment", "home.cold_stones", "The Cold Stones (the portal speaks)", "Hear the portal out");
        // Raised as the conversation ends. Not a Talk To the portal: any talk with it (its menu, another story's) would count.
        GameEventDefinition toldOfStones = Event("ColdStonesHeard", "home.cold_stones_heard", "The portal told of the stones", "Hear what the portal has to say of the stones");

        var rune = Load<ItemDefinition>(RunePath) ?? throw new InvalidOperationException("No Pact Rune: run BuildTerritory first");

        QuestDefinition cold = Quest("TheColdStones");
        Basics(cold, "thecoldstones", "The Cold Stones", null, QuestStart.ByQuestGiver, null, true, rune,
            "The portal has something to tell you about the old stones out past the meadows. Hear it out, then light one.",
            "The portal has felt how far your fire reaches. Out past the meadows each of the old lands has a boundary stone that once burned from this hearth.\n\n" +
            "A stone doesn't know you until you have found one of the places in its land: walk up to one and it goes on your map (M). The stone tells you which way the nearest lies. The first stone you light needs nothing but your hand; the rest want a Pact Rune.\n\n" +
            "The Haven window's Lands tab (H) lists every region, what it is like and what it would give.",
            (QuestObjective.Create(ObjectiveType.QuestEvent, 1, toldOfStones), "Hear what the portal has to say of the stones", false),
            (QuestObjective.Create(ObjectiveType.QuestEvent, 1, stoneLit), "Light a boundary stone (find one of its places first)", false));

        QuestDefinition asks = Quest("WhatTheLandAsks");
        Basics(asks, "whatthelandasks", "What the Land Asks", cold, QuestStart.Automatically, null, false, rune,
            "A lit stone has to be held. Do what a held land asks of you, lay a Pact Rune in its stone, and make it Secure.",
            "A lit stone is a promise, and the forest will test it. A held land wears a little at every dawn and at the end of every raid, faster under blight. Below 25 it is slipping; at nothing the forest takes it back.\n\n" +
            "Each held land asks something now and then: to hunt, gather or fish in it. Its stone and the Lands tab say what. A Pact Rune laid in its stone adds a good deal more, and each of its places you find adds a little.\n\n" +
            "From a hold of 70 a land is Secure: it sends what it grows home to the stash at every dawn, and the corrupted nights will not come through it.",
            (QuestObjective.Create(ObjectiveType.QuestEvent, 1, taskDone), "Do what a held land asks (its stone tells you)", true),
            (QuestObjective.Create(ObjectiveType.QuestEvent, 1, runeOffered), "Offer a Pact Rune at a lit stone", true),
            (QuestObjective.Create(ObjectiveType.QuestEvent, 1, secure), "Make a land Secure (a hold of 70)", true));

        QuestDefinition forest = Quest("TheForestAnswers");
        Basics(forest, "theforestanswers", "The Forest Answers", cold, QuestStart.OnQuestEvent, gathers, false, rune,
            "The forest is gathering against one of your lands. Answer it before it pushes in: stand watch at that land's stone, or burn the rot's heart on a raid.",
            "The forest doesn't give land up quietly. It has gathered against the weakest of your lands, and when the warning runs out it will push in and take a good share of the hold.\n\n" +
            "Stand watch at that land's boundary stone: what the forest sends comes for the stone, and if you beat it all without straying the push is broken. Or burn the rot's heart on a raid: while a push is coming, every raid's briefing has the rumour of it, and each heart halves the push. Two stop it.\n\n" +
            "If it pushes in first, it will gather again before long. Answer the next one.",
            // Counted if it came first: the quest is announced a few seconds after the warning (story pacing), and a friend can
            // burn a heart in that moment. No push can have been answered before the first warning, which starts it.
            (QuestObjective.Create(ObjectiveType.QuestEvent, 1, answered), "Answer the forest: stand watch at its stone, or burn the rot's heart on a raid", true));

        Edit(Load<TerritorySettings>(SettingsPath), so =>
        {
            so.FindProperty("unlockedBy").objectReferenceValue = cold;
            so.FindProperty("unlockedWhen").objectReferenceValue = toldOfStones;
            so.FindProperty("stonesObjective").intValue = 2;
            so.FindProperty("stoneLit").objectReferenceValue = stoneLit;
            so.FindProperty("taskDone").objectReferenceValue = taskDone;
            so.FindProperty("runeOffered").objectReferenceValue = runeOffered;
            so.FindProperty("forestGathers").objectReferenceValue = gathers;
            so.FindProperty("pushAnswered").objectReferenceValue = answered;
            so.FindProperty("landSecure").objectReferenceValue = secure;
        });

        var database = Load<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        database.CollectAll();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        // Loaded again from disk: made in this same run, a scene object kept one as an empty reference (BuildFirstAct, 2026-11-10).
        GameEventDefinition moment = Load<GameEventDefinition>(Quests + "Events/ColdStonesMoment.asset");
        toldOfStones = Load<GameEventDefinition>(Quests + "Events/ColdStonesHeard.asset");
        cold = Load<QuestDefinition>(Quests + "TheColdStones.asset");
        asks = Load<QuestDefinition>(Quests + "WhatTheLandAsks.asset");
        forest = Load<QuestDefinition>(Quests + "TheForestAnswers.asset");

        string home = Home(cold, asks, forest, moment, toldOfStones);
        string problems = string.Join("; ", new[] { cold, asks, forest }.SelectMany(q => q.FindProblems().Select(p => q.name + ": " + p)));
        return $"{cold.DisplayName}; {asks.DisplayName}; {forest.DisplayName}; {home}; problems: {(problems.Length > 0 ? problems : "none")}";
    }

    // ----- Quests -----

    private static void Basics(QuestDefinition quest, string id, string title, QuestDefinition after, QuestStart start, GameEventDefinition startEvent,
        bool inOrder, ItemDefinition reward, string description, string notes, params (QuestObjective objective, string text, bool countBefore)[] steps)
    {
        Edit(quest, so =>
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = title;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("journalNotes").stringValue = notes;
            so.FindProperty("chain").stringValue = Chain;
            so.FindProperty("rank").intValue = (int)QuestRank.Haven;
            so.FindProperty("availableObjective").stringValue = "";
            so.FindProperty("announceStart").boolValue = true;
            so.FindProperty("announceCompletion").boolValue = true;
            so.FindProperty("start").intValue = (int)start;
            so.FindProperty("startEvent").objectReferenceValue = startEvent;
            so.FindProperty("startDelay").floatValue = 0f;
            so.FindProperty("closedBy").objectReferenceValue = null;
            so.FindProperty("startedFirst").arraySize = 0;
            SerializedProperty prerequisites = so.FindProperty("prerequisites");
            prerequisites.arraySize = after != null ? 1 : 0;
            if (after != null) prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = after;
            so.FindProperty("objectivesInOrder").boolValue = inOrder;
            SerializedProperty objectives = so.FindProperty("objectives");
            objectives.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++)
            {
                SerializedProperty step = objectives.GetArrayElementAtIndex(i);
                step.boxedValue = steps[i].objective;
                step.FindPropertyRelative("description").stringValue = steps[i].text;
                step.FindPropertyRelative("countAlreadyOwned").boolValue = steps[i].countBefore;
            }

            so.FindProperty("turnInToGiver").boolValue = false;
            SerializedProperty items = so.FindProperty("rewardItems");
            items.arraySize = 1;
            items.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = reward;
            items.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 1;
            so.FindProperty("rewardResources").arraySize = 0;
            so.FindProperty("rewardRecipes").arraySize = 0;
            so.FindProperty("onStarted").arraySize = 0;
            so.FindProperty("onCompleted").arraySize = 0;
            so.FindProperty("completionEvent").objectReferenceValue = null;
        });
    }

    // ----- Home: the portal's words -----

    // The words, kept here so a test or another builder can find them.
    public const string Call = "<i>Little one. Three times through me, and three times home. Your fire reaches further than it did. Come and sit by me when you can: there is something out past my meadows you should know of.</i>";

    private static string Home(QuestDefinition cold, QuestDefinition asks, QuestDefinition forest, GameEventDefinition moment, GameEventDefinition toldOfStones)
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScene);
        Transform root = GameObject.Find(Moments)?.transform ?? throw new InvalidOperationException("No " + Moments + " in " + HomeScene);
        HomePortal portal = Object.FindAnyObjectByType<HomePortal>(FindObjectsInactive.Include);
        StoryScript waking = root.Find("Story - The Waking").GetComponent<StoryScript>();
        SayNode voice = waking.Nodes.OfType<SayNode>().First();
        StorySpeaker Portal() => new StorySpeaker { player = voice.speaker.player, name = voice.speaker.name, model = voice.speaker.model, portrait = voice.speaker.portrait, voice = voice.speaker.voice, voiceFrom = voice.speaker.voiceFrom };
        SayNode Say(string line) => new SayNode { speaker = Portal(), line = line };
        SayNode Aside(string line) => new SayNode { speaker = Portal(), line = line, aside = true, afterFightPause = false };

        // ----- After the third raid: the call -----
        StoryScript call = Fresh(root, "Story - The Cold Stones");
        var thrice = new WhenRaidsDone { raids = RaidsBefore };
        var settle = new WaitNode { seconds = 8f };
        var quiet = new WaitForQuietNode();
        var calling = Aside(Call);
        var begin = new StartQuestNode { quest = cold };
        Link(call, (thrice, 0, settle), (settle, 0, quiet), (quiet, 0, calling), (calling, 0, begin));

        // ----- What the stones were: the conversation at the portal -----
        StoryScript told = Fresh(root, "Story - What the Stones Were");
        var sat = new WhenQuestEvent { questEvent = moment };
        var reach = Say("<i>Your fire reaches further than it did, little one. Far enough that I can feel what lies at the end of it: stones, cold ones, out past the meadows.</i>");
        var walked = Say("<i>When the pact was kept, your people did not only tend a hearth beside me. They walked the land the pact gave them, and where it ended they set stones, and lit them from this fire.</i>");
        var remembers = Say("<i>A lit stone said: this much of the forest remembers a hearth. Inside that ring nobody lost the way home, and nobody lost their name.</i>");
        var cold_ = Say("<i>When the hearth went cold, the stones went cold, one by one. The forest took back what they held, and every road and every name in it. That is where your people are now: out in the land that forgot us.</i>");
        var answer = new ChoiceNode
        {
            speaker = Portal(),
            line = "<i>The stones are still out there. They only want a hand that keeps a fire.</i>",
            answers = new List<string> { "Then I'll light them again.", "Won't the forest want the land back?" },
            mustAnswer = true,
        };
        var light = Say("<i>Good. Not all of them, and not all at once: only what you can hold.</i>");
        var back = Say("<i>It will. The pact gave the forest its room as well, and that is the one thing it has not forgotten. It will push against every stone you light. Hold what you take, and in time it leaves it be.</i>");
        var how = Say("<i>Find one of the places a stone remembers, so that it knows you, then lay your hand on it. The first will answer to my fire alone. The rest want a Pact Rune, the pact's own mark, scattered through the woods since it broke.</i>");
        var why = Say("<i>Every land you hold is a road home for whoever is still out there, and a door shut on what comes at night. Begin with the nearest stone.</i>");
        var heard = new RaiseQuestEventNode { questEvent = toldOfStones };
        Link(told, (sat, 0, reach), (reach, 0, walked), (walked, 0, remembers), (remembers, 0, cold_), (cold_, 0, answer),
            (answer, 0, light), (answer, 1, back), (light, 0, how), (back, 0, how), (how, 0, why), (why, 0, heard));

        // ----- A stone burns again -----
        StoryScript lit = Fresh(root, "Story - A Stone Burns Again");
        var first = new WhenQuest { quest = cold, when = WhenQuest.Moment.IsCompleted };
        var beat = new WaitNode { seconds = 2f };
        var felt = Aside("<i>I felt that. Out past the meadows, a stone remembers me again.</i>");
        var held = Aside("<i>Now it must be held, little one. A lit stone is a promise, and the forest will test it.</i>");
        Link(lit, (first, 0, beat), (beat, 0, felt), (felt, 0, held));

        // ----- The forest gathers for the first time (it may be told on a raid: heard on coming home) -----
        StoryScript gathering = Fresh(root, "Story - The Forest Gathers");
        var gathers = new WhenQuest { quest = forest, when = WhenQuest.Moment.Starts, alsoAfterLoading = true };
        var still = new WaitForQuietNode();
        var feel = Aside("<i>Little one, do you feel it? The forest is gathering against one of your stones. It means to push in.</i>");
        Link(gathering, (gathers, 0, still), (still, 0, feel));

        // ----- A land made Secure: the long goal -----
        StoryScript secured = Fresh(root, "Story - Secure Ground");
        var done = new WhenQuest { quest = asks, when = WhenQuest.Moment.IsCompleted, alsoAfterLoading = true };
        var pause = new WaitNode { seconds = 2f };
        var stillNow = Aside("<i>Secure. Feel how still it is out there now. A land held that firmly sends home what it grows at every dawn, and the night's bands will not come through it.</i>");
        var ring = Aside("<i>Hold a ring of them round me, little one, and the forest will remember us for good.</i>");
        Link(secured, (done, 0, pause), (pause, 0, stillNow), (stillNow, 0, ring));

        // ----- A push broken (by a watch at home, or a heart burned on a raid: heard on coming home) -----
        StoryScript broken = Fresh(root, "Story - The Push Broken");
        var answered = new WhenQuest { quest = forest, when = WhenQuest.Moment.IsCompleted, alsoAfterLoading = true };
        var calm = new WaitForQuietNode();
        var ready = Aside("<i>It broke against you. It will try again, somewhere else. Let it find you ready.</i>");
        Link(broken, (answered, 0, calm), (calm, 0, ready));

        Whispers(portal, cold, asks, forest, moment);
        foreach (StoryScript built in new[] { call, told, lit, gathering, secured, broken }) KeepTriggerIds(built);
        SaveableEntityTools.AssignSceneIds(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        StoryScript[] stories = { call, told, lit, gathering, secured, broken };
        string problems = string.Join("; ", stories.SelectMany(s => s.FindProblems().Select(p => s.name + ": " + p)));
        return $"home: {stories.Length} stories, story problems: {(problems.Length > 0 ? problems : "none")}, dirty={scene.isDirty}";
    }

    // What the portal says while each quest is open. Put first in the list, so the story's own steps speak before them.
    private static void Whispers(HomePortal portal, QuestDefinition cold, QuestDefinition asks, QuestDefinition forest, GameEventDefinition moment)
    {
        var so = new SerializedObject(portal);
        SerializedProperty whispers = so.FindProperty("questWhispers");
        for (int i = whispers.arraySize - 1; i >= 0; i--)
        {
            Object owner = whispers.GetArrayElementAtIndex(i).FindPropertyRelative("quest").objectReferenceValue;
            if (owner == cold || owner == asks || owner == forest) whispers.DeleteArrayElementAtIndex(i);
        }

        (QuestDefinition quest, string text, GameEventDefinition said)[] mine =
        {
            (cold, "<i>Find a place that a stone remembers, then lay your hand on the stone. The nearest stands not far past the meadows.</i>", moment),
            (asks, "<i>A held land asks something of you now and then, and its stone will tell you what. A Pact Rune laid in the stone holds it better still.</i>", null),
            (forest, "<i>The forest is gathering against your stone, little one. Stand watch beside it, or burn the rot's heart on a raid. It will not wait for you.</i>", null),
        };
        for (int i = 0; i < mine.Length; i++)
        {
            whispers.InsertArrayElementAtIndex(i);
            SerializedProperty whisper = whispers.GetArrayElementAtIndex(i);
            whisper.FindPropertyRelative("quest").objectReferenceValue = mine[i].quest;
            whisper.FindPropertyRelative("text").stringValue = mine[i].text;
            whisper.FindPropertyRelative("moment").objectReferenceValue = mine[i].said;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // A story of this builder's own, made again each run on the same object: its save id and its triggers' ids are kept (by
    // order, KeepTriggerIds), so a saved game still knows which have fired. Made from nothing, every one would play again.
    private static readonly Dictionary<StoryScript, List<string>> oldTriggers = new Dictionary<StoryScript, List<string>>();

    private static StoryScript Fresh(Transform root, string name)
    {
        Transform old = root.Find(name);
        GameObject go;
        var ids = new List<string>();
        if (old != null)
        {
            go = old.gameObject;
            if (go.TryGetComponent(out StoryScript previous))
            {
                ids.AddRange(previous.Nodes.OfType<StoryTrigger>().Select(t => t.id));
                Object.DestroyImmediate(previous);
            }
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<SaveableEntity>();
        }

        StoryScript story = go.AddComponent<StoryScript>();
        oldTriggers[story] = ids;
        return story;
    }

    // After the links: each trigger takes back the id its place had last run.
    private static void KeepTriggerIds(StoryScript story)
    {
        if (!oldTriggers.TryGetValue(story, out List<string> ids)) return;
        List<StoryTrigger> triggers = story.Nodes.OfType<StoryTrigger>().ToList();
        for (int i = 0; i < triggers.Count && i < ids.Count; i++) triggers[i].id = ids[i];
        oldTriggers.Remove(story);
    }

    // Adds the nodes (in order of first appearance, laid out left to right) and the links.
    private static void Link(StoryScript story, params (StoryNode from, int output, StoryNode to)[] links)
    {
        var added = new List<StoryNode>(story.Nodes);
        var depth = new Dictionary<StoryNode, int>();
        foreach (StoryNode node in added) depth[node] = Mathf.RoundToInt((node.position.x - 40f) / 300f);
        foreach (var (from, output, to) in links)
        {
            foreach (StoryNode node in new[] { from, to })
            {
                if (added.Contains(node)) continue;
                int column = node == from ? 0 : depth.TryGetValue(from, out int d) ? d + 1 : 1;
                depth[node] = column;
                int row = depth.Values.Count(v => v == column) - 1;
                node.position = new Vector2(40f + column * 300f, 60f + row * 170f);
                story.AddNode(node);
                added.Add(node);
            }

            story.Connect(from, output, to);
        }
    }

    // ----- Helpers -----

    private static QuestDefinition Quest(string file)
    {
        string path = Quests + file + ".asset";
        var quest = Load<QuestDefinition>(path);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestDefinition>();
            AssetDatabase.CreateAsset(quest, path);
        }

        return quest;
    }

    private static GameEventDefinition Event(string file, string id, string name, string objective)
    {
        string path = Quests + "Events/" + file + ".asset";
        var gameEvent = Load<GameEventDefinition>(path);
        if (gameEvent == null)
        {
            gameEvent = ScriptableObject.CreateInstance<GameEventDefinition>();
            AssetDatabase.CreateAsset(gameEvent, path);
        }

        Edit(gameEvent, so =>
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("objectiveText").stringValue = objective;
        });
        return gameEvent;
    }

    private static void Edit(Object target, Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
