using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Story graph nodes in the "Quest" menu. Story nodes for quests: raising a quest event, starting a quest, moving its
// progress on, counting a deed as done, and giving items. Each is small: settings, a Title and Category for the graph's
// Add menu, and Run (or Listen for a trigger). See StoryNode.

[Serializable]
public sealed class RaiseQuestEventNode : StoryNode
{
    [Tooltip("Raised now: completes objectives waiting for it, starts quests and other stories that listen.")]
    public GameEventDefinition questEvent;

    public override string Title => "Raise Quest Event";
    public override string Category => "Quest";

    public override IEnumerator Run(StoryContext context)
    {
        GameEvents.Raise(questEvent, context.Player != null ? context.Player.gameObject : null);
        yield break;
    }

    public override string FindProblem() => questEvent == null ? "choose the Quest Event" : null;
}

[Serializable]
public sealed class StartQuestNode : StoryNode
{
    public QuestDefinition quest;

    public override string Title => "Start Quest";
    public override string Category => "Quest";

    public override IEnumerator Run(StoryContext context)
    {
        if (QuestManager.Current != null && !QuestManager.Current.TryStartQuest(quest, out string reason))
        {
            Debug.Log($"Story couldn't start {quest?.DisplayName}: {reason}", context.Script);
        }

        yield break;
    }

    public override string FindProblem() => quest == null ? "choose the quest" : null;
}

[Serializable]
public sealed class QuestProgressNode : StoryNode
{
    [Tooltip("Moves one objective of a quest under way on: what the player did earlier makes this quest shorter.")]
    public QuestDefinition quest;
    [Tooltip("Which objective: 1 is the first.")]
    [Min(1)] public int objective = 1;
    [Min(1)] public int amount = 1;

    public override string Title => "Add Quest Progress";
    public override string Category => "Quest";

    public override IEnumerator Run(StoryContext context)
    {
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        log.AddProgress(quest, objective - 1, amount);
        yield break;
    }

    public override string FindProblem() => quest == null ? "choose the quest" :
        objective > quest.Objectives.Count ? "the quest has no such objective" : null;
}

[Serializable]
public sealed class GiveItemsNode : StoryNode
{
    public List<ItemAmount> items = new List<ItemAmount>();
    [Tooltip("Puts on what can be worn or held right away (a two-handed weapon puts the torch away).")]
    public bool equip;

    public override string Title => "Give Items";
    public override string Category => "Quest";

    public override IEnumerator Run(StoryContext context)
    {
        if (context.Player != null && context.Player.Bag != null)
        {
            ItemGrantResult result = context.Player.Bag.Give(items);
            if (result.HasOverflow)
            {
                GameMessages.Post($"Can't carry it all: {AmountText.Describe(result.Overflow)} left behind.", MessageKind.Warning);
            }

            if (equip && context.Player.Equipment != null)
            {
                foreach (ItemAmount amount in items)
                {
                    if (amount.Item != null && amount.Item.IsEquippable)
                    {
                        context.Player.Equipment.EquipFromBag(amount.Item);
                    }
                }
            }
        }

        yield break;
    }

    public override string FindProblem() => items.Count == 0 ? "add the items" : null;
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
