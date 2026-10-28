using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>What the Story Graph window draws for the selected Story Script (`unity command run_script --file AgentScripts/ProbeStoryGraph.cs --entry ProbeStoryGraph.Report --args '["Story - The Waking Stone"]'`).</summary>
public static class ProbeStoryGraph
{
    public static string Report(string storyName)
    {
        GameObject story = GameObject.Find(storyName);
        Selection.activeGameObject = story;
        var window = EditorWindow.GetWindow<StoryGraphWindow>();
        window.Load(story.GetComponent<StoryScript>());
        GraphView view = window.rootVisualElement.Query<GraphView>().First();
        int nodes = view.nodes.ToList().Count;
        int edges = view.edges.ToList().Count;
        string titles = string.Join(" > ", view.nodes.ToList().Select(n => n.title));
        var script = story.GetComponent<StoryScript>();
        return $"drawn: {nodes} nodes, {edges} links (script: {script.Nodes.Count} nodes, {script.Links.Count} links); problems: {string.Join("; ", script.FindProblems())}\n{titles}";
    }
}
