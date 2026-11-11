using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Loose ends across every quest, story, gate and the portal, the faults a single quest's checklist can't see: a quest that
/// can never start (or waits for one that can't), something a story waits for that nothing raises, a portal moment that
/// swallows the click with no story to play, a rescue no raid places. QuestAuditTests asks for none; the first audit
/// (2026-11-11) found a quest waiting on a removed Drinking Spot and the Road North finishing before its conversation.
/// Reads saved files only (QuestSceneIndex), so no scene is opened.
/// </summary>
public static class QuestAudit
{
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";
    private static readonly Regex MomentPattern = new Regex(@"moment: \{fileID: \d+, guid: ([0-9a-f]{32})");

    public static List<QuestDefinition> Quests() =>
        AssetDatabase.FindAssets("t:QuestDefinition").Select(g => AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(g))).Where(q => q != null).ToList();

    /// <summary>Why each quest that can never start can't: nobody gives it, its event is never raised, or one before it can't start.</summary>
    public static Dictionary<QuestDefinition, string> CantStart()
    {
        List<QuestDefinition> quests = Quests();
        var listed = new HashSet<QuestDefinition>(Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).Quests);
        var why = new Dictionary<QuestDefinition, string>();

        string Blocked(QuestDefinition quest, HashSet<QuestDefinition> seen)
        {
            if (quest == null) return "a quest that no longer exists";
            if (why.TryGetValue(quest, out string known)) return known;
            if (!seen.Add(quest)) return "a loop of prerequisites";
            string reason = !listed.Contains(quest) ? "it isn't in the ContentDatabase"
                : quest.Start == QuestStart.ByQuestGiver && !QuestSceneIndex.HasGiver(quest) && !QuestSceneIndex.IsStartedByStory(quest) ? "nobody gives it and no story starts it"
                : quest.Start == QuestStart.OnQuestEvent && (quest.StartEvent == null || !QuestSceneIndex.IsRaised(quest.StartEvent, quests)) ? $"nothing raises {Name(quest.StartEvent)}, which starts it"
                : quest.Start == QuestStart.WhenPlayerDoes && (quest.StartWhen == null || !IsSomewhere(quest.StartWhen.DeedTarget)) ? "nothing in a scene is what the player must do it to"
                : null;
            foreach (QuestDefinition before in quest.Prerequisites.Concat(quest.StartedFirst))
            {
                string inner = reason == null ? Blocked(before, seen) : null;
                if (inner != null) reason = $"it waits for {Name(before)}, which can't start ({inner})";
            }

            seen.Remove(quest);
            why[quest] = reason;
            return reason;
        }

        foreach (QuestDefinition quest in quests) Blocked(quest, new HashSet<QuestDefinition>());
        return why.Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    /// <summary>What a story or gate waits for that can never come: a Quest Event nothing raises, a quest that can't start.</summary>
    public static List<string> WaitsForNothing()
    {
        List<QuestDefinition> quests = Quests();
        var found = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:GameEventDefinition"))
        {
            var gameEvent = AssetDatabase.LoadAssetAtPath<GameEventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            List<QuestSceneUse> waits = QuestSceneIndex.UsesOf(gameEvent).Where(u => u.Role == QuestSceneRole.Listens).ToList();
            if (waits.Count > 0 && !QuestSceneIndex.IsRaised(gameEvent, quests))
            {
                found.Add($"{string.Join(", ", waits.Select(u => u.Describe()).Distinct())} waits for {gameEvent.name}, which nothing raises");
            }
        }

        foreach (KeyValuePair<QuestDefinition, string> stuck in CantStart())
        {
            foreach (QuestSceneUse use in QuestSceneIndex.UsesOf(stuck.Key).Where(u => u.Role == QuestSceneRole.Listens || u.Role == QuestSceneRole.Gates))
            {
                found.Add($"{use.Describe()} waits for {stuck.Key.name}, which can't start: {stuck.Value}");
            }
        }

        return found;
    }

    /// <summary>
    /// Portal whispers whose moment no story listens for. Using the portal during the quest raises the moment instead of
    /// opening the menu (HomePortal), so with no story to play it the click does nothing, once.
    /// </summary>
    public static List<string> SilentPortalMoments()
    {
        var found = new List<string>();
        foreach (string document in File.ReadAllText(HomeScene).Split(new[] { "\n--- !u!" }, System.StringSplitOptions.None).Where(d => d.Contains("questWhispers:")))
        {
            foreach (Match moment in MomentPattern.Matches(document))
            {
                var gameEvent = AssetDatabase.LoadAssetAtPath<GameEventDefinition>(AssetDatabase.GUIDToAssetPath(moment.Groups[1].Value));
                if (gameEvent != null && !QuestSceneIndex.UsesOf(gameEvent).Any(u => u.Role == QuestSceneRole.Listens))
                {
                    found.Add($"the portal raises {gameEvent.name} for a quest, but no story plays it");
                }
            }
        }

        return found;
    }

    /// <summary>People a quest asks to be found and brought home that no raid rumour, raid event or prefab places.</summary>
    public static List<string> UnplacedRescues()
    {
        var texts = Directory.GetFiles("Assets/_Project/Data/Raids", "*.asset", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("Assets/_Project/Prefabs", "*.prefab", SearchOption.AllDirectories)).Select(File.ReadAllText).ToList();
        var found = new List<string>();
        foreach (QuestDefinition quest in Quests())
        {
            foreach (QuestObjective objective in quest.Objectives.Where(o => o != null && o.Type == ObjectiveType.RescuePerson && o.Person != null))
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(objective.Person));
                if (!texts.Any(t => t.Contains(guid)))
                {
                    found.Add($"{quest.name}: nothing on a raid places {objective.Person.name}");
                }
            }
        }

        return found.Distinct().ToList();
    }

    /// <summary>Each quest's own checklist (QuestChecklist), the lines that say something is missing.</summary>
    public static List<string> Missing() =>
        Quests().SelectMany(q => QuestChecklist.For(q).Where(c => c.Status == QuestCheck.State.Missing).Select(c => $"{q.name}: {c.Text}")).ToList();

    private static bool IsSomewhere(Object target) => !(target is QuestTargetDefinition) || QuestSceneIndex.MarkedAs(target).Count > 0;

    private static string Name(Object asset) => asset != null ? asset.name : "nothing";
}
