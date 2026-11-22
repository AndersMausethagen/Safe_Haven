using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum InteractionResult
{
    Completed,
    Cancelled
}

/// <summary>
/// Something the player can work on with the Interact key: a tree, an ore vein, a herb, an NPC.
/// It only describes the interaction (animation, duration, reach) and raises events. PlayerInteractor runs it
/// and the player's animation follows. Hook gameplay (loot, depletion, dialogue) to the Started, Completed
/// and Cancelled events, or call Complete() yourself when the work is done.
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "what the player may do with it, judged in their own game")]
public sealed class Interactable : MonoBehaviour
{
    private static readonly List<Interactable> active = new List<Interactable>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    [SerializeField] private InteractionAnimation animationType = InteractionAnimation.GatherLow;
    [Tooltip("Seconds of work until the interaction completes. 0 keeps going until Complete() is called.")]
    [SerializeField, Min(0f)] private float duration = 3f;
    [Tooltip("How close the player must be, measured to the nearest point of this object's colliders.")]
    [SerializeField, Min(0f)] private float range = 1.5f;
    [Tooltip("Disable this object when the interaction completes (the resource is used up).")]
    [SerializeField] private bool deactivateOnComplete = true;
    [Tooltip("Work: done over Duration with a work bar and animation. Use: done on the press (talking, opening a chest, " +
             "station or menu); Duration and Animation are ignored.")]
    [SerializeField] private InteractionKind kind = InteractionKind.Work;

    [SerializeField] private UnityEvent started = new UnityEvent();
    [SerializeField] private UnityEvent completed = new UnityEvent();
    [SerializeField] private UnityEvent cancelled = new UnityEvent();

    private Collider[] colliders;
    private IInteractionShape shape;
    private IInteractionCondition[] conditions = System.Array.Empty<IInteractionCondition>();
    private GameObject interactor;
    private float elapsed;
    private bool inUse;
    private bool completeRequested;

    /// <summary>Every enabled Interactable in the scene.</summary>
    public static IReadOnlyList<Interactable> Active => active;

    public InteractionAnimation AnimationType => animationType;

    /// <summary>Seconds the work takes. Can be changed while playing, such as the work left on a scaffold.</summary>
    public float Duration
    {
        get => duration;
        set => duration = Mathf.Max(0f, value);
    }
    public float Range => range;

    /// <summary>Work over time, or a Use done on the press (PlayerInteractor completes it at once).</summary>
    public InteractionKind Kind => kind;

    public bool IsInUse => inUse;
    public bool IsAvailable => isActiveAndEnabled && !inUse;
    /// <summary>Who is working on this right now. Still set while the Completed and Cancelled events run.</summary>
    public GameObject Interactor => interactor;
    /// <summary>
    /// 0-1 progress toward completion; stays 0 for interactions without a duration. Work that is kept between tries (a scaffold
    /// half raised) shows its own share instead (ShownProgress), so stopping and starting again does not empty the bar.
    /// </summary>
    public float Progress => duration > 0f && !HoldOpen ? (ShownProgress != null ? Mathf.Clamp01(ShownProgress()) : Mathf.Clamp01(elapsed / duration)) : 0f;

    /// <summary>Seconds into the current interaction.</summary>
    public float Elapsed => inUse ? elapsed : 0f;

    /// <summary>
    /// The share shown while working when the work outlives one try (Construction: the building's share done). Set from Started;
    /// cleared when the interaction ends.
    /// </summary>
    public System.Func<float> ShownProgress { get; set; }

    /// <summary>
    /// Ignores the duration for the current interaction, so it runs until Complete() is called (the gathering minigame
    /// sets it). Cleared when the interaction ends.
    /// </summary>
    public bool HoldOpen { get; set; }

    public UnityEvent Started => started;
    public UnityEvent Completed => completed;
    public UnityEvent Cancelled => cancelled;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
        shape = GetComponent<IInteractionShape>();
        conditions = GetComponents<IInteractionCondition>();
    }

    /// <summary>Sets it up from code (an interaction made while playing, such as a construction site, or a Use such as a
    /// door to knock at).</summary>
    public void Configure(InteractionAnimation animation, float seconds, float reach, bool deactivateWhenComplete,
        InteractionKind? interaction = null)
    {
        kind = interaction ?? kind;
        animationType = animation;
        duration = Mathf.Max(0f, seconds);
        range = Mathf.Max(0f, reach);
        deactivateOnComplete = deactivateWhenComplete;
        colliders = GetComponentsInChildren<Collider>();
        shape = GetComponent<IInteractionShape>();
        conditions = GetComponents<IInteractionCondition>();
        if (isActiveAndEnabled)
        {
            InteractableGrid.Remove(this);
            InteractableGrid.Add(this);
        }
    }

    private void OnEnable()
    {
        active.Add(this);
        InteractableGrid.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
        InteractableGrid.Remove(this);
    }

    // Kept by InteractableGrid: the cells it is filed under, and the last query that offered it.
    internal RectInt FiledArea { get; set; }
    internal bool IsFiled { get; set; }
    internal int GridStamp { get; set; }
    internal bool HasShape => shape != null;

    /// <summary>What it covers, for filing it by place: its colliders in use, or its position when it has none.</summary>
    internal Bounds FilingBounds()
    {
        var bounds = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!IsUsable(colliders[i])) continue;
                if (any) bounds.Encapsulate(colliders[i].bounds);
                else bounds = colliders[i].bounds;
                any = true;
            }
        }

        return bounds;
    }

    /// <summary>Horizontal distance from a point to the nearest point of this object.</summary>
    public float DistanceFrom(Vector3 point)
    {
        return DirectionFrom(point).magnitude;
    }

    /// <summary>
    /// A cheap first test for scans over every Interactable (a thousand trees and rocks round the Haven): false when no
    /// collider's bounds come within <paramref name="distance"/> (horizontally) of the point, so the precise
    /// <see cref="DistanceFrom"/>, which asks the physics engine per collider, can be skipped. True does not promise
    /// the object is that close.
    /// </summary>
    public bool MightBeWithin(Vector3 point, float distance)
    {
        if (shape != null)
        {
            return DistanceFrom(point) <= distance;
        }

        float reach = distance * distance;
        bool anyCollider = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (!IsUsable(candidate))
            {
                continue;
            }

            anyCollider = true;
            Bounds bounds = candidate.bounds;
            // Two-argument Max: the three-argument overload takes a params array and allocates on every call.
            float dx = Mathf.Max(0f, Mathf.Max(bounds.min.x - point.x, point.x - bounds.max.x));
            float dz = Mathf.Max(0f, Mathf.Max(bounds.min.z - point.z, point.z - bounds.max.z));
            if (dx * dx + dz * dz <= reach)
            {
                return true;
            }
        }

        if (anyCollider)
        {
            return false;
        }

        Vector3 offset = transform.position - point;
        return offset.x * offset.x + offset.z * offset.z <= reach;
    }

    /// <summary>Horizontal vector from a point to the nearest point of this object.</summary>
    public Vector3 DirectionFrom(Vector3 point)
    {
        Vector3 toNearest = ClosestPoint(point) - point;
        toNearest.y = 0f;
        return toNearest;
    }

    /// <summary>Marks the work as finished. The player's interaction ends on its next update.</summary>
    public void Complete()
    {
        if (inUse)
        {
            completeRequested = true;
        }
    }

    // Called by PlayerInteractor.
    public bool TryBegin(GameObject user)
    {
        return TryBegin(user, out _);
    }

    /// <summary>Checks the same requirements as TryBegin without starting work or raising any events.</summary>
    public bool CanInteract(GameObject user, out string blockedReason)
    {
        blockedReason = null;
        if (!IsAvailable)
        {
            blockedReason = inUse ? "Already in use." : "Unavailable.";
            return false;
        }

        foreach (IInteractionCondition condition in conditions)
        {
            if (!condition.CanInteract(user, out blockedReason))
            {
                return false;
            }
        }

        // In a friend's Haven, what is the host's alone is refused here, once, for every such thing (InAFriendsHaven: HostOnly).
        if (HavenVisit.InHostsHaven && HostsAlone())
        {
            blockedReason = HavenVisit.Refusal;
            return false;
        }

        return true;
    }

    // Whether any part answering this is the host's alone: looked up once (a visitor's indicator asks every frame).
    private bool? hostsAlone;

    private bool HostsAlone()
    {
        if (hostsAlone.HasValue)
        {
            return hostsAlone.Value;
        }

        hostsAlone = false;
        foreach (MonoBehaviour part in GetComponents<MonoBehaviour>())
        {
            if (part != null && InAFriendsHavenAttribute.Of(part.GetType()) == HavenUse.HostOnly)
            {
                hostsAlone = true;
                break;
            }
        }

        return hostsAlone.Value;
    }

    /// <summary>Starts work only when every requirement passes; otherwise returns the reason shown by the indicator.</summary>
    public bool TryBegin(GameObject user, out string blockedReason)
    {
        if (!CanInteract(user, out blockedReason))
        {
            return false;
        }

        inUse = true;
        interactor = user;
        elapsed = 0f;
        completeRequested = false;
        started.Invoke();
        return true;
    }

    /// <summary>Advances the work and returns true once it is finished.</summary>
    public bool Tick(float deltaTime)
    {
        if (!inUse)
        {
            return false;
        }

        elapsed += deltaTime;
        return completeRequested || (duration > 0f && !HoldOpen && elapsed >= duration);
    }

    public void End(InteractionResult result)
    {
        if (!inUse)
        {
            return;
        }

        inUse = false;
        elapsed = 0f;
        HoldOpen = false;
        ShownProgress = null;

        try
        {
            if (result == InteractionResult.Completed)
            {
                completed.Invoke();
                if (deactivateOnComplete)
                {
                    gameObject.SetActive(false);
                }
            }
            else
            {
                cancelled.Invoke();
            }
        }
        finally
        {
            interactor = null;
        }
    }

    private Vector3 ClosestPoint(Vector3 point)
    {
        if (shape != null)
        {
            return shape.ClosestPoint(point);
        }

        Vector3 closest = transform.position;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (!IsUsable(candidate))
            {
                continue;
            }

            // Collider.ClosestPoint is unsupported on concave mesh colliders, so use their bounds.
            bool concave = candidate is MeshCollider mesh && !mesh.convex;
            Vector3 candidatePoint = concave ? candidate.bounds.ClosestPoint(point) : candidate.ClosestPoint(point);
            float sqrDistance = (candidatePoint - point).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = candidatePoint;
            }
        }

        return closest;
    }

    private static bool IsUsable(Collider candidate) =>
        candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy && !candidate.isTrigger;
}
