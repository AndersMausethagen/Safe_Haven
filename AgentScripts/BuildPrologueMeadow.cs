using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

/// <summary>
/// The prologue starts in the sun (`unity command script run -- --file AgentScripts/BuildPrologueMeadow.cs --entry
/// BuildPrologueMeadow.Data`, then `.Land`, then `.Moments`). West of the walled forest a green meadow: the morning camp in
/// its north-west corner, the lake, the stream that runs into it, the river that runs on into the forest, and the old road
/// across both. Everything the player meets is ordinary designer data: quests and offers (Content Editor, Quest Map),
/// story graphs under "Morning Moments" (Story Graph), reactions on the Quest Events (Scene Reactions).
/// <para>
/// Data: the new quests, events and the worn sword, and their place in ForestPrologue. Land: the meadow's terrain, the pass
/// in the forest's west wall, the road, the rivers, the lake, the woods, and the NavMesh. Moments: the camp, the people, the
/// looks, the reactions and the stories. Each is safe to run again and replaces what it made before.
/// </para>
/// <para>
/// Since 2026-11-10 (Docs/QuestRedesign.md, step 2) the main line is seven quests where it was twelve: the weapon is chosen
/// as the party sets out, wolves come out of the trees at the cart, the shelter and the fire are one quest, the berries are
/// Eirik's errand, the camp is attacked twice, and the stones are reached and woken in one quest. Where a Tree Line, a
/// Gather Fuel, a Light Fire or an Activate Portal quest is mentioned in older notes, it is gone.
/// </para>
/// </summary>
public static class BuildPrologueMeadow
{
    private const string PrologueScene = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    private const string Prologue = "Assets/_Project/Data/Prologue/";
    private const string Items = "Assets/_Project/Data/Items/";
    private const string MainChain = "Prologue - The Unquiet Forest";
    private const string SideChain = "Prologue - Before the Road";
    private const string CampChain = "Prologue - By the Fire";
    private static readonly Vector2 RoadWolves = new Vector2(-203f, -4f);
    private const string MeadowData = Prologue + "Vegetation/Prologue Meadow Terrain.asset";
    private const string TrunksAsset = Prologue + "Vegetation/Prologue Forest Trunks.asset";
    private const string NavMeshAsset = "Assets/_Project/Scenes/Prologue_MistyWoods/NavMesh-NavMesh.asset";

    // The meadow, in world metres: west of the forest (which spans -100..100), as deep as it is.
    private const float MeadowWest = -400f;
    private const float Seam = -100f;
    private const float Depth = 200f;
    private const float MeadowHeight = 45f;

    private const float LakeSurface = 12.75f;
    private static readonly Vector2 LakeCentre = new Vector2(-351f, -18f);
    private static readonly Vector2 LakeRadius = new Vector2(34f, 26f);
    private static readonly Vector2 Camp = new Vector2(-362f, 48f);
    // Open grass between the road and the north slope's oaks, whose crowns would hide anything under them from above.
    private static readonly Vector2 BoarMeadow = new Vector2(-262f, 17f);
    private static readonly Vector2 Cart = new Vector2(-232f, 4f);

    private static readonly Vector2[] RoadWest = { new Vector2(-398f, 67f), new Vector2(-369f, 35f), new Vector2(-339f, 16f), new Vector2(-300f, 7f),
        new Vector2(-250f, -1f), new Vector2(-200f, -5f), new Vector2(-150f, -8f), new Vector2(-100f, -10.5f) };
    private static readonly Vector2[] RoadEast = { new Vector2(-100f, -10.5f), new Vector2(-78f, -12f), new Vector2(-50f, -14f), new Vector2(-20f, -16f),
        new Vector2(-6f, -18f), new Vector2(8f, -17f), new Vector2(30f, -6f), new Vector2(60f, -18f), new Vector2(86f, -40f) };
    private static readonly Vector2[] RoadToStones = { new Vector2(-6f, -18f), new Vector2(-4f, -8f), new Vector2(-1f, 1f), new Vector2(0f, 5f) };
    private static readonly Vector2[] NorthStream = { new Vector2(-341f, 99f), new Vector2(-338f, 76f), new Vector2(-335f, 58f), new Vector2(-338f, 34f),
        new Vector2(-340f, 16f), new Vector2(-345f, 2f) };
    // x, surface height, z: from the lake to the forest's waterfall, on through the basin and out through its south wall.
    private static readonly Vector3[] EastRiver = { new Vector3(-325f, 12.7f, -22f), new Vector3(-300f, 12.65f, -24f), new Vector3(-256f, 12.55f, -26f),
        new Vector3(-205f, 12.45f, -27f), new Vector3(-150f, 12.35f, -24f), new Vector3(-112f, 12.25f, -24f), new Vector3(-100f, 12.15f, -24.5f),
        new Vector3(-93f, 8f, -26f), new Vector3(-86f, 5.2f, -29f), new Vector3(-70f, 4.4f, -40f), new Vector3(-58f, 3.8f, -52f), new Vector3(-40f, 3.2f, -70f),
        new Vector3(-12f, 2.6f, -86f), new Vector3(12f, 2.2f, -99f) };

    // ==================================================================== Data

    public static string Data()
    {
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";

        GameEventDefinition met = Event("MetTheExpedition", "prologue.met_the_expedition", "Met the Expedition", "Hear the others out by the fire");
        GameEventDefinition entered = Event("ForestEntered", "prologue.forest_entered", "Entered the Forest", "Walk with the expedition to the forest");
        Event("TorchesLit", "prologue.torches_lit", "Torches Lit", "");
        Event("FireTalk", "prologue.fire_talk", "Talk by the Morning Fire", "");
        Event("RoadAmbush", "prologue.road_ambush", "Ambush on the Road", "");
        GameEventDefinition chosen = Load<GameEventDefinition>(Prologue + "Events/WeaponChosen.asset");
        Named(chosen, "Choose a weapon");
        Event("CartInspected", "prologue.cart_inspected", "Looked Over the Cart", "");
        Event("RoadGone", "prologue.road_gone", "The Road Behind Is Gone", "");
        Event("PressedSigrid", "prologue.pressed_sigrid", "Pressed Sigrid About the Stones", "");
        Event("TouchedLakeStone", "prologue.touched_lake_stone", "Touched the Stone by the Lake", "");
        Event("NameGoneMissing", "prologue.name_gone_missing", "A Name Went Missing by the Fire", "");
        Event("CameForCoin", "prologue.came_for_coin", "Came for the Coin", "");
        Event("CameForKin", "prologue.came_for_kin", "Came for Someone", "");
        Event("CameForTruth", "prologue.came_for_truth", "Came to Know", "");
        GameEventDefinition lessonHit = Event("LessonHit", "prologue.lesson_hit", "Struck the Dummy", "");
        GameEventDefinition lessonDodged = Event("LessonDodged", "prologue.lesson_dodged", "Dodged in the Lesson", "");
        GameEventDefinition lessonDefended = Event("LessonDefended", "prologue.lesson_defended", "Took a Blow Well in the Lesson", "");
        GameEventDefinition lessonDone = Event("LessonDone", "prologue.lesson_done", "Finished the Lesson", "");

        QuestDefinition morning = Quest("MorningCamp");
        QuestDefinition road = Quest("RoadToForest");
        QuestDefinition shelter = Existing("FindShelter");
        QuestDefinition rest = Existing("CatchBreath"), wolves = Existing("DefendCamp"), stones = Existing("ReachPortal"), hold = Existing("HoldPortal");
        var prologue = Load<PrologueDefinition>(Prologue + "ForestPrologue.asset");

        Edit(morning, so =>
        {
            Basics(so, "prologue.morning_camp", "A Morning by the Fire", MainChain,
                "Four of the expedition went into the forest two days ago and should have been back by now. It is early, the sun is out and the porridge is on. Hear the others out by the fire.",
                "Speak to Sigrid by the fire.");
            Starts(so, QuestStart.ByQuestGiver);
            Prerequisites(so);
            Announce(so, true, false);
            TurnIn(so, true, "Sigrid");
            Objectives(so, false, (ObjectiveType.QuestEvent, "Hear the others out by the fire", 1, met));
        });
        Edit(road, so =>
        {
            Basics(so, "prologue.road_to_forest", "The Road East", MainChain,
                "Sigrid wants to reach the forest by noon and find out what became of the four who went ahead. Walk the old road with the others; they keep a steady pace and wait for you if you fall behind.",
                "Speak to Sigrid when you are ready to set out.");
            Starts(so, QuestStart.ByQuestGiver);
            Prerequisites(so, morning);
            Announce(so, false, false);
            TurnIn(so, false, "");
            Objectives(so, false, (ObjectiveType.QuestEvent, "Walk with the expedition to the forest", 1, entered));
        });
        // One quest from the tree line to the first fire. It was four (the tree line, the shelter, the fuel, the fire), each
        // handed over in a talk with Sigrid: nine quests in six minutes of the playtest. It starts by itself as the forest
        // closes in, and its chapters end at its objectives (PrologueChapter.EndsAtObjective).
        const string shelterBriefing = "That was no weather. The sun went down hours early and this storm came out of a clear sky. Ingrid, Bjarne, Tor and Liv walked into this two days ago, so we go on, not back. Follow the old road into the trees and find us dry shelter. We are right behind you.";
        Edit(shelter, so =>
        {
            so.FindProperty("description").stringValue = "It should be noon, and it is the middle of the night. The expedition came for four of its own, and the forest has already put out the sun. Follow the old road in, find dry shelter and get a fire going beside it.";
            so.FindProperty("availableObjective").stringValue = "Stay close. The expedition is moving on.";
            Starts(so, QuestStart.Automatically);
            so.FindProperty("startDelay").floatValue = 5f;
            Prerequisites(so, road);
            Announce(so, false, false);
            TurnIn(so, false, "");
            var steps = new List<(ObjectiveType type, string text, int amount, Object target)> { (ObjectiveType.ReachPlace, "Find the rock shelter along the old road", 1, null) };
            // What the fire costs, so the quest and the build menu always agree (PrologueTests checks it).
            foreach (ItemAmount cost in prologue.Campfire.ConstructionItems) steps.Add((ObjectiveType.CollectItem, "", cost.Amount, cost.Item));
            steps.Add((ObjectiveType.BuildStructure, "Build a Campfire beside the shelter (G)", 1, null));
            Objectives(so, false, steps.ToArray());
            SerializedProperty list = so.FindProperty("objectives");
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty step = list.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("target").objectReferenceValue = i == 0 ? Load<QuestTargetDefinition>("Assets/_Project/Data/Quests/Targets/Rock Shelter.asset") : null;
                step.FindPropertyRelative("building").objectReferenceValue = i == list.arraySize - 1 ? prologue.Campfire : null;
                step.FindPropertyRelative("person").objectReferenceValue = null;
                // Wood cut in the morning, and anything picked up on the way in, counts towards the fire.
                step.FindPropertyRelative("countAlreadyOwned").boolValue = i > 0 && i < list.arraySize - 1;
            }
        });

        // The berries are Eirik's errand during the rest: the wolves wait for whoever has gone to pick them, for a while.
        Edit(Existing("BerriesForThePot"), so =>
        {
            so.FindProperty("chain").stringValue = CampChain;
            so.FindProperty("description").stringValue = "Eirik says nobody fights well on an empty stomach. There are berries in the clearing west of the shelter: pick five and stew them over the fire in his waterskin. Whatever is out there won't wait for ever.";
            so.FindProperty("availableObjective").stringValue = "";
            Starts(so, QuestStart.ByQuestGiver);
            Prerequisites(so, shelter);
            Announce(so, true, true);
            so.FindProperty("closedBy").objectReferenceValue = wolves;
            TurnIn(so, true, "Eirik");
            Rewards(so, (Item("Bandage"), 2));
        });

        // The rest starts by itself once the fire burns, and lasts until the wolves find it.
        Edit(rest, so =>
        {
            so.FindProperty("description").stringValue = "A fire at last. Sit a while and talk to the others. It won't last.";
            so.FindProperty("availableObjective").stringValue = "Warm yourself by the fire.";
            Starts(so, QuestStart.Automatically);
            so.FindProperty("startDelay").floatValue = 3f;
            Prerequisites(so, shelter);
            Announce(so, false, false);
            TurnIn(so, false, "");
            so.FindProperty("objectives").GetArrayElementAtIndex(0).FindPropertyRelative("description").stringValue = "Rest by the fire, and talk with the others";
        });

        // Reaching the stones and waking them is one quest: nobody stops to be briefed with the old troll behind them.
        Edit(stones, so =>
        {
            so.FindProperty("description").stringValue = "Something much larger is approaching. Leave the camp, take the old path north to the standing stones, and wake them.";
            TurnIn(so, false, "");
            Objectives(so, true, (ObjectiveType.ReachPlace, "", 1, null), (ObjectiveType.QuestEvent, "Touch the centre stone", 1, prologue.PortalActivatedEvent));
            SerializedProperty list = so.FindProperty("objectives");
            list.GetArrayElementAtIndex(0).FindPropertyRelative("target").objectReferenceValue = Load<QuestTargetDefinition>("Assets/_Project/Data/Quests/Targets/Waystones.asset");
            list.GetArrayElementAtIndex(1).FindPropertyRelative("target").objectReferenceValue = null;
        });
        Edit(hold, so => Prerequisites(so, stones));
        // The journal's notes for the quests that took others in (BuildJournal holds the same text).
        Edit(road, so => so.FindProperty("journalNotes").stringValue = "The old road runs straight at the trees. Somebody built it to go somewhere, and nobody at the camp knows where.\n\nChoose your weapon by how you want to fight rather than by its numbers. You will be making others soon enough.");
        Edit(shelter, so => so.FindProperty("journalNotes").stringValue = "It really is noon. Everyone agrees on that and no one can explain the stars. Runa says the forest has moved since the others came through, which is not something forests do.\n\nSticks and small rocks lie about on the forest floor, and you can pick them up as you walk: this is how every fire you ever build starts. Press (G) to open the build menu. It is the first fire anyone has lit in here for a very long time, though you won't know that yet.");
        Edit(stones, so => so.FindProperty("journalNotes").stringValue = "Whatever it is, the trees are moving out of its way. Don't stop to look.\n\nSigrid has seen writing like this once before, and she doesn't say where. The stones are older than the road, and much older than anyone who should know how to use them.");

        foreach (string retired in new[] { "TreeLine", "GatherFuel", "LightFire", "ActivatePortal" })
        {
            string path = Prologue + "Quests/" + retired + ".asset";
            if (Load<QuestDefinition>(path) != null) AssetDatabase.DeleteAsset(path);
        }

        // Optional errands by the fire. Closed By ends them when the expedition sets out.
        Edit(Quest("HuntForThePot"), so =>
        {
            Basics(so, "prologue.hunt_for_the_pot", "Meat for the Road", SideChain,
                "Runa has seen blight boar on the grass north of the road, east of the camp, where they have no business being. She lent you her spare bow. Bring two down.", "");
            Side(so, road);
            Objectives(so, false, (ObjectiveType.KillMonster, "Bring down two blight boar on the grass north of the road", 2,
                Load<MonsterDefinition>("Assets/_Project/Data/Monsters/BlightBoar.asset")));
            Rewards(so, (Item("CrudeStoneArrow"), 20));
        });
        Edit(Quest("FishForBreakfast"), so =>
        {
            Basics(so, "prologue.fish_for_breakfast", "Anything But Porridge", SideChain,
                "Eirik would give a lot for something other than porridge. He handed you a rod and a handful of grubs. Catch two fish in the lake south of the road.", "");
            Side(so, road);
            Objectives(so, false, (ObjectiveType.CatchFish, "Catch two fish in the lake", 2, null));
            Rewards(so, (Item("StewedBerries"), 2));
        });
        Edit(Quest("WoodForTheRoad"), so =>
        {
            Basics(so, "prologue.wood_for_the_road", "Wood for the Road", SideChain,
                "The expedition never has enough firewood. Eirik gave you a hatchet. Fell a tree on the slope north of the road and bring six logs.", "");
            Side(so, road);
            Objectives(so, false, (ObjectiveType.GatherResource, "Gather six logs on the slope north of the road", 6, Item("Materials/OakWood")));
            // Kindling for the fire to come: "A Spark in the Dark" counts what the player already carries.
            Rewards(so, (Load<PrologueDefinition>(Prologue + "ForestPrologue.asset").Sticks, 4));
        });
        Edit(Quest("EirikLesson"), so =>
        {
            // At the morning camp, before the road (2026-11-09: it was at the forest's edge, against a rootling). Eirik spars
            // the player himself; neither can die (SparNode: whoever is worn to a quarter yields).
            Basics(so, "prologue.eirik_lesson", "A Lesson from Eirik", SideChain,
                "Eirik would rather you learned with a practice sword at the camp than on whatever waits down the road. A dummy first, then Eirik himself.", "");
            Side(so, road);
            Objectives(so, true,
                (ObjectiveType.QuestEvent, "Strike the dummy three times (left mouse)", 3, lessonHit),
                (ObjectiveType.QuestEvent, "Dodge (Space)", 1, lessonDodged),
                (ObjectiveType.QuestEvent, "Parry Eirik: hold the right mouse just before his blow lands, or dodge through it", 1, lessonDefended),
                (ObjectiveType.QuestEvent, "Win the duel: wear Eirik down until he yields", 1, lessonDone));
            Rewards(so, (Item("Bandage"), 2));
        });
        // One errand at a time, in the order they are offered (wood follows the morning by itself); Eirik's lesson opens with
        // the first (2026-11-09: the chain was set in the Quest Map by hand, and a Data run lost it; it lives here now).
        Edit(Quest("FishForBreakfast"), so => Prerequisites(so, Existing("WoodForTheRoad")));
        Edit(Quest("HuntForThePot"), so => Prerequisites(so, Existing("FishForBreakfast")));
        Edit(Quest("EirikLesson"), so => Prerequisites(so, morning));
        AxeNames();
        var chapterData = new SerializedObject(Load<PrologueDefinition>(Prologue + "ForestPrologue.asset"));
        chapterData.FindProperty("startingBackpack").objectReferenceValue = Item("HideRucksack");
        chapterData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(chapterData.targetObject);
        string ranks = Ranks();
        string sword = WornSword();
        PracticeSword();
        string boar = UnsavedCopy("Assets/_Project/Prefabs/Enemies/BlightBoar.prefab", Prologue + "ExpeditionBoar.prefab");
        UnsavedCopy("Assets/_Project/Prefabs/Enemies/Rootling.prefab", Prologue + "ExpeditionRootling.prefab");
        string moon = ForestMoon();
        string chapters = Chapters(morning, road, shelter, rest, wolves, stones, hold, shelterBriefing);
        AssetDatabase.SaveAssets();
        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database.CollectAll();
            EditorUtility.SetDirty(database);
        }

        AssetDatabase.SaveAssets();
        return $"{chapters}; {ranks}; {sword}; {boar}; {moon}; problem: {Load<PrologueDefinition>(Prologue + "ForestPrologue.asset").FindProblem() ?? "none"}";
    }

    // The whole sequence, written out: nine chapters for seven quests.
    private static string Chapters(QuestDefinition morning, QuestDefinition road, QuestDefinition shelter, QuestDefinition rest, QuestDefinition wolves,
        QuestDefinition stones, QuestDefinition hold, string shelterBriefing)
    {
        var prologue = Load<PrologueDefinition>(Prologue + "ForestPrologue.asset");
        var so = new SerializedObject(prologue);
        so.FindProperty("startingTorch").objectReferenceValue = null;
        so.FindProperty("survivedText").stringValue = "Sigrid: It is open! Everyone, through! The old troll's fist comes down on the stones. You reach for the others... and the forest disappears.";
        SerializedProperty chapters = so.FindProperty("chapters");

        // What the fights and the flight say and hold stays as it is tuned (BuildPrologueFinale, the Content Editor).
        var kept = new Dictionary<PrologueStep, (string arrival, string status, float duration, int initial, List<(Object prefab, float weight, int fromWave)> enemies)>();
        for (int i = 0; i < chapters.arraySize; i++)
        {
            SerializedProperty row = chapters.GetArrayElementAtIndex(i);
            var behaviour = (PrologueStep)row.FindPropertyRelative("behaviour").intValue;
            var enemies = new List<(Object prefab, float weight, int fromWave)>();
            SerializedProperty list = row.FindPropertyRelative("enemies");
            for (int e = 0; e < list.arraySize; e++)
            {
                SerializedProperty enemy = list.GetArrayElementAtIndex(e);
                enemies.Add((enemy.FindPropertyRelative("prefab").objectReferenceValue, enemy.FindPropertyRelative("weight").floatValue, enemy.FindPropertyRelative("fromWave").intValue));
            }

            kept[behaviour] = (row.FindPropertyRelative("arrivalText").stringValue, row.FindPropertyRelative("statusText").stringValue,
                row.FindPropertyRelative("duration").floatValue, row.FindPropertyRelative("initialEnemies").intValue, enemies);
        }

        foreach (PrologueStep needed in new[] { PrologueStep.DefendCamp, PrologueStep.ReachPortal, PrologueStep.HoldPortal })
        {
            if (!kept.ContainsKey(needed)) throw new System.InvalidOperationException("ForestPrologue has no " + needed + " chapter to keep.");
        }

        chapters.arraySize = 0;
        SerializedProperty Row(QuestDefinition quest, PrologueStep behaviour, string arrival, int endsAt = 0)
        {
            int at = chapters.arraySize++;
            SerializedProperty chapter = chapters.GetArrayElementAtIndex(at);
            chapter.FindPropertyRelative("quest").objectReferenceValue = quest;
            chapter.FindPropertyRelative("behaviour").intValue = (int)behaviour;
            chapter.FindPropertyRelative("arrivalText").stringValue = arrival;
            chapter.FindPropertyRelative("statusText").stringValue = kept.TryGetValue(behaviour, out var old) && behaviour != PrologueStep.Story ? old.status : "";
            chapter.FindPropertyRelative("duration").floatValue = 0f;
            chapter.FindPropertyRelative("initialEnemies").intValue = 0;
            chapter.FindPropertyRelative("enemies").ClearArray();
            chapter.FindPropertyRelative("site").intValue = (int)PrologueSite.Meadow;
            chapter.FindPropertyRelative("companions").intValue = (int)PrologueCompanions.WaitAtSite;
            chapter.FindPropertyRelative("timeToTalk").boolValue = false;
            chapter.FindPropertyRelative("endsAtObjective").intValue = endsAt;
            chapter.FindPropertyRelative("waves").intValue = 1;
            chapter.FindPropertyRelative("nextWaveText").stringValue = "";
            return chapter;
        }

        void Story(SerializedProperty chapter, PrologueSite site, PrologueCompanions companions, bool talk)
        {
            chapter.FindPropertyRelative("site").intValue = (int)site;
            chapter.FindPropertyRelative("companions").intValue = (int)companions;
            chapter.FindPropertyRelative("timeToTalk").boolValue = talk;
        }

        void Fight(SerializedProperty chapter, PrologueStep behaviour)
        {
            var old = kept[behaviour];
            chapter.FindPropertyRelative("duration").floatValue = old.duration;
            chapter.FindPropertyRelative("initialEnemies").intValue = old.initial;
            SerializedProperty list = chapter.FindPropertyRelative("enemies");
            foreach ((Object prefab, float weight, int fromWave) in old.enemies)
            {
                SerializedProperty enemy = list.GetArrayElementAtIndex(list.arraySize++);
                enemy.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                enemy.FindPropertyRelative("weight").floatValue = weight;
                enemy.FindPropertyRelative("fromWave").intValue = fromWave;
            }
        }

        Story(Row(morning, PrologueStep.Story, MorningBriefing), PrologueSite.Meadow, PrologueCompanions.StandStill, true);
        Story(Row(road, PrologueStep.Story, RoadBriefing), PrologueSite.Meadow, PrologueCompanions.StoryLeads, false);
        // Shelter from the Storm, in two parts: to the shelter, then the fire beside it.
        Row(shelter, PrologueStep.FindShelter, shelterBriefing, 1);
        Row(shelter, PrologueStep.LightFire, FireBriefing).FindPropertyRelative("statusText").stringValue = "Build a Campfire beside the shelter (G)";
        // The rest opens with what Sigrid can't remember (Story - A Name Gone Missing), so the chapter has no line of its own.
        Story(Row(rest, PrologueStep.Story, ""), PrologueSite.Camp, PrologueCompanions.WaitAtSite, true);
        // The camp was two wolves and over in fifteen seconds (playtest logs): three now, and three more when they have fallen.
        SerializedProperty camp = Row(wolves, PrologueStep.DefendCamp, kept[PrologueStep.DefendCamp].arrival);
        Fight(camp, PrologueStep.DefendCamp);
        camp.FindPropertyRelative("initialEnemies").intValue = 3;
        camp.FindPropertyRelative("waves").intValue = 2;
        camp.FindPropertyRelative("nextWaveText").stringValue = "Runa: More of them, coming round the other side!";
        // Something Is Coming, in two parts: the flight to the stones, then the hand on the stone. Both are told by stories.
        Row(stones, PrologueStep.ReachPortal, "", 1);
        Row(stones, PrologueStep.ActivatePortal, "");
        Fight(Row(hold, PrologueStep.HoldPortal, kept[PrologueStep.HoldPortal].arrival), PrologueStep.HoldPortal);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prologue);
        return $"{chapters.arraySize} chapters";
    }

    private const string FireBriefing = "Dry stone overhead. Good work. Now a fire, before anyone can think clearly: eight sticks and twelve small rocks off the forest floor, then build it beside the shelter (G).";
    private const string MorningBriefing = "There you are. Come and stand by the fire with the rest of us. You should know who you are walking with before we set out.";
    private const string RoadBriefing = "The packs are tied and the fire is nearly out. Ingrid's party should have been back yesterday, and the forest is half a morning down the road. If you have anything left to do here, do it now: once we set out, we do not come back for it. Are you ready?";

    // Gold for the prologue's story, bronze for the errands before the road. Only the prologue's own chains: every other
    // quest's kind (rescues, the haven, tales, errands) is set by BuildQuestCategories, and this used to turn them all gold.
    private static string Ranks()
    {
        int story = 0, optional = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            var quest = Load<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (quest.Chain != SideChain && quest.Chain != CampChain && quest.Chain != MainChain) continue;
            QuestRank rank = quest.Chain == MainChain ? QuestRank.MainStory : QuestRank.Optional;
            story += rank == QuestRank.MainStory ? 1 : 0;
            optional += rank == QuestRank.Optional ? 1 : 0;
            Edit(quest, so => so.FindProperty("rank").intValue = (int)rank);
        }

        return $"{story} story quests, {optional} optional";
    }

    // The sword has no Tier I of its own in the game, so the expedition carries an old one: a copper sword's moves, the club's damage.
    // Eirik's practice sword for the lesson at the camp: the copper sword's look, blunted.
    private static void PracticeSword()
    {
        const string weaponPath = "Assets/_Project/Data/Combat/Weapons/PracticeSword.asset";
        string itemPath = Items + "Weapons/PracticeSword.asset";
        if (Load<WeaponDefinition>(weaponPath) == null) AssetDatabase.CopyAsset("Assets/_Project/Data/Combat/Weapons/CopperSword.asset", weaponPath);
        var weapon = Load<WeaponDefinition>(weaponPath);
        var weaponData = new SerializedObject(weapon);
        weaponData.FindProperty("displayName").stringValue = "Practice Sword";
        weaponData.FindProperty("damage").floatValue = 6f;
        weaponData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weapon);
        if (Load<ItemDefinition>(itemPath) == null) AssetDatabase.CopyAsset(Items + "Weapons/CopperSword.asset", itemPath);
        var item = Load<ItemDefinition>(itemPath);
        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = "practice_sword";
        so.FindProperty("displayName").stringValue = "Practice Sword";
        so.FindProperty("description").stringValue = "A blunted blade Eirik keeps for teaching. It bruises; it does not cut.";
        so.FindProperty("tier").intValue = 1;
        so.FindProperty("weapon").objectReferenceValue = weapon;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static string WornSword()
    {
        const string weaponPath = "Assets/_Project/Data/Combat/Weapons/WornSword.asset";
        string itemPath = Items + "Weapons/WornSword.asset";
        if (Load<WeaponDefinition>(weaponPath) == null) AssetDatabase.CopyAsset("Assets/_Project/Data/Combat/Weapons/CopperSword.asset", weaponPath);
        var weapon = Load<WeaponDefinition>(weaponPath);
        var weaponData = new SerializedObject(weapon);
        weaponData.FindProperty("displayName").stringValue = "Worn Sword";
        weaponData.FindProperty("damage").floatValue = 12f;
        weaponData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weapon);
        if (Load<ItemDefinition>(itemPath) == null) AssetDatabase.CopyAsset(Items + "Weapons/CopperSword.asset", itemPath);
        var item = Load<ItemDefinition>(itemPath);
        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = "worn_sword";
        so.FindProperty("displayName").stringValue = "Worn Sword";
        so.FindProperty("description").stringValue = "An old soldier's sword, nicked and re-ground more times than anyone remembers. It still holds an edge.";
        so.FindProperty("tier").intValue = 1;
        so.FindProperty("weapon").objectReferenceValue = weapon;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return "worn sword";
    }

    // "Woodsman's Axe" promised a tool, and it is a fighting axe: the hatchets fell trees. Its asset and id stay, for saves and recipes.
    private static void AxeNames()
    {
        var item = Load<ItemDefinition>("Assets/_Project/Data/Items/WoodsmansAxe.asset");
        // The object kept its first name (StoneAxe), and every import warned that it doesn't match the file's name.
        if (item.name != "WoodsmansAxe") item.name = "WoodsmansAxe";
        var so = new SerializedObject(item);
        so.FindProperty("displayName").stringValue = "Hand Axe";
        so.FindProperty("description").stringValue = "A compact one-handed fighting axe carried by forest expeditions. No use on a tree. Leaves a hand free for a torch.";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        var weapon = new SerializedObject(Load<WeaponDefinition>("Assets/_Project/Data/Combat/Weapons/WoodsmansAxe.asset"));
        weapon.FindProperty("displayName").stringValue = "Hand Axe";
        weapon.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weapon.targetObject);
        var copper = Load<ItemDefinition>("Assets/_Project/Data/Items/Weapons/CopperHandAxe.asset");
        var copperData = new SerializedObject(copper);
        copperData.FindProperty("description").stringValue = "A hand axe that grew up. It bites deeper than the sword and reaches less.";
        copperData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(copper);
    }

    // More moon than the raid's night, on a copy so the raid keeps its own: the forest should be dark, not unreadable.
    private static string ForestMoon()
    {
        string path = Prologue + "Prologue Forest Night.asset";
        if (Load<DayNightProfile>(path) == null) AssetDatabase.CopyAsset("Assets/_Project/Data/Environment/Misty Woods Day Night.asset", path);
        var profile = Load<DayNightProfile>(path);
        var so = new SerializedObject(profile);
        so.FindProperty("moonIntensity").floatValue = MoonIntensity;
        so.FindProperty("moonColour").colorValue = new Color(0.8f, 0.86f, 1f);
        so.FindProperty("shadowFill").floatValue = 2.2f;
        so.FindProperty("shadowDarkness").floatValue = 0.45f;
        foreach (string gradient in new[] { "ambientSky", "ambientEquator", "ambientGround" })
        {
            SerializedProperty property = so.FindProperty(gradient);
            Gradient value = property.gradientValue;
            Gradient original = Load<DayNightProfile>("Assets/_Project/Data/Environment/Misty Woods Day Night.asset") is DayNightProfile raid ? new SerializedObject(raid).FindProperty(gradient).gradientValue : value;
            var keys = new GradientColorKey[original.colorKeys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                GradientColorKey key = original.colorKeys[i];
                // Only the small hours of the gradient (it runs over the day), so the morning stays the morning.
                float hour = key.time * 24f;
                bool night = hour < 5f || hour > 20f;
                keys[i] = new GradientColorKey(night ? key.color * AmbientLift : key.color, key.time);
            }

            value.SetKeys(keys, original.alphaKeys);
            property.gradientValue = value;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        ForestVolume();
        return $"forest moon {MoonIntensity}";
    }

    // The raid's post-processing, on a copy with a thinner night veil: the veil is what closed in as a dark ring around the player.
    private static void ForestVolume()
    {
        string path = Prologue + "Prologue Forest Look.asset";
        if (Load<VolumeProfile>(path) == null) AssetDatabase.CopyAsset("Assets/_Project/Settings/Rendering/Volumes/MistyWoods_Raid_Volume.asset", path);
        var volume = Load<VolumeProfile>(path);
        if (volume.TryGet(out FogOfWar veil))
        {
            veil.nightVeil.value = 0.4f;
            veil.veilDarkening.value = 0.25f;
            EditorUtility.SetDirty(veil);
        }

        if (volume.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments colour))
        {
            colour.postExposure.value = 0.4f;
            EditorUtility.SetDirty(colour);
        }

        EditorUtility.SetDirty(volume);
    }

    private const float MoonIntensity = 10f;
    private const float AmbientLift = 2.7f;

    private static string UnsavedCopy(string source, string path)
    {
        var existing = Load<GameObject>(path);
        if (existing != null && existing.GetComponentInChildren<SaveableEntity>(true) == null) return "boar copy kept";
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(source));
        try
        {
            foreach (SaveableEntity saveable in instance.GetComponentsInChildren<SaveableEntity>(true)) Object.DestroyImmediate(saveable);
            instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            return "boar copy made";
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void Basics(SerializedObject so, string id, string name, string chain, string description, string available)
    {
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("chain").stringValue = chain;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("availableObjective").stringValue = available;
    }

    private static void Starts(SerializedObject so, QuestStart start)
    {
        so.FindProperty("start").intValue = (int)start;
        so.FindProperty("startEvent").objectReferenceValue = null;
        so.FindProperty("startDelay").floatValue = 0f;
    }

    private static void Prerequisites(SerializedObject so, params QuestDefinition[] quests)
    {
        SerializedProperty list = so.FindProperty("prerequisites");
        list.arraySize = quests.Length;
        for (int i = 0; i < quests.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
    }

    private static void Announce(SerializedObject so, bool start, bool end)
    {
        so.FindProperty("announceStart").boolValue = start;
        so.FindProperty("announceCompletion").boolValue = end;
    }

    private static void TurnIn(SerializedObject so, bool turnIn, string to)
    {
        so.FindProperty("turnInToGiver").boolValue = turnIn;
        so.FindProperty("turnInTo").stringValue = to;
    }

    private static void Side(SerializedObject so, QuestDefinition closedBy)
    {
        Starts(so, QuestStart.ByQuestGiver);
        Prerequisites(so);
        Announce(so, true, true);
        so.FindProperty("closedBy").objectReferenceValue = closedBy;
        string giver = so.FindProperty("id").stringValue.Contains("hunt") ? "Runa" : "Eirik";
        TurnIn(so, true, giver);
    }

    private static readonly (string quest, string ask, string text, string accept, string waiting, string thanks) LessonOffer =
        ("EirikLesson", "Show me how to use this.",
            "Before we set out? Gladly. Take this practice sword; it bruises, it doesn't cut. Hit the dummy by the fire until your arm knows the way, then come and try it on me.",
            "Show me.",
            "Dummy first, then me. Watch my shoulders: the blow is coming when they drop.",
            "You'll do. Better than most I have trained, worse than you will be. Take these; you will want them.");

    private static void Rewards(SerializedObject so, params (ItemDefinition item, int amount)[] rewards)
    {
        SerializedProperty list = so.FindProperty("rewardItems");
        list.arraySize = rewards.Length;
        for (int i = 0; i < rewards.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("item").objectReferenceValue = rewards[i].item;
            entry.FindPropertyRelative("amount").intValue = rewards[i].amount;
        }
    }

    private static void Objectives(SerializedObject so, bool inOrder, params (ObjectiveType type, string text, int amount, Object target)[] objectives)
    {
        so.FindProperty("objectivesInOrder").boolValue = inOrder;
        SerializedProperty list = so.FindProperty("objectives");
        list.arraySize = objectives.Length;
        for (int i = 0; i < objectives.Length; i++)
        {
            SerializedProperty o = list.GetArrayElementAtIndex(i);
            o.FindPropertyRelative("type").intValue = (int)objectives[i].type;
            o.FindPropertyRelative("description").stringValue = objectives[i].text;
            o.FindPropertyRelative("amount").intValue = objectives[i].amount;
            o.FindPropertyRelative("item").objectReferenceValue = objectives[i].target as ItemDefinition;
            o.FindPropertyRelative("resource").objectReferenceValue = null;
            o.FindPropertyRelative("monster").objectReferenceValue = objectives[i].target as MonsterDefinition;
            o.FindPropertyRelative("building").objectReferenceValue = null;
            o.FindPropertyRelative("raid").objectReferenceValue = null;
            o.FindPropertyRelative("questEvent").objectReferenceValue = objectives[i].target as GameEventDefinition;
            o.FindPropertyRelative("mustExtract").boolValue = false;
            o.FindPropertyRelative("countAlreadyOwned").boolValue = false;
        }
    }

    private static void Edit(QuestDefinition quest, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(quest);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
    }

    private static QuestDefinition Quest(string file)
    {
        string path = Prologue + "Quests/" + file + ".asset";
        var quest = Load<QuestDefinition>(path);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestDefinition>();
            AssetDatabase.CreateAsset(quest, path);
        }

        return quest;
    }

    private static QuestDefinition Existing(string file) => Load<QuestDefinition>(Prologue + "Quests/" + file + ".asset");

    private static GameEventDefinition Event(string file, string id, string name, string objective)
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
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("objectiveText").stringValue = objective;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameEvent);
        return gameEvent;
    }

    private static void Named(GameEventDefinition gameEvent, string objective)
    {
        var so = new SerializedObject(gameEvent);
        so.FindProperty("objectiveText").stringValue = objective;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameEvent);
    }

    // ==================================================================== Land

    public static string Land()
    {
        Scene scene = OpenPrologue(out string refused);
        if (refused != null) return refused;
        WorldAssets.EnsureAll();

        Terrain forest = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include).First(t => AssetDatabase.GetAssetPath(t.terrainData).EndsWith("Prologue Forest Terrain.asset"));
        Transform land = forest.transform.parent;
        OpenPass(forest);
        Terrain meadow = MakeMeadow(forest, land);
        Connect(meadow, forest);

        // The World Builder: road, rivers, woods. It keeps what stands on the ground on the ground.
        Transform old = land.Find("World");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var world = new GameObject("World").transform;
        world.SetParent(land, false);
        WaterSurface lake = PlaceLake(meadow);
        Features(world, forest);
        WorldBuilder.BuildChanged(true);
        string report = WorldBuilder.LastReport;

        StitchSeam(forest, meadow);
        DigLake(meadow, lake);
        StitchSeam(forest, meadow);
        Grass(meadow);
        GrassSetup.ClearUnderWater(forest);
        GrassSetup.BakeGroundColour(forest);
        TerrainDisplacementEditor.Apply(meadow);
        TerrainDisplacementEditor.Apply(forest);
        CopyMist(forest, meadow);
        Walls(meadow);
        string trunks = Trunks(forest, land);

        foreach (FogGround fog in Object.FindObjectsByType<FogGround>(FindObjectsInactive.Include)) fog.Bake();
        var surface = land.GetComponentInParent<Unity.AI.Navigation.NavMeshSurface>();
        surface.BuildNavMesh();
        NavMeshData navData = surface.navMeshData;
        AssetDatabase.DeleteAsset(NavMeshAsset);
        AssetDatabase.CreateAsset(navData, NavMeshAsset);
        surface.navMeshData = Load<NavMeshData>(NavMeshAsset);
        EditorUtility.SetDirty(surface);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return $"meadow {meadow.terrainData.size}; lake {lake.name}; {trunks}; features: {world.childCount}; build: {report}; layers forest: {forest.terrainData.terrainLayers.Length} meadow: {meadow.terrainData.terrainLayers.Length}; seam error {SeamError(forest, meadow):F3} m";
    }

    // A low pass through the forest's high west wall where the road and the river come in. The wall's foot and the land
    // outside it are met by the meadow's ground, which takes this edge's heights.
    private static void OpenPass(Terrain forest)
    {
        TerrainData data = forest.terrainData;
        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        float height = data.size.y;
        // World heights: the World Builder lowers this terrain to make room, and a run after that would otherwise dig deeper.
        float lowered = forest.transform.position.y;
        for (int row = 0; row < resolution; row++)
        {
            float z = row / (resolution - 1f) * Depth - Depth * 0.5f;
            float across = Smooth((z + 52f) / 14f) * (1f - Smooth((z - 6f) / 16f));
            if (across <= 0f) continue;
            float notch = PassFloor(z);
            for (int column = 0; column < resolution; column++)
            {
                float x = column / (resolution - 1f) * Depth - Depth * 0.5f;
                if (x > -56f) break;
                float into = 1f - Smooth((x + 66f) / 10f);
                float wanted = Mathf.Lerp(notch, 4.6f, Smooth((x - Seam) / 34f)) - lowered;
                float old = heights[row, column] * height;
                float opened = Mathf.Lerp(old, Mathf.Min(old, wanted), across * into);
                // On the road's own line it is built up too: the old wall fell eleven metres in twelve, with gullies either side that
                // carried whoever walked it off the road and down to the river.
                float road = 1f - Smooth((Mathf.Abs(z - RoadZ(x)) - 6f) / 8f);
                heights[row, column] = Mathf.Lerp(opened, wanted, road * into) / height;
            }
        }

        data.SetHeights(0, 0, heights);
        data.SetTreeInstances(data.treeInstances, true);
        EditorUtility.SetDirty(data);
    }

    private static float RoadZ(float x) => Mathf.Lerp(-10.5f, -14f, Mathf.InverseLerp(Seam, -50f, x));

    // The pass at the seam: the river's bed at z = -24, the road a little higher at z = -10.
    private static float PassFloor(float z) => z <= -24f
        ? 12.6f + 6f * Smooth((-24f - z) / 14f)
        : 12.6f + 3f * Smooth((z + 24f) / 14f);

    private static Terrain MakeMeadow(Terrain forest, Transform parent)
    {
        Transform oldTerrain = parent.Find("Meadow Terrain");
        if (oldTerrain != null) Object.DestroyImmediate(oldTerrain.gameObject);
        AssetDatabase.DeleteAsset(MeadowData);
        AssetDatabase.DeleteAsset(Prologue + "Vegetation/Prologue Meadow Terrain World.asset");

        const int resolution = 513;
        float width = Seam - MeadowWest;
        var data = new TerrainData { heightmapResolution = resolution, alphamapResolution = 512 };
        data.size = new Vector3(width, MeadowHeight, Depth);
        data.SetDetailResolution(512, 16);
        AssetDatabase.CreateAsset(data, MeadowData);

        TerrainData forestData = forest.terrainData;
        float[,] rim = forestData.GetHeights(0, 0, 1, resolution);
        var heights = new float[resolution, resolution];
        for (int row = 0; row < resolution; row++)
        {
            float z = row / (resolution - 1f) * Depth - Depth * 0.5f;
            float edge = rim[row, 0] * forestData.size.y + forest.transform.position.y;
            for (int column = 0; column < resolution; column++)
            {
                float x = MeadowWest + column / (resolution - 1f) * width;
                float h = MeadowGround(x, z);
                // Rising to meet the forest's wall over the last stretch.
                h = Mathf.Lerp(h, edge, Smooth((x - (Seam - 55f)) / 55f));
                heights[row, column] = h / MeadowHeight;
            }
        }

        data.SetHeights(0, 0, heights);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Meadow Terrain";
        terrainObject.transform.SetParent(parent, true);
        terrainObject.transform.position = new Vector3(MeadowWest, 0f, -Depth * 0.5f);
        terrainObject.isStatic = forest.gameObject.isStatic;
        var terrain = terrainObject.GetComponent<Terrain>();
        terrain.heightmapPixelError = forest.heightmapPixelError;
        terrain.treeDistance = forest.treeDistance;
        terrain.treeBillboardDistance = forest.treeBillboardDistance;
        terrain.detailObjectDistance = forest.detailObjectDistance;
        terrain.detailObjectDensity = forest.detailObjectDensity;
        terrain.drawInstanced = false;
        terrain.groupingID = forest.groupingID;
        terrain.allowAutoConnect = true;
        forest.allowAutoConnect = true;
        var world = terrainObject.AddComponent<WorldTerrain>();
        WorldTerrainEditor.UseLook(world, terrain, true);
        data.terrainLayers = new[] { WorldAssets.Layer("Meadow Grass") };
        WorldPaint.Apply(terrain, world.PaintRules);
        return terrain;
    }

    // The meadow's own ground: low rolling green, a dip for the lake and a shallow valley for the river.
    private static float MeadowGround(float x, float z)
    {
        float hills = 17f + (Mathf.PerlinNoise(x / 70f + 31.7f, z / 70f + 4.3f) - 0.5f) * 6f
            + (Mathf.PerlinNoise(x / 23f + 7.1f, z / 23f + 19.3f) - 0.5f) * 1.2f
            + (Mathf.PerlinNoise(x / 6f + 21f, z / 6f + 9f) - 0.5f) * 0.2f;
        hills += Smooth((z - 40f) / 60f) * 6f + Smooth((-60f - z) / 40f) * 4f + Smooth((-370f - x) / 30f) * 3f;

        // Level ground for the camp.
        float toCamp = Vector2.Distance(new Vector2(x, z), Camp);
        hills = Mathf.Lerp(18f, hills, Smooth((toCamp - 14f) / 18f));

        // The river's valley, falling very slightly towards the forest.
        float toRiver = DistanceToRiver(x, z);
        float valley = 1f - Smooth((toRiver - 5f) / 24f);
        hills = Mathf.Lerp(hills, 13.4f, valley);

        // The lake's basin: just above the water around it, dug out by Carve afterwards.
        float lake = new Vector2((x - LakeCentre.x) / LakeRadius.x, (z - LakeCentre.y) / LakeRadius.y).magnitude;
        return Mathf.Lerp(LakeSurface + 0.45f, hills, Smooth((lake - 1.05f) / 1.2f));
    }

    private static float DistanceToRiver(float x, float z)
    {
        var p = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 1; i < EastRiver.Length && EastRiver[i - 1].x < Seam; i++)
        {
            var a = new Vector2(EastRiver[i - 1].x, EastRiver[i - 1].z);
            var b = new Vector2(EastRiver[i].x, EastRiver[i].z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }

        return best;
    }

    private static void Connect(Terrain meadow, Terrain forest)
    {
        meadow.SetNeighbors(null, null, forest, null);
        forest.SetNeighbors(meadow, null, null, null);
    }

    private static void Features(Transform world, Terrain forest)
    {
        TerrainLayer forestSoil = forest.terrainData.terrainLayers[1];

        // Ground first: the forest's floor reaches out past its edge, so the meadow gives way to moss before the trees close in.
        var forestFloor = Feature<GroundPaintArea>(world, "Forest Floor at the Edge", Flat(new[] { new Vector2(-136f, 99f), new Vector2(-100.5f, 99f), new Vector2(-100.5f, -99f), new Vector2(-136f, -99f) }));
        forestFloor.Spline.Closed = true;
        forestFloor.Configure(WorldAssets.PaintRules("Dark Forest"), 22f);

        var roadWest = Feature<PathFeature>(world, "The Old Road (Meadow)", Flat(RoadWest));
        roadWest.Configure(WorldAssets.Layer("Trodden Path"), null);
        Set(roadWest, "width", 3.4f);
        Set(roadWest, "softEdge", 1.6f);
        Set(roadWest, "flatten", 0.85f);
        var roadEast = Feature<PathFeature>(world, "The Old Road (Forest)", Flat(RoadEast));
        roadEast.Configure(forestSoil, null);
        Set(roadEast, "width", 3f);
        Set(roadEast, "softEdge", 1.6f);
        Set(roadEast, "flatten", 0.85f);
        var toStones = Feature<PathFeature>(world, "Old Path to the Waystones", Flat(RoadToStones));
        toStones.Configure(forestSoil, null);

        var stream = Feature<RiverFeature>(world, "Stream from the North", Flat(NorthStream));
        stream.Configure(WorldAssets.RiverWater(), WorldAssets.Layer("Gravel"), WorldAssets.Layer("Muddy Bank"), WorldAssets.Palette("River Stones"), WorldAssets.Palette("Riverbank"));
        Set(stream, "width", 3f);
        Set(stream, "depth", 0.8f);
        var river = Feature<RiverFeature>(world, "River to the Forest", EastRiver);
        river.Configure(WorldAssets.RiverWater(), WorldAssets.Layer("Gravel"), WorldAssets.Layer("Muddy Bank"), WorldAssets.Palette("River Stones"), WorldAssets.Palette("Riverbank"));
        var riverData = new SerializedObject(river);
        riverData.FindProperty("surface").intValue = (int)RiverFeature.SurfaceMode.SplineHeight;
        riverData.FindProperty("width").floatValue = 5f;
        riverData.FindProperty("depth").floatValue = 1f;
        riverData.FindProperty("bankWidth").floatValue = 5f;
        riverData.ApplyModifiedPropertiesWithoutUndo();

        // Oaks on the slopes north and south, thickening into dark spruce along the forest's edge. The road and the river keep a gap.
        (string name, Vector2[] points)[] groves =
        {
            ("Grove on the North Slope", new[] { new Vector2(-325f, 99f), new Vector2(-140f, 99f), new Vector2(-140f, 40f), new Vector2(-215f, 30f), new Vector2(-325f, 44f) }),
            ("Grove South of the River", new[] { new Vector2(-398f, -99f), new Vector2(-140f, -99f), new Vector2(-140f, -46f), new Vector2(-300f, -52f), new Vector2(-398f, -60f) }),
            ("Grove in the North-West", new[] { new Vector2(-398f, 99f), new Vector2(-348f, 99f), new Vector2(-348f, 74f), new Vector2(-398f, 82f) }),
        };
        foreach ((string name, Vector2[] points) in groves)
        {
            var grove = Feature<BiomeArea>(world, name, Flat(points));
            grove.Spline.Closed = true;
            grove.Configure(WorldMenus.HavenGroveLayers());
        }

        (string name, Vector2[] points)[] edges =
        {
            ("Forest Edge North", new[] { new Vector2(-140f, 99f), new Vector2(-101f, 99f), new Vector2(-101f, 9f), new Vector2(-128f, 8f) }),
            ("Forest Edge South", new[] { new Vector2(-140f, -38f), new Vector2(-101f, -38f), new Vector2(-101f, -99f), new Vector2(-140f, -99f) }),
        };
        foreach ((string name, Vector2[] points) in edges)
        {
            var edge = Feature<BiomeArea>(world, name, Flat(points));
            edge.Spline.Closed = true;
            edge.Configure(WorldMenus.DarkForestLayers());
        }
    }

    private static WaterSurface PlaceLake(Terrain meadow)
    {
        Transform old = meadow.transform.parent.Find("Morning Lake");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        WaterSetup.Set set = WaterSetup.EnsureAssets();
        var at = new Vector3(LakeCentre.x, LakeSurface, LakeCentre.y);
        GameObject water = WaterSetup.CreateWater("Morning Lake", set.Still, at, new Vector3(LakeRadius.x / 5f, 1f, LakeRadius.y / 5f));
        water.transform.SetParent(meadow.transform.parent, true);
        WaterSurface surface = water.GetComponent<WaterSurface>();
        surface.IsStill = true;
        surface.Oval = true;
        return surface;
    }

    private static void DigLake(Terrain meadow, WaterSurface lake)
    {
        WaterSetup.Carve(meadow, lake.transform, 2.6f, 4f, null, true);
        GrassSetup.ClearUnderWater(meadow);
        LakeSurfaceLook(lake.gameObject);
    }

    /// <summary>The Haven's footprints and ground shape on both terrains (`--entry BuildPrologueMeadow.Footprints`). Land keeps them.</summary>
    public static string Footprints()
    {
        Scene scene = OpenPrologue(out string refused);
        if (refused != null) return refused;
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        foreach (Terrain terrain in terrains) TerrainDisplacementEditor.UseStandardFootprints(terrain);
        // Grounds the Haven doesn't have, by what they are most like there: litter, moss and humus.
        foreach (Terrain terrain in terrains)
        {
            TerrainSurfaceProfile profile = terrain.GetComponent<TerrainSurface>().Profile;
            foreach (TerrainSurfaceProfile.Layer layer in profile.layers)
            {
                switch (layer.terrainLayer.name)
                {
                    case "Pine Needle Floor": layer.footprintResponse = 0.8f; break;
                    case "Mossy Forest Floor": layer.footprintResponse = 1f; break;
                    case "Forest Soil": layer.footprintResponse = 0.9f; break;
                    case "Weathered Stone": layer.footprintResponse = 0f; break;
                    // As gentle as the Haven's grass, now that the ground is shaped as strongly as the Haven's.
                    case "Meadow Grass": case "Lush Grass": layer.amplitude = 0.6f; break;
                }
            }

            EditorUtility.SetDirty(profile);
            TerrainDisplacementEditor.UseStandardFootprints(terrain);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return $"{terrains.Length} terrains on the standard; dirty {scene.isDirty}";
    }

    /// <summary>Only the lake's look, on the open scene (`--entry BuildPrologueMeadow.LakeLook`).</summary>
    public static string LakeLook()
    {
        Scene scene = OpenPrologue(out string refused);
        if (refused != null) return refused;
        LakeSurfaceLook(GameObject.Find("Morning Lake"));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "lake surface and material set; dirty " + scene.isDirty;
    }

    // An oval to match the carved shore: a square plane shows its straight edge where a river's channel cuts through the bank.
    // Its own material, as the tarn's is dark and murky: this one is clear and green in the sun, like the river running out of it.
    private static void LakeSurfaceLook(GameObject water)
    {
        const string meshPath = Prologue + "Vegetation/Morning Lake Surface.asset";
        const string materialPath = "Assets/_Project/Art/Generated/Water/Water Lake (Meadow).mat";
        const int segments = 96;
        const float radius = 5f, overShore = 0.15f; // the plane's half size is 5, and it follows the carved shore just beyond it
        var vertices = new Vector3[segments + 1];
        var normals = new Vector3[segments + 1];
        var tangents = new Vector4[segments + 1];
        var uvs = new Vector2[segments + 1];
        var triangles = new int[segments * 3];
        normals[0] = Vector3.up;
        tangents[0] = new Vector4(1f, 0f, 0f, -1f);
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float reach = radius * WaterSetup.ShoreWobble(angle) + overShore;
            var at = new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
            vertices[i + 1] = at;
            normals[i + 1] = Vector3.up;
            tangents[i + 1] = new Vector4(1f, 0f, 0f, -1f);
            uvs[i + 1] = new Vector2(at.x / 10f + 0.5f, at.z / 10f + 0.5f);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i == segments - 1 ? 1 : i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        var mesh = new Mesh { name = "Morning Lake Surface", vertices = vertices, normals = normals, tangents = tangents, uv = uvs, triangles = triangles };
        mesh.RecalculateBounds();
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        water.GetComponent<MeshFilter>().sharedMesh = Load<Mesh>(meshPath);

        if (Load<Material>(materialPath) == null) AssetDatabase.CopyAsset("Assets/_Project/Art/Generated/Water/Water Still.mat", materialPath);
        var material = Load<Material>(materialPath);
        material.SetColor("_ShallowColor", new Color(0.2f, 0.42f, 0.38f));
        material.SetColor("_DeepColor", new Color(0.04f, 0.17f, 0.2f));
        material.SetFloat("_Clarity", 1.6f);
        EditorUtility.SetDirty(material);
        water.GetComponent<MeshRenderer>().sharedMaterial = material;
        WaterSurface surface = water.GetComponent<WaterSurface>();
        surface.Oval = true;
        surface.Drinkable = true;
        EditorUtility.SetDirty(surface);
    }

    /// <summary>What changed since Land, without replacing the terrain: rivers built again so they can be drunk from (`--entry
    /// BuildPrologueMeadow.Water`), the lake's look, the walls moved in. Moments then bakes the NavMesh.</summary>
    public static string Water()
    {
        Scene scene = OpenPrologue(out string refused);
        if (refused != null) return refused;
        WorldBuilder.BuildChanged(true);
        LakeSurfaceLook(GameObject.Find("Morning Lake"));
        Terrain meadow = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include).First(t => t.transform.position.x < Seam);
        Walls(meadow);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return WorldBuilder.LastReport;
    }

    // The World Builder and the lake work on each terrain alone: the shared edge takes the forest's heights exactly.
    private static void StitchSeam(Terrain forest, Terrain meadow)
    {
        TerrainData f = forest.terrainData, m = meadow.terrainData;
        int resolution = m.heightmapResolution;
        float[,] rim = f.GetHeights(0, 0, 1, resolution);
        var column = new float[resolution, 1];
        for (int row = 0; row < resolution; row++)
        {
            column[row, 0] = (rim[row, 0] * f.size.y + forest.transform.position.y - meadow.transform.position.y) / m.size.y;
        }

        m.SetHeights(resolution - 1, 0, column);
        EditorUtility.SetDirty(m);
    }

    private static float SeamError(Terrain forest, Terrain meadow)
    {
        TerrainData f = forest.terrainData, m = meadow.terrainData;
        int resolution = m.heightmapResolution;
        float[,] a = f.GetHeights(0, 0, 1, resolution), b = m.GetHeights(resolution - 1, 0, 1, resolution);
        float worst = 0f;
        for (int row = 0; row < resolution; row++)
        {
            worst = Mathf.Max(worst, Mathf.Abs(a[row, 0] * f.size.y + forest.transform.position.y - b[row, 0] * m.size.y - meadow.transform.position.y));
        }

        return worst;
    }

    private static void Grass(Terrain terrain)
    {
        List<GameObject> grass = GrassSetup.EnsureAssets();
        GrassSetup.AddTo(terrain, grass);
        GrassSetup.GroundFill[] fills = GrassSetup.DefaultFill(terrain.terrainData);
        GrassSetup.FillByGround(terrain, grass, fills, 3);
        GrassSetup.ClearUnderWater(terrain);
        GrassSetup.BakeGroundColour(terrain);
    }

    private static void CopyMist(Terrain forest, Terrain meadow)
    {
        TerrainMist own = meadow.GetComponent<TerrainMist>();
        if (own == null) own = meadow.gameObject.AddComponent<TerrainMist>();
        EditorUtility.CopySerialized(forest.GetComponent<TerrainMist>(), own);
    }

    // The invisible edge of the world, around the meadow and forest and well inside the terrain: nobody walks to where the
    // ground runs out. The camera follows the player all the way (the map will be extended so its edge can't be seen; a view
    // limit that stopped following near the edge overrode the start's framing and was removed, 2026-11-08).
    private static void Walls(Terrain meadow)
    {
        const float inset = 14f, half = 2f;
        Transform boundary = GameObject.Find("NavMesh/Environment/Boundary").transform;
        float west = meadow.transform.position.x + inset - half;
        float east = -Seam - inset + half;
        float north = Depth * 0.5f - inset + half;
        float middle = (west + east) * 0.5f;
        float span = east - west;
        foreach ((string wall, float z) in new[] { ("North Wall", north), ("South Wall", -north) })
        {
            Transform t = boundary.Find(wall);
            t.position = new Vector3(middle, t.position.y, z);
            t.localScale = new Vector3(span + half * 2f, t.localScale.y, t.localScale.z);
        }

        foreach ((string wall, float x) in new[] { ("West Wall", west), ("East Wall", east) })
        {
            Transform t = boundary.Find(wall);
            t.position = new Vector3(x, t.position.y, t.position.z);
        }
    }

    // The forest's trunks lost some trees to the road and the river, and moved with the pass. The raid keeps its own mesh.
    private static string Trunks(Terrain forest, Transform land)
    {
        Transform holder = land.Find("Map Content/" + RaidMapSurface.TrunksName);
        Mesh mesh = RaidMapSurface.TrunkMesh(forest, holder.position);
        mesh.name = "Prologue Forest Trunks";
        AssetDatabase.DeleteAsset(TrunksAsset);
        AssetDatabase.CreateAsset(mesh, TrunksAsset);
        holder.GetComponent<MeshCollider>().sharedMesh = Load<Mesh>(TrunksAsset);
        return $"{forest.terrainData.treeInstanceCount} forest trees";
    }

    private static Vector3[] Flat(Vector2[] points) => points.Select(p =>
    {
        float y = WorldGround.TryGround(p, out Vector3 ground, out _) ? ground.y : 0f;
        return new Vector3(p.x, y, p.y);
    }).ToArray();

    private static T Feature<T>(Transform root, string name, Vector3[] points) where T : WorldFeature
    {
        var go = new GameObject(name, typeof(SplineContainer));
        go.transform.SetParent(root, false);
        var feature = go.AddComponent<T>();
        Spline spline = feature.Spline;
        spline.Clear();
        foreach (Vector3 p in points) spline.Add(new BezierKnot(new float3(p.x, p.y, p.z)), TangentMode.AutoSmooth);
        return feature;
    }

    private static void Set(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

    // ==================================================================== Moments

    public static string Moments()
    {
        Scene scene = OpenPrologue(out string refused);
        if (refused != null) return refused;

        PrologueDirector director = Object.FindAnyObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        PrologueCompanion[] companions = Object.FindObjectsByType<PrologueCompanion>(FindObjectsInactive.Include);
        Transform Person(string name) => companions.First(c => c.Resident != null && c.Resident.DisplayName == name).transform;
        Transform sigrid = Person("Sigrid"), eirik = Person("Eirik"), runa = Person("Runa");

        GameObject oldRoot = GameObject.Find("Prologue Meadow");
        // What saved games remember of its stories goes to the ones made again (StoryBuilder).
        if (oldRoot != null) StoryBuilder.Remember(oldRoot.transform);
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);
        var root = new GameObject("Prologue Meadow").transform;
        Transform moments = new GameObject("Morning Moments").transform;
        moments.SetParent(root, false);

        Transform intro = GameObject.Find("Intro Moments").transform;
        Transform oldChoice = intro.Find("Story - Bow or Axe");
        StoryBuilder.Remember(intro);
        if (oldChoice != null) Object.DestroyImmediate(oldChoice.gameObject);

        Vector3 fireAt = Ground(Camp.x, Camp.y);
        Fire(root, fireAt);
        string places = Places(director, root, fireAt, sigrid, eirik, runa);
        GiverData(companions, eirik, runa);
        SigridOffers(sigrid);
        string marks = OldWaypoints(sigrid, eirik);
        LeaderLine(intro);
        Look(root);
        Transform threshold = Threshold(root);
        Transform route = Route(root, "The Road to the Cart (route)", new[] { new Vector2(-359f, 41f), new Vector2(-350f, 27f), new Vector2(-339f, 16f), new Vector2(-300f, 7f), new Vector2(-250f, -1f), Cart + new Vector2(-2f, -7f) }, "The Cart");
        Transform routeOn = Route(root, "The Road to the Trees (route)", new[] { Cart + new Vector2(-2f, -7f), new Vector2(-200f, -5f), new Vector2(-150f, -8f), new Vector2(-115f, -9.8f), new Vector2(-99f, -10.6f) }, "The Tree Line");
        Transform treeLine = routeOn.GetChild(routeOn.childCount - 1);
        Waypoints(root, sigrid, eirik, runa, treeLine);

        MobSpawnZone boars = Zone(root, "Meadow Boars", Prologue + "ExpeditionBoar.prefab", BoarMeadow, 9f, 3, MobSpawnTrigger.Manual, false, true);
        MobSpawnZone ambushZone = Zone(root, "Rootlings on the Road", Prologue + "ExpeditionRootling.prefab", new Vector2(-46f, -3f), 6f, 4, MobSpawnTrigger.Manual, true, false);
        // What the boar are running from: the first fight, by the cart and in daylight.
        MobSpawnZone roadWolves = Zone(root, "Wolves on the Road", Prologue + "ExpeditionWolf.prefab", RoadWolves, 5f, 3, MobSpawnTrigger.Manual, true, false);
        MobSpawnZone practice = Zone(root, "Practice Rootling", Prologue + "ExpeditionRootling.prefab", new Vector2(-61f, 1f), 3f, 1, MobSpawnTrigger.Manual, true, true);
        var practiceData = new SerializedObject(practice);
        practiceData.FindProperty("respawnSeconds").floatValue = 5f;
        practiceData.ApplyModifiedPropertiesWithoutUndo();
        Damageable dummy = Dummy(root);
        Interactable cartProp = CartProp(root);
        Interactable lakeStone = LakeStone(root);
        Transform troll = GameObject.Find("Intro Moments/The Old Troll (story)")?.transform ?? intro.Find("The Old Troll (story)");
        // Tall enough to tower over the stones, short enough that the camera can see all of it.
        troll.localScale = Vector3.one * 1.3f;
        foreach (SkinnedMeshRenderer skin in troll.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.sharedMaterial = Load<Material>("Assets/_Project/Art/Enemies/MossTroll.mat");
        Transform[] runners = Runners(root);
        Transform behind = RoadBehind(root);
        Secrets(root);
        Waypoint(dummy.transform, Existing("EirikLesson"), 0, "The dummy", QuestWaypointWhen.Active);
        Waypoint(eirik, Existing("EirikLesson"), 0, "Eirik", QuestWaypointWhen.ReadyToTurnIn);

        Walls(Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include).First(t => t.transform.position.x < Seam));
        SaveableEntityTools.AssignSceneIds(scene);
        Reactions(root, threshold, sigrid, eirik);
        Stories(moments, sigrid, eirik, runa, route, treeLine, threshold, ambushZone, boars);
        Beats(moments, root, companions, sigrid, eirik, runa, route, routeOn, treeLine, dummy, practice, cartProp, lakeStone, troll, runners, behind, roadWolves);

        var surface = Object.FindAnyObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        surface.BuildNavMesh();
        NavMeshData navData = surface.navMeshData;
        AssetDatabase.DeleteAsset(NavMeshAsset);
        AssetDatabase.CreateAsset(navData, NavMeshAsset);
        surface.navMeshData = Load<NavMeshData>(NavMeshAsset);
        EditorUtility.SetDirty(surface);

        StoryBuilder.Finish();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        string problems = string.Join("; ", moments.GetComponentsInChildren<StoryScript>().SelectMany(s => s.FindProblems()));
        return $"{places}; boars {boars.transform.position:F0}; {marks}; problems: {(problems.Length > 0 ? problems : "none")}; dirty {scene.isDirty}";
    }

    // A fire to stand around: the campfire's model and light, without what makes it a building the player owns.
    private static void Fire(Transform root, Vector3 at)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/_Project/Prefabs/Structures/Campfire.prefab"));
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "Morning Fire";
        instance.transform.SetParent(root, false);
        instance.transform.position = at;
        Object.DestroyImmediate(instance.transform.Find("HealthBar").gameObject);
        Strip(instance, false, part => false);

        var seat = Load<GameObject>("Assets/_Project/Prefabs/Environment/World/Rotting Log.prefab");
        foreach ((Vector2 offset, float turn) in new[] { (new Vector2(-3.4f, 0.8f), 80f), (new Vector2(0.8f, 3.5f), 170f) })
        {
            if (seat == null) break;
            var log = (GameObject)PrefabUtility.InstantiatePrefab(seat);
            log.transform.SetParent(instance.transform, false);
            log.transform.position = Ground(at.x + offset.x, at.z + offset.y);
            log.transform.rotation = Quaternion.Euler(0f, turn, 0f) * seat.transform.rotation;
        }
    }

    private static string Places(PrologueDirector director, Transform root, Vector3 fire, Transform sigrid, Transform eirik, Transform runa)
    {
        // Where they stand as the game starts: around the fire, facing it.
        Stand(sigrid, fire, new Vector2(-2.7f, 1.1f));
        Stand(eirik, fire, new Vector2(1.2f, 2.7f));
        Stand(runa, fire, new Vector2(2.8f, -0.6f));

        Transform camp = new GameObject("Morning Camp").transform;
        camp.SetParent(root, false);
        camp.position = Ground(fire.x - 0.6f, fire.z - 3.8f);
        camp.rotation = Quaternion.LookRotation(new Vector3(fire.x - camp.position.x, 0f, fire.z - camp.position.z));

        var so = new SerializedObject(director);
        so.FindProperty("meadowPoint").objectReferenceValue = camp;
        var edge = (Transform)so.FindProperty("startPoint").objectReferenceValue;
        so.ApplyModifiedPropertiesWithoutUndo();
        edge.position = Ground(-82f, -12f);
        edge.rotation = Quaternion.LookRotation(Vector3.right);

        // The rig is seen from the first frame, so it starts where the player does.
        Player player = Object.FindAnyObjectByType<Player>(FindObjectsInactive.Include);
        Vector3 shift = camp.position - player.transform.position;
        player.transform.SetPositionAndRotation(camp.position, camp.rotation);
        foreach (string name in new[] { "Main Camera", "Player Cinemachine Camera" })
        {
            GameObject rig = GameObject.Find(name);
            if (rig != null) rig.transform.position += shift;
        }

        return $"camp {camp.position:F0}, forest's edge {edge.position:F0}";
    }

    private static void Stand(Transform person, Vector3 fire, Vector2 offset)
    {
        Vector3 at = Ground(fire.x + offset.x, fire.z + offset.y);
        person.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(fire.x - at.x, 0f, fire.z - at.z)));
    }

    private static void GiverData(PrologueCompanion[] companions, Transform eirik, Transform runa)
    {
        var lines = new Dictionary<string, (string line, string[] topics)>
        {
            ["Sigrid"] = ("Take what time you need, but not too much. It is a long walk, and I want to be at the forest by noon.", new[]
            {
                "What are we actually looking for?",
                "The cause. Trolls do not leave their crags for no reason, and wolves do not forget fire. Something in that forest has changed, and the villages want it named before it reaches their doors.",
                "Who went ahead?",
                "Ingrid, Bjarne, Tor and Liv. An alchemist, a smith, a carpenter and a scholar: the ones who can make sense of what they find. I sent them on two days ago to look over the old road. It was a short job.",
                "Why does it take someone who reads runes?",
                "Because the only old things out there are stones and roots, and the marks on them are older than any village. Whatever is wrong, I expect to find it written down.",
            }),
            ["Eirik"] = ("Eat something. It is a long road, and the porridge is not getting any better.", new[]
            {
                "What do you think we will find?",
                "Wolves, probably. Perhaps a witch, perhaps a sick troll. It is usually less than the stories and more than you packed for. Four friends, if we are lucky. Bjarne owes me money.",
                "Have you been in that forest before?",
                "Once, as a boy, with my sister. Spruce, moss, the quiet you get in old woods. Nothing in it frightened me but the dark. I would like that to still be true.",
            }),
            ["Runa"] = ("Nice morning. That is usually how it starts.", new[]
            {
                "What have the tracks told you?",
                "Deer, boar, wolf, all running west out of the trees, out here where they don't belong. Nothing sleeps in the open unless it is afraid to sleep at home.",
                "Why did you come along?",
                "Someone is paying. And I would rather know what is coming out of there than wait for it to find me.",
            }),
        };

        foreach (PrologueCompanion companion in companions)
        {
            if (companion.Resident == null || !lines.TryGetValue(companion.Resident.DisplayName, out var said)) continue;
            var so = new SerializedObject(companion);
            so.FindProperty("meadowLine").stringValue = said.line;
            SerializedProperty topics = so.FindProperty("morningTopics");
            topics.arraySize = said.topics.Length / 2;
            for (int i = 0; i < topics.arraySize; i++)
            {
                topics.GetArrayElementAtIndex(i).FindPropertyRelative("question").stringValue = said.topics[i * 2];
                topics.GetArrayElementAtIndex(i).FindPropertyRelative("answer").stringValue = said.topics[i * 2 + 1];
            }

            if (companion.Resident.DisplayName == "Sigrid") AddPressTopic(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        SideGiver(runa, new[]
        {
            ("HuntForThePot", "Anything I can help with?",
                "As it happens. There are blight boar on the grass north of the road, east of the camp, which is wrong all by itself, and we could use the meat. Take my spare bow: hold the left button to draw, let go to shoot. Bring down two.",
                "I'll hunt them.",
                "Two boar, on the grass north of the road, east of the camp. Hold the left button to draw.",
                "Clean enough. Keep the bow; you've earned it, and I was tired of carrying it."),
        });
        SideGiver(eirik, new[]
        {
            ("WoodForTheRoad", "Do you need firewood?",
                "We always need firewood. Take this hatchet; there are good trees on the slope north of the road. Six logs will do us.",
                "I'll bring wood.",
                "Six logs from the slope north of the road. Walk up to a tree and get chopping.",
                "That will keep us warm. Keep the hatchet."),
            ("FishForBreakfast", "Shall I catch us some fish?",
                "Now that is a fine idea. Anything but this porridge. Here is a rod and a handful of grubs, and the lake is just south of the road. Two fish, and I'll owe you.",
                "I'll go fishing.",
                "Two fish from the lake. Hold attack to swing back, let go to cast, and strike when the float goes under.",
                "Breakfast! You may be the best person on this expedition. Keep the rod; there is bound to be water in those woods."),
            LessonOffer,
            ("BerriesForThePot", "Is there anything to eat?",
                "Dry bread, unless you fancy a walk. There are berry bushes in the clearing west of the shelter. Pick five and stew them over the fire; take my waterskin for the pot. Be quick about it. I do not like how quiet it is.",
                "I'll get them.",
                "Five berries from the clearing to the west, then the pot. My waterskin is in your pack.",
                "Hot food. You have just made three friends for life. Take these, in case the night is not done with us."),
        });
    }

    // By the fire in the forest, the player can push her on what Eirik has been hinting at; the waystones answer it.
    private static void AddPressTopic(SerializedObject sigrid)
    {
        const string question = "Eirik says to watch your hands around those stones.";
        SerializedProperty topics = sigrid.FindProperty("campTopics");
        int at = -1;
        for (int i = 0; i < topics.arraySize; i++)
        {
            if (topics.GetArrayElementAtIndex(i).FindPropertyRelative("question").stringValue == question) at = i;
        }

        if (at < 0)
        {
            at = topics.arraySize++;
        }

        SerializedProperty topic = topics.GetArrayElementAtIndex(at);
        topic.FindPropertyRelative("question").stringValue = question;
        topic.FindPropertyRelative("answer").stringValue = "Eirik sees a great deal for a man who pretends not to. Yes. I have seen stones like these before, and I know what they do. Ask me again when you have to touch one.";
        topic.FindPropertyRelative("raises").objectReferenceValue = Load<GameEventDefinition>(Prologue + "Events/PressedSigrid.asset");
    }

    // Keeps the giver above the PrologueCompanion, so its waiting line comes before the camp chatter (Talkable asks in order).
    private static void SideGiver(Transform person, (string quest, string ask, string text, string accept, string waiting, string thanks)[] offers)
    {
        QuestGiver giver = person.GetComponent<QuestGiver>();
        if (giver == null)
        {
            giver = person.gameObject.AddComponent<QuestGiver>();
            PrologueCompanion companion = person.GetComponent<PrologueCompanion>();
            int above = System.Array.IndexOf(person.GetComponents<Component>(), companion);
            int at = System.Array.IndexOf(person.GetComponents<Component>(), giver);
            for (int i = at; i > above; i--) UnityEditorInternal.ComponentUtility.MoveComponentUp(giver);
        }

        var so = new SerializedObject(giver);
        so.FindProperty("speakerName").stringValue = person.GetComponent<PrologueCompanion>().Resident.DisplayName;
        SerializedProperty list = so.FindProperty("sideOffers");
        list.arraySize = offers.Length;
        for (int i = 0; i < offers.Length; i++)
        {
            SerializedProperty offer = list.GetArrayElementAtIndex(i);
            offer.FindPropertyRelative("quest").objectReferenceValue = Existing(offers[i].quest);
            offer.FindPropertyRelative("askText").stringValue = offers[i].ask;
            offer.FindPropertyRelative("text").stringValue = offers[i].text;
            offer.FindPropertyRelative("acceptText").stringValue = offers[i].accept;
            offer.FindPropertyRelative("waitingText").stringValue = offers[i].waiting;
            offer.FindPropertyRelative("thanksText").stringValue = offers[i].thanks;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sigrid briefs twice, in the morning and before the road. After that the night gives nobody time to be briefed.
    private static void SigridOffers(Transform sigrid)
    {
        var so = new SerializedObject(sigrid.GetComponent<QuestGiver>());
        SerializedProperty offers = so.FindProperty("offers");
        offers.arraySize = 0;
        AddOffer(offers, 0, Existing("MorningCamp"), MorningBriefing, "Tell me.", "Finish your porridge and listen. This is the only quiet you will get today.");
        AddOffer(offers, 1, Existing("RoadToForest"), RoadBriefing, "I'm ready. Let's go.", "Stay on the road. We keep a steady pace, and the others will wait if you fall behind.");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The marks older builds put in the scene for quests that are merged or gone: Sigrid's own (a briefing to fetch, a quest
    // to hand in) go, and the ones at the shelter and the stones point at the step of the quest that now holds them.
    private static string OldWaypoints(Transform sigrid, Transform eirik)
    {
        int removed = 0, moved = 0;
        foreach (QuestWaypoint mark in sigrid.GetComponents<QuestWaypoint>())
        {
            Object.DestroyImmediate(mark);
            removed++;
        }

        QuestDefinition shelter = Existing("FindShelter"), stones = Existing("ReachPortal");
        int fire = shelter.Objectives.Count;
        foreach (QuestWaypoint mark in Object.FindObjectsByType<QuestWaypoint>(FindObjectsInactive.Include))
        {
            (QuestDefinition quest, int objective, string label, string name)? now = mark.name switch
            {
                "Quest Waypoint - Shelter from the Storm" => (shelter, 1, "Shelter", mark.name),
                "Quest Waypoint - A Fire of Our Own" => (shelter, fire, "Build the fire here", "Quest Waypoint - Shelter from the Storm (the fire)"),
                "Quest Waypoint - Shelter from the Storm (the fire)" => (shelter, fire, "Build the fire here", mark.name),
                "Quest Waypoint - Something Is Coming" => (stones, 1, "The standing stones", mark.name),
                "Quest Waypoint - Wake the Waystones" => (stones, 2, "The centre stone", "Quest Waypoint - Something Is Coming (the stone)"),
                "Quest Waypoint - Something Is Coming (the stone)" => (stones, 2, "The centre stone", mark.name),
                _ => null,
            };
            if (now.HasValue)
            {
                mark.Configure(now.Value.quest, now.Value.objective, now.Value.label);
                mark.name = now.Value.name;
                EditorUtility.SetDirty(mark);
                moved++;
            }
            else if (mark.Quest == null)
            {
                // A mark for a quest that no longer exists.
                Object.DestroyImmediate(mark.gameObject.GetComponents<Component>().Length <= 2 ? (Object)mark.gameObject : mark);
                removed++;
            }
        }

        Waypoint(eirik, Existing("BerriesForThePot"), 0, "Eirik", QuestWaypointWhen.ReadyToTurnIn);
        return $"old marks: {removed} removed, {moved} kept";
    }

    private static void AddOffer(SerializedProperty offers, int at, QuestDefinition quest, string text, string accept, string waiting)
    {
        offers.InsertArrayElementAtIndex(at);
        SerializedProperty offer = offers.GetArrayElementAtIndex(at);
        offer.FindPropertyRelative("quest").objectReferenceValue = quest;
        offer.FindPropertyRelative("text").stringValue = text;
        offer.FindPropertyRelative("acceptText").stringValue = accept;
        offer.FindPropertyRelative("waitingText").stringValue = waiting;
        offer.FindPropertyRelative("askText").stringValue = "";
        offer.FindPropertyRelative("thanksText").stringValue = "";
    }

    // The old troll's story sent them up "the old road"; the road runs east now and a path leads to the stones.
    private static void LeaderLine(Transform intro)
    {
        foreach (StoryScript story in intro.GetComponentsInChildren<StoryScript>(true))
        {
            foreach (SayNode say in story.Nodes.OfType<SayNode>())
            {
                if (say.line != null && say.line.Contains("Follow the old road north"))
                {
                    say.line = say.line.Replace("Follow the old road north, to the standing stones", "Take the old path north, to the standing stones");
                    EditorUtility.SetDirty(story);
                }
            }
        }

        var quest = Existing("ReachPortal");
        var so = new SerializedObject(quest);
        so.FindProperty("description").stringValue = "Something much larger is approaching. Leave the camp and take the old path north to the standing stones.";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
    }

    // The first look is the Haven's sunlit one; the second is the raid's own, faded in when the forest closes over them.
    private static void Look(Transform root)
    {
        Transform sky = GameObject.Find("Sky and Weather").transform;
        Volume global = GameObject.Find("Global Volume").GetComponent<Volume>();
        Volume night = sky.Find("Night Volume").GetComponent<Volume>();
        Volume golden = sky.Find("Golden Hour Volume").GetComponent<Volume>();
        Volume overcast = sky.Find("Overcast Volume").GetComponent<Volume>();
        DayNightCycle cycle = sky.GetComponentInChildren<DayNightCycle>(true);
        WeatherSystem weather = sky.GetComponentInChildren<WeatherSystem>(true);

        global.sharedProfile = Profile("SafeHaven_Volume");
        night.sharedProfile = Profile("SafeHaven_Night");
        golden.sharedProfile = Profile("SafeHaven_GoldenHour");
        overcast.sharedProfile = Profile("SafeHaven_HomeOvercast");
        EditorUtility.SetDirty(global);

        Volume Second(string name, string profile, int priority)
        {
            Transform old = sky.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(sky, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0f;
            volume.sharedProfile = Load<VolumeProfile>(Prologue + profile + ".asset") ?? Profile(profile);
            return volume;
        }

        SceneLook look = sky.GetComponent<SceneLook>();
        if (look == null) look = sky.gameObject.AddComponent<SceneLook>();
        var data = new SerializedObject(look);
        data.FindProperty("dayNight").objectReferenceValue = cycle;
        data.FindProperty("secondDayNight").objectReferenceValue = Load<DayNightProfile>(Prologue + "Prologue Forest Night.asset");
        data.FindProperty("startAmount").floatValue = 0f;
        SerializedProperty pairs = data.FindProperty("volumes");
        (Volume first, Volume second, bool driven)[] table =
        {
            (global, Second("Forest Look", "Prologue Forest Look", 5), false),
            (night, Second("Forest Night", "MistyWoods_Raid_Night", 6), true),
            (golden, Second("Forest Golden Hour", "MistyWoods_GoldenHour", 6), true),
            (overcast, Second("Forest Overcast", "MistyWoods_Overcast", 7), true),
        };
        pairs.arraySize = table.Length;
        for (int i = 0; i < table.Length; i++)
        {
            SerializedProperty pair = pairs.GetArrayElementAtIndex(i);
            pair.FindPropertyRelative("first").objectReferenceValue = table[i].first;
            pair.FindPropertyRelative("second").objectReferenceValue = table[i].second;
            pair.FindPropertyRelative("drivenBySky").boolValue = table[i].driven;
        }

        data.ApplyModifiedPropertiesWithoutUndo();

        var cycleData = new SerializedObject(cycle);
        cycleData.FindProperty("profile").objectReferenceValue = Load<DayNightProfile>("Assets/_Project/Data/Environment/Safe Haven Day Night.asset");
        cycleData.FindProperty("startHour").floatValue = 8.5f;
        cycleData.FindProperty("clockRuns").boolValue = false;
        cycleData.ApplyModifiedPropertiesWithoutUndo();
        var weatherData = new SerializedObject(weather);
        weatherData.FindProperty("startWeather").objectReferenceValue = Weather("Clear");
        weatherData.FindProperty("changesByItself").boolValue = false;
        weatherData.ApplyModifiedPropertiesWithoutUndo();
    }

    // Nobody goes in before the expedition does: the whole edge of the forest, lifted when they cross.
    private static Transform Threshold(Transform root)
    {
        var go = new GameObject("Forest Threshold");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(Seam - 1f, 30f, 0f);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(1f, 80f, Depth);
        return go.transform;
    }

    private static Transform Route(Transform root, string name, Vector2[] points, string lastPoint)
    {
        var route = new GameObject(name).transform;
        route.SetParent(root, false);
        for (int i = 0; i < points.Length; i++)
        {
            var point = new GameObject(i == points.Length - 1 ? lastPoint : "Point " + i).transform;
            point.SetParent(route, false);
            point.position = Ground(points[i].x, points[i].y);
        }

        return route;
    }

    private static void Waypoints(Transform root, Transform sigrid, Transform eirik, Transform runa, Transform treeLine)
    {
        Waypoint(sigrid, Existing("MorningCamp"), 0, "Sigrid", QuestWaypointWhen.Available);
        Waypoint(sigrid, Existing("RoadToForest"), 0, "Sigrid", QuestWaypointWhen.Available);
        Waypoint(sigrid, Existing("MorningCamp"), 0, "Sigrid", QuestWaypointWhen.ReadyToTurnIn);
        Waypoint(treeLine, Existing("RoadToForest"), 0, "The forest", QuestWaypointWhen.Active);
        Waypoint(runa, Existing("HuntForThePot"), 0, "Runa", QuestWaypointWhen.ReadyToTurnIn);
        Waypoint(eirik, Existing("FishForBreakfast"), 0, "Eirik", QuestWaypointWhen.ReadyToTurnIn);
        Waypoint(eirik, Existing("WoodForTheRoad"), 0, "Eirik", QuestWaypointWhen.ReadyToTurnIn);
        Waypoint(Mark(root, "Boar Meadow", BoarMeadow.x, BoarMeadow.y), Existing("HuntForThePot"), 0, "The boar", QuestWaypointWhen.Active);
        Waypoint(Mark(root, "Fishing Spot", -345f, 12f), Existing("FishForBreakfast"), 0, "The lake", QuestWaypointWhen.Active);
        Waypoint(Mark(root, "Slope with Trees", -300f, 48f), Existing("WoodForTheRoad"), 0, "The trees", QuestWaypointWhen.Active);
    }

    private static Transform Mark(Transform root, string name, float x, float z)
    {
        var mark = new GameObject(name).transform;
        mark.SetParent(root, false);
        mark.position = Ground(x, z);
        return mark;
    }

    private static void Waypoint(Transform at, QuestDefinition quest, int objective, string label, QuestWaypointWhen when)
    {
        string name = $"Quest Waypoint - {quest.DisplayName} ({when})";
        Transform old = at.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(at, false);
        go.AddComponent<QuestWaypoint>().Configure(quest, objective, label, when);
    }

    private static MobSpawnZone Zone(Transform root, string name, string prefab, Vector2 at, float radius, int count, MobSpawnTrigger trigger, bool hunt, bool respawn)
    {
        var zoneObject = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/_Project/Prefabs/Raids/Mob Spawn Zone.prefab"));
        zoneObject.name = name;
        zoneObject.transform.SetParent(root, true);
        zoneObject.transform.position = Ground(at.x, at.y);
        MobSpawnZone zone = zoneObject.GetComponent<MobSpawnZone>();
        var data = new SerializedObject(zone);
        SerializedProperty creatures = data.FindProperty("creatures");
        creatures.ClearArray();
        creatures.InsertArrayElementAtIndex(0);
        creatures.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = Load<GameObject>(prefab);
        creatures.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
        data.FindProperty("count").vector2IntValue = new Vector2Int(count, count);
        data.FindProperty("trigger").intValue = (int)trigger;
        data.FindProperty("wakeDistance").floatValue = 40f;
        data.FindProperty("huntPlayer").boolValue = hunt;
        data.FindProperty("respawn").boolValue = respawn;
        data.FindProperty("respawnSeconds").floatValue = 60f;
        SerializedProperty corners = data.FindProperty("corners");
        Vector3[] square = { new Vector3(-radius, 0f, -radius), new Vector3(radius, 0f, -radius), new Vector3(radius, 0f, radius), new Vector3(-radius, 0f, radius) };
        corners.arraySize = square.Length;
        for (int i = 0; i < square.Length; i++) corners.GetArrayElementAtIndex(i).vector3Value = square[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        return zone;
    }

    // ----- Reactions: what stays changed after loading -----

    private static void Reactions(Transform root, Transform threshold, Transform sigrid, Transform eirik)
    {
        Transform sky = GameObject.Find("Sky and Weather").transform;
        var look = sky.GetComponent<SceneLook>();
        var cycle = sky.GetComponentInChildren<DayNightCycle>(true);
        var weather = sky.GetComponentInChildren<WeatherSystem>(true);
        var audio = Object.FindAnyObjectByType<SceneAudio>(FindObjectsInactive.Include);
        GameEventDefinition entered = Load<GameEventDefinition>(Prologue + "Events/ForestEntered.asset");

        var go = new GameObject("Entered the Forest - Reaction");
        go.transform.SetParent(root, false);
        var listener = go.AddComponent<GameEventListener>();
        var data = new SerializedObject(listener);
        data.FindProperty("listenFor").intValue = (int)ListenFor.QuestEvent;
        data.FindProperty("gameEvent").objectReferenceValue = entered;
        data.FindProperty("respondIfAlreadyHappened").boolValue = true;
        SerializedProperty actions = data.FindProperty("actions");
        actions.arraySize = 0;

        SerializedProperty Action(SceneEventActionKind kind)
        {
            int at = actions.arraySize++;
            SerializedProperty action = actions.GetArrayElementAtIndex(at);
            action.FindPropertyRelative("kind").intValue = (int)kind;
            return action;
        }

        SerializedProperty fade = Action(SceneEventActionKind.ChangeLook);
        fade.FindPropertyRelative("look").objectReferenceValue = look;
        fade.FindPropertyRelative("lookAmount").floatValue = 1f;
        fade.FindPropertyRelative("transitionSeconds").floatValue = 10f;
        SerializedProperty storm = Action(SceneEventActionKind.ChangeWeather);
        storm.FindPropertyRelative("weatherSystem").objectReferenceValue = weather;
        storm.FindPropertyRelative("weather").objectReferenceValue = Weather("Rain + Mist");
        storm.FindPropertyRelative("transitionSeconds").floatValue = 14f;
        SerializedProperty dark = Action(SceneEventActionKind.ChangeTimeOfDay);
        dark.FindPropertyRelative("dayNight").objectReferenceValue = cycle;
        dark.FindPropertyRelative("hour").floatValue = 23f;
        dark.FindPropertyRelative("transitionSeconds").floatValue = 4f;
        // The dark forest has its own music (2026-11-09; it was silent): fights still bring the combat music.
        SerializedProperty eerie = Action(SceneEventActionKind.PlayMusic);
        eerie.FindPropertyRelative("sceneAudio").objectReferenceValue = audio;
        SerializedProperty eerieTracks = eerie.FindPropertyRelative("music.tracks");
        eerieTracks.arraySize = 1;
        eerieTracks.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue =
            Load<AudioClip>(ForestMusic);
        eerieTracks.GetArrayElementAtIndex(0).FindPropertyRelative("volume").floatValue = 1f;
        eerieTracks.GetArrayElementAtIndex(0).FindPropertyRelative("startAt").floatValue = 0f;
        eerie.FindPropertyRelative("fightsInterrupt").boolValue = true;
        SerializedProperty open = Action(SceneEventActionKind.SetObjectActive);
        open.FindPropertyRelative("target").objectReferenceValue = threshold.gameObject;
        open.FindPropertyRelative("active").boolValue = false;
        SerializedProperty noBirds = Action(SceneEventActionKind.SetObjectActive);
        noBirds.FindPropertyRelative("target").objectReferenceValue = GameObject.Find("Birds");
        noBirds.FindPropertyRelative("active").boolValue = false;
        foreach (GameObject torch in Torches(sigrid, eirik))
        {
            SerializedProperty lit = Action(SceneEventActionKind.SetObjectActive);
            lit.FindPropertyRelative("target").objectReferenceValue = torch;
            lit.FindPropertyRelative("active").boolValue = true;
        }

        data.ApplyModifiedPropertiesWithoutUndo();

        // On the road they light them themselves. Only live: after loading mid-walk it is morning again.
        var lighting = new GameObject("Torches Lit - Reaction");
        lighting.transform.SetParent(root, false);
        var lightListener = lighting.AddComponent<GameEventListener>();
        var lightData = new SerializedObject(lightListener);
        lightData.FindProperty("listenFor").intValue = (int)ListenFor.QuestEvent;
        lightData.FindProperty("gameEvent").objectReferenceValue = Load<GameEventDefinition>(Prologue + "Events/TorchesLit.asset");
        lightData.FindProperty("respondIfAlreadyHappened").boolValue = false;
        SerializedProperty lightActions = lightData.FindProperty("actions");
        foreach (GameObject torch in Torches(sigrid, eirik))
        {
            int at = lightActions.arraySize++;
            SerializedProperty action = lightActions.GetArrayElementAtIndex(at);
            action.FindPropertyRelative("kind").intValue = (int)SceneEventActionKind.SetObjectActive;
            action.FindPropertyRelative("target").objectReferenceValue = torch;
            action.FindPropertyRelative("active").boolValue = true;
        }

        lightData.ApplyModifiedPropertiesWithoutUndo();

        // A torch in the hand makes no sense in the morning sun.
        foreach (GameObject torch in Torches(sigrid, eirik)) torch.SetActive(false);
    }

    private static IEnumerable<GameObject> Torches(params Transform[] people) => people
        .SelectMany(p => p.GetComponentsInChildren<ExpeditionTorch>(true)).Select(t => t.gameObject);

    // ----- Stories -----

    private static void Stories(Transform moments, Transform sigrid, Transform eirik, Transform runa, Transform route, Transform treeLine, Transform threshold, MobSpawnZone ambushZone, MobSpawnZone boars)
    {
        QuestDefinition morning = Existing("MorningCamp"), road = Existing("RoadToForest"), shelter = Existing("FindShelter");
        GameEventDefinition met = Load<GameEventDefinition>(Prologue + "Events/MetTheExpedition.asset");
        GameEventDefinition entered = Load<GameEventDefinition>(Prologue + "Events/ForestEntered.asset");
        GameEventDefinition litEvent = Load<GameEventDefinition>(Prologue + "Events/TorchesLit.asset");
        GameEventDefinition fireTalk = Load<GameEventDefinition>(Prologue + "Events/FireTalk.asset");
        GameEventDefinition chosen = Load<GameEventDefinition>(Prologue + "Events/WeaponChosen.asset");
        GameEventDefinition ambushed = Load<GameEventDefinition>(Prologue + "Events/RoadAmbush.asset");
        StorySpeaker S() => Speaker(sigrid, "Sigrid");
        StorySpeaker E() => Speaker(eirik, "Eirik");
        StorySpeaker R() => Speaker(runa, "Runa");
        SayNode Aside(StorySpeaker who, string line) => new SayNode { speaker = who, line = line, aside = true, afterFightPause = false };

        // ----- Music in the morning -----
        var music = Story(moments, "Story - Morning Music");
        var sceneStarts = new WhenSceneStarts { once = false };
        var inTheSun = new HasHappenedNode { questEvent = entered };
        // Fights interrupt it: the wolves on the road brought no combat music (the user, 2026-11-11).
        var play = new MusicNode { change = MusicNode.Change.Play, fightsInterrupt = true };
        var clip = Load<AudioClip>("Assets/_Project/Audio/Safe Haven - Heartwood Sanctuary.mp3");
        // Loaded on the road after the wolves: the forest's music, as it was when saved.
        var pastWolves = new HasHappenedNode { questEvent = Event("RoadWolvesBeaten", "prologue.road_wolves_beaten", "Beat the Wolves on the Road", "") };
        var playForest = new MusicNode { change = MusicNode.Change.Play, fightsInterrupt = true };
        var seenWest = new RevealGroundNode { centre = Mark(moments, "Known Ground (west)", -335f, 0f), radius = 125f };
        var seenEast = new RevealGroundNode { centre = Mark(moments, "Known Ground (east)", -212f, 0f), radius = 112f };
        Link(music, (sceneStarts, 0, inTheSun), (inTheSun, 1, pastWolves), (pastWolves, 1, play), (pastWolves, 0, playForest), (inTheSun, 1, seenWest), (inTheSun, 1, seenEast));
        PlaylistOf(music, play, clip);
        PlaylistOf(music, playForest, Load<AudioClip>(ForestMusic));

        // ----- Everyone says who they are -----
        var intro = Story(moments, "Story - The Morning Fire");
        var joined = new WhenQuest { quest = morning, when = WhenQuest.Moment.Starts, once = false, alsoAfterLoading = true };
        var told = new HasHappenedNode { questEvent = met };
        var beat = new WaitNode { seconds = 0.4f };
        var hello = new SayNode { speaker = S(), line = "For anyone who slept through last night's talk, and I know who you are: I am Sigrid, and we walk east today. The villages asked what is wrong in that forest. Two days ago I sent Ingrid, Bjarne, Tor and Liv ahead to look. They should have been back yesterday." };
        var soldier = new SayNode { speaker = E(), line = "Eirik. Twenty winters in the shield wall at Granheim. I walk in front, I carry the heavy things and I make sure everyone eats. There is porridge. It is bad." };
        var tracker = new SayNode { speaker = R(), line = "Runa. I find the tracks, and I shoot what makes them. Trolls off their crags, witches on the roads, wolves that don't run from fire. I want to see it for myself before I believe any of it." };
        var why = new ChoiceNode
        {
            speaker = S(),
            line = "And you. You came to us with a strong back and few questions. Nobody walks towards that forest by accident. Why are you here?",
            answers = new List<string> { "For the coin.", "Someone I know went in there.", "To see if the stories are true." },
            mustAnswer = true,
        };
        var coin = new RaiseQuestEventNode { questEvent = Load<GameEventDefinition>(Prologue + "Events/CameForCoin.asset") };
        var kin = new RaiseQuestEventNode { questEvent = Load<GameEventDefinition>(Prologue + "Events/CameForKin.asset") };
        var truth = new RaiseQuestEventNode { questEvent = Load<GameEventDefinition>(Prologue + "Events/CameForTruth.asset") };
        var coinReply = new SayNode { speaker = R(), line = "An honest answer. I like you already." };
        var kinReply = new SayNode { speaker = E(), line = "Then we are after the same thing. We will find them." };
        var truthReply = new SayNode { speaker = S(), line = "Careful. The ones who go looking for the truth of it are the ones who find it." };
        var welcome = new SayNode { speaker = S(), line = "We leave when you are ready. If you want to earn your porridge first, ask the others. They always have something." };
        var heard = new RaiseQuestEventNode { questEvent = met };
        Link(intro, (joined, 0, told), (told, 1, beat), (beat, 0, hello), (hello, 0, soldier), (soldier, 0, tracker), (tracker, 0, why),
            (why, 0, coin), (why, 1, kin), (why, 2, truth), (coin, 0, coinReply), (kin, 0, kinReply), (truth, 0, truthReply),
            (coinReply, 0, welcome), (kinReply, 0, welcome), (truthReply, 0, welcome), (welcome, 0, heard));

        // ----- They talk by the fire while the player is free -----
        var chat = Story(moments, "Story - Talk by the Fire");
        var afterIntro = new WhenQuestEvent { questEvent = met };
        var settle = new WaitNode { seconds = 7f };
        var talkOne = Aside(E(), "A bewitched forest. My grandmother called any forest that ate her goats bewitched.");
        var talkTwo = Aside(R(), "Your grandmother never saw wolves walk into a lit camp.");
        var talkThree = Aside(S(), "Save it for the road. We have until the porridge gets worse.");
        var talkFour = Aside(E(), "Nothing gets worse than this porridge.");
        var remembered = new RaiseQuestEventNode { questEvent = fireTalk };
        Link(chat, (afterIntro, 0, settle), (settle, 0, talkOne), (talkOne, 0, talkTwo), (talkTwo, 0, talkThree), (talkThree, 0, talkFour), (talkFour, 0, remembered));

        // ----- What the errands hand over -----
        var bow = Story(moments, "Story - Runa's Spare Bow");
        var hunt = new WhenQuest { quest = Existing("HuntForThePot"), when = WhenQuest.Moment.Starts };
        var giveBow = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("CrudeWoodenBow"), 1), new ItemAmount(Item("CrudeStoneArrow"), 30) } };
        Link(bow, (hunt, 0, giveBow));
        // The boar are out on open grass, where the player can see them, for as long as the hunt is on.
        var huntOn = new WhenQuest { quest = Existing("HuntForThePot"), when = WhenQuest.Moment.Starts, once = false, alsoAfterLoading = true };
        var boarsOut = new SpawnEnemiesNode { zone = boars };
        var huntDone = new WhenQuest { quest = Existing("HuntForThePot"), when = WhenQuest.Moment.IsCompleted, once = false };
        var boarsGone = new DespawnEnemiesNode { zone = boars };
        var moveOn = new WhenQuest { quest = road, when = WhenQuest.Moment.Starts, once = false };
        var boarsLeft = new DespawnEnemiesNode { zone = boars };
        Link(bow, (huntOn, 0, boarsOut), (huntDone, 0, boarsGone), (moveOn, 0, boarsLeft));
        var rod = Story(moments, "Story - Eirik's Rod");
        var fish = new WhenQuest { quest = Existing("FishForBreakfast"), when = WhenQuest.Moment.Starts };
        var giveRod = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("FishingRod"), 1), new ItemAmount(Item("Materials/Grubs"), 8) } };
        Link(rod, (fish, 0, giveRod));
        var axe = Story(moments, "Story - Eirik's Axe");
        var wood = new WhenQuest { quest = Existing("WoodForTheRoad"), when = WhenQuest.Moment.Starts };
        var giveAxe = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("StoneHatchet"), 1) } };
        Link(axe, (wood, 0, giveAxe));

        // ----- Too keen -----
        var eager = Story(moments, "Story - Not Without Us");
        Transform hint = new GameObject("Threshold Hint").transform;
        hint.SetParent(moments, false);
        hint.position = Ground(Seam - 9f, -10.5f);
        var atEdge = new WhenPlayerNear { place = hint, radius = 8f, once = false };
        var setOutYet = new CheckQuestNode { quest = road };
        var notYet = Aside(S(), "Not alone, and not before we are ready. Come back to the others.");
        Link(eager, (atEdge, 0, setOutYet), (setOutYet, 0, notYet));

        // ----- Across the line -----
        var line2 = Story(moments, "Story - The Forest Closes In");
        var across = new WhenQuestEvent { questEvent = entered };
        var flash = new LightningNode();
        var shake = new ShakeCameraNode { strength = 0.5f };
        var thunder = new PlaySoundNode { sound = Load<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Thunder.asset") };
        var wind = Aside(R(), "There. Feel that? The wind turned, and there is mist coming up off the moss.");
        var listen = new WaitNode { seconds = 5f };
        var clearSky = Aside(E(), "Twenty years a soldier, and I have never seen the sun simply leave.");
        Link(line2, (across, 0, flash), (across, 0, shake), (across, 0, thunder), (across, 0, wind), (across, 0, listen), (listen, 0, clearSky));

        // ----- The storm breaks with the first attack on the camp, and stays broken after loading -----
        var breaks = Story(moments, "Story - The Storm Breaks");
        QuestDefinition wolves = Existing("DefendCamp");
        var attack = new WhenQuest { quest = wolves, when = WhenQuest.Moment.Starts };
        var gathers = new ChangeWeatherNode { weather = Weather("Storm + Mist"), transitionSeconds = 20f };
        var loaded = new WhenSceneStarts { once = false };
        var settled = new WaitNode { seconds = 0.2f };
        var already = new CheckQuestNode { quest = wolves };
        var underWay = new ChangeWeatherNode { weather = Weather("Storm + Mist"), transitionSeconds = 0f };
        var over = new ChangeWeatherNode { weather = Weather("Storm + Mist"), transitionSeconds = 0f };
        Link(breaks, (attack, 0, gathers), (loaded, 0, settled), (settled, 0, already), (already, 1, underWay), (already, 2, over));

        // ----- Armed before the road -----
        // The moment the player says they are ready. It was at the tree line, after a walk with nothing in hand; the walk
        // (Story - The Road East) waits for the choice. After the errands, so a borrowed bow or practice sword isn't what
        // the player goes in with.
        var pick = Story(moments, "Story - Choose a Weapon");
        var stop = new WhenQuest { quest = road, when = WhenQuest.Moment.Starts, once = false, alsoAfterLoading = true };
        var hasOne = new HasHappenedNode { questEvent = chosen };
        var gather = new WaitNode { seconds = 0.6f };
        var choice = new ChoiceNode
        {
            speaker = S(),
            line = "Then one thing first. Nobody walks that road unarmed. We carry spare arms in the packs. They are old and they are plain, but they will do. Take what suits you.",
            answers = new List<string> { "The sword.", "The axe.", "The spear.", "The club.", "The bow." },
            mustAnswer = true,
        };
        var giveSword = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("Weapons/WornSword"), 1) } };
        var giveAxeNow = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("WoodsmansAxe"), 1) } };
        var giveSpear = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("Weapons/FireHardenedSpear"), 1) } };
        var giveClub = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("Weapons/KnottedClub"), 1) } };
        var giveBowNow = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("CrudeWoodenBow"), 1), new ItemAmount(Item("CrudeStoneArrow"), 30) } };
        Link(pick, (stop, 0, hasOne), (hasOne, 1, gather), (gather, 0, choice),
            (choice, 0, giveSword), (choice, 1, giveAxeNow), (choice, 2, giveSpear), (choice, 3, giveClub), (choice, 4, giveBowNow));
        (GiveItemsNode give, StorySpeaker who, string line)[] after =
        {
            (giveSword, E(), "A soldier's choice. It is old iron, but it holds an edge. Guard well and let them come to you."),
            (giveAxeNow, E(), "The axe. Good. It splits wolves and firewood alike, and it leaves you a hand free."),
            (giveSpear, E(), "Long reach, and it wants both hands. If you ever need a light instead, press L to swap them."),
            (giveClub, R(), "Simple. I respect that. Hit it until it stops, and it leaves you a hand free."),
            (giveBowNow, R(), "A bow! Then there are two of us. Hold the left button to draw, let go to shoot. It wants both hands: if you ever need a light instead, press L to swap them."),
        };
        foreach ((GiveItemsNode give, StorySpeaker who, string line) in after)
        {
            // The line first: the walk sets off when the event is raised, and nobody should be talking then.
            var said = new SayNode { speaker = who, line = line };
            Link(pick, (give, 0, said), (said, 0, new RaiseQuestEventNode { questEvent = chosen }));
        }

        // ----- Wolves on the way in -----
        var ambush = Story(moments, "Story - Rootlings on the Road");
        Transform spot = new GameObject("Ambush Spot").transform;
        spot.SetParent(moments, false);
        spot.position = Ground(-52f, -14f);
        var near = new WhenPlayerNear { place = spot, radius = 10f, once = false };
        var sprung = new HasHappenedNode { questEvent = ambushed };
        var searching = new CheckQuestNode { quest = shelter };
        var remember = new RaiseQuestEventNode { questEvent = ambushed };
        var spawn = new SpawnEnemiesNode { zone = ambushZone };
        var warning = new SayNode { speaker = R(), line = "Off the road, on your left! Rootlings. They were lying in wait, and they don't do that.", afterFightPause = false };
        var cleared = new WhenZoneCleared { zone = ambushZone };
        var afterFight = new SayNode { speaker = S(), line = "Roots do not set ambushes on a road. Something taught them to. Keep moving. We need walls around us before more of them wake." };
        Link(ambush, (near, 0, sprung), (sprung, 1, searching), (searching, 1, remember), (remember, 0, spawn), (remember, 0, warning), (cleared, 0, afterFight));
    }

    // ----- Props -----

    // A wall behind the player once the road has gone, with mist standing in it.
    private static Transform RoadBehind(Transform root)
    {
        var go = new GameObject("The Road Behind");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(Seam - 3f, 30f, 0f);
        go.AddComponent<BoxCollider>().size = new Vector3(1f, 80f, Depth);
        var mist = new GameObject("Mist Where the Road Was");
        mist.transform.SetParent(go.transform, false);
        mist.transform.position = Ground(Seam - 2f, -14f);
        var fog = mist.AddComponent<FogArea>();
        var fogData = new SerializedObject(fog);
        fogData.FindProperty("density").floatValue = 0.35f;
        fogData.FindProperty("radius").floatValue = 34f;
        fogData.FindProperty("height").floatValue = 9f;
        fogData.FindProperty("followsWeather").boolValue = false;
        fogData.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(false);
        return go.transform;
    }

    // Strips a prop's behaviour, a component only once nothing left on its object requires it: removing them in any order
    // logged "Can't remove X because Y depends on it" on every rebuild (and left those for a later pass).
    private static void Strip(GameObject prop, bool children, System.Func<Component, bool> keep)
    {
        for (int pass = 0; pass < 12; pass++)
        {
            bool removed = false;
            foreach (Component part in children ? prop.GetComponentsInChildren<Component>(true) : prop.GetComponents<Component>())
            {
                if (part == null || part is Transform || keep(part) || IsRequired(part)) continue;
                Object.DestroyImmediate(part);
                removed = true;
            }

            if (!removed) break;
        }
    }

    private static bool IsRequired(Component part)
    {
        foreach (Component other in part.GetComponents<Component>())
        {
            if (other == null || other == part) continue;
            foreach (RequireComponent need in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                foreach (System.Type type in new[] { need.m_Type0, need.m_Type1, need.m_Type2 })
                {
                    if (type != null && type.IsInstanceOfType(part) && part.GetComponents(type).Length == 1) return true;
                }
            }
        }

        return false;
    }

    private static Damageable Dummy(Transform root)
    {
        var dummy = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/_Project/Prefabs/Enemies/TrainingDummy.prefab"));
        dummy.name = "Eirik's Dummy";
        dummy.transform.SetParent(root, false);
        // By the morning camp, east of the fire: the lesson is before the road now (2026-11-09).
        dummy.transform.position = Ground(Camp.x + 9f, Camp.y - 3f);
        dummy.transform.rotation = Quaternion.LookRotation(Vector3.back);
        return dummy.GetComponent<Damageable>();
    }

    // The first party's handcart, tipped off the road. Stripped of what makes it something to pull.
    private static Interactable CartProp(Transform root)
    {
        var cart = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/_Project/Prefabs/Transport/Handcart.prefab"));
        PrefabUtility.UnpackPrefabInstance(cart, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        cart.name = "Abandoned Handcart";
        cart.transform.SetParent(root, false);
        cart.transform.position = Ground(Cart.x, Cart.y);
        cart.transform.rotation = Quaternion.Euler(0f, 55f, 16f);
        Strip(cart, true, part => part is MeshFilter || part is MeshRenderer || part is LODGroup);

        Bounds bounds = new Bounds(cart.transform.position, Vector3.one);
        foreach (Renderer renderer in cart.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(renderer.bounds);
        var box = cart.AddComponent<BoxCollider>();
        box.center = cart.transform.InverseTransformPoint(bounds.center);
        box.size = bounds.size * 0.8f;
        return Usable(cart, InteractionAnimation.GatherLow, 1f);
    }

    // A small dormant cousin of the waystones, for whoever wanders round the lake.
    private static Interactable LakeStone(Transform root)
    {
        Transform model = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t => t.name == "Left Waystone");
        GameObject stone = Object.Instantiate(model.gameObject, root);
        stone.name = "Old Stone by the Lake";
        foreach (Transform child in stone.transform) Object.DestroyImmediate(child.gameObject);
        stone.transform.position = Ground(-313f, -6f) + Vector3.up * 1.2f;
        stone.transform.rotation = Quaternion.Euler(4f, 30f, -6f);
        stone.transform.localScale = model.lossyScale * 0.7f;
        return Usable(stone, InteractionAnimation.GatherLow, 0.8f);
    }

    private static Interactable Usable(GameObject go, InteractionAnimation animation, float seconds)
    {
        var interactable = go.GetComponent<Interactable>();
        if (interactable == null) interactable = go.AddComponent<Interactable>();
        interactable.Configure(animation, seconds, 2.5f, false);
        return interactable;
    }

    // Animals running from the forest: for looks only, so nothing in them thinks or can be hit.
    private static Transform[] Runners(Transform root)
    {
        var group = new GameObject("Running Animals").transform;
        group.SetParent(root, false);
        var runners = new Transform[3];
        // East of the cart, out of sight of it: they come down the road and past it (Story - The Road East).
        Vector2[] starts = { new Vector2(-188f, 3f), new Vector2(-183f, -2f), new Vector2(-191f, -12f) };
        for (int i = 0; i < runners.Length; i++)
        {
            var boar = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Prologue + "ExpeditionBoar.prefab"));
            boar.name = "Running Boar " + i;
            boar.transform.SetParent(group, false);
            boar.transform.position = Ground(starts[i].x, starts[i].y);
            foreach (Behaviour behaviour in boar.GetComponentsInChildren<Behaviour>(true))
                if (behaviour is MonsterAI || behaviour is NavMeshAgent) behaviour.enabled = false;
            foreach (Collider collider in boar.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            boar.SetActive(false);
            runners[i] = boar.transform;
        }

        return runners;
    }

    // Something for whoever leaves the road: experience stones and caches at the far ends of the map.
    private static void Secrets(Transform root)
    {
        var group = new GameObject("Off the Road").transform;
        group.SetParent(root, false);
        var stone = Load<GameObject>("Assets/_Project/Prefabs/Interactables/ExperienceStone.prefab");
        foreach (Vector2 at in new[] { new Vector2(-338f, 78f), new Vector2(-296f, -74f), new Vector2(-160f, 62f), new Vector2(58f, 58f), new Vector2(72f, -30f), new Vector2(-58f, 62f) })
        {
            var made = (GameObject)PrefabUtility.InstantiatePrefab(stone);
            made.transform.SetParent(group, false);
            made.transform.position = Ground(at.x, at.y);
        }
    }

    // ----- The beats of the new telling -----

    private static void Beats(Transform moments, Transform root, PrologueCompanion[] companions, Transform sigrid, Transform eirik, Transform runa, Transform route, Transform routeOn,
        Transform treeLine, Damageable dummy, MobSpawnZone practice, Interactable cartProp, Interactable lakeStone, Transform troll, Transform[] runners, Transform behind,
        MobSpawnZone roadWolves)
    {
        StorySpeaker S() => Speaker(sigrid, "Sigrid");
        StorySpeaker E() => Speaker(eirik, "Eirik");
        StorySpeaker R() => Speaker(runa, "Runa");
        SayNode Aside(StorySpeaker who, string line) => new SayNode { speaker = who, line = line, aside = true, afterFightPause = false };
        GameEventDefinition Ev(string name) => Load<GameEventDefinition>(Prologue + "Events/" + name + ".asset");
        QuestDefinition road = Existing("RoadToForest"), lesson = Existing("EirikLesson");

        // ----- The road, with a stop at the cart -----
        var walkStory = Story(moments, "Story - The Road East");
        var setOut = new WhenQuest { quest = road, when = WhenQuest.Moment.Starts, once = false, alsoAfterLoading = true };
        // 3.5 m/s (was 2): the player walks at 5 and was always waiting for them (playtest, 2026-11-09).
        const float Pace = 3.5f, Fade = 8f;
        var walk = new WalkPartyNode { route = route, pace = Pace, waitForPlayer = 22f, waitUntilArrived = true };
        // Nobody sets off until the player is armed (Story - Choose a Weapon). What hangs on the walk's progress starts with
        // the walk: Wait Until The Walk Reaches lets everything through while there is no walk.
        var armed = new WaitForEventNode { questEvent = Ev("WeaponChosen"), orAfterSeconds = 0f };
        Link(walkStory, (setOut, 0, armed), (armed, 0, walk));
        foreach ((float reaches, float hour) in new[] { (0.4f, 11.5f), (0.78f, 14f) })
        {
            var wait = new WaitForWalkNode { reaches = reaches };
            Link(walkStory, (armed, 0, wait), (wait, 0, new ChangeTimeOfDayNode { hour = hour, seconds = Fade, fadeStraightThere = true }));
        }

        foreach ((float reaches, StorySpeaker who, string line) in new[]
        {
            (0.05f, E(), "Steady now. It is a fine morning to walk into something stupid."),
            (0.3f, R(), "A shepherd told me his dogs won't go past the tree line any more. Dogs. I have seen those dogs go after a bear."),
            (0.58f, S(), "Rumours grow in the telling. That is why we go and look, instead of sitting in a longhouse and listening."),
            (0.85f, E(), "Is it past noon already? We have not been walking an hour."),
        })
        {
            var wait = new WaitForWalkNode { reaches = reaches };
            Link(walkStory, (armed, 0, wait), (wait, 0, Aside(who, line)));
        }

        var sighted = Aside(R(), "Hold up. That's Bjarne's handcart, tipped off the road. Have a look; I'll watch the trees.");
        var looked = new WaitForEventNode { questEvent = Ev("CartInspected"), orAfterSeconds = 25f };
        // The first fight, in daylight: boar come down the road past the cart, and what they run from comes after them.
        var running = Aside(R(), "Boar, running flat out, and not from us. Off the road!");
        var behindThem = new WaitNode { seconds = 5f };
        var wolvesCry = Aside(R(), "Wolves behind them! In the open, in daylight. They don't do that. Weapons!");
        var wolvesCome = new SpawnEnemiesNode { zone = roadWolves, waitUntilDefeated = true };
        var moveOn = Aside(S(), "Wolves that hunt by day and do not fear four armed people. Whatever happened to this cart was quick, and it did not end here. We go on.");
        Link(walkStory, (walk, 0, sighted), (sighted, 0, looked), (looked, 0, running), (looked, 0, behindThem), (behindThem, 0, wolvesCry), (behindThem, 0, wolvesCome), (wolvesCome, 0, moveOn));
        // After the wolves the forest's music comes early, over the rest of the road (the user, 2026-11-11). The tree line
        // plays it again (Reactions), which carries on without restarting (SceneAudio.PlayEventMusic).
        var beaten = new RaiseQuestEventNode { questEvent = Ev("RoadWolvesBeaten") };
        var forestMusic = new MusicNode { change = MusicNode.Change.Play, fightsInterrupt = true };
        Link(walkStory, (wolvesCome, 0, beaten), (wolvesCome, 0, forestMusic));
        PlaylistOf(walkStory, forestMusic, Load<AudioClip>(ForestMusic));
        float[] runToZ = { 6f, 1f, -13f };
        for (int i = 0; i < runners.Length; i++)
        {
            Transform runner = runners[i];
            Transform runTo = Mark(root, "Running Boar " + i + " Goes To", -272f, runToZ[i]);
            var show = new SetActiveNode { target = runner.gameObject, active = true };
            var run = new MoveObjectNode { target = runner, destination = runTo, seconds = 8f, animationSpeed = 1.6f, waitUntilArrived = true };
            var gone = new SetActiveNode { target = runner.gameObject, active = false };
            Link(walkStory, (looked, 0, show), (show, 0, run), (run, 0, gone));
        }

        var walkOn = new WalkPartyNode { route = routeOn, pace = Pace, waitForPlayer = 22f, waitUntilArrived = true };
        foreach ((float reaches, float hour) in new[] { (0.15f, 17f), (0.36f, 19f), (0.58f, 20.5f), (0.8f, 22.5f) })
        {
            var wait = new WaitForWalkNode { reaches = reaches };
            Link(walkStory, (moveOn, 0, wait), (wait, 0, new ChangeTimeOfDayNode { hour = hour, seconds = Fade, fadeStraightThere = true }));
        }

        var crossed = new RaiseQuestEventNode { questEvent = Ev("ForestEntered") };
        var stoneWakes = new GlowNode { target = lakeStone.transform, colour = new Color(0.55f, 0.8f, 1f), intensity = 5f, range = 9f, fadeSeconds = 6f };
        Link(walkStory, (moveOn, 0, walkOn), (moveOn, 0, stoneWakes), (walkOn, 0, crossed));
        foreach ((float reaches, StorySpeaker who, string line) in new[]
        {
            (0.05f, R(), "The shadows are wrong. They were behind us when we crossed the stream."),
            (0.16f, S(), "Keep walking."),
            (0.42f, E(), "Sigrid. The sun is going down. It was morning."),
            (0.7f, R(), "No birds. Have you noticed? Not one for a while now."),
            (0.9f, E(), "A whole day gone in a morning's walk. I don't like this, and I have liked very few things."),
        })
        {
            var wait = new WaitForWalkNode { reaches = reaches };
            Link(walkStory, (moveOn, 0, wait), (wait, 0, Aside(who, line)));
        }

        var toBirds = new WaitForWalkNode { reaches = 0.32f };
        var quiet = new SetActiveNode { target = GameObject.Find("Birds"), active = false };
        Link(walkStory, (moveOn, 0, toBirds), (toBirds, 0, quiet));

        var toTorches = new WaitForWalkNode { reaches = 0.55f };
        // The player is given a firefly lantern, not a torch (the user, 2026-11-11): no flame for the mist, both hands free.
        var lightUp = Aside(S(), "I can see that. Light your torches. You, take this: fireflies in a jar. No flame for the rain to put out.");
        var torchesOnce = new HasHappenedNode { questEvent = Ev("TorchesLit") };
        var giveTorch = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("PrimitiveFireflyLantern"), 1) } };
        var torchesNow = new RaiseQuestEventNode { questEvent = Ev("TorchesLit") };
        var lightAgain = new RaiseQuestEventNode { questEvent = Ev("TorchesLit") };
        Link(walkStory, (moveOn, 0, toTorches), (toTorches, 0, lightUp), (lightUp, 0, torchesOnce), (torchesOnce, 1, giveTorch), (giveTorch, 0, torchesNow), (torchesOnce, 0, lightAgain));
        var toDying = new WaitForWalkNode { reaches = 0.4f };
        var dying = new WitherGroundNode { centre = Mark(root, "Where the Grass Dies", -150f, -8f), radius = 26f, seconds = 12f };
        Link(walkStory, (moveOn, 0, toDying), (toDying, 0, dying));

        // ----- What the cart and the stone tell -----
        var cartStory = Story(moments, "Story - The Handcart");
        var usedCart = new WhenUsed { target = cartProp, once = false };
        var seenCart = new HasHappenedNode { questEvent = Ev("CartInspected") };
        var satchel = new SayNode { speaker = new StorySpeaker(), line = "<i>Ingrid's satchel, still strapped on. Her notes stop in the middle of a word. Nobody leaves bandages behind unless they run.</i>" };
        var bandages = new GiveItemsNode { items = { new ItemAmount(Item("Bandage"), 2) } };
        var markCart = new RaiseQuestEventNode { questEvent = Ev("CartInspected") };
        Link(cartStory, (usedCart, 0, seenCart), (seenCart, 1, bandages), (seenCart, 1, satchel), (satchel, 0, markCart));
        var stoneStory = Story(moments, "Story - The Stone by the Lake");
        var usedStone = new WhenUsed { target = lakeStone, once = false };
        var hums = new SayNode { speaker = new StorySpeaker(), line = "<i>The stone is warm, though the morning is not, and it hums under your hand. The marks on it are older than the road.</i>" };
        var markStone = new RaiseQuestEventNode { questEvent = Ev("TouchedLakeStone") };
        Link(stoneStory, (usedStone, 0, hums), (hums, 0, markStone));

        // ----- Nobody goes back -----
        var goneStory = Story(moments, "Story - The Road Behind");
        var inside = new WhenPlayerNear { place = Mark(moments, "Inside the Trees", -86f, -12f), radius = 9f, once = false };
        var wasEntered = new HasHappenedNode { questEvent = Ev("ForestEntered") };
        var goneBefore = new HasHappenedNode { questEvent = Ev("RoadGone") };
        var noticed = new WaitNode { seconds = 3f };
        var nowGone = new RaiseQuestEventNode { questEvent = Ev("RoadGone") };
        var whereRoad = Aside(R(), "Sigrid. Look behind us. Where is the road? It was right there, and now there's only mist.");
        Link(goneStory, (inside, 0, wasEntered), (wasEntered, 0, goneBefore), (goneBefore, 1, noticed), (noticed, 0, nowGone), (nowGone, 0, whereRoad));
        var goneGo = new GameObject("The Road Is Gone - Reaction");
        goneGo.transform.SetParent(root, false);
        var goneListener = goneGo.AddComponent<GameEventListener>();
        var goneData = new SerializedObject(goneListener);
        goneData.FindProperty("listenFor").intValue = (int)ListenFor.QuestEvent;
        goneData.FindProperty("gameEvent").objectReferenceValue = Ev("RoadGone");
        goneData.FindProperty("respondIfAlreadyHappened").boolValue = true;
        SerializedProperty goneActions = goneData.FindProperty("actions");
        goneActions.arraySize = 1;
        goneActions.GetArrayElementAtIndex(0).FindPropertyRelative("kind").intValue = (int)SceneEventActionKind.SetObjectActive;
        goneActions.GetArrayElementAtIndex(0).FindPropertyRelative("target").objectReferenceValue = behind.gameObject;
        goneActions.GetArrayElementAtIndex(0).FindPropertyRelative("active").boolValue = true;
        goneData.ApplyModifiedPropertiesWithoutUndo();

        // ----- Eirik's lesson -----
        var lessonStory = Story(moments, "Story - A Lesson from Eirik");
        var taught = new WhenQuest { quest = lesson, when = WhenQuest.Moment.Starts };
        var giveSword = new GiveItemsNode { equip = true, items = { new ItemAmount(Item("Weapons/PracticeSword"), 1) } };
        Link(lessonStory, (taught, 0, giveSword));
        var struck = new WhenObjectHit { target = dummy, once = false };
        var countHit = new RaiseQuestEventNode { questEvent = Ev("LessonHit") };
        var dodged = new WhenPlayerDoes { move = WhenPlayerDoes.Move.Dodge, once = false };
        var lessonOn = new CheckQuestNode { quest = lesson };
        var countDodge = new RaiseQuestEventNode { questEvent = Ev("LessonDodged") };
        var parried = new WhenPlayerDoes { move = WhenPlayerDoes.Move.Parry, once = false };
        var through = new WhenPlayerDoes { move = WhenPlayerDoes.Move.DodgeThroughABlow, once = false };
        var countBlock = new RaiseQuestEventNode { questEvent = Ev("LessonDefended") };
        var countBlock2 = new RaiseQuestEventNode { questEvent = Ev("LessonDefended") };
        Link(lessonStory, (struck, 0, countHit), (dodged, 0, lessonOn), (lessonOn, 1, countDodge), (parried, 0, countBlock), (through, 0, countBlock2));
        // The duel: Eirik himself, neither can die (whoever is worn to a quarter yields); lose, and he offers another go.
        var firstDodge = new WhenQuestEvent { questEvent = Ev("LessonDodged") };
        var tip = Aside(E(), "Good. Now me. When I wind up, raise your guard just before the blow, or roll through it.");
        var squareUp = new WaitNode { seconds = 3f };
        var duel = new SparNode { companion = eirik.GetComponent<Companion>(), yieldAt = 0.25f };
        var yields = Aside(E(), "Enough, enough! I yield. You'll do.");
        var countDone = new RaiseQuestEventNode { questEvent = Ev("LessonDone") };
        var lost = Aside(E(), "Down you go. Catch your breath; we go again when you're ready.");
        var again = new WaitNode { seconds = 5f };
        Link(lessonStory, (firstDodge, 0, tip), (firstDodge, 0, squareUp), (squareUp, 0, duel), (duel, 0, yields), (duel, 0, countDone),
            (duel, 1, lost), (lost, 0, again), (again, 0, duel));

        // ----- A moment of warmth, until the wolves -----
        // The rest ends 9 s after the player talks to someone, or 50 s after Sigrid's lost name has been said (the first run
        // through in Play had the wolves arrive as that scene ended). Whoever took Eirik's errand is waited for: until the
        // stew is handed in, or two and a half minutes more at most, so an errand left lying can't stop the night.
        var rest = Story(moments, "Story - A Moment of Warmth");
        QuestDefinition warmth = Existing("CatchBreath"), wolves = Existing("DefendCamp"), pot = Existing("BerriesForThePot");
        GameEventDefinition restedEvent = Load<PrologueDefinition>(Prologue + "ForestPrologue.asset").RestedEvent;
        // Each ending checks that the rest is still on, so whichever comes first is the only one.
        StoryNode Ends(StoryNode from, int output)
        {
            var still = new CheckQuestNode { quest = warmth };
            var over = new RaiseQuestEventNode { questEvent = restedEvent };
            Link(rest, (from, output, still), (still, 1, over), (over, 0, new StartQuestNode { quest = wolves }));
            return still;
        }

        var sat = new WhenQuest { quest = warmth, when = WhenQuest.Moment.Starts, once = false, alsoAfterLoading = true };
        // Or after 40 s, should the scene never be said: nothing optional may hold the night up.
        var heardOut = new WaitForEventNode { questEvent = Ev("NameGoneMissing"), orAfterSeconds = 40f };
        var enough = new WaitNode { seconds = 50f };
        var errand = new CheckQuestNode { quest = pot };
        var longer = new WaitNode { seconds = 150f };
        Link(rest, (sat, 0, heardOut), (heardOut, 0, enough), (enough, 0, errand), (errand, 1, longer));
        Ends(errand, 0);
        Ends(errand, 2);
        Ends(longer, 0);
        var fed = new WhenQuest { quest = pot, when = WhenQuest.Moment.IsCompleted, once = false };
        var full = new WaitNode { seconds = 9f };
        Link(rest, (fed, 0, full));
        Ends(full, 0);
        foreach (PrologueCompanion companion in companions)
        {
            if (!companion.gameObject.activeSelf || companion.Resident == null) continue;
            // Talking to them counts as Talk To their resident record (Talkable, through PrologueCompanion): no event per person.
            var spoke = new WhenPlayerDid { deed = QuestObjective.Deed(ObjectiveType.TalkTo, companion.Resident), once = false };
            var resting = new CheckQuestNode { quest = warmth };
            var moment = new WaitNode { seconds = 9f };
            var busy = new CheckQuestNode { quest = pot };
            Link(rest, (spoke, 0, resting), (resting, 1, moment), (moment, 0, busy));
            Ends(busy, 0);
            Ends(busy, 2);
        }

        // ----- A name gone missing -----
        // The one beat of the rest nobody should miss: the forest is already taking them. The portal's "the forest does not
        // kill the lost, it forgets them" then answers something the player saw.
        var name = Story(moments, "Story - A Name Gone Missing");
        // Also after loading, until it has been said to the end: the rest's clock waits for it.
        var sitDown = new WhenQuest { quest = warmth, when = WhenQuest.Moment.Starts, once = false, alsoAfterLoading = true };
        var saidBefore = new HasHappenedNode { questEvent = Ev("NameGoneMissing") };
        var said = new RaiseQuestEventNode { questEvent = Ev("NameGoneMissing") };
        var settleIn = new WaitNode { seconds = 1.5f };
        var sit = new SayNode { speaker = S(), line = "A fire. Sit down, all of you, and get your breath back. Whatever is out there will still be there when we are done." };
        var count = new SayNode { speaker = S(), line = "At first light we find the four who went ahead. Ingrid. Bjarne. Tor. And..." };
        var gap = new SayNode { speaker = new StorySpeaker(), line = "<i>She stops. Her lips move, and nothing comes.</i>" };
        var liv = new SayNode { speaker = R(), line = "Liv." };
        var ofCourse = new SayNode { speaker = S(), line = "Liv. Of course. Eleven years I have known her, and for a moment there was a hole where her name should be." };
        var aWhile = new WaitNode { seconds = 3f };
        var mine = Aside(E(), "It is this place. At the tree line I had to think for my own sister's name. I did not like to say.");
        var hungry = new WaitNode { seconds = 5f };
        var bread = Aside(E(), "And there is only dry bread, unless someone fancies a walk. Ask me if you do.");
        Link(name, (sitDown, 0, saidBefore), (saidBefore, 1, settleIn), (settleIn, 0, sit), (sit, 0, count), (count, 0, gap), (gap, 0, liv), (liv, 0, ofCourse),
            (ofCourse, 0, said), (said, 0, aWhile), (aWhile, 0, mine), (mine, 0, hungry), (hungry, 0, bread));

        // ----- They remember -----
        var memory = Story(moments, "Story - They Remember");
        var sitting = new WhenQuest { quest = warmth, when = WhenQuest.Moment.Starts };
        (QuestDefinition quest, StorySpeaker who, string line)[] errands =
        {
            (Existing("FishForBreakfast"), E(), "That trout this morning. I keep thinking about that trout."),
            (Existing("HuntForThePot"), R(), "You shot straight this morning. It may matter before the night is out."),
        };
        foreach ((QuestDefinition quest, StorySpeaker who, string line) in errands)
        {
            var did = new CheckQuestNode { quest = quest };
            var wait = new WaitNode { seconds = quest == errands[0].quest ? 3f : 12f };
            Link(memory, (sitting, 0, wait), (wait, 0, did), (did, 2, Aside(who, line)));
        }

        foreach ((string gameEvent, StorySpeaker who, string line) in new[]
        {
            ("CameForCoin", R(), "Still think the coin is worth it?"),
            ("CameForKin", E(), "Whoever you came for: we have not stopped looking. Remember that."),
            ("CameForTruth", S(), "You wanted to know if the stories were true. How are you finding it?"),
        })
        {
            var came = new HasHappenedNode { questEvent = Ev(gameEvent) };
            var wait = new WaitNode { seconds = 22f };
            Link(memory, (sitting, 0, wait), (wait, 0, came), (came, 0, Aside(who, line)));
        }

        var fuel = new WhenQuestEvent { questEvent = Load<PrologueDefinition>(Prologue + "ForestPrologue.asset").ShelterEvent };
        var afterSigrid = new WaitNode { seconds = 7f };
        var cutWood = new CheckQuestNode { quest = Existing("WoodForTheRoad") };
        Link(memory, (fuel, 0, afterSigrid), (afterSigrid, 0, cutWood), (cutWood, 2, Aside(E(), "And the kindling you cut this morning is dry in your pack. We only need stones.")));
        var cooking = new WhenQuest { quest = pot, when = WhenQuest.Moment.Starts };
        var caught = new CheckQuestNode { quest = Existing("FishForBreakfast") };
        var fewer = new QuestProgressNode { quest = pot, objective = 1, amount = 3 };
        Link(memory, (cooking, 0, caught), (caught, 2, fewer), (fewer, 0, Aside(E(), "I salted what was left of your fish, so we need fewer berries. Two handfuls will do.")));

        // ----- Sigrid at the stones -----
        // Her hand first, and nothing: the stones wake for the player's. "Why me?" is asked here and answered much later.
        var stones = Story(moments, "Story - What Sigrid Knows");
        var touching = new WhenQuestEvent { questEvent = Load<PrologueDefinition>(Prologue + "ForestPrologue.asset").PortalReachedEvent };
        var pause = new WaitNode { seconds = 1.5f };
        var herHand = new SayNode { speaker = new StorySpeaker(), line = "<i>Sigrid lays her palm flat on the centre stone and holds it there. Nothing. The stone stays cold.</i>" };
        var notForMe = new SayNode { speaker = S(), line = "It will not wake for me. It never has. You, then. Put your hand on the centre stone." };
        var asked = new HasHappenedNode { questEvent = Ev("PressedSigrid") };
        var owns = new SayNode { speaker = S(), line = "You asked me by the fire, so here it is. I have seen stones like these wake once. It took everyone who stood too near, and it put them somewhere else. Ingrid knew that when she came. Now you do." };
        var silent = new SayNode { speaker = S(), line = "Whatever you see, do not let go of the stone. There are things I should have told you, and there is no time left to tell them." };
        var sawOne = new HasHappenedNode { questEvent = Ev("TouchedLakeStone") };
        var same = new SayNode { speaker = new StorySpeaker(), line = "<i>The same marks as the stone by the lake, and the same warmth. It was already awake this morning.</i>" };
        Link(stones, (touching, 0, pause), (pause, 0, herHand), (herHand, 0, notForMe), (notForMe, 0, asked), (asked, 0, owns), (asked, 1, silent),
            (owns, 0, sawOne), (silent, 0, sawOne), (sawOne, 0, same));

        // ----- The old troll comes for the stones -----
        var returns = Story(moments, "Story - The Old Troll Returns");
        var hold = new WhenQuest { quest = Existing("HoldPortal"), when = WhenQuest.Moment.Starts, once = false, alsoAfterLoading = true };
        var coming = new WaitNode { seconds = 40f };
        Transform trollFrom = Mark(moments, "Old Troll Comes From", 3f, 46f);
        Transform trollTo = Mark(moments, "Old Troll Reaches the Stones", 2.5f, 15f);
        var appear = new SetActiveNode { target = troll.gameObject, active = true };
        var place = new MoveObjectNode { target = troll, destination = trollFrom, seconds = 0f, waitUntilArrived = true };
        var advance = new MoveObjectNode { target = troll, destination = trollTo, seconds = 19f, animationSpeed = 0.7f, waitUntilArrived = true };
        var lit = new GlowNode { target = troll, colour = new Color(0.55f, 0.65f, 0.85f), intensity = 9f, range = 22f, fadeSeconds = 2f };
        var spotted = new SayNode { speaker = R(), line = "It followed us! The old one, coming through the trees, right for the stones!", afterFightPause = false };
        var tremble = new ShakeCameraNode { strength = 0.5f };
        var step = new PlaySoundNode { sound = Load<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/TrollStep.asset"), at = troll };
        var hurry = new WaitNode { seconds = 7f };
        var nearly = new SayNode { speaker = S(), line = "Almost! Hold it off, just a little longer!", afterFightPause = false };
        // A puppet while it is walked to the stones: its own mind would turn it on whoever is nearest instead (it was set
        // in the scene by hand once, and a rebuild lost it; it lives here now).
        var puppet = new CreatureHuntsNode { creature = troll.GetComponent<MonsterAI>(), hunt = false };
        Link(returns, (hold, 0, coming), (coming, 0, appear), (appear, 0, puppet), (puppet, 0, place), (place, 0, advance), (puppet, 0, lit), (puppet, 0, spotted), (puppet, 0, tremble), (puppet, 0, step), (puppet, 0, hurry), (hurry, 0, nearly));
        var look = new FocusCameraNode { target = Mark(moments, "Between the Troll and the Stones", 2f, 12f), seconds = 4.5f, revealRadius = 22f };
        // The camera turns while it is still walking; the arm comes down as the minute runs out (40 + 19 + 0.8 + a moment).
        var turnToLook = new WaitNode { seconds = 15.5f };
        var swing = new InvokeEventNode();
        Animator trollBody = troll.GetComponentInChildren<Animator>(true);
        if (trollBody != null) UnityEditor.Events.UnityEventTools.AddStringPersistentListener(swing.response, trollBody.SetTrigger, "Attack");
        var blow = new WaitNode { seconds = 0.8f };
        var crash = new ShakeCameraNode { strength = 1f };
        var flash = new LightningNode();
        var boom = new PlaySoundNode { sound = Load<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Thunder.asset"), at = troll };
        Link(returns, (puppet, 0, turnToLook), (turnToLook, 0, look), (advance, 0, swing), (advance, 0, blow), (blow, 0, crash), (blow, 0, flash), (blow, 0, boom));
    }

    /// <summary>The portal remembers why the player came (`--entry BuildPrologueMeadow.HomeEcho`). In SafeHaven, under Home Moments: it speaks once the Waking is over.</summary>
    public static string HomeEcho()
    {
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity");
        Transform home = GameObject.Find("Home Moments").transform;
        Transform old = home.Find("Story - Why You Came");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        StoryScript waking = home.Find("Story - The Waking").GetComponent<StoryScript>();
        RaiseQuestEventNode end = waking.Nodes.OfType<RaiseQuestEventNode>().Last();
        SayNode voice = waking.Nodes.OfType<SayNode>().First();
        StorySpeaker Portal() => new StorySpeaker { player = voice.speaker.player, name = voice.speaker.name, model = voice.speaker.model, portrait = voice.speaker.portrait, voice = voice.speaker.voice, voiceFrom = voice.speaker.voiceFrom };
        StoryScript story = Story(home, "Story - Why You Came");
        var talked = new WhenQuestEvent { questEvent = end.questEvent };
        var wait = new WaitNode { seconds = 2f };
        Link(story, (talked, 0, wait));
        foreach ((string gameEvent, string line) in new[]
        {
            ("CameForCoin", "<i>You came for coin. There is none here. Stay anyway. They need you to.</i>"),
            ("CameForKin", "<i>You came looking for someone. Now there are others looking for you. Let them find a light.</i>"),
            ("CameForTruth", "<i>You came to see whether the stories were true. Now you are one of them.</i>"),
        })
        {
            var came = new HasHappenedNode { questEvent = Load<GameEventDefinition>(Prologue + "Events/" + gameEvent + ".asset") };
            Link(story, (wait, 0, came), (came, 0, new SayNode { speaker = Portal(), line = line }));
        }

        SaveableEntityTools.AssignSceneIds(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"echo after {end.questEvent.name}; problems: {string.Join("; ", story.FindProblems())}; dirty {scene.isDirty}";
    }

    private const string ForestMusic = "Assets/_Project/Audio/Misty woods - Eerie raid.mp3";

    private static void PlaylistOf(StoryScript story, MusicNode node, AudioClip clip)
    {
        var data = new SerializedObject(story);
        SerializedProperty nodes = data.FindProperty("nodes");
        for (int i = 0; i < nodes.arraySize; i++)
        {
            SerializedProperty entry = nodes.GetArrayElementAtIndex(i);
            if (entry.managedReferenceValue != node) continue;
            SerializedProperty tracks = entry.FindPropertyRelative("playlist").FindPropertyRelative("tracks");
            tracks.arraySize = 1;
            tracks.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
            tracks.GetArrayElementAtIndex(0).FindPropertyRelative("volume").floatValue = 1f;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // Keeps what saved games remember of each story (its save id and triggers): StoryBuilder, 2026-11-11.
    private static StoryScript Story(Transform root, string name) => StoryBuilder.Fresh(root, name);

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

    private static StorySpeaker Speaker(Transform model, string name) => new StorySpeaker { name = name, model = model };

    private static Vector3 Ground(float x, float z)
    {
        var guess = new Vector3(x, 0f, z);
        Terrain terrain = Terrains.Under(guess);
        guess.y = terrain.SampleHeight(guess) + terrain.transform.position.y;
        return NavMesh.SamplePosition(guess, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : guess;
    }

    private static Scene OpenPrologue(out string refused)
    {
        refused = null;
        Scene open = SceneManager.GetActiveScene();
        if (open.isDirty)
        {
            refused = "the open scene has unsaved changes";
            return open;
        }

        return open.path == PrologueScene ? open : EditorSceneManager.OpenScene(PrologueScene);
    }

    private static VolumeProfile Profile(string name) => Load<VolumeProfile>("Assets/_Project/Settings/Rendering/Volumes/" + name + ".asset");
    private static WeatherDefinition Weather(string name) => Load<WeatherDefinition>("Assets/_Project/Data/Environment/Weather/" + name + ".asset");
    private static ItemDefinition Item(string file) => Load<ItemDefinition>(Items + file + ".asset");
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
