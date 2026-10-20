using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The quest tracker on screen: every active quest with its objectives and progress, done objectives in green, and
/// "Return to ..." for quests waiting to be handed in. It only reads the QuestLog, refreshing when quests change.
/// </summary>
public sealed class QuestTrackerView : MonoBehaviour
{
    private const string Done = "#8BE08B";
    private const string Muted = "#AAB0BC";

    [SerializeField] private TMP_Text label;
    [Tooltip("Hidden when there are no active quests.")]
    [SerializeField] private GameObject panel;
    [SerializeField, Min(1)] private int maxQuests = 4;

    private void OnEnable()
    {
        QuestManager.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        QuestManager.Changed -= Refresh;
    }

    public void Refresh()
    {
        var active = new List<QuestLog.Entry>();
        foreach (QuestLog.Entry entry in GameSession.Quests.Entries)
        {
            if (entry.Status == QuestStatus.Active || entry.Status == QuestStatus.ReadyToTurnIn)
            {
                active.Add(entry);
            }
        }

        active.Sort((a, b) => string.CompareOrdinal(a.Quest.DisplayName, b.Quest.DisplayName));
        panel.SetActive(active.Count > 0);
        var builder = new StringBuilder();
        for (int q = 0; q < active.Count && q < maxQuests; q++)
        {
            QuestLog.Entry entry = active[q];
            builder.Append(q > 0 ? "\n\n" : "").Append("<b>").Append(entry.Quest.DisplayName).Append("</b>");
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

        if (active.Count > maxQuests)
        {
            builder.Append($"\n\n<color={Muted}>+{active.Count - maxQuests} more</color>");
        }

        label.text = builder.ToString();
    }
}
