using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a tree in the world fellable with an axe. It sits beside the tree's ResourceNode (the "Tree" node: needs an
/// axe, the swing minigame, grows back) and ResourceSource (whose first drop, Wood, is the minigame's bonus log). How
/// many logs and which woods follow TreeChoppingSettings, from the axe's tier and the tree's size (its height, measured
/// once). Each blow opens the notch in the trunk, shakes leaves out of the crown and makes the wood creak more as it gives; the last one brings it down as
/// a FelledTree, which gives the logs when it lands and leaves the rest of the work on the ground. Painted terrain trees
/// get a stand-in with one near the player (TerrainTreeFelling).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable), typeof(ResourceNode), typeof(ResourceSource))]
[InAFriendsHaven(HavenUse.Anyone, "their own yield; the work on the host's land goes to the host (CoopLands)")]
public sealed class ChoppableTree : MonoBehaviour, IHarvestBody
{
    private const float ChopHeight = 1.1f;
    private static readonly List<ChoppableTree> enabledTrees = new List<ChoppableTree>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => enabledTrees.Clear();

    [Tooltip("This tree's own wood (an oak gives Oak Wood, a hardwood tree Hardwood, an elder Elderwood): every log it " +
             "gives is this. Empty: the woods roll by the axe's tier (TreeChoppingSettings), as the painted forest's do.")]
    [SerializeField] private ItemDefinition wood;

    private readonly List<ItemAmount> logs = new List<ItemAmount>();
    private Interactable interactable;
    private ResourceSource source;
    private ResourceNode node;
    private CapsuleCollider trunk;
    private System.Func<TreeParts> standIn;
    private TreeParts parts;
    private TreeNotch notch;
    private float blows; // swings' worth of cutting so far (a critical cuts deeper, a glancing blow barely bites)

    /// <summary>The tree's size for its yield (1 = a tree of the settings' reference height).</summary>
    public float Size { get; private set; } = 1f;

    public float HarvestSize => Size;

    /// <summary>The tree's own wood, or null: the woods roll by the axe's tier.</summary>
    public ItemDefinition Wood => wood;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        source = GetComponent<ResourceSource>();
        node = GetComponent<ResourceNode>();
        trunk = GetComponent<CapsuleCollider>();
        float top = float.MinValue;
        float bottom = float.MaxValue;
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
        {
            top = Mathf.Max(top, part.bounds.max.y);
            bottom = Mathf.Min(bottom, part.bounds.min.y);
        }

        Height = top > bottom ? top - bottom : 0f;
        Size = TreeChoppingSettings.Current.SizeOf(Height);
    }

    /// <summary>Every tree in a loaded scene that is enabled, stand-ins included.</summary>
    public static IReadOnlyList<ChoppableTree> Enabled => enabledTrees;

    /// <summary>How tall the model stands (metres), measured once.</summary>
    public float Height { get; private set; }

    /// <summary>False while felled and not yet grown back.</summary>
    public bool IsStanding => node == null || !node.IsDepleted;

    /// <summary>Sets the size for a stand-in without a model of its own (a terrain tree's, see TerrainTreeFelling).</summary>
    public void SetSize(float size) => Size = size;

    /// <summary>What a stand-in without a model of its own looks like, read only if it comes to it.</summary>
    public void SetStandIn(System.Func<TreeParts> tree) => standIn = tree;

    /// <summary>True for the invisible stand-in of a painted tree, which TerrainTreeFelling answers for.</summary>
    public bool IsStandIn => standIn != null;

    /// <summary>How tall the trunk's collider stands, which is the part a falling tree can meet.</summary>
    public float TrunkHeight => trunk != null ? trunk.height * transform.lossyScale.y : 6f;

    /// <summary>A falling tree has hit this one: it takes the knock and hands back its parts for the leaves it sheds.</summary>
    public TreeParts Knocked(Vector3 direction, float degrees)
    {
        TreeSway.Knock(gameObject, direction, degrees, TreeChoppingSettings.Current.KnockKept);
        return Parts;
    }

    private TreeParts Parts => parts ??= WithWood(standIn != null ? standIn() : TreeParts.Of(gameObject));

    private TreeParts WithWood(TreeParts tree)
    {
        tree.Wood = wood;
        tree.Tier = node != null && node.Definition != null ? node.Definition.Tier : 0;
        // Its own regrowth, unless it stands in for a terrain tree (which says its own).
        if (standIn == null && node != null && node.Definition != null)
        {
            tree.RegrowSeconds = node.Definition.RespawnSeconds > 0f ? node.Definition.RespawnSeconds : -1f;
        }

        return tree;
    }

    private void OnEnable()
    {
        enabledTrees.Add(this);
        interactable.Started.AddListener(OnStarted);
        interactable.Completed.AddListener(OnFelled);
        node.Swung += OnSwung;
    }

    private void OnDisable()
    {
        enabledTrees.Remove(this);
        interactable.Started.RemoveListener(OnStarted);
        interactable.Completed.RemoveListener(OnFelled);
        node.Swung -= OnSwung;
    }

    private void OnStarted() => blows = 0;

    // A hard blow shakes more down, and from halfway through the wood starts to complain.
    private void OnSwung(SwingResult result, Vector3 from)
    {
        blows += GatheringMinigameSettings.Current.ProgressFor(result);
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        TreeParts tree = Parts;
        if (tree.HasFoliage)
        {
            int leaves = result == SwingResult.Critical ? 18 : result == SwingResult.Normal ? 10 : 3;
            TreeEffects.Play(tree.LeafFall != null ? tree.LeafFall : rules.LeafFall, tree.Base + tree.Crown.center, Quaternion.identity, tree.Crown.size, tree.LeafTint, leaves);
        }

        int swings = node.Definition != null ? node.Definition.Swings : 0;
        if (swings > 1 && blows * 2 >= swings && blows < swings)
        {
            AudioManager.Play(rules.CreakSound, tree.Base + Vector3.up * ChopHeight, Size);
        }

        if (swings > 0 && blows < swings)
        {
            if (notch == null)
            {
                notch = TreeNotch.Open(transform, TrunkCentre + Vector3.up * ChopHeight, TrunkRadius, rules.CutFace);
            }

            notch.Widen(Mathf.Min(blows / swings, 1f), from);
        }
    }

    private void OnFelled()
    {
        if (notch != null)
        {
            Destroy(notch.gameObject);
            notch = null;
        }

        GameObject user = interactable.Interactor;
        int axe = Mathf.Max(1, ItemInventory.GatheringTier(user, ToolType.Axe));
        Roll(TreeChoppingSettings.Current.FellingLogs(axe, Size), axe, Size, () => Random.value, logs, wood);
        if (FelledTree.Spawn(Parts, Size, user, logs) == null)
        {
            source.GrantItems(user, logs);
        }
    }

    /// <summary>Saplings (in the raids, 4-7 m) at most this tall can be snapped by a heavy blow or a charge (Snap).</summary>
    public const float SnapHeight = 7.5f;

    /// <summary>How tall it stands: its model's height, or for a terrain tree's stand-in (no model of its own) its tree's.</summary>
    public float StandingHeight => Height > 0.1f ? Height : Parts.Height;

    /// <summary>
    /// Snapped by a giant's blow or a charge going through it (GroundImpacts): a sapling (at most <see cref="SnapHeight"/>
    /// tall) falls as a felled tree away from <paramref name="from"/>, its wood left lying for whoever wants it; it grows
    /// back like a felled one. False for a bigger tree or one already down.
    /// </summary>
    public bool Snap(Vector3 from)
    {
        if (!IsStanding || StandingHeight > SnapHeight || node == null || !node.BreakDown())
        {
            return false;
        }

        if (notch != null)
        {
            Destroy(notch.gameObject);
            notch = null;
        }

        TreeParts parts = Parts;
        Vector3 away = parts.Base - from;
        away.y = 0f;
        FelledTree.Spawn(parts, Size, null, new List<ItemAmount>(), away.sqrMagnitude > 1e-3f ? away : Vector3.forward);
        return true;
    }

    /// <summary>Chips fly from the trunk, on the side facing the axe, not the edge of the crown.</summary>
    public Vector3 HitPoint(Vector3 from)
    {
        Vector3 toward = from - TrunkCentre;
        toward.y = 0f;
        return TrunkCentre + Vector3.up * ChopHeight + (toward.sqrMagnitude > 0.0001f ? toward.normalized * TrunkRadius : Vector3.zero);
    }

    // The trunk's foot, which needn't be the tree's origin.
    private Vector3 TrunkCentre
    {
        get
        {
            if (trunk == null)
            {
                return transform.position;
            }

            Vector3 foot = transform.TransformPoint(trunk.center);
            foot.y = transform.position.y;
            return foot;
        }
    }

    public float TrunkRadius => trunk != null ? trunk.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 0.3f;

    /// <summary>All the logs a tree of <paramref name="size"/> holds for an axe of <paramref name="axeTier"/>, merged by wood.</summary>
    public static void Roll(int axeTier, float size, System.Func<float> random, List<ItemAmount> into) =>
        Roll(TreeChoppingSettings.Current.Logs(axeTier, size), axeTier, size, random, into);

    /// <summary><paramref name="total"/> logs of a tree of <paramref name="size"/>, merged by wood; all of them
    /// <paramref name="ownWood"/> when the tree has one.</summary>
    public static void Roll(int total, int axeTier, float size, System.Func<float> random, List<ItemAmount> into, ItemDefinition ownWood = null)
    {
        into.Clear();
        if (ownWood != null)
        {
            if (total > 0)
            {
                into.Add(new ItemAmount(ownWood, total));
            }

            return;
        }

        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        var counts = new Dictionary<ItemDefinition, int>();
        for (int i = 0; i < total; i++)
        {
            ItemDefinition wood = rules.RollLog(axeTier, size, random);
            if (wood != null)
            {
                counts.TryGetValue(wood, out int n);
                counts[wood] = n + 1;
            }
        }

        foreach (var pair in counts)
        {
            into.Add(new ItemAmount(pair.Key, pair.Value));
        }
    }
}
