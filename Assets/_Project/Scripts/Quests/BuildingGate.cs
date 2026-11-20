using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One building per quest, learned when that quest starts (Docs/QuestFlowReview_2026-11-20.md rule 3 and C2, approved by the user
/// 2026-11-21). A building listed in some quest's Unlocks Buildings is introduced by that quest: it is learned when one of those quests
/// starts (<see cref="TeachOnStart"/>, announced across the screen) and no other way before, so a rescue or a job no longer fills the build
/// menu with things nobody explained (<see cref="Open"/> filters what residents and jobs teach). A building no quest lists is taught as it
/// always was. The quests are the game's ContentDatabase list.
/// </summary>
public static class BuildingGate
{
    private static Dictionary<BuildingDefinition, List<QuestDefinition>> introducedBy;

    // Entering Play doesn't reload scripts in this project: the map is made again from the content each session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => introducedBy = null;

    /// <summary>The quests whose start introduces <paramref name="building"/>; empty when none does.</summary>
    public static IReadOnlyList<QuestDefinition> IntroducedBy(BuildingDefinition building)
    {
        Dictionary<BuildingDefinition, List<QuestDefinition>> map = Map();
        return building != null && map.TryGetValue(building, out List<QuestDefinition> quests) ? quests : (IReadOnlyList<QuestDefinition>)System.Array.Empty<QuestDefinition>();
    }

    /// <summary>True when <paramref name="building"/> may be learned now: no quest introduces it, or one that does has started.</summary>
    public static bool MayLearn(BuildingDefinition building, QuestLog log = null)
    {
        IReadOnlyList<QuestDefinition> quests = IntroducedBy(building);
        if (quests.Count == 0)
        {
            return true;
        }

        log ??= Log;
        foreach (QuestDefinition quest in quests)
        {
            if (log != null && log.StatusOf(quest) != QuestStatus.NotStarted)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The buildings of <paramref name="buildings"/> that may be learned now (<see cref="MayLearn"/>).</summary>
    public static List<BuildingDefinition> Open(IEnumerable<BuildingDefinition> buildings, QuestLog log = null)
    {
        var open = new List<BuildingDefinition>();
        foreach (BuildingDefinition building in buildings ?? System.Array.Empty<BuildingDefinition>())
        {
            if (building != null && MayLearn(building, log))
            {
                open.Add(building);
            }
        }

        return open;
    }

    /// <summary>Teaches <paramref name="user"/> what <paramref name="quest"/> introduces, announced: called as the quest starts.</summary>
    public static void TeachOnStart(QuestDefinition quest, GameObject user)
    {
        if (quest == null || quest.UnlocksBuildings.Count == 0 || user == null || !user.TryGetComponent(out PlayerCrafting crafting))
        {
            return;
        }

        crafting.UnlockBuildings(quest.UnlocksBuildings);
    }

    /// <summary>Quietly teaches what every quest already started introduces (loading a game, arriving in a scene).</summary>
    public static void TeachStarted(GameObject user)
    {
        QuestLog log = Log;
        if (log == null || user == null || !user.TryGetComponent(out PlayerCrafting crafting))
        {
            return;
        }

        var learned = new List<BuildingDefinition>();
        foreach (KeyValuePair<BuildingDefinition, List<QuestDefinition>> pair in Map())
        {
            foreach (QuestDefinition quest in pair.Value)
            {
                if (log.StatusOf(quest) != QuestStatus.NotStarted)
                {
                    learned.Add(pair.Key);
                    break;
                }
            }
        }

        crafting.UnlockBuildings(learned, announce: false);
    }

    private static QuestLog Log => QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;

    // Made once a play session from the content list; in the Editor outside Play every time, so edited assets count at once.
    private static Dictionary<BuildingDefinition, List<QuestDefinition>> Map()
    {
        if (introducedBy != null && Application.isPlaying)
        {
            return introducedBy;
        }

        var map = new Dictionary<BuildingDefinition, List<QuestDefinition>>();
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        foreach (QuestDefinition quest in content != null ? content.Quests : (IReadOnlyList<QuestDefinition>)System.Array.Empty<QuestDefinition>())
        {
            if (quest == null)
            {
                continue;
            }

            foreach (BuildingDefinition building in quest.UnlocksBuildings)
            {
                if (building == null)
                {
                    continue;
                }

                if (!map.TryGetValue(building, out List<QuestDefinition> quests))
                {
                    map[building] = quests = new List<QuestDefinition>();
                }

                if (!quests.Contains(quest))
                {
                    quests.Add(quest);
                }
            }
        }

        introducedBy = map;
        return map;
    }
}
