using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Raid events (`unity command run_script --file AgentScripts/BuildRaidEvents.cs --entry BuildRaidEvents.Apply`): ten things
/// that can happen in the Misty Woods (GDD §9.3), their site prefabs and the Raid Events system on the raid. Each has a
/// warning, and its choices say the price plainly. Sites are placeholder shapes with a pale beacon light. Run BuildGear,
/// BuildFood and BuildCreatures first. Safe to run again: events are rewritten, site prefabs are kept once made.
/// </summary>
public static class BuildRaidEvents
{
    private const string Events = "Assets/_Project/Data/Raids/Events/";
    private const string Sites = "Assets/_Project/Prefabs/Raids/Events/Sites/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Raid = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string Wood = "Assets/_Project/Art/Forest/Materials/Dead Wood.mat";
    private const string Stone = "Assets/_Project/Data/Prologue/Shelter Stone.mat";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";

    private delegate void Outcome(SerializedProperty list);

    private sealed class Choice
    {
        public string Label, Detail, Result;
        public (string item, int amount)[] Cost = new (string, int)[0];
        public Outcome[] Outcomes = new Outcome[0];
    }

    public static string Apply()
    {
        PrefabBuildUtility.EnsureFolder(Events.TrimEnd('/'));
        PrefabBuildUtility.EnsureFolder(Sites.TrimEnd('/'));
        var pool = new List<RaidEventDefinition>
        {
            TrappedPedlar(), ThreeWishes(), TradersWagon(), TollTroll(), ThickeningFog(), BeaconLight(), LivingStones(), Oskorei(), OldBarrow(), CursedTree(),
        };
        AssetDatabase.SaveAssets();
        string system = AddToRaid(pool);
        AssetDatabase.SaveAssets();
        return $"{pool.Count} raid events; {system}";
    }

    // ----- The events -----

    private static RaidEventDefinition TrappedPedlar()
    {
        return Event("TrappedPedlar", "Cries from the Ditch", 1f, 40f, 90f, 0, "Someone is shouting for help, off the path.", "Quest Started", Site("Trapped Pedlar", root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(3.4f, 0.5f, 0.5f), Mat(Wood)).localRotation = Quaternion.Euler(0f, 25f, 6f);
                Piece(root, PrimitiveType.Cube, new Vector3(0.7f, 0.25f, 0.7f), new Vector3(0.6f, 0.45f, 0.5f), Tint("Pedlar Pack", new Color(0.5f, 0.35f, 0.22f)));
            }, true),
            "A pedlar lies pinned under a fallen spruce, his pack just out of his reach. \"Please. They'll smell me soon.\"", null,
            new Choice
            {
                Label = "Lift the log", Detail = "Costs you 45 seconds, and it's loud work.", Result = "He limps off into the trees, but not before pressing his purse into your hand.",
                Outcomes = new[] { Time(-45f), Noise(20f), Loot(("OldSilverPenny", 3, 6, 1f), ("HealingDraught", 1, 1, 0.6f), ("Antidote", 1, 1, 0.4f), ("Honey", 1, 2, 0.5f)) },
            },
            new Choice
            {
                Label = "Take his pack and go", Detail = "Quick. You'll carry it with you for a while.", Result = "You don't look back. His voice follows you for a long time.",
                Outcomes = new[] { Loot(("OldSilverPenny", 2, 4, 1f), ("Linen", 1, 2, 0.6f)), Boon("Guilty Conscience", 180f, new StatBonus(StatType.MaxStamina, -15f)) },
            });
    }

    private static RaidEventDefinition ThreeWishes()
    {
        return Event("ThreeWishes", "Three Wishes", 0.8f, 90f, 100f, 0, "A hooded stranger stands at a crossing in the trees. You could swear they weren't there before.", "Spotted", Site("Crossroads Stranger", root =>
            {
                Piece(root, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.7f, 1f, 0.7f), Tint("Stranger Cloak", new Color(0.13f, 0.13f, 0.16f)));
                Piece(root, PrimitiveType.Sphere, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.4f, Tint("Stranger Cloak", new Color(0.13f, 0.13f, 0.16f)));
                Piece(root, PrimitiveType.Cylinder, new Vector3(0.5f, 1.1f, 0.1f), new Vector3(0.05f, 1.1f, 0.05f), Mat(Wood));
            }, true),
            "\"Three roads,\" says the stranger, not looking up. \"Take one. I always collect.\"", null,
            new Choice
            {
                Label = "Ask for strength", Detail = "+25% damage for 3 minutes. It takes a quarter of your health to give it.", Result = "Something in your arms wakes up. Something in your chest goes quiet.",
                Outcomes = new[] { Health(-0.25f), Boon("Stranger's Strength", 180f, new StatBonus(StatType.DamageBonus, 0.25f)) },
            },
            new Choice
            {
                Label = "Ask for riches", Detail = "A troll's idol, worth a great deal. Everything nearby will know you have it.", Result = "It's heavier than it looks, and the forest has noticed.",
                Outcomes = new[] { Loot(("GildedTrollIdol", 1, 1, 1f)), Spawn(new[] { "WolfPack" }, 1, 12f, true), Spawn(new[] { "RootlingSwarm" }, 1, 12f, true) },
            },
            new Choice
            {
                Label = "Ask for time", Detail = "+2 minutes on the clock. -25 health until it's paid back.", Result = "The light shifts. You feel older.",
                Outcomes = new[] { Time(120f), Boon("Borrowed Time", 300f, new StatBonus(StatType.MaxHealth, -25f)) },
            });
    }

    private static RaidEventDefinition TradersWagon()
    {
        return Event("TradersWagon", "Overturned Wagon", 1.2f, 40f, 90f, 0, "A broken wheel and scattered parcels on an old track. Someone left in a hurry.", "Quest Started", Site("Overturned Wagon", root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 0.7f, 3f), Mat(Wood)).localRotation = Quaternion.Euler(0f, 0f, 62f);
                Piece(root, PrimitiveType.Cylinder, new Vector3(1.1f, 0.5f, 1.1f), new Vector3(1f, 0.08f, 1f), Mat(Wood)).localRotation = Quaternion.Euler(0f, 0f, 90f);
                Piece(root, PrimitiveType.Cylinder, new Vector3(-1.4f, 0.1f, -1.2f), new Vector3(1f, 0.08f, 1f), Mat(Wood));
                Piece(root, PrimitiveType.Cube, new Vector3(-0.9f, 0.2f, 1.5f), new Vector3(0.6f, 0.4f, 0.6f), Tint("Pedlar Pack", new Color(0.5f, 0.35f, 0.22f)));
                Piece(root, PrimitiveType.Cube, new Vector3(0.9f, 0.15f, -1.5f), new Vector3(0.5f, 0.3f, 0.4f), Tint("Pedlar Pack", new Color(0.5f, 0.35f, 0.22f)));
            }, true),
            "A trader's wagon on its side, one wheel in splinters. Parcels lie in the mud. Claw marks on the boards, not old.", null,
            new Choice
            {
                Label = "Search it carefully", Detail = "Costs you 30 seconds. Quiet.", Result = "You go through it parcel by parcel.",
                Outcomes = new[] { Time(-30f), Loot(("OldSilverPenny", 2, 5, 1f), ("Linen", 1, 3, 0.7f), ("CopperIngot", 2, 4, 0.7f), ("IronIngot", 1, 2, 0.4f)) },
            },
            new Choice
            {
                Label = "Grab what you can", Detail = "Fast and loud. Whatever did this may still be close.", Result = "You tear it open and stuff your pack.",
                Outcomes = new[]
                {
                    Noise(30f), Loot(("OldSilverPenny", 3, 7, 1f), ("IronIngot", 2, 3, 0.7f), ("Linen", 2, 3, 0.7f), ("RunestoneFragment", 1, 1, 0.2f)),
                    Chance(0.65f, new[] { Tell("It was still close.", MessageKind.Warning), Spawn(new[] { "WolfPack" }, 1, 10f, true) }, new Outcome[0]),
                },
            },
            new Choice
            {
                Label = "Mend the wheel", Cost = new[] { ("OakPlank", 2), ("NettleCord", 1) }, Detail = "The trader hides close by. Fair traders pay fairly.",
                Result = "A thin man comes out of the ferns, thanks you three times and tells you the safe way.",
                Outcomes = new[] { Loot(("RunestoneFragment", 1, 1, 1f), ("Honey", 2, 2, 1f), ("Antidote", 1, 2, 1f), ("OldSilverPenny", 4, 8, 1f)), Reveal(45f, true) },
            });
    }

    private static RaidEventDefinition TollTroll()
    {
        return Event("TollTroll", "The Toll Troll", 1f, 90f, 90f, 0, "Huge tracks in the mud, and somewhere ahead, slow deep laughter.", "TrollStep", Site("Toll Troll", root =>
            {
                var troll = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Enemies/SmallMossTrollVisual.prefab");
                var body = (GameObject)PrefabUtility.InstantiatePrefab(troll, root);
                PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.transform.localScale = Vector3.one * 2.2f;
                Piece(root, PrimitiveType.Cube, new Vector3(1.6f, 0.5f, 0f), new Vector3(1.2f, 1f, 2.4f), Mat(Stone));
                Piece(root, PrimitiveType.Cube, new Vector3(-1.6f, 0.5f, 0f), new Vector3(1.2f, 1f, 2.4f), Mat(Stone));
            }, true),
            "A troll sits across the way, chin on its fists. \"Toll,\" it says. \"Or riddle. What runs and never walks, has a bed and never sleeps?\"", null,
            new Choice
            {
                Label = "Pay the toll", Cost = new[] { ("Bone", 3) }, Detail = "It likes bones. It'll tell you what it knows.", Result = "It crunches them thoughtfully and points out every way through the woods.",
                Outcomes = new[] { Reveal(70f, true), Time(30f) },
            },
            new Choice
            {
                Label = "\"A river.\"", Detail = "An answer.", Result = "It laughs until moss falls off it, and tosses you something from its pile.",
                Outcomes = new[] { Loot(("Trollstone", 1, 1, 1f), ("OldSilverPenny", 3, 6, 1f)) },
            },
            new Choice
            {
                Label = "\"The wind.\"", Detail = "An answer.", Result = "\"No,\" it says, and stands up.",
                Outcomes = new[] { Health(-0.15f), Spawn(new[] { "SmallMossTroll" }, 2, 6f, true) },
            },
            new Choice
            {
                Label = "Walk through it", Detail = "It won't move. Its children might.", Result = "It whistles through its teeth, and the rocks around you get up.",
                Outcomes = new[] { Spawn(new[] { "SmallMossTroll" }, 3, 6f, true), Noise(25f) },
            });
    }

    private static RaidEventDefinition ThickeningFog()
    {
        return Event("ThickeningFog", "Thickening Fog", 1f, 60f, 100f, 0, "The birdsong stops. Somewhere close, fog is gathering with pale lights in it.", "Spotted", Site("Fog Bank", root =>
            {
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    Piece(root, PrimitiveType.Cube, new Vector3(Mathf.Cos(angle) * 3f, 0.7f, Mathf.Sin(angle) * 3f), new Vector3(0.5f, 1.4f, 0.3f), Mat(Stone));
                }

                var fog = new GameObject("Fog").AddComponent<FogArea>();
                fog.transform.SetParent(root, false);
                var fogData = new SerializedObject(fog);
                fogData.FindProperty("density").floatValue = 0.22f;
                fogData.FindProperty("radius").floatValue = 16f;
                fogData.FindProperty("height").floatValue = 5f;
                fogData.FindProperty("followsWeather").boolValue = false;
                fogData.ApplyModifiedPropertiesWithoutUndo();

                var chest = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/SupplyCache.prefab"), root);
                var cache = new SerializedObject(chest.GetComponent<LootCache>());
                SetLoot(cache.FindProperty("drops").FindPropertyRelative("entries"), ("WispEmber", 1, 2, 1f), ("RunestoneFragment", 1, 1, 0.5f), ("MoonsilverOre", 2, 3, 0.6f), ("OldSilverPenny", 3, 6, 1f));
                cache.ApplyModifiedPropertiesWithoutUndo();
            }, false),
            "Inside the fog a ring of old stones, and a chest nobody left by accident.", new[] { Spawn(new[] { "MarshLight" }, 3, 7f, false) });
    }

    private static RaidEventDefinition BeaconLight()
    {
        return Event("BeaconLight", "Old Beacon", 1f, 60f, 60f, 0, "A faint glow on the slope: an old beacon, still laid with dry wood.", "Quest Started", Site("Old Beacon", root =>
            {
                Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(2f, 0.3f, 2f), Mat(Stone));
                for (int i = 0; i < 5; i++)
                {
                    Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.9f, 0f), new Vector3(0.18f, 1.2f, 0.18f), Mat(Wood)).localRotation = Quaternion.Euler(18f, i * 72f, 0f);
                }
            }, true),
            "A stone hearth on a bare rise, stacked with dry wood. You can see a long way from here, and a long way off, things can see it.", null,
            new Choice
            {
                Label = "Light it", Detail = "Shows you the land and every way out. Everything for a long way will come to look.", Result = "It goes up with a roar. For a moment the whole forest is laid out in front of you.",
                Outcomes = new[] { Reveal(90f, true), Noise(50f) },
            },
            new Choice
            {
                Label = "Keep a small fire and rest", Detail = "Heals you. Costs you 20 seconds.", Result = "You sit with your hands to the warmth until they stop shaking.",
                Outcomes = new[] { Time(-20f), Health(0.5f) },
            });
    }

    private static RaidEventDefinition LivingStones()
    {
        return Event("LivingStones", "The Living Path", 0.9f, 60f, 110f, 0, "Close by, stone grinds on stone. The runestones are moving.", "TrollStep", Site("Living Stones", root =>
            {
                for (int i = 0; i < 3; i++)
                {
                    Piece(root, PrimitiveType.Cube, new Vector3(-1.8f + i * 1.8f, 1f, 0f), new Vector3(0.7f, 2f, 0.35f), Mat(Stone)).localRotation = Quaternion.Euler(0f, i * 17f, i == 1 ? 4f : -3f);
                }
            }, true),
            "Three runestones stand where the path should be. One is green with fresh moss. One is warm to the touch. One is scored with claw marks. Old rhyme: \"Moss grows slow, so moss won't lie.\"", null,
            new Choice
            {
                Label = "Follow the mossy stone", Detail = "It hasn't moved in a long time.", Result = "The path opens, and it leads somewhere shorter than it should.",
                Outcomes = new[] { Time(90f), Reveal(40f, false) },
            },
            new Choice
            {
                Label = "Follow the warm stone", Detail = "Something is under it. Could be good.", Result = "You put your shoulder to it.",
                Outcomes = new[]
                {
                    Chance(0.5f, new[] { Tell("A hollow under it, and something left there long ago.", MessageKind.Gain), Loot(("RunestoneFragment", 1, 1, 1f), ("OldSilverPenny", 3, 5, 1f)) },
                        new[] { Tell("Nothing. The path has moved while your back was turned.", MessageKind.Warning), Time(-45f) }),
                },
            },
            new Choice
            {
                Label = "Follow the clawed stone", Detail = "Something else went this way.", Result = "The path closes behind you, and the cold comes up out of the ground.",
                Outcomes = new[] { Time(-60f), Status("Chill") },
            });
    }

    private static RaidEventDefinition Oskorei()
    {
        return Event("Oskorei", "The Oskorei", 0.8f, 150f, 60f, 1, "A horn, far off and coming closer. The Oskorei ride. Get off the path, or be ready.", "Thunder", null, null,
            new[] { Noise(40f), Spawn(new[] { "Draug" }, 2, 34f, true), Spawn(new[] { "ForestBittenWolf" }, 2, 30f, true) });
    }

    private static RaidEventDefinition OldBarrow()
    {
        return Event("OldBarrow", "The Open Barrow", 1f, 90f, 90f, 0, "The ground has slipped on a low mound. Something glints in the dark inside.", "Spotted", Site("Open Barrow", root =>
            {
                Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), new Vector3(7f, 3.4f, 7f), Tint("Barrow Turf", new Color(0.24f, 0.3f, 0.17f)));
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.8f, 3.2f), new Vector3(1.6f, 1.6f, 0.4f), Tint("Barrow Dark", new Color(0.03f, 0.03f, 0.04f)));
                Piece(root, PrimitiveType.Cube, new Vector3(-1f, 1f, 3.3f), new Vector3(0.4f, 2f, 0.5f), Mat(Stone));
                Piece(root, PrimitiveType.Cube, new Vector3(1f, 1f, 3.3f), new Vector3(0.4f, 2f, 0.5f), Mat(Stone));
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 2.05f, 3.3f), new Vector3(2.6f, 0.4f, 0.6f), Mat(Stone));
            }, true),
            "A stone-framed door into the mound. Silver in the dark, and an old smell. Whoever lies in there has not been disturbed in a long time.", null,
            new Choice
            {
                Label = "Reach in and take it", Detail = "Barrow silver and grave iron. The owner may mind.", Result = "Your fingers close on cold metal. Behind it, something breathes in.",
                Outcomes = new[] { Loot(("GraveIron", 2, 3, 1f), ("OldSilverPenny", 5, 10, 1f), ("GildedTrollIdol", 1, 1, 0.15f)), Spawn(new[] { "Draug" }, 1, 4f, true) },
            },
            new Choice
            {
                Label = "Leave an offering", Cost = new[] { ("OldSilverPenny", 2) }, Detail = "The dead remember manners.", Result = "A cold breath passes over you, and it feels like thanks.",
                Outcomes = new[] { Boon("Barrow's Blessing", 300f, new StatBonus(StatType.MaxHealth, 20f), new StatBonus(StatType.FrostResistance, 0.15f)) },
            });
    }

    private static RaidEventDefinition CursedTree()
    {
        return Event("CursedTree", "The Blighted Tree", 1f, 60f, 90f, 0, "One tree stands dead in the green, and the ground around it is black.", "Spotted", Site("Blighted Tree", root =>
            {
                Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f), new Vector3(0.9f, 2f, 0.9f), Tint("Blight Bark", new Color(0.09f, 0.08f, 0.08f)));
                Piece(root, PrimitiveType.Cylinder, new Vector3(0.7f, 3.2f, 0f), new Vector3(0.22f, 1.1f, 0.22f), Tint("Blight Bark", new Color(0.09f, 0.08f, 0.08f))).localRotation = Quaternion.Euler(0f, 0f, -55f);
                Piece(root, PrimitiveType.Cylinder, new Vector3(-0.6f, 3.6f, 0.2f), new Vector3(0.2f, 1f, 0.2f), Tint("Blight Bark", new Color(0.09f, 0.08f, 0.08f))).localRotation = Quaternion.Euler(15f, 0f, 50f);
                Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(7f, 0.03f, 7f), Tint("Blight Ground", new Color(0.05f, 0.05f, 0.05f)));
            }, true),
            "Rot runs up the trunk in black veins and something like a pulse moves under the bark. Roots stir by your feet.", null,
            new Choice
            {
                Label = "Cut out the rot", Detail = "Costs 30 seconds. Rootlings will defend it.", Result = "It screams without sound. Clean sap runs from the wound, and it gives something back.",
                Outcomes = new[] { Time(-30f), Spawn(new[] { "RootlingSwarm" }, 1, 5f, true), Loot(("!PurifiedEssence", 2, 3, 1f), ("HealingHerb", 2, 4, 1f), ("Elderwood", 1, 2, 0.6f)) },
            },
            new Choice
            {
                Label = "Drink from the black sap", Detail = "Strength for 4 minutes, and poison in your blood.", Result = "It tastes like the bottom of a well.",
                Outcomes = new[] { Status("Poison"), Boon("Black Sap", 240f, new StatBonus(StatType.Strength, 6f), new StatBonus(StatType.MaxStamina, 15f)) },
            });
    }

    // ----- Building an event -----

    private static RaidEventDefinition Event(string file, string label, float weight, float earliest, float latestLeft, int stage, string warning, string sound, GameObject site, string siteText,
        Outcome[] onStart, params Choice[] choices)
    {
        string path = Events + file + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<RaidEventDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        var so = new SerializedObject(definition);
        so.FindProperty("id").stringValue = file;
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("earliestSeconds").floatValue = earliest;
        so.FindProperty("latestSecondsLeft").floatValue = latestLeft;
        so.FindProperty("minimumStage").intValue = stage;
        so.FindProperty("warning").stringValue = warning;
        so.FindProperty("warningSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SoundDefinition>(Sounds + sound + ".asset");
        so.FindProperty("sitePrefab").objectReferenceValue = site;
        so.FindProperty("siteDistance").vector2Value = new Vector2(35f, 70f);
        so.FindProperty("siteText").stringValue = siteText ?? "";

        SerializedProperty start = so.FindProperty("onStart");
        start.ClearArray();
        foreach (Outcome outcome in onStart ?? new Outcome[0])
        {
            outcome(start);
        }

        SerializedProperty list = so.FindProperty("choices");
        list.ClearArray();
        foreach (Choice choice in choices)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("label").stringValue = choice.Label;
            entry.FindPropertyRelative("detail").stringValue = choice.Detail ?? "";
            entry.FindPropertyRelative("result").stringValue = choice.Result ?? "";
            SerializedProperty cost = entry.FindPropertyRelative("cost");
            cost.arraySize = choice.Cost.Length;
            for (int i = 0; i < choice.Cost.Length; i++)
            {
                cost.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = FindItem(choice.Cost[i].item);
                cost.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = choice.Cost[i].amount;
            }

            SerializedProperty outcomes = entry.FindPropertyRelative("outcomes");
            outcomes.ClearArray();
            foreach (Outcome outcome in choice.Outcomes)
            {
                outcome(outcomes);
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
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
    private static Outcome Health(float share) => list => Add(list, new ChangeHealthOutcome(), e => e.FindPropertyRelative("share").floatValue = share);
    private static Outcome Noise(float radius) => list => Add(list, new MakeNoiseOutcome(), e => e.FindPropertyRelative("radius").floatValue = radius);

    private static Outcome Reveal(float radius, bool exits) => list => Add(list, new RevealMapOutcome(), e =>
    {
        e.FindPropertyRelative("radius").floatValue = radius;
        e.FindPropertyRelative("showExits").boolValue = exits;
    });

    private static Outcome Tell(string text, MessageKind kind) => list => Add(list, new TellOutcome(), e =>
    {
        e.FindPropertyRelative("text").stringValue = text;
        e.FindPropertyRelative("kind").intValue = (int)kind;
    });

    private static Outcome Status(string file) => list => Add(list, new ApplyStatusOutcome(), e =>
        e.FindPropertyRelative("effect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/" + file + ".asset"));

    private static Outcome Boon(string name, float seconds, params StatBonus[] bonuses) => list => Add(list, new GiveBoonOutcome(), e =>
    {
        e.FindPropertyRelative("name").stringValue = name;
        e.FindPropertyRelative("seconds").floatValue = seconds;
        SerializedProperty stats = e.FindPropertyRelative("bonuses");
        stats.arraySize = bonuses.Length;
        for (int i = 0; i < bonuses.Length; i++)
        {
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue = (int)bonuses[i].stat;
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("op").intValue = (int)bonuses[i].op;
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = bonuses[i].value;
        }
    });

    private static Outcome Spawn(string[] prefabs, int count, float radius, bool hunt) => list => Add(list, new SpawnCreaturesOutcome(), e =>
    {
        SerializedProperty rows = e.FindPropertyRelative("prefabs");
        rows.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + prefabs[i] + ".prefab");
            rows.GetArrayElementAtIndex(i).objectReferenceValue = prefab != null ? prefab : throw new Exception("No enemy " + prefabs[i]);
        }

        e.FindPropertyRelative("count").intValue = count;
        e.FindPropertyRelative("radius").floatValue = radius;
        e.FindPropertyRelative("hunt").boolValue = hunt;
    });

    private static Outcome Loot(params (string what, int min, int max, float chance)[] rows) => list => Add(list, new GiveLootOutcome(), e =>
        SetLoot(e.FindPropertyRelative("loot").FindPropertyRelative("entries"), rows));

    private static Outcome Chance(float chance, Outcome[] lucky, Outcome[] not) => list => Add(list, new ChanceOutcome(), e =>
    {
        e.FindPropertyRelative("chance").floatValue = chance;
        SerializedProperty yes = e.FindPropertyRelative("ifLucky");
        yes.ClearArray();
        foreach (Outcome outcome in lucky)
        {
            outcome(yes);
        }

        SerializedProperty no = e.FindPropertyRelative("ifNot");
        no.ClearArray();
        foreach (Outcome outcome in not)
        {
            outcome(no);
        }
    });

    // "!" marks a resource (essences), the rest are items.
    private static void SetLoot(SerializedProperty entries, params (string what, int min, int max, float chance)[] rows)
    {
        entries.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            bool resource = rows[i].what.StartsWith("!");
            entry.FindPropertyRelative("item").objectReferenceValue = resource ? null : FindItem(rows[i].what);
            entry.FindPropertyRelative("resource").objectReferenceValue = resource
                ? AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/_Project/Data/ResourceInventory/Definitions/" + rows[i].what.Substring(1) + ".asset")
                : null;
            entry.FindPropertyRelative("minQuantity").intValue = rows[i].min;
            entry.FindPropertyRelative("maxQuantity").intValue = rows[i].max;
            entry.FindPropertyRelative("probability").floatValue = rows[i].chance;
        }
    }

    private static ItemDefinition FindItem(string file)
    {
        foreach (string folder in new[] { "Materials/", "", "Food/" })
        {
            var found = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + folder + file + ".asset");
            if (found != null)
            {
                return found;
            }
        }

        throw new Exception("No item " + file);
    }

    // ----- Sites -----

    private static GameObject Site(string name, Action<Transform> shape, bool hasChoices)
    {
        string path = Sites + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject(name);
        try
        {
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            shape(model);

            // Solid enough to walk up to and to be outlined; the beacon says where it is from far away.
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(2f, 2f, 2f);

            var beacon = new GameObject("Beacon").transform;
            beacon.SetParent(root.transform, false);
            Piece(beacon, PrimitiveType.Cylinder, new Vector3(0f, 12f, 0f), new Vector3(0.5f, 12f, 0.5f), Beam());
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(beacon, false);
            light.transform.localPosition = new Vector3(0f, 3f, 0f);
            light.type = LightType.Point;
            light.color = new Color(0.75f, 0.65f, 1f);
            light.intensity = 3f;
            light.range = 12f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<AtmosphereLight>();

            var site = root.AddComponent<RaidEventSite>();
            var siteData = new SerializedObject(site);
            siteData.FindProperty("beacon").objectReferenceValue = beacon.gameObject;
            siteData.ApplyModifiedPropertiesWithoutUndo();
            if (hasChoices)
            {
                var interactable = root.AddComponent<Interactable>();
                var data = new SerializedObject(interactable);
                data.FindProperty("kind").intValue = (int)InteractionKind.Use;
                data.FindProperty("deactivateOnComplete").boolValue = false;
                data.FindProperty("range").floatValue = 3f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                box.isTrigger = true;
            }

            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform Piece(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = material;
        return piece.transform;
    }

    private static Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

    private static Material Tint(string name, Color color)
    {
        string path = Sites + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static Material Beam()
    {
        string path = Sites + "Event Beacon.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(0.75f, 0.65f, 1f, 0.12f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    // ----- The raid -----

    private static string AddToRaid(List<RaidEventDefinition> pool)
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(Raid);
        var so = new SerializedObject(raid);
        SerializedProperty systems = so.FindProperty("systems");
        SerializedProperty entry = null;
        for (int i = 0; i < systems.arraySize; i++)
        {
            SerializedProperty candidate = systems.GetArrayElementAtIndex(i);
            if (candidate.managedReferenceValue is RaidEventsSystem)
            {
                entry = candidate;
            }
        }

        bool added = entry == null;
        if (added)
        {
            systems.InsertArrayElementAtIndex(systems.arraySize);
            entry = systems.GetArrayElementAtIndex(systems.arraySize - 1);
            entry.managedReferenceValue = new RaidEventsSystem();
        }

        SerializedProperty settings = entry.FindPropertyRelative("settings");
        SerializedProperty list = settings.FindPropertyRelative("pool");
        list.arraySize = pool.Count;
        for (int i = 0; i < pool.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
        }

        settings.FindPropertyRelative("firstDelay").floatValue = 45f;
        settings.FindPropertyRelative("interval").floatValue = 110f;
        settings.FindPropertyRelative("intervalSpread").floatValue = 35f;
        settings.FindPropertyRelative("maxPerRaid").intValue = 4;
        settings.FindPropertyRelative("maxOpenSites").intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(raid);
        return added ? "Raid Events added to Misty Woods" : "Raid Events updated on Misty Woods";
    }
}
