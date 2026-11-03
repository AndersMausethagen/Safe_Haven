using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The quest tracker on screen: every active quest with its objectives and progress, done objectives in green, and
/// "Return to ..." for quests waiting to be handed in, and the time left on timed quests (QuestTimers). It only reads the
/// QuestLog, plus authored Available Objective instructions before acceptance. It refreshes when quests or registered
/// timers change (and every second while a timer runs). On a raid it shows only what can be done there and says how many
/// wait at home. It also makes the waypoint arrow that points the way to the active quest (QuestWaypointHud).
/// </summary>
public sealed class QuestTrackerView : MonoBehaviour
{
    private const string Done = "#8BE08B";
    private const string Muted = "#AAB0BC";

    [SerializeField] private TMP_Text label;
    [Tooltip("Hidden when there are no active quests.")]
    [SerializeField] private GameObject panel;
    [SerializeField, Min(1)] private int maxQuests = 4;

    private QuestWaypointHud waypoints;

    private void OnEnable()
    {
        QuestManager.Changed += Refresh;
        QuestTimers.Changed += Refresh;
        Refresh();
        if (waypoints == null && Application.isPlaying)
        {
            waypoints = QuestWaypointHud.Create();
        }
    }

    private void OnDisable()
    {
        QuestManager.Changed -= Refresh;
        QuestTimers.Changed -= Refresh;
    }

    private float nextTick;
    private bool timed;
    private bool wasOnARaid;

    private void Update()
    {
        bool onARaid = RaidDirector.Current != null;
        if (onARaid != wasOnARaid)
        {
            wasOnARaid = onARaid;
            Refresh();
        }

        if (timed && Time.time >= nextTick)
        {
            nextTick = Time.time + 1f;
            Refresh();
        }
    }

    public void Refresh()
    {
        bool onARaid = RaidDirector.Current != null;
        int atHome = 0;
        var active = new List<QuestLog.Entry>();
        foreach (QuestLog.Entry entry in GameSession.Quests.Entries)
        {
            if (entry.Status == QuestStatus.Active || entry.Status == QuestStatus.ReadyToTurnIn)
            {
                if (onARaid && !CanBeDoneOnThisRaid(entry))
                {
                    atHome++;
                }
                else
                {
                    active.Add(entry);
                }
            }
        }

        var available = new List<QuestDefinition>();
        QuestManager manager = QuestManager.Instance;
        if (manager != null && !onARaid)
            foreach (QuestDefinition quest in manager.Quests)
                if (quest != null && !string.IsNullOrWhiteSpace(quest.AvailableObjective) &&
                    GameSession.Quests.StatusOf(quest) == QuestStatus.NotStarted && GameSession.Quests.PrerequisitesMet(quest))
                    available.Add(quest);

        // The main story on top, then side quests, then errands.
        active.Sort((a, b) => a.Quest.Rank != b.Quest.Rank ? a.Quest.Rank.CompareTo(b.Quest.Rank) : string.CompareOrdinal(a.Quest.DisplayName, b.Quest.DisplayName));
        available.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : string.CompareOrdinal(a.DisplayName, b.DisplayName));
        panel.SetActive(active.Count + available.Count + atHome > 0);
        var builder = new StringBuilder();
        timed = false;
        for (int q = 0; q < active.Count && q < maxQuests; q++)
        {
            QuestLog.Entry entry = active[q];
            builder.Append(q > 0 ? "\n\n" : "").Append(Title(entry.Quest));
            if (QuestTimers.TryGet(entry.Quest, out float left))
            {
                timed = true;
                builder.Append($"  <color={Muted}>{(int)left / 60}:{(int)left % 60:00}</color>");
            }
            if (entry.Status == QuestStatus.ReadyToTurnIn)
            {
                builder.Append($"\n<color={Done}>• Return to {entry.Quest.TurnInTo}</color>");
                continue;
            }
            for (int i = 0; i < entry.Counts.Length; i++)
            {
                QuestObjective objective = entry.Quest.Objectives[i];
                string line = objective.Amount > 1 ? $"{objective.Describe()}  {entry.Counts[i]}/{objective.Amount}" : objective.Describe();
                if (entry.IsObjectiveDone(i))
                {
                    builder.Append($"\n<color={Done}>• {line}  (done)</color>");
                }
                else if (entry.IsObjectiveOpen(i))
                {
                    builder.Append("\n• ").Append(line);
                }
                else
                {
                    builder.Append($"\n<color={Muted}>• {line}</color>");
                }
            }
        }

        int shown = Mathf.Min(active.Count, maxQuests);
        foreach (QuestDefinition quest in available)
        {
            if (shown >= maxQuests) break;
            bool waiting = manager.IsWaiting(quest);
            string instruction = waiting && DialogueBox.IsSpeaking
                ? "Finish the conversation to continue." : quest.AvailableObjective;
            builder.Append(shown > 0 ? "\n\n" : "").Append(Title(quest))
                .Append("\n• ").Append(instruction);
            timed |= waiting;
            shown++;
        }

        if (active.Count + available.Count > maxQuests)
        {
            builder.Append($"\n\n<color={Muted}>+{active.Count + available.Count - maxQuests} more</color>");
        }

        if (atHome > 0)
        {
            builder.Append(builder.Length > 0 ? "\n\n" : "").Append($"<color={Muted}>{atHome} more at home</color>");
        }

        label.text = GameInput.WithRebinds(builder.ToString());
    }

    // Anything with a clock, and anything with an open step that can be done out here.
    private static bool CanBeDoneOnThisRaid(QuestLog.Entry entry)
    {
        if (QuestTimers.TryGet(entry.Quest, out _))
        {
            return true;
        }

        if (entry.Status != QuestStatus.Active)
        {
            return false;
        }

        for (int i = 0; i < entry.Counts.Length; i++)
        {
            QuestObjective objective = entry.Quest.Objectives[i];
            if (entry.IsObjectiveOpen(i) && (objective.CanProgressOnARaid || (objective.Type == ObjectiveType.QuestEvent && HasWaypointHere(entry.Quest))))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasWaypointHere(QuestDefinition quest)
    {
        foreach (QuestWaypoint waypoint in QuestWaypoint.Active)
        {
            if (waypoint != null && waypoint.Quest == quest)
            {
                return true;
            }
        }

        return false;
    }

    private static string Title(QuestDefinition quest)
    {
        string name = $"<b><color={QuestMarkerSettings.Current.RankHex(quest.Rank)}>{quest.DisplayName}</color></b>";
        return quest.Rank == QuestRank.MainStory ? name : $"{name}  <size=75%><color={Muted}>{QuestMarkerSettings.RankName(quest.Rank)}</color></size>";
    }
}
