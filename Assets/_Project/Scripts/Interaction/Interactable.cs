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
public sealed class Interactable : MonoBehaviour
{
    private static readonly List<Interactable> active = new List<Interactable>();

    [SerializeField] private InteractionAnimation animationType = InteractionAnimation.GatherLow;
    [Tooltip("Seconds of work until the interaction completes. 0 keeps going until Complete() is called.")]
    [SerializeField, Min(0f)] private float duration = 3f;
    [Tooltip("How close the player must be, measured to the nearest point of this object's colliders.")]
    [SerializeField, Min(0f)] private float range = 1.5f;
    [Tooltip("Disable this object when the interaction completes (the resource is used up).")]
    [SerializeField] private bool deactivateOnComplete = true;

    [SerializeField] private UnityEvent started = new UnityEvent();
    [SerializeField] private UnityEvent completed = new UnityEvent();
    [SerializeField] private UnityEvent cancelled = new UnityEvent();

    private Collider[] colliders;
    private IInteractionCondition[] conditions = System.Array.Empty<IInteractionCondition>();
    private GameObject interactor;
    private float elapsed;
    private bool inUse;
    private bool completeRequested;

    /// <summary>Every enabled Interactable in the scene.</summary>
    public static IReadOnlyList<Interactable> Active => active;

    public InteractionAnimation AnimationType => animationType;
    public float Range => range;
    public bool IsInUse => inUse;
    public bool IsAvailable => isActiveAndEnabled && !inUse;
    /// <summary>Who is working on this right now. Still set while the Completed and Cancelled events run.</summary>
    public GameObject Interactor => interactor;
    /// <summary>0-1 progress toward completion; stays 0 for interactions without a duration.</summary>
    public float Progress => duration > 0f && !HoldOpen ? Mathf.Clamp01(elapsed / duration) : 0f;

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
        conditions = GetComponents<IInteractionCondition>();
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    /// <summary>Horizontal distance from a point to the nearest point of this object.</summary>
    public float DistanceFrom(Vector3 point)
    {
        return DirectionFrom(point).magnitude;
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

        return true;
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
        Vector3 closest = transform.position;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate == null || !candidate.enabled || candidate.isTrigger)
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
}
