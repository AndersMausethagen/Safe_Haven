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
public sealed class PlayerInteractor : MonoBehaviour
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
        if (health != null)
        {
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        stateMachine.InteractionRequested -= OnInteractionRequested;
        stateMachine.StateChanged -= OnPlayerStateChanged;
        if (health != null)
        {
            health.Died -= OnDied;
        }

        if (isInteracting)
        {
            End(InteractionResult.Cancelled);
        }
    }

    private void Update()
    {
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
    private void OnDied(Damageable victim, DamageInfo killingBlow) => CancelInteraction();

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
            blockedReason = "Move closer to interact.";
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

        // What is in the hands is always nearest, so it only answers when nothing else in reach does: a cart can be
        // wheeled up to a chest or the portal and used without dropping it.
        return held != null && !nearestInReach ? held : nearest;
    }
}
