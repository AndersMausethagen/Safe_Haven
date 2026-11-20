using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A rescued resident's quest line starts the moment they are saved (the user, 2026-11-20: "When we save Ingrid, or any other NPC,
/// they should start a quest immediately where we learn what they can help us with"), and the building they work at is only learned
/// once that first quest has started ("the purification altar should not be unlocked until after starting that quest").
/// <para>
/// The first quest of ResidentDefinition › Quest Line starts at the rescue whatever its prerequisites and is never held for a
/// homecoming; its giver (the resident) still says the offer's words when first spoken to (QuestGiver). ResidentNpc.TeachWhatTheyKnow
/// asks <see cref="MayTeach"/> before teaching a resident's workplaces. A resident already at home in an older save gets the same:
/// their first quest starts when the player next arrives (ResidentNpc.TeachEveryoneHome).
/// </para>
/// </summary>
public static class ResidentQuests
{
    /// <summary>The first quest of the resident's quest line, or null when they have none.</summary>
    public static QuestDefinition FirstQuest(ResidentDefinition resident)
    {
        if (resident == null || resident.QuestLine == null)
        {
            return null;
        }

        foreach (QuestOffer offer in resident.QuestLine)
        {
            if (offer != null && offer.Quest != null)
            {
                return offer.Quest;
            }
        }

        return null;
    }

    /// <summary>True when the resident's workplaces may be taught: they have no quest line, or its first quest has started.</summary>
    public static bool MayTeach(ResidentDefinition resident)
    {
        QuestDefinition first = FirstQuest(resident);
        QuestLog log = Log;
        return first == null || log == null || log.StatusOf(first) != QuestStatus.NotStarted;
    }

    /// <summary>
    /// Starts the resident's first quest if it hasn't started (prerequisites aside: a rescue is reason enough), then teaches the player
    /// their workplaces, announced. Returns true when the quest started now.
    /// </summary>
    public static bool StartFirst(ResidentDefinition resident, GameObject user, bool announce = true)
    {
        QuestDefinition first = FirstQuest(resident);
        QuestLog log = Log;
        bool started = false;
        if (first != null && log != null && log.StatusOf(first) == QuestStatus.NotStarted && !log.IsClosed(first))
        {
            started = log.TryStart(first, prerequisitesAside: true);
        }

        ResidentNpc.TeachWhatTheyKnow(resident, user, announce);
        return started;
    }

    private static QuestLog Log => QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;

    private static HashSet<QuestDefinition> givenByPeople;

    // Entering Play doesn't reload scripts in this project: the set is made again each session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => givenByPeople = null;

    /// <summary>True when <paramref name="quest"/> is in some resident's Quest Line: a person gives it (the tracker puts it first).</summary>
    public static bool IsGivenByAPerson(QuestDefinition quest)
    {
        if (quest == null)
        {
            return false;
        }

        if (givenByPeople == null || !Application.isPlaying)
        {
            givenByPeople = new HashSet<QuestDefinition>();
            ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
            foreach (ResidentDefinition person in content != null ? content.Residents : (IReadOnlyList<ResidentDefinition>)System.Array.Empty<ResidentDefinition>())
            {
                foreach (QuestOffer offer in person != null && person.QuestLine != null ? person.QuestLine : (IReadOnlyList<QuestOffer>)System.Array.Empty<QuestOffer>())
                {
                    if (offer != null && offer.Quest != null)
                    {
                        givenByPeople.Add(offer.Quest);
                    }
                }
            }
        }

        return givenByPeople.Contains(quest);
    }
}
