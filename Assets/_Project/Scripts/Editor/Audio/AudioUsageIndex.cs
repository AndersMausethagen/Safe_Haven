using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Which scenes, prefabs and data assets use each audio clip and sound asset, found by scanning direct asset references.
/// Code that loads audio by name (Resources.Load) is invisible to it, so files in a Resources folder are never called unused.
/// </summary>
public static class AudioUsageIndex
{
    private static readonly HashSet<string> ContainerExtensions = new HashSet<string>
    {
        ".unity", ".prefab", ".asset", ".controller", ".overridecontroller", ".playable", ".mixer", ".anim",
    };

    private static Dictionary<string, List<string>> users;
    private static List<string> clips = new List<string>();
    private static List<string> sounds = new List<string>();

    public static bool Ready => users != null;
    public static System.DateTime ScannedAt { get; private set; }
    public static IReadOnlyList<string> Clips => clips;
    public static IReadOnlyList<string> Sounds => sounds;

    /// <summary>Scans the project. Returns false when the user cancelled.</summary>
    public static bool Scan()
    {
        var found = new Dictionary<string, List<string>>();
        clips = PathsOf("t:AudioClip");
        sounds = PathsOf("t:SoundDefinition");
        var audio = new HashSet<string>(clips);
        audio.UnionWith(sounds);

        var containers = new List<string>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (path.StartsWith("Assets/") && ContainerExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                containers.Add(path);
            }
        }

        try
        {
            for (int i = 0; i < containers.Count; i++)
            {
                string container = containers[i];
                if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Finding audio usage", container, i / (float)containers.Count))
                {
                    return false;
                }

                foreach (string dependency in AssetDatabase.GetDependencies(container, false))
                {
                    if (dependency != container && audio.Contains(dependency))
                    {
                        if (!found.TryGetValue(dependency, out List<string> list)) found[dependency] = list = new List<string>();
                        list.Add(container);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        users = found;
        ScannedAt = System.DateTime.Now;
        return true;
    }

    public static void ScanIfNeeded()
    {
        if (!Ready) Scan();
    }

    /// <summary>The assets that reference this one directly. Empty before a scan.</summary>
    public static IReadOnlyList<string> UsersOf(Object asset) => UsersOf(AssetDatabase.GetAssetPath(asset));

    public static IReadOnlyList<string> UsersOf(string path)
    {
        return users != null && users.TryGetValue(path, out List<string> list) ? list : (IReadOnlyList<string>)System.Array.Empty<string>();
    }

    /// <summary>True when nothing uses it, or only sound assets that are themselves unused.</summary>
    public static bool IsUnused(string path)
    {
        if (!Ready || path.Contains("/Resources/")) return false;
        foreach (string user in UsersOf(path))
        {
            if (!sounds.Contains(user) || !IsUnused(user)) return false;
        }

        return true;
    }

    /// <summary>Audio clips and sound assets under a folder that nothing uses.</summary>
    public static List<string> Unused(string folder)
    {
        var result = new List<string>();
        if (!Ready) return result;
        foreach (string path in sounds)
        {
            if (InFolder(path, folder) && IsUnused(path)) result.Add(path);
        }

        foreach (string path in clips)
        {
            if (InFolder(path, folder) && IsUnused(path)) result.Add(path);
        }

        return result;
    }

    /// <summary>Forgets a deleted or moved asset so the lists stay correct without a rescan.</summary>
    public static void Forget(string path)
    {
        clips.Remove(path);
        sounds.Remove(path);
        users?.Remove(path);
    }

    public static bool InFolder(string path, string folder) =>
        string.IsNullOrEmpty(folder) || path.StartsWith(folder.TrimEnd('/') + "/");

    private static List<string> PathsOf(string filter)
    {
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        paths.Sort(System.StringComparer.OrdinalIgnoreCase);
        return paths;
    }
}
