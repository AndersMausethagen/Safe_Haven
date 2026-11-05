using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One thing the Level Check found, with an optional fix.</summary>
public sealed class LevelIssue
{
    public string Kind;
    public string Message;
    public GameObject Target;
    public string FixLabel;
    public Action Fix;
}

/// <summary>
/// Checks a level for the mistakes that are easy to make and hard to spot (Safe Haven > Scene > Level Check, or
/// `unity command sh_level`): gameplay objects floating above or buried in the ground, out of the NavMesh's reach,
/// under water, crowded on top of each other or off the terrain; duplicate or missing save ids; missing scripts; and
/// leftover objects (empty terrains, stray primitives at the origin). Also counts what the level contains.
/// </summary>
public static class LevelCheck
{
    public const float FloatTolerance = 0.25f;
    public const float BuryTolerance = 0.6f;

    /// <summary>What the scene contains, by kind (resource nodes by their definition).</summary>
    public static SortedDictionary<string, int> Census(Scene scene)
    {
        var counts = new SortedDictionary<string, int>();
        void Count(string key, int n = 1)
        {
            if (n > 0) counts[key] = counts.TryGetValue(key, out int c) ? c + n : n;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (ResourceNode node in root.GetComponentsInChildren<ResourceNode>(true))
                Count("Resource: " + (node.Definition != null ? node.Definition.name : "(no definition)"));
            Count("Interactables", root.GetComponentsInChildren<Interactable>(true).Length);
            Count("Water sources", root.GetComponentsInChildren<WaterSource>(true).Length);
            Count("Monsters", root.GetComponentsInChildren<MonsterAI>(true).Length);
            Count("Spawn groups", root.GetComponentsInChildren<SpawnGroup>(true).Length);
            Count("Saveable objects", root.GetComponentsInChildren<SaveableEntity>(true).Length);
            Count("Lights", root.GetComponentsInChildren<Light>(true).Count(l => l.type != LightType.Directional));
            Count("Water surfaces", root.GetComponentsInChildren<WaterSurface>(true).Length);
        }

        GameObject scatter = SceneFolders.Find(scene, ScatterBrush.RootName);
        if (scatter != null)
            foreach (Transform folder in scatter.transform)
                Count("Scattered: " + folder.name, folder.childCount);

        return counts;
    }

    public static List<LevelIssue> Run(Scene scene)
    {
        var issues = new List<LevelIssue>();
        GameObject[] roots = scene.GetRootGameObjects();
        List<GameObject> gameplay = roots.SelectMany(r => r.GetComponentsInChildren<ResourceNode>(true).Select(c => c.gameObject)
                .Concat(r.GetComponentsInChildren<Interactable>(true).Select(c => c.gameObject)))
            .Distinct().Where(g => g.activeInHierarchy).ToList();
        bool hasNavMesh = NavMesh.CalculateTriangulation().vertices.Length > 0;
        WaterSurface[] water = Object.FindObjectsByType<WaterSurface>();

        foreach (GameObject go in gameplay)
        {
            CheckGrounding(go, issues);
            if (hasNavMesh && !NavMesh.SamplePosition(go.transform.position, out _, 2f, NavMesh.AllAreas))
            {
                issues.Add(new LevelIssue { Kind = "Unreachable", Target = go, Message = $"{go.name} is more than 2 m from the NavMesh: residents and monsters cannot reach it." });
            }

            if (go.GetComponent<WaterSource>() == null && water.Any(w => w.Contains(go.transform.position + Vector3.up * 0.1f, 0f, out float top) && go.transform.position.y < top - 0.2f))
            {
                issues.Add(new LevelIssue { Kind = "Under water", Target = go, Message = $"{go.name} stands under water." });
            }

            if (!OnTerrain(go.transform.position))
            {
                issues.Add(new LevelIssue { Kind = "Off the terrain", Target = go, Message = $"{go.name} is outside every terrain." });
            }
        }

        // Crowding: resource nodes on top of each other.
        List<ResourceNode> nodes = roots.SelectMany(r => r.GetComponentsInChildren<ResourceNode>()).ToList();
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position) < 1f)
                {
                    issues.Add(new LevelIssue { Kind = "Crowded", Target = nodes[j].gameObject, Message = $"{nodes[j].name} is less than 1 m from {nodes[i].name}." });
                }
            }
        }

        // Save ids.
        var seen = new Dictionary<string, SaveableEntity>();
        foreach (SaveableEntity entity in roots.SelectMany(r => r.GetComponentsInChildren<SaveableEntity>(true)))
        {
            string problem = string.IsNullOrEmpty(entity.Id) ? "has no save id" : seen.ContainsKey(entity.Id) ? $"shares its save id with {seen[entity.Id].name}" : null;
            if (!string.IsNullOrEmpty(entity.Id)) seen[entity.Id] = entity;
            if (problem != null)
            {
                issues.Add(new LevelIssue
                {
                    Kind = "Save id", Target = entity.gameObject, Message = $"{entity.name} {problem}: its state would not save correctly.",
                    FixLabel = "Assign ids", Fix = () => SaveableEntityTools.AssignSceneIds(scene)
                });
            }
        }

        // Missing scripts and leftovers.
        foreach (GameObject go in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing > 0)
            {
                GameObject target = go;
                issues.Add(new LevelIssue
                {
                    Kind = "Missing script", Target = go, Message = $"{go.name} has {missing} missing script(s).",
                    FixLabel = "Remove", Fix = () => { Undo.RegisterCompleteObjectUndo(target, "Remove Missing Scripts"); GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target); }
                });
            }

            if (go.TryGetComponent(out Terrain terrain) && terrain.terrainData == null)
            {
                issues.Add(Leftover(go, $"{go.name} is a terrain without terrain data."));
            }
            else if ((go.transform.parent == null || SceneFolders.IsFolder(go.transform.parent.gameObject)) && go.transform.position == Vector3.zero && go.transform.childCount == 0
                     && (go.name == "Cube" || go.name == "Sphere" || go.name == "GameObject" || go.name == "New Game Object"))
            {
                issues.Add(Leftover(go, $"{go.name} at the world origin looks like a leftover."));
            }
        }

        return issues;
    }

    private static LevelIssue Leftover(GameObject go, string message) => new LevelIssue
    {
        Kind = "Leftover", Target = go, Message = message, FixLabel = "Delete", Fix = () => Undo.DestroyObjectImmediate(go)
    };

    private static void CheckGrounding(GameObject go, List<LevelIssue> issues)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0 || !GroundPlacement.TryGround(go.transform.position, out Vector3 ground, out _, go.transform))
        {
            return;
        }

        float bottom = renderers.Min(r => r.bounds.min.y);
        float gap = bottom - ground.y;
        if (gap > FloatTolerance || -gap > BuryTolerance)
        {
            issues.Add(new LevelIssue
            {
                Kind = gap > 0 ? "Floating" : "Buried", Target = go,
                Message = gap > 0 ? $"{go.name} floats {gap:0.00} m above the ground." : $"{go.name} is {-gap:0.00} m into the ground.",
                FixLabel = "Drop to ground", Fix = () => GroundPlacement.Drop(go.transform, false)
            });
        }
    }

    private static bool OnTerrain(Vector3 position) => Terrain.activeTerrains.Any(t =>
    {
        if (t.terrainData == null) return false;
        Vector3 local = position - t.transform.position;
        return local.x >= 0f && local.z >= 0f && local.x <= t.terrainData.size.x && local.z <= t.terrainData.size.z;
    });
}

/// <summary>The Level Check window: the open scene's contents and problems, each with Select and, where it can, Fix.</summary>
public sealed class LevelCheckWindow : EditorWindow
{
    private List<LevelIssue> issues = new List<LevelIssue>();
    private SortedDictionary<string, int> census = new SortedDictionary<string, int>();
    private Vector2 scroll;
    private string filter = "All";

    [MenuItem("Safe Haven/Scene/Level Check")]
    public static void Open() => GetWindow<LevelCheckWindow>("Level Check").Run();

    private void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        issues = LevelCheck.Run(scene);
        census = LevelCheck.Census(scene);
        Repaint();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Check Again", GUILayout.Height(24))) Run();
            using (new EditorGUI.DisabledScope(!issues.Any(i => i.Fix != null)))
            {
                if (GUILayout.Button($"Fix All Fixable ({issues.Count(i => i.Fix != null)})", GUILayout.Height(24)))
                {
                    Undo.IncrementCurrentGroup();
                    foreach (LevelIssue issue in issues.Where(i => i.Fix != null && i.Target != null).ToList()) issue.Fix();
                    Undo.SetCurrentGroupName("Level Check fixes");
                    Run();
                }
            }
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Contents", EditorStyles.boldLabel);
        foreach (KeyValuePair<string, int> pair in census)
        {
            EditorGUILayout.LabelField(pair.Key, pair.Value.ToString());
        }

        EditorGUILayout.Space();
        string[] kinds = new[] { "All" }.Concat(issues.Select(i => i.Kind).Distinct()).ToArray();
        int pick = Mathf.Max(0, Array.IndexOf(kinds, filter));
        pick = GUILayout.Toolbar(pick, kinds.Select(k => k == "All" ? $"All ({issues.Count})" : $"{k} ({issues.Count(i => i.Kind == k)})").ToArray());
        filter = kinds[pick];
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
        }

        foreach (LevelIssue issue in issues.Where(i => filter == "All" || i.Kind == filter).ToList())
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(issue.Kind, EditorStyles.boldLabel, GUILayout.Width(110));
                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
                using (new EditorGUI.DisabledScope(issue.Target == null))
                {
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = issue.Target;
                        SceneView.FrameLastActiveSceneView();
                    }
                }

                if (issue.Fix != null && GUILayout.Button(issue.FixLabel, GUILayout.Width(110)))
                {
                    issue.Fix();
                    Run();
                    GUIUtility.ExitGUI();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }
}
