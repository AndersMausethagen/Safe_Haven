using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>When a MobSpawnZone fills itself.</summary>
public enum MobSpawnTrigger
{
    /// <summary>As soon as the scene starts.</summary>
    OnStart = 0,
    /// <summary>When the player comes within Wake Distance of the zone.</summary>
    PlayerNear = 1,
    /// <summary>Only when something calls Spawn (a quest step, a trigger, an event).</summary>
    Manual = 2,
}

/// <summary>
/// An area drawn on the ground where creatures appear: a polygon (drag its corners in the Scene view; Shift-click an edge
/// to add a corner, Ctrl-click a corner to remove it). It spawns Count creatures from its list (one prefab for a
/// specific creature, several for a weighted random pick; or a Spawn Table asset), at random spots inside it on walkable
/// ground, kept Separation apart. It can fill itself when the scene starts, when the player comes near, or only when told
/// to (Spawn, e.g. from a quest), and can refill a while after its creatures are dead. <see cref="Cleared"/> (and On
/// Cleared) fires when everything it spawned is dead, which is what a "kill them all" step listens to.
/// <para>
/// In a raid, spawned creatures get the raid's strength (RaidDirector.PrepareSpawned). Hunt Player sends them straight at
/// the player. Works in any scene; nothing it spawns is saved.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class MobSpawnZone : MonoBehaviour
{
    [Tooltip("The zone's corners, relative to this object (height is ignored). Edit them in the Scene view.")]
    [SerializeField] private List<Vector3> corners = new List<Vector3>
    {
        new Vector3(-5f, 0f, -5f), new Vector3(5f, 0f, -5f), new Vector3(5f, 0f, 5f), new Vector3(-5f, 0f, 5f),
    };

    [Header("What")]
    [Tooltip("Creatures to pick from, by weight. One entry spawns exactly that creature. Ignored when a Spawn Table is set.")]
    [SerializeField] private List<SpawnTable.Entry> creatures = new List<SpawnTable.Entry>();
    [Tooltip("Optional shared list of creatures; replaces the list above.")]
    [SerializeField] private SpawnTable table;
    [Tooltip("How many creatures, at least and at most.")]
    [SerializeField] private Vector2Int count = new Vector2Int(2, 4);
    [Tooltip("Creatures keep at least this far apart, in metres.")]
    [SerializeField, Min(0f)] private float separation = 2f;
    [Tooltip("Turns each creature to a random facing.")]
    [SerializeField] private bool randomFacing = true;
    [Tooltip("Creatures head straight for the player and never give up the chase.")]
    [SerializeField] private bool huntPlayer;

    [Header("When")]
    [SerializeField] private MobSpawnTrigger trigger = MobSpawnTrigger.PlayerNear;
    [Tooltip("For Player Near: how close the player must come to the zone's edge, in metres.")]
    [SerializeField, Min(0f)] private float wakeDistance = 25f;
    [Tooltip("Fill the zone again once its creatures are dead...")]
    [SerializeField] private bool respawn;
    [Tooltip("...this many seconds later, when the player isn't within Wake Distance (so nothing appears in plain sight).")]
    [SerializeField, Min(0f)] private float respawnSeconds = 180f;

    [Header("Events")]
    [SerializeField] private UnityEvent onSpawned = new UnityEvent();
    [SerializeField] private UnityEvent onCleared = new UnityEvent();

    private readonly List<GameObject> alive = new List<GameObject>();
    private readonly List<Vector3> worldCorners = new List<Vector3>();
    private System.Random random;
    private bool hasSpawned;
    private bool clearAnnounced = true;
    private float respawnAt = -1f;

    /// <summary>Raised when everything the zone spawned is dead.</summary>
    public event Action Cleared;

    /// <summary>Raised with each creature the zone spawns.</summary>
    public event Action<GameObject> CreatureSpawned;

    public IReadOnlyList<Vector3> Corners => corners;
    public MobSpawnTrigger Trigger => trigger;
    public int AliveCount
    {
        get
        {
            Prune();
            return alive.Count;
        }
    }

    /// <summary>True once the zone has spawned and everything it spawned is dead.</summary>
    public bool IsCleared => hasSpawned && AliveCount == 0;

    /// <summary>The creatures it can spawn, from the table or its own list.</summary>
    public IReadOnlyList<SpawnTable.Entry> Entries => table != null ? table.Entries : creatures;

    /// <summary>The corners in world space.</summary>
    public IReadOnlyList<Vector3> WorldCorners
    {
        get
        {
            worldCorners.Clear();
            foreach (Vector3 corner in corners)
            {
                worldCorners.Add(transform.TransformPoint(corner));
            }

            return worldCorners;
        }
    }

    /// <summary>Replaces the corners (editor tools).</summary>
    public void SetCorners(IEnumerable<Vector3> points)
    {
        corners = new List<Vector3>(points);
    }

    private void Awake()
    {
        random = new System.Random(Environment.TickCount ^ transform.position.GetHashCode());
    }

    private void Start()
    {
        if (trigger == MobSpawnTrigger.OnStart)
        {
            Spawn();
        }
    }

    private void Update()
    {
        Prune();
        if (hasSpawned && !clearAnnounced && alive.Count == 0)
        {
            clearAnnounced = true;
            respawnAt = respawn ? Time.time + respawnSeconds : -1f;
            Cleared?.Invoke();
            onCleared.Invoke();
        }

        if (trigger == MobSpawnTrigger.PlayerNear && !hasSpawned && PlayerWithin(wakeDistance))
        {
            Spawn();
        }
        else if (respawnAt >= 0f && Time.time >= respawnAt && !PlayerWithin(wakeDistance))
        {
            respawnAt = -1f;
            Spawn();
        }
    }

    /// <summary>Fills the zone now (again, if it was cleared). Returns how many creatures appeared.</summary>
    public int Spawn()
    {
        IReadOnlyList<SpawnTable.Entry> entries = Entries;
        if (entries == null || entries.Count == 0 || corners.Count < 3)
        {
            Debug.LogWarning($"{name}: a Mob Spawn Zone needs at least one creature and three corners.", this);
            return 0;
        }

        hasSpawned = true;
        int wanted = random.Next(Mathf.Min(count.x, count.y), Mathf.Max(count.x, count.y) + 1);
        var counts = new Dictionary<GameObject, int>();
        var placed = new List<Vector3>();
        int made = 0;
        for (int i = 0; i < wanted; i++)
        {
            GameObject prefab = SpawnTable.Pick(entries, random, counts);
            if (prefab == null || !TryPlace(placed, out Vector3 at))
            {
                continue;
            }

            Quaternion facing = randomFacing ? Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) : transform.rotation;
            GameObject creature = Instantiate(prefab, at, facing);
            counts.TryGetValue(prefab, out int n);
            counts[prefab] = n + 1;
            placed.Add(at);
            Prepare(creature);
            alive.Add(creature);
            made++;
            CreatureSpawned?.Invoke(creature);
        }

        if (made > 0)
        {
            clearAnnounced = false;
            onSpawned.Invoke();
        }

        return made;
    }

    /// <summary>Removes every creature the zone spawned that is still alive (a quest that resets).</summary>
    public void Despawn()
    {
        foreach (GameObject creature in alive)
        {
            if (creature != null)
            {
                Destroy(creature);
            }
        }

        alive.Clear();
        hasSpawned = false;
        clearAnnounced = true;
        respawnAt = -1f;
    }

    // A random spot inside the zone, on the navmesh, apart from the others; a few tries.
    private bool TryPlace(List<Vector3> taken, out Vector3 at)
    {
        IReadOnlyList<Vector3> world = WorldCorners;
        float height = transform.position.y;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (!ZonePolygon.TryRandomPoint(world, random, out Vector3 flat))
            {
                break;
            }

            flat.y = height;
            if (!NavMesh.SamplePosition(flat, out NavMeshHit hit, 4f, NavMesh.AllAreas) || !ZonePolygon.Contains(world, hit.position))
            {
                continue;
            }

            bool crowded = false;
            foreach (Vector3 other in taken)
            {
                crowded |= (other - hit.position).sqrMagnitude < separation * separation;
            }

            if (!crowded)
            {
                at = hit.position;
                return true;
            }
        }

        at = default;
        return false;
    }

    private void Prepare(GameObject creature)
    {
        foreach (MonsterAI monster in creature.GetComponentsInChildren<MonsterAI>())
        {
            monster.ResetHome();
        }

        if (RaidDirector.Current != null)
        {
            RaidDirector.Current.PrepareSpawned(creature, huntPlayer);
            return;
        }

        if (huntPlayer && Player.Current != null)
        {
            foreach (MonsterAI monster in creature.GetComponentsInChildren<MonsterAI>())
            {
                monster.PlayerOnly = true;
                monster.Relentless = true;
                monster.Engage(Player.Current.Health);
            }
        }
    }

    private void Prune()
    {
        alive.RemoveAll(creature => creature == null || creature.TryGetComponent(out Damageable health) && !health.IsAlive);
    }

    private bool PlayerWithin(float distance)
    {
        if (Player.Current == null)
        {
            return false;
        }

        Vector3 player = Player.Current.transform.position;
        IReadOnlyList<Vector3> world = WorldCorners;
        if (ZonePolygon.Contains(world, player))
        {
            return true;
        }

        for (int i = 0, j = world.Count - 1; i < world.Count; j = i++)
        {
            if (DistanceToSegment(player, world[j], world[i]) <= distance)
            {
                return true;
            }
        }

        return false;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector2 p = new Vector2(point.x, point.z), s = new Vector2(a.x, a.z), e = new Vector2(b.x, b.z);
        Vector2 line = e - s;
        float t = line.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - s, line) / line.sqrMagnitude) : 0f;
        return Vector2.Distance(p, s + line * t);
    }

    private void OnDrawGizmos()
    {
        IReadOnlyList<Vector3> world = WorldCorners;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        for (int i = 0, j = world.Count - 1; i < world.Count; j = i++)
        {
            Gizmos.DrawLine(world[j] + Vector3.up * 0.1f, world[i] + Vector3.up * 0.1f);
        }
    }
}
