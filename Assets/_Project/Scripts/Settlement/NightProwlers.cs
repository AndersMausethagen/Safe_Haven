using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Night at home (GDD §6.3): after the first ward breach has been won, creatures come out of the forest at night and
/// prowl the dark outside the ward, more of them and worse (NightSettings) with every breach beaten back, stronger for
/// the dark, and carrying the night's Cursed Essence. They never step into light (the ward, campfires, torches:
/// VisibilitySource.IsLit), so they can't see or follow anyone standing in it; whoever walks out into the dark is fair
/// game. Every so often an idle prowler closes in: on the player in the dark, or to the edge of the light nearest it
/// when the player stands in light. They appear out of sight, away from the player, and slink off at dawn. They are not saved. None come during a
/// breach. Lives in SafeHaven, beside the sky rig.
/// </summary>
[DisallowMultipleComponent]
public sealed class NightProwlers : MonoBehaviour
{
    [Tooltip("What prowls (weak creatures). One is picked at random for each spawn.")]
    [SerializeField] private List<GameObject> prowlers = new List<GameObject>();
    [SerializeField, Min(0)] private int maxAlive = 3;
    [Tooltip("Seconds between spawns while fewer than Max Alive prowl.")]
    [SerializeField, Min(1f)] private float spawnInterval = 25f;
    [Tooltip("How far from the player they appear (metres).")]
    [SerializeField] private Vector2 spawnDistance = new Vector2(30f, 48f);
    [Tooltip("They appear at least this far outside any light (the ward included).")]
    [SerializeField, Min(0f)] private float lightMargin = 6f;
    [Tooltip("Off: they come every night from the start (testing).")]
    [SerializeField] private bool requireBreachWon = true;
    [Tooltip("Seconds a prowler has at dawn to walk off before it is removed.")]
    [SerializeField, Min(1f)] private float dawnSeconds = 20f;
    [Tooltip("Seconds between an idle prowler moving in on the player again (a random pick in this range).")]
    [SerializeField] private Vector2 stalkSeconds = new Vector2(8f, 16f);

    private const float CheckInterval = 1f;
    private readonly List<MonsterAI> alive = new List<MonsterAI>();
    private readonly Dictionary<MonsterAI, float> leavingUntil = new Dictionary<MonsterAI, float>();
    private readonly Dictionary<MonsterAI, float> nextStalkAt = new Dictionary<MonsterAI, float>();
    private DayNightCycle cycle;
    private float nextCheck;
    private float nextSpawnAt;
    private bool announced;

    public IReadOnlyList<MonsterAI> Alive => alive;

    /// <summary>True when prowlers may come now: night, past the first breach, no breach running.</summary>
    public bool IsProwlingTime
    {
        get
        {
            if (cycle == null || !cycle.IsNight || Player.Current == null)
            {
                return false;
            }

            if (requireBreachWon && GameSession.Settlement.BreachesWon == 0)
            {
                return false;
            }

            return WardBreachDirector.Current == null || !WardBreachDirector.Current.IsActive;
        }
    }

    private void Start()
    {
        cycle = FindAnyObjectByType<DayNightCycle>();
        nextSpawnAt = Time.time + spawnInterval * 0.5f;
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + CheckInterval;
        alive.RemoveAll(ai => ai == null || ai.CurrentState == MonsterAI.State.Dead);
        if (nextStalkAt.Count > alive.Count * 2)
        {
            nextStalkAt.Clear();
        }

        if (!IsProwlingTime)
        {
            announced = false;
            SendHome();
            return;
        }

        NightSettings settings = NightSettings.Current;
        if (!announced)
        {
            announced = true;
            GameMessages.Post(settings.NightfallText, MessageKind.Warning);
        }

        leavingUntil.Clear();
        int most = settings.ProwlersAlive(maxAlive, GameSession.Settlement.BreachesWon);
        if (alive.Count < most && Time.time >= nextSpawnAt && prowlers.Count > 0)
        {
            nextSpawnAt = Time.time + spawnInterval * settings.ProwlerSpawnScale;
            TrySpawn();
        }

        Stalk();
    }

    // Idle prowlers keep closing in: on the player if they stand in the dark (their senses take it from there), or to
    // the edge of the light nearest them if they don't, where they wait.
    private void Stalk()
    {
        Vector3 player = Player.Current.transform.position;
        foreach (MonsterAI ai in alive)
        {
            if (ai.CurrentState != MonsterAI.State.Idle || !ai.enabled)
            {
                continue;
            }

            if (nextStalkAt.TryGetValue(ai, out float at) && Time.time < at)
            {
                continue;
            }

            nextStalkAt[ai] = Time.time + Random.Range(stalkSeconds.x, Mathf.Max(stalkSeconds.x, stalkSeconds.y));
            Vector3 destination = EdgeOfTheDark(player, ai.transform.position) + Flat(Random.insideUnitCircle) * 3f;
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, NavMesh.AllAreas) && !VisibilitySource.IsLit(hit.position))
            {
                ai.Roam(hit.position);
            }
        }
    }

    // The player's position if it is dark there; otherwise the first dark ground on the way from the player to `from`.
    private static Vector3 EdgeOfTheDark(Vector3 player, Vector3 from)
    {
        if (!VisibilitySource.IsLit(player))
        {
            return player;
        }

        Vector3 away = from - player;
        away.y = 0f;
        float length = away.magnitude;
        Vector3 step = length > 0.01f ? away / length : Vector3.forward;
        for (float d = 1f; d < Mathf.Max(length, 40f); d += 1f)
        {
            Vector3 point = player + step * d;
            if (!VisibilitySource.IsLit(point, 1.5f))
            {
                return point;
            }
        }

        return from;
    }

    private void TrySpawn()
    {
        Vector3 player = Player.Current.transform.position;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            float distance = Random.Range(spawnDistance.x, Mathf.Max(spawnDistance.x, spawnDistance.y));
            Vector3 wanted = player + new Vector3(direction.x, 0f, direction.y) * distance;
            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 4f, NavMesh.AllAreas)
                || VisibilitySource.IsLit(hit.position, lightMargin) || IsOnScreen(hit.position))
            {
                continue;
            }

            GameObject deep = NightSettings.Current.DeepProwler(GameSession.Settlement.BreachesWon, Random.value);
            GameObject prefab = deep != null ? deep : prowlers[Random.Range(0, prowlers.Count)];
            if (prefab == null)
            {
                return;
            }

            GameObject prowler = Instantiate(prefab, hit.position, Quaternion.LookRotation(player - hit.position));
            NightSettings.Current.Strengthen(prowler, this);
            if (prowler.TryGetComponent(out MonsterAI ai))
            {
                ai.AvoidArea = position => VisibilitySource.IsLit(position);
                // Drawn toward the edge of the dark near the player; their senses do the rest.
                ai.Roam(Vector3.Lerp(hit.position, player, 0.5f));
                alive.Add(ai);
            }

            return;
        }
    }

    // At dawn they walk off into the forest and are removed once out of sight or out of time.
    private void SendHome()
    {
        Vector3 player = Player.Current != null ? Player.Current.transform.position : transform.position;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            MonsterAI ai = alive[i];
            if (!leavingUntil.TryGetValue(ai, out float until))
            {
                leavingUntil[ai] = Time.time + dawnSeconds;
                Vector3 away = ai.transform.position - player;
                away.y = 0f;
                Vector3 destination = ai.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 40f;
                if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    ai.Roam(hit.position);
                }

                continue;
            }

            if (Time.time >= until || !IsOnScreen(ai.transform.position))
            {
                leavingUntil.Remove(ai);
                nextStalkAt.Remove(ai);
                alive.RemoveAt(i);
                Destroy(ai.gameObject);
            }
        }
    }

    private static Vector3 Flat(Vector2 v) => new Vector3(v.x, 0f, v.y);

    private static bool IsOnScreen(Vector3 position)
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return false;
        }

        Vector3 viewport = view.WorldToViewportPoint(position + Vector3.up);
        return viewport.z > 0f && viewport.x > -0.05f && viewport.x < 1.05f && viewport.y > -0.05f && viewport.y < 1.05f;
    }
}
