using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A boss arena's walls that rise out of the ground at one stage of the fight and sink again at a later one: the Heartroot's root walls,
/// four spokes that split Guardian's Hollow into lanes at 66% and go down at 33% (Docs/HeartrootEncounterDesign_2026-11-17.md §2.2,
/// Docs/HeartrootBossReview_2026-11-17.md C7). The mirror of CollapsingGround.
/// <para>
/// Each wall is placed in the scene where it stands when risen (so it is seen and fitted in the Editor); the scene starts with it sunk out of
/// sight. As its stage begins it warns (dust, a crack, an optional mark on the ground), waits until the boss is off its line, nudges anyone
/// standing on it aside (never hurt or trapped), then rises with its colliders on and a carving NavMeshObstacle, so creatures walk round
/// by the gaps. A wall stops what flies (a Thornwave's thorns: MonsterProjectile stops on any collider) but not what comes from under or
/// above (Grasping Roots, eruptions): that rule is the arena's, not this component's.
/// </para>
/// <para>
/// Where it belongs: on an object in the arena scene, with the walls (each a child holding its model and colliders) in rising order.
/// A friend's game (co-op) runs its own from its copy of the boss, whose phases follow the host's health, so the walls stand on every
/// game: the boss is known by its BossDefinition (BossEncounter.AnyPhaseEntered), as the one placed here is removed on a friend's game.
/// A copy that arrives with the fight already past both stages raises nothing.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "each game raises and sinks its own walls from its copy of the boss's phases")]
public sealed class RisingGround : MonoBehaviour
{
    [Serializable]
    public sealed class Piece
    {
        [Tooltip("The wall that rises: its model and colliders, placed where it stands when risen.")]
        public Transform piece;
        [Tooltip("Seconds after the previous wall (or the stage, for the first) before this one starts to warn.")]
        [Min(0f)] public float after;
    }

    [Tooltip("The boss whose stages raise and sink the walls. Known by its Boss Definition: on a friend's game (co-op) the host's copy does it too.")]
    [SerializeField] private BossEncounter boss;
    [Tooltip("The stage that raises them (1 is the first of the boss's Phases: the Heartroot's 66%).")]
    [SerializeField, Min(1)] private int riseAtPhase = 1;
    [Tooltip("The stage that sinks them again (the Heartroot's 33%: 2). 0: they stand to the end.")]
    [SerializeField, Min(0)] private int sinkAtPhase = 2;
    [Tooltip("The walls, in the order they rise.")]
    [SerializeField] private List<Piece> pieces = new List<Piece>();
    [Tooltip("Seconds of cracks and dust before a wall comes up: the warning to step off its line.")]
    [SerializeField, Min(0.5f)] private float warning = 2.5f;
    [Tooltip("Seconds a wall takes to rise.")]
    [SerializeField, Min(0.1f)] private float riseSeconds = 1.2f;
    [Tooltip("Seconds a wall takes to sink.")]
    [SerializeField, Min(0.1f)] private float sinkSeconds = 1.6f;
    [Tooltip("Metres below where it stands that a wall waits, out of sight. 0: its own height and a little more.")]
    [SerializeField, Min(0f)] private float depth;
    [Tooltip("Metres beyond a wall's sides that a body standing there is nudged clear of as it rises.")]
    [SerializeField, Min(0f)] private float nudgeMargin = 0.6f;
    [Tooltip("Seconds the nudge takes.")]
    [SerializeField, Min(0.05f)] private float nudgeSeconds = 0.35f;
    [Tooltip("Camera shake as a wall breaks the ground.")]
    [SerializeField, Min(0f)] private float shake = 0.5f;
    [SerializeField] private SoundDefinition crackSound;
    [SerializeField] private SoundDefinition riseSound;
    [SerializeField] private SoundDefinition sinkSound;
    [Tooltip("Dust spawned at a wall's foot as it warns, rises and sinks (a particle prefab). Empty: none.")]
    [SerializeField] private GameObject dust;
    [Tooltip("Laid on the ground along a wall's line while it warns, turned with the wall (a crack decal, a telegraph strip). Empty: none.")]
    [SerializeField] private GameObject warningMark;

    private static readonly Collider[] Overlaps = new Collider[32];
    private static readonly List<Transform> Nudged = new List<Transform>();

    // The boss this listens for, read from the one placed as it starts listening (on a friend's game that one is then removed).
    private BossDefinition watching;
    private bool rising;
    private bool sinking;
    private Wall[] walls = new Wall[0];
    // The warning laid for the wall about to rise, so a sink that cuts the rise short takes it away too.
    private GameObject warningShown;

    // What is known of each wall from where it stands risen, worked out before it is first sunk.
    private sealed class Wall
    {
        public Transform Piece;
        public Vector3 Rest;
        public Bounds Local;
        public float Depth;
        public bool Up;
        public GameObject Obstacle;
    }

    /// <summary>How many walls stand risen now.</summary>
    public int Risen { get; private set; }

    public IReadOnlyList<Piece> Pieces => pieces;

    private void Awake()
    {
        var list = new List<Wall>();
        foreach (Piece each in pieces)
        {
            if (each == null || each.piece == null)
            {
                continue;
            }

            Bounds local = LocalBounds(each.piece);
            list.Add(new Wall
            {
                Piece = each.piece,
                Rest = each.piece.localPosition,
                Local = local,
                Depth = depth > 0f ? depth : local.size.y * each.piece.lossyScale.y + 0.5f,
            });
            each.piece.gameObject.SetActive(false);
        }

        walls = list.ToArray();
    }

    private void OnEnable()
    {
        BossDefinition placed = BossEncounter.DefinitionOf(boss);
        if (placed != null)
        {
            watching = placed;
        }

        BossEncounter.AnyPhaseEntered += OnPhase;
    }

    private void OnDisable()
    {
        BossEncounter.AnyPhaseEntered -= OnPhase;
    }

    private void OnPhase(BossEncounter from, int phase)
    {
        if (watching == null || from == null || from.Definition != watching)
        {
            return;
        }

        // Past the stage that sinks them (a friend's copy arriving late can pass both at once): whatever stands goes down, nothing more rises.
        if (sinkAtPhase > 0 && phase >= sinkAtPhase)
        {
            if (!sinking)
            {
                sinking = true;
                rising = true;
                StopAllCoroutines();
                if (warningShown != null)
                {
                    Destroy(warningShown);
                }

                StartCoroutine(SinkAll());
            }

            return;
        }

        if (!rising && phase >= riseAtPhase)
        {
            rising = true;
            StartCoroutine(RiseAll(from));
        }
    }

    private IEnumerator RiseAll(BossEncounter from)
    {
        for (int i = 0; i < walls.Length; i++)
        {
            float after = i < pieces.Count && pieces[i] != null ? pieces[i].after : 0f;
            if (after > 0f)
            {
                yield return new WaitForSeconds(after);
            }

            yield return Rise(walls[i], from);
        }
    }

    private IEnumerator Rise(Wall wall, BossEncounter from)
    {
        Vector3 foot = FootOf(wall);
        AudioManager.Play(crackSound, foot);
        Burst(foot);
        CameraShake.Add(shake * 0.5f);
        if (warningMark != null)
        {
            warningShown = Instantiate(warningMark, foot, wall.Piece.rotation);
        }

        yield return new WaitForSeconds(warning);
        // Never through the boss: it waits until his body is off the line (or he is down).
        float bossRadius = from != null && from.TryGetComponent(out NavMeshAgent bossAgent)
            ? bossAgent.radius * Mathf.Abs(from.transform.lossyScale.x) + 0.5f : 1.5f;
        while (from != null && from.IsAwake && OnLine(wall, from.transform.position, bossRadius))
        {
            yield return new WaitForSeconds(0.25f);
        }

        if (warningShown != null)
        {
            Destroy(warningShown);
            warningShown = null;
        }

        NudgeOff(wall, from != null ? from.transform : null);
        Transform piece = wall.Piece;
        Vector3 low = wall.Rest - LocalUp(piece) * wall.Depth;
        piece.localPosition = low;
        piece.gameObject.SetActive(true);
        wall.Up = true;
        Risen++;
        wall.Obstacle = Carve(wall);
        AudioManager.Play(riseSound, foot);
        Burst(foot);
        CameraShake.Add(shake);
        for (float t = 0f; t < riseSeconds; t += Time.deltaTime)
        {
            float k = 1f - (1f - t / riseSeconds) * (1f - t / riseSeconds);
            piece.localPosition = Vector3.Lerp(low, wall.Rest, k);
            yield return null;
        }

        piece.localPosition = wall.Rest;
    }

    private IEnumerator SinkAll()
    {
        foreach (Wall wall in walls)
        {
            if (wall.Up)
            {
                StartCoroutine(Sink(wall));
            }
        }

        yield break;
    }

    private IEnumerator Sink(Wall wall)
    {
        Transform piece = wall.Piece;
        Vector3 foot = FootOf(wall);
        AudioManager.Play(sinkSound, foot);
        Burst(foot);
        CameraShake.Add(shake * 0.5f);
        // Nothing stands on a wall going down.
        foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        Vector3 from = piece.localPosition;
        Vector3 low = wall.Rest - LocalUp(piece) * wall.Depth;
        for (float t = 0f; t < sinkSeconds; t += Time.deltaTime)
        {
            float k = t / sinkSeconds;
            piece.localPosition = Vector3.Lerp(from, low, k * k);
            yield return null;
        }

        if (wall.Obstacle != null)
        {
            Destroy(wall.Obstacle);
            wall.Obstacle = null;
        }

        foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = true;
        }

        piece.localPosition = wall.Rest;
        piece.gameObject.SetActive(false);
        wall.Up = false;
        Risen--;
    }

    // Closed to creatures where it stands, turned with it: a box the size of the wall, carved out of the navmesh, so they go by the gaps.
    private GameObject Carve(Wall wall)
    {
        var gap = new GameObject(wall.Piece.name + " (carve)");
        gap.transform.SetParent(wall.Piece, false);
        gap.transform.localPosition = wall.Local.center;
        NavMeshObstacle obstacle = gap.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = wall.Local.size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        return gap;
    }

    // Everyone standing on its line is moved clear sideways: the player, companions and creatures of this game. The host's creatures on a
    // friend's game, and friends' bodies in the host's, are moved by their own game.
    private void NudgeOff(Wall wall, Transform bossBody)
    {
        Transform piece = wall.Piece;
        Vector3 scale = piece.lossyScale;
        Vector3 half = Vector3.Scale(wall.Local.extents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        Vector3 centre = piece.parent != null ? piece.parent.TransformPoint(wall.Rest) : wall.Rest;
        centre += piece.rotation * Vector3.Scale(wall.Local.center, scale);
        int count = Physics.OverlapBoxNonAlloc(centre, half + new Vector3(nudgeMargin, 1f, nudgeMargin), Overlaps, piece.rotation, ~0, QueryTriggerInteraction.Ignore);
        Nudged.Clear();
        for (int i = 0; i < count; i++)
        {
            Transform body = Movable(Overlaps[i]);
            if (body == null || body == bossBody || Nudged.Contains(body))
            {
                continue;
            }

            Nudged.Add(body);
            Vector3 local = Quaternion.Inverse(piece.rotation) * (body.position - centre);
            float metres = NudgeOut(local, half, nudgeMargin + 0.4f, out Vector3 way);
            if (metres <= 0f)
            {
                continue;
            }

            Vector3 world = piece.rotation * way;
            world.y = 0f;
            if (world.sqrMagnitude < 1e-4f)
            {
                continue;
            }

            // Drag toward a point behind it by a negative distance: away, along the way out.
            ForcedMove.Drag(body, body.position - world.normalized, -metres, nudgeSeconds);
        }

        Nudged.Clear();
    }

    // What a nudge moves: something walking (a CharacterController or a NavMeshAgent), not a copy run by the host or a friend's body.
    private static Transform Movable(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        CharacterController walker = collider.GetComponentInParent<CharacterController>();
        if (walker != null)
        {
            return walker.TryGetComponent(out FriendHitbox _) ? null : walker.transform;
        }

        NavMeshAgent agent = collider.GetComponentInParent<NavMeshAgent>();
        if (agent == null || !agent.enabled)
        {
            return null;
        }

        return agent.TryGetComponent(out MonsterAI creature) && creature.IsRemote ? null : agent.transform;
    }

    /// <summary>
    /// How far, and which way, a body at <paramref name="local"/> (in the wall's frame, from its centre) must move to stand clear of a wall
    /// <paramref name="half"/> its extents, plus <paramref name="margin"/>: out through the nearer long side, so it never crosses the wall.
    /// 0 when it already stands clear. Pure, for tests.
    /// </summary>
    public static float NudgeOut(Vector3 local, Vector3 half, float margin, out Vector3 way)
    {
        way = Vector3.zero;
        float outX = half.x + margin - Mathf.Abs(local.x);
        float outZ = half.z + margin - Mathf.Abs(local.z);
        if (outX <= 0f || outZ <= 0f)
        {
            return 0f;
        }

        if (outX <= outZ)
        {
            way = new Vector3(local.x >= 0f ? 1f : -1f, 0f, 0f);
            return outX;
        }

        way = new Vector3(0f, 0f, local.z >= 0f ? 1f : -1f);
        return outZ;
    }

    // Whether a point stands on a wall's line (in its footprint, widened by margin), seen from above.
    private static bool OnLine(Wall wall, Vector3 point, float margin)
    {
        Transform piece = wall.Piece;
        Vector3 scale = piece.lossyScale;
        Vector3 half = Vector3.Scale(wall.Local.extents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        Vector3 centre = (piece.parent != null ? piece.parent.TransformPoint(wall.Rest) : wall.Rest) + piece.rotation * Vector3.Scale(wall.Local.center, scale);
        return NudgeOut(Quaternion.Inverse(piece.rotation) * (point - centre), half, margin, out _) > 0f;
    }

    // The middle of its foot where it stands risen: where its warning, dust and sounds are.
    private static Vector3 FootOf(Wall wall)
    {
        Transform piece = wall.Piece;
        Vector3 rest = piece.parent != null ? piece.parent.TransformPoint(wall.Rest) : wall.Rest;
        Vector3 bottom = new Vector3(wall.Local.center.x, wall.Local.min.y, wall.Local.center.z);
        return rest + piece.rotation * Vector3.Scale(bottom, piece.lossyScale);
    }

    // A metre up as the wall's parent sees it (its scale included), so it rises straight out of the ground whatever it is turned to.
    private static Vector3 LocalUp(Transform piece) => piece.parent != null ? piece.parent.InverseTransformVector(Vector3.up) : Vector3.up;

    private void Burst(Vector3 at)
    {
        if (dust != null)
        {
            Destroy(Instantiate(dust, at, Quaternion.identity), 8f);
        }
    }

    // Its size in its own frame, from its meshes and box colliders (inactive ones too), so a wall turned on a diagonal carves and nudges
    // along its own line rather than a square round it.
    private static Bounds LocalBounds(Transform piece)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (MeshFilter filter in piece.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null)
            {
                Encapsulate(ref bounds, ref any, piece, filter.transform, filter.sharedMesh.bounds);
            }
        }

        foreach (BoxCollider box in piece.GetComponentsInChildren<BoxCollider>(true))
        {
            Encapsulate(ref bounds, ref any, piece, box.transform, new Bounds(box.center, box.size));
        }

        return any ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private static void Encapsulate(ref Bounds bounds, ref bool any, Transform piece, Transform part, Bounds partBounds)
    {
        Vector3 min = partBounds.min, max = partBounds.max;
        for (int corner = 0; corner < 8; corner++)
        {
            var point = new Vector3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
            Vector3 local = piece.InverseTransformPoint(part.TransformPoint(point));
            if (!any)
            {
                bounds = new Bounds(local, Vector3.zero);
                any = true;
            }
            else
            {
                bounds.Encapsulate(local);
            }
        }
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Nudged.Clear();
    }
}
