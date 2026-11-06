using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks in the snow that lead somewhere: in winter, a trail of prints winds out from each creature lair and wild herd
/// towards home, so a player who comes across one can follow it to what made it (or keep well away). Trails are laid
/// only round the player (the footprint field keeps prints within about 12 m of them), a few prints a frame, into the
/// terrain's footprint field (where other walkers' prints go), and laid again every few seconds, so the trail shows on
/// ahead as the player follows it. Each trail's way is fixed by where its den is, so it is the same each visit.
/// <para>Where it belongs: once in the Haven scene (set up by AgentScripts/BuildWinter.cs).</para>
/// </summary>
public sealed class WinterTracks : MonoBehaviour
{
    [Tooltip("Snow on the ground (0-1) from which trails show.")]
    [SerializeField, Range(0f, 1f)] private float fromSnow = 0.3f;
    [Tooltip("Metres from the player within which a den's trail is laid.")]
    [SerializeField, Min(10f)] private float layWithin = 60f;
    [Tooltip("How long a trail runs out from its den (metres).")]
    [SerializeField, Min(5f)] private float trailLength = 45f;
    [Tooltip("Metres between prints along a trail.")]
    [SerializeField, Min(0.3f)] private float stride = 0.8f;
    [Tooltip("Seconds between laying the near part of a trail again: as the player follows it, more of it shows.")]
    [SerializeField, Min(2f)] private float relaySeconds = 8f;
    [Tooltip("Only prints this close to the player are laid (the footprint field only keeps prints round the player).")]
    [SerializeField, Min(4f)] private float printsWithin = 13f;
    [Tooltip("Prints stamped per frame at most.")]
    [SerializeField, Min(1)] private int printsPerFrame = 12;

    private readonly List<Vector3> dens = new List<Vector3>();
    private readonly Dictionary<int, float> laidAt = new Dictionary<int, float>();
    private readonly Queue<(Vector3 at, Vector3 forward)> pending = new Queue<(Vector3, Vector3)>();
    private float nextScan;

    private float nextDenScan;

    // Lairs and herds, found again now and then (a generated raid map makes its lairs after the scene starts).
    private void FindDens()
    {
        dens.Clear();
        foreach (MobSpawnZone zone in FindObjectsByType<MobSpawnZone>())
        {
            dens.Add(zone.transform.position);
        }

        foreach (WildlifeHerd herd in FindObjectsByType<WildlifeHerd>())
        {
            dens.Add(herd.transform.position);
        }
    }

    private void Update()
    {
        for (int i = 0; i < printsPerFrame && pending.Count > 0; i++)
        {
            (Vector3 at, Vector3 forward) = pending.Dequeue();
            PlayerTerrainFootprints.TryStamp(at, forward, false);
        }

        if (Time.time < nextScan)
        {
            return;
        }

        nextScan = Time.time + 2f;
        if (Time.time >= nextDenScan)
        {
            nextDenScan = Time.time + 20f;
            FindDens();
        }

        WeatherSystem weather = WeatherSystem.Active;
        Player player = Player.Current;
        if (weather == null || player == null || weather.Snow < fromSnow)
        {
            return;
        }

        Vector3 home = HomePortal.Current != null ? HomePortal.Current.transform.position : Vector3.zero;
        for (int i = 0; i < dens.Count; i++)
        {
            Vector3 den = dens[i];
            if ((den - player.transform.position).sqrMagnitude > layWithin * layWithin
                || (laidAt.TryGetValue(i, out float last) && Time.time - last < relaySeconds))
            {
                continue;
            }

            laidAt[i] = Time.time;
            Lay(Trail(den, home, trailLength, stride), player.transform.position, printsWithin, pending);
        }
    }

    /// <summary>
    /// The way a trail goes from a den: out towards home (within 50 degrees either side, fixed by the den's position),
    /// wandering gently, one print every stride, alternating feet.
    /// </summary>
    public static List<(Vector3 at, Vector3 forward)> Trail(Vector3 den, Vector3 home, float length, float stride)
    {
        var prints = new List<(Vector3, Vector3)>();
        Vector3 towards = home - den;
        towards.y = 0f;
        if (towards.sqrMagnitude < 0.01f)
        {
            towards = Vector3.forward;
        }

        float seed = Mathf.Abs(Mathf.Sin(den.x * 12.9898f + den.z * 78.233f) * 43758.5453f) % 1f;
        float heading = Mathf.Atan2(towards.x, towards.z) + (seed - 0.5f) * Mathf.Deg2Rad * 100f;
        Vector3 at = den;
        bool left = false;
        for (float walked = 0f; walked < length; walked += stride)
        {
            heading += Mathf.Sin(walked * 0.21f + seed * 40f) * 0.06f;
            var forward = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
            at += forward * stride;
            left = !left;
            prints.Add((at + Vector3.Cross(Vector3.up, forward) * (left ? -0.13f : 0.13f), forward));
        }

        return prints;
    }

    private static void Lay(List<(Vector3 at, Vector3 forward)> trail, Vector3 near, float within, Queue<(Vector3, Vector3)> into)
    {
        Terrain terrain = Terrain.activeTerrain;
        foreach ((Vector3 at, Vector3 forward) in trail)
        {
            Vector3 point = at;
            if ((new Vector2(point.x - near.x, point.z - near.z)).sqrMagnitude > within * within)
            {
                continue;
            }

            if (terrain != null)
            {
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            }

            into.Enqueue((point, forward));
        }
    }
}
