using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The forest forgets the lost: the words and the rumour that make a lost companion something to go after, the exits only a large map has,
/// and the message feed in two. Safe to run again.
/// </summary>
public static class BuildCompanionAmnesia
{
    private const string Events = "Assets/_Project/Data/Raids/Rumours/Events/";
    private const string Rumours = "Assets/_Project/Data/Raids/Rumours/";
    private static readonly string[] Raids = { "Assets/_Project/Data/Raids/MistyWoods.asset", "Assets/_Project/Data/Raids/TrollValley.asset" };

    public static string Run()
    {
        Words();
        RaidEventDefinition site = Site();
        RaidRumourDefinition rumour = Rumour(site);
        string exits = RaidsFind(rumour);
        string feed = SplitFeed();
        AssetDatabase.SaveAssets();
        return $"event {site.Id}; rumour {rumour.Id}; {exits}; {feed}";
    }

    private static void Words()
    {
        var settings = new SerializedObject(SettlementSettings.Current);
        settings.FindProperty("memoryReturnsEvery").intValue = 2;
        settings.FindProperty("foundComesHomeWithin").floatValue = 20f;
        settings.FindProperty("lostReportLine").stringValue = "The forest doesn't kill the lost. It forgets them, and they forget everything else. They are out there, and they won't know you.";
        settings.FindProperty("foundFollowLine").stringValue = "{0} doesn't know you, or their own name. They follow anyway. Get them to an exit alive.";
        settings.FindProperty("foundReportLine").stringValue = "{0} came home with you, and remembers none of it.";
        settings.FindProperty("leftBehindLine").stringValue = "You found {0}, and left them out there. They will still be walking.";
        Strings(settings.FindProperty("strangerLines"), new[]
        {
            "You keep looking at me as if I should know you. I'm sorry. I don't. They tell me you came back for me. Thank you, whoever you are.",
            "They say this is home. They say you brought me back to it. I believe them, I suppose. I don't remember the fire. I don't remember you.",
            "I know how to hold a blade and not where I learned it. Everyone here calls me by a name. I've started answering to it.",
        });
        Lines(settings.FindProperty("returningLines"), new Dictionary<CompanionMemory, string>
        {
            { CompanionMemory.Failed, "We ran from something, didn't we? You and I, with nothing to show for it. I woke up remembering how it felt." },
            { CompanionMemory.HardFight, "There was a fight. A bad one, back to back. I can still feel it in my arm. That was you beside me. That was you." },
            { CompanionMemory.CloseCall, "I dreamed of a beacon and a clock running out, and you shouting at me to move. That wasn't a dream, was it?" },
            { CompanionMemory.RoseInRank, "Something came back to me last night. The day I decided you could be trusted. I don't know how I ever stopped." },
            { CompanionMemory.HelpedUp, "I was on the ground once, bleeding, and someone hauled me up. Your hands. I remember your hands." },
            { CompanionMemory.BossBeaten, "That great rotten thing in the hollow. We killed it, you and me. How does a person forget a thing like that?" },
        });
        settings.FindProperty("allBackLine").stringValue = "That's the last of it, I think. Every raid, every road, you. Whatever the forest took, it didn't get to keep it.";

        SerializedProperty grief = settings.FindProperty("griefLines");
        const string Hint = "Someone saw {0} out there, walking in circles. Walking. Do you understand? Go and look.";
        bool has = false;
        for (int i = 0; i < grief.arraySize; i++)
        {
            has |= grief.GetArrayElementAtIndex(i).stringValue == Hint;
        }

        if (!has)
        {
            grief.arraySize++;
            grief.GetArrayElementAtIndex(grief.arraySize - 1).stringValue = Hint;
        }

        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(SettlementSettings.Current);
    }

    private static RaidEventDefinition Site()
    {
        string path = Events + "TheLost.asset";
        var made = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(path);
        if (made == null)
        {
            made = ScriptableObject.CreateInstance<RaidEventDefinition>();
            AssetDatabase.CreateAsset(made, path);
        }

        var pattern = new SerializedObject(AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(Events + "SigridsHorn.asset"));
        var data = new SerializedObject(made);
        data.FindProperty("id").stringValue = "rumour.thelost";
        data.FindProperty("displayName").stringValue = "Someone Wandering";
        data.FindProperty("weight").floatValue = 0f;
        data.FindProperty("sitePrefab").objectReferenceValue = pattern.FindProperty("sitePrefab").objectReferenceValue;
        data.FindProperty("siteText").stringValue = "Someone stands among the trees, turning a branch over in their hands. They look up at you the way you would look at a stranger, and hope you'll know what to do.";

        SerializedProperty onStart = data.FindProperty("onStart");
        onStart.arraySize = 1;
        onStart.GetArrayElementAtIndex(0).managedReferenceValue = new StandTheLostOutcome();

        SerializedProperty choices = data.FindProperty("choices");
        choices.arraySize = 1;
        SerializedProperty choice = choices.GetArrayElementAtIndex(0);
        choice.FindPropertyRelative("label").stringValue = "Say their name";
        choice.FindPropertyRelative("detail").stringValue = "They may follow you. They won't know why. Bring them to an exit alive, or they stay out here.";
        choice.FindPropertyRelative("result").stringValue = "It means nothing to them. But when you turn to go, they come too.";
        choice.FindPropertyRelative("cost").arraySize = 0;
        SerializedProperty outcomes = choice.FindPropertyRelative("outcomes");
        outcomes.arraySize = 1;
        outcomes.GetArrayElementAtIndex(0).managedReferenceValue = new LeadTheLostOutcome();
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(made);
        return made;
    }

    private static RaidRumourDefinition Rumour(RaidEventDefinition site)
    {
        string path = Rumours + "TheLost.asset";
        var made = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(path);
        if (made == null)
        {
            made = ScriptableObject.CreateInstance<RaidRumourDefinition>();
            AssetDatabase.CreateAsset(made, path);
        }

        var data = new SerializedObject(made);
        data.FindProperty("id").stringValue = "rumour.thelost";
        data.FindProperty("briefing").stringValue = "{0} has been seen walking these woods. They didn't answer to their name.";
        data.FindProperty("site").objectReferenceValue = site;
        data.FindProperty("weight").floatValue = 1f;
        data.FindProperty("urgent").boolValue = true;
        data.FindProperty("seeksLost").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(made);
        return made;
    }

    // Both raids can lead to them, so nobody is out of reach behind a locked way or a raid not chosen. Each large map gets its own exits' names.
    private static string RaidsFind(RaidRumourDefinition rumour)
    {
        var names = new Dictionary<string, string[]>
        {
            { Raids[0], new[] { "Split Birch", "Old Cairn", "Drowned Stones" } },
            { Raids[1], new[] { "Rockfall Gap", "Goat Stair", "Cold Ford" } },
        };
        string said = "";
        foreach (string path in Raids)
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(path);
            RaidRumoursSystem system = raid.GetSystem<RaidRumoursSystem>();
            var pool = new List<RaidRumourDefinition>(system.Pool);
            if (!pool.Contains(rumour))
            {
                pool.Add(rumour);
                system.Configure(pool, system.Count);
            }

            var data = new SerializedObject(raid);
            Strings(data.FindProperty("largeRaid").FindPropertyRelative("extraExits"), names[path]);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(raid);
            said += $"{raid.DisplayName}: {pool.Count} rumours, {raid.LargeRaid.extraExits.Length} extra exits; ";
        }

        return said;
    }

    // Pickups stay beside the character; what is posted moves to the left, above the status lines.
    private static string SplitFeed()
    {
        const string Path = "Assets/_Project/Prefabs/UI/ItemHUD.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            MessageFeedView pickups = null;
            MessageFeedView notices = null;
            foreach (MessageFeedView view in root.GetComponentsInChildren<MessageFeedView>(true))
            {
                if (view.name == "Notices") notices = view;
                else pickups = view;
            }

            var pickupData = new SerializedObject(pickups);
            var template = (TMP_Text)pickupData.FindProperty("lineTemplate").objectReferenceValue;
            var area = (RectTransform)template.transform.parent;
            if (notices == null)
            {
                RectTransform copy = Object.Instantiate(area, area.parent);
                copy.name = "Notices";
                TMP_Text copyLine = copy.Find(template.name).GetComponent<TMP_Text>();
                notices = copy.GetComponent<MessageFeedView>();
                if (notices == null)
                {
                    notices = copy.gameObject.AddComponent<MessageFeedView>();
                    EditorUtility.CopySerialized(pickups, notices);
                }

                var noticeLine = new SerializedObject(notices);
                noticeLine.FindProperty("lineTemplate").objectReferenceValue = copyLine;
                noticeLine.ApplyModifiedPropertiesWithoutUndo();
            }

            pickupData.FindProperty("showPosted").boolValue = false;
            pickupData.FindProperty("showPickups").boolValue = true;
            pickupData.FindProperty("followPlayer").boolValue = true;
            pickupData.ApplyModifiedPropertiesWithoutUndo();

            var noticeData = new SerializedObject(notices);
            noticeData.FindProperty("showPosted").boolValue = true;
            noticeData.FindProperty("showPickups").boolValue = false;
            noticeData.FindProperty("followPlayer").boolValue = false;
            noticeData.ApplyModifiedPropertiesWithoutUndo();

            var noticeTemplate = (TMP_Text)noticeData.FindProperty("lineTemplate").objectReferenceValue;
            var noticeArea = (RectTransform)noticeTemplate.transform.parent;
            noticeArea.anchorMin = noticeArea.anchorMax = Vector2.zero;
            noticeArea.pivot = Vector2.zero;
            noticeArea.anchoredPosition = new Vector2(36f, 232f);
            noticeArea.sizeDelta = new Vector2(620f, 260f);
            noticeTemplate.alignment = TextAlignmentOptions.Left;

            string layouts = Tidy(area, TextAnchor.LowerRight) + Tidy(noticeArea, TextAnchor.LowerLeft);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            return $"feed split ({layouts})";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // A line that wraps takes the room of two, so the next doesn't print over it.
    private static string Tidy(RectTransform area, TextAnchor alignment)
    {
        var layout = area.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            return area.name + " has no layout group; ";
        }

        layout.childAlignment = alignment;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.spacing = 4f;
        foreach (LayoutElement fixedSize in area.GetComponentsInChildren<LayoutElement>(true))
        {
            fixedSize.preferredHeight = -1f;
            fixedSize.minHeight = -1f;
        }

        return area.name + " ok; ";
    }

    private static void Strings(SerializedProperty list, string[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }

    private static void Lines(SerializedProperty list, Dictionary<CompanionMemory, string> lines)
    {
        list.arraySize = lines.Count;
        int i = 0;
        foreach (KeyValuePair<CompanionMemory, string> entry in lines)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i++);
            row.FindPropertyRelative("kind").intValue = (int)entry.Key;
            Strings(row.FindPropertyRelative("lines"), new[] { entry.Value });
        }
    }
}
