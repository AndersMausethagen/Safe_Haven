using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks where a quest is going on (the shelter, the waystones, the stone in the clearing): while its quest is active
/// (and, with an objective chosen, while that objective is still open), the quest tracker points the way to it with an
/// arrow at the edge of the screen and the distance (QuestWaypointHud). Quick setup: GameObject › Safe Haven › Quest
/// Waypoint, then pick the quest.
/// </summary>
[AddComponentMenu("Safe Haven/Quests/Quest Waypoint")]
public sealed class QuestWaypoint : MonoBehaviour
{
    private static readonly List<QuestWaypoint> active = new List<QuestWaypoint>();

    [SerializeField] private QuestDefinition quest;
    [Tooltip("Only while this objective (1 is the first) is open. 0: for the whole quest.")]
    [SerializeField, Min(0)] private int objective;
    [Tooltip("Shown under the arrow, e.g. \"Shelter\". Empty: the quest's name.")]
    [SerializeField] private string label;

    public static IReadOnlyList<QuestWaypoint> Active => active;

    public QuestDefinition Quest => quest;
    public string Label => string.IsNullOrWhiteSpace(label) && quest != null ? quest.DisplayName : label;

    /// <summary>Sets the waypoint up (editor tools).</summary>
    public void Configure(QuestDefinition target, int objectiveNumber, string text)
    {
        quest = target;
        objective = objectiveNumber;
        label = text;
    }

    private void OnEnable() => active.Add(this);

    private void OnDisable() => active.Remove(this);

    /// <summary>True while the player should be shown the way here.</summary>
    public bool IsWanted(QuestLog log)
    {
        if (quest == null || log == null)
        {
            return false;
        }

        QuestLog.Entry entry = log.Get(quest);
        if (entry == null || entry.Status != QuestStatus.Active)
        {
            return false;
        }

        int index = objective - 1;
        return index < 0 || index >= entry.Counts.Length || !entry.IsObjectiveDone(index) && entry.IsObjectiveOpen(index);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.3f);
    }
}
