using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// The intro's new moments (`unity command run_script --file AgentScripts/BuildIntroPart3.cs --entry BuildIntroPart3.Apply`):
/// the start moved back down the road; a choice of bow or axe before setting out (no fixed starting weapon); a cooking
/// chapter after the fire (pick berries in the clearing west of the camp, stew them with Eirik's waterskin) where small
/// moss trolls ambush the player; the old troll's tremor after the wolves (shake, footsteps, the camera turning to it as
/// it walks towards the fire); questions to ask each companion by the fire; companions' names turned to the camera. All
/// moments are story graphs in the scene.
/// Replaces what it built before. Reopens the scene that was open.
/// History: BuildPrologueMeadow.cs has since moved the start to the meadow and replaced the bow or axe; don't run this again
/// on the current scene.
/// </summary>
public static class BuildIntroPart3
{
    private const string PrologueScene = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    private const string Prologue = "Assets/_Project/Data/Prologue/";
    private const string Root = "Intro Moments";
    private const float StartMovedBack = 22f;

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";

        Event("WeaponChosen", "prologue.weapon_chosen");
        Event("TrollAmbush", "prologue.troll_ambush");
        string data = Data(BerriesQuest());
        AssetDatabase.SaveAssets();

        // Opening a scene unloads assets nothing holds on to, so they are loaded again afterwards.
        Scene scene = EditorSceneManager.OpenScene(PrologueScene);
        var weaponChosen = Load<GameEventDefinition>(Prologue + "Events/WeaponChosen.asset");
        var ambushSprung = Load<GameEventDefinition>(Prologue + "Events/TrollAmbush.asset");
        QuestDefinition berries = Quest("BerriesForThePot");
        PrologueDirector director = Object.FindAnyObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        var directorData = new SerializedObject(director);
        Transform start = (Transform)directorData.FindProperty("startPoint").objectReferenceValue;
        Transform camp = (Transform)directorData.FindProperty("campPoint").objectReferenceValue;
        PrologueCompanion[] companions = Object.FindObjectsByType<PrologueCompanion>();
        PrologueCompanion Companion(string name) => companions.First(c => c.Resident != null && c.Resident.DisplayName == name);
        Transform sigrid = Companion("Sigrid").transform, eirik = Companion("Eirik").transform, runa = Companion("Runa").transform;

        string moved = MoveStart(start, companions);
        Topics(companions);
        foreach (PrologueCompanion companion in companions)
        {
            Transform label = companion.transform.Find("Name");
            if (label != null && label.GetComponent<FaceCamera>() == null) label.gameObject.AddComponent<FaceCamera>();
        }

        GameObject old = GameObject.Find(Root);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(Root).transform;

        // ----- Bow or axe -----
        var choiceStory = Story(root, "Story - Bow or Axe", Vector3.zero);
        var started = new WhenQuestEvent { questEvent = director.Definition.StartedEvent, once = false };
        var chosenBefore = new HasHappenedNode { questEvent = weaponChosen };
        var wait = new WaitNode { seconds = 1.2f };
        var choice = new ChoiceNode
        {
            speaker = Speaker(sigrid, "Sigrid"),
            line = "Before we set out: the cart only had room for two weapons, and you are the one walking in front. The bow keeps them at a distance. The axe keeps them honest. Which will you carry?",
            answers = new List<string> { "The bow.", "The axe." },
            mustAnswer = true,
        };
        var giveBow = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("CrudeWoodenBow"), 1), new ItemAmount(Item("CrudeStoneArrow"), 99) } };
        var giveAxe = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("WoodsmansAxe"), 1) } };
        var bowChosen = new RaiseQuestEventNode { questEvent = weaponChosen };
        var axeChosen = new RaiseQuestEventNode { questEvent = weaponChosen };
        var bowLine = new SayNode { speaker = Speaker(runa, "Runa"), line = "A bow! Then there are two of us. It wants both hands, so your torch went into your pack. Press L to swap bow and torch whenever you need the light. Hold the left button to draw, let go to shoot." };
        var axeLine = new SayNode { speaker = Speaker(eirik, "Eirik"), line = "The axe. Good. It splits wolves and firewood alike, and you keep the torch in your other hand." };
        Link(choiceStory, (started, 0, chosenBefore), (chosenBefore, 1, wait), (wait, 0, choice),
            (choice, 0, giveBow), (giveBow, 0, bowChosen), (bowChosen, 0, bowLine),
            (choice, 1, giveAxe), (giveAxe, 0, axeChosen), (axeChosen, 0, axeLine));

        // ----- Berries for the pot, and the small trolls -----
        Vector3 clearingAt = Ground(camp.position.x - 25f, camp.position.z - 9f);
        var clearing = new GameObject("Berry Clearing").transform;
        clearing.SetParent(root, false);
        clearing.position = clearingAt;
        var bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Resources/BerriesNode.prefab");
        Vector2[] bushes = { new Vector2(-3f, 2f), new Vector2(2.5f, 3f), new Vector2(4f, -2f), new Vector2(-1f, -4f), new Vector2(-5f, -1f), new Vector2(1f, 0.5f) };
        foreach (Vector2 offset in bushes)
        {
            var bush = (GameObject)PrefabUtility.InstantiatePrefab(bushPrefab, scene);
            bush.transform.SetParent(clearing, true);
            bush.transform.position = Ground(clearingAt.x + offset.x, clearingAt.z + offset.y);
            bush.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        Waypoint(clearing, berries, 1, "Berries");
        Waypoint(camp, berries, 2, "Campfire");

        var zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/Mob Spawn Zone.prefab");
        var zoneObject = (GameObject)PrefabUtility.InstantiatePrefab(zonePrefab, scene);
        zoneObject.name = "Small Troll Ambush";
        zoneObject.transform.SetParent(root, true);
        zoneObject.transform.position = Ground(clearingAt.x - 11f, clearingAt.z - 6f);
        MobSpawnZone zone = zoneObject.GetComponent<MobSpawnZone>();
        var zoneData = new SerializedObject(zone);
        SerializedProperty creatures = zoneData.FindProperty("creatures");
        creatures.ClearArray();
        creatures.InsertArrayElementAtIndex(0);
        creatures.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/SmallMossTroll.prefab");
        creatures.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
        zoneData.FindProperty("count").vector2IntValue = new Vector2Int(2, 2);
        zoneData.FindProperty("trigger").intValue = (int)MobSpawnTrigger.Manual;
        zoneData.FindProperty("huntPlayer").boolValue = true;
        zoneData.FindProperty("respawn").boolValue = false;
        SerializedProperty corners = zoneData.FindProperty("corners");
        Vector3[] square = { new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, -4f), new Vector3(4f, 0f, 4f), new Vector3(-4f, 0f, 4f) };
        corners.arraySize = square.Length;
        for (int i = 0; i < square.Length; i++) corners.GetArrayElementAtIndex(i).vector3Value = square[i];
        zoneData.ApplyModifiedPropertiesWithoutUndo();

        var waterskin = Story(root, "Story - Eirik's Waterskin", new Vector3(0f, 0f, 1f));
        var berriesStart = new WhenQuest { quest = berries, when = WhenQuest.Moment.Starts };
        var giveSkin = new GiveItemsNode { items = { new ItemAmount(Item("Waterskin"), 1) } };
        Link(waterskin, (berriesStart, 0, giveSkin));

        var ambush = Story(root, "Story - Troll Ambush", new Vector3(0f, 0f, 2f));
        var near = new WhenPlayerNear { place = clearing, radius = 7f, once = false };
        var sprung = new HasHappenedNode { questEvent = ambushSprung };
        var picking = new CheckQuestNode { quest = berries };
        var remember = new RaiseQuestEventNode { questEvent = ambushSprung };
        var pause = new WaitNode { seconds = 2.5f };
        var shout = new SayNode { speaker = Speaker(runa, "Runa"), line = "Down! Moss trolls in the bushes, the small kind. Don't let them get behind you!" };
        var spawn = new SpawnEnemiesNode { zone = zone };
        var cleared = new WhenZoneCleared { zone = zone };
        var after = new SayNode { speaker = Speaker(runa, "Runa"), line = "Small ones never come alone and never come this far down. Something up in the high woods is scaring them. Finish the berries and get back to the fire." };
        Link(ambush, (near, 0, sprung), (sprung, 1, picking), (picking, 1, remember), (remember, 0, pause), (pause, 0, spawn),
            (pause, 0, shout), (cleared, 0, after));

        // ----- The old troll -----
        Vector3 away = Ground(camp.position.x + 30f, camp.position.z - 38f);
        Vector3 closer = Ground(camp.position.x + 21f, camp.position.z - 26f);
        var trollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/OldMossTroll.prefab");
        var troll = (GameObject)PrefabUtility.InstantiatePrefab(trollPrefab, scene);
        troll.name = "The Old Troll (story)";
        troll.transform.SetParent(root, true);
        troll.transform.SetPositionAndRotation(away, Quaternion.LookRotation(new Vector3(camp.position.x - away.x, 0f, camp.position.z - away.z)));
        troll.transform.localScale = Vector3.one * 1.8f;
        foreach (Behaviour behaviour in troll.GetComponentsInChildren<Behaviour>(true))
            if (behaviour is MonsterAI || behaviour is NavMeshAgent) behaviour.enabled = false;
        foreach (Collider collider in troll.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        troll.SetActive(false);
        var trollGoes = new GameObject("Old Troll Walks To").transform;
        trollGoes.SetParent(root, false);
        trollGoes.position = closer;

        var tremor = Story(root, "Story - The Old Troll", new Vector3(0f, 0f, 3f));
        var leaving = new WhenQuest { quest = Quest("ReachPortal"), when = WhenQuest.Moment.Starts };
        var show = new SetActiveNode { target = troll, active = true };
        var stomp = Load<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/TrollStep.asset");
        var shake = new ShakeCameraNode { strength = 0.75f };
        var flash = new LightningNode();
        var moonlit = new GlowNode { target = troll.transform, colour = new Color(0.55f, 0.65f, 0.85f), intensity = 9f, range = 22f, fadeSeconds = 0.6f };
        var flashAgain = new WaitNode { seconds = 1.6f };
        var secondFlash = new LightningNode();
        var thunder = new PlaySoundNode { sound = Load<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Thunder.asset"), at = troll.transform };
        var step = new PlaySoundNode { sound = stomp, at = troll.transform };
        var walk = new MoveObjectNode { target = troll.transform, destination = trollGoes, seconds = 9f };
        var look = new FocusCameraNode { target = troll.transform, seconds = 4f };
        var sigridSees = new SayNode { speaker = Speaker(sigrid, "Sigrid"), line = "That is no thunder. An old one, a mountain troll, and it is walking towards our fire." };
        var runaSees = new SayNode { speaker = Speaker(runa, "Runa"), line = "It hasn't seen us yet. It will smell the smoke soon enough." };
        var go = new SayNode { speaker = Speaker(sigrid, "Sigrid"), line = "Leave the fire! Follow the old road north, to the standing stones. Now!" };
        Link(tremor, (leaving, 0, show), (show, 0, shake), (show, 0, step), (show, 0, walk), (show, 0, look), (show, 0, flash),
            (show, 0, moonlit), (show, 0, flashAgain), (flashAgain, 0, secondFlash), (flashAgain, 0, thunder),
            (look, 0, sigridSees), (sigridSees, 0, runaSees), (runaSees, 0, go));
        StoryNode last = go;
        for (int i = 0; i < 3; i++)
        {
            var gap = new WaitNode { seconds = 10f };
            var again = new ShakeCameraNode { strength = 0.35f + i * 0.1f };
            var thud = new PlaySoundNode { sound = stomp, at = troll.transform };
            Link(tremor, (last, 0, gap), (gap, 0, again), (gap, 0, thud));
            last = gap;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string problems = string.Join("; ", root.GetComponentsInChildren<StoryScript>().SelectMany(s => s.FindProblems()));
        if (!string.IsNullOrEmpty(previous) && previous != PrologueScene) EditorSceneManager.OpenScene(previous);
        return $"{data}; {moved}; clearing {clearingAt:F0}; troll {away:F0}; problems: {(problems.Length > 0 ? problems : "none")}";
    }

    // ----- Data -----

    private static string Data(QuestDefinition berries)
    {
        var prologue = Load<PrologueDefinition>(Prologue + "ForestPrologue.asset");
        var so = new SerializedObject(prologue);
        so.FindProperty("startingWeapon").objectReferenceValue = null;
        SerializedProperty chapters = so.FindProperty("chapters");
        int at = -1;
        for (int i = 0; i < chapters.arraySize; i++)
        {
            SerializedProperty chapter = chapters.GetArrayElementAtIndex(i);
            Object quest = chapter.FindPropertyRelative("quest").objectReferenceValue;
            if (quest == berries)
            {
                chapters.DeleteArrayElementAtIndex(i);
                i--;
                continue;
            }

            if (quest == Quest("LightFire")) at = i + 1;
            if (quest == Quest("ReachPortal")) chapter.FindPropertyRelative("arrivalText").stringValue = "";
        }

        chapters.InsertArrayElementAtIndex(at);
        SerializedProperty added = chapters.GetArrayElementAtIndex(at);
        added.FindPropertyRelative("quest").objectReferenceValue = berries;
        added.FindPropertyRelative("behaviour").intValue = (int)PrologueStep.GatherFuel;
        added.FindPropertyRelative("arrivalText").stringValue =
            "Eirik: Warm hands, empty bellies. There are berry bushes in the clearing west of the shelter. Pick five and stew them over the fire; take my waterskin for the pot.";
        added.FindPropertyRelative("statusText").stringValue = "";
        added.FindPropertyRelative("duration").floatValue = 0f;
        added.FindPropertyRelative("initialEnemies").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prologue);

        QuestDefinition breath = Quest("CatchBreath");
        var breathData = new SerializedObject(breath);
        SerializedProperty prerequisites = breathData.FindProperty("prerequisites");
        prerequisites.arraySize = 1;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = berries;
        breathData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(breath);
        AssetDatabase.SaveAssets();
        return $"cooking chapter {at}";
    }

    private static QuestDefinition BerriesQuest()
    {
        string path = Prologue + "Quests/BerriesForThePot.asset";
        var quest = Load<QuestDefinition>(path);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestDefinition>();
            AssetDatabase.CreateAsset(quest, path);
        }

        var so = new SerializedObject(quest);
        so.FindProperty("id").stringValue = "prologue.berries";
        so.FindProperty("displayName").stringValue = "Berries for the Pot";
        so.FindProperty("description").stringValue = "Eirik says nobody fights well on an empty stomach. Pick berries in the clearing west of the shelter and stew them over the campfire in his waterskin.";
        so.FindProperty("chain").stringValue = "Prologue - The Unquiet Forest";
        SerializedProperty prerequisites = so.FindProperty("prerequisites");
        prerequisites.arraySize = 1;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = Quest("LightFire");
        so.FindProperty("start").intValue = (int)QuestStart.Automatically;
        so.FindProperty("objectivesInOrder").boolValue = true;
        SerializedProperty objectives = so.FindProperty("objectives");
        objectives.arraySize = 2;
        Objective(objectives.GetArrayElementAtIndex(0), ObjectiveType.GatherResource, "Pick berries in the clearing west of the shelter", 5, Item("Materials/Berries"));
        Objective(objectives.GetArrayElementAtIndex(1), ObjectiveType.CraftItem, "Stew the berries at the campfire", 1, Item("StewedBerries"));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
        AssetDatabase.SaveAssets();
        return quest;
    }

    private static void Objective(SerializedProperty objective, ObjectiveType type, string description, int amount, ItemDefinition item)
    {
        objective.FindPropertyRelative("type").intValue = (int)type;
        objective.FindPropertyRelative("description").stringValue = description;
        objective.FindPropertyRelative("amount").intValue = amount;
        objective.FindPropertyRelative("item").objectReferenceValue = item;
        objective.FindPropertyRelative("resource").objectReferenceValue = null;
        objective.FindPropertyRelative("questEvent").objectReferenceValue = null;
        objective.FindPropertyRelative("mustExtract").boolValue = false;
        objective.FindPropertyRelative("countAlreadyOwned").boolValue = false;
    }

    private static GameEventDefinition Event(string file, string id)
    {
        string path = Prologue + "Events/" + file + ".asset";
        var gameEvent = Load<GameEventDefinition>(path);
        if (gameEvent == null)
        {
            gameEvent = ScriptableObject.CreateInstance<GameEventDefinition>();
            AssetDatabase.CreateAsset(gameEvent, path);
        }

        var so = new SerializedObject(gameEvent);
        so.FindProperty("id").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameEvent);
        return gameEvent;
    }

    // ----- Scene -----

    // The expedition starts further down the road, so the walk to the shelter is longer.
    private static string MoveStart(Transform start, PrologueCompanion[] companions)
    {
        Vector3 wanted = Ground(0f, -50f - StartMovedBack);
        Vector3 shift = wanted - start.position;
        if (shift.magnitude < 1f) return "start already moved";
        start.position = wanted;
        Player player = Object.FindAnyObjectByType<Player>(FindObjectsInactive.Include);
        if (player != null) player.transform.position = wanted;
        foreach (PrologueCompanion companion in companions)
            companion.transform.position = Ground(companion.transform.position.x + shift.x, companion.transform.position.z + shift.z);
        return $"start {wanted:F0}";
    }

    private static void Topics(PrologueCompanion[] companions)
    {
        var all = new Dictionary<string, string[]>
        {
            ["Sigrid"] = new[]
            {
                "Why did the villages send you?",
                "They did not send me. They asked for someone who could read the old marks, and I was the only one who answered. Draw your own conclusions. Everyone else does.",
                "What do the runes on your staff say?",
                "Words for things your tongue has no names for. Most of them mean wait. One of them means open. Pray we only need the first kind.",
                "Do you know what is doing this to the forest?",
                "I know what it feels like: a door left open in winter. Someone opened it on purpose. Whether they meant to let something in or out... that is what we came to find.",
            },
            ["Eirik"] = new[]
            {
                "How long have you been a soldier?",
                "Twenty winters in the shield wall at Granheim. I stopped counting the fights a long time ago. I still count the friends.",
                "Why did you join the expedition?",
                "My sister's farm is two valleys from here. Her dogs ran into these woods last month and never came back. Someone has to walk in front. Better me than you.",
                "Do you trust Sigrid?",
                "I trust that she wants to get out of here alive. That is enough for tonight. But watch her hands when she talks about the stones. She knows more than she says.",
            },
            ["Runa"] = new[]
            {
                "Where did you learn to shoot like that?",
                "Poaching on the jarl's land. You learn to hit with the first arrow when the second one brings the wardens.",
                "What did you see out there?",
                "Troll tracks. Small ones, the moss kind. They never come this far down. Something is pushing everything out of the high woods, and it is big.",
                "Any advice for the dark?",
                "Walk where the moss is thick; it's quieter. Keep your back to the fire, never your face. And if you hear something heavy breathing, don't run until I say so.",
            },
        };

        foreach (PrologueCompanion companion in companions)
        {
            if (companion.Resident == null || !all.TryGetValue(companion.Resident.DisplayName, out string[] lines)) continue;
            var so = new SerializedObject(companion);
            SerializedProperty topics = so.FindProperty("campTopics");
            topics.arraySize = lines.Length / 2;
            for (int i = 0; i < topics.arraySize; i++)
            {
                topics.GetArrayElementAtIndex(i).FindPropertyRelative("question").stringValue = lines[i * 2];
                topics.GetArrayElementAtIndex(i).FindPropertyRelative("answer").stringValue = lines[i * 2 + 1];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static StoryScript Story(Transform root, string name, Vector3 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = offset;
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

    private static void Waypoint(Transform at, QuestDefinition quest, int objective, string label)
    {
        string name = $"Quest Waypoint - {quest.DisplayName} {objective}";
        Transform old = at.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(at, false);
        go.AddComponent<QuestWaypoint>().Configure(quest, objective, label);
    }

    private static StorySpeaker Speaker(Transform model, string name) => new StorySpeaker { name = name, model = model };

    private static Vector3 Ground(float x, float z)
    {
        Terrain terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : Object.FindAnyObjectByType<Terrain>();
        var guess = new Vector3(x, 0f, z);
        guess.y = terrain != null ? terrain.SampleHeight(guess) + terrain.transform.position.y : 0f;
        return NavMesh.SamplePosition(guess, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : guess;
    }

    private static ItemDefinition Item(string file) => Load<ItemDefinition>("Assets/_Project/Data/Items/" + file + ".asset");
    private static QuestDefinition Quest(string file) => Load<QuestDefinition>(Prologue + "Quests/" + file + ".asset");
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
