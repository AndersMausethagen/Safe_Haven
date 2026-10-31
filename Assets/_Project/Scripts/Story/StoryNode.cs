using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A connection in a story graph: from one node's output to the next node.</summary>
[Serializable]
public sealed class StoryLink
{
    public string from;
    public int output;
    public string to;

    public StoryLink(string from, int output, string to)
    {
        this.from = from;
        this.output = output;
        this.to = to;
    }
}

/// <summary>What a running node can reach: the script it belongs to, the player, and which output it chose.</summary>
public sealed class StoryContext
{
    public StoryContext(StoryScript script)
    {
        Script = script;
    }

    public StoryScript Script { get; }
    public Player Player => Player.Current;

    /// <summary>The output the node just run leaves through (a choice's answer). 0 for most nodes; -1 stops here.</summary>
    public int Output { get; set; }

    /// <summary>True in the quiet right after a fight (StoryPacing), when a line should wait.</summary>
    public bool IsCatchingBreath => QuestManager.Instance != null
        && QuestManager.Instance.Pacing.IsCatchingBreath(Time.time, StoryPacingSettings.Current);
}

/// <summary>
/// One step of a story graph (StoryScript): something that starts the story (a trigger) or something that happens
/// (a line, a wait, the weather turning). Nodes hold their settings and scene references; the graph's links say what
/// comes next. Add a kind by subclassing: give it a Title and a Category for the graph's menu, and Run (or, for a
/// trigger, Listen). Values are saved by [SerializeReference], so renaming a node class breaks graphs that use it.
/// </summary>
[Serializable]
public abstract class StoryNode
{
    [HideInInspector] public string id = Guid.NewGuid().ToString("N");
    [HideInInspector] public Vector2 position;

    /// <summary>The name shown on the node.</summary>
    public abstract string Title { get; }

    /// <summary>The group in the graph's Add menu ("Triggers", "Talk", "World", "Camera & Sound", "Quest", "Flow").</summary>
    public abstract string Category { get; }

    /// <summary>True for nodes that start a story (no input).</summary>
    public virtual bool IsTrigger => false;

    public virtual int OutputCount => 1;

    public virtual string OutputName(int index) => "";

    /// <summary>Does the node's work, over as many frames as it needs. Set context.Output to choose an output.</summary>
    public virtual IEnumerator Run(StoryContext context)
    {
        yield break;
    }

    /// <summary>For triggers: starts calling <paramref name="fire"/> when the story should begin. Returns how to stop.</summary>
    public virtual Action Listen(StoryScript script, Action fire) => null;

    /// <summary>What is missing, in a sentence, or null.</summary>
    public virtual string FindProblem() => null;
}

/// <summary>A trigger: fires once (or every time) its condition is met.</summary>
[Serializable]
public abstract class StoryTrigger : StoryNode
{
    [Tooltip("Fire only the first time (remembered in saves when the Story Script has a SaveableEntity). Off: every time.")]
    public bool once = true;

    public override string Category => "Triggers";
    public override bool IsTrigger => true;
}
