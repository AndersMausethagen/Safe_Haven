using UnityEngine;

/// <summary>
/// A small boat. It floats by real buoyancy: each float point is pushed up by how deep it is under the water, so a loaded
/// boat sits lower, and rocks on what bumps it. The player sits at the oars: forward and back drive it along its length, left
/// and right turn the bow, wherever the camera looks. Each stroke pushes, so a heavy boat is slow to get going and slow to stop.
/// It only rows while it is in the water. Getting out needs ground in reach; a swimmer can climb back in (PlayerTransport).
/// <para>
/// A river carries it: drag acts against the water, not the ground, so it drifts with the current and rowing upstream is
/// slower than down. Only with someone aboard: left alone it is tied up, and stays where it was left.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Transport))]
public sealed class Rowboat : MonoBehaviour, ITransportDriver
{
    private const float RowerMass = 80f;
    private const float WaterReach = 1.5f;
    private const float TurnSpeed = 0.9f;
    private const float TurnResponse = 3f;
    private const float MaxTurnAcceleration = 4f;
    private const float BackingShare = 0.6f;
    private const float StrokesPerSecond = 0.9f;
    private const float OarSwing = 28f;

    [Tooltip("Points low on the hull, spread out, that the water pushes up.")]
    [SerializeField] private Transform[] floats = System.Array.Empty<Transform>();
    [Tooltip("Where the rower sits. The player's feet go at this point plus Seat Offset.")]
    [SerializeField] private Transform seat;
    [SerializeField] private Vector3 seatOffset = new Vector3(0f, -0.75f, 0f);
    [Tooltip("Oar pivots, swung with each stroke.")]
    [SerializeField] private Transform[] oars = System.Array.Empty<Transform>();
    [Tooltip("Newtons on each float point for each metre it is under. More floats higher.")]
    [SerializeField, Min(1f)] private float buoyancy = 5000f;
    [Tooltip("Newtons per m/s on each float point, so it settles instead of bobbing forever.")]
    [SerializeField, Min(0f)] private float heaveDamping = 300f;
    [Tooltip("Water's drag along the hull. With Row Force it sets the top speed.")]
    [SerializeField, Min(0f)] private float forwardDrag = 30f;
    [Tooltip("Water's drag across the hull: it is why a boat goes where it points.")]
    [SerializeField, Min(0f)] private float sideDrag = 500f;
    [Tooltip("Newtons of a stroke at its strongest.")]
    [SerializeField, Min(0f)] private float rowForce = 620f;

    private Transport transport;
    private PlayerTransport rower;
    private Quaternion[] oarRest;
    private bool afloat;
    private static bool taught;

    public bool InUse => rower != null;

    /// <summary>Where in the stroke the oars are, -1..1, for the rower's arms. 0 when not rowing.</summary>
    public float Stroke { get; private set; }

    /// <summary>How deep the hull sits at its first float point, in metres. 0 out of the water.</summary>
    public float Draught { get; private set; }

    public bool IsAfloat => afloat;
    public Transform Seat => seat;
    public Vector3 SeatOffset => seatOffset;

    public bool IsHeldBy(GameObject user) => rower != null && rower.gameObject == user;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => taught = false;

    private void Awake()
    {
        transport = GetComponent<Transport>();
        transport.Body.centerOfMass = new Vector3(0f, -0.1f, 0f);
        transport.Body.angularDamping = 1.5f;
        transport.Body.linearDamping = 0.02f;
        transport.Body.interpolation = RigidbodyInterpolation.Interpolate;
        oarRest = new Quaternion[oars.Length];
        for (int i = 0; i < oars.Length; i++)
        {
            oarRest[i] = oars[i].localRotation;
        }
    }

    public void AddActions(MenuPage page, GameObject user)
    {
        var player = user != null ? user.GetComponent<PlayerTransport>() : null;
        if (player != null)
        {
            page.Add(new MenuAction("Sit at the oars", transport.LoadText(), player.Using == null, () => Board(player), closesMenu: true));
        }
    }

    public void Board(PlayerTransport player)
    {
        rower = player;
        transport.ExtraMass = RowerMass;
        transport.Wake();
        player.Began(transport);
        player.SitAt(seat, seatOffset);
        if (!taught)
        {
            taught = true;
            GameMessages.Post($"W and S row, A and D turn. {GameInput.KeyLabel(GameInput.Interact)} gets you out.");
        }
    }

    /// <summary>Gets out onto the nearest ground in reach. Refused, with a word, when there is none.</summary>
    public bool TryDisembark()
    {
        if (rower == null)
        {
            return false;
        }

        if (!FindShore(out Vector3 spot))
        {
            GameMessages.Post("Row closer to the bank to get out.", MessageKind.Warning);
            return false;
        }

        PlayerTransport was = rower;
        LetGo();
        was.StandAt(spot);
        return true;
    }

    public void LetGo()
    {
        if (rower == null)
        {
            return;
        }

        PlayerTransport was = rower;
        rower = null;
        transport.ExtraMass = 0f;
        Stroke = 0f;
        was.Ended();
    }

    private bool FindShore(out Vector3 spot)
    {
        spot = default;
        float best = float.PositiveInfinity;
        float wade = rower.Movement.Settings.SwimDepth * 0.7f;
        Vector3 centre = transform.position;
        for (float radius = 1.75f; radius <= 3.5f; radius += 0.75f)
        {
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 probe = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius + Vector3.up * 4f;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 10f, Transport.GroundMask, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f)
                {
                    continue;
                }

                float deep = WaterSurface.TryFind(ground.point + Vector3.up * WaterReach, WaterReach, out float surface) ? surface - ground.point.y : 0f;
                float distance = (ground.point - centre).sqrMagnitude;
                if (deep <= wade && distance < best)
                {
                    best = distance;
                    spot = ground.point;
                }
            }
        }

        return best < float.PositiveInfinity;
    }

    private void FixedUpdate()
    {
        Rigidbody body = transport.Body;
        if (body.isKinematic)
        {
            return;
        }

        afloat = Float(body) >= floats.Length / 2;
        if (!afloat)
        {
            return;
        }

        Drag(body);
        if (rower != null)
        {
            Row(body);
        }
    }

    private int Float(Rigidbody body)
    {
        int wet = 0;
        for (int i = 0; i < floats.Length; i++)
        {
            Vector3 point = floats[i].position;
            float depth = WaterSurface.TryFind(point + Vector3.up * WaterReach, WaterReach * 2f, out float surface) ? surface - point.y : 0f;
            if (i == 0)
            {
                Draught = Mathf.Max(0f, depth);
            }

            if (depth <= 0f)
            {
                continue;
            }

            wet++;
            float heave = body.GetPointVelocity(point).y;
            body.AddForceAtPosition(Vector3.up * (depth * buoyancy - heave * heaveDamping), point);
        }

        return wet;
    }

    private void Drag(Rigidbody body)
    {
        Vector3 water = rower != null ? WaterSurface.CurrentAt(transform.position + Vector3.up * WaterReach, WaterReach * 2f) : Vector3.zero;
        Vector3 local = transform.InverseTransformDirection(body.linearVelocity - water);
        local.y = 0f;
        float along = -local.z * Mathf.Abs(local.z) * forwardDrag;
        float across = -local.x * Mathf.Abs(local.x) * sideDrag - local.x * sideDrag * 0.2f;
        body.AddForce(transform.TransformDirection(new Vector3(across, 0f, along)));
    }

    private void Row(Rigidbody body)
    {
        Vector2 keys = rower.Movement.MoveKeys;
        if (keys.sqrMagnitude < 0.01f)
        {
            Stroke = Mathf.MoveTowards(Stroke, 0f, Time.fixedDeltaTime * 2f);
            return;
        }

        float yaw = Mathf.Clamp((keys.x * TurnSpeed - body.angularVelocity.y) * TurnResponse, -MaxTurnAcceleration, MaxTurnAcceleration);
        body.AddTorque(Vector3.up * yaw, ForceMode.Acceleration);

        Stroke = Mathf.Sin(Time.time * StrokesPerSecond * Mathf.PI * 2f);
        float power = 0.55f + 0.45f * Stroke;
        // Backing water is weaker than pulling.
        float drive = keys.y >= 0f ? keys.y : keys.y * BackingShare;
        Vector3 ahead = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        body.AddForce(ahead * (rowForce * drive * power * transport.SpeedScale * rower.EffortScale));
        rower.PayForRowing(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        for (int i = 0; i < oars.Length; i++)
        {
            float side = i == 0 ? 1f : -1f;
            oars[i].localRotation = oarRest[i] * Quaternion.Euler(0f, Stroke * OarSwing * side, 0f);
        }
    }
}
