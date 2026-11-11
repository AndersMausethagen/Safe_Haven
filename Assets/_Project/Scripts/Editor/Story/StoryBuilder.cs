using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What every story builder (AgentScripts: BuildFirstAct, BuildHavensReach, BuildPrologueMeadow) needs to make stories in a
/// scene again without making saved games forget them. A story remembers which triggers have fired by its save id and its
/// triggers' ids, both random when made by hand; remade from nothing, every story would play again in every saved game.
/// <list type="bullet">
/// <item><see cref="Fresh"/> builds a story on its old object (keeping its save id), or on a new one that takes back the id
/// <see cref="Remember"/> noted before a builder cleared its root, or an id worked out from its name.</item>
/// <item><see cref="Finish"/>, after the links and before saving: each trigger takes back the id its place had (by order), or
/// one worked out from the story's name and its place, so the next run gives the same.</item>
/// <item><see cref="Link"/> adds nodes laid out left to right and connects them.</item>
/// </list>
/// </summary>
public static class StoryBuilder
{
    private static readonly Dictionary<string, (string saveId, List<string> triggers)> remembered = new Dictionary<string, (string, List<string>)>();
    private static readonly Dictionary<StoryScript, List<string>> building = new Dictionary<StoryScript, List<string>>();

    /// <summary>Notes every story under <paramref name="root"/> (by name) before a builder clears it.</summary>
    public static void Remember(Transform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (StoryScript story in root.GetComponentsInChildren<StoryScript>(true))
        {
            remembered[story.name] = (story.TryGetComponent(out SaveableEntity saved) ? saved.Id : null, story.Nodes.OfType<StoryTrigger>().Select(t => t.id).ToList());
        }
    }

    /// <summary>A story to build under <paramref name="root"/>: on its old object when it is still there, else a new one.</summary>
    public static StoryScript Fresh(Transform root, string name)
    {
        Transform old = root.Find(name);
        GameObject go;
        List<string> ids;
        if (old != null)
        {
            go = old.gameObject;
            ids = go.TryGetComponent(out StoryScript previous) ? previous.Nodes.OfType<StoryTrigger>().Select(t => t.id).ToList() : new List<string>();
            if (previous != null)
            {
                Object.DestroyImmediate(previous);
            }
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(root, false);
            var saveable = go.AddComponent<SaveableEntity>();
            string id = remembered.TryGetValue(name, out var noted) && !string.IsNullOrEmpty(noted.saveId) ? noted.saveId : StableId("story:" + root.name + "/" + name);
            var so = new SerializedObject(saveable);
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            ids = noted.triggers ?? new List<string>();
        }

        StoryScript story = go.AddComponent<StoryScript>();
        building[story] = ids;
        return story;
    }

    /// <summary>Gives every story made by <see cref="Fresh"/> its triggers' ids back. Call after the links, before saving. Returns how many stories.</summary>
    public static int Finish()
    {
        int count = 0;
        foreach (KeyValuePair<StoryScript, List<string>> made in building)
        {
            if (made.Key == null)
            {
                continue;
            }

            List<StoryTrigger> triggers = made.Key.Nodes.OfType<StoryTrigger>().ToList();
            for (int i = 0; i < triggers.Count; i++)
            {
                triggers[i].id = i < made.Value.Count && !string.IsNullOrEmpty(made.Value[i]) ? made.Value[i] : StableId($"trigger:{made.Key.name}#{i}");
            }

            EditorUtility.SetDirty(made.Key);
            count++;
        }

        building.Clear();
        remembered.Clear();
        return count;
    }

    /// <summary>Adds the nodes (in order of first appearance, laid out left to right) and the links.</summary>
    public static void Link(StoryScript story, params (StoryNode from, int output, StoryNode to)[] links)
    {
        var added = new List<StoryNode>(story.Nodes);
        var depth = new Dictionary<StoryNode, int>();
        foreach (StoryNode node in added) depth[node] = Mathf.RoundToInt((node.position.x - 40f) / 300f);
        foreach (var (from, output, to) in links)
        {
            foreach (StoryNode node in new[] { from, to })
            {
                if (added.Contains(node)) continue;
                int column = node == from ? 0 : depth.TryGetValue(from, out int d) ? d + 1 : 1;
                depth[node] = column;
                int row = depth.Values.Count(v => v == column) - 1;
                node.position = new Vector2(40f + column * 300f, 60f + row * 170f);
                story.AddNode(node);
                added.Add(node);
            }

            story.Connect(from, output, to);
        }
    }

    /// <summary>The same 32-character id for the same words, every run.</summary>
    public static string StableId(string words)
    {
        using MD5 md5 = MD5.Create();
        return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes(words)).Select(b => b.ToString("x2")));
    }
}
