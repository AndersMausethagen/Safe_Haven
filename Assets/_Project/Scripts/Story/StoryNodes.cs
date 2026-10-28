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
        return () => GameEvents.Raised -= heard;
    }

    public override string FindProblem() => quest == null ? "choose the quest" : null;
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

    public DialogueSpeaker ToSpeaker() =>
        player ? DialogueSpeaker.Player(name) : new DialogueSpeaker(string.IsNullOrWhiteSpace(name) && model != null ? model.name : name, model, portrait);

    public string Describe() => player ? "Player" : string.IsNullOrWhiteSpace(name) ? model != null ? model.name : "?" : name;
}

[Serializable]
public sealed class SayNode : StoryNode
{
    public StorySpeaker speaker = new StorySpeaker();
    [TextArea(2, 5)] public string line;

    public override string Title => "Say";
    public override string Category => "Talk";

    public override IEnumerator Run(StoryContext context)
    {
        bool done = false;
        DialogueBox box = DialogueBox.Current;
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

    public override string Title => "Choice";
    public override string Category => "Talk";
    public override int OutputCount => Mathf.Max(1, answers.Count);
    public override string OutputName(int index) => index < answers.Count ? answers[index] : "";

    public override IEnumerator Run(StoryContext context)
    {
        int picked = -2;
        var choices = new List<DialogueChoice>();
        foreach (string answer in answers)
        {
            choices.Add(new DialogueChoice(answer));
        }

        DialogueBox.Current.Ask(speaker.ToSpeaker(), line, choices, index => picked = index);
        while (picked == -2)
        {
            yield return null;
        }

        context.Output = picked;
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
public sealed class GiveItemsNode : StoryNode
{
    public List<ItemAmount> items = new List<ItemAmount>();

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
    [Tooltip("What glows: a light appears at it (and fades out again when Glow is off).")]
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

    public override string Title => "Focus Camera";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        PlayerCameraController camera = UnityEngine.Object.FindAnyObjectByType<PlayerCameraController>();
        if (camera == null || target == null)
        {
            yield break;
        }

        camera.BeginFocus(target);
        yield return new WaitForSeconds(seconds);
        camera.EndFocus(target);
    }

    public override string FindProblem() => target == null ? "choose what to look at" : null;
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
