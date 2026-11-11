using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The player's side of horses: holding a lassoed one (feet planted, facing it, hands on the rope, so no swing or other
/// interaction gets in the way) and riding one. In the saddle the player sits on the horse's back (PlayerMovement's seat,
/// as in a boat) and steers it with the movement keys, relative to the camera as walking is; Space gallops while the
/// horse has the stamina, the sneak key walks it, and E gets down (or, on a breaking ride, gives the barley it asks for).
/// Added to the player the first time it is needed (<see cref="For"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRiding : MonoBehaviour
{
    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Damageable health;
    private HorseTaming holding;
    private bool taught;

    /// <summary>The horse ridden now, or null.</summary>
    public Horse Riding { get; private set; }
    public bool IsRiding => Riding != null;
    public bool IsHoldingRope => holding != null;

    /// <summary>The player's riding, added the first time it is asked for. Null for anything that isn't the player.</summary>
    public static PlayerRiding For(GameObject user)
    {
        if (user == null || !user.TryGetComponent(out PlayerMovement _))
        {
            return null;
        }

        return user.TryGetComponent(out PlayerRiding riding) ? riding : user.AddComponent<PlayerRiding>();
    }

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        stateMachine.AboardInteraction += OnInteract;
        if (health != null)
        {
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        stateMachine.AboardInteraction -= OnInteract;
        if (health != null)
        {
            health.Died -= OnDied;
        }

        if (Riding != null)
        {
            Dismount(thrown: false);
        }

        ReleaseRope();
    }

    // ----- On the rope -----

    public void HoldRope(HorseTaming horse)
    {
        holding = horse;
        stateMachine.SetAboard(true);
        movement.SetSpeedScale(this, 0f);
    }

    public void ReleaseRope()
    {
        if (holding == null)
        {
            return;
        }

        holding = null;
        movement.ClearFocusDirection();
        movement.SetSpeedScale(this, 1f);
        if (Riding == null)
        {
            stateMachine.SetAboard(false);
        }
    }

    // ----- In the saddle -----

    public void Mount(Horse horse)
    {
        if (horse == null || !horse.IsAlive || horse.Rider != null)
        {
            return;
        }

        if (Riding != null)
        {
            Dismount(thrown: false);
        }

        Riding = horse;
        horse.Rider = this;
        stateMachine.SetAboard(true);
        movement.SitAt(horse.Seat, horse.SeatOffset);
        horse.Motion.BeginRidden();
        HorseHud.Ensure();
        if (!taught && horse.IsTamed)
        {
            taught = true;
            GameMessages.Post($"Steer with the movement keys. Space gallops while it has the breath, {GameInput.KeyLabel(GameInput.Sneak)} walks it, {GameInput.KeyLabel(GameInput.Interact)} gets you down.");
        }
    }

    /// <summary>The tamed copy takes the wild horse's place under the rider (HorseTaming).</summary>
    public void SwapTo(Horse tamed)
    {
        Horse was = Riding;
        if (was != null)
        {
            was.Rider = null;
        }

        Riding = null;
        Mount(tamed);
    }

    /// <summary>Gets down beside the horse (or is thrown off it, to whichever side has ground).</summary>
    public void Dismount(bool thrown)
    {
        Horse horse = Riding;
        if (horse == null)
        {
            return;
        }

        Riding = null;
        horse.Rider = null;
        horse.Motion.EndRidden();
        Vector3 side = horse.transform.right * (thrown ? 1.8f : 1.2f);
        Vector3 spot = horse.transform.position + side;
        if (!NavMesh.SamplePosition(spot, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)
            && !NavMesh.SamplePosition(horse.transform.position - side, out hit, 2.5f, NavMesh.AllAreas))
        {
            hit.position = horse.transform.position;
        }

        movement.StandAt(hit.position);
        if (holding == null)
        {
            stateMachine.SetAboard(false);
        }

        if (horse.Taming != null)
        {
            horse.Taming.OnRiderLeft();
        }
    }

    private void OnInteract()
    {
        HorseTaming taming = holding != null ? holding : Riding != null ? Riding.Taming : null;
        if (taming != null && taming.OnHandlerInteract())
        {
            return;
        }

        if (Riding != null)
        {
            Dismount(thrown: false);
        }
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        if (Riding != null)
        {
            Dismount(thrown: true);
        }

        ReleaseRope();
    }

    private void Update()
    {
        if (holding != null)
        {
            Vector3 toward = holding.transform.position - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.01f)
            {
                movement.SetFocusDirection(toward.normalized);
            }
        }

        if (Riding == null)
        {
            return;
        }

        if (!Riding.IsAlive)
        {
            Dismount(thrown: true);
            return;
        }

        bool free = !GamePause.IsPaused && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen) && !DialogueBox.IsOpen;
        Vector3 steer = free ? movement.MovementInput : Vector3.zero;
        int gait = free && GameInput.Dodge != null && GameInput.Dodge.IsPressed() ? 1
            : free && GameInput.Sneak != null && GameInput.Sneak.IsPressed() ? -1 : 0;
        Riding.Motion.Ride(steer, gait);
    }
}
