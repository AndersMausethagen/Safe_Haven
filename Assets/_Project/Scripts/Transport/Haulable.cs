using UnityEngine;

/// <summary>
/// A transport moved by its handles: a cart dragged behind the player, a wheelbarrow pushed ahead. The player is a
/// CharacterController, which no joint can hold, so the player's hands are a spring: a force at the handle towards where
/// the hands are, capped by what a person can pull (TransportDefinition.PullForce). The rest is physics. The cart lags
/// behind its mass, swings wide on a turn, rolls on sphere colliders over whatever is there and stops at what stops it.
/// <para>
/// The wheels' colliders are frictionless; the script gives them their grip: none along the wheel, as much as a tyre
/// has across it, so it tracks instead of sliding. How far the handle is from the hands is the Strain, which slows the
/// player: a cart that is stuck or too heavy for the hill holds them back, and if it stays stuck they let go.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Transport))]
public sealed class Haulable : MonoBehaviour, ITransportDriver
{
    private const float Stiffness = 90f;
    private const float Damping = 16f;
    private const float Slack = 0.2f;
    private const float Stretch = 1.4f;
    private const float RollingDrag = 0.5f;
    private const float ParkedDrag = 6f;
    private const float WheelGrip = 9f;
    private const float RollSpring = 60f;
    private const float RollDamping = 10f;
    private const float YawSpring = 26f;
    private const float YawDamping = 8f;
    private const float MaxYawAcceleration = 30f;
    private const float RollingSpeed = 0.4f;

    [Tooltip("Where the player holds it.")]
    [SerializeField] private Transform handle;
    [Tooltip("Wheel pivots, each with a frictionless SphereCollider. Their first child spins.")]
    [SerializeField] private Transform[] wheels = System.Array.Empty<Transform>();
    [SerializeField, Min(0.05f)] private float wheelRadius = 0.4f;
    [Tooltip("Pushed ahead of the player (a wheelbarrow), not dragged behind.")]
    [SerializeField] private bool pushed;
    [Tooltip("Keeps it from tipping; low for a cart, ahead of the legs for a barrow.")]
    [SerializeField] private Vector3 centreOfMass = new Vector3(0f, 0.3f, 0f);
    [Tooltip("How high the hands hold the handle, above the player's feet.")]
    [SerializeField, Min(0f)] private float handleHeight = 0.8f;
    [Tooltip("How far from the player's centre the hands are.")]
    [SerializeField, Min(0f)] private float holdDistance = 0.55f;

    private Transport transport;
    private IHauler hauler;
    private Collider[] colliders;
    private float stuckFor;
    private float nextRumble;

    public bool InUse => hauler != null;
    public bool Pushed => pushed;

    /// <summary>How far the handle is from the hands, 0 (in them) to 1 (at the end of the player's reach).</summary>
    public float Strain { get; private set; }

    /// <summary>The share of normal speed the strain leaves.</summary>
    public float StrainScale => Mathf.Lerp(1f, TransportSettings.Current.StrainedSpeed, Strain);

    public bool IsHeldBy(GameObject user) => hauler != null && hauler.gameObject == user;

    /// <summary>Who pulls it now (the player, or a horse it is hitched to), or null.</summary>
    public IHauler Hauler => hauler;

    /// <summary>The nearest cart (not a barrow) nobody holds, within <paramref name="reach"/> metres, or null.</summary>
    public static Haulable NearestFree(Vector3 point, float reach)
    {
        Haulable best = null;
        float bestDistance = reach * reach;
        foreach (Transport transport in Transport.All)
        {
            if (transport != null && transport.Driver is Haulable cart && !cart.pushed && cart.hauler == null)
            {
                float distance = (transport.transform.position - point).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    best = cart;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private void Awake()
    {
        transport = GetComponent<Transport>();
        colliders = GetComponentsInChildren<Collider>();
        transport.Body.centerOfMass = centreOfMass;
        transport.Body.linearDamping = 0.05f;
        transport.Body.angularDamping = 1.5f;
        transport.Body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void AddActions(MenuPage page, GameObject user)
    {
        var player = user != null ? user.GetComponent<PlayerTransport>() : null;
        if (hauler is HorseHitch hitch)
        {
            page.Add(new MenuAction($"Hitched to {hitch.Horse.HorseName}", "Unhitch it from the horse's menu", false, null));
        }
        else if (player != null)
        {
            page.Add(new MenuAction(pushed ? "Lift the handles" : "Take the shafts", transport.LoadText(), player.Using == null, () => Take(player), closesMenu: true));
        }
    }

    /// <summary>The player takes it. Their own body stops colliding with it, or they would push against what they pull.</summary>
    public void Take(PlayerTransport player) => Take((IHauler)player);

    /// <summary>Something takes the handles: the player, or a horse it is hitched to.</summary>
    public void Take(IHauler by)
    {
        // Taken from someone else (the player takes a resident's cart): they let go first, so their colliders and their hold come back.
        if (hauler != null && hauler != by)
        {
            LetGo();
        }

        hauler = by;
        stuckFor = 0f;
        Strain = 0f;
        IgnorePlayer(true);
        transport.Wake();
        by.Began(transport);
    }

    public void LetGo()
    {
        if (hauler == null)
        {
            return;
        }

        IHauler was = hauler;
        hauler = null;
        Strain = 0f;
        IgnorePlayer(false, was);
        was.Ended();
    }

    private void IgnorePlayer(bool ignore, IHauler by = null)
    {
        foreach (Collider own in (by ?? hauler).HaulerColliders)
        {
            if (own == null)
            {
                continue;
            }

            foreach (Collider part in colliders)
            {
                Physics.IgnoreCollision(own, part, ignore);
            }
        }
    }

    private void FixedUpdate()
    {
        Rigidbody body = transport.Body;
        if (body.isKinematic)
        {
            return;
        }

        Grip(body);
        if (hauler == null)
        {
            return;
        }

        Pull(body);
        Steady(body);
        Rumble(body);
    }

    private void Grip(Rigidbody body)
    {
        float drag = hauler != null ? RollingDrag : ParkedDrag;
        float share = body.mass / Mathf.Max(1, wheels.Length);
        float grip = SideGrip();
        foreach (Transform wheel in wheels)
        {
            if (!Physics.Raycast(wheel.position, Vector3.down, out RaycastHit ground, wheelRadius + 0.15f, Transport.GroundMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            Vector3 velocity = body.GetPointVelocity(wheel.position);
            Vector3 side = Vector3.ProjectOnPlane(transform.right, ground.normal).normalized;
            Vector3 rolling = Vector3.Cross(side, ground.normal);
            Vector3 wanted = -side * (Vector3.Dot(velocity, side) * grip / Time.fixedDeltaTime) - rolling * (Vector3.Dot(velocity, rolling) * drag);
            body.AddForceAtPosition(Vector3.ClampMagnitude(wanted, WheelGrip) * share, wheel.position);
        }
    }

    // Pushing a wheel ahead of you is reversing a trailer: at walking pace a gripping wheel swings it round faster than
    // any steering can catch. In the hands the handles set its heading (Steady), so the wheel gives way.
    private float SideGrip() => pushed && hauler != null ? 0f : 0.5f;

    private void Pull(Rigidbody body)
    {
        Vector3 at = handle.position;
        Vector3 reach = HoldPoint(at) - at;
        Vector3 spring = reach * Stiffness + (hauler.Velocity - body.GetPointVelocity(at)) * Damping;
        Vector3 pull = Vector3.ClampMagnitude(spring * body.mass, transport.Definition.PullForce * hauler.PullScale);
        if (pushed)
        {
            // A push at the handles is behind the centre of mass, like balancing a broom on its end: any slant grows. The
            // arms lift at the handles; the shove goes through the load, and Steady keeps the heading.
            Vector3 lift = Vector3.up * pull.y;
            body.AddForceAtPosition(lift, at);
            body.AddForce(pull - lift);
        }
        else
        {
            body.AddForceAtPosition(pull, at);
        }

        Strain = Mathf.Clamp01((reach.magnitude - Slack) / Stretch);
        stuckFor = Strain >= 1f ? stuckFor + Time.fixedDeltaTime : 0f;
        if (stuckFor >= TransportSettings.Current.StuckSeconds)
        {
            if (hauler is PlayerTransport)
            {
                GameMessages.Post($"The {transport.Definition.DisplayName} won't move. You let go.", MessageKind.Warning);
            }

            LetGo();
        }
    }

    // A cart hangs from the hands on whichever side it is; a barrow is held out ahead, in the way the player faces.
    private Vector3 HoldPoint(Vector3 handleAt)
    {
        Vector3 player = hauler.transform.position;
        Vector3 toward = pushed ? hauler.transform.forward : handleAt - player;
        toward.y = 0f;
        toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : -hauler.transform.forward;
        return player + toward * (holdDistance + hauler.ExtraReach) + Vector3.up * handleHeight;
    }

    private void Steady(Rigidbody body)
    {
        if (hauler == null)
        {
            return;
        }

        // Hands keep it from rolling over; one wheel can't.
        float lean = Vector3.Dot(Vector3.Cross(transform.up, Vector3.up), transform.forward);
        float rollSpeed = Vector3.Dot(body.angularVelocity, transform.forward);
        body.AddTorque(transform.forward * (lean * RollSpring - rollSpeed * RollDamping), ForceMode.Acceleration);
        if (!pushed)
        {
            return;
        }

        // A barrow goes where it is pointed.
        Vector3 ahead = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        float turn = Vector3.SignedAngle(ahead, hauler.transform.forward, Vector3.up) * Mathf.Deg2Rad;
        float yaw = Mathf.Clamp(turn * YawSpring - body.angularVelocity.y * YawDamping, -MaxYawAcceleration, MaxYawAcceleration);
        body.AddTorque(Vector3.up * yaw, ForceMode.Acceleration);
    }

    private void Rumble(Rigidbody body)
    {
        TransportSettings settings = TransportSettings.Current;
        // The player's own noise: a resident's cart rumbling at home is no noise of theirs.
        if (Time.time < nextRumble || body.linearVelocity.sqrMagnitude < RollingSpeed * RollingSpeed || Player.Current == null || Player.Current.Noise == null
            || !(hauler is PlayerTransport))
        {
            return;
        }

        nextRumble = Time.time + settings.RumbleSeconds;
        float load = Mathf.Clamp01(transport.Weight / transport.Definition.Capacity);
        Player.Current.Noise.Make(transform.position, settings.RumbleRadius + settings.RumbleWhenFull * load);
    }

    private void Update()
    {
        Rigidbody body = transport.Body;
        if (body.isKinematic)
        {
            return;
        }

        float turned = Vector3.Dot(body.linearVelocity, transform.forward) / wheelRadius * Mathf.Rad2Deg * Time.deltaTime;
        foreach (Transform wheel in wheels)
        {
            if (wheel.childCount > 0)
            {
                wheel.GetChild(0).Rotate(Vector3.right, turned, Space.Self);
            }
        }
    }
}
