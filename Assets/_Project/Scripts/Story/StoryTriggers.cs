using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Story graph nodes in the "Triggers" menu. What starts a story (StoryTrigger): the scene starting, a quest event or
// quest change, a building finished, something used, the player near or striking something, a move the player makes, a
// deed done (Talk To, Reach, Use, Find, Kill), a zone cleared. Each is small: settings, a Title and Category for the
// graph's Add menu, and Run (or Listen for a trigger). See StoryNode.

[Serializable]
public sealed class WhenSceneStarts : StoryTrigger
{
    public override string Title => "When Scene Starts";

    // A frame later, so saved progress (which triggers already fired) is in before it decides.
    public override Action Listen(StoryScript script, Action fire)
    {
        Coroutine waiting = script.StartCoroutine(NextFrame(fire));
        return () =>
        {
            if (waiting != null && script != null)
            {
                script.StopCoroutine(waiting);
            }
        };
    }

    private static IEnumerator NextFrame(Action fire)
    {
        yield return null;
        fire();
    }
}

[Serializable]
public sealed class WhenQuestEvent : StoryTrigger
{
    [Tooltip("Starts when this Quest Event is raised (a zone, an interaction, another story).")]
    public GameEventDefinition questEvent;

    public override string Title => "When Story Event";

    public override Action Listen(StoryScript script, Action fire)
    {
        Action<GameEvent> heard = raised =>
        {
            if (raised.Kind == GameEventKind.Custom && raised.Subject == questEvent)
            {
                fire();
            }
        };
        GameEvents.Raised += heard;
        return () => GameEvents.Raised -= heard;
    }

    public override string FindProblem() => questEvent == null ? "choose the Quest Event" : null;
}

[Serializable]
public sealed class WhenQuest : StoryTrigger
{
    public enum Moment { Starts, IsCompleted }

    public QuestDefinition quest;
    public Moment when = Moment.Starts;
    [Tooltip("Also fires when a game is loaded into the quest while it is still under way, for what must carry on after loading (a walk, a talk the quest waits for). Turn Once off with it, and check with Has It Happened? that the moment hasn't passed.")]
    public bool alsoAfterLoading;

    public override string Title => "When Quest " + (when == Moment.Starts ? "Starts" : "Is Completed");

    public override Action Listen(StoryScript script, Action fire)
    {
        GameEventKind kind = when == Moment.Starts ? GameEventKind.QuestStarted : GameEventKind.QuestCompleted;
        Action<GameEvent> heard = raised =>
        {
            if (raised.Kind == kind && raised.Subject == quest)
            {
                fire();
            }
        };
        GameEvents.Raised += heard;
        Coroutine waiting = alsoAfterLoading ? script.StartCoroutine(IfUnderWay(fire)) : null;
        return () =>
        {
            GameEvents.Raised -= heard;
            if (waiting != null && script != null)
            {
                script.StopCoroutine(waiting);
            }
        };
    }

    // A frame later, like When Scene Starts, so saved progress is in before it decides. Pacing can start the quest in
    // that same frame; the quest's own event then fires it, so here it must already have been under way before this.
    private IEnumerator IfUnderWay(Action fire)
    {
        QuestLog before = GameSession.Quests;
        QuestStatus atStart = before.StatusOf(quest);
        yield return null;
        QuestLog log = GameSession.Quests;
        bool underWay = when == Moment.Starts
            ? log.StatusOf(quest) == QuestStatus.Active && (log != before || atStart == QuestStatus.Active)
            : log.StatusOf(quest) == QuestStatus.Completed;
        if (underWay)
        {
            fire();
        }
    }

    public override string FindProblem() => quest == null ? "choose the quest" : null;
}

[Serializable]
public sealed class WhenBuilt : StoryTrigger
{
    [Tooltip("Starts when this structure is finished. Empty: any structure.")]
    public BuildingDefinition building;

    public override string Title => "When Structure Is Built";

    public override Action Listen(StoryScript script, Action fire)
    {
        Action<GameEvent> heard = raised =>
        {
            if (raised.Kind == GameEventKind.Built && (building == null || raised.Subject == building))
            {
                fire();
            }
        };
        GameEvents.Raised += heard;
        return () => GameEvents.Raised -= heard;
    }
}

[Serializable]
[InAFriendsHaven(HavenUse.Anyone, "a story's moment, the visitor's own story")]
public sealed class WhenUsed : StoryTrigger
{
    [Tooltip("Starts when the player finishes using this object (its Interactable). Create one with the + button.")]
    public Interactable target;

    public override string Title => "When Object Is Used";

    public override Action Listen(StoryScript script, Action fire)
    {
        if (target == null)
        {
            return null;
        }

        UnityAction heard = () => fire();
        target.Completed.AddListener(heard);
        Interactable used = target;
        return () =>
        {
            if (used != null)
            {
                used.Completed.RemoveListener(heard);
            }
        };
    }

    public override string FindProblem() => target == null ? "choose the object to use" : null;
}

[Serializable]
public sealed class WhenPlayerNear : StoryTrigger
{
    [Tooltip("The place; the player coming within Radius metres of it starts the story.")]
    public Transform place;
    [Min(0.5f)] public float radius = 6f;

    public override string Title => "When Player Comes Near";

    public override Action Listen(StoryScript script, Action fire)
    {
        Coroutine watching = script.StartCoroutine(Watch(fire));
        return () =>
        {
            if (watching != null && script != null)
            {
                script.StopCoroutine(watching);
            }
        };
    }

    private IEnumerator Watch(Action fire)
    {
        var wait = new WaitForSeconds(0.2f);
        bool inside = false;
        while (true)
        {
            Player player = Player.Current;
            bool now = place != null && player != null && (player.transform.position - place.position).sqrMagnitude <= radius * radius;
            if (now && !inside)
            {
                fire();
            }

            inside = now;
            yield return wait;
        }
    }

    public override string FindProblem() => place == null ? "choose the place" : null;
}

[Serializable]
public sealed class WhenObjectHit : StoryTrigger
{
    [Tooltip("Starts each time this takes a hit (a training dummy, a gate to break). Turn Once off to count every hit.")]
    public Damageable target;

    public override string Title => "When Object Is Hit";

    public override Action Listen(StoryScript script, Action fire)
    {
        if (target == null)
        {
            return null;
        }

        Action<DamageInfo, DamageResult> heard = (hit, result) => fire();
        target.Damaged += heard;
        Damageable watched = target;
        return () =>
        {
            if (watched != null)
            {
                watched.Damaged -= heard;
            }
        };
    }

    public override string FindProblem() => target == null ? "choose what is hit" : null;
}

[Serializable]
public sealed class WhenPlayerDoes : StoryTrigger
{
    public enum Move { Dodge, Parry, DodgeThroughABlow }

    [Tooltip("What the player has to do: for a lesson, or a story that answers it.")]
    public Move move = Move.Dodge;

    public override string Title => "When Player Does: " + move;

    public override Action Listen(StoryScript script, Action fire)
    {
        // The player can arrive after the story wakes, so it hooks up once they are there.
        Action stop = null;
        Coroutine waiting = script.StartCoroutine(HookUp(fire, unhook => stop = unhook));
        return () =>
        {
            if (waiting != null && script != null)
            {
                script.StopCoroutine(waiting);
            }

            stop?.Invoke();
        };
    }

    private IEnumerator HookUp(Action fire, Action<Action> hooked)
    {
        while (Player.Current == null)
        {
            yield return null;
        }

        Player player = Player.Current;
        switch (move)
        {
            case Move.Dodge when player.TryGetComponent(out PlayerDodge dodge):
                Action<Vector3> dodged = direction => fire();
                dodge.Dodged += dodged;
                hooked(() => { if (dodge != null) dodge.Dodged -= dodged; });
                break;
            case Move.Parry when player.TryGetComponent(out PlayerGuard guard):
                Action<DamageInfo> parried = hit => fire();
                guard.Parried += parried;
                hooked(() => { if (guard != null) guard.Parried -= parried; });
                break;
            case Move.DodgeThroughABlow:
                Damageable health = player.Health;
                Action<DamageInfo> evaded = hit => fire();
                health.Evaded += evaded;
                hooked(() => { if (health != null) health.Evaded -= evaded; });
                break;
        }
    }
}

[Serializable]
public sealed class WhenZoneCleared : StoryTrigger
{
    public override bool IsFight => true;

    [Tooltip("Starts when everything this Mob Spawn Zone spawned is dead.")]
    public MobSpawnZone zone;

    public override string Title => "When Enemies Are Defeated";

    public override Action Listen(StoryScript script, Action fire)
    {
        if (zone == null)
        {
            return null;
        }

        Action heard = fire;
        zone.Cleared += heard;
        MobSpawnZone watched = zone;
        return () =>
        {
            if (watched != null)
            {
                watched.Cleared -= heard;
            }
        };
    }

    public override string FindProblem() => zone == null ? "choose the Mob Spawn Zone" : null;
}

/// <summary>
/// Starts a story once the player has been through the portal a number of times, however each raid ended (the journal's
/// record, which remembers the last 60): the portal's word about the boundary stones after the third raid. Checked as the
/// story wakes (home from a raid, or a game loaded), a frame late so saved progress is in, and whenever a raid ends.
/// </summary>
[Serializable]
public sealed class WhenRaidsDone : StoryTrigger
{
    [Tooltip("How many raids, any way they ended (up to 60).")]
    [Min(1)] public int raids = 3;

    public override string Title => $"When {raids} Raid{(raids == 1 ? "" : "s")} Are Done";

    /// <summary>True once the player has finished this many raids.</summary>
    public static bool Done(int raids) => GameSession.Progress.Journal.Raids.Count >= raids;

    public override Action Listen(StoryScript script, Action fire)
    {
        int wanted = raids;
        Coroutine waiting = script.StartCoroutine(Check(wanted, fire));
        Action<GameEvent> heard = raised =>
        {
            if (raised.Kind == GameEventKind.RaidFinished && Done(wanted))
            {
                fire();
            }
        };
        GameEvents.Raised += heard;
        return () =>
        {
            GameEvents.Raised -= heard;
            if (waiting != null && script != null)
            {
                script.StopCoroutine(waiting);
            }
        };
    }

    private static IEnumerator Check(int wanted, Action fire)
    {
        yield return null;
        if (Done(wanted))
        {
            fire();
        }
    }
}

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
