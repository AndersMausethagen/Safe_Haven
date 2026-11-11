using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trees placed in a scene as objects (a World Builder grove's, or by hand) are only drawn while far from the player:
/// once a scene has loaded each one is stripped to its mesh, levels of detail, collider and nav obstacle, with no
/// scripts at all, so the thousand trees round the Haven cost nothing to run. Near the player (and on demand: hovered
/// with the mouse, or reached by a falling tree) a live copy with all its gathering takes its place, and goes again once
/// the player is well away and the tree is standing, still and not being worked. A tree being felled or growing back
/// keeps its live copy until it stands again. The same pattern as painted terrain trees (TerrainTreeFelling): full
/// objects only where they can be used.
/// <para>
/// Takes only trees with no state of their own to save (no SaveableEntity, quest gate or other script) and no model
/// variants; anything else stays as it is. Added by itself to every loaded scene that has such trees.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NodeStreaming : MonoBehaviour
{
    private const float LiveRadius = 10f;
    private const float ReleaseRadius = 14f;
    private const float WantedSeconds = 3f;
    private const float RefreshSeconds = 0.25f;
    private const float CellSize = 16f;

    // What a stripped tree may carry; anything else on it means it has a part to play of its own and stays live.
    private static readonly System.Type[] Strippable =
    {
        typeof(ChoppableTree), typeof(TreeLeafLook), typeof(ResourceNodeFeedback), typeof(ResourceNode), typeof(ResourceSource), typeof(Interactable),
    };

    private static readonly List<NodeStreaming> active = new List<NodeStreaming>();
    private static readonly Dictionary<Collider, Entry> byCollider = new Dictionary<Collider, Entry>();
    private static readonly Dictionary<GameObject, Entry> liveByObject = new Dictionary<GameObject, Entry>();

    /// <summary>A placed tree: drawn as a bare mesh, or by its live copy while that exists.</summary>
    public sealed class Entry
    {
        public NodeStreaming Owner;
        public GameObject Placed;
        public GameObject Template;
        public GameObject Live;
        public Vector3 Foot;
        public float Height;
        public float WantedUntil;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Entry> live = new List<Entry>();
    private readonly Dictionary<Vector2Int, List<Entry>> cells = new Dictionary<Vector2Int, List<Entry>>();
    private readonly List<Entry> scratch = new List<Entry>();
    private Transform templates;
    private Scene scene;
    private float nextRefresh;

    /// <summary>Every scene's streamed trees.</summary>
    public static IReadOnlyList<NodeStreaming> Active => active;

    /// <summary>This scene's streamed trees, live or not.</summary>
    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>True for the collider of a tree drawn as a bare mesh: it is a tree, though nothing on it says so.</summary>
    public static bool IsDormantTree(Collider collider) => collider != null && byCollider.TryGetValue(collider, out Entry entry) && entry.Live == null;

    /// <summary>The live Interactable for a collider of a streamed tree, making its live copy if it has none (it is kept a
    /// few seconds, and longer while still asked for). Null for any other collider.</summary>
    public static Interactable Wake(Collider collider)
    {
        if (collider == null || !byCollider.TryGetValue(collider, out Entry entry)) return null;
        GameObject copy = entry.Owner.MakeLive(entry, Time.time + WantedSeconds);
        return copy != null ? copy.GetComponent<Interactable>() : null;
    }

    /// <summary>Keeps a streamed tree's live copy a few seconds more (it is being looked at). Anything else is ignored.</summary>
    public static void KeepAwake(Interactable target)
    {
        if (target != null && liveByObject.TryGetValue(target.gameObject, out Entry entry))
        {
            entry.WantedUntil = Mathf.Max(entry.WantedUntil, Time.time + WantedSeconds);
        }
    }

    /// <summary>Gives every streamed tree within <paramref name="radius"/> of a point its live copy (a falling tree is
    /// about to reach them), and syncs physics so they can be found at once.</summary>
    public static void WakeNear(Vector3 point, float radius)
    {
        bool any = false;
        foreach (NodeStreaming streaming in active)
        {
            streaming.scratch.Clear();
            streaming.Near(point, radius, streaming.scratch);
            foreach (Entry entry in streaming.scratch)
            {
                if (entry.Live == null)
                {
                    streaming.MakeLive(entry, Time.time + WantedSeconds);
                    any = true;
                }
            }
        }

        if (any) Physics.SyncTransforms();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        active.Clear();
        byCollider.Clear();
        liveByObject.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++) Take(SceneManager.GetSceneAt(i));
    }

    private static void OnSceneLoaded(Scene loaded, LoadSceneMode mode) => Take(loaded);

    // After the scene's Awake (so each tree has measured itself) and before any Start: the trees it can take are stripped.
    private static void Take(Scene loaded)
    {
        if (!loaded.isLoaded) return;
        foreach (NodeStreaming already in active)
        {
            if (already.scene == loaded) return;
        }

        var found = new List<ChoppableTree>();
        foreach (GameObject root in loaded.GetRootGameObjects())
        {
            foreach (ChoppableTree tree in root.GetComponentsInChildren<ChoppableTree>(false))
            {
                if (CanStream(tree)) found.Add(tree);
            }
        }

        if (found.Count == 0) return;
        var holder = new GameObject("Streamed Trees");
        SceneManager.MoveGameObjectToScene(holder, loaded);
        var streaming = holder.AddComponent<NodeStreaming>();
        streaming.scene = loaded;
        streaming.Strip(found);
    }

    private static bool CanStream(ChoppableTree tree)
    {
        if (tree.IsStandIn || !tree.TryGetComponent(out ResourceNode node) || node.Definition == null || node.IsDepleted) return false;
        if (node.Definition.ModelVariants.Count > 0) return false;
        foreach (MonoBehaviour script in tree.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script != null && System.Array.IndexOf(Strippable, script.GetType()) < 0) return false;
        }

        return true;
    }

    private void Strip(List<ChoppableTree> trees)
    {
        var holder = new GameObject("Templates");
        holder.SetActive(false);
        holder.transform.SetParent(transform, false);
        templates = holder.transform;
        var kinds = new Dictionary<string, GameObject>();
        foreach (ChoppableTree tree in trees)
        {
            GameObject placed = tree.gameObject;
            string kind = Kind(placed);
            if (!kinds.TryGetValue(kind, out GameObject template))
            {
                // Copied while it still has everything, under an inactive parent so it never wakes.
                template = Instantiate(placed, templates);
                template.name = placed.name;
                template.transform.localScale = Vector3.one;
                kinds[kind] = template;
            }

            var entry = new Entry { Owner = this, Placed = placed, Template = template, Foot = placed.transform.position, Height = tree.Height };
            foreach (System.Type type in Strippable)
            {
                foreach (Component script in placed.GetComponents(type)) DestroyImmediate(script);
            }

            foreach (Collider collider in placed.GetComponentsInChildren<Collider>(true)) byCollider[collider] = entry;
            entries.Add(entry);
            Vector2Int cell = Cell(entry.Foot);
            if (!cells.TryGetValue(cell, out List<Entry> list)) cells[cell] = list = new List<Entry>();
            list.Add(entry);
        }

        Debug.Log($"[NodeStreaming] {scene.name}: {entries.Count} trees drawn without scripts until the player is near ({kinds.Count} kinds).");
    }

    // Trees of one kind share a gathering definition, meshes and materials. Materials too: variants of one model in other
    // colours (a blossoming one, an autumn one) share its mesh, and a live copy of the first one's template took its colours.
    private static string Kind(GameObject placed)
    {
        var kind = new System.Text.StringBuilder(placed.GetComponent<ResourceNode>().Definition.name);
        foreach (MeshFilter filter in placed.GetComponentsInChildren<MeshFilter>(true))
        {
            kind.Append('|').Append(filter.sharedMesh != null ? filter.sharedMesh.GetEntityId().ToString() : "0");
        }

        foreach (Renderer renderer in placed.GetComponentsInChildren<Renderer>(true))
        {
            kind.Append(renderer.enabled ? '#' : '-');
            foreach (Material material in renderer.sharedMaterials)
            {
                kind.Append(material != null ? material.GetEntityId().ToString() : "0").Append(',');
            }
        }

        return kind.ToString();
    }

    private void OnEnable() => active.Add(this);

    private void OnDisable() => active.Remove(this);

    private void OnDestroy()
    {
        foreach (Entry entry in entries)
        {
            if (entry.Placed == null) continue;
            foreach (Collider collider in entry.Placed.GetComponentsInChildren<Collider>(true)) byCollider.Remove(collider);
        }
    }

    private void Update()
    {
        if (Time.time < nextRefresh || Player.Current == null) return;
        nextRefresh = Time.time + RefreshSeconds;
        Vector3 player = Player.Current.transform.position;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            Entry entry = live[i];
            Vector3 offset = entry.Foot - player;
            offset.y = 0f;
            if (entry.Live == null || (offset.sqrMagnitude > ReleaseRadius * ReleaseRadius && Time.time > entry.WantedUntil && IsSettled(entry.Live)))
            {
                Release(entry);
                live.RemoveAt(i);
            }
        }

        scratch.Clear();
        Near(player, LiveRadius, scratch);
        foreach (Entry entry in scratch)
        {
            if (entry.Live == null) MakeLive(entry, 0f);
        }
    }

    // Felled and growing back, being worked, or still swaying from a knock: it keeps its live copy.
    private static bool IsSettled(GameObject copy)
    {
        if (!copy.activeSelf) return false;
        if (copy.TryGetComponent(out ResourceNode node) && node.IsDepleted) return false;
        if (copy.TryGetComponent(out Interactable interactable) && interactable.IsInUse) return false;
        return !copy.TryGetComponent(out TreeSway sway) || !sway.enabled;
    }

    private GameObject MakeLive(Entry entry, float wantedUntil)
    {
        entry.WantedUntil = Mathf.Max(entry.WantedUntil, wantedUntil);
        if (entry.Live != null) return entry.Live;
        if (entry.Placed == null) return null;
        Transform placed = entry.Placed.transform;
        GameObject copy = Instantiate(entry.Template, placed.position, placed.rotation, placed.parent);
        copy.transform.localScale = placed.localScale;
        copy.name = entry.Placed.name;
        entry.Placed.SetActive(false);
        entry.Live = copy;
        liveByObject[copy] = entry;
        live.Add(entry);
        return copy;
    }

    private static void Release(Entry entry)
    {
        if (!ReferenceEquals(entry.Live, null))
        {
            liveByObject.Remove(entry.Live);
            if (entry.Live != null) Destroy(entry.Live);
        }

        entry.Live = null;
        entry.WantedUntil = 0f;
        if (entry.Placed != null) entry.Placed.SetActive(true);
    }

    private void Near(Vector3 point, float radius, List<Entry> into)
    {
        Vector2Int min = Cell(point - new Vector3(radius, 0f, radius));
        Vector2Int max = Cell(point + new Vector3(radius, 0f, radius));
        float reach = radius * radius;
        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                if (!cells.TryGetValue(new Vector2Int(x, z), out List<Entry> list)) continue;
                foreach (Entry entry in list)
                {
                    Vector3 offset = entry.Foot - point;
                    if (offset.x * offset.x + offset.z * offset.z <= reach) into.Add(entry);
                }
            }
        }
    }

    private static Vector2Int Cell(Vector3 at) => new Vector2Int(Mathf.FloorToInt(at.x / CellSize), Mathf.FloorToInt(at.z / CellSize));
}
