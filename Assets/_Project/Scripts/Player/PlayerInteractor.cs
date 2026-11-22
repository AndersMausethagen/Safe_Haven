using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the player's side of an interaction. On Interact it picks the nearest Interactable in reach, moves the
/// player into PlayerState.Interacting, faces the target and ticks it until it completes. It ends the interaction
/// when the player leaves that state (movement, Escape, ...), so every way out is reported as a cancel.
/// Animation and other presentation observe InteractionStarted and InteractionEnded.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerStateMachine))]
public sealed class PlayerInteractor : MonoBehaviour, IPlayerActivity
{
    [Tooltip("Nearby objects can be previewed before entering interaction reach.")]
    [SerializeField, Min(0f)] private float previewRange = 6f;

    private readonly List<Interactable> nearby = new List<Interactable>();
    private PlayerMovement movement;
    private PlayerStateMachine stateMachine;
    private Damageable health;
    private PlayerTransport transportUser;
    private Interactable current;
    private bool isInteracting;

    /// <summary>How long Interact is held on something with a hold of its own (IInteractionHold) before the hold runs, not the tap.</summary>
    public const float HoldSeconds = 0.35f;

    // Pressed on something with a hold of its own: waiting to see whether it is a tap or a hold (UpdateHold).
    private Interactable pressed;
    private float pressedAt;

    /// <summary>The interaction in progress, or null.</summary>
    public Interactable Current => isInteracting ? current : null;

    public event Action<Interactable> InteractionStarted;
    /// <summary>The Interactable may already be destroyed if it was removed during the interaction.</summary>
    public event Action<Interactable, InteractionResult> InteractionEnded;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
        transportUser = GetComponent<PlayerTransport>();
    }

    private void OnEnable()
    {
        stateMachine.InteractionRequested += OnInteractionRequested;
        stateMachine.StateChanged += OnPlayerStateChanged;
        PlayerInterrupts.For(gameObject).Add(this);
    }

    private void OnDisable()
    {
        stateMachine.InteractionRequested -= OnInteractionRequested;
        stateMachine.StateChanged -= OnPlayerStateChanged;
        if (TryGetComponent(out PlayerInterrupts interrupts))
        {
            interrupts.Remove(this);
        }

        if (isInteracting)
        {
            End(InteractionResult.Cancelled);
        }
    }

    private void Update()
    {
        if (pressed != null)
        {
            UpdateHold();
        }

        if (!isInteracting)
        {
            return;
        }

        if (current == null || !current.isActiveAndEnabled)
        {
            End(InteractionResult.Cancelled);
            return;
        }

        // CanMove is false while interacting, so the interactor turns the player toward the target itself.
        movement.TurnToward(current.DirectionFrom(transform.position));

        if (current.Tick(Time.deltaTime))
        {
            End(InteractionResult.Completed);
        }
    }

    // Killed while at work (a scaffold hammered at home when the breach came): the work stops with them, here for every death
    // (home, raid, co-op), and they don't wake at the portal still hammering the far-off site.
    /// <summary>Death alone ends it (PlayerInterrupts tells every activity of a death).</summary>
    public PlayerInterrupt EndsOn => PlayerInterrupt.None;

    public void Interrupt(PlayerInterrupt reason) => CancelInteraction();

    /// <summary>Stops what the player is working at, unfinished (struck down, say: work doesn't go on without them).</summary>
    public void CancelInteraction()
    {
        if (isInteracting)
        {
            End(InteractionResult.Cancelled);
        }
    }

    /// <summary>The nearest target shared by the world indicator and input. Reachable objects take priority.</summary>
    public Interactable FindFocus()
    {
        return Current != null ? Current : FindNearest();
    }

    /// <summary>The reason given when the target is only out of reach (the interaction marker says no more than its name then).</summary>
    public const string TooFar = "Move closer to interact.";

    /// <summary>Read-only readiness, including the player's state, reach and the target's tool requirements.</summary>
    public bool CanInteractWith(Interactable target, out string blockedReason)
    {
        blockedReason = null;
        if (!isActiveAndEnabled || target == null || (health != null && !health.IsAlive))
        {
            blockedReason = "Unavailable.";
            return false;
        }

        if (isInteracting || !stateMachine.CanMove || movement.IsDashing)
        {
            blockedReason = "Busy.";
            return false;
        }

        if (!movement.IsGrounded)
        {
            blockedReason = "Stand on the ground to interact.";
            return false;
        }

        if (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen)
        {
            blockedReason = "Close the open window.";
            return false;
        }

        if (!target.CanInteract(gameObject, out blockedReason))
        {
            return false;
        }

        if (target.DistanceFrom(transform.position) > target.Range)
        {
            blockedReason = TooFar;
            return false;
        }

        return true;
    }

    private void OnInteractionRequested()
    {
        if (isInteracting)
        {
            return;
        }

        Interactable target = FindFocus();
        if (target == null)
        {
            return;
        }

        // Something that does another thing when held (IInteractionHold): wait for the release, or the hold, before either.
        if (target.TryGetComponent(out IInteractionHold hold) && hold.HoldVerb(gameObject) != null && CanInteractWith(target, out _))
        {
            pressed = target;
            pressedAt = Time.unscaledTime;
            return;
        }

        Begin(target);
    }

    // A press waiting to be a tap or a hold: let go early, the tap runs (Begin); held on, the hold does. Walking off, a window opening or
    // the target going forgets the press.
    private void UpdateHold()
    {
        Interactable target = pressed;
        bool down = GameInput.Interact != null && GameInput.Interact.IsPressed();
        if (target == null || !target.isActiveAndEnabled || !CanInteractWith(target, out _) || !target.TryGetComponent(out IInteractionHold hold))
        {
            pressed = null;
            return;
        }

        if (down && Time.unscaledTime - pressedAt < HoldSeconds)
        {
            return;
        }

        pressed = null;
        if (down && hold.HoldVerb(gameObject) != null)
        {
            hold.Held(gameObject);
        }
        else
        {
            Begin(target);
        }
    }

    private void Begin(Interactable target)
    {
        if (!CanInteractWith(target, out string blockedReason) || !target.TryBegin(gameObject, out blockedReason))
        {
            GameMessages.Post(blockedReason, MessageKind.Warning);
            return;
        }

        // A Use is done on the press: no Interacting state, animation or work bar.
        if (target.Kind == InteractionKind.Use)
        {
            target.End(InteractionResult.Completed);
            return;
        }

        if (!stateMachine.BeginInteraction())
        {
            target.End(InteractionResult.Cancelled);
            return;
        }

        current = target;
        isInteracting = true;
        InteractionStarted?.Invoke(target);
    }

    private void OnPlayerStateChanged(PlayerState previous, PlayerState next)
    {
        if (isInteracting && next != PlayerState.Interacting)
        {
            End(InteractionResult.Cancelled);
        }
    }

    private void End(InteractionResult result)
    {
        Interactable finished = current;
        current = null;
        // Cleared first so the state change below doesn't re-enter this method.
        isInteracting = false;

        stateMachine.EndInteraction();

        if (finished != null)
        {
            finished.End(result);
        }

        InteractionEnded?.Invoke(finished, result);
    }

    private Interactable FindNearest()
    {
        Vector3 origin = transform.position;
        Interactable nearest = null;
        float nearestDistance = float.MaxValue;
        bool nearestInReach = false;
        Interactable held = transportUser != null && transportUser.Using != null ? transportUser.Using.Interactable : null;

        // Only the Interactables filed near the player: the scene holds well over a thousand.
        nearby.Clear();
        InteractableGrid.Near(origin, Mathf.Max(previewRange, InteractableGrid.LongestRange), nearby);
        List<Interactable> candidates = nearby;
        for (int i = 0; i < candidates.Count; i++)
        {
            Interactable candidate = candidates[i];
            if (candidate == null || candidate == held || !candidate.IsAvailable)
            {
                continue;
            }

            float reach = Mathf.Max(previewRange, candidate.Range);
            if (!candidate.MightBeWithin(origin, reach))
            {
                continue;
            }

            float distance = candidate.DistanceFrom(origin);
            bool inReach = distance <= candidate.Range;
            if (distance > reach)
            {
                continue;
            }

            if (nearest == null || (inReach && !nearestInReach) || (inReach == nearestInReach && distance < nearestDistance))
            {
                nearest = candidate;
                nearestDistance = distance;
                nearestInReach = inReach;
            }
        }

        // Between several in reach, the one under the mouse (the user, 2026-11-19); a pad keeps the nearest.
        if (nearestInReach && !GameInput.UsingGamepad)
        {
            Interactable pointed = Pointed(origin, candidates, held);
            if (pointed != null)
            {
                nearest = pointed;
            }
        }

        // What is in the hands is always nearest, so it only answers when nothing else in reach does: a cart can be
        // wheeled up to a chest or the portal and used without dropping it.
        return held != null && !nearestInReach ? held : nearest;
    }

    // Pixels (at 1080 high) from the cursor a thing's middle may be on screen to count as pointed at, without a collider under it.
    private const float PointedPixels = 56f;
    private const float PointedRayLength = 120f;
    private static readonly RaycastHit[] pointedHits = new RaycastHit[16];

    /// <summary>
    /// The candidate in reach that the cursor is over: one whose collider the cursor's ray meets first, else the one whose
    /// middle is nearest the cursor on screen within <see cref="PointedPixels"/>. Null when the cursor is over none of them.
    /// </summary>
    private Interactable Pointed(Vector3 origin, List<Interactable> candidates, Interactable held)
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return null;
        }

        Vector2 cursor = GameInput.PointerPosition;
        Ray ray = view.ScreenPointToRay(cursor);
        int count = Physics.RaycastNonAlloc(ray, pointedHits, PointedRayLength, ~0, QueryTriggerInteraction.Collide);
        Interactable hitFirst = null;
        float hitDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Interactable owner = pointedHits[i].collider != null ? pointedHits[i].collider.GetComponentInParent<Interactable>() : null;
            if (owner != null && pointedHits[i].distance < hitDistance && InReach(owner, origin, candidates, held))
            {
                hitFirst = owner;
                hitDistance = pointedHits[i].distance;
            }
        }

        if (hitFirst != null)
        {
            return hitFirst;
        }

        float limit = PointedPixels * Screen.height / 1080f;
        Interactable closest = null;
        float closestPixels = limit * limit;
        foreach (Interactable candidate in candidates)
        {
            if (!InReach(candidate, origin, candidates, held))
            {
                continue;
            }

            Vector3 screen = view.WorldToScreenPoint(candidate.transform.position + Vector3.up * 0.5f);
            if (screen.z <= 0f)
            {
                continue;
            }

            float pixels = ((Vector2)screen - cursor).sqrMagnitude;
            if (pixels < closestPixels)
            {
                closestPixels = pixels;
                closest = candidate;
            }
        }

        return closest;
    }

    private static bool InReach(Interactable candidate, Vector3 origin, List<Interactable> candidates, Interactable held) =>
        candidate != null && candidate != held && candidate.IsAvailable && candidates.Contains(candidate) && candidate.DistanceFrom(origin) <= candidate.Range;
}
