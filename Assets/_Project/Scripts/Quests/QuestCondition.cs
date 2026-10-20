using System;
using UnityEngine;

/// <summary>
/// "Only after quest X has started / is done", as a field any component can show in the Inspector. Leave the quest
/// empty for no condition. QuestGate uses the same rule for interactions.
/// </summary>
[Serializable]
public struct QuestCondition
{
    [Tooltip("Optional. The quest this depends on; empty means always.")]
    [SerializeField] private QuestDefinition quest;
    [SerializeField] private QuestGate.Requirement requirement;

    public QuestCondition(QuestDefinition quest, QuestGate.Requirement requirement)
    {
        this.quest = quest;
        this.requirement = requirement;
    }

    public QuestDefinition Quest => quest;
    public QuestGate.Requirement Requirement => requirement;

    /// <summary>True when there is no quest, or the player's quest log meets the requirement.</summary>
    public bool IsMet => IsMetBy(GameSession.Quests);

    public bool IsMetBy(QuestLog log)
    {
        return Check(quest, requirement, log);
    }

    public static bool Check(QuestDefinition quest, QuestGate.Requirement requirement, QuestLog log)
    {
        if (quest == null)
        {
            return true;
        }

        QuestStatus status = log.StatusOf(quest);
        return requirement == QuestGate.Requirement.Started ? status != QuestStatus.NotStarted : status == QuestStatus.Completed;
    }
}
