using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The player's side of horses: holding a lassoed one (feet planted, facing it, hands on the rope, so no swing or other
/// interaction gets in the way) and riding one. In the saddle the player sits on the horse's back (PlayerMovement's seat,
/// as in a boat) and steers it with the movement keys, relative to the camera as walking is; Space gallops while the
/// horse has the stamina (standing still, it rears up on its hind legs), the sneak key walks it, and the whistle key (X)
/// gets down. E is the breaking ride's alone (the
/// barley it asks for), so feeding it never throws the player off by mistake.
/// On a horse of their own the player fights from the saddle: their weapon swings and their bow draws as on foot
/// (PlayerStateMachine › CanFightAboard), the pack's rider clips show it (PlayerRidingAnimation), and Horsemanship's
/// Mounted Damage adds to it. Getting on is only from beside the horse: it turns its side to the player, who steps to its
/// shoulder and climbs on along the pack's clip's own path (HorseSettings › Mount Path); getting off runs it backwards.
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
    private Vector3 lastSeat;
    private float ridden;
    private PlayerRidingAnimation body;
    private Coroutine moving;
    private bool feetFree;
    private float nextReminder;
    private float nextRear;
    // Space: a tap toggles the canter (free), a hold gallops (on its stamina).
    private float spacePressedAt = -10f;
    private bool cruising;
    private const float TapSeconds = 0.22f;
    private float nextKick;
    private PlayerCombat combat;

    /// <summary>The frame the player last left the saddle: the same press of X mustn't whistle too (HorseWhistle).</summary>
    public int LeftSaddleFrame { get; private set; } = -10;

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
        combat = GetComponent<PlayerCombat>();
    }

    /// <summary>How much a blow from the saddle staggers: at a gallop, a charge knocks the struck off their feet (three
    /// times the poise). 1 on foot or slower.</summary>
    public static float ChargePoise(GameObject player)
    {
        return player != null && player.TryGetComponent(out PlayerRiding riding) && riding.Riding != null && riding.Riding.Motion.IsGalloping ? 3f : 1f;
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
        feetFree = false;
        stateMachine.SetAboard(true);
        movement.SetSpeedScale(this, 0f);
    }

    /// <summary>The horse has given in: the player keeps the rope but walks up to it to get on.</summary>
    public void SlackRope()
    {
        feetFree = true;
        movement.ClearFocusDirection();
        movement.SetSpeedScale(this, 1f);
    }

    /// <summary>It fights again: feet planted, facing it.</summary>
    public void TautenRope()
    {
        feetFree = false;
        movement.SetSpeedScale(this, 0f);
    }

    public void ReleaseRope()
    {
        if (holding == null)
        {
            return;
        }

        holding = null;
        feetFree = false;
        movement.ClearFocusDirection();
        movement.SetSpeedScale(this, 1f);
        if (Riding == null)
        {
            stateMachine.SetAboard(false);
        }
    }

    // ----- In the saddle -----

    /// <summary>Whether the player can get on this horse now: near enough (HorseSettings › Mount Reach), it grown, out of
    /// its stall and nobody on it. <paramref name="why"/> says why not, for the player.</summary>
    public bool CanMount(Horse horse, out string why)
    {
        why = null;
        if (horse == null || !horse.IsAlive || horse.Rider != null || Riding != null)
        {
            return false;
        }

        if (horse.IsStabled)
        {
            why = "Lead it out of the stable first.";
            return false;
        }

        if (!horse.IsGrown)
        {
            why = "It is too young to ride.";
            return false;
        }

        string holder = CoopHorses.HeldByFriend(horse);
        if (holder != null)
        {
            why = $"{holder} has {horse.HorseName} in hand.";
            return false;
        }

        Vector3 to = transform.position - horse.transform.position;
        to.y = 0f;
        if (to.magnitude > HorseSettings.Current.MountReach)
        {
            why = "Walk up beside it to get on.";
            return false;
        }

        return true;
    }

    public void Mount(Horse horse) => Mount(horse, animate: true);

    /// <summary>Gets on: animated, the horse turns its side to the player, they step to it and climb on along the pack's
    /// own path (only from near enough, <see cref="CanMount"/>); not animated (the tamed copy taking the wild horse's
    /// place), straight into the saddle.</summary>
    public void Mount(Horse horse, bool animate)
    {
        if (animate && !CanMount(horse, out string why))
        {
            if (!string.IsNullOrEmpty(why))
            {
                GameMessages.Post(why, MessageKind.Warning);
            }

            return;
        }

        if (horse == null || !horse.IsAlive || horse.Rider != null || !CoopHorses.TakeInHand(horse))
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
        horse.Motion.BeginRidden();
        if (animate && body.HasLayer)
        {
            moving = StartCoroutine(GetOn(horse));
        }
        else
        {
            movement.SitAt(horse.Seat, horse.SeatOffset);
        }

        lastSeat = horse.transform.position;
        ridden = 0f;
        HorseHud.Ensure();
        if (!taught && horse.IsTamed)
        {
            taught = true;
            GameMessages.Post($"Steer with the movement keys. Tap {GameInput.KeyLabel(GameInput.Dodge)} to canter, hold it to gallop on its breath; {GameInput.KeyLabel(GameInput.Sneak)} walks it, {GameInput.KeyLabel(GameInput.Whistle)} gets you down.");
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

    private IEnumerator GetOn(Horse horse)
    {
        HorseSettings settings = HorseSettings.Current;
        Vector3 to = transform.position - horse.transform.position;
        to.y = 0f;
        bool right = Vector3.Dot(to, horse.transform.right) > 0f;
        // It turns its side to the rider: whichever side they are on.
        Vector3 side = to.sqrMagnitude > 0.01f ? to.normalized : right ? horse.transform.right : -horse.transform.right;
        Vector3 forward = Quaternion.Euler(0f, -90f, 0f) * (right ? side : -side);
        Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
        float started = Time.time;
        while (horse != null && !horse.Motion.TurnToward(facing) && Time.time - started < 1.6f)
        {
            horse.Motion.Ride(Vector3.zero, 0);
            yield return null;
        }

        // A step to where the pack's clip starts: beside its shoulder, facing it.
        Vector3 start = PathPoint(horse, 0f, right, out _);
        started = Time.time;
        while (horse != null && Time.time - started < 0.9f)
        {
            Vector3 step = start - transform.position;
            step.y = 0f;
            if (step.magnitude < 0.06f)
            {
                break;
            }

            movement.Lunge(step.normalized * Mathf.Min(1.8f, step.magnitude / Mathf.Max(Time.deltaTime, 0.001f)), Time.deltaTime * 1.5f);
            horse.Motion.Ride(Vector3.zero, 0);
            yield return null;
        }

        if (horse == null)
        {
            moving = null;
            yield break;
        }

        // Up into the saddle along the clip's own path, so the body rises only when the clip climbs.
        float ground = transform.position.y;
        body.PlayMount(true, right, settings.MountSeconds);
        started = Time.time;
        while (horse != null && Time.time - started < settings.MountSeconds)
        {
            Follow(horse, (Time.time - started) / settings.MountSeconds, right, ground);
            horse.Motion.Ride(Vector3.zero, 0);
            yield return null;
        }

        if (horse != null)
        {
            movement.SitAt(horse.Seat, horse.SeatOffset);
        }

        moving = null;
    }

    // Where the body is a share of the way through getting on (0 beside the horse, 1 in the saddle), from one side: the
    // clip's path from the saddle, in the rider's own size, along the horse's facing.
    private Vector3 PathPoint(Horse horse, float t, bool right, out float turn)
    {
        Vector4 point = HorseSettings.Current.MountPoint(t, right);
        turn = point.w;
        Vector3 seated = horse.Seat.TransformPoint(horse.SeatOffset);
        Quaternion facing = Quaternion.Euler(0f, horse.transform.eulerAngles.y, 0f);
        return seated + facing * ((Vector3)point * body.HumanScale);
    }

    // The body on the path; its start is put on the ground it stood on, whatever the horse's height, easing out by the saddle.
    private void Follow(Horse horse, float t, bool right, float ground)
    {
        Vector3 at = PathPoint(horse, t, right, out float turn);
        Vector3 start = PathPoint(horse, 0f, right, out _);
        at.y += (ground - start.y) * (1f - Mathf.SmoothStep(0f, 1f, t));
        movement.SitAt(horse.Seat, horse.Seat.InverseTransformPoint(at), turn);
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
        // Getting down: off the side with ground, the pack's clip along the same path backwards.
        if (!thrown && horse.IsAlive && body.HasLayer)
        {
            foreach (bool right in new[] { false, true })
            {
                Vector3 foot = PathPoint(horse, 0f, right, out _);
                if (NavMesh.SamplePosition(foot, out NavMeshHit ground, 1.2f, NavMesh.AllAreas))
                {
                    stateMachine.CanFightAboard = false;
                    moving = StartCoroutine(GetOff(horse, right, ground.position));
                    return;
                }
            }
        }

        Vector3 side = horse.transform.right * (thrown ? 1.8f : 1.2f);
        if (!NavMesh.SamplePosition(horse.transform.position + side, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)
            && !NavMesh.SamplePosition(horse.transform.position - side, out hit, 2.5f, NavMesh.AllAreas))
        {
            hit.position = horse.transform.position;
        }

        GetDown(horse, hit.position);
    }

    private IEnumerator GetOff(Horse horse, bool right, Vector3 ground)
    {
        HorseSettings settings = HorseSettings.Current;
        body.PlayMount(false, right, settings.MountSeconds);
        float started = Time.time;
        while (horse != null && Time.time - started < settings.MountSeconds)
        {
            Follow(horse, 1f - (Time.time - started) / settings.MountSeconds, right, ground.y);
            horse.Motion.Ride(Vector3.zero, 0);
            yield return null;
        }

        moving = null;
        if (horse != null)
        {
            GetDown(horse, ground);
        }
    }

    private void GetDown(Horse horse, Vector3 at)
    {
        if (Riding != horse)
        {
            return;
        }

        Riding = null;
        horse.Rider = null;
        LeftSaddleFrame = Time.frameCount;
        if (PlayerCameraController.Current != null)
        {
            PlayerCameraController.Current.SetCloseness(this, 1f);
        }

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

        // E in the saddle doesn't get the player down (the whistle key does): a reminder, now and then.
        if (Riding != null && !IsMounting && Time.time >= nextReminder)
        {
            nextReminder = Time.time + 6f;
            GameMessages.Post($"{GameInput.KeyLabel(GameInput.Whistle)} gets you down.", MessageKind.Info);
        }
    }

    // The whistle key, in the saddle: getting down (from a horse being broken in too, which then bolts).
    private void TryGetDown()
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
        if (holding != null && !feetFree)
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
        // The view draws back a little at a gallop, for the ground ahead.
        if (PlayerCameraController.Current != null)
        {
            PlayerCameraController.Current.SetCloseness(this, Riding.Motion.IsGalloping ? 1.15f : 1f);
        }

        // From the saddle with a weapon only: bare fists can't reach anything from up there.
        bool armed = combat != null && combat.Weapon != null && combat.Weapon.Stance != WeaponStance.Unarmed;
        stateMachine.CanFightAboard = armed && Riding.IsTamed && !Riding.Motion.IsSwimming && holding == null && !IsMounting;
        if (!armed && Riding.IsTamed && GameInput.Attack != null && GameInput.Attack.WasPressedThisFrame() && Time.time >= nextReminder
            && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen))
        {
            nextReminder = Time.time + 6f;
            GameMessages.Post("Take a weapon in hand to fight from the saddle.", MessageKind.Info);
        }
        if (IsMounting)
        {
            return;
        }

        bool free = !GamePause.IsPaused && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen) && !DialogueBox.IsOpen;
        if (free && GameInput.Whistle != null && GameInput.Whistle.WasPressedThisFrame())
        {
            TryGetDown();
            if (Riding == null || IsMounting)
            {
                return;
            }
        }

        Vector3 steer = free ? movement.MovementInput : Vector3.zero;
        // Space while standing still: it rears up on its hind legs (a horse of the player's own).
        if (free && Riding.IsTamed && GameInput.Dodge != null && GameInput.Dodge.WasPressedThisFrame() && steer.sqrMagnitude < 0.01f
            && Riding.Motion.Velocity.magnitude < 0.6f && !Riding.Motion.IsSwimming && Time.time >= nextRear)
        {
            nextRear = Time.time + 2.5f;
            Riding.Motion.PlayRear();
        }

        bool moving = steer.sqrMagnitude > 0.01f;
        // R: the horse rears and strikes out ahead with its forelegs (bond 40, or a bold horse).
        if (free && Riding.IsTamed && GameInput.HorseKick != null && GameInput.HorseKick.WasPressedThisFrame() && Time.time >= nextKick)
        {
            if (Riding.Motion.KickOnCommand())
            {
                nextKick = Time.time + 5f;
            }
            else if (Riding.Stats.temperament != HorseTemperament.Bold && Riding.CurrentBond() < 40f)
            {
                nextKick = Time.time + 2f;
                GameMessages.Post($"{Riding.HorseName} won't strike out for you yet (a bond of 40, or a bold horse).", MessageKind.Info);
            }
        }

        if (free && GameInput.Dodge != null)
        {
            if (GameInput.Dodge.WasPressedThisFrame())
            {
                spacePressedAt = Time.time;
            }

            if (GameInput.Dodge.WasReleasedThisFrame() && Time.time - spacePressedAt <= TapSeconds && moving)
            {
                cruising = !cruising;
            }
        }

        // Stopping ends the canter.
        if (!moving && Riding.Motion.Velocity.magnitude < 0.3f)
        {
            cruising = false;
        }

        bool burst = free && GameInput.Dodge != null && GameInput.Dodge.IsPressed() && Time.time - spacePressedAt > TapSeconds;
        bool walk = free && GameInput.Sneak != null && GameInput.Sneak.IsPressed();
        int gait = walk ? -1 : burst ? 1 : cruising ? 2 : 0;
        Riding.Motion.Ride(steer, gait);
    }
}
