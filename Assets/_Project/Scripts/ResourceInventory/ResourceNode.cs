using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A gathering node in the world, such as a tree, a rock or an ore vein, configured by a GatheringNodeDefinition. Before
/// the player starts it checks for the right tool (a Tier II node needs a Tier II tool of its kind), and once gathered it
/// disappears and grows back after the node's respawn time. The yield itself is granted by the ResourceSource beside it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable), typeof(ResourceSource))]
public sealed class ResourceNode : MonoBehaviour, IInteractionCondition, ISaveable, IInteractionName
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
    private Coroutine countdown;

    public GatheringNodeDefinition Definition => node;
    public string InteractionName => node != null ? node.DisplayName : null;
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
        if (Application.isPlaying)
        {
            ApplyVariant();
        }

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
        WatchTimers();
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnGathered);
        if (countdown != null)
        {
            StopCoroutine(countdown);
            countdown = null;
        }
    }

    private bool HasTimer => hideAt >= 0f || (IsDepleted && node != null && node.RespawnSeconds > 0f);

    private void WatchTimers()
    {
        if (countdown == null && isActiveAndEnabled && HasTimer)
        {
            countdown = StartCoroutine(CountDown());
        }
    }

    // Runs only while the break plays or the node grows back: an untouched node costs nothing per frame (there are a
    // thousand round the Haven).
    private IEnumerator CountDown()
    {
        while (HasTimer)
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

            yield return null;
        }

        countdown = null;
    }

    /// <summary>The look this spot gets: the node's own model or one of its definition's variants (the same one every
    /// time for the same spot), at a random size and facing. A variant takes the place and size of the model it replaces
    /// (its "Model" child, or the part holding its mesh), whose colliders stay.</summary>
    private void ApplyVariant()
    {
        Transform model = transform.Find("Model");
        Renderer first = GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => !(r is ParticleSystemRenderer));
        if (node == null || first == null)
        {
            return;
        }

        Transform look = model != null ? model : first.transform;
        Vector3 at = transform.position;
        var random = new System.Random(Mathf.RoundToInt(at.x * 10f) * 73856093 ^ Mathf.RoundToInt(at.z * 10f) * 19349663 ^ node.name.GetHashCode());
        IReadOnlyList<GameObject> variants = node.ModelVariants;
        int pick = random.Next(variants.Count + 1) - 1;
        if (pick >= 0 && variants[pick] != null)
        {
            Bounds original = BoundsOf(look);
            foreach (Renderer part in look.GetComponentsInChildren<Renderer>(true))
            {
                if (part.TryGetComponent(out MeshFilter filter))
                {
                    DestroyImmediate(filter);
                }

                DestroyImmediate(part);
            }

            GameObject variant = Instantiate(variants[pick], look, false);
            variant.name = variants[pick].name;
            HeldItemView.StripToVisual(variant);
            Bounds fitted = BoundsOf(variant.transform);
            float factor = fitted.size.magnitude > 0.0001f ? original.size.magnitude / fitted.size.magnitude : 1f;
            variant.transform.localScale *= factor;
            fitted = BoundsOf(variant.transform);
            variant.transform.position += new Vector3(original.center.x - fitted.center.x, original.min.y - fitted.min.y, original.center.z - fitted.center.z);
        }

        Vector2 scale = node.VariantScale;
        look.localScale *= Mathf.Lerp(Mathf.Min(scale.x, scale.y), Mathf.Max(scale.x, scale.y), (float)random.NextDouble());
        look.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * look.localRotation;
    }

    private static Bounds BoundsOf(Transform root)
    {
        Renderer[] parts = root.GetComponentsInChildren<Renderer>(true);
        if (parts.Length == 0)
        {
            return new Bounds(root.position, Vector3.zero);
        }

        Bounds bounds = parts[0].bounds;
        foreach (Renderer part in parts)
        {
            bounds.Encapsulate(part.bounds);
        }

        return bounds;
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
            if (part != null)
            {
                part.enabled = present;
            }
        }

        interactable.enabled = present;
        if (!present && hideDelay > 0f)
        {
            hideAt = Time.time + hideDelay;
        }
        else
        {
            hideAt = -1f;
            SetRenderers(present);
        }

        WatchTimers();
    }

    private void SetRenderers(bool visible)
    {
        foreach (Renderer part in renderers)
        {
            part.enabled = visible;
        }
    }
}
