using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A story told in the scene, drawn as a graph of nodes (Safe Haven › Quests › Story Graph): triggers that start it (a
/// Quest Event, using an object, the player walking near, a zone cleared, the scene starting) and steps that follow,
/// one after another along the links (a line, a wait, the rain coming, the camera turning to the troll, enemies pouring
/// out, a reward). A node with several links out runs them side by side; a Choice leaves through the answer picked.
/// <para>
/// Lives on an object in the scene, so nodes can point at scene objects directly. With a SaveableEntity beside it, the
/// triggers that have fired are saved, so a once-only story doesn't replay after loading (what the story changed in the
/// world is kept by those objects' own SaveableEntities). Quests stay data: a story moves them on by raising Quest
/// Events and starting quests, and reacts to them through its triggers.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/Quests/Story Script")]
public sealed class StoryScript : MonoBehaviour, ISaveable
{
    [SerializeReference] private List<StoryNode> nodes = new List<StoryNode>();
    [SerializeField] private List<StoryLink> links = new List<StoryLink>();
    [Tooltip("Logs each node as it runs, to follow a story while testing.")]
    [SerializeField] private bool logSteps;

    private readonly HashSet<string> fired = new HashSet<string>();
    private readonly List<Action> stopListening = new List<Action>();
    private readonly Dictionary<string, StoryNode> byId = new Dictionary<string, StoryNode>();
    private int running;

    public IReadOnlyList<StoryNode> Nodes => nodes;
    public IReadOnlyList<StoryLink> Links => links;

    /// <summary>True while any part of the story is running.</summary>
    public bool IsRunning => running > 0;

    /// <summary>Raised with each node as it starts running.</summary>
    public event Action<StoryNode> NodeStarted;

    public string SaveKey => "story";

    // ----- Editing (the graph window) -----

    public void AddNode(StoryNode node) => nodes.Add(node);

    public void RemoveNode(StoryNode node)
    {
        nodes.Remove(node);
        links.RemoveAll(link => link.from == node.id || link.to == node.id);
    }

    public void Connect(StoryNode from, int output, StoryNode to)
    {
        if (from == null || to == null || from == to || to.IsTrigger)
        {
            return;
        }

        links.RemoveAll(link => link.from == from.id && link.output == output && link.to == to.id);
        links.Add(new StoryLink(from.id, output, to.id));
    }

    public void Disconnect(StoryNode from, int output, StoryNode to)
    {
        links.RemoveAll(link => link.from == from.id && link.output == output && link.to == to.id);
    }

    public StoryNode Find(string id)
    {
        foreach (StoryNode node in nodes)
        {
            if (node != null && node.id == id)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>Problems with the graph, in plain words, for the graph window.</summary>
    public List<string> FindProblems()
    {
        var problems = new List<string>();
        bool anyTrigger = false;
        foreach (StoryNode node in nodes)
        {
            if (node == null)
            {
                problems.Add("A node's type no longer exists (renamed?); remove it.");
                continue;
            }

            anyTrigger |= node.IsTrigger;
            string problem = node.FindProblem();
            if (problem != null)
            {
                problems.Add($"{node.Title}: {problem}");
            }
        }

        if (nodes.Count > 0 && !anyTrigger)
        {
            problems.Add("No trigger, so the story never starts.");
        }

        return problems;
    }

    // ----- Running -----

    private void OnEnable()
    {
        byId.Clear();
        foreach (StoryNode node in nodes)
        {
            if (node != null)
            {
                byId[node.id] = node;
            }
        }

        foreach (StoryNode node in nodes)
        {
            if (node is StoryTrigger trigger)
            {
                Action stop = trigger.Listen(this, () => Fire(trigger));
                if (stop != null)
                {
                    stopListening.Add(stop);
                }
            }
        }
    }

    private void OnDisable()
    {
        foreach (Action stop in stopListening)
        {
            stop();
        }

        stopListening.Clear();
        StopAllCoroutines();
        running = 0;
    }

    /// <summary>Starts the story from a trigger, unless it is once-only and has fired before.</summary>
    public void Fire(StoryTrigger trigger)
    {
        if (trigger == null || !isActiveAndEnabled || trigger.once && fired.Contains(trigger.id))
        {
            return;
        }

        fired.Add(trigger.id);
        Log(trigger);
        NodeStarted?.Invoke(trigger);
        FollowLinks(trigger, 0);
    }

    /// <summary>Runs the story from <paramref name="node"/> (testing a branch from the graph window).</summary>
    public void RunFrom(StoryNode node)
    {
        if (node != null && isActiveAndEnabled)
        {
            StartCoroutine(RunNode(node));
        }
    }

    public bool HasFired(StoryTrigger trigger) => fired.Contains(trigger.id);

    private IEnumerator RunNode(StoryNode node)
    {
        running++;
        try
        {
            Log(node);
            NodeStarted?.Invoke(node);
            var context = new StoryContext(this);
            IEnumerator steps = node.Run(context);
            while (true)
            {
                object step;
                try
                {
                    if (!steps.MoveNext())
                    {
                        break;
                    }

                    step = steps.Current;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    yield break;
                }

                yield return step;
            }

            if (context.Output >= 0)
            {
                FollowLinks(node, context.Output);
            }
        }
        finally
        {
            running--;
        }
    }

    private void FollowLinks(StoryNode node, int output)
    {
        foreach (StoryLink link in links)
        {
            if (link.from == node.id && link.output == output && byId.TryGetValue(link.to, out StoryNode next) && next != null)
            {
                StartCoroutine(RunNode(next));
            }
        }
    }

    private void Log(StoryNode node)
    {
        if (logSteps)
        {
            Debug.Log($"{name}: {node.Title}", this);
        }
    }

    // ----- Saving -----

    public string CaptureState() => string.Join(",", fired);

    public void RestoreState(string state)
    {
        fired.Clear();
        if (!string.IsNullOrEmpty(state))
        {
            fired.UnionWith(state.Split(','));
        }
    }
}
