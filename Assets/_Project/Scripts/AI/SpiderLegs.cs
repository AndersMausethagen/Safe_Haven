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
/// </summary>
[DisallowMultipleComponent]
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

    private const int GroundMask = ~((1 << 2) | (1 << 4));
    private static readonly RaycastHit[] Hits = new RaycastHit[8];

    private MonsterAI monster;
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

    private void Awake()
    {
        monster = GetComponent<MonsterAI>();
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
    }

    private void OnDisable()
    {
        if (monster != null)
        {
            monster.AttackStarted -= OnAttack;
        }
    }

    private void OnAttack() => attackAt = Time.time;

    private void Plant()
    {
        foreach (Leg leg in legs)
        {
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
        float stepTime = stepSeconds / Mathf.Clamp(speed / 2.5f, 1f, 3f);
        float attack = Mathf.Clamp01((Time.time - attackAt) / attackSeconds);
        float rearing = attack < 1f ? Mathf.Sin(attack * Mathf.PI) : 0f;

        StepLegs(stepTime, rearing);
        PlaceBody(rearing, speed);
        foreach (Leg leg in legs)
        {
            Solve(leg, rearing);
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
        foreach (Leg leg in legs)
        {
            if (leg.stepT < 1f)
            {
                leg.stepT = Mathf.Min(1f, leg.stepT + Time.deltaTime / stepTime);
                float t = Mathf.SmoothStep(0f, 1f, leg.stepT);
                leg.foot = Vector3.Lerp(leg.from, leg.to, t) + Vector3.up * (Mathf.Sin(leg.stepT * Mathf.PI) * stepHeight);
                continue;
            }

            Vector3 wanted = Ground(transform.TransformPoint(leg.rest) + lead);
            bool far = (wanted - leg.foot).sqrMagnitude > stepDistance * stepDistance;
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
        int nf = 0, nb = 0, nl = 0, nr = 0;
        foreach (Leg leg in legs)
        {
            Vector3 local = transform.InverseTransformPoint(leg.planted ? leg.to : leg.foot);
            float height = local.y;
            all += height;
            if (leg.rest.z >= 0f) { front += height; nf++; } else { back += height; nb++; }
            if (leg.rest.x < 0f) { left += height; nl++; } else { right += height; nr++; }
        }

        all /= Mathf.Max(1, legs.Length);
        float pitch = nf > 0 && nb > 0 ? Mathf.Atan2(back / nb - front / nf, 2f) * Mathf.Rad2Deg : 0f;
        float roll = nl > 0 && nr > 0 ? Mathf.Atan2(right / nr - left / nl, 2f) * Mathf.Rad2Deg : 0f;
        float bob = Mathf.Sin(Time.time * (6f + speed * 2f)) * 0.03f * Mathf.Clamp01(speed);
        Vector3 offset = bodyRest + new Vector3(0f, all + bob + rearing * 0.35f, rearing * lunge);
        body.localPosition = Vector3.Lerp(body.localPosition, offset, 1f - Mathf.Exp(-14f * Time.deltaTime));
        Quaternion tilt = Quaternion.Euler(pitch - rearing * 18f, 0f, roll);
        body.localRotation = Quaternion.Slerp(body.localRotation, bodyRestRotation * tilt, 1f - Mathf.Exp(-10f * Time.deltaTime));
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
        if (leg.front && rearing > 0f)
        {
            Vector3 raised = hip + transform.forward * (lengths.x * 0.6f) + Vector3.up * rear;
            foot = Vector3.Lerp(foot, raised, rearing);
        }

        Vector3 outward = foot - transform.position;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : transform.forward;
        Vector3 ankle = foot + (outward * 0.2f + Vector3.up).normalized * lengths.z;

        Vector3 toAnkle = ankle - hip;
        float reach = Mathf.Clamp(toAnkle.magnitude, Mathf.Abs(lengths.x - lengths.y) + 0.01f, lengths.x + lengths.y - 0.01f);
        Vector3 dir = toAnkle.sqrMagnitude > 1e-6f ? toAnkle.normalized : outward;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        Vector3 bend = side.sqrMagnitude > 1e-6f ? Vector3.Cross(side.normalized, dir).normalized : Vector3.up;
        if (Vector3.Dot(bend, Vector3.up) < 0f)
        {
            bend = -bend;
        }

        float cos = Mathf.Clamp((lengths.x * lengths.x + reach * reach - lengths.y * lengths.y) / (2f * lengths.x * reach), -1f, 1f);
        Vector3 knee = hip + dir * (lengths.x * cos) + bend * (lengths.x * Mathf.Sqrt(1f - cos * cos));
        // The ankle where the femur and tibia actually reach (the wanted one may be out of reach).
        ankle = knee + (ankle - knee).normalized * lengths.y;

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
