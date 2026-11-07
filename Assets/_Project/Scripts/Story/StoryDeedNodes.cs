using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Starts a story when the player does something, said the way a quest objective says it: Talk To Sigrid, Reach the
/// Western Beacon, Use the Lake Stone, Find Ingrid, Kill a Troll. One trigger for every person, place and thing: no Quest
/// Event made for each.
/// </summary>
[Serializable]
public sealed class WhenPlayerDid : StoryTrigger
{
    [Tooltip("What the player does: talk to someone, reach a place, use a thing, find someone, kill or craft something. Its amount is ignored.")]
    public QuestObjective deed = QuestObjective.Deed(ObjectiveType.TalkTo, null);

    public override string Title => "When Player: " + (deed != null ? deed.Describe() : "?");

    public override Action Listen(StoryScript script, Action fire)
    {
        Action<GameEvent> heard = raised =>
        {
            if (deed != null && raised.ForPlayerSide && deed.Matches(raised))
            {
                fire();
            }
        };
        GameEvents.Raised += heard;
        return () => GameEvents.Raised -= heard;
    }

    public override string FindProblem()
    {
        string problem = deed != null ? deed.FindProblem() : "is empty";
        return problem != null ? "the deed " + problem : null;
    }
}

/// <summary>
/// Counts a deed as done, for quests and other stories: Talk To the Portal once its lines have been said, Reach a place
/// the story walked the player to. The deed is remembered like one the player did by hand.
/// </summary>
[Serializable]
public sealed class CountAsDoneNode : StoryNode
{
    [Tooltip("The deed to count as done: Talk To, Reach Place, Use Object or Rescue Person, and who or what.")]
    public QuestObjective deed = QuestObjective.Deed(ObjectiveType.TalkTo, null);

    public override string Title => "Count As Done: " + (deed != null ? deed.Describe() : "?");
    public override string Category => "Quest";

    public override IEnumerator Run(StoryContext context)
    {
        if (deed != null && deed.DeedKind.HasValue)
        {
            Player player = context.Player;
            QuestDeeds.Raise(deed.DeedKind.Value, deed.DeedTarget, player != null ? player.gameObject : null);
        }

        yield break;
    }

    public override string FindProblem()
    {
        if (deed == null || !deed.DeedKind.HasValue)
        {
            return "choose a deed: Talk To, Reach Place, Use Object or Rescue Person";
        }

        return deed.FindProblem();
    }
}
