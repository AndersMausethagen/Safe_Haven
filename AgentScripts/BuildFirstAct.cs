using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Step 3 of Docs/QuestRedesign.md: the first act's story, up to the Heartroot
/// (`unity command script run --file AgentScripts/BuildFirstAct.cs --entry BuildFirstAct.Apply`). Run after
/// BuildQuestCategories, BuildFirstBoss and BuildBossTurningPoint. Safe to run again.
/// <para>
/// Data: two Main Story quests after the first breach (What Breathes Under the Roots, The Road North), the Hearth Ember made
/// at a campfire, the Heartroot that wakes only for one, its last word, and what the people brought home remember of the
/// night the door broke. Home: stories under Home Moments in SafeHaven: the roots seen in the first light, the three stones
/// remembering the pact, rootlings coming up through the ward, the root in the breach's dead, and the twist at the portal.
/// </para>
/// </summary>
public static class BuildFirstAct
{
    private const string Quests = "Assets/_Project/Data/Quests/";
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string Story = "The Hearth and the Door";
    private const string Moments = "Home Moments";

    public static string Apply()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty) return "the open scene " + SceneManager.GetSceneAt(i).name + " has unsaved changes";
        }

        ItemDefinition ember = Ember();
        GameEventDefinition moment = Event("RoadNorthMoment", "home.road_north", "The Road North (the portal speaks)");
        // The quest waits for the conversation's end (FixQuestAudit, 2026-11-11): a Talk To the portal counted every talk before it.
        GameEventDefinition told = Event("RoadNorthHeard", "home.road_north_heard", "The portal told of the Heartroot");
        QuestDefinition hunt = WhatBreathes(ember);
        QuestDefinition road = RoadNorth(hunt, told);
        TheHeartroot(ember);
        People();

        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        database.CollectAll();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        // Loaded again from disk: made in this same run, the scene's portal kept it as an empty reference (2026-11-10).
        moment = Load<GameEventDefinition>(Quests + "Events/RoadNorthMoment.asset");
        told = Load<GameEventDefinition>(Quests + "Events/RoadNorthHeard.asset");

        string home = Home(hunt, road, moment, told);
        string problems = string.Join("; ", new[] { hunt, road }.SelectMany(q => q.FindProblems().Select(p => q.name + ": " + p)));
        return $"{ember.DisplayName}; {hunt.DisplayName}; {road.DisplayName}; {home}; problems: {(problems.Length > 0 ? problems : "none")}";
    }

    // ----- The ember -----

    // A coal from the player's own fire. What the forest can't forget is what wakes its guardian (Askeladden: an ordinary
    // thing that matters). Made at any campfire, known from the start, so a fire is all it asks.
    private static ItemDefinition Ember()
    {
        ItemDefinition item = Copy<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset", "Assets/_Project/Data/Items/HearthEmber.asset");
        Edit(item, so =>
        {
            so.FindProperty("id").stringValue = "hearth_ember";
            so.FindProperty("displayName").stringValue = "Hearth Ember";
            so.FindProperty("description").stringValue = "A live coal from your own fire, banked in moss and bark. It stays warm for days. The forest cannot forget a hearth that someone tends, and this is a piece of one.";
            so.FindProperty("icon").objectReferenceValue = Load<ItemDefinition>("Assets/_Project/Data/Items/HandTorch.asset").Icon;
            so.FindProperty("weight").floatValue = 0.3f;
            // Not the copied Sticks prefab: dropped, an ember looked like sticks (HavenBuildingTests: a world prefab of its own).
            so.FindProperty("prefab").objectReferenceValue = null;
        });

        RecipeDefinition recipe = Copy<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/StewedBerries.asset", "Assets/_Project/Data/Crafting/Recipes/HearthEmber.asset");
        ItemDefinition sticks = Load<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset");
        Edit(recipe, so =>
        {
            so.FindProperty("id").stringValue = "hearth_ember";
            so.FindProperty("output").objectReferenceValue = item;
            so.FindProperty("outputAmount").intValue = 1;
            so.FindProperty("craftSeconds").floatValue = 4f;
            so.FindProperty("knownFromStart").boolValue = true;
            SerializedProperty cost = so.FindProperty("itemCost");
            cost.arraySize = 1;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = sticks;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 3;
        });
        return item;
    }

    // ----- Quests -----

    private static QuestDefinition WhatBreathes(ItemDefinition ember)
    {
        QuestDefinition quest = Quest("WhatBreathesUnderTheRoots");
        var heartroot = Load<MonsterDefinition>("Assets/_Project/Data/Monsters/TheHeartroot.asset");
        Basics(quest, "whatbreathesundertheroots", "What Breathes Under the Roots", Q("TheWeakeningWard"),
            "The roots that closed the road, the roots that choked the portal's stones, the roots in the breach's dead: one thing is pushing them. Something old breathes under them in a hollow deep in the Misty Woods. It will not wake for steel. Carry an ember from your own hearth to it, wake it, and bring it down.",
            "Make the ember at any campfire, from three sticks. The portal's briefing tells you when the hollow can be found: it marks the place for the whole raid. The Heartroot fights in three stages, and it can be fought again on a later raid if it beats you; what you learned of it stays learned.\n\nIt is the forest's guardian, or it was. Nobody at home is sure which.",
            (QuestObjective.Create(ObjectiveType.CraftItem, 1, ember), "Make a Hearth Ember at your campfire"),
            (QuestObjective.Create(ObjectiveType.KillMonster, 1, heartroot), "Wake the Heartroot in its hollow, and bring it down"));
        return quest;
    }

    private static QuestDefinition RoadNorth(QuestDefinition after, GameEventDefinition told)
    {
        QuestDefinition quest = Quest("TheRoadNorth");
        Basics(quest, "theroadnorth", "The Road North", after,
            "The Heartroot is still, and the roots have gone quiet. The portal has something to tell you about what you brought down, and about what lies north of it.",
            "Whatever the portal says, the road north to Troll Valley is open now: choose it in the raid planner.",
            (QuestObjective.Create(ObjectiveType.QuestEvent, 1, told), "Speak to the portal"));
        Edit(quest, so =>
        {
            so.FindProperty("startDelay").floatValue = 4f;
            // Only this conversation counts, not one from before the quest.
            so.FindProperty("objectives").GetArrayElementAtIndex(0).FindPropertyRelative("countAlreadyOwned").boolValue = false;
        });
        return quest;
    }

    private static void Basics(QuestDefinition quest, string id, string title, QuestDefinition after, string description, string notes,
        params (QuestObjective objective, string text)[] steps)
    {
        Edit(quest, so =>
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = title;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("journalNotes").stringValue = notes;
            so.FindProperty("chain").stringValue = Story;
            so.FindProperty("rank").intValue = (int)QuestRank.MainStory;
            so.FindProperty("availableObjective").stringValue = "";
            so.FindProperty("announceStart").boolValue = true;
            so.FindProperty("announceCompletion").boolValue = true;
            so.FindProperty("start").intValue = (int)QuestStart.Automatically;
            so.FindProperty("startEvent").objectReferenceValue = null;
            so.FindProperty("startDelay").floatValue = 0f;
            so.FindProperty("closedBy").objectReferenceValue = null;
            so.FindProperty("startedFirst").arraySize = 0;
            SerializedProperty prerequisites = so.FindProperty("prerequisites");
            prerequisites.arraySize = 1;
            prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = after;
            // Any order: a boss beaten before the quest counts (QuestObjective.Owned), with or without an ember made.
            so.FindProperty("objectivesInOrder").boolValue = false;
            SerializedProperty objectives = so.FindProperty("objectives");
            objectives.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++)
            {
                SerializedProperty step = objectives.GetArrayElementAtIndex(i);
                step.boxedValue = steps[i].objective;
                step.FindPropertyRelative("description").stringValue = steps[i].text;
                step.FindPropertyRelative("countAlreadyOwned").boolValue = true;
            }

            so.FindProperty("turnInToGiver").boolValue = false;
            so.FindProperty("rewardItems").arraySize = 0;
            so.FindProperty("rewardResources").arraySize = 0;
            so.FindProperty("rewardRecipes").arraySize = 0;
            so.FindProperty("onStarted").arraySize = 0;
            so.FindProperty("onCompleted").arraySize = 0;
            so.FindProperty("completionEvent").objectReferenceValue = null;
        });
    }

    // ----- The Heartroot: it wakes for fire, and its last word -----

    private static void TheHeartroot(ItemDefinition ember)
    {
        var site = Load<RaidEventDefinition>("Assets/_Project/Data/Raids/Rumours/Events/TheHeartroot.asset");
        Edit(site, so =>
        {
            so.FindProperty("siteText").stringValue = SiteText;
            SerializedProperty wake = so.FindProperty("choices").GetArrayElementAtIndex(0);
            wake.FindPropertyRelative("detail").stringValue = WakeDetail;
            SerializedProperty cost = wake.FindPropertyRelative("cost");
            cost.arraySize = 1;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = ember;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 1;
            wake.FindPropertyRelative("result").stringValue = "You lay the ember in the split of the stump. The breathing stops. Then the stump opens, and something stands up out of it.";
        });
        Edit(Load<RaidRumourDefinition>("Assets/_Project/Data/Raids/Rumours/TheHeartroot.asset"), so => so.FindProperty("briefing").stringValue = Briefing);

        var boss = Load<BossDefinition>("Assets/_Project/Data/Monsters/Boss The Heartroot.asset");
        Edit(boss, so =>
        {
            so.FindProperty("defeatLine").stringValue = DefeatLine;
            SerializedProperty lines = so.FindProperty("homeLines");
            for (int i = 0; i < lines.arraySize; i++)
            {
                if (lines.GetArrayElementAtIndex(i).stringValue.Contains("keep it away from the fire")) lines.GetArrayElementAtIndex(i).stringValue = TrophyLine;
            }
        });
    }

    // The same words live in BuildFirstBoss and BuildBossTurningPoint, so running those again keeps them.
    public const string SiteText = "A stump as wide as a house, split down the middle, and breathing. Dead trees stand round it in a ring, and nothing grows inside. It does not stir for footsteps or for steel. In the split, where a heart would be, the wood is black, as if a fire once burned there.";
    public const string WakeDetail = "Lay a Hearth Ember in the split: only a hearth's fire wakes it. A fight to the end, in three stages. Beating it teaches you what it knows, for good.";
    public const string Briefing = "Something old is breathing under the roots, deep in the woods. The portal has felt it turn over. It will not wake for steel.";
    public const string DefeatLine = "The Heartroot sinks to its knees, and speaks once, in a voice like the portal's own: \"Hearth-keeper.\" Then it lies still. What it knew of the forest's hold is yours now: you have learned Heartroot's Grasp. And something has opened: the portal can reach Troll Valley.";
    public const string TrophyLine = "Whatever you carried back from that hollow, the portal wants it by the fire. I'd never have thought it.";

    // ----- What they remember of the night the door broke -----

    private static void People()
    {
        Welcome("Settlement/Ingrid", "I watched your fire for three nights from under a root. You should know two things. The old troll at the stones didn't come for us: it came for the door. And on the second night something walked past my root in Granheim mail, like Eirik's. I called to it. It turned, and it didn't know its name. When the stones broke I heard Bjarne's hammer start up, somewhere deep in the woods, slow, like he's working one-handed. Let me be of use until we find them.");
        Welcome("Prologue/Residents/Bjarne", "I blazed the trees going in, a cut every twenty paces. Coming back, my marks were on the wrong side of them. The road hadn't moved. The roots had moved it, under us, in the night. Tor was thrown further in than me, with a bad leg. Listen for a saw where nobody saws.");
        Welcome("Prologue/Residents/Tor", "There's a hollow in the deep woods where nothing grows, and the ground breathes there. In, out, slow as a sleeper. I lay three nights on this leg listening to it. Whatever is down there is old, and it's what moves the roots. Now, where do you want your planks?");
    }

    private static void Welcome(string resident, string line)
    {
        var data = Load<ResidentDefinition>("Assets/_Project/Data/" + resident + ".asset") ?? throw new InvalidOperationException("No resident " + resident);
        Edit(data, so => so.FindProperty("welcomeLine").stringValue = line);
    }

    // ----- Home: the stories -----

    private static string Home(QuestDefinition hunt, QuestDefinition road, GameEventDefinition moment, GameEventDefinition roadNorthHeard)
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScene);
        Transform root = GameObject.Find(Moments)?.transform ?? throw new InvalidOperationException("No " + Moments + " in " + HomeScene);
        HomePortal portal = Object.FindAnyObjectByType<HomePortal>(FindObjectsInactive.Include);
        StoryScript waking = root.Find("Story - The Waking").GetComponent<StoryScript>();
        SayNode voice = waking.Nodes.OfType<SayNode>().First();
        StorySpeaker Portal() => new StorySpeaker { player = voice.speaker.player, name = voice.speaker.name, model = voice.speaker.model, portrait = voice.speaker.portrait, voice = voice.speaker.voice, voiceFrom = voice.speaker.voiceFrom };
        SayNode Say(string line) => new SayNode { speaker = Portal(), line = line };
        SayNode Aside(string line) => new SayNode { speaker = Portal(), line = line, aside = true, afterFightPause = false };

        Interactable[] stones = Object.FindObjectsByType<QuestGate>(FindObjectsInactive.Include)
            .Where(g => g.name == "RootboundStone" && g.transform.parent != null && g.transform.parent.name == "Rootbound Stones (Life Orbs)").OrderBy(g => g.transform.position.x).Select(g => g.GetComponent<Interactable>()).ToArray();
        if (stones.Length != 3) throw new InvalidOperationException($"Expected three rootbound stones, found {stones.Length}");
        Transform nearest = stones.OrderBy(s => (s.transform.position - portal.transform.position).sqrMagnitude).First().transform;
        StoneMarks(stones);

        // ----- The first light shows the roots -----
        StoryScript seen = StoryBuilder.Fresh(root, "Story - The Roots Move");
        var freeThem = new WhenQuest { quest = Q("RootboundStones"), when = WhenQuest.Moment.Starts };
        var afterFire = new WaitNode { seconds = 6f };
        var look = new FocusCameraNode { target = nearest, seconds = 4f, revealRadius = 10f };
        var roots = Say("<i>Look, now that there is light to see by. Three of my stones, choked in roots. And the roots are moving, little one. Slowly. Towards me.</i>");
        StoryBuilder.Link(seen, (freeThem, 0, afterFire), (afterFire, 0, look), (afterFire, 0, roots));

        // ----- Each stone remembers a piece of the pact -----
        StoryScript remember = StoryBuilder.Fresh(root, "Story - The Stones Remember");
        string[] pieces =
        {
            "<i>I remember this. A fire beside my stones, and someone to keep it.</i>",
            "<i>And this. Their door, and the night kept from it, for as long as the fire burned.</i>",
            "<i>And the guardian under the roots, who kept the forest's half. Where is it now?</i>",
        };
        for (int i = 0; i < stones.Length; i++)
        {
            var freed = new WhenUsed { target = stones[i] };
            StoryBuilder.Link(remember, (freed, 0, new WaitNode { seconds = 0.8f }));
            StoryBuilder.Link(remember, (freed, 0, Aside(pieces[i])));
        }

        // ----- Something comes up through the thin ward -----
        MobSpawnZone zone = Zone(root, "Rootlings Under the Ward", Centre(stones) * 0.5f + portal.transform.position * 0.5f);
        StoryScript upThrough = StoryBuilder.Fresh(root, "Story - Up Through the Ward");
        var allFree = new WhenQuest { quest = Q("RootboundStones"), when = WhenQuest.Moment.IsCompleted };
        var rumble = new ShakeCameraNode { strength = 0.4f };
        var crack = new PlaySoundNode { sound = Load<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Tree Crack.asset"), at = zone.transform };
        var warn = Aside("<i>Little one, behind you! My ward is too thin yet. They are coming up through it.</i>");
        var spawn = new SpawnEnemiesNode { zone = zone, waitUntilDefeated = true };
        var after = Aside("<i>Rootlings, in my clearing. Feed me those orbs, and they will not get in so easily again.</i>");
        StoryBuilder.Link(upThrough, (allFree, 0, rumble), (allFree, 0, crack), (allFree, 0, warn), (allFree, 0, spawn), (spawn, 0, after));

        // ----- After the first breach: the root in them -----
        StoryScript rootIn = StoryBuilder.Fresh(root, "Story - The Root in Them");
        var held = new WhenQuest { quest = Q("TheWeakeningWard"), when = WhenQuest.Moment.IsCompleted };
        var breathe = new WaitNode { seconds = 3f };
        var sawIt = Say("<i>Did you see what grew out of them as they fell? Root. The same root that choked my stones, and closed the road behind your people.</i>");
        var oneThing = Say("<i>It is all one thing, little one. Something old is pushing those roots, from somewhere deep in the woods. When the woods tell me where, I will tell you.</i>");
        StoryBuilder.Link(rootIn, (held, 0, breathe), (breathe, 0, sawIt), (sawIt, 0, oneThing));

        // ----- The road north: what the Heartroot was -----
        StoryScript twist = StoryBuilder.Fresh(root, "Story - The Road North");
        var told = new WhenQuestEvent { questEvent = moment };
        var bringIt = Say("<i>You brought it down. Lay what you carried out of the hollow by the fire, little one, where it can feel the warmth. It is owed that much.</i>");
        var truth = Say("<i>I have not told you everything. The Heartroot was no monster. It was the forest's guardian: its half of the pact. Your kind let the hearth beside me go cold a hundred winters ago. It kept its half for ninety winters after that, alone, until the hunger got into its roots.</i>");
        var answer = new ChoiceNode
        {
            speaker = Portal(),
            line = "<i>That is what you killed. I am sorry. Someone had to.</i>",
            answers = new List<string> { "Then we broke the pact first.", "Why didn't you tell me?" },
            mustAnswer = true,
        };
        var broke = Say("<i>Your people did, long before you were born. You are mending it. Every fire kept here is a payment on that debt.</i>");
        var whyNot = Say("<i>Would you have woken it, knowing? It could not be saved, only stopped. Better by the hand of someone who keeps a hearth.</i>");
        var coin = new HasHappenedNode { questEvent = Prologue("CameForCoin") };
        var kin = new HasHappenedNode { questEvent = Prologue("CameForKin") };
        var why = new HasHappenedNode { questEvent = Prologue("CameForTruth") };
        var forCoin = Say("<i>You came for coin. There is none in this. But there is a debt now, and you have begun to pay it.</i>");
        var forKin = Say("<i>You came for someone. Every one of your people is out there because of what was let go cold. Bring them home, and the debt grows smaller.</i>");
        var forTruth = Say("<i>You came to see if the stories were true. Here is the one nobody tells: we broke the pact first.</i>");
        var title = Say("<i>Its last word was for you. Hearth-keeper. Nobody has called anyone that in a hundred winters, and I did not teach it to you. One day you will tell me why these stones woke for your hand.</i>");
        var north = Say("<i>With its roots gone quiet I can reach further. North, past the troll country, the road is open. The old one that broke my stones the night you fell walks that road. It came for the door then. Go carefully.</i>");
        var done = new RaiseQuestEventNode { questEvent = roadNorthHeard };
        StoryBuilder.Link(twist, (told, 0, bringIt), (bringIt, 0, truth), (truth, 0, answer), (answer, 0, broke), (answer, 1, whyNot),
            (broke, 0, coin), (whyNot, 0, coin), (coin, 0, forCoin), (coin, 1, kin), (kin, 0, forKin), (kin, 1, why), (why, 0, forTruth), (why, 1, title),
            (forCoin, 0, title), (forKin, 0, title), (forTruth, 0, title), (title, 0, north), (north, 0, done));

        Whispers(portal, hunt, road, moment);
        StoryBuilder.Finish();
        SaveableEntityTools.AssignSceneIds(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string problems = string.Join("; ", new[] { seen, remember, upThrough, rootIn, twist }.SelectMany(s => s.FindProblems().Select(p => s.name + ": " + p)));
        return $"home: 5 stories, rootlings at {zone.transform.position:F0}, story problems: {(problems.Length > 0 ? problems : "none")}, dirty={scene.isDirty}";
    }

    /// <summary>Only the three stones' marks on the maps (`--entry BuildFirstAct.Marks`): the user asked to see them (2026-11-12).</summary>
    public static string Marks()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty) return "the open scene " + SceneManager.GetSceneAt(i).name + " has unsaved changes";
        }

        Scene scene = EditorSceneManager.OpenScene(HomeScene);
        Interactable[] stones = Object.FindObjectsByType<QuestGate>(FindObjectsInactive.Include)
            .Where(g => g.name == "RootboundStone" && g.transform.parent != null && g.transform.parent.name == "Rootbound Stones (Life Orbs)").OrderBy(g => g.transform.position.x).Select(g => g.GetComponent<Interactable>()).ToArray();
        if (stones.Length != 3) return $"expected three rootbound stones, found {stones.Length}";
        string marks = StoneMarks(stones);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{marks}; dirty={scene.isDirty}";
    }

    // Each stone shows on the minimap and the world map (and the tracker points to it) while The Rootbound Stones is open,
    // until its own orb is freed.
    private static string StoneMarks(Interactable[] stones)
    {
        QuestDefinition quest = Q("RootboundStones");
        var placed = new List<string>();
        foreach (Interactable stone in stones)
        {
            Transform old = stone.transform.Find("Quest Mark");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var mark = new GameObject("Quest Mark");
            mark.transform.SetParent(stone.transform, false);
            var waypoint = mark.AddComponent<QuestWaypoint>();
            waypoint.Configure(quest, 1, "Rootbound stone", QuestWaypointWhen.Active, stone.GetComponent<ResourceNode>());
            EditorUtility.SetDirty(waypoint);
            placed.Add($"{stone.transform.position:F0}{(waypoint.UntilGathered != null ? "" : " (no node!)")}");
        }

        return "stone marks at " + string.Join(", ", placed);
    }

    // What the portal says while each new quest is open. The story's own steps go last in the list, so they speak first.
    private static void Whispers(HomePortal portal, QuestDefinition hunt, QuestDefinition road, GameEventDefinition moment)
    {
        var so = new SerializedObject(portal);
        SerializedProperty whispers = so.FindProperty("questWhispers");
        for (int i = whispers.arraySize - 1; i >= 0; i--)
        {
            Object owner = whispers.GetArrayElementAtIndex(i).FindPropertyRelative("quest").objectReferenceValue;
            if (owner == hunt || owner == road) whispers.DeleteArrayElementAtIndex(i);
        }

        foreach ((QuestDefinition quest, string text, GameEventDefinition said) in new[]
        {
            (hunt, "<i>Now the woods have told me. Something breathes under the roots, little one, in a hollow deep in the Misty Woods: a guardian, as old as my stones. It will not wake for steel. Take an ember from your own fire to it. That, it cannot ignore.</i>", (GameEventDefinition)null),
            (road, "<i>Come and sit by me, little one. There is something I should have told you before you went to that hollow.</i>", moment),
        })
        {
            int at = whispers.arraySize++;
            SerializedProperty whisper = whispers.GetArrayElementAtIndex(at);
            whisper.FindPropertyRelative("quest").objectReferenceValue = quest;
            whisper.FindPropertyRelative("text").stringValue = text;
            whisper.FindPropertyRelative("moment").objectReferenceValue = said;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Two of the prologue's unsaved rootlings, hunting whoever is near, only when a story asks.
    private static MobSpawnZone Zone(Transform root, string name, Vector3 at)
    {
        Transform old = root.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var zoneObject = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/_Project/Prefabs/Raids/Mob Spawn Zone.prefab"));
        zoneObject.name = name;
        zoneObject.transform.SetParent(root, true);
        zoneObject.transform.position = NavMesh.SamplePosition(at, out NavMeshHit hit, 8f, NavMesh.AllAreas) ? hit.position : at;
        MobSpawnZone zone = zoneObject.GetComponent<MobSpawnZone>();
        var data = new SerializedObject(zone);
        SerializedProperty creatures = data.FindProperty("creatures");
        creatures.ClearArray();
        creatures.InsertArrayElementAtIndex(0);
        creatures.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = Load<GameObject>("Assets/_Project/Data/Prologue/ExpeditionRootling.prefab");
        creatures.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
        data.FindProperty("count").vector2IntValue = new Vector2Int(2, 2);
        data.FindProperty("trigger").intValue = (int)MobSpawnTrigger.Manual;
        data.FindProperty("wakeDistance").floatValue = 40f;
        data.FindProperty("huntPlayer").boolValue = true;
        data.FindProperty("respawn").boolValue = false;
        SerializedProperty corners = data.FindProperty("corners");
        const float r = 3f;
        Vector3[] square = { new Vector3(-r, 0f, -r), new Vector3(r, 0f, -r), new Vector3(r, 0f, r), new Vector3(-r, 0f, r) };
        corners.arraySize = square.Length;
        for (int i = 0; i < square.Length; i++) corners.GetArrayElementAtIndex(i).vector3Value = square[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        return zone;
    }

    private static Vector3 Centre(Interactable[] stones) => stones.Aggregate(Vector3.zero, (sum, s) => sum + s.transform.position) / stones.Length;

    // ----- Helpers -----

    private static QuestDefinition Q(string file) => Load<QuestDefinition>(Quests + file + ".asset") ?? throw new InvalidOperationException("Missing quest " + file);

    private static GameEventDefinition Prologue(string file) => Load<GameEventDefinition>("Assets/_Project/Data/Prologue/Events/" + file + ".asset") ?? throw new InvalidOperationException("Missing event " + file);

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

    private static GameEventDefinition Event(string file, string id, string name)
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
        });
        return gameEvent;
    }

    private static T Copy<T>(string from, string to) where T : Object
    {
        if (Load<T>(to) == null && !AssetDatabase.CopyAsset(from, to)) throw new InvalidOperationException("Could not make " + to);
        return Load<T>(to);
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
