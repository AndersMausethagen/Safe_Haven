using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The playtest log: whether runs are logged, and the milestones the Run Comparison window measures each run against
/// (GDD §21, the first 90 minutes). Edited on the Systems Editor's Playtest page.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Playtest", fileName = "PlaytestSettings")]
public sealed class PlaytestSettings : GameSettings<PlaytestSettings>
{
    [Tooltip("Write a log for every run, in builds too. Each run is one file in the Playtests folder next to the saves.")]
    [SerializeField] private bool logRuns = true;

    [Tooltip("Moments to time in every run. The first log row that matches marks the milestone.")]
    [SerializeField] private List<PlaytestMilestone> milestones = new List<PlaytestMilestone>
    {
        // Minutes from New Game, the prologue included (Docs/QuestRedesign.md).
        new PlaytestMilestone("Awake", "QuestCompleted", "Awakening", "", 16f),
        new PlaytestMilestone("First fire", "QuestCompleted", "FirstCamp", "", 19f),
        new PlaytestMilestone("Portal awakened", "QuestCompleted", "AwakeningThePortal", "", 24f),
        new PlaytestMilestone("First raid over", "RaidFinished", "", "", 42f),
        new PlaytestMilestone("First extraction", "RaidFinished", "", "Extracted", 42f),
        new PlaytestMilestone("Growing home", "QuestCompleted", "AGrowingHome", "", 120f),
        new PlaytestMilestone("First death", "PlayerDied", "", "", 0f),
    };

    public bool LogRuns => logRuns;
    public IReadOnlyList<PlaytestMilestone> Milestones => milestones;
}

/// <summary>A moment to time: the first log row with this event and, when set, this subject and detail.</summary>
[Serializable]
public sealed class PlaytestMilestone
{
    [Tooltip("Shown as the column title.")]
    public string label;
    [Tooltip("The log's event column, e.g. QuestCompleted, RaidFinished, Killed, Built, PlayerDied, SceneEntered.")]
    public string eventName;
    [Tooltip("The asset or scene name in the subject column, e.g. FirstCamp. Empty matches any.")]
    public string subject;
    [Tooltip("The detail column, e.g. Extracted for a raid. Empty matches any.")]
    public string detail;
    [Tooltip("When it should happen, in played minutes. 0 for no target.")]
    [Min(0f)] public float targetMinutes;

    public PlaytestMilestone(string label, string eventName, string subject, string detail, float targetMinutes)
    {
        this.label = label;
        this.eventName = eventName;
        this.subject = subject;
        this.detail = detail;
        this.targetMinutes = targetMinutes;
    }

    public bool Matches(PlaytestEntry entry)
    {
        return Same(eventName, entry.Event) &&
               (string.IsNullOrEmpty(subject) || Same(subject, entry.Subject)) &&
               (string.IsNullOrEmpty(detail) || Same(detail, entry.Detail));
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
