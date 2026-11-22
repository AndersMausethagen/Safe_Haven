using System;
using UnityEngine;

/// <summary>
/// Walks a many-legged creature (the giant black widow, 2026-11-14) with no animation clips: each leg is three segments
/// (femur, tibia, tarsus) reaching from a hip on the body to a foot on the ground. A foot stays planted until the body has
/// moved it too far from where it would rest, then steps there in an arc; the legs go in two alternating groups (a
/// spider's tetrapod gait), so half the legs always hold the body. The body rides at its height over the feet, tilting with
/// the ground. Knees bend up and out, solved each frame (two-bone IK to the ankle, the tarsus hanging to the foot).
/// <para>
/// It reads its monster for the rest: as an attack starts the front legs rear up and the body lunges, while staggered the
/// body sags, and once dead it stops (the corpse takes the body and legs as loose parts, DeathRagdoll). Movement itself is
/// the monster's (NavMeshAgent): this only places the legs and the body's look. The legs are built once in the editor
/// (<see cref="Build"/>), as plain parts under Model, so a corpse and a cut work on them as on any rigid-part creature.
/// </para>
/// <para>
/// Its distances are in metres at scale 1 and grow with the creature (the Black Widow is the giant's body at half size). A slashing
/// blow can cut a leg off (the user, 2026-11-22): the leg falls away, the gait goes on without it, the body sags to the side that
/// lost it and limps, and it moves slower for every leg gone (Sever, Severed).
/// </para>
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "walks the legs the copy moves")]
public sealed class SpiderLegs : MonoBehaviour
{
    [Serializable]
    public sealed class Leg
    {
        [Tooltip("Where the leg leaves the body (moves with Body).")]
        public Transform hip;
        [Tooltip("Femur, tibia, tarsus: each a part whose mesh runs along +Z from 0 to 1, scaled to its length.")]
        public Transform[] segments = new Transform[3];
        [Tooltip("Where its foot rests, from the creature's root (x right, z ahead), on the ground.")]
        public Vector3 rest;
        [Tooltip("0 or 1: the legs of one group step together, never while the other group is stepping.")]
        public int group;
        [Tooltip("A front leg: it rears up in an attack.")]
        public bool front;

        [NonSerialized] public Vector3 foot;
        [NonSerialized] public Vector3 from;
        [NonSerialized] public Vector3 to;
        [NonSerialized] public float stepT = 1f;
        [NonSerialized] public bool planted;
        [NonSerialized] public bool lost;
    }

    [SerializeField] private Transform body;
    [SerializeField] private Leg[] legs = Array.Empty<Leg>();
    [Tooltip("Femur, tibia and tarsus lengths in metres.")]
    [SerializeField] private Vector3 lengths = new Vector3(1.15f, 1.3f, 0.75f);
    [Tooltip("Height of the body's middle over the ground its feet stand on.")]
    [SerializeField, Min(0.1f)] private float bodyHeight = 0.95f;
    [Tooltip("A foot steps once the place it would rest is this far from where it stands.")]
    [SerializeField, Min(0.05f)] private float stepDistance = 0.85f;
    [Tooltip("How high a foot lifts mid-step, and how long a step takes at a walk (faster when the body is faster).")]
    [SerializeField, Min(0f)] private float stepHeight = 0.45f;
    [SerializeField, Min(0.05f)] private float stepSeconds = 0.22f;
    [Tooltip("How far ahead of the body's motion a foot reaches when it steps, in seconds of that motion.")]
    [SerializeField, Min(0f)] private float stepLead = 0.25f;
    [Tooltip("How far an attack throws the body forward, and how high the front legs rear.")]
    [SerializeField, Min(0f)] private float lunge = 0.7f;
    [SerializeField, Min(0f)] private float rear = 1.1f;
    [SerializeField, Min(0.05f)] private float attackSeconds = 0.6f;

    [Header("Cut legs")]
    [Tooltip("Chance a slashing blow that lands (and doesn't kill) cuts off the leg nearest it; a heavy blow or a critical doubles it.")]
    [SerializeField, Range(0f, 1f)] private float severChance = 0.15f;
    [Tooltip("At most this many legs can be cut off; at most half of one side's.")]
    [SerializeField, Range(0, 8)] private int mostSevered = 4;
    [Tooltip("How much slower it moves for each leg lost (0.12 = 12% each).")]
    [SerializeField, Range(0f, 0.5f)] private float slowPerLeg = 0.12f;
    [Tooltip("How far its body sags (metres at scale 1) toward a side for each leg it has lost there.")]
    [SerializeField, Min(0f)] private float sagPerLeg = 0.12f;

    private const int GroundMask = ~((1 << 2) | (1 << 4));
    private static readonly RaycastHit[] Hits = new RaycastHit[8];

    private MonsterAI monster;
    private Damageable health;
    private int severed;
    private Vector3 lastRoot;
    private Vector3 velocity;
    private float attackAt = -10f;
    private Vector3 bodyRest;
    private Quaternion bodyRestRotation;
    private bool started;
    private readonly bool[] groupStepping = new bool[2];

    public Transform Body => body;
    public Leg[] Legs => legs;
    public Vector3 Lengths => lengths;

    /// <summary>How many legs have been cut off.</summary>
    public int Severed => severed;

    /// <summary>Raised with the leg's index when one is cut off.</summary>
    public event Action<int> LegSevered;

    // Its metres at scale 1, grown with the creature.
    private float Size => Mathf.Max(0.05f, transform.lossyScale.y);

    private void Awake()
    {
        monster = GetComponent<MonsterAI>();
        health = GetComponent<Damageable>();
        if (body != null)
        {
            bodyRest = body.localPosition;
            bodyRestRotation = body.localRotation;
        }
    }

    private void OnEnable()
    {
        if (monster != null)
        {
            monster.AttackStarted += OnAttack;
        }

        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    private void OnDisable()
    {
        if (monster != null)
        {
            monster.AttackStarted -= OnAttack;
        }

        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }
    }

    // A slashing blow that lands may take the leg nearest where it struck. Only in the game that judges the blow (a friend's copy is told
    // nothing of it, and walks all eight).
    private void OnDamaged(DamageInfo info, DamageResult result)
    {
        if (!result.Landed || result.Killed || info.Strike != StrikeKind.Slash || info.IsOverTime || monster == null || monster.IsRemote)
        {
            return;
        }

        bool heavy = info.IsCritical || (info.Technique & HitTechnique.Heavy) != 0;
        if (UnityEngine.Random.value >= Mathf.Clamp01(severChance * (heavy ? 2f : 1f)))
        {
            return;
        }

        Sever(NearestLeg(info.HitPoint));
    }

    /// <summary>The standing leg whose knee is nearest <paramref name="point"/>, or -1.</summary>
    public int NearestLeg(Vector3 point)
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < legs.Length; i++)
        {
            Leg leg = legs[i];
            if (leg.lost || leg.segments.Length < 2 || leg.segments[1] == null)
            {
                continue;
            }

            float distance = (leg.segments[1].position - point).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>True when the leg can still be cut off: standing, within Most Severed, and its side keeps at least half its legs.</summary>
    public bool CanSever(int index)
    {
        if (index < 0 || index >= legs.Length || legs[index].lost || severed >= mostSevered)
        {
            return false;
        }

        bool left = legs[index].rest.x < 0f;
        int side = 0, gone = 0;
        foreach (Leg leg in legs)
        {
            if (leg.rest.x < 0f == left)
            {
                side++;
                gone += leg.lost ? 1 : 0;
            }
        }

        return gone + 1 <= side / 2;
    }

    /// <summary>Cuts a leg off: its parts fall away as a loose piece, the gait goes on without it, and the creature slows. False when it
    /// can't be (CanSever).</summary>
    public bool Sever(int index)
    {
        if (!CanSever(index))
        {
            return false;
        }

        Leg leg = legs[index];
        leg.lost = true;
        severed++;
        DropLeg(leg);
        if (monster != null)
        {
            monster.SpeedScale *= Mathf.Max(0.1f, 1f - slowPerLeg);
        }

        LegSevered?.Invoke(index);
        return true;
    }

    // The leg's three parts, as they stand, become one loose piece that tumbles and is gone after a while.
    private void DropLeg(Leg leg)
    {
        var piece = new GameObject("Severed Leg");
        piece.transform.position = leg.segments[0] != null ? leg.segments[0].position : transform.position;
        foreach (Transform segment in leg.segments)
        {
            if (segment != null)
            {
                segment.SetParent(piece.transform, true);
            }
        }

        var box = piece.AddComponent<CapsuleCollider>();
        box.direction = 2;
        float length = (lengths.x + lengths.y + lengths.z) * Size;
        box.height = length * 0.6f;
        box.radius = 0.08f * Size;
        var rigid = piece.AddComponent<Rigidbody>();
        rigid.mass = 6f * Size;
        rigid.AddForce((Vector3.up * 2f + (piece.transform.position - transform.position).normalized * 2.5f) * Size, ForceMode.VelocityChange);
        rigid.AddTorque(UnityEngine.Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
        if (Application.isPlaying)
        {
            Destroy(piece, 25f);
        }
        for (int s = 0; s < leg.segments.Length; s++)
        {
            leg.segments[s] = null;
        }
    }

    private void OnAttack() => attackAt = Time.time;

    private void Plant()
    {
        foreach (Leg leg in legs)
        {
            if (leg.lost)
            {
                continue;
            }

            leg.foot = Ground(transform.TransformPoint(leg.rest));
            leg.from = leg.to = leg.foot;
            leg.stepT = 1f;
            leg.planted = true;
        }

        lastRoot = transform.position;
        started = true;
    }

    private void LateUpdate()
    {
        if (body == null || legs.Length == 0)
        {
            return;
        }

        if (monster != null && monster.CurrentState == MonsterAI.State.Dead)
        {
            enabled = false;
            return;
        }

        if (!started)
        {
            Plant();
        }

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        Vector3 moved = transform.position - lastRoot;
        lastRoot = transform.position;
        velocity = Vector3.Lerp(velocity, moved / dt, 1f - Mathf.Exp(-10f * dt));
        // A teleport (a spawn, a warp) plants the feet anew rather than dragging them across the world.
        if (moved.sqrMagnitude > 25f)
        {
            Plant();
        }

        float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        // A leg short and the rest take up its work: a slower, hitching step.
        float stepTime = stepSeconds * (1f + severed * 0.12f) / Mathf.Clamp(speed / (2.5f * Size), 1f, 3f);
        float attack = Mathf.Clamp01((Time.time - attackAt) / attackSeconds);
        float rearing = attack < 1f ? Mathf.Sin(attack * Mathf.PI) : 0f;

        StepLegs(stepTime, rearing);
        PlaceBody(rearing, speed);
        foreach (Leg leg in legs)
        {
            if (!leg.lost)
            {
                Solve(leg, rearing);
            }
        }
    }

    private void StepLegs(float stepTime, float rearing)
    {
        groupStepping[0] = groupStepping[1] = false;
        foreach (Leg leg in legs)
        {
            groupStepping[leg.group & 1] |= leg.stepT < 1f;
        }

        Vector3 lead = new Vector3(velocity.x, 0f, velocity.z) * stepLead;
        float size = Size;
        float reachAll = (lengths.x + lengths.y + lengths.z) * size;
        foreach (Leg leg in legs)
        {
            if (leg.lost)
            {
                continue;
            }

            // A foot left behind out of reach (a fast turn, a lunge, a foot caught on a root or a rock) steps at once, whatever the
            // other group does, and one far past reach is put down where it belongs: legs stretched to it looked like taffy (the
            // user's playtest, 2026-11-21).
            if (leg.hip != null)
            {
                float fromHip = Vector3.Distance(leg.hip.position, leg.stepT < 1f ? leg.to : leg.foot);
                if (fromHip > reachAll * 1.35f)
                {
                    leg.foot = leg.from = leg.to = Ground(transform.TransformPoint(leg.rest) + lead);
                    leg.stepT = 1f;
                    continue;
                }

                // Only where the step brings the foot clearly nearer: a reach that comes from the body rearing or lunging in an
                // attack (the foot is already where it rests) would re-step every step time, paddling (the review of 2026-11-21).
                if (leg.stepT >= 1f && fromHip > reachAll * 0.95f && rearing <= 0f)
                {
                    Vector3 nearer = Ground(transform.TransformPoint(leg.rest) + lead);
                    if (Vector3.Distance(leg.hip.position, nearer) < fromHip - 0.25f * size)
                    {
                        leg.from = leg.foot;
                        leg.to = nearer;
                        leg.stepT = 0f;
                        groupStepping[leg.group & 1] = true;
                        continue;
                    }
                }
            }

            if (leg.stepT < 1f)
            {
                leg.stepT = Mathf.Min(1f, leg.stepT + Time.deltaTime / stepTime);
                float t = Mathf.SmoothStep(0f, 1f, leg.stepT);
                leg.foot = Vector3.Lerp(leg.from, leg.to, t) + Vector3.up * (Mathf.Sin(leg.stepT * Mathf.PI) * stepHeight * size);
                continue;
            }

            Vector3 wanted = Ground(transform.TransformPoint(leg.rest) + lead);
            bool far = (wanted - leg.foot).sqrMagnitude > stepDistance * stepDistance * size * size;
            if (far && !groupStepping[(leg.group + 1) & 1])
            {
                leg.from = leg.foot;
                leg.to = wanted;
                leg.stepT = 0f;
                groupStepping[leg.group & 1] = true;
            }
        }
    }

    // Over its feet at its height, pitched and rolled with the ground they stand on; lunging and rearing in an attack.
    private void PlaceBody(float rearing, float speed)
    {
        float front = 0f, back = 0f, left = 0f, right = 0f, all = 0f;
        int nf = 0, nb = 0, nl = 0, nr = 0, standing = 0, lostLeft = 0, lostRight = 0, lostFront = 0, lostBack = 0;
        foreach (Leg leg in legs)
        {
            if (leg.lost)
            {
                lostLeft += leg.rest.x < 0f ? 1 : 0;
                lostRight += leg.rest.x < 0f ? 0 : 1;
                lostFront += leg.rest.z >= 0f ? 1 : 0;
                lostBack += leg.rest.z >= 0f ? 0 : 1;
                continue;
            }

            standing++;
            Vector3 local = transform.InverseTransformPoint(leg.planted ? leg.to : leg.foot);
            float height = local.y;
            all += height;
            if (leg.rest.z >= 0f) { front += height; nf++; } else { back += height; nb++; }
            if (leg.rest.x < 0f) { left += height; nl++; } else { right += height; nr++; }
        }

        all /= Mathf.Max(1, standing);
        float pitch = nf > 0 && nb > 0 ? Mathf.Atan2(back / nb - front / nf, 2f) * Mathf.Rad2Deg : 0f;
        float roll = nl > 0 && nr > 0 ? Mathf.Atan2(right / nr - left / nl, 2f) * Mathf.Rad2Deg : 0f;
        // Short of legs, it sags toward the side and end that lost them, lower the more it has lost, and its body hitches as it goes.
        // In the body's own units, as the bob and the rearing are: they grow with the creature.
        float sag = sagPerLeg;
        roll += (lostRight - lostLeft) * 7f;
        pitch += (lostFront - lostBack) * 5f;
        float hitch = severed > 0 ? Mathf.Abs(Mathf.Sin(Time.time * (4f + speed * 1.5f))) * 0.05f * severed * Mathf.Clamp01(speed) : 0f;
        float bob = Mathf.Sin(Time.time * (6f + speed * 2f)) * 0.03f * Mathf.Clamp01(speed);
        Vector3 offset = bodyRest + new Vector3(0f, all + bob - hitch - severed * sag * 0.5f + rearing * 0.35f, rearing * lunge);
        body.localPosition = Vector3.Lerp(body.localPosition, offset, 1f - Mathf.Exp(-14f * Time.deltaTime));
        // Tilted in the model's frame (x right, z ahead), not the mesh's: the mesh lies rotated from its export, and its own
        // axes turned the roll into a yaw.
        Quaternion tilt = Quaternion.Euler(pitch - rearing * 18f, 0f, roll);
        body.localRotation = Quaternion.Slerp(body.localRotation, tilt * bodyRestRotation, 1f - Mathf.Exp(-10f * Time.deltaTime));
    }

    // Two-bone IK from hip to ankle (knee up and out), the tarsus from ankle to foot.
    private void Solve(Leg leg, float rearing)
    {
        if (leg.hip == null || leg.segments.Length < 3)
        {
            return;
        }

        Vector3 hip = leg.hip.position;
        Vector3 foot = leg.foot;
        Vector3 l = lengths * Size;
        if (leg.front && rearing > 0f)
        {
            Vector3 raised = hip + transform.forward * (l.x * 0.6f) + Vector3.up * (rear * Size);
            foot = Vector3.Lerp(foot, raised, rearing);
        }

        Vector3 outward = foot - transform.position;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : transform.forward;
        Vector3 ankle = foot + (outward * 0.2f + Vector3.up).normalized * l.z;

        Vector3 toAnkle = ankle - hip;
        float reach = Mathf.Clamp(toAnkle.magnitude, Mathf.Abs(l.x - l.y) + 0.01f, l.x + l.y - 0.01f);
        Vector3 dir = toAnkle.sqrMagnitude > 1e-6f ? toAnkle.normalized : outward;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        Vector3 bend = side.sqrMagnitude > 1e-6f ? Vector3.Cross(side.normalized, dir).normalized : Vector3.up;
        if (Vector3.Dot(bend, Vector3.up) < 0f)
        {
            bend = -bend;
        }

        float cos = Mathf.Clamp((l.x * l.x + reach * reach - l.y * l.y) / (2f * l.x * reach), -1f, 1f);
        Vector3 knee = hip + dir * (l.x * cos) + bend * (l.x * Mathf.Sqrt(1f - cos * cos));
        // The ankle where the femur and tibia actually reach (the wanted one may be out of reach).
        ankle = knee + (ankle - knee).normalized * l.y;

        // The tarsus keeps its length: a foot out of reach is reached toward, not stretched to (it steps there soon after).
        Vector3 toFoot = foot - ankle;
        if (toFoot.magnitude > l.z * 1.1f)
        {
            foot = ankle + toFoot.normalized * (l.z * 1.1f);
        }

        Place(leg.segments[0], hip, knee);
        Place(leg.segments[1], knee, ankle);
        Place(leg.segments[2], ankle, foot);
    }

    private static void Place(Transform segment, Vector3 from, Vector3 to)
    {
        if (segment == null)
        {
            return;
        }

        Vector3 along = to - from;
        segment.position = from;
        if (along.sqrMagnitude > 1e-6f)
        {
            segment.rotation = Quaternion.LookRotation(along, Vector3.up);
        }

        Vector3 scale = segment.localScale;
        // Its mesh runs one unit along +Z: its length is its z scale, in the parent's units.
        float parentScale = segment.parent != null ? Mathf.Max(1e-4f, segment.parent.lossyScale.z) : 1f;
        segment.localScale = new Vector3(scale.x, scale.y, along.magnitude / parentScale);
    }

    // The ground under a point: the first hit that isn't this creature or any other creature.
    private Vector3 Ground(Vector3 point)
    {
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 3f, Vector3.down, Hits, 8f, GroundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 found = new Vector3(point.x, transform.position.y, point.z);
        for (int i = 0; i < count; i++)
        {
            Collider hit = Hits[i].collider;
            if (hit.transform.IsChildOf(transform) || hit.attachedRigidbody != null || hit.GetComponentInParent<Damageable>() != null && !hit.GetComponentInParent<Terrain>())
            {
                continue;
            }

            if (Hits[i].distance < best)
            {
                best = Hits[i].distance;
                found = Hits[i].point;
            }
        }

        return found;
    }

    // ----- Built once, in the editor -----

    /// <summary>
    /// Makes the legs under <paramref name="model"/>: eight legs of three parts each from hips round the front of the
    /// body, with <paramref name="segment"/> (a mesh one unit along +Z) and <paramref name="material"/>. Replaces legs made
    /// before. Editor tools call it while setting up the prefab.
    /// </summary>
    public void Build(Transform model, Transform bodyPart, Mesh segment, Material material, float bodyLength, float span)
    {
        body = bodyPart;
        Transform old = model.Find("Legs");
        if (old != null)
        {
            DestroyImmediate(old.gameObject);
        }

        var holder = new GameObject("Legs").transform;
        holder.SetParent(model, false);
        float[] angles = { 52f, 18f, -16f, -48f };
        float[] thickness = { 0.11f, 0.095f, 0.06f };
        legs = new Leg[8];
        for (int i = 0; i < 8; i++)
        {
            int pair = i % 4;
            float sideSign = i < 4 ? -1f : 1f;
            var leg = new Leg { group = (pair + (i < 4 ? 0 : 1)) & 1, front = pair == 0 };
            var hip = new GameObject($"Hip {(sideSign < 0 ? "L" : "R")}{pair + 1}").transform;
            hip.SetParent(bodyPart, false);
            // Round the front part of the body (the cephalothorax), a little apart.
            float along = bodyLength * (0.28f - pair * 0.07f);
            hip.position = bodyPart.position + transform.forward * along + transform.right * (sideSign * bodyLength * 0.12f) - Vector3.up * 0.05f;
            leg.hip = hip;
            float angle = angles[pair] * Mathf.Deg2Rad;
            Vector3 way = new Vector3(sideSign * Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            leg.rest = transform.InverseTransformPoint(hip.position) + way * span;
            leg.rest.y = 0f;
            for (int s = 0; s < 3; s++)
            {
                var part = new GameObject($"Leg {(sideSign < 0 ? "L" : "R")}{pair + 1} {(s == 0 ? "Femur" : s == 1 ? "Tibia" : "Tarsus")}", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                part.SetParent(holder, false);
                part.GetComponent<MeshFilter>().sharedMesh = segment;
                part.GetComponent<MeshRenderer>().sharedMaterial = material;
                float parentScale = Mathf.Max(1e-4f, holder.lossyScale.x);
                part.localScale = new Vector3(thickness[s] / parentScale, thickness[s] / parentScale, 1f);
                leg.segments[s] = part;
            }

            legs[i] = leg;
        }

        started = false;
    }

    /// <summary>
    /// A leg part's mesh: one unit along +Z, round, thick at the joint and tapering, with a knob at the start, made of
    /// rings so it shades smoothly. Radius 1 at the base (the part's x and y scale set its thickness).
    /// </summary>
    public static Mesh SegmentMesh()
    {
        const int sides = 10;
        float[] zs = { 0f, 0.06f, 0.2f, 0.55f, 0.9f, 1f };
        // Thick at both joints, so one part meets the next without a gap.
        float[] rs = { 0.8f, 1f, 0.9f, 0.76f, 0.72f, 0.7f };
        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();
        for (int ring = 0; ring < zs.Length; ring++)
        {
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                var around = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices.Add(around * rs[ring] + Vector3.forward * zs[ring]);
                normals.Add(around);
                uvs.Add(new Vector2(s / (float)sides, zs[ring]));
            }
        }

        for (int ring = 0; ring < zs.Length - 1; ring++)
        {
            for (int s = 0; s < sides; s++)
            {
                int a = ring * (sides + 1) + s, b = a + sides + 1;
                // Clockwise seen from outside (Unity's front face).
                triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
        }

        // Caps, so a cut or a gap never shows inside.
        int startCentre = vertices.Count;
        vertices.Add(Vector3.zero); normals.Add(Vector3.back); uvs.Add(Vector2.zero);
        int endCentre = vertices.Count;
        vertices.Add(Vector3.forward); normals.Add(Vector3.forward); uvs.Add(Vector2.up);
        int last = (zs.Length - 1) * (sides + 1);
        for (int s = 0; s < sides; s++)
        {
            triangles.AddRange(new[] { startCentre, s + 1, s });
            triangles.AddRange(new[] { endCentre, last + s, last + s + 1 });
        }

        var mesh = new Mesh { name = "Spider Leg Segment" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
