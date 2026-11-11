using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The player's side of horses: holding a lassoed one (feet planted, facing it, hands on the rope, so no swing or other
/// interaction gets in the way) and riding one. In the saddle the player sits on the horse's back (PlayerMovement's seat,
/// as in a boat) and steers it with the movement keys, relative to the camera as walking is; Space gallops while the
/// horse has the stamina, the sneak key walks it, and E gets down (or, on a breaking ride, gives the barley it asks for).
/// On a horse of their own the player fights from the saddle: their weapon swings and their bow draws as on foot
/// (PlayerStateMachine › CanFightAboard), the pack's rider clips show it (PlayerRidingAnimation), and Horsemanship's
/// Mounted Damage adds to it. Getting on and off plays the pack's clips while the body is moved between the ground beside
/// the horse and the saddle. Added to the player the first time it is needed (<see cref="For"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRiding : MonoBehaviour
{
    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Damageable health;
    private HorseTaming holding;
    private bool taught;
    private Vector3 lastSeat;
    private float ridden;
    private PlayerRidingAnimation body;
    private Coroutine moving;

    /// <summary>True while getting on or off: no steering, no getting down again.</summary>
    public bool IsMounting => moving != null;

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
        body = TryGetComponent(out PlayerRidingAnimation found) ? found : gameObject.AddComponent<PlayerRidingAnimation>();
    }

    /// <summary>How much harder a blow from the saddle lands: Horsemanship's Mounted Damage, and a quarter more at a gallop.
    /// 1 on foot.</summary>
    public static float MountedScale(GameObject player)
    {
        if (player == null || !player.TryGetComponent(out PlayerRiding riding) || riding.Riding == null)
        {
            return 1f;
        }

        return Mathf.Max(0.2f, 1f + PlayerHorsemanship.MountedDamage()) * (riding.Riding.Motion.IsGalloping ? 1.25f : 1f);
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

        // Gone (a scene change, a load): straight down, no clip to play out.
        if (Riding != null)
        {
            Dismount(thrown: true);
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

    public void Mount(Horse horse) => Mount(horse, animate: true);

    public void Mount(Horse horse, bool animate)
    {
        if (horse == null || !horse.IsAlive || horse.Rider != null || horse.IsStabled || !horse.IsGrown)
        {
            return;
        }

        if (Riding != null)
        {
            Dismount(thrown: true);
        }

        Riding = horse;
        horse.Rider = this;
        stateMachine.SetAboard(true);
        Vector3 from = horse.Seat.InverseTransformPoint(transform.position);
        movement.SitAt(horse.Seat, horse.SeatOffset);
        horse.Motion.BeginRidden();
        if (animate && body.HasLayer)
        {
            bool right = Vector3.Dot(transform.position - horse.transform.position, horse.transform.right) > 0f;
            body.PlayMount(true, right);
            moving = StartCoroutine(Climb(horse, from, horse.SeatOffset, null));
        }

        lastSeat = horse.transform.position;
        ridden = 0f;
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
        StopMoving();
        Mount(tamed, animate: false);
    }

    // Moves the body from one place by the seat to another while the clip plays, then does what comes after.
    private IEnumerator Climb(Horse horse, Vector3 from, Vector3 to, System.Action then)
    {
        float started = Time.time;
        while (Time.time - started < PlayerRidingAnimation.MountSeconds && horse != null)
        {
            float t = Mathf.SmoothStep(0f, 1f, (Time.time - started) / PlayerRidingAnimation.MountSeconds);
            movement.SitAt(horse.Seat, Vector3.Lerp(from, to, t));
            horse.Motion.Ride(Vector3.zero, 0);
            yield return null;
        }

        if (horse != null)
        {
            movement.SitAt(horse.Seat, to);
        }

        moving = null;
        then?.Invoke();
    }

    private void StopMoving()
    {
        if (moving != null)
        {
            StopCoroutine(moving);
            moving = null;
        }
    }

    /// <summary>Gets down beside the horse (or is thrown off it, to whichever side has ground).</summary>
    public void Dismount(bool thrown)
    {
        Horse horse = Riding;
        if (horse == null)
        {
            return;
        }

        StopMoving();
        Vector3 side = horse.transform.right * (thrown ? 1.8f : 1.2f);
        Vector3 spot = horse.transform.position + side;
        bool right = true;
        if (!NavMesh.SamplePosition(spot, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
        {
            right = false;
            if (!NavMesh.SamplePosition(horse.transform.position - side, out hit, 2.5f, NavMesh.AllAreas))
            {
                hit.position = horse.transform.position;
            }
        }

        // Getting down: the clip, the body lowered to the ground beside it, and then the horse is free.
        if (!thrown && horse.IsAlive && body.HasLayer)
        {
            stateMachine.CanFightAboard = false;
            body.PlayMount(false, right);
            Vector3 ground = hit.position;
            moving = StartCoroutine(Climb(horse, horse.SeatOffset, horse.Seat.InverseTransformPoint(ground), () => GetDown(horse, ground)));
            return;
        }

        GetDown(horse, hit.position);
    }

    private void GetDown(Horse horse, Vector3 at)
    {
        if (Riding != horse)
        {
            return;
        }

        Riding = null;
        horse.Rider = null;
        stateMachine.CanFightAboard = false;
        CountRidden(horse, force: true);
        horse.Motion.EndRidden();
        movement.StandAt(at);
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
            if (IsMounting)
            {
                return;
            }

            if (Riding.Motion.IsSwimming)
            {
                GameMessages.Post("Not in deep water: ride it to the bank first.", MessageKind.Warning);
                return;
            }

            Dismount(thrown: false);
        }
    }

    // The road ridden: every hundred metres the bond grows and Horsemanship is trained (a kilometre's worth every kilometre).
    private void CountRidden(Horse horse, bool force = false)
    {
        Vector3 at = horse.transform.position;
        Vector3 step = at - lastSeat;
        step.y = 0f;
        lastSeat = at;
        // A warp (a portal, a load) is not riding.
        if (step.sqrMagnitude < 25f * 25f)
        {
            ridden += step.magnitude;
        }

        if (ridden >= 100f || force && ridden > 1f)
        {
            if (horse.IsTamed)
            {
                horse.Ridden(ridden);
                PlayerHorsemanship.Practise(HorseWork.Kilometre, ridden / 1000f);
            }

            ridden = 0f;
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

        CountRidden(Riding);
        stateMachine.CanFightAboard = Riding.IsTamed && !Riding.Motion.IsSwimming && holding == null && !IsMounting;
        if (IsMounting)
        {
            return;
        }

        bool free = !GamePause.IsPaused && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen) && !DialogueBox.IsOpen;
        Vector3 steer = free ? movement.MovementInput : Vector3.zero;
        int gait = free && GameInput.Dodge != null && GameInput.Dodge.IsPressed() ? 1
            : free && GameInput.Sneak != null && GameInput.Sneak.IsPressed() ? -1 : 0;
        Riding.Motion.Ride(steer, gait);
    }
}
