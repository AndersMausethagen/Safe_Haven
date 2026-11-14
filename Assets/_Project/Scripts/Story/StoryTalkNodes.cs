using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Story graph nodes in the "Talk" menu. Story nodes that speak: a line said by someone (StorySpeaker), and a choice the
// player answers. Each is small: settings, a Title and Category for the graph's Add menu, and Run (or Listen for a
// trigger). See StoryNode.

/// <summary>Who says a line in a story: the player, or someone (or something) in the scene.</summary>
[Serializable]
public sealed class StorySpeaker
{
    // Nobody set: the narrator. No name and no face, and the box gives the words the room (write them in <i>italics</i>).
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

    public string Describe() => !player && string.IsNullOrWhiteSpace(name) && model == null && portrait == null ? "Narrator" : player ? "Player" : string.IsNullOrWhiteSpace(name) ? model != null ? model.name : "?" : name;
}

[Serializable]
[WaitsForThePlayer]
public sealed class SayNode : StoryNode
{
    public StorySpeaker speaker = new StorySpeaker();
    [TextArea(2, 5)] public string line;
    [Tooltip("Waits out the pause after a fight (Systems Editor › World › Story Pacing), so a line doesn't land on the last blow. Off: right away.")]
    public bool afterFightPause = true;
    [Tooltip("Spoken aside, as talk on the road or by the fire: a subtitle that leaves the controls and the camera to the player. It waits for a conversation or another aside to end first. Every line of a story that is part of a fight is an aside.")]
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
        // Around a fight a conversation box stops the play: a word from someone is enough.
        if (aside || context.InFight)
        {
            // Barks give way: the story waits only for lines that matter.
            while (DialogueBox.IsSpeakingImportantly)
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
[WaitsForThePlayer]
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
