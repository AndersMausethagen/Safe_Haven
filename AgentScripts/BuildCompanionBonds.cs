using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What going along together comes to: ranks by raids, and what people say of a raid just gone, generally and in the expedition's own words. Safe to run again.
/// Run: unity command script run -- --file AgentScripts/BuildCompanionBonds.cs --entry BuildCompanionBonds.Run
/// </summary>
public static class BuildCompanionBonds
{
    private const string People = "Assets/_Project/Data/Prologue/Residents/";

    public static string Run()
    {
        Edit(SettlementSettings.Current, so =>
        {
            SerializedProperty ranks = so.FindProperty("bondRanks");
            ranks.arraySize = 2;
            Rank(ranks.GetArrayElementAtIndex(0), "Trusted", 3, 0.15f, 0.1f);
            Rank(ranks.GetArrayElementAtIndex(1), "Sworn", 8, 0.3f, 0.2f);
            Lines(so.FindProperty("memoryLines"), new Dictionary<CompanionMemory, string[]>
            {
                [CompanionMemory.BossBeaten] = new[] { "I keep hearing it fall. I think I always will. Good. It should be remembered that it can be done.", "We killed it. You and me. I didn't think I'd ever say that about anything out there." },
                [CompanionMemory.HelpedUp] = new[] { "You came back for me. Out there, with all of that around you. I won't forget it.", "I was on the ground and I heard you coming. That's the only reason I'm talking to you now." },
                [CompanionMemory.Failed] = new[] { "We should never have gone that deep. I'm glad you're breathing.", "It went badly. It goes badly sometimes. We go again when you're ready." },
                [CompanionMemory.RoseInRank] = new[] { "I've stopped checking whether you're behind me. You always are.", "You know how I fight now, and I know how you do. That's worth more than a good sword." },
                [CompanionMemory.CloseCall] = new[] { "Another minute out there and we'd both be part of that forest.", "I felt that portal close behind us. Let's not do that again." },
                [CompanionMemory.HardFight] = new[] { "My arms are still shaking. We gave them something to think about.", "That was a lot of them. We were better." },
            });
        });

        Own("Sigrid", new Dictionary<CompanionMemory, string[]>
        {
            [CompanionMemory.BossBeaten] = new[] { "I've buried good people on less than what we just did. Write it down somewhere. It should outlast us." },
            [CompanionMemory.HelpedUp] = new[] { "I gave you an order once to leave me if it came to that. You didn't. We'll talk about discipline. Later." },
            [CompanionMemory.Failed] = new[] { "A retreat is only a defeat if you learn nothing. What did we learn?" },
            [CompanionMemory.RoseInRank] = new[] { "I've served under worse than you. Don't let it go to your head." },
        }, "Good. On your feet. I mean, on mine.", "Back in line. Thank you.");
        Own("Eirik", new Dictionary<CompanionMemory, string[]>
        {
            [CompanionMemory.BossBeaten] = new[] { "Twenty winters I've been telling that kind of story. First time it's been true." },
            [CompanionMemory.HelpedUp] = new[] { "Don't look at me like that. I was resting. ...Thank you." },
            [CompanionMemory.CloseCall] = new[] { "My knees are too old to run from a closing portal. Next time leave earlier." },
            [CompanionMemory.HardFight] = new[] { "That many, and I'm still here. Don't tell the young ones. They'll want to try it." },
        }, "Ugh. Took your time.", "Still got one more swing in me, then.");
        Own("Runa", new Dictionary<CompanionMemory, string[]>
        {
            [CompanionMemory.BossBeaten] = new[] { "I've tracked things my whole life, and never one that big. It left nothing behind but quiet. I don't trust the quiet." },
            [CompanionMemory.HelpedUp] = new[] { "You crossed open ground to reach me. Stupid. Don't stop." },
            [CompanionMemory.CloseCall] = new[] { "I counted our steps to the exit. I'd rather not count that close again." },
            [CompanionMemory.Failed] = new[] { "The forest doesn't hate us. It doesn't notice us. That's worse. Next time I pick the way." },
        }, "Bow's still good. So am I.", "Move. I've got your back.");

        foreach (string kind in new[] { "Fighter", "Archer" })
        {
            Edit(AssetDatabase.LoadAssetAtPath<CompanionDefinition>($"Assets/_Project/Data/Companions/{kind}.asset"), so =>
                Strings(so.FindProperty("fightLines").FindPropertyRelative("helpedUp"), new[] { "Thank you. I thought that was it.", "I owe you one.", "Still here. Still here." }));
        }

        AssetDatabase.SaveAssets();
        return "bonds, memories and thanks written";
    }

    private static void Own(string person, Dictionary<CompanionMemory, string[]> lines, params string[] thanks)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ResidentDefinition>($"{People}{person}.asset");
        Edit(asset, so =>
        {
            Lines(so.FindProperty("memoryLines"), lines);
            Strings(so.FindProperty("fightLines").FindPropertyRelative("helpedUp"), thanks);
        });
    }

    private static void Rank(SerializedProperty entry, string name, int raids, float health, float damage)
    {
        entry.FindPropertyRelative("name").stringValue = name;
        entry.FindPropertyRelative("raids").intValue = raids;
        entry.FindPropertyRelative("healthBonus").floatValue = health;
        entry.FindPropertyRelative("damageBonus").floatValue = damage;
    }

    private static void Lines(SerializedProperty list, Dictionary<CompanionMemory, string[]> lines)
    {
        list.arraySize = lines.Count;
        int i = 0;
        foreach (KeyValuePair<CompanionMemory, string[]> entry in lines)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i++);
            row.FindPropertyRelative("kind").intValue = (int)entry.Key;
            Strings(row.FindPropertyRelative("lines"), entry.Value);
        }
    }

    private static void Strings(SerializedProperty list, string[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }

    private static void Edit(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
