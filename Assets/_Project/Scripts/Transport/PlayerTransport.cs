using System.Collections;
using UnityEngine;

/// <summary>
/// The player's side of transport: what they are moving, how much it slows them, and everything that makes them let go.
/// Attacking, guarding, dodging, casting, swimming or dying drops the handles at once, so a fight never needs a key to
/// get free of the cart; it stays where it fell, cargo and all, to come back to (GDD §8.5). A cart in the hands goes through
/// the portal with the player. It comes home if it is near them when they extract, and it is lost if they fail.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement), typeof(PlayerStateMachine), typeof(CharacterController))]
public sealed class PlayerTransport : MonoBehaviour, IHauler, IPlayerActivity
{
    private const float BoardReach = 3.5f;
    private const float ArrivalSeconds = 5f;
    private const float SettleSeconds = 0.3f;
    private const float ClimbSeconds = 1f;

    private PlayerStateMachine stateMachine;
    private Damageable health;
    private PlayerStamina stamina;
    private float climb;

    public static PlayerTransport Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>What the player is moving now, or null.</summary>
    public Transport Using { get; private set; }

    public PlayerMovement Movement { get; private set; }
    public CharacterController Controller { get; private set; }
    /// <summary>True with no stamina left for the work.</summary>
    public bool IsTired => stamina != null && stamina.IsEmpty;

    /// <summary>The share of normal effort a tired player still manages.</summary>
    public float EffortScale => IsTired ? TransportSettings.Current.TiredSpeed : 1f;

    /// <summary>The player's speed over the ground, for the spring at the handle.</summary>
    public Vector3 Velocity => Movement.HorizontalVelocity;

    float IHauler.PullScale => 1f;
    float IHauler.ExtraReach => 0f;
    System.Collections.Generic.IEnumerable<Collider> IHauler.HaulerColliders { get { yield return Controller; } }

    private void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        Controller = GetComponent<CharacterController>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
        stamina = GetComponent<PlayerStamina>();
    }

    private void OnEnable()
    {
        Current = this;
        stateMachine.AttackRequested += LetGoToFight;
        stateMachine.DodgeRequested += LetGoToFight;
        stateMachine.ShieldBashRequested += LetGoToFight;
        stateMachine.SkillRequested += OnSkill;
        stateMachine.GuardRequested += OnGuard;
        stateMachine.AboardInteraction += OnAboardInteraction;
        stateMachine.SwimmingInteraction += OnSwimmingInteraction;
        PlayerInterrupts.For(gameObject).Add(this);
    }

    private void OnDisable()
    {
        stateMachine.AttackRequested -= LetGoToFight;
        stateMachine.DodgeRequested -= LetGoToFight;
        stateMachine.ShieldBashRequested -= LetGoToFight;
        stateMachine.SkillRequested -= OnSkill;
        stateMachine.GuardRequested -= OnGuard;
        stateMachine.AboardInteraction -= OnAboardInteraction;
        stateMachine.SwimmingInteraction -= OnSwimmingInteraction;
        if (TryGetComponent(out PlayerInterrupts interrupts))
        {
            interrupts.Remove(this);
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    private IEnumerator Start()
    {
        if (!GameSession.TakeCarriedTransport(out GameSession.CarriedTransport carried))
        {
            yield break;
        }

        // A scene's setup steps can outlast its Start: a generated raid lays out its ground and moves the player to its
        // start behind the loading screen, which would leave a cart set down here behind.
        bool ready = false;
        SceneTransition.WhenReady(() => ready = true);
        while (!ready)
        {
            yield return null;
        }

        // Set down too early it falls through a scene that is still waking. The player standing proves the ground is there.
        for (float waited = 0f; !Movement.IsGrounded && waited < ArrivalSeconds; waited += Time.unscaledDeltaTime)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(SettleSeconds);
        Arrive(carried);
    }

    private void Update()
    {
        if (Using == null)
        {
            return;
        }

        if (Using.Driver is Haulable haulable)
        {
            if (stateMachine.IsSwimming)
            {
                haulable.LetGo();
                return;
            }

            Movement.SetSpeedScale(this, Using.SpeedScale * haulable.StrainScale * EffortScale);
            PayForHaul();
        }
    }

    private void PayForHaul()
    {
        Vector3 velocity = Using.Body.linearVelocity;
        if (stamina == null || velocity.sqrMagnitude < 0.04f)
        {
            return;
        }

        TransportDefinition definition = Using.Definition;
        float over = Mathf.InverseLerp(definition.Capacity, definition.OverloadLimit, Using.Weight);
        float load = Using.Weight / definition.Capacity;
        // Averaged over a moment, or every bump in the ground is a little hill.
        climb = Mathf.Lerp(climb, velocity.y / velocity.magnitude, 1f - Mathf.Exp(-Time.deltaTime / ClimbSeconds));
        stamina.Drain(TransportSettings.Current.HaulEffort(over, load, Mathf.Max(0f, climb)) * Time.deltaTime);
    }

    /// <summary>Rowing costs what swimming does, near enough; Rowboat asks for it each frame it rows.</summary>
    public void PayForRowing(float deltaTime)
    {
        if (stamina != null)
        {
            stamina.Drain(TransportSettings.Current.RowingEffort * deltaTime);
        }
    }

    /// <summary>Called by a driver when the player takes it.</summary>
    public void Began(Transport transport)
    {
        Using = transport;
        climb = 0f;
        TransportHud.Create();
    }

    /// <summary>Called by a driver when the player no longer has it.</summary>
    public void Ended()
    {
        Using = null;
        Movement.SetSpeedScale(this, 1f);
        // Only the aboard this set (a boat's seat): one rule for standing up, whoever sat the player down undoes it. A cart let
        // go under a rider switched the saddle's aboard off, and the rider could act as if on foot.
        if (seatedAboard && stateMachine.IsAboard)
        {
            stateMachine.SetAboard(false);
        }

        seatedAboard = false;
    }

    private bool seatedAboard;

    /// <summary>Sits the player down on something that moves.</summary>
    public void SitAt(Transform seat, Vector3 offset)
    {
        seatedAboard = true;
        stateMachine.SetAboard(true);
        Movement.SitAt(seat, offset);
    }

    public void StandAt(Vector3 spot) => Movement.StandAt(spot);

    private void LetGoToFight()
    {
        if (Using != null && Using.Driver is Haulable haulable)
        {
            haulable.LetGo();
        }
    }

    private void OnSkill(int slot) => LetGoToFight();

    private void OnGuard(bool raised)
    {
        if (raised)
        {
            LetGoToFight();
        }
    }

    /// <summary>Death alone ends it (PlayerInterrupts tells every activity of a death).</summary>
    public PlayerInterrupt EndsOn => PlayerInterrupt.None;

    public void Interrupt(PlayerInterrupt reason)
    {
        if (Using != null)
        {
            Using.Driver.LetGo();
        }

        // A boat's LetGo leaves the rower on the seat (getting out is Disembark's, at a shore): the seat would hold the body
        // and undo the move to the portal when they wake (HomeRevival).
        bool riding = TryGetComponent(out PlayerRiding rider) && rider.IsRiding;
        if (Movement.IsSeated && !riding)
        {
            Movement.StandAt(transform.position);
        }
    }

    private void OnAboardInteraction()
    {
        if (Using != null && Using.Driver is Rowboat boat)
        {
            boat.TryDisembark();
        }
    }

    // Interacting is off while swimming, but a boat that drifted out must never be lost for it.
    private void OnSwimmingInteraction()
    {
        foreach (Transport transport in Transport.All)
        {
            if (transport.Driver is Rowboat boat && !boat.InUse && (transport.transform.position - transform.position).sqrMagnitude < BoardReach * BoardReach)
            {
                boat.Board(this);
                return;
            }
        }
    }

    // ----- Through the portal -----

    /// <summary>A cart in the hands comes along through the portal, and leaves home for now.</summary>
    public void LeaveForRaid()
    {
        if (Using == null || !(Using.Driver is Haulable))
        {
            return;
        }

        Transport transport = Using;
        // A visitor's cart stays in the host's Haven, as their horse does (HorseTravel.LeaveForRaid): taken through, its removal
        // never reached the host (the raid is under way by then), and extracting with it set down a second cart with the same load.
        if (HavenVisit.IsVisiting)
        {
            transport.Driver.LetGo();
            return;
        }

        GameSession.CarryTransport(new GameSession.CarriedTransport(transport.Definition, transport.CaptureState()));
        transport.Driver.LetGo();
        transport.Remove();
    }

    /// <summary>The cart that would come home if the raid ended now: the nearest to the player. Null with none.</summary>
    public Transport RaidCart(out float distance)
    {
        Transport nearest = null;
        distance = float.PositiveInfinity;
        foreach (Transport transport in Transport.All)
        {
            float away = Vector3.Distance(transport.transform.position, transform.position);
            if (transport.Driver is Haulable && away < distance)
            {
                distance = away;
                nearest = transport;
            }
        }

        return nearest;
    }

    /// <summary>Lighting the way home starts the clock on a cart left somewhere: say so while there is still time.</summary>
    public void WarnIfLeavingCart()
    {
        Transport cart = RaidCart(out float distance);
        if (cart != null && distance > TransportSettings.Current.ExtractionRange)
        {
            GameMessages.Post($"The {cart.Definition.DisplayName} is {distance:0} m away. Bring it to the circle or it stays behind.", MessageKind.Warning);
        }
    }

    /// <summary>The raid is over. On extraction the cart nearest the player comes home; the rest of the forest keeps.</summary>
    public void LeaveRaid(RaidReport report, bool extracted)
    {
        Transport nearest = RaidCart(out float distance);
        if (nearest == null)
        {
            return;
        }

        bool home = extracted && distance <= TransportSettings.Current.ExtractionRange;
        if (home)
        {
            GameSession.CarryTransport(new GameSession.CarriedTransport(nearest.Definition, nearest.CaptureState()));
        }

        if (report != null)
        {
            report.TransportHome = home;
            int taken = nearest.TryGetComponent(out CartScavenging scavenging) ? scavenging.Taken : 0;
            report.TransportLine = RaidEnding(nearest.Definition.DisplayName, nearest.Cargo.CountItems(), home)
                + (taken > 0 ? $" Creatures took {taken} from it." : "");
        }
    }

    /// <summary>"The Handcart came home with 30 items." for the report on a raid.</summary>
    public static string RaidEnding(string name, int items, bool home)
    {
        string load = $"{items} item{(items == 1 ? "" : "s")}";
        if (home)
        {
            return items > 0 ? $"The {name} came home with {load}." : $"The {name} came home.";
        }

        return items > 0 ? $"The {name} stayed behind, and {load} with it." : $"The {name} stayed behind.";
    }

    /// <summary>What the portal's briefing says about the cart in the hands. Empty without one.</summary>
    public string RaidWarning()
    {
        if (Using == null || !(Using.Driver is Haulable))
        {
            return "";
        }

        int items = Using.Cargo.CountItems();
        string load = items > 0 ? $", with {items} item{(items == 1 ? "" : "s")} in it," : "";
        return $"The {Using.Definition.DisplayName}{load} comes with you. Fail, or leave it more than {TransportSettings.Current.ExtractionRange:0} m away when you extract, and it is lost.";
    }

    private void Arrive(GameSession.CarriedTransport carried)
    {
        if (!TransportDeployment.FindGroundAround(transform, out Vector3 at))
        {
            at = transform.position + Vector3.up;
        }

        SaveableEntity spawned = SaveableEntity.Spawn(carried.Definition.Prefab, at, transform.rotation);
        var transport = spawned.GetComponent<Transport>();
        transport.RestoreState(carried.State);
        if (transport.Driver is Haulable haulable)
        {
            haulable.Take(this);
        }

        // Home again: the save on coming home was written before the cart and its load were set down. A frame on, once its
        // Start has run: saved in the frame it was spawned, its components were left out (SaveableEntity before Start) and
        // the cart loaded back empty.
        if (RaidDirector.Current == null)
        {
            StartCoroutine(SaveOnceSetDown());
        }
    }

    private static System.Collections.IEnumerator SaveOnceSetDown()
    {
        yield return null;
        SaveGameService.SaveAfterArrival();
    }
}
