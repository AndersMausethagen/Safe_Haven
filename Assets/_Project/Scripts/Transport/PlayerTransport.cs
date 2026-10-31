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
public sealed class PlayerTransport : MonoBehaviour
{
    private const float BoardReach = 3.5f;
    private const float ArrivalSeconds = 5f;
    private const float SettleSeconds = 0.3f;

    private PlayerStateMachine stateMachine;
    private Damageable health;

    public static PlayerTransport Current { get; private set; }

    /// <summary>What the player is moving now, or null.</summary>
    public Transport Using { get; private set; }

    public PlayerMovement Movement { get; private set; }
    public CharacterController Controller { get; private set; }
    /// <summary>The player's speed over the ground, for the spring at the handle.</summary>
    public Vector3 Velocity => Movement.HorizontalVelocity;

    private void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        Controller = GetComponent<CharacterController>();
        stateMachine = GetComponent<PlayerStateMachine>();
        health = GetComponent<Damageable>();
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
        if (health != null)
        {
            health.Died += OnDied;
        }
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
        if (health != null)
        {
            health.Died -= OnDied;
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

            Movement.SetSpeedScale(this, Using.SpeedScale * haulable.StrainScale);
        }
    }

    /// <summary>Called by a driver when the player takes it.</summary>
    public void Began(Transport transport)
    {
        Using = transport;
        TransportHud.Create();
    }

    /// <summary>Called by a driver when the player no longer has it.</summary>
    public void Ended()
    {
        Using = null;
        Movement.SetSpeedScale(this, 1f);
        if (stateMachine.IsAboard)
        {
            stateMachine.SetAboard(false);
        }
    }

    /// <summary>Sits the player down on something that moves.</summary>
    public void SitAt(Transform seat, Vector3 offset)
    {
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

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        if (Using != null)
        {
            Using.Driver.LetGo();
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
            report.TransportLine = RaidEnding(nearest.Definition.DisplayName, nearest.Cargo.CountItems(), home);
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
    }
}
