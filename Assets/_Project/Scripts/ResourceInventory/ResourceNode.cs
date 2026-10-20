using UnityEngine;

/// <summary>
/// A gathering node in the world, such as a tree, a rock or an ore vein, configured by a GatheringNodeDefinition. Before
/// the player starts it checks for the right tool (a Tier II node needs a Tier II tool of its kind), and once gathered it
/// disappears and grows back after the node's respawn time. The yield itself is granted by the ResourceSource beside it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable), typeof(ResourceSource))]
public sealed class ResourceNode : MonoBehaviour, IInteractionCondition, ISaveable
{
    [System.Serializable]
    private struct SavedNode
    {
        public bool depleted;
        public float respawnRemaining;
    }

    [SerializeField] private GatheringNodeDefinition node;

    private Interactable interactable;
    private ResourceNodeFeedback feedback;
    private Renderer[] renderers;
    private Collider[] colliders;
    private float respawnAt;
    private float hideAt = -1f;

    public GatheringNodeDefinition Definition => node;
    public bool IsDepleted { get; private set; }

    /// <summary>A minigame swing landed on this node: its result and where the swinger stood.</summary>
    public event System.Action<SwingResult, Vector3> Swung;

    /// <summary>The node was gathered (any way, also by hand).</summary>
    public event System.Action Harvested;

    /// <summary>The node grew back after being gathered.</summary>
    public event System.Action Regrown;

    /// <summary>Seconds until the node grows back, or 0 when it is present.</summary>
    public float RespawnRemaining => IsDepleted ? Mathf.Max(0f, respawnAt - Time.time) : 0f;

    public string SaveKey => "node";

    // The regrowth time is saved as time left, since the game clock starts over after loading.
    public string CaptureState()
    {
        return JsonUtility.ToJson(new SavedNode { depleted = IsDepleted, respawnRemaining = RespawnRemaining });
    }

    public void RestoreState(string state)
    {
        SavedNode saved = JsonUtility.FromJson<SavedNode>(state);
        if (saved.depleted)
        {
            respawnAt = Time.time + saved.respawnRemaining;
            SetPresent(false);
        }
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        if (!TryGetComponent(out feedback))
        {
            feedback = gameObject.AddComponent<ResourceNodeFeedback>();
        }
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnGathered);
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnGathered);
    }

    private void Update()
    {
        if (hideAt >= 0f && Time.time >= hideAt)
        {
            hideAt = -1f;
            SetRenderers(false);
        }

        if (IsDepleted && node != null && node.RespawnSeconds > 0f && Time.time >= respawnAt)
        {
            SetPresent(true);
            Regrown?.Invoke();
        }
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        reason = null;
        if (node == null || node.GatherTool == ToolType.None)
        {
            return true;
        }

        if (ItemInventory.GatheringTier(user, node.GatherTool) >= node.RequiredToolTier)
        {
            return true;
        }

        reason = $"Needs a Tier {Tiers.ToRoman(node.RequiredToolTier)} {node.GatherTool} to gather {node.DisplayName}.";
        return false;
    }

    /// <summary>Announces a minigame swing on this node (called by PlayerGathering) for its feedback and listeners.</summary>
    public void NotifySwing(SwingResult result, Vector3 from)
    {
        Swung?.Invoke(result, from);
    }

    private void OnGathered()
    {
        Harvested?.Invoke();
        if (node == null || !node.DepleteOnGather)
        {
            return;
        }

        respawnAt = Time.time + node.RespawnSeconds;
        SetPresent(false, feedback != null ? feedback.BreakSeconds : 0f);
    }

    // The node stays active while depleted so it can count down to growing back. Its model may stay visible a moment
    // longer than its colliders so the break can play.
    private void SetPresent(bool present, float hideDelay = 0f)
    {
        IsDepleted = !present;
        foreach (Collider part in colliders)
        {
            part.enabled = present;
        }

        interactable.enabled = present;
        if (!present && hideDelay > 0f)
        {
            hideAt = Time.time + hideDelay;
            return;
        }

        hideAt = -1f;
        SetRenderers(present);
    }

    private void SetRenderers(bool visible)
    {
        foreach (Renderer part in renderers)
        {
            part.enabled = visible;
        }
    }
}
