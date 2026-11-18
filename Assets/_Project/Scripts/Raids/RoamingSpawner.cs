using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Adds roaming creatures around the player while a raid runs, on top of the seeded camps (the raid's Roaming Spawns system, RoamingSpawnsSystem).
/// Each appears out of sight, between the near and far distance from the player, out of the water, away from other creatures and
/// only where it can walk to the player, then wanders to a spot near them and looks around. How many may be out at once
/// grows with the danger stage and with extraction alarms; a raid has a total budget so roamers can't be farmed.
/// Roamers that lose the player (idle and far away, out of sight) are removed and refunded to the budget, so the
/// pressure follows the player instead of piling up where they no longer are. Pauses while the raid has as many creatures
/// alive as the global rules allow. Installed by RoamingSpawnsSystem.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoamingSpawner : MonoBehaviour
{
    private const float ThinkInterval = 1f;
    private const int PlacementAttempts = 12;
    private const float NavMeshSnap = 4f;

    private readonly List<GameObject> roots = new List<GameObject>();
    private readonly List<MonsterAI> members = new List<MonsterAI>();
    private readonly Dictionary<GameObject, float> nextHunt = new Dictionary<GameObject, float>();
    private NavMeshPath path;
    private System.Random random;
    private RaidDirector director;
    private Transform container;
    private float nextThink;
    private float nextSpawn = -1f;
    private int spent;

    /// <summary>Roaming creatures still alive.</summary>
    public int AliveCount { get; private set; }

    /// <summary>Roaming creatures spawned this raid, minus those cleaned up far from the player.</summary>
    public int Spent => spent;

    /// <summary>Extra roamers allowed out at once on top of the settings' cap (raised by Forest Annoyance).</summary>
    public int ExtraCap { get; set; }

    /// <summary>Multiplies the time between spawns; below 1 spawns faster (lowered by Forest Annoyance).</summary>
    public float IntervalScale { get; set; } = 1f;

    private RoamingSpawnSettings Settings { get; set; }

    /// <summary>The settings to spawn by (from the raid's Roaming Spawns system).</summary>
    public void Configure(RoamingSpawnSettings settings) => Settings = settings;

    private void Awake()
    {
        director = GetComponent<RaidDirector>();
        // NavMeshPath wraps a native object, so it's created here on the main thread rather than in a field initializer.
        path = new NavMeshPath();
        random = new System.Random();
    }

    private void Update()
    {
        RoamingSpawnSettings settings = Settings;
        // Co-op: following the host, their game sends the creatures.
        if (settings == null || !settings.Enabled || !director.IsRunning || director.Player == null || Time.time < nextThink || CreatureSource.Remote != null)
        {
            return;
        }

        nextThink = Time.time + ThinkInterval;
        Vector3 player = director.Player.transform.position;
        Tidy(settings, player);

        if (director.Session.Elapsed < settings.FirstDelay)
        {
            return;
        }

        if (nextSpawn < 0f)
        {
            nextSpawn = Time.time;
        }

        int cap = settings.AliveCap(director.StageIndex, director.Creatures.AlarmsRaised, director.Session.Definition.SessionEnemyDensity) + ExtraCap;
        if (Time.time >= nextSpawn && AliveCount < cap && spent < settings.TotalLimit && !AtGlobalLimit())
        {
            // A failed placement retries on the next think rather than waiting a whole interval.
            if (TrySpawn(settings, player, null))
            {
                nextSpawn = Time.time + settings.Interval * Mathf.Max(0.1f, IntervalScale);
            }
        }
    }

    /// <summary>Sends every roaming group that isn't fighting straight to the player.</summary>
    public void HuntNow()
    {
        RoamingSpawnSettings settings = Settings;
        if (settings == null || director == null || director.Player == null)
        {
            return;
        }

        Vector3 player = director.Player.transform.position;
        foreach (GameObject root in roots)
        {
            if (root != null)
            {
                Hunt(root, player, settings);
            }
        }
    }

    /// <summary>
    /// Spawns up to <paramref name="groups"/> groups at once around the player, ignoring the alive cap and the raid's budget
    /// (but not the global creature limit), all hunting the player. Uses the given table, or the roaming table when null.
    /// Returns how many groups appeared (on a co-op friend's game, how many were asked of the host's game, which makes them).
    /// </summary>
    public int SpawnWave(int groups, SpawnTable table)
    {
        RoamingSpawnSettings settings = Settings;
        if (settings == null || director == null || director.Player == null)
        {
            return 0;
        }

        Vector3 player = director.Player.transform.position;
        int spawned = 0;
        for (int i = 0; i < groups && !AtGlobalLimit(); i++)
        {
            if (TrySpawn(settings, player, table))
            {
                spawned++;
            }
        }
        return spawned;
    }

    // The global rules can cap how many creatures a raid has alive at once (0 = no cap).
    private static bool AtGlobalLimit()
    {
        int limit = RaidGlobalSettings.Current.MaxCreaturesAlive;
        if (limit <= 0)
        {
            return false;
        }

        int alive = 0;
        foreach (MonsterAI ai in MonsterAI.All)
        {
            if (ai != null && ai.CurrentState != MonsterAI.State.Dead && ++alive >= limit)
            {
                return true;
            }
        }

        return false;
    }

    // Counts the living, sends idle roamers hunting again, and removes those that lost the player.
    private void Tidy(RoamingSpawnSettings settings, Vector3 player)
    {
        AliveCount = 0;
        for (int i = roots.Count - 1; i >= 0; i--)
        {
            GameObject root = roots[i];
            members.Clear();
            if (root != null)
            {
                root.GetComponentsInChildren(members);
            }

            int alive = 0;
            Vector3 position = Vector3.zero;
            bool busy = false;
            bool idle = false;
            foreach (MonsterAI ai in members)
            {
                if (ai.CurrentState == MonsterAI.State.Dead)
                {
                    continue;
                }

                alive++;
                // A pack's root stays where it spawned; its members are where the group really is.
                position += ai.transform.position;
                busy |= ai.CurrentState == MonsterAI.State.Chasing || ai.CurrentState == MonsterAI.State.Attacking || ai.CurrentState == MonsterAI.State.Staggered;
                // A thief running with what it took is busy too (it flees as Idle): tidied away, the stack went with it, untold.
                busy |= ai.IsFleeing || !ai.Loot.IsEmpty;
                idle |= ai.CurrentState == MonsterAI.State.Idle;
            }

            if (alive == 0)
            {
                nextHunt.Remove(root);
                roots.RemoveAt(i);
                continue;
            }

            position /= alive;
            if (!busy && Flat(position - player) > settings.DespawnDistance && !RaidSpots.IsVisible(position, root.transform))
            {
                spent = Mathf.Max(0, spent - alive);
                nextHunt.Remove(root);
                roots.RemoveAt(i);
                Destroy(root);
                continue;
            }

            AliveCount += alive;
            // Idle roamers look around where they stopped for a while before heading for the player again.
            if (!busy && idle && Flat(position - player) > settings.HuntSpread && Time.time >= nextHunt[root])
            {
                Hunt(root, player, settings);
            }
        }
    }

    private bool TrySpawn(RoamingSpawnSettings settings, Vector3 player, SpawnTable table)
    {
        if (!NavMesh.SamplePosition(player, out NavMeshHit playerHit, NavMeshSnap, NavMesh.AllAreas))
        {
            return false;
        }

        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(settings.MinDistance, settings.MaxDistance, (float)random.NextDouble());
            Vector3 candidate = player + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (!NavMesh.SamplePosition(RaidSpots.Grounded(candidate), out NavMeshHit hit, NavMeshSnap, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 point = hit.position;
            if (Flat(point - player) < settings.MinDistance || !RaidSpots.IsDryEnough(point) || RaidSpots.IsVisible(point) || NearCreature(point, settings.CampClearance)
                || !NavMesh.CalculatePath(point, playerHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            SpawnTable source = table != null ? table : settings.Table;
            GameObject prefab = source != null ? source.PickAt(random, GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Tier : 0) : null;
            if (prefab == null)
            {
                return false;
            }

            Vector3 facing = player - point;
            facing.y = 0f;
            Quaternion turn = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            // Co-op, on the host's run (only the forest's wrath gets here: Update makes none): the host's game makes them and
            // sends them after this player, as an event's creatures are. Made here, they were removed as they woke.
            if (CreatureSource.Remote != null)
            {
                CreatureSource.Remote.Spawn(prefab, point, turn.eulerAngles.y, true);
                return true;
            }

            GameObject root = Instantiate(prefab, point, turn, Container());
            director.Creatures.Prepare(root, false);
            roots.Add(root);

            int count = 0;
            foreach (MonsterAI ai in root.GetComponentsInChildren<MonsterAI>())
            {
                count++;
            }
            spent += count;
            AliveCount += count;
            Hunt(root, player, settings);
            return true;
        }

        return false;
    }

    // Sends every member to one spot near the player, each a step apart so a pack doesn't bunch onto one point.
    private void Hunt(GameObject root, Vector3 player, RoamingSpawnSettings settings)
    {
        Vector2 offset = new Vector2((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f) * settings.HuntSpread;
        Vector3 destination = player + new Vector3(offset.x, 0f, offset.y);
        MonsterAI lead = root.GetComponentInChildren<MonsterAI>();
        Vector3 from = lead != null ? lead.transform.position : root.transform.position;
        bool found = NavMesh.SamplePosition(RaidSpots.Grounded(destination), out NavMeshHit hit, NavMeshSnap * 2f, NavMesh.AllAreas);
        // The ground probe can land on a rock top whose NavMesh is cut off from the rest; fall back to the player's spot.
        if (!found || !NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            found = NavMesh.SamplePosition(player, out hit, NavMeshSnap, NavMesh.AllAreas);
        }
        if (found)
        {
            destination = hit.position;
        }

        nextHunt[root] = Time.time + settings.Interval;
        foreach (MonsterAI ai in root.GetComponentsInChildren<MonsterAI>())
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            ai.Roam(destination + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.5f);
        }
    }

    private static bool NearCreature(Vector3 point, float clearance)
    {
        float limit = clearance * clearance;
        foreach (MonsterAI ai in MonsterAI.All)
        {
            if (ai != null && ai.CurrentState != MonsterAI.State.Dead && (ai.transform.position - point).sqrMagnitude < limit)
            {
                return true;
            }
        }

        return false;
    }

    private static float Flat(Vector3 offset)
    {
        offset.y = 0f;
        return offset.magnitude;
    }

    private Transform Container()
    {
        if (container == null)
        {
            container = new GameObject("Roaming Spawns").transform;
        }

        return container;
    }
}
