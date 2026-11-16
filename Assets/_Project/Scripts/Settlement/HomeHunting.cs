using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Game beyond the ward by day: a few creatures keep to the forest outside the light, so hide, meat and bone can be had before
/// the first raid, for whoever walks out and hunts them (Valheim's deer). They are the world's own game (deer, stag, wild boar:
/// neutral and timid, MonsterAI.Timid), so they run rather than fight and are hunted by sneaking or with a bow. They never step into light (VisibilitySource.IsLit), so
/// the clearing stays safe, and they carry no Cursed Essence, which only raids give (GDD §13.5). They leave at nightfall and during
/// a breach, when the woods belong to worse things. Lives in SafeHaven, beside the night prowlers.
/// </summary>
[DisallowMultipleComponent]
public sealed class HomeHunting : MonoBehaviour
{
    private const float CheckInterval = 2f;
    private const int SpawnAttempts = 12;

    [Tooltip("What can be hunted. One is picked at random for each spawn.")]
    [SerializeField] private List<GameObject> game = new List<GameObject>();
    [SerializeField, Min(0)] private int maxAlive = 3;
    [Tooltip("Seconds between new animals while fewer than Max Alive roam.")]
    [SerializeField, Min(1f)] private float spawnInterval = 90f;
    [Tooltip("How far from the player they appear (metres).")]
    [SerializeField] private Vector2 spawnDistance = new Vector2(30f, 48f);
    [Tooltip("They appear at least this far outside any light (the ward included).")]
    [SerializeField, Min(0f)] private float lightMargin = 6f;

    private readonly List<MonsterAI> alive = new List<MonsterAI>();
    private DayNightCycle cycle;
    private float nextCheck;
    private float nextSpawnAt;

    private bool IsHuntingTime => Player.Current != null && (cycle == null || !cycle.IsNight)
        && (WardBreachDirector.Current == null || !WardBreachDirector.Current.IsActive);

    private void Start()
    {
        cycle = FindAnyObjectByType<DayNightCycle>();
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + CheckInterval;
        alive.RemoveAll(ai => ai == null || ai.CurrentState == MonsterAI.State.Dead);
        if (!IsHuntingTime)
        {
            foreach (MonsterAI ai in alive)
            {
                Destroy(ai.gameObject);
            }

            alive.Clear();
            return;
        }

        if (alive.Count < maxAlive + GameSession.Here.UpgradeBonus(level => level.extraGame) && Time.time >= nextSpawnAt && game.Count > 0)
        {
            nextSpawnAt = Time.time + spawnInterval;
            TrySpawn();
        }
    }

    private void TrySpawn()
    {
        Vector3 player = Player.Current.transform.position;
        for (int attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            float distance = Random.Range(spawnDistance.x, Mathf.Max(spawnDistance.x, spawnDistance.y));
            Vector3 wanted = player + new Vector3(direction.x, 0f, direction.y) * distance;
            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 4f, NavMesh.AllAreas) || VisibilitySource.IsLit(hit.position, lightMargin))
            {
                continue;
            }

            GameObject prefab = game[Random.Range(0, game.Count)];
            if (prefab == null)
            {
                return;
            }

            GameObject animal = Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            foreach (LootOnDeath loot in animal.GetComponentsInChildren<LootOnDeath>(true))
            {
                loot.WithholdsEssence = true;
            }

            if (animal.TryGetComponent(out MonsterAI ai))
            {
                ai.AvoidArea = position => VisibilitySource.IsLit(position);
                alive.Add(ai);
            }

            return;
        }
    }
}
