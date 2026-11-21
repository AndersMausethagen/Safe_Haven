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
    private static string Done => UIStyle.Current.GoodHex;
    private static string Muted => UIStyle.Current.MutedHex;

    [SerializeField] private TMP_Text label;
    [Tooltip("Hidden when there are no active quests.")]
    [SerializeField] private GameObject panel;
    [SerializeField, Min(1)] private int maxQuests = 4;

    private QuestWaypointHud waypoints;

    private void OnEnable()
    {
        QuestManager.Changed += Refresh;
        QuestTimers.Changed += Refresh;
        GameInput.KeysChanged += Refresh;
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
        GameInput.KeysChanged -= Refresh;
    }

    private float nextTick;
    private bool timed;
    private bool wasOnARaid;

    private void Update()
    {
        bool onARaid = RaidDirector.Current != null;
        if (onARaid != wasOnARaid || HudRules.Current != writtenFor)
        {
            wasOnARaid = onARaid;
            writtenFor = HudRules.Current;
            Refresh();
        }

        if (timed && Time.time >= nextTick)
        {
            nextTick = Time.time + 1f;
            Refresh();
        }
        else if (onARaid && Time.time >= nextRaidLook)
        {
            // Someone a raid's event brings in later (a stranger to talk to) makes their quest one that can be done here.
            nextRaidLook = Time.time + 5f;
            Refresh();
        }
    }

    private float nextRaidLook;
    private HudRules.Density writtenFor = (HudRules.Density)(-1);

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
        QuestManager manager = QuestManager.Current;
        if (manager != null && !onARaid)
            foreach (QuestDefinition quest in manager.Quests)
                if (quest != null && !string.IsNullOrWhiteSpace(quest.AvailableObjective) &&
                    GameSession.Quests.StatusOf(quest) == QuestStatus.NotStarted && GameSession.Quests.PrerequisitesMet(quest))
                    available.Add(quest);

        // The main story on top, then rescues, the haven, tales and errands.
        string pinned = GameSession.Progress.Journal.PinnedQuest;
        active.Sort((a, b) => InTrackerOrder(a, b, pinned));
        int hidden = OnePerKind(active, pinned);
        available.Sort(ByRankThenName);
        // What the waypoint arrow may lead to: what is shown here (it led to a quest hidden behind another of its colour).
        Shown.Clear();
        foreach (QuestLog.Entry entry in active)
        {
            Shown.Add(entry.Quest);
        }

        foreach (QuestDefinition quest in available)
        {
            Shown.Add(quest);
        }

        panel.SetActive(active.Count + available.Count + atHome > 0);
        var builder = new StringBuilder();
        timed = false;
        // A Contextual HUD (HudRules): the quest on top in full, its finished and not-yet-open steps left out; the rest by their titles.
        // Minimal: the quest on top alone. Three quests with every step stood down the right edge (the HUD review of 2026-11-18).
        bool everything = HudRules.ShowsEverything;
        int limit = HudRules.IsMinimal ? 1 : maxQuests;
        for (int q = 0; q < active.Count && q < limit; q++)
        {
            QuestLog.Entry entry = active[q];
            builder.Append(q > 0 ? (everything ? "\n\n" : "\n") : "").Append(Title(entry.Quest));
            if (QuestTimers.TryGet(entry.Quest, out float left))
            {
                timed = true;
                builder.Append($"  <color={Muted}>{(int)left / 60}:{(int)left % 60:00}</color>");
            }
            if (q > 0 && !everything)
            {
                continue;
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
                    if (everything) builder.Append($"\n<color={Done}>• {line}  (done)</color>");
                }
                else if (entry.IsObjectiveOpen(i))
                {
                    builder.Append("\n• ").Append(line);
                    string blocker = objective.Blocker();
                    if (blocker != null)
                    {
                        builder.Append($"\n   <color={Muted}>{blocker}</color>");
                    }
                }
                else if (everything)
                {
                    builder.Append($"\n<color={Muted}>• {line}</color>");
                }
            }
        }

        int shown = Mathf.Min(active.Count, limit);
        foreach (QuestDefinition quest in available)
        {
            if (shown >= limit) break;
            bool waiting = manager.IsWaiting(quest);
            string instruction = waiting && DialogueBox.IsSpeaking
                ? "Finish the conversation to continue." : quest.AvailableObjective;
            builder.Append(shown > 0 ? (everything ? "\n\n" : "\n") : "").Append(Title(quest));
            if (shown == 0 || everything)
            {
                builder.Append("\n• ").Append(instruction);
            }

            timed |= waiting;
            shown++;
        }

        int more = hidden + Mathf.Max(0, active.Count + available.Count - limit);
        if (more > 0)
        {
            builder.Append($"\n\n<color={Muted}>+{more} more in the journal</color>");
        }

        if (atHome > 0)
        {
            builder.Append(builder.Length > 0 ? "\n\n" : "").Append($"<color={Muted}>{atHome} more at home</color>");
        }

        string nextRescue = NextRescueWaitsFor(manager);
        if (nextRescue != null)
        {
            builder.Append(builder.Length > 0 ? "\n\n" : "").Append($"<color={Muted}>{nextRescue}</color>");
        }

        label.text = GameInput.WithRebinds(builder.ToString());
    }

    // While no rescue is under way, what the next one waits for (the systems review, 2026-11-21: a hidden gate reads as a lock, a gate
    // tied to something in the world reads as story): "Someone Holding the Line: after Copper Fire". Only the nearest: one quest left.
    private static string NextRescueWaitsFor(QuestManager manager)
    {
        QuestLog log = manager != null ? manager.Log : null;
        if (log == null)
        {
            return null;
        }

        QuestDefinition next = null;
        QuestDefinition before = null;
        foreach (QuestDefinition quest in manager.Quests)
        {
            if (quest == null || quest.Rank != QuestRank.Rescue)
            {
                continue;
            }

            QuestStatus status = log.StatusOf(quest);
            if (status == QuestStatus.Active || status == QuestStatus.ReadyToTurnIn)
            {
                return null;
            }

            if (status != QuestStatus.NotStarted || next != null)
            {
                continue;
            }

            QuestDefinition missing = null;
            int open = 0;
            foreach (QuestDefinition prerequisite in quest.Prerequisites)
            {
                if (prerequisite != null && log.StatusOf(prerequisite) != QuestStatus.Completed)
                {
                    missing = prerequisite;
                    open++;
                }
            }

            if (open == 1)
            {
                next = quest;
                before = missing;
            }
        }

        return next != null ? $"Next to be found: {next.DisplayName}, after {before.DisplayName}" : null;
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

        HashSet<Object> here = null;
        for (int i = 0; i < entry.Counts.Length; i++)
        {
            QuestObjective objective = entry.Quest.Objectives[i];
            if (!entry.IsObjectiveOpen(i))
            {
                continue;
            }

            // Talking to someone, using or reaching something: only where they are. "Listen to the portal" showed on every raid,
            // the portal being at home (the HUD review of 2026-11-18).
            bool deed = objective.Type == ObjectiveType.TalkTo || objective.Type == ObjectiveType.UseObject || objective.Type == ObjectiveType.ReachPlace;
            if (deed)
            {
                here ??= DeedTargetsHere();
                if (objective.DeedTarget != null && here.Contains(objective.DeedTarget) || HasWaypointHere(entry.Quest))
                {
                    return true;
                }
            }
            else if (objective.CanProgressOnARaid || (objective.Type == ObjectiveType.QuestEvent && HasWaypointHere(entry.Quest)))
            {
                return true;
            }
        }

        return false;
    }

    // Whom one can talk to and what one can use or reach in the scenes loaded now: everything that raises those deeds.
    private static HashSet<Object> DeedTargetsHere()
    {
        var found = new HashSet<Object>();
        foreach (Talkable talkable in Object.FindObjectsByType<Talkable>(FindObjectsSortMode.None))
        {
            if (talkable.QuestIdentity != null) found.Add(talkable.QuestIdentity);
        }

        foreach (HomePortal portal in Object.FindObjectsByType<HomePortal>(FindObjectsSortMode.None))
        {
            if (portal.QuestTarget != null) found.Add(portal.QuestTarget);
        }

        foreach (CarvedStone stone in Object.FindObjectsByType<CarvedStone>(FindObjectsSortMode.None))
        {
            if (stone.QuestTarget != null) found.Add(stone.QuestTarget);
        }

        foreach (GameEventTrigger trigger in Object.FindObjectsByType<GameEventTrigger>(FindObjectsSortMode.None))
        {
            if (trigger.Target != null) found.Add(trigger.Target);
        }

        return found;
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

    /// <summary>
    /// Keeps the first quest of each kind (colour) and the pinned one, in their order, and returns how many it left out: the tracker
    /// shows one gold, one teal, one green... and the rest are in the journal (the user, 2026-11-15: too many at once after the first
    /// raid). Anything on a clock stays, as it can't wait.
    /// </summary>
    /// <summary>The quests the tracker shows now (under way, one of each kind and the pinned one; to hand in; ready to take): the
    /// waypoint arrow leads to these first. Empty with no tracker.</summary>
    public static readonly HashSet<QuestDefinition> Shown = new HashSet<QuestDefinition>();

    // Entering Play doesn't reload scripts in this project: statics carry over unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Shown.Clear();

    public static int OnePerKind(List<QuestLog.Entry> sorted, string pinned)
    {
        var seen = new HashSet<QuestRank>();
        int before = sorted.Count;
        sorted.RemoveAll(entry =>
        {
            bool firstOfKind = seen.Add(entry.Quest.Rank); // the pinned one is sorted first, so it takes its kind's place
            // One waiting to be handed in always shows: "Return to X" hidden behind another of its colour leaves it forgotten.
            return !(firstOfKind || entry.Quest.Id == pinned || entry.Status == QuestStatus.ReadyToTurnIn || QuestTimers.TryGet(entry.Quest, out _));
        });
        return before - sorted.Count;
    }

    /// <summary>The tracker's order: by kind (QuestMarkerSettings.RankOrder), then by name.</summary>
    /// <summary>The tracker's order of quests under way: the pinned one first, then the main story, rescues, the Haven, tales and errands.
    /// What else shows "the quest in hand" (the portal's window) orders by this, so the two never name different quests.</summary>
    public static int InTrackerOrder(QuestLog.Entry a, QuestLog.Entry b, string pinned) =>
        (a.Quest.Id == pinned) != (b.Quest.Id == pinned) ? (a.Quest.Id == pinned ? -1 : 1) : ByRankThenName(a.Quest, b.Quest);

    public static int ByRankThenName(QuestDefinition a, QuestDefinition b)
    {
        int byRank = QuestMarkerSettings.RankOrder(a.Rank).CompareTo(QuestMarkerSettings.RankOrder(b.Rank));
        if (byRank != 0)
        {
            return byRank;
        }

        // Within a colour, what a person gives comes first (the quest flow review's C3): "The Alchemist" was hidden behind the
        // portal's "A ..." quests, which sorted ahead by name.
        bool aPerson = ResidentQuests.IsGivenByAPerson(a), bPerson = ResidentQuests.IsGivenByAPerson(b);
        return aPerson != bPerson ? (aPerson ? -1 : 1) : string.CompareOrdinal(a.DisplayName, b.DisplayName);
    }

    private static string Title(QuestDefinition quest)
    {
        string name = $"<b><color={QuestMarkerSettings.Current.RankHex(quest.Rank)}>{quest.DisplayName}</color></b>";
        return quest.Rank == QuestRank.MainStory ? name : $"{name}  <size=75%><color={Muted}>{QuestMarkerSettings.RankName(quest.Rank)}</color></size>";
    }
}
