using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A kind of gathering node in the world: an oak, a copper vein, a flax patch, a Life Mote. It says what tool gathering
/// needs, how long it takes, what it yields and when it grows back. The yield is a drop table, so a node can give
/// material items (wood, ore, berries), essences, or both. Kept apart from what it yields, so one material can come from
/// several nodes and a node can give several things.
/// This is authored data only; the ResourceNode in the world keeps the runtime state (depleted, regrowth timer).
/// </summary>
[CreateAssetMenu(fileName = "NewGatheringNode", menuName = "Safe Haven/Resources/Gathering Node")]
public sealed class GatheringNodeDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier. Leave empty to use the asset name.")]
    [SerializeField] private string id;
    [Tooltip("Shown on the interaction prompt. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [Tooltip("Progression tier. Higher tiers need better tools and grow in more dangerous places.")]
    [SerializeField, Range(Tiers.Min, Tiers.Max)] private int tier = 1;

    [Header("Yield")]
    [Tooltip("What one gather gives. Items go to the bag, essences to the essence bars.")]
    [SerializeField] private ItemDropTable yields = new ItemDropTable();

    [Header("Gathering")]
    [Tooltip("The tool needed to gather it. None means bare hands always work.")]
    [SerializeField] private ToolType gatherTool;
    [Tooltip("Lowest tool tier that can gather it. 1 requires an actual Tier I tool; only Gather Tool None allows bare hands.")]
    [SerializeField, Range(1, Tiers.Max)] private int requiredToolTier = 1;
    [Tooltip("Animation the player plays while gathering.")]
    [SerializeField] private InteractionAnimation gatherAnimation = InteractionAnimation.GatherLow;
    [Tooltip("Seconds of work to gather once. Not used when the tool has the timing minigame (see Swings).")]
    [SerializeField, Min(0.1f)] private float gatherSeconds = 3f;
    [Tooltip("Swings of the timing minigame needed to gather it, for tools that use the minigame (Gathering Minigame " +
             "settings; the axe and pickaxe by default). Each critical swing adds one extra of the main yield.")]
    [SerializeField, Range(1, 10)] private int swings = 3;
    [Tooltip("How fast the swing bar moves on this node. Above 1 is harder (tough wood, dense ore).")]
    [SerializeField, Range(0.5f, 2f)] private float swingSpeed = 1f;
    [Tooltip("How close the player must stand, in meters.")]
    [SerializeField, Min(0.1f)] private float gatherRange = 1.5f;
    [Tooltip("Remove the node from the world once it has been gathered, until it grows back.")]
    [SerializeField] private bool depleteOnGather = true;
    [Tooltip("Seconds until a gathered node grows back. 0 means it never comes back.")]
    [SerializeField, Min(0f)] private float respawnSeconds = 180f;

    [Header("Break Apart")]
    [Tooltip("When gathered it cracks into about this many tumbling pieces of its own model, instead of shrinking away. 0: it doesn't. How they fly is in its Feel profile (Harvest Feedback). After changing this or its models: Safe Haven › Setup › Resources › Crack Mined Nodes.")]
    [SerializeField, Range(0, 16)] private int burstPieces;
    [Tooltip("Its models, cracked ahead of time. Made by Safe Haven › Setup › Resources › Crack Mined Nodes.")]
    [SerializeField] private NodeFracture fracture;
    [Tooltip("What the burst gives on top of the yield: a few more of what it holds, and the small rocks that break off.")]
    [SerializeField] private ItemDropTable burstYields = new ItemDropTable();

    [Header("Feel")]
    [Tooltip("Sounds, particles and shake for swings and the break when gathered, shared by a family of nodes " +
             "(Wood, Stone, Ore, Fibre). Empty uses the Default profile in Systems › Gathering Minigame.")]
    [SerializeField] private HarvestFeedbackProfile feedback;

    [Header("World")]
    [Tooltip("3D model shown in the world. Used when the node prefab is created or its model replaced.")]
    [SerializeField] private GameObject worldModelPrefab;
    [Tooltip("Generated gathering node prefab.")]
    [SerializeField] private GameObject worldPrefab;
    [Tooltip("Other looks for the same node (stones that each look a little different): each placed node picks its own " +
             "(or keeps its own model), always the same one for the same spot. Colliders and scripts on them are ignored.")]
    [SerializeField] private List<GameObject> modelVariants = new List<GameObject>();
    [Tooltip("Each placed node is scaled by a random amount in this range and turned at random, so copies don't match.")]
    [SerializeField] private Vector2 variantScale = new Vector2(0.9f, 1.1f);

    public string Id => string.IsNullOrWhiteSpace(id) ? MakeId(name) : id.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public int Tier => tier;
    public ItemDropTable Yields => yields;
    public ToolType GatherTool => gatherTool;
    public int RequiredToolTier => gatherTool == ToolType.None ? 0 : Mathf.Max(1, requiredToolTier);
    public InteractionAnimation GatherAnimation => gatherAnimation;
    public float GatherSeconds => gatherSeconds;
    public int Swings => Mathf.Max(1, swings);
    public float SwingSpeed => swingSpeed;
    public float GatherRange => gatherRange;
    public bool DepleteOnGather => depleteOnGather;
    public float RespawnSeconds => respawnSeconds;
    public int BurstPieces => burstPieces;
    public NodeFracture Fracture => fracture;
    public ItemDropTable BurstYields => burstYields;
    public HarvestFeedbackProfile Feedback => feedback;
    public GameObject WorldModelPrefab => worldModelPrefab;
    public GameObject WorldPrefab => worldPrefab;
    public IReadOnlyList<GameObject> ModelVariants => modelVariants;
    public Vector2 VariantScale => variantScale;

    /// <summary>The first thing the node yields, for icons and lists.</summary>
    public Object MainYield => yields.Entries.Count > 0 && yields.Entries[0] != null ? yields.Entries[0].Drop : null;

    private void OnValidate()
    {
        yields.Validate();
        burstYields.Validate();
    }

    private static string MakeId(string source)
    {
        var builder = new StringBuilder(source.Length);
        foreach (char character in source)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
        }

        return builder.ToString();
    }
}
