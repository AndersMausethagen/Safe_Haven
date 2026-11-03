using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// The kinds of story graph nodes. Each is small: settings, a Title and Category for the graph's Add menu, and Run (or
// Listen for a trigger). See StoryNode.

// ----- Triggers -----

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

    public override string Title => "When Quest Event";

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

// ----- Talk -----

/// <summary>Who says a line in a story: the player, or someone (or something) in the scene.</summary>
[Serializable]
public sealed class StorySpeaker
{
    [Tooltip("The player speaks (their name below, or \"You\").")]
    public bool player;
    public string name;
    [Tooltip("The speaker in the scene: their face is the portrait and the camera frames them.")]
    public Transform model;
    [Tooltip("A picture instead of the live portrait. Optional.")]
    public Sprite portrait;
    [Tooltip("Heard as each of their lines starts. Optional: most people are only read.")]
    public SoundDefinition voice;
    [Tooltip("Where that voice comes from, when they have no model here: the portal, say.")]
    public Transform voiceFrom;

    public DialogueSpeaker ToSpeaker() =>
        player ? DialogueSpeaker.Player(name) : new DialogueSpeaker(string.IsNullOrWhiteSpace(name) && model != null ? model.name : name, model, portrait, 0f, voice, voiceFrom);

    public string Describe() => player ? "Player" : string.IsNullOrWhiteSpace(name) ? model != null ? model.name : "?" : name;
}

[Serializable]
public sealed class SayNode : StoryNode
{
    public StorySpeaker speaker = new StorySpeaker();
    [TextArea(2, 5)] public string line;
    [Tooltip("Waits out the pause after a fight (Systems Editor › World › Story Pacing), so a line doesn't land on the last blow. Off: right away.")]
    public bool afterFightPause = true;
    [Tooltip("Spoken aside, as talk on the road or by the fire: a subtitle that leaves the controls and the camera to the player. It waits for a conversation or another aside to end first.")]
    public bool aside;

    public override string Title => aside ? "Say Aside" : "Say";
    public override string Category => "Talk";

    public override IEnumerator Run(StoryContext context)
    {
        if (afterFightPause)
        {
            // A frame first: the last enemy's death can be what started this, and the pacing hears of it next frame.
            yield return null;
            while (context.IsCatchingBreath)
            {
                yield return null;
            }
        }

        bool done = false;
        DialogueBox box = DialogueBox.Current;
        if (aside)
        {
            while (DialogueBox.IsSpeaking)
            {
                yield return null;
            }

            box.Aside(speaker.ToSpeaker(), line, () => done = true);
            // Something else can take the strip over (a fight's line, a conversation), which drops this line's callback.
            while (!done && box.IsSubtitling)
            {
                yield return null;
            }

            yield break;
        }

        Action closed = () => done = true;
        box.Closed += closed;
        box.Say(speaker.ToSpeaker(), line, () => done = true);
        while (!done)
        {
            yield return null;
        }

        box.Closed -= closed;
    }

    public override string FindProblem() => string.IsNullOrWhiteSpace(line) ? "write the line" : null;
}

[Serializable]
public sealed class ChoiceNode : StoryNode
{
    public StorySpeaker speaker = new StorySpeaker();
    [TextArea(2, 5)] public string line;
    [Tooltip("The answers; each has its own output. Walking away leaves through no output at all.")]
    public List<string> answers = new List<string> { "Yes.", "No." };
    [Tooltip("Asks again when the player closes the box without answering (a choice the story can't go on without).")]
    public bool mustAnswer;
    [Tooltip("Waits out the pause after a fight (Systems Editor › World › Story Pacing), so the question doesn't land on the last blow. Off: right away.")]
    public bool afterFightPause = true;

    public override string Title => "Choice";
    public override string Category => "Talk";
    public override int OutputCount => Mathf.Max(1, answers.Count);
    public override string OutputName(int index) => index < answers.Count ? answers[index] : "";

    public override IEnumerator Run(StoryContext context)
    {
        if (afterFightPause)
        {
            yield return null;
            while (context.IsCatchingBreath)
            {
                yield return null;
            }
        }

        int picked = -2;
        var choices = new List<DialogueChoice>();
        for (int i = 0; i < answers.Count; i++)
        {
            QuestDefinition starts = QuestStartedBy(context.Script, i);
            choices.Add(new DialogueChoice(answers[i], null, true, starts, QuestAnswer.Starts));
        }

        while (true)
        {
            picked = -2;
            DialogueBox.Current.Ask(speaker.ToSpeaker(), line, choices, index => picked = index);
            while (picked == -2)
            {
                yield return null;
            }

            if (picked >= 0 || !mustAnswer)
            {
                break;
            }

            yield return new WaitForSeconds(1f);
        }

        context.Output = picked;
    }

    // An answer leading straight to Start Quest says so on its button.
    private QuestDefinition QuestStartedBy(StoryScript script, int answer)
    {
        if (script == null)
        {
            return null;
        }

        foreach (StoryLink link in script.Links)
        {
            if (link.from == id && link.output == answer && script.Find(link.to) is StartQuestNode start && start.quest != null)
            {
                return start.quest;
            }
        }

        return null;
    }

    public override string FindProblem() => answers.Count == 0 ? "add at least one answer" : null;
}

// ----- Flow -----

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

    // Say and Choice do this themselves; this is for what comes without a line: a cutscene, a sound, a monster.
    public override IEnumerator Run(StoryContext context)
    {
        yield return null;
        while (MonsterAI.AnyHuntingPlayerSide() || context.IsCatchingBreath)
        {
            yield return null;
        }
    }
}

[Serializable]
public sealed class WaitUntilDefeatedNode : StoryNode
{
    public MobSpawnZone zone;

    public override string Title => "Wait Until Enemies Are Defeated";
    public override string Category => "Flow";

    public override IEnumerator Run(StoryContext context)
    {
        yield return null;
        while (zone != null && !zone.IsCleared)
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
        QuestLog log = QuestManager.Instance != null ? QuestManager.Instance.Log : GameSession.Quests;
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
        QuestLog log = QuestManager.Instance != null ? QuestManager.Instance.Log : GameSession.Quests;
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
        QuestLog log = QuestManager.Instance != null ? QuestManager.Instance.Log : GameSession.Quests;
        QuestStatus status = log.StatusOf(quest);
        context.Output = status == QuestStatus.Completed ? 2 : status == QuestStatus.NotStarted || status == QuestStatus.Lapsed ? 0 : 1;
        yield break;
    }

    public override string FindProblem() => quest == null ? "choose the quest" : null;
}

// ----- Quest -----

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
        if (QuestManager.Instance != null && !QuestManager.Instance.TryStartQuest(quest, out string reason))
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
        QuestLog log = QuestManager.Instance != null ? QuestManager.Instance.Log : GameSession.Quests;
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

// ----- World -----

[Serializable]
public sealed class SpawnEnemiesNode : StoryNode
{
    [Tooltip("The zone fills itself now. Create one with the + button and draw its area in the scene.")]
    public MobSpawnZone zone;
    [Tooltip("Carry on only once everything it spawned is dead.")]
    public bool waitUntilDefeated;

    public override string Title => "Spawn Enemies";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (zone == null)
        {
            yield break;
        }

        zone.Spawn();
        while (waitUntilDefeated && !zone.IsCleared)
        {
            yield return null;
        }
    }

    public override string FindProblem() => zone == null ? "choose the Mob Spawn Zone" : null;
}

[Serializable]
public sealed class DespawnEnemiesNode : StoryNode
{
    [Tooltip("Everything this zone spawned goes away, and it stops respawning: the hunt is over, the party has moved on.")]
    public MobSpawnZone zone;

    public override string Title => "Remove Enemies";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (zone != null)
        {
            zone.Despawn();
        }

        yield break;
    }

    public override string FindProblem() => zone == null ? "choose the Mob Spawn Zone" : null;
}

[Serializable]
public sealed class SetActiveNode : StoryNode
{
    public GameObject target;
    public bool active = true;

    public override string Title => active ? "Show Object" : "Hide Object";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (target != null)
        {
            target.SetActive(active);
        }

        yield break;
    }

    public override string FindProblem() => target == null ? "choose the object" : null;
}

[Serializable]
public sealed class MoveObjectNode : StoryNode
{
    [Tooltip("What moves (a creature walks: its Animator's Speed is set while it moves).")]
    public Transform target;
    [Tooltip("Where it goes; it turns to face the way it is going.")]
    public Transform destination;
    [Min(0f)] public float seconds = 4f;
    [Tooltip("Animation playback multiplier while moving. Lower values give large creatures a slower, heavier gait.")]
    [Min(0.05f)] public float animationSpeed = 1f;
    [Tooltip("Carry on only once it has arrived.")]
    public bool waitUntilArrived;

    private static readonly int Speed = Animator.StringToHash("Speed");

    public override string Title => "Move Object";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (target == null || destination == null)
        {
            yield break;
        }

        IEnumerator moving = Move(target, destination.position, seconds, animationSpeed);
        if (waitUntilArrived)
        {
            yield return moving;
        }
        else
        {
            context.Script.StartCoroutine(moving);
        }
    }

    private static IEnumerator Move(Transform mover, Vector3 to, float seconds, float animationSpeed)
    {
        Vector3 from = mover.position;
        Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
        if (flat.sqrMagnitude > 0.01f)
        {
            mover.rotation = Quaternion.LookRotation(flat);
        }

        Animator animator = mover.GetComponentInChildren<Animator>();
        bool walks = animator != null && HasSpeed(animator);
        float previousSpeed = animator != null ? animator.speed : 1f;
        float speed = seconds > 0f ? Vector3.Distance(from, to) / seconds : 0f;
        try
        {
            if (walks) animator.speed = previousSpeed * animationSpeed;
            for (float t = 0f; t < seconds && mover != null; t += Time.deltaTime)
            {
                mover.position = Vector3.Lerp(from, to, t / seconds);
                if (walks) animator.SetFloat(Speed, speed);

                yield return null;
            }
            if (mover != null) mover.position = to;
        }
        finally
        {
            if (walks && animator != null)
            {
                animator.SetFloat(Speed, 0f);
                animator.speed = previousSpeed;
            }
        }
    }

    private static bool HasSpeed(Animator animator)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float)
            {
                return true;
            }
        }

        return false;
    }

    public override string FindProblem() => target == null ? "choose what moves" : destination == null ? "choose where it goes" :
        animationSpeed <= 0f ? "animation speed must be positive" : null;
}

[Serializable]
public sealed class SetUsableNode : StoryNode
{
    [Tooltip("Whether the player can use this object (its Interactable): off when its part in the story is over.")]
    public Interactable target;
    public bool usable;

    public override string Title => usable ? "Make Usable" : "Stop Being Usable";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (target != null)
        {
            target.enabled = usable;
        }

        yield break;
    }

    public override string FindProblem() => target == null ? "choose the object" : null;
}

[Serializable]
public sealed class GlowNode : StoryNode
{
    [Tooltip("What glows: a light appears at it (and fades out again when Glow is off). It lifts the dark around it, so it is seen at night.")]
    public Transform target;
    public bool glow = true;
    public Color colour = new Color(0.55f, 0.85f, 1f);
    [Min(0f)] public float intensity = 4f;
    [Min(0.1f)] public float range = 6f;
    [Min(0f)] public float fadeSeconds = 1.5f;

    public override string Title => glow ? "Start Glowing" : "Stop Glowing";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (target == null)
        {
            yield break;
        }

        Transform holder = target.Find("Story Glow");
        Light light = holder != null ? holder.GetComponent<Light>() : null;
        if (light == null)
        {
            var go = new GameObject("Story Glow");
            go.transform.SetParent(target, false);
            go.transform.localPosition = Vector3.up;
            light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
        }

        if (!light.TryGetComponent(out VisibilitySource sight))
        {
            sight = light.gameObject.AddComponent<VisibilitySource>();
        }

        sight.Configure(range * 0.6f, 0.5f, true, range * 0.6f);
        sight.enabled = glow;

        light.color = colour;
        light.range = range;
        float from = light.intensity;
        float to = glow ? intensity : 0f;
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            light.intensity = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }

        light.intensity = to;
    }

    public override string FindProblem() => target == null ? "choose what glows" : null;
}

[Serializable]
public sealed class WitherGroundNode : StoryNode
{
    [Tooltip("The grass around this dies and turns brown (or comes back when Wither is off).")]
    public Transform centre;
    public bool wither = true;
    [Min(0.5f)] public float radius = 6f;
    [Min(0f)] public float seconds = 3f;

    public override string Title => wither ? "Wither Ground" : "Restore Ground";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (centre == null)
        {
            yield break;
        }

        if (!centre.TryGetComponent(out WitherSpot spot))
        {
            spot = centre.gameObject.AddComponent<WitherSpot>();
            spot.Amount = 0f;
        }

        spot.Radius = radius;
        float from = spot.Amount;
        float to = wither ? 1f : 0f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            spot.Amount = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }

        spot.Amount = to;
    }

    public override string FindProblem() => centre == null ? "choose where" : null;
}

[Serializable]
public sealed class ChangeWeatherNode : StoryNode
{
    public WeatherDefinition weather;
    [Min(0f)] public float transitionSeconds = 4f;

    public override string Title => "Change Weather";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        WeatherSystem system = UnityEngine.Object.FindAnyObjectByType<WeatherSystem>();
        if (system != null)
        {
            system.SetWeather(weather, transitionSeconds);
        }
        else
        {
            Debug.LogWarning("Change Weather: the scene has no WeatherSystem (World Tools › Sky & Weather).", context.Script);
        }

        yield break;
    }

    public override string FindProblem() => weather == null ? "choose the weather" : null;
}

[Serializable]
public sealed class ChangeTimeOfDayNode : StoryNode
{
    [Tooltip("The hour the clock moves forward to (7.5 is early morning, 19 sunset, 23 night).")]
    [Range(0f, 24f)] public float hour = 21f;
    [Min(0f)] public float seconds = 20f;
    [Tooltip("Instead of Seconds, it follows the party's walk (Walk The Party): the hour arrives as they do, and the sun waits when they wait.")]
    public bool followWalk;

    public override string Title => "Change Time of Day";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        DayNightCycle cycle = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        if (cycle == null)
        {
            Debug.LogWarning("Change Time of Day: the scene has no DayNightCycle (World Tools › Sky & Weather).", context.Script);
            yield break;
        }

        if (!followWalk)
        {
            cycle.GoToHour(hour, seconds);
            yield break;
        }

        // A frame first: the walk may have started beside this.
        yield return null;
        PartyWalk walk = PartyWalk.Current;
        float from = cycle.Hour;
        float to = DayNightCycle.ForwardTo(from, hour);
        while (walk != null && !walk.IsDone)
        {
            cycle.Hour = Mathf.Lerp(from, to, walk.Progress);
            yield return null;
        }

        cycle.Hour = hour;
    }
}

[Serializable]
public sealed class ChangeLookNode : StoryNode
{
    [Tooltip("The scene's Scene Look: a second set of post-processing volumes and day and night lighting.")]
    public SceneLook look;
    [Tooltip("0: the scene's first look, 1: the second.")]
    [Range(0f, 1f)] public float amount = 1f;
    [Min(0f)] public float seconds = 8f;

    public override string Title => "Change Look";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (look != null)
        {
            look.FadeTo(amount, seconds);
        }

        yield break;
    }

    public override string FindProblem() => look == null ? "choose the Scene Look" : null;
}

[Serializable]
public sealed class WalkPartyNode : StoryNode
{
    private const float RefreshSeconds = 0.25f;
    private const float Lead = 4f;

    [Tooltip("An empty object whose children are the route's points, in order: the party walks through them.")]
    public Transform route;
    [Tooltip("Who walks. Empty: every companion in the scene.")]
    public List<Companion> companions = new List<Companion>();
    [Tooltip("The party's steady pace, in metres a second (the player's walk is faster than this).")]
    [Min(0.5f)] public float pace = 2.6f;
    [Tooltip("They stop and wait when the player falls this far behind. 0: they don't wait.")]
    [Min(0f)] public float waitForPlayer = 22f;
    [Tooltip("Carry on only once they have arrived. Off: what follows starts as they set out.")]
    public bool waitUntilArrived = true;

    public override string Title => "Walk The Party";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (route == null || route.childCount < 2)
        {
            yield break;
        }

        var points = new Vector3[route.childCount];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = route.GetChild(i).position;
        }

        var walk = new PartyWalk(points);
        PartyWalk.Current = walk;
        var party = new List<Companion>(companions.Count > 0 ? companions : new List<Companion>(Companion.Active));
        IEnumerator walking = Walk(walk, party, context);
        if (waitUntilArrived)
        {
            yield return walking;
        }
        else
        {
            context.Script.StartCoroutine(walking);
        }
    }

    private IEnumerator Walk(PartyWalk walk, List<Companion> party, StoryContext context)
    {
        foreach (Companion member in party)
        {
            member.SetPace(pace);
        }

        try
        {
            float refresh = 0f;
            while (!walk.IsDone)
            {
                if (!MustWait(walk, party, context.Player))
                {
                    walk.Advance(pace * Time.deltaTime);
                }

                refresh -= Time.deltaTime;
                if (refresh <= 0f)
                {
                    refresh = RefreshSeconds;
                    Order(walk, party);
                }

                yield return null;
            }

            // Into their places at the end of the road, where the last order leads them.
            Order(walk, party);
        }
        finally
        {
            foreach (Companion member in party)
            {
                if (member != null) member.SetPace(0f);
            }
        }
    }

    // Each walks to a spot a little ahead of them on the road (kept in formation, turned with the road), so they stride
    // steadily at the party's pace instead of catching up and stopping.
    private static void Order(PartyWalk walk, List<Companion> party)
    {
        Quaternion turn = Quaternion.LookRotation(walk.Heading(Lead));
        Vector3 spot = walk.Ahead(Lead);
        foreach (Companion member in party)
        {
            if (member != null) member.Command(CompanionOrder.Stay, spot + turn * member.FormationOffset);
        }
    }

    private bool MustWait(PartyWalk walk, List<Companion> party, Player player)
    {
        // Nobody walks off during a conversation or a fight, or from someone who has fallen behind.
        if (DialogueBox.IsOpen || MonsterAI.AnyHuntingPlayerSide())
        {
            return true;
        }

        Vector3 here = walk.Position;
        if (waitForPlayer > 0f && player != null && Flat(player.transform.position - here) > waitForPlayer && Behind(walk, player))
        {
            return true;
        }

        foreach (Companion member in party)
        {
            if (member != null && Flat(member.transform.position - here) > Lead + 8f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Behind(PartyWalk walk, Player player) =>
        Vector3.Dot(player.transform.position - walk.Position, walk.Heading()) < 0f;

    private static float Flat(Vector3 offset)
    {
        offset.y = 0f;
        return offset.magnitude;
    }

    public override string FindProblem() => route == null ? "choose the route (an object with its points as children)" :
        route.childCount < 2 ? "the route needs at least two points (children of it)" : null;
}

[Serializable]
public sealed class RevealGroundNode : StoryNode
{
    [Tooltip("The ground around this becomes explored: the fog of war lifts and it shows on the map, as if the player had already been there.")]
    public Transform centre;
    [Min(1f)] public float radius = 60f;

    public override string Title => "Reveal Ground";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (centre != null && ExplorationMap.Current != null)
        {
            ExplorationMap.Current.RevealAround(centre.position, radius);
        }

        yield break;
    }

    public override string FindProblem() => centre == null ? "choose where" : null;
}

[Serializable]
public sealed class InvokeEventNode : StoryNode
{
    [Tooltip("Anything else: any component's method or property, picked in the Inspector.")]
    public UnityEvent response = new UnityEvent();

    public override string Title => "Do Something";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        response.Invoke();
        yield break;
    }
}

// ----- Camera & Sound -----

[Serializable]
public sealed class ShakeCameraNode : StoryNode
{
    [Range(0f, 1f)] public float strength = 0.6f;

    public override string Title => "Shake Camera";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        CameraShake.Add(strength);
        yield break;
    }
}

[Serializable]
public sealed class FocusCameraNode : StoryNode
{
    [Tooltip("What the camera turns to look at, for Seconds, before turning back to the player.")]
    public Transform target;
    [Min(0f)] public float seconds = 3f;
    [Tooltip("Lifts the dark (the night and unexplored ground) this many metres around it while the camera looks, so it can be seen. 0: off.")]
    [Min(0f)] public float revealRadius = 14f;

    public override string Title => "Focus Camera";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        PlayerCameraController camera = PlayerCameraController.Current;
        if (camera == null || target == null)
        {
            yield break;
        }

        GameObject sight = null;
        if (revealRadius > 0f)
        {
            sight = new GameObject("Story Sight");
            sight.transform.SetParent(target, false);
            sight.AddComponent<VisibilitySource>().Configure(revealRadius, 0.4f, true, revealRadius, false);
        }

        camera.BeginFocus(target);
        yield return new WaitForSeconds(seconds);
        camera.EndFocus(target);
        if (sight != null)
        {
            UnityEngine.Object.Destroy(sight);
        }
    }

    public override string FindProblem() => target == null ? "choose what to look at" : null;
}

[Serializable]
public sealed class LightningNode : StoryNode
{
    public override string Title => "Lightning";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        WeatherSystem system = UnityEngine.Object.FindAnyObjectByType<WeatherSystem>();
        if (system != null)
        {
            system.Strike();
        }
        else
        {
            Debug.LogWarning("Lightning: the scene has no WeatherSystem (World Tools › Sky & Weather).", context.Script);
        }

        yield break;
    }
}

[Serializable]
public sealed class PlaySoundNode : StoryNode
{
    public SoundDefinition sound;
    [Tooltip("Where it is heard from. Empty: at the player.")]
    public Transform at;

    public override string Title => "Play Sound";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        Vector3 where = at != null ? at.position : context.Player != null ? context.Player.transform.position : Vector3.zero;
        AudioManager.Play(sound, where);
        yield break;
    }

    public override string FindProblem() => sound == null ? "choose the sound" : null;
}

[Serializable]
public sealed class MusicNode : StoryNode
{
    public enum Change { Play, Silence, Resume }

    public Change change = Change.Play;
    [Tooltip("For Play: the music, such as combat music.")]
    public MusicPlaylist playlist = new MusicPlaylist();

    public override string Title => change == Change.Play ? "Play Music" : change == Change.Silence ? "Silence Music" : "Resume Music";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        SceneAudio audio = UnityEngine.Object.FindAnyObjectByType<SceneAudio>();
        if (audio == null)
        {
            yield break;
        }

        switch (change)
        {
            case Change.Play: audio.PlayEventMusic(playlist); break;
            case Change.Silence: audio.SilenceMusic(); break;
            case Change.Resume: audio.ResumeMusic(); break;
        }
    }

    public override string FindProblem() => change == Change.Play && !playlist.HasTracks ? "add the music" : null;
}
