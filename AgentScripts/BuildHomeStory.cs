using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Why the player builds a haven (`unity command run_script --file AgentScripts/BuildHomeStory.cs --entry BuildHomeStory.Apply`):
/// the forest forgets the lost, a tended hearth is the one thing it can't forget, so lighting one and making a home is how
/// the seven are found and brought back. Rewrites the home quests around that, turns "A Stranger in the Mist" into the
/// search for Ingrid, adds "A Light They Can See" (raise the haven to a Hamlet), and builds the moments as story graphs in
/// SafeHaven under Home Moments. Replaces what it built before. Reopens SafeHaven.
/// </summary>
public static class BuildHomeStory
{
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string Quests = "Assets/_Project/Data/Quests/";
    private const string Root = "Home Moments";
    private const string TheLostExpedition = "The Lost Expedition";

    public static string Apply()
    {
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";

        GameEventDefinition waking = Event("TheWaking", "home.the_waking", "");
        GameEventDefinition hamlet = Event("HavenIsAHamlet", "home.haven_hamlet", "Raise Safe Haven to a Hamlet (H)");
        Event("TravelerFreed", null, "Find Ingrid in the Misty Woods");
        Event("AlchemistHired", null, "Speak to Ingrid and give her work");
        QuestDefinition light = ALightTheyCanSee(hamlet);
        QuestTexts();
        BringIngridHome();
        Ingrid();
        Breach();
        Haven(hamlet);
        Load<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset").CollectAll();
        EditorUtility.SetDirty(Load<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset"));
        AssetDatabase.SaveAssets();

        string raid = RaidWaypoint();
        string home = HomeMoments();
        return $"{light.DisplayName}: {string.Join("; ", light.FindProblems())} | {raid} | {home}";
    }

    // ----- Quests -----

    private static void QuestTexts()
    {
        Text("Awakening", null, "You wake beneath a great tree in a quiet clearing, and you wake alone. Its bark is warm, and something in it seems to be waiting for you.");
        Text("SettlingIn", null, "A hearth starts small. Gather the fallen branches and loose stones the clearing offers, by hand; watch your resource bars (I) fill.");
        Text("FirstCamp", "A Light in the Wood", "The forest cannot forget a hearth that someone tends. Light one inside the Heart Tree's ward so your people can see it through the mist, and set a table beside it to work at. Open the build menu with G.");
        Text("FoodAndWater", "Strength for the Road", "Eirik was right: nobody fights well on an empty stomach, and you will be walking people home through the Misty Woods. Pick berries, fetch water from the stream and stew a meal at the campfire.");
        Text("RootboundStones", null, "The door to the Misty Woods needs the Heart Tree's light, and cursed roots have reached inside the ward to choke it. Three stones around the clearing still hold that light. Free their Life Orbs.");
        Text("AwakeningThePortal", null, "Across the stream stand the stones that dropped you here. Feed them the Life Orbs and the way back to your people opens.");
        Text("ThroughThePortal", null, "Your people are past the door, and the Misty Woods won't wait. Craft stone tools by hand from your bag (B), then step through. Bring home hardwood and iron ore for the haven, and get back to an extraction point before the portal closes. Carried things are lost if you fall; your tools and weapon come back damaged.");
        Text("KnowYourEnemy", null, "Forest-Bitten Wolves hunt the Misty Woods, on the same paths your people must walk home. Thin their numbers before they grow bolder.");
        Text("AGrowingHome", null, "Ingrid was the first. When the others come through that door they will need more than a fire: Tor will ask for a sawbench before he asks for supper, Bjarne for a furnace, and everyone for somewhere dry to set down what they carried. Build them.");
        Text("TheWeakeningWard", null, "The light that called Ingrid home was seen by other things too. Something is testing the ward. Prepare, then speak to the Heart Tree when you are ready.");
        Text("SmithsRequest", null, "An old note on the board, in the last smith's hand: this hearth wants copper, and proof you can work it.");
    }

    private static void Text(string file, string displayName, string description)
    {
        var quest = Quest(file);
        var so = new SerializedObject(quest);
        if (displayName != null) so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = description;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
    }

    // The traveller was a stranger found by chance. She is one of the seven, so the portal sends the player looking for her.
    private static void BringIngridHome()
    {
        QuestDefinition quest = Quest("AStrangerInTheMist");
        var so = new SerializedObject(quest);
        so.FindProperty("displayName").stringValue = "Bring Ingrid Home";
        so.FindProperty("description").stringValue = "Sigrid's voice came through the stones: Ingrid was right behind her when they broke, and she is hurt. Find her in the Misty Woods, keep her close, and bring her through an extraction point with you. If she falls she will wait where you found her.";
        so.FindProperty("chain").stringValue = TheLostExpedition;
        so.FindProperty("start").intValue = (int)QuestStart.Automatically;
        so.FindProperty("startEvent").objectReferenceValue = null;
        so.FindProperty("objectivesInOrder").boolValue = true;
        SerializedProperty prerequisites = so.FindProperty("prerequisites");
        prerequisites.arraySize = 1;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = Quest("AwakeningThePortal");
        SerializedProperty objectives = so.FindProperty("objectives");
        objectives.arraySize = 2;
        Objective(objectives.GetArrayElementAtIndex(0), Load<GameEventDefinition>(Quests + "Events/TravelerFreed.asset"));
        Objective(objectives.GetArrayElementAtIndex(1), Load<GameEventDefinition>(Quests + "Events/TravelerRescued.asset"));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
    }

    private static QuestDefinition ALightTheyCanSee(GameEventDefinition hamlet)
    {
        string path = Quests + "ALightTheyCanSee.asset";
        var quest = Load<QuestDefinition>(path);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestDefinition>();
            AssetDatabase.CreateAsset(quest, path);
        }

        var so = new SerializedObject(quest);
        so.FindProperty("id").stringValue = "a_light_they_can_see";
        so.FindProperty("displayName").stringValue = "A Light They Can See";
        so.FindProperty("description").stringValue = "The brighter Safe Haven burns, the further its light carries through the mist, and six of your people are still out there. Build, clear the overgrowth and raise the haven to a Hamlet. The Heart Tree's Haven page (H) shows what counts.";
        so.FindProperty("chain").stringValue = TheLostExpedition;
        so.FindProperty("start").intValue = (int)QuestStart.Automatically;
        SerializedProperty prerequisites = so.FindProperty("prerequisites");
        prerequisites.arraySize = 2;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = Quest("TheWeakeningWard");
        prerequisites.GetArrayElementAtIndex(1).objectReferenceValue = Quest("AGrowingHome");
        SerializedProperty objectives = so.FindProperty("objectives");
        objectives.arraySize = 1;
        Objective(objectives.GetArrayElementAtIndex(0), hamlet);
        SerializedProperty rewards = so.FindProperty("rewardResources");
        rewards.arraySize = 1;
        rewards.GetArrayElementAtIndex(0).FindPropertyRelative("resource").objectReferenceValue = LifeOrb();
        rewards.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
        return quest;
    }

    // What the Rootbound Stones quest gathers, so the essence isn't named twice.
    private static Object LifeOrb() => Quest("RootboundStones").Objectives[0].Resource;

    private static void Objective(SerializedProperty objective, GameEventDefinition questEvent)
    {
        objective.FindPropertyRelative("type").intValue = (int)ObjectiveType.QuestEvent;
        objective.FindPropertyRelative("description").stringValue = "";
        objective.FindPropertyRelative("amount").intValue = 1;
        objective.FindPropertyRelative("questEvent").objectReferenceValue = questEvent;
        objective.FindPropertyRelative("countAlreadyOwned").boolValue = true;
    }

    private static GameEventDefinition Event(string file, string id, string objectiveText)
    {
        string path = Quests + "Events/" + file + ".asset";
        var gameEvent = Load<GameEventDefinition>(path);
        if (gameEvent == null)
        {
            gameEvent = ScriptableObject.CreateInstance<GameEventDefinition>();
            AssetDatabase.CreateAsset(gameEvent, path);
        }

        var so = new SerializedObject(gameEvent);
        if (id != null) so.FindProperty("id").stringValue = id;
        so.FindProperty("objectiveText").stringValue = objectiveText;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameEvent);
        return gameEvent;
    }

    // ----- People and places -----

    private static void Ingrid()
    {
        var ingrid = Load<ResidentDefinition>("Assets/_Project/Data/Settlement/Ingrid.asset");
        var so = new SerializedObject(ingrid);
        so.FindProperty("description").stringValue = "The expedition's alchemist, and the first of the seven to come home. She knows more about the curse than she lets on.";
        so.FindProperty("rescueLine").stringValue = "It's you! I saw a light through the trees and thought the forest had finally taken my wits. Get me out of here.";
        so.FindProperty("arrivalLine").stringValue = "I kept saying the word so the forest couldn't take it. Fire. Fire. Fire. And then I saw yours.";
        so.FindProperty("welcomeLine").stringValue = "I watched your fire for three nights from under a root. When the stones broke, Bjarne was thrown north, towards the troll country. The rest I couldn't see. Let me be of use until we find them.";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ingrid);
    }

    private static void Breach()
    {
        var breach = Load<WardBreachDefinition>("Assets/_Project/Data/Settlement/FirstWardBreach.asset");
        var so = new SerializedObject(breach);
        so.FindProperty("warningText").stringValue = "The Heart Tree's light flickers. Your fire has been seen by more than your friends: something is testing the ward along the old path. Prepare, then speak to the tree.";
        so.FindProperty("wonText").stringValue = "The last wolf falls. The Heart Tree's light steadies and the ward closes again. The home you built held.";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(breach);
    }

    private static void Haven(GameEventDefinition hamlet)
    {
        var settings = Load<HavenSettings>("Assets/_Project/Resources/HavenSettings.asset");
        var so = new SerializedObject(settings);
        SerializedProperty ranks = so.FindProperty("ranks");
        for (int i = 0; i < ranks.arraySize; i++)
        {
            SerializedProperty rank = ranks.GetArrayElementAtIndex(i);
            if (rank.FindPropertyRelative("name").stringValue == "Hamlet") rank.FindPropertyRelative("reachedEvent").objectReferenceValue = hamlet;
        }

        string[] people = { "Prologue/Residents/Sigrid", "Settlement/Ingrid", "Prologue/Residents/Bjarne", "Prologue/Residents/Tor", "Prologue/Residents/Eirik", "Prologue/Residents/Runa", "Prologue/Residents/Liv" };
        SerializedProperty expedition = so.FindProperty("expedition");
        expedition.arraySize = people.Length;
        for (int i = 0; i < people.Length; i++)
        {
            expedition.GetArrayElementAtIndex(i).objectReferenceValue = Load<ResidentDefinition>($"Assets/_Project/Data/{people[i]}.asset");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    // The arrow in the raid points to Ingrid until she is freed; she walks with the player after that.
    private static string RaidWaypoint()
    {
        string path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("Raid_MistyWoods t:Scene").First());
        Scene scene = EditorSceneManager.OpenScene(path);
        RescuableTraveler traveler = Object.FindAnyObjectByType<RescuableTraveler>(FindObjectsInactive.Include);
        if (traveler == null) return "no traveler in " + path;

        const string name = "Quest Waypoint - Ingrid";
        Transform old = traveler.transform.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(traveler.transform, false);
        go.AddComponent<QuestWaypoint>().Configure(Quest("AStrangerInTheMist"), 1, "Ingrid");
        EditorSceneManager.SaveScene(scene);
        return "waypoint on " + traveler.name;
    }

    // ----- Home -----

    private static string HomeMoments()
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScene);
        var waking = Load<GameEventDefinition>(Quests + "Events/TheWaking.asset");
        var talked = Load<GameEventDefinition>(Quests + "Events/TalkedToHeartTree.asset");
        HeartTree tree = Object.FindAnyObjectByType<HeartTree>(FindObjectsInactive.Include);
        RaidPortal portal = Object.FindAnyObjectByType<RaidPortal>(FindObjectsInactive.Include);

        GameObject old = GameObject.Find(Root);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(Root).transform;
        TheWaking(root, waking, talked);
        FirstFire(root, tree.transform);
        TheDoorWakes(root, portal.transform);
        BjarnesHammer(root);

        Whispers(tree, waking);
        var portalData = new SerializedObject(portal);
        portalData.FindProperty("ruinText").stringValue = "The stones that dropped you here, cracked and cold. A faint hum lingers in them, and under it a voice you know. Life Orbs might wake them.";
        portalData.ApplyModifiedPropertiesWithoutUndo();
        var board = new SerializedObject(Object.FindAnyObjectByType<QuestGiver>(FindObjectsInactive.Include));
        board.FindProperty("greeting").stringValue = "\"To whoever comes after: this hearth wants copper, and hands that can work it. Smelt ingots and make a pickaxe. What I left behind this board is yours.\" - the last smith";
        board.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{root.childCount} stories, dirty={scene.isDirty}";
    }

    private static void TheWaking(Transform root, GameEventDefinition waking, GameEventDefinition talked)
    {
        StoryScript story = Story(root, "Story - The Waking");
        var heard = new WhenQuestEvent { questEvent = waking };
        var caught = new ChoiceNode
        {
            speaker = Tree(),
            line = "Warmth stirs in the old bark. <i>Little one. You fell out of the dark with the whole forest at your heels, and I caught you. I could only catch one.</i>",
            answers = new List<string> { "Where are the others?", "What are you?" },
            mustAnswer = true,
        };
        var whatAreYou = Say("<i>The last of an old promise. I keep the dark from this clearing. But that is not what you need to know.</i>");
        var alive = Say("<i>Seven sparks, scattered through the Misty Woods. They are alive. I hear them when the wind is right. But the forest does not kill the lost, little one. It forgets them.</i>");
        var forgets = new ChoiceNode
        {
            speaker = Tree(),
            line = "<i>First the way home. Then their names. Then there is no one left to find.</i>",
            answers = new List<string> { "How do I reach them?", "Open the door. I'll go now." },
            mustAnswer = true,
        };
        var nothing = Say("<i>With what? You have nothing but your hands, and I cannot open it.</i>");
        var pact = Say("<i>The door across the stream sleeps, and I am too thin to wake it. Long ago people kept a hearth beneath my branches, and I kept the night from their door. That was the pact. No fire has burned here in a hundred winters.</i>");
        var hearth = Say("<i>So light one. The forest cannot forget a hearth that someone tends. It will feed my ward, and out in the mist your people will see it and know which way is home.</i>");
        var plan = Say("<i>A fire first. Then a home worth coming back to. Then we go and fetch them. And one day, little one, you will tell me why those old stones woke for your hand.</i>");
        var done = new RaiseQuestEventNode { questEvent = talked };
        Link(story, (heard, 0, caught), (caught, 0, alive), (caught, 1, whatAreYou), (whatAreYou, 0, alive), (alive, 0, forgets),
            (forgets, 0, pact), (forgets, 1, nothing), (nothing, 0, pact), (pact, 0, hearth), (hearth, 0, plan), (plan, 0, done));
    }

    private static void FirstFire(Transform root, Transform tree)
    {
        StoryScript story = Story(root, "Story - First Fire");
        var built = new WhenBuilt { building = Campfire() };
        var wait = new WaitNode { seconds = 1.2f };
        var glow = new GlowNode { target = tree, colour = new Color(1f, 0.72f, 0.38f), intensity = 8f, range = 16f, fadeSeconds = 2f };
        var sound = new PlaySoundNode { sound = Load<HavenSettings>("Assets/_Project/Resources/HavenSettings.asset").RankSound, at = tree };
        var first = Say("<i>There. A hearth under my branches, after a hundred winters.</i>");
        var second = Say("<i>Do you feel that? The ward has remembered what it is for. And far out in the mist, little one, someone has stopped walking and turned to look.</i>");
        var fade = new GlowNode { target = tree, glow = false, fadeSeconds = 3f };
        Link(story, (built, 0, wait), (wait, 0, glow), (wait, 0, sound), (wait, 0, first), (first, 0, second), (second, 0, fade));
    }

    private static void TheDoorWakes(Transform root, Transform portal)
    {
        StoryScript story = Story(root, "Story - The Door Wakes");
        var repaired = new WhenQuest { quest = Quest("AwakeningThePortal"), when = WhenQuest.Moment.IsCompleted };
        var wait = new WaitNode { seconds = 1f };
        var focus = new FocusCameraNode { target = portal, seconds = 7f };
        var sigrid = new SayNode { speaker = new StorySpeaker { name = "Sigrid" }, line = "<i>...can anyone hear... Ingrid was right behind me when the stones broke. She's hurt. Find her first. Do you hear me? Find her first...</i>" };
        var tree = Say("<i>The stones have gone quiet, but she is right. One spark burns close, just past the door, hiding from the wolves. Make yourself tools, and go.</i>");
        Link(story, (repaired, 0, wait), (wait, 0, focus), (wait, 0, sigrid), (sigrid, 0, tree));
    }

    private static void BjarnesHammer(Transform root)
    {
        StoryScript story = Story(root, "Story - Bjarne's Hammer");
        var reached = new WhenQuest { quest = Quest("ALightTheyCanSee"), when = WhenQuest.Moment.IsCompleted };
        var wait = new WaitNode { seconds = 1.5f };
        var hammer = Say("<i>Listen. Far to the north, past the troll country, a hammer on stone. Three beats and a rest, over and over. Someone out there has seen your light and is answering it.</i>");
        var bjarne = new SayNode { speaker = new StorySpeaker { player = true }, line = "That's Bjarne." };
        var north = Say("<i>Then he is still himself. The way north is not open yet. Grow strong, little one, and keep this fire high. He can hold on a long while now.</i>");
        Link(story, (reached, 0, wait), (wait, 0, hammer), (hammer, 0, bjarne), (bjarne, 0, north));
    }

    private static void Whispers(HeartTree tree, GameEventDefinition waking)
    {
        (string quest, string text, GameEventDefinition moment)[] lines =
        {
            ("Awakening", "<i>Listen, little one. Rest your hand on the bark.</i>", waking),
            ("SettlingIn", "<i>Start with what the clearing gives. Fallen branches, loose stones, your own two hands. That is how every hearth began. Your resource bars (I) will fill.</i>", null),
            ("FirstCamp", "<i>Build your fire inside my ward, and a table to work at beside it. Open the build menu (G). The sooner it burns, the sooner they can see it.</i>", null),
            ("FoodAndWater", "<i>Now feed yourself. Berries, water from the stream, a stew over your fire. You will be walking people home.</i>", null),
            ("RootboundStones", "<i>The forest has put its roots in even here. Three stones around the clearing still hold my light, choked and cold. Free them, and bring the Life Orbs back.</i>", null),
            ("AwakeningThePortal", "<i>Now the door, across the stream. Feed it my light and it will open for you, as it did in the dark.</i>", null),
            ("ThroughThePortal", "<i>Make yourself tools and go. Take what the woods will give you, and come home before the door closes. It will not wait, and neither will the wolves.</i>", null),
            ("AStrangerInTheMist", "<i>She is close, little one. Just past the door, off the path, hiding. Find her, keep her near you, and bring her through.</i>", null),
            ("AGrowingHome", "<i>One fire brought Ingrid home. The others will want more than a fire: a bench, a furnace, a dry place to set down what they carry. Build what they will need.</i>", null),
            ("TheWeakeningWard", "<i>The light that reached her reached other things as well. They are on the old path now, testing me. Prepare yourself. When you are ready, tell me, and I will let them come.</i>", null),
            ("ALightTheyCanSee", "<i>The brighter this place burns, the further it can be seen. Build. Clear the old growth. Give them something worth walking towards.</i>", null),
        };

        var so = new SerializedObject(tree);
        SerializedProperty whispers = so.FindProperty("questWhispers");
        whispers.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            SerializedProperty whisper = whispers.GetArrayElementAtIndex(i);
            whisper.FindPropertyRelative("quest").objectReferenceValue = Quest(lines[i].quest);
            whisper.FindPropertyRelative("text").stringValue = lines[i].text;
            whisper.FindPropertyRelative("moment").objectReferenceValue = lines[i].moment;
        }

        string[] idle =
        {
            "<i>Every hearth under my branches is a little more of the old pact kept.</i>",
            "<i>The forest cannot forget a place that someone tends.</i>",
            "<i>I listen for your people every night. Some nights I hear them.</i>",
            "<i>A hundred winters I kept this clearing alone. I had forgotten how loud a home is. Do not stop.</i>",
        };
        SerializedProperty fallback = so.FindProperty("whispers");
        fallback.arraySize = idle.Length;
        for (int i = 0; i < idle.Length; i++)
        {
            fallback.GetArrayElementAtIndex(i).stringValue = idle[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ----- Helpers -----

    private static StorySpeaker Tree() => new StorySpeaker { name = "The Heart Tree" };

    private static SayNode Say(string line) => new SayNode { speaker = Tree(), line = line };

    private static BuildingDefinition Campfire() => AssetDatabase.FindAssets("t:BuildingDefinition").Select(AssetDatabase.GUIDToAssetPath)
        .Select(Load<BuildingDefinition>).First(building => building.DisplayName == "Campfire");

    private static StoryScript Story(Transform root, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<SaveableEntity>();
        return go.AddComponent<StoryScript>();
    }

    // Adds the nodes (in order of first appearance, laid out left to right) and the links.
    private static void Link(StoryScript story, params (StoryNode from, int output, StoryNode to)[] links)
    {
        var added = new List<StoryNode>(story.Nodes);
        var depth = new Dictionary<StoryNode, int>();
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

    private static QuestDefinition Quest(string file) => Load<QuestDefinition>(Quests + file + ".asset");

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
