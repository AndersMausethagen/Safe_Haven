#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Who comes home, and when (`unity command script run --file AgentScripts/BuildRescueOrder.cs --entry BuildRescueOrder.Apply`).
/// See Docs/RescueOrder.md. Everyone is found on a raid, in an order where each answers the problem the player has just met,
/// and their rescue opens the building they work at: a job teaches its workplace, and a rescued person teaches the player the
/// workplaces of every job they can do. Each rumour waits for the person before (Raid Rumour › After Home); a quest per
/// rescue says what problem they answer, and one after it gives them work. Run it last: after BuildRaidReasons,
/// BuildFoundation, BuildHavenProgression, BuildHomeStory and BuildOpeningOrder, whose older settings it replaces. Safe to run again.
/// </summary>
public static class BuildRescueOrder
{
    private const string Data = "Assets/_Project/Data/";
    private const string Buildings = Data + "Buildings/";
    private const string Quests = Data + "Quests/";
    private const string Rumours = Data + "Raids/Rumours/";
    private const string Events = Rumours + "Events/";
    private const string Sites = "Assets/_Project/Prefabs/Raids/Events/Sites/";
    private const string Traveler = "Assets/_Project/Prefabs/Characters/StrandedTraveler.prefab";
    private const string Raid = Data + "Raids/MistyWoods.asset";
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string Expedition = "The Lost Expedition";
    private const string Work = "Hands at Home";

    private delegate void Outcome(SerializedProperty list);

    private sealed class Choice
    {
        public string Label, Detail, Result;
        public Outcome[] Outcomes = new Outcome[0];
    }

    /// <summary>One person in the order: who, the job and workplace their work quest asks for, and the words.</summary>
    private sealed class Person
    {
        public string Name, Resident, Job, Workplace;
        public string RescueTitle, RescueText, Whisper;
        public string WorkFile, WorkTitle, WorkText;
    }

    // The order of Docs/RescueOrder.md. Ingrid is the story's first rescue (AStrangerInTheMist) and is not a rumour. Yrsa
    // comes fifth, after Eirik (the design review's 8.10, 2026-11-14: food and the Kitchen felt too late as tenth); her
    // meals need meat, fish, mushrooms and berries from the woods, not the farm that comes after her.
    private static readonly Person[] Order =
    {
        new Person
        {
            Name = "Bjarne", Resident = "Prologue/Residents/Bjarne", Job = "Smith", Workplace = "Forge",
            RescueTitle = "The Smith in the Woods",
            RescueText = "Every raid sends your club and tools home more broken, and the Workshop can only patch them. Ingrid heard a hammer in the deep woods at dusk: Bjarne, the expedition's smith. Look for him in the portal's briefing, find him and bring him home.",
            Whisper = "<i>Listen. A hammer, far off in the deep woods, slow and uneven. One of yours. Ask me what the woods hold before you go, and I will tell you where.</i>",
            WorkFile = "TheSmith", WorkTitle = "A Smith's Hearth",
            WorkText = "Bjarne mends what the woods break, at half the cost, and smelts the copper you bring him. Give him the Smith's work, and raise the Smithy he showed you ({key:Build}). At Tier II it makes copper tools.",
        },
        new Person
        {
            Name = "Tor", Resident = "Prologue/Residents/Tor", Job = "Carpenter", Workplace = "Sawbench",
            RescueTitle = "Planks, Not Logs",
            RescueText = "Logs are heavy and good for little: shields, walls and a dry shed all want planks, and a hatchet won't make them. Bjarne says Tor, the expedition's carpenter, was thrown into the deep woods with a broken leg. Listen for a saw where nobody saws, and bring him home.",
            Whisper = "<i>A saw, out where nobody saws. Your carpenter is hurt, but he is still measuring.</i>",
            WorkFile = "WoodcuttersLodge", WorkTitle = "The Woodcutter's Lodge",
            WorkText = "Tor turns logs into planks while you are away. Give him the Carpenter's work and build the Woodcutter's Lodge he showed you, near the trees ({key:Build}). Palisades, a storage shed and a watch post all start with a plank, and at Tier II his lodge plans a Wainwright's Yard.",
        },
        new Person
        {
            Name = "Eirik", Resident = "Prologue/Residents/Eirik", Job = "Warden", Workplace = "WatchPost",
            RescueTitle = "Someone Holding the Line",
            RescueText = "Something has started testing the ward, and walls alone won't hold it. Someone has been holding a clearing against wolves for days: it can only be Eirik, who taught you to fight. Bring him home.",
            Whisper = "<i>Something tests my ward. Out there, someone else is holding a clearing against the wolves. Bring him to hold mine.</i>",
            WorkFile = "TheWarden", WorkTitle = "The Watch",
            WorkText = "Eirik keeps the ward: in a breach it holds half as long again. Give him the Warden's work, and build a Watch Post where he can see the old path ({key:Build}).",
        },
        new Person
        {
            Name = "Yrsa", Resident = "Settlement/Yrsa", Job = "Cook", Workplace = "Kitchen",
            RescueTitle = "A Fire in the Woods",
            RescueText = "Berries and a stew over the campfire keep you alive, and that is all they do. Someone has been cooking in the deep woods, well enough that the wolves come for the smell. Find the cook and bring her home.",
            Whisper = "<i>Someone is cooking out there. The wolves can smell it. So can I.</i>",
            WorkFile = "TheCook", WorkTitle = "Show Me Your Kitchen",
            WorkText = "Yrsa cooked for a jarl once, and tells you so. Give her the Cook's work and build a Kitchen ({key:Build}): her meals last, and send you out stronger.",
        },
        new Person
        {
            Name = "Gunnar", Resident = "Settlement/Gunnar", Job = "Hunter", Workplace = "TanningRack",
            RescueTitle = "The Hunter's Pit",
            RescueText = "Hides pile up in the stash and rot there, and copper is the last armour you can make without leather to line it. A hunter has been setting snares in the deep woods, alone by choice. Find him, and bring him home.",
            Whisper = "<i>A snare line, a mile of it, and at the end a man who did not mean to be caught.</i>",
            WorkFile = "TheHunter", WorkTitle = "Hides and Hooks",
            WorkText = "Gunnar cures hides into leather. Give him the Hunter's work and build him a Hunter's Lodge ({key:Build}). He knows the water as well as the woods: as a Fisher he runs a Dock.",
        },
        new Person
        {
            Name = "Asa", Resident = "Settlement/Asa", Job = "Farmer", Workplace = "FarmPlot",
            RescueTitle = "Seed from the Ashes",
            RescueText = "Berries and game don't stretch to a haven full of people, and long raids leave you hungry. A farmer was seen at a burned farmstead in the deep woods, still guarding her seed. Bring her home.",
            Whisper = "<i>Smoke from a farmstead that burned long ago. Someone kept the seed.</i>",
            WorkFile = "TheFarmer", WorkTitle = "Good Dirt",
            WorkText = "Asa grows what you can't gather. Give her the Farmer's work and lay out a Farm Plot ({key:Build}). She weaves too: as a Weaver she runs a Tailor's Shop, and iron gear will want its linen.",
        },
        new Person
        {
            Name = "Runa", Resident = "Prologue/Residents/Runa", Job = "Tracker", Workplace = null,
            RescueTitle = "Marks in the Dark",
            RescueText = "Every raid is a guess: you go through and take what the woods give. Runa, the expedition's tracker, would know where to look. Marks cut on the trees and seen only at night may be hers.",
            Whisper = "<i>Marks on the trees that only the dark shows. Go through at night.</i>",
            WorkFile = "TheTracker", WorkTitle = "Eyes in the Woods",
            WorkText = "Runa reads the woods before you go. Give her the Tracker's work, and every briefing names one more thing that's out there.",
        },
        new Person
        {
            Name = "Liv", Resident = "Prologue/Residents/Liv", Job = "RuneScholar", Workplace = null,
            RescueTitle = "The Humming Stones",
            RescueText = "The deep woods take more than steel. Liv, the expedition's rune scholar, could cut runes for a raid, and a ring of old stones is said to be humming with someone inside it.",
            Whisper = "<i>Old stones are humming in the deep woods. Someone is inside the ring, writing.</i>",
            WorkFile = "TheRuneScholar", WorkTitle = "Runes for the Road",
            WorkText = "Liv knows runes nobody else does. Give her the Rune Scholar's work, and she teaches you to cut them at the Cauldron.",
        },
        new Person
        {
            Name = "Hild", Resident = "Settlement/Hild", Job = "Mason", Workplace = "Cottage",
            RescueTitle = "A Wall Round Nothing",
            RescueText = "Palisades burn and huts are cramped: the haven has outgrown wood. A mason has been seen in the deep woods, building walls round nothing at all. Bring her home.",
            Whisper = "<i>A wall went up overnight out there, round nothing. Whoever built it will want to build a better one here.</i>",
            WorkFile = "TheMason", WorkTitle = "Stone on Stone",
            WorkText = "Hild builds in stone. Give her the Mason's work: she shows you a Cottage, four beds and a proper hearth (Workshop Tier II), and Stone Walls at Tier III.",
        },
        new Person
        {
            Name = "Sigrid", Resident = "Prologue/Residents/Sigrid", Job = "Leader", Workplace = "HavenBanner",
            RescueTitle = "The Horn at Dusk",
            RescueText = "Everyone else is home. Only Sigrid, who led the expedition and whose voice you heard in the stones, is still out there. A horn has been heard at dusk, calling the expedition's signal.",
            Whisper = "<i>A horn at dusk, the expedition's signal. She is the last of them. Bring her home, little one.</i>",
            WorkFile = "TheLeader", WorkTitle = "Under One Banner",
            WorkText = "Sigrid keeps the portal's watch: with her as Leader every raid has more time. Raise a Haven Banner where everyone can see it ({key:Build}).",
        },
    };

    // What each job teaches: its workplace, so whoever does it opens it. The Cauldron is Ingrid's, for healing, from the start.
    private static readonly (string job, string[] buildings)[] Teaching =
    {
        ("Alchemist", new[] { "PurificationAltar", "Cauldron" }),
        ("Smith", new[] { "Forge" }),
        ("Carpenter", new[] { "Sawbench", "WainwrightsYard" }),
        ("Warden", new[] { "WatchPost" }),
        ("Hunter", new[] { "TanningRack" }),
        ("Fisher", new[] { "Dock" }),
        ("Farmer", new[] { "FarmPlot" }),
        ("Weaver", new[] { "Loom" }),
        ("Cook", new[] { "Kitchen" }),
        ("RuneScholar", new string[0]), // Liv works at the Cauldron, which Ingrid, always found first, already opened.
        ("Leader", new[] { "HavenBanner" }),
        ("Mason", new[] { "Cottage", "StoneWall" }),
    };

    /// <summary>Only the rescue and work quests (their objectives), without the rumours, people or the Haven scene.</summary>
    public static string QuestsOnly()
    {
        string quests = QuestsInOrder();
        AssetDatabase.SaveAssets();
        return quests;
    }

    public static string Apply()
    {
        string buildings = TaughtBuildings();
        string rumours = RumoursInOrder();
        People();
        string quests = QuestsInOrder();

        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        database.CollectAll();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        return $"{buildings} | {rumours} | {quests} | {ApplyHome()}";
    }

    /// <summary>The Haven scene's part: the portal's lines and the horn at dusk. Skipped while an open scene has unsaved work.</summary>
    public static string ApplyHome()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty) return "home skipped: " + SceneManager.GetSceneAt(i).name + " has unsaved changes (run ApplyHome once it is saved)";
        }

        return Home();
    }

    // ----- Buildings: a workplace is taught by whoever works there -----

    private static string TaughtBuildings()
    {
        var taught = new HashSet<string>();
        foreach ((string job, string[] buildings) in Teaching)
        {
            Edit(Job(job), so =>
            {
                SerializedProperty list = so.FindProperty("teachesBuildings");
                list.arraySize = buildings.Length;
                for (int i = 0; i < buildings.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = Building(buildings[i]);
                }
            });
            taught.UnionWith(buildings);
        }

        foreach (string name in taught)
        {
            Edit(Building(name), so => so.FindProperty("knownFromStart").boolValue = false);
        }

        // Bjarne comes before Tor now, so the Smithy needs only the Workshop; Ingrid's Cauldron likewise. The Wainwright's Yard
        // is Tor's at Lodge II: hauling is the problem once copper ore and planks come home in loads.
        Requires("Forge", ("ConstructionTable", 1));
        Requires("Cauldron", ("ConstructionTable", 1));
        Requires("WainwrightsYard", ("Sawbench", 2));
        return taught.Count + " workplaces taught by people";
    }

    private static void Requires(string building, params (string building, int tier)[] needs) => Edit(Building(building), so =>
    {
        SerializedProperty list = so.FindProperty("requires");
        list.arraySize = needs.Length;
        for (int i = 0; i < needs.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("building").objectReferenceValue = Building(needs[i].building);
            list.GetArrayElementAtIndex(i).FindPropertyRelative("tier").intValue = needs[i].tier;
        }
    });

    // ----- Rumours: each person waits for the one before -----

    private static string RumoursInOrder()
    {
        var traveler = AssetDatabase.LoadAssetAtPath<GameObject>(Traveler).GetComponent<RescuableTraveler>();
        Outcome Rescue(string who) => list => Add(list, new RescueResidentOutcome(), e =>
        {
            e.FindPropertyRelative("resident").objectReferenceValue = Resident(who);
            e.FindPropertyRelative("traveler").objectReferenceValue = traveler;
        });

        // The four who were never on the expedition get sites of their own.
        NewRumour("Gunnar", "A snare line runs for a mile through the deep woods, and ends at a pit nobody remembers digging.",
            Event("GunnarsPit", "The Hunter's Pit", Site("Gunnars Pit", Pit),
                "A pit trap, well made, with a hunter at the bottom of it and a dead boar across him.",
                new Choice { Label = "Cut a pole and haul him out", Detail = "Half a minute.", Result = "\"Mind the stakes,\" he says, from somewhere under the boar.", Outcomes = new[] { Time(-30f), Rescue("Settlement/Gunnar") } }));
        NewRumour("Asa", "Smoke has been seen over a farmstead in the deep woods that burned down years ago.",
            Event("AsasFarmstead", "The Burned Farmstead", Site("Asas Farmstead", Farmstead),
                "Black timbers and a cellar door. A woman sits on the door with a sack of seed in her lap, and won't get up.",
                new Choice { Label = "Carry the seed for her", Detail = "It's heavy: less stamina for a few minutes.", Result = "\"Mind it. That's the last of my village.\"", Outcomes = new[] { Boon("Seed Sack", 180f, new StatBonus(StatType.MaxStamina, -15f)), Rescue("Settlement/Asa") } }));
        NewRumour("Yrsa", "Someone is cooking in the deep woods. The smell carries for a mile, and so far only the wolves have followed it.",
            Event("YrsasFire", "A Fire in the Woods", Site("Yrsas Fire", Cookfire),
                "A cookfire in the open, a pot on it, and a woman stirring it with a wolf-spear across her knees.",
                new Choice { Label = "Eat first", Detail = "It's very good. The wolves think so too.", Result = "Best thing you've eaten since the stones broke.", Outcomes = new[] { Boon("Hot Meal", 300f, new StatBonus(StatType.MaxStamina, 20f)), Spawn("WolfPack", 1, 14f), Rescue("Settlement/Yrsa") } },
                new Choice { Label = "Put the fire out and go", Detail = "Quiet. She will tell you what she thinks of it all the way home.", Result = "\"A good stew, wasted.\"", Outcomes = new[] { Rescue("Settlement/Yrsa") } }));
        NewRumour("Hild", "A wall of dry stone has gone up overnight in the deep woods, round nothing at all.",
            Event("HildsWall", "A Wall Round Nothing", Site("Hilds Wall", Wall),
                "A ring of dry-stone wall, chest high and dead level. From inside it someone tells you to mind the coping.",
                new Choice { Label = "Help her finish it, then climb out", Detail = "A minute and a half: she won't leave it half done.", Result = "\"There. Now it'll stand.\"", Outcomes = new[] { Time(-90f), Rescue("Settlement/Hild") } },
                new Choice { Label = "Pull a stretch down", Detail = "Quick, and loud, and she'll never forgive you.", Result = "She climbs out over the rubble without a word.", Outcomes = new[] { Noise(35f), Rescue("Settlement/Hild") } }));

        var pool = new List<RaidRumourDefinition>();
        string before = "Settlement/Ingrid";
        foreach (Person person in Order)
        {
            var rumour = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(Rumours + person.Name + ".asset");
            if (rumour == null) throw new InvalidOperationException("No rumour for " + person.Name);
            Edit(rumour, so =>
            {
                so.FindProperty("seeks").objectReferenceValue = Resident(person.Resident);
                so.FindProperty("afterHome").objectReferenceValue = Resident(before);
                so.FindProperty("needsBuilding").objectReferenceValue = null;
                so.FindProperty("urgent").boolValue = true;
            });
            pool.Add(rumour);
            before = person.Resident;
        }

        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(Raid);
        int added = 0;
        Edit(raid, so =>
        {
            SerializedProperty systems = so.FindProperty("systems");
            for (int i = 0; i < systems.arraySize; i++)
            {
                if (!(systems.GetArrayElementAtIndex(i).managedReferenceValue is RaidRumoursSystem)) continue;
                SerializedProperty list = systems.GetArrayElementAtIndex(i).FindPropertyRelative("pool");
                foreach (RaidRumourDefinition rumour in pool)
                {
                    bool has = false;
                    for (int j = 0; j < list.arraySize; j++) has |= list.GetArrayElementAtIndex(j).objectReferenceValue == rumour;
                    if (has) continue;
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = rumour;
                    added++;
                }
            }
        });
        return $"{pool.Count} people in order, {added} rumours added to the Misty Woods";
    }

    private static void NewRumour(string file, string line, RaidEventDefinition site)
    {
        string path = Rumours + file + ".asset";
        var rumour = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(path);
        if (rumour == null)
        {
            rumour = ScriptableObject.CreateInstance<RaidRumourDefinition>();
            AssetDatabase.CreateAsset(rumour, path);
        }

        Edit(rumour, so =>
        {
            so.FindProperty("id").stringValue = "rumour." + file.ToLowerInvariant();
            so.FindProperty("briefing").stringValue = line;
            so.FindProperty("site").objectReferenceValue = site;
            so.FindProperty("weight").floatValue = 2f;
            so.FindProperty("afterExtractions").intValue = 1;
        });
    }

    private static RaidEventDefinition Event(string file, string label, GameObject site, string siteText, params Choice[] choices)
    {
        string path = Events + file + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<RaidEventDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        Edit(definition, so =>
        {
            so.FindProperty("id").stringValue = "rumour." + file.ToLowerInvariant();
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("weight").floatValue = 0f; // Never drawn at random: a rumour places it.
            so.FindProperty("sitePrefab").objectReferenceValue = site;
            so.FindProperty("siteText").stringValue = siteText;
            SerializedProperty list = so.FindProperty("choices");
            list.ClearArray();
            foreach (Choice choice in choices)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("label").stringValue = choice.Label;
                entry.FindPropertyRelative("detail").stringValue = choice.Detail ?? "";
                entry.FindPropertyRelative("result").stringValue = choice.Result ?? "";
                entry.FindPropertyRelative("cost").ClearArray();
                SerializedProperty outcomes = entry.FindPropertyRelative("outcomes");
                outcomes.ClearArray();
                foreach (Outcome outcome in choice.Outcomes) outcome(outcomes);
            }
        });
        return definition;
    }

    private static void Add(SerializedProperty list, RaidEventOutcome outcome, Action<SerializedProperty> fill = null)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.managedReferenceValue = outcome;
        fill?.Invoke(entry);
    }

    private static Outcome Time(float seconds) => list => Add(list, new ChangeTimeOutcome(), e => e.FindPropertyRelative("seconds").floatValue = seconds);
    private static Outcome Noise(float radius) => list => Add(list, new MakeNoiseOutcome(), e => e.FindPropertyRelative("radius").floatValue = radius);

    private static Outcome Boon(string name, float seconds, StatBonus bonus) => list => Add(list, new GiveBoonOutcome(), e =>
    {
        e.FindPropertyRelative("name").stringValue = name;
        e.FindPropertyRelative("seconds").floatValue = seconds;
        SerializedProperty stats = e.FindPropertyRelative("bonuses");
        stats.arraySize = 1;
        stats.GetArrayElementAtIndex(0).FindPropertyRelative("stat").intValue = (int)bonus.stat;
        stats.GetArrayElementAtIndex(0).FindPropertyRelative("op").intValue = (int)bonus.op;
        stats.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue = bonus.value;
    });

    private static Outcome Spawn(string prefab, int count, float radius) => list => Add(list, new SpawnCreaturesOutcome(), e =>
    {
        SerializedProperty rows = e.FindPropertyRelative("prefabs");
        rows.arraySize = 1;
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/" + prefab + ".prefab");
        rows.GetArrayElementAtIndex(0).objectReferenceValue = enemy != null ? enemy : throw new InvalidOperationException("No enemy " + prefab);
        e.FindPropertyRelative("count").intValue = count;
        e.FindPropertyRelative("radius").floatValue = radius;
        e.FindPropertyRelative("hunt").boolValue = true;
    });

    // ----- Sites: Tor's shelter (collider, site, Interactable, beacon) with a placeholder model of their own -----

    private static GameObject Site(string name, Action<Transform> shape)
    {
        string path = Sites + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            var tor = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(Events + "TorsShelter.asset");
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(tor.SitePrefab), path)) throw new InvalidOperationException("Could not copy a site to " + path);
        }

        // A site kit-built by Tools/Blender/raid_sites.py (BuildStructureModels.RaidSites) keeps its look.
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Art/Raids/Sites/{name}/{name}.fbx") != null)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform model = root.transform.Find("Model");
            for (int i = model.childCount - 1; i >= 0; i--) Object.DestroyImmediate(model.GetChild(i).gameObject);
            shape(model);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Haven/Base " + name + ".mat");

    private static void Pit(Transform model)
    {
        Piece(model, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(2.6f, 0.02f, 2.6f), Vector3.zero, Mat("Stone"));
        for (int i = 0; i < 7; i++)
        {
            float angle = i * Mathf.PI * 2f / 7f;
            var at = new Vector3(Mathf.Cos(angle) * 1.4f, 0.4f, Mathf.Sin(angle) * 1.4f);
            Piece(model, PrimitiveType.Cylinder, at, new Vector3(0.08f, 0.45f, 0.08f), new Vector3(Mathf.Sin(angle) * 20f, 0f, -Mathf.Cos(angle) * 20f), Mat("Timber"));
        }

        Piece(model, PrimitiveType.Sphere, new Vector3(0.2f, 0.25f, 0.1f), new Vector3(1.1f, 0.5f, 0.6f), new Vector3(0f, 25f, 0f), Mat("Bone"));
    }

    private static void Farmstead(Transform model)
    {
        Piece(model, PrimitiveType.Cube, new Vector3(-1.2f, 0.6f, 0f), new Vector3(0.25f, 1.2f, 0.25f), Vector3.zero, Mat("Ember"));
        Piece(model, PrimitiveType.Cube, new Vector3(1.2f, 0.4f, -0.3f), new Vector3(0.25f, 0.8f, 0.25f), new Vector3(0f, 0f, 12f), Mat("Ember"));
        Piece(model, PrimitiveType.Cube, new Vector3(0f, 0.9f, -0.2f), new Vector3(2.8f, 0.2f, 0.2f), new Vector3(0f, 0f, -18f), Mat("Ember"));
        Piece(model, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.7f), new Vector3(1f, 0.1f, 0.8f), Vector3.zero, Mat("Timber"));
        Piece(model, PrimitiveType.Sphere, new Vector3(0.1f, 0.35f, 0.7f), new Vector3(0.5f, 0.6f, 0.45f), Vector3.zero, Mat("Cloth"));
    }

    private static void Cookfire(Transform model)
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Piece(model, PrimitiveType.Sphere, new Vector3(Mathf.Cos(angle) * 0.6f, 0.1f, Mathf.Sin(angle) * 0.6f), new Vector3(0.28f, 0.2f, 0.28f), Vector3.zero, Mat("Stone"));
        }

        Piece(model, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.04f, 0.9f), Vector3.zero, Mat("Ember"));
        Piece(model, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.2f, 0.5f), Vector3.zero, Mat("Iron"));
        Piece(model, PrimitiveType.Cylinder, new Vector3(1.3f, 0.2f, 0.3f), new Vector3(0.35f, 0.6f, 0.35f), new Vector3(90f, 30f, 0f), Mat("Timber"));
    }

    private static void Wall(Transform model)
    {
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI / 6f;
            var at = new Vector3(Mathf.Cos(angle) * 1.6f, 0.55f, Mathf.Sin(angle) * 1.6f);
            Piece(model, PrimitiveType.Cube, at, new Vector3(0.9f, 1.1f, 0.45f), new Vector3(0f, -angle * Mathf.Rad2Deg + 90f, 0f), Mat("Stone"));
        }
    }

    private static void Piece(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = Quaternion.Euler(euler);
        piece.transform.localScale = scale;
        piece.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    // ----- People: words for the four found by chance, and Ingrid's pointer to Bjarne -----

    private static void People()
    {
        // The first act's line (BuildFirstAct, the night the door broke), so the two builders don't undo each other.
        Line("Settlement/Ingrid", "welcomeLine", "I watched your fire for three nights from under a root. You should know two things. The old troll at the stones didn't come for us: it came for the door. And on the second night something walked past my root in Granheim mail, like Eirik's. I called to it. It turned, and it didn't know its name. When the stones broke I heard Bjarne's hammer start up, somewhere deep in the woods, slow, like he's working one-handed. Let me be of use until we find them.");
        Line("Settlement/Gunnar", "rescueLine", "Took you long enough. That boar's been lying on me since yesterday.");
        Line("Settlement/Asa", "rescueLine", "You're not from the village. Doesn't matter. Help me with this sack.");
        Line("Settlement/Yrsa", "rescueLine", "Mind the pot. If you've come to eat, you'll carry.");
        Line("Settlement/Hild", "rescueLine", "Mind the coping. It's the only level thing for a mile.");
    }

    private static void Line(string resident, string field, string text) => Edit(Resident(resident), so => so.FindProperty(field).stringValue = text);

    // ----- Quests: one to bring each home, saying what they answer; one to give them work -----

    private static string QuestsInOrder()
    {
        QuestDefinition before = Quest("AStrangerInTheMist");
        int made = 0;
        foreach (Person person in Order)
        {
            ResidentDefinition who = Resident(person.Resident);
            QuestDefinition rescue = Made("Bring" + person.Name + "Home", "rescue." + person.Name.ToLowerInvariant(), person.RescueTitle, Expedition, person.RescueText, before, true,
                (QuestObjective.Deed(ObjectiveType.RescuePerson, who, false), $"Find {person.Name} in the Misty Woods"),
                (QuestObjective.Deed(ObjectiveType.RescuePerson, who, true), $"Bring {person.Name} home safely"));
            JobDefinition job = Job(person.Job);
            var work = new List<(QuestObjective, string)> { (QuestObjective.Create(ObjectiveType.AssignJob, 1, job), $"Give {person.Name} the {job.DisplayName}'s work") };
            if (person.Workplace != null)
            {
                BuildingDefinition place = Building(person.Workplace);
                // What it waits for, from its own definition, so the step says why it may not be in the menu yet. Not the
                // Workshop at its first tier: that stands from the opening (Docs/RescueOrder.md).
                string waits = "";
                foreach (BuildingRequirement requirement in place.Requires)
                {
                    if (requirement.building != null && !(requirement.building.name == "ConstructionTable" && requirement.tier <= 1))
                    {
                        waits = requirement.tier > 1 ? $": it wants {BuildingUnlocks.Describe(requirement)}" : $": it wants the {requirement.building.DisplayName} standing";
                        break;
                    }
                }

                work.Add((QuestObjective.Create(ObjectiveType.BuildStructure, 1, place), $"Build a {place.DisplayName} (build menu: {{key:Build}}){waits}"));
            }

            // The smith's point is the mending: once is enough to see it costs half now.
            if (job.GearRepairDiscount > 0f)
            {
                work.Add((QuestObjective.Create(ObjectiveType.RepairItem, 1), $"Repair a worn piece of gear at the Workshop: with {person.Name} at work it costs less"));
            }

            Made(person.WorkFile, "work." + person.Job.ToLowerInvariant(), person.WorkTitle, Work, person.WorkText, rescue, false, work.ToArray());
            before = rescue;
            made += 2;
        }

        // Ingrid's is the first of the work quests; the lodge's walls and the shed follow Tor.
        Edit(Quest("TheAlchemist"), so =>
        {
            so.FindProperty("chain").stringValue = Work;
            so.FindProperty("description").stringValue = "Ingrid brought two things home: a healer's hands and the old ways of cleansing cursed things. Give her the Alchemist's work and build the Purification Altar she showed you ({key:Build}). Her Cauldron brews healing draughts.";
        });
        After("RaiseTheWalls", Quest("WoodcuttersLodge"));
        QuestDefinition growing = Quest("AGrowingHome");
        Edit(growing, so =>
        {
            so.FindProperty("description").stringValue = "Ingrid, Bjarne and Tor are home, and more will come through that door. They need beds, and somewhere dry to set down what they carry. Build them.";
            SerializedProperty objectives = so.FindProperty("objectives");
            objectives.arraySize = 2;
            Set(objectives.GetArrayElementAtIndex(0), QuestObjective.Create(ObjectiveType.BuildStructure, 1, Building("StorageShed")), "Build a Storage Shed (build menu: {key:Build})");
            Set(objectives.GetArrayElementAtIndex(1), QuestObjective.Create(ObjectiveType.BuildStructure, 1, Building("Hut")), "Build a Hut (build menu: {key:Build})");
        });
        After("AGrowingHome", Quest("WoodcuttersLodge"));
        Made("HeavyLoads", "work.heavy_loads", "Heavy Loads", Work,
            "Copper ore and planks weigh more than any pack can take home. Tor knows carts: raise his Woodcutter's Lodge to Tier II and he plans a Wainwright's Yard, where barrows, handcarts and a rowboat are built.",
            Quest("WoodcuttersLodge"), true,
            (QuestObjective.Upgrade(Building("Sawbench"), 2), "Raise the Woodcutter's Lodge to Tier II ({key:Haven})"),
            (QuestObjective.Create(ObjectiveType.BuildStructure, 1, Building("WainwrightsYard")), "Build a Wainwright's Yard (build menu: {key:Build})"),
            (QuestObjective.Create(ObjectiveType.CraftItem, 1, Find<ItemDefinition>("Wheelbarrow")), "Make a Wheelbarrow at the Wainwright's Yard"));
        return made + 1 + " quests in order";
    }

    private static QuestDefinition Made(string file, string id, string title, string chain, string description, QuestDefinition after, bool inOrder,
        params (QuestObjective objective, string text)[] objectives)
    {
        string path = Quests + file + ".asset";
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        bool isNew = quest == null;
        if (isNew)
        {
            quest = ScriptableObject.CreateInstance<QuestDefinition>();
            AssetDatabase.CreateAsset(quest, path);
        }

        Edit(quest, so =>
        {
            if (isNew) so.FindProperty("id").stringValue = id; // An older quest keeps its id, so saves keep its progress.
            so.FindProperty("displayName").stringValue = title;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("chain").stringValue = chain;
            so.FindProperty("start").intValue = (int)QuestStart.Automatically;
            so.FindProperty("startEvent").objectReferenceValue = null;
            so.FindProperty("objectivesInOrder").boolValue = inOrder;
            SerializedProperty prerequisites = so.FindProperty("prerequisites");
            prerequisites.arraySize = 1;
            prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = after;
            SerializedProperty list = so.FindProperty("objectives");
            list.arraySize = objectives.Length;
            for (int i = 0; i < objectives.Length; i++) Set(list.GetArrayElementAtIndex(i), objectives[i].objective, objectives[i].text);
        });
        return quest;
    }

    private static void Set(SerializedProperty objective, QuestObjective value, string text)
    {
        objective.boxedValue = value;
        objective.FindPropertyRelative("description").stringValue = text;
        objective.FindPropertyRelative("countAlreadyOwned").boolValue = true;
    }

    private static void After(string file, QuestDefinition before) => Edit(Quest(file), so =>
    {
        SerializedProperty prerequisites = so.FindProperty("prerequisites");
        prerequisites.arraySize = 1;
        prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = before;
    });

    // ----- Home: the portal hears each one, and the old moment about Bjarne in the north is Sigrid's horn now -----

    private static string Home()
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScene);
        HomePortal tree = Object.FindAnyObjectByType<HomePortal>(FindObjectsInactive.Include);
        if (tree == null) return "no portal in " + HomeScene;

        // Later whispers win while two quests are active, so the rescues go last, in order.
        var so = new SerializedObject(tree);
        SerializedProperty whispers = so.FindProperty("questWhispers");
        foreach (Person person in Order)
        {
            QuestDefinition quest = Quest("Bring" + person.Name + "Home");
            int at = -1;
            for (int i = 0; i < whispers.arraySize; i++)
            {
                if (whispers.GetArrayElementAtIndex(i).FindPropertyRelative("quest").objectReferenceValue == quest) at = i;
            }

            if (at < 0)
            {
                whispers.InsertArrayElementAtIndex(whispers.arraySize);
                at = whispers.arraySize - 1;
            }

            SerializedProperty whisper = whispers.GetArrayElementAtIndex(at);
            whisper.FindPropertyRelative("quest").objectReferenceValue = quest;
            whisper.FindPropertyRelative("text").stringValue = person.Whisper;
            whisper.FindPropertyRelative("moment").objectReferenceValue = null;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        string moment = SigridsHorn();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{Order.Length} portal whispers; {moment}";
    }

    private static string SigridsHorn()
    {
        GameObject old = GameObject.Find("Story - Bjarne's Hammer");
        if (old == null) return "Sigrid's horn already set";
        old.name = "Story - Sigrid's Horn";
        var story = new SerializedObject(old.GetComponent<StoryScript>());
        SerializedProperty nodes = story.FindProperty("nodes");
        var lines = new Queue<string>(new[]
        {
            "<i>Listen. Far off, at dusk, a horn: three long notes and a rest, over and over. The expedition's signal. Someone out there has seen your light and is answering it.</i>",
            "That's Sigrid.",
            "<i>Then she is still herself, and she is holding on. She will want the others home before her. Bring them, little one, and keep this fire high.</i>",
        });
        for (int i = 0; i < nodes.arraySize && lines.Count > 0; i++)
        {
            SerializedProperty line = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("line");
            if (line != null) line.stringValue = lines.Dequeue();
        }

        story.ApplyModifiedPropertiesWithoutUndo();
        return "Bjarne's Hammer is Sigrid's Horn";
    }

    // ----- Helpers -----

    private static QuestDefinition Quest(string file) => Load<QuestDefinition>(Quests + file + ".asset");
    private static BuildingDefinition Building(string file) => Load<BuildingDefinition>(Buildings + file + ".asset");
    private static ResidentDefinition Resident(string path) => Load<ResidentDefinition>(Data + path + ".asset");
    private static JobDefinition Job(string name) => Find<JobDefinition>(name);

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null ? asset : throw new InvalidOperationException("Missing " + path);
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
