using System;
using System.Collections;
using UnityEngine;

// Story graph nodes in the "Flow" menu. Story nodes that wait or branch: a time, a quiet moment, a fight won, a walk
// finished, an event, and whether something has happened or a quest stands somewhere. Each is small: settings, a Title
// and Category for the graph's Add menu, and Run (or Listen for a trigger). See StoryNode.

[Serializable]
public sealed class WaitNode : StoryNode
{
    [Min(0f)] public float seconds = 1f;

    public override string Title => "Wait";
    public override string Category => "Flow";

    public override IEnumerator Run(StoryContext context)
    {
        yield return new WaitForSeconds(seconds);
    }
}

[Serializable]
public sealed class WaitForQuietNode : StoryNode
{
    public override string Title => "Wait For Quiet";
    public override string Category => "Flow";

    // Say and Choice do this themselves; this is for what comes without a line: a cutscene, a sound, a monster. It waits at
    // most as long as a quest does (Story Pacing › Longest Hold), so a monster nobody can reach can't hold the story for good.
    public override IEnumerator Run(StoryContext context)
    {
        yield return null;
        float giveUpAt = Time.time + StoryPacingSettings.Current.LongestHoldSeconds;
        while ((MonsterAI.AnyHuntingPlayerSide() || context.IsCatchingBreath) && Time.time < giveUpAt)
        {
            yield return null;
        }
    }
}

[Serializable]
public sealed class WaitUntilDefeatedNode : StoryNode
{
    public MobSpawnZone zone;

    public override bool IsFight => true;

    public override string Title => "Wait Until Enemies Are Defeated";
    public override string Category => "Flow";

    public override IEnumerator Run(StoryContext context)
    {
        yield return null;
        // Co-op, where another game makes the creatures, there is nothing here to defeat: waiting would never end.
        while (zone != null && !zone.IsCleared && CreatureSource.Remote == null)
        {
            yield return null;
        }
    }

    public override string FindProblem() => zone == null ? "choose the Mob Spawn Zone" : null;
}

[Serializable]
public sealed class WaitForWalkNode : StoryNode
{
    [Tooltip("Carries on once the party's walk (Walk The Party) has come this far: 0.5 is halfway. For talk that belongs to a place on the road, whatever the player does on the way.")]
    [Range(0f, 1f)] public float reaches = 0.5f;

    public override string Title => "Wait Until The Walk Reaches";
    public override string Category => "Flow";

    public override IEnumerator Run(StoryContext context)
    {
        // A frame first: the walk may have started beside this.
        yield return null;
        while (PartyWalk.Current != null && PartyWalk.Current.Progress < reaches)
        {
            yield return null;
        }
    }
}

[Serializable]
public sealed class WaitForEventNode : StoryNode
{
    [Tooltip("Carries on when this Quest Event has happened, or straight away if it already has.")]
    public GameEventDefinition questEvent;
    [Tooltip("Or after this many seconds, so the story can't get stuck on something optional. 0: it waits as long as it takes.")]
    [Min(0f)] public float orAfterSeconds = 20f;

    public override string Title => "Wait For Quest Event";
    public override string Category => "Flow";

    public override IEnumerator Run(StoryContext context)
    {
        float end = Time.time + orAfterSeconds;
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        while (!log.HasHappened(questEvent) && (orAfterSeconds <= 0f || Time.time < end))
        {
            yield return null;
        }
    }

    public override string FindProblem() => questEvent == null ? "choose the Quest Event" : null;
}

[Serializable]
public sealed class HasHappenedNode : StoryNode
{
    [Tooltip("Leaves through Yes when this Quest Event has happened in this game (saved in the quest log), through No when not.")]
    public GameEventDefinition questEvent;

    public override string Title => "Has It Happened?";
    public override string Category => "Flow";
    public override int OutputCount => 2;
    public override string OutputName(int index) => index == 0 ? "Yes" : "No";

    public override IEnumerator Run(StoryContext context)
    {
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        context.Output = log.HasHappened(questEvent) ? 0 : 1;
        yield break;
    }

    public override string FindProblem() => questEvent == null ? "choose the Quest Event" : null;
}

[Serializable]
public sealed class CheckQuestNode : StoryNode
{
    [Tooltip("Leaves through the output for where the player is with this quest (waiting to be handed in counts as Active).")]
    public QuestDefinition quest;

    public override string Title => "Check Quest";
    public override string Category => "Flow";
    public override int OutputCount => 3;
    public override string OutputName(int index) => index == 0 ? "Not Started" : index == 1 ? "Active" : "Completed";

    public override IEnumerator Run(StoryContext context)
    {
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        QuestStatus status = log.StatusOf(quest);
        context.Output = status == QuestStatus.Completed ? 2 : status == QuestStatus.NotStarted || status == QuestStatus.Lapsed ? 0 : 1;
        yield break;
    }

    public override string FindProblem() => quest == null ? "choose the quest" : null;
}
