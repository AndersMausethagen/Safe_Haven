using System.Collections.Generic;
using UnityEngine;

/// <summary>What kind of gathering node a RandomResourceNode may become, by the tool it takes.</summary>
public enum ResourceNodeKind
{
    Any = 0,
    /// <summary>Felled with an axe: trees and wood.</summary>
    Wood = 1,
    /// <summary>Broken with a pickaxe: stone and ore.</summary>
    Rock = 2,
    /// <summary>Cut with a sickle: fibre and plants.</summary>
    Plant = 3,
    /// <summary>Picked up by hand: sticks, small rocks, berries, herbs.</summary>
    Forage = 4,
}

/// <summary>
/// A placeholder for a gathering node: when the scene starts it becomes one, picked at random from every gathering node
/// (ContentDatabase) of the chosen Kind and within the Tier range, or from its own list. The node keeps its own look
/// variants (GatheringNodeDefinition › Model Variants), so the same stone node can look different spot to spot. Stable
/// Pick makes a spot pick the same node every visit (for home); otherwise each visit rolls again (raids). At home
/// only nodes in season are picked (GatheringNodeDefinition › In Seasons): no berries in winter.
/// Nothing it becomes is saved.
/// </summary>
[DisallowMultipleComponent]
public sealed class RandomResourceNode : MonoBehaviour
{
    [Tooltip("Which nodes it may become. Any picks from every kind.")]
    [SerializeField] private ResourceNodeKind kind = ResourceNodeKind.Any;
    [Tooltip("Lowest and highest node tier it may become (0 is forage, 1 is the first tools).")]
    [SerializeField] private Vector2Int tiers = new Vector2Int(1, 1);
    [Tooltip("Only these nodes (Kind and Tier are ignored). Empty: every node that matches.")]
    [SerializeField] private List<GatheringNodeDefinition> only = new List<GatheringNodeDefinition>();
    [Tooltip("The same spot always becomes the same node. Off: a new pick each time the scene loads.")]
    [SerializeField] private bool stablePick;

    private readonly List<GatheringNodeDefinition> candidates = new List<GatheringNodeDefinition>();

    /// <summary>The node it turned into, once the scene has started.</summary>
    public GameObject Spawned { get; private set; }

    public ResourceNodeKind Kind => kind;
    public Vector2Int Tiers => tiers;

    private void Start()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        IReadOnlyList<GatheringNodeDefinition> pool = only.Count > 0 ? only : content != null ? content.GatheringNodes : null;
        Collect(pool, kind, tiers, candidates, only.Count > 0, Seasons.Today);
        if (candidates.Count == 0)
        {
            Debug.LogWarning($"{name}: no gathering node with a world prefab matches {kind}, tier {tiers.x}-{tiers.y}.", this);
            return;
        }

        Vector3 at = transform.position;
        var random = stablePick
            ? new System.Random(Mathf.RoundToInt(at.x * 10f) * 73856093 ^ Mathf.RoundToInt(at.z * 10f) * 19349663)
            : new System.Random(System.Environment.TickCount ^ transform.position.GetHashCode());
        Spawned = Become(candidates[Pick(candidates, random.NextDouble())]);
    }

    /// <summary>Which of <paramref name="from"/> a roll in [0, 1) picks, by each node's Spawn Weight (all at 0: evenly).</summary>
    public static int Pick(IReadOnlyList<GatheringNodeDefinition> from, double roll)
    {
        float total = 0f;
        foreach (GatheringNodeDefinition node in from)
        {
            total += Mathf.Max(0f, node.SpawnWeight);
        }

        if (total <= 0f)
        {
            return Mathf.Min(from.Count - 1, (int)(roll * from.Count));
        }

        double left = roll * total;
        for (int i = 0; i < from.Count; i++)
        {
            left -= Mathf.Max(0f, from[i].SpawnWeight);
            if (left < 0d)
            {
                return i;
            }
        }

        return from.Count - 1;
    }

    /// <summary>The nodes that match: those with a world prefab, of the kind and within the tiers (all of them for an
    /// explicit list).</summary>
    /// <param name="season">At home, the season now (Seasons.Today): nodes out of season are left out, unless that
    /// leaves nothing. Null: any season (raids).</param>
    public static void Collect(IReadOnlyList<GatheringNodeDefinition> pool, ResourceNodeKind kind, Vector2Int tiers,
        List<GatheringNodeDefinition> into, bool explicitList = false, string season = null)
    {
        into.Clear();
        if (pool == null)
        {
            return;
        }

        int low = Mathf.Min(tiers.x, tiers.y);
        int high = Mathf.Max(tiers.x, tiers.y);
        foreach (GatheringNodeDefinition node in pool)
        {
            if (node != null && node.WorldPrefab != null
                && (explicitList || (node.Tier >= low && node.Tier <= high && Matches(node, kind))))
            {
                into.Add(node);
            }
        }

        if (string.IsNullOrEmpty(season))
        {
            return;
        }

        int before = into.Count;
        into.RemoveAll(node => !node.InSeason(season));
        if (into.Count == 0 && before > 0)
        {
            // Nothing of this kind grows now: the spot stays as it was picked, so a spot is never simply empty.
            Collect(pool, kind, tiers, into, explicitList);
        }
    }

    public static bool Matches(GatheringNodeDefinition node, ResourceNodeKind kind)
    {
        switch (kind)
        {
            case ResourceNodeKind.Wood: return node.GatherTool == ToolType.Axe;
            case ResourceNodeKind.Rock: return node.GatherTool == ToolType.Pickaxe;
            case ResourceNodeKind.Plant: return node.GatherTool == ToolType.Sickle;
            case ResourceNodeKind.Forage: return node.GatherTool == ToolType.None;
            default: return true;
        }
    }

    // The node's prefab, made while switched off so it can drop its save id first (a picked node is never saved), then
    // switched on where this placeholder stands.
    private GameObject Become(GatheringNodeDefinition node)
    {
        var holder = new GameObject("(random node)");
        holder.SetActive(false);
        GameObject made = Instantiate(node.WorldPrefab, transform.position, transform.rotation, holder.transform);
        made.name = node.WorldPrefab.name;
        if (made.TryGetComponent(out SaveableEntity saveable))
        {
            DestroyImmediate(saveable);
        }

        made.transform.SetParent(transform.parent, true);
        Destroy(holder);
        made.SetActive(true);
        gameObject.SetActive(false);
        return made;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = kind switch
        {
            ResourceNodeKind.Wood => new Color(0.55f, 0.35f, 0.2f),
            ResourceNodeKind.Rock => new Color(0.6f, 0.6f, 0.65f),
            ResourceNodeKind.Plant => new Color(0.4f, 0.75f, 0.3f),
            ResourceNodeKind.Forage => new Color(0.85f, 0.75f, 0.35f),
            _ => new Color(0.9f, 0.9f, 0.9f),
        };
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up);
    }
}
