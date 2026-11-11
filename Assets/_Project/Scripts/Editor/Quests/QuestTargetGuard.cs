using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// What in a scene quests know by name (a <c>[QuestIdentity]</c> field: a Quest Zone's place, an Inspect Point's thing, a
/// Talkable's person), so a builder that clears and remakes objects can say what it took away. The Haven's Drinking Spot went
/// this way in a world rebuild, and Strength for the Road waited for it for a week (found 2026-11-11). Used by the World
/// Builder and the Haven World Generator; QuestAuditTests finds the same afterwards.
/// </summary>
public static class QuestTargetGuard
{
    /// <summary>Every person, place or thing quests know in the scene, with the object that is it.</summary>
    public static Dictionary<Object, string> Snapshot(Scene scene)
    {
        var found = new Dictionary<Object, string>();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return found;
        }

        foreach (KeyValuePair<System.Type, HashSet<string>> marked in QuestSceneIndex.IdentityFields)
        {
            if (!typeof(Component).IsAssignableFrom(marked.Key))
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren(marked.Key, true))
                {
                    foreach (string name in marked.Value)
                    {
                        FieldInfo field = marked.Key.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field?.GetValue(component) is Object identity && identity != null && !found.ContainsKey(identity))
                        {
                            found[identity] = PathOf(component.transform);
                        }
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// What quests knew in the scene before and no longer find there, said as an error naming each (once a build is done).
    /// An empty list when nothing went.
    /// </summary>
    public static List<string> Report(Dictionary<Object, string> before, Scene scene, string who)
    {
        Dictionary<Object, string> now = Snapshot(scene);
        List<string> gone = before.Where(pair => pair.Key != null && !now.ContainsKey(pair.Key))
            .Select(pair => $"{pair.Value} (quests know it as {pair.Key.name})").ToList();
        if (gone.Count > 0)
        {
            Debug.LogError($"{who} removed what quests know by name: {string.Join("; ", gone)}. A quest asking for it can never be done: " +
                "put it back, mark another object with it (the quest's Make It Work list), or change the quest.");
        }

        return gone;
    }

    private static string PathOf(Transform transform)
    {
        string path = transform.name;
        for (Transform up = transform.parent; up != null; up = up.parent)
        {
            path = up.name + "/" + path;
        }

        return path;
    }
}
