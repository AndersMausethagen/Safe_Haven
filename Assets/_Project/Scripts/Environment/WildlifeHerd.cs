using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Where a few timid animals live (a glade, a meadow): they are there only while the player is near enough to come upon
/// them, made when the player draws close and cleared when they leave, so the world's animals cost nothing out of sight.
/// Once they have fled they stay away for a while. The hours they keep (deer at dawn and dusk, foxes by night) are a curve.
/// <para>Where it belongs: placed by the Haven world generator in its glades and meadows; also by hand.</para>
/// </summary>
public sealed class WildlifeHerd : MonoBehaviour
{
    [SerializeField] private List<Wildlife> kinds = new List<Wildlife>();
    [SerializeField] private Vector2Int count = new Vector2Int(1, 3);
    [Tooltip("Metres round this point they are found in.")]
    [SerializeField, Min(1f)] private float radius = 10f;
    [Tooltip("How likely they are about at each hour (0-24 across, 0-1 up). Empty: always.")]
    [SerializeField] private AnimationCurve byHour = new AnimationCurve();
    [Tooltip("They appear when the player comes within this many metres, and go when the player is further than Gone Beyond.")]
    [SerializeField, Min(10f)] private float appearWithin = 55f;
    [SerializeField, Min(20f)] private float goneBeyond = 85f;
    [Tooltip("Seconds before animals come back after they fled or the hour was wrong.")]
    [SerializeField, Min(10f)] private float awaySeconds = 300f;

    private readonly List<Wildlife> living = new List<Wildlife>();
    private float nextCheck;
    private float awayUntil;

    public void Configure(IReadOnlyList<Wildlife> animals, Vector2Int howMany, float area, AnimationCurve hours)
    {
        kinds = new List<Wildlife>(animals);
        count = howMany;
        radius = area;
        byHour = hours ?? new AnimationCurve();
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + 1f + Random.value * 0.5f;
        Player player = Player.Current;
        if (player == null)
        {
            return;
        }

        float distance = Vector3.Distance(player.transform.position, transform.position);
        if (distance > goneBeyond)
        {
            Clear();
            return;
        }

        bool fled = living.Count > 0 && living.TrueForAll(animal => animal == null || animal.HasFled);
        if (fled)
        {
            Clear();
            awayUntil = Time.time + awaySeconds;
            return;
        }

        if (living.Count == 0 && distance < appearWithin && Time.time >= awayUntil && kinds.Count > 0)
        {
            // The hour decides once, as the player arrives: if they are not about now, they are not for a while.
            if (byHour != null && byHour.length > 0 && Random.value > byHour.Evaluate(AtmosphereConditions.Hour))
            {
                awayUntil = Time.time + awaySeconds * 0.5f;
                return;
            }

            Appear(player.transform.position);
        }
    }

    private void Appear(Vector3 player)
    {
        Wildlife kind = kinds[Random.Range(0, kinds.Count)];
        if (kind == null)
        {
            return;
        }

        int wanted = Random.Range(count.x, count.y + 1);
        for (int i = 0; i < wanted * 3 && living.Count < wanted; i++)
        {
            Vector2 around = Random.insideUnitCircle * radius;
            Vector3 at = transform.position + new Vector3(around.x, 0f, around.y);
            // Never right beside the player: they were there first.
            if ((at - player).sqrMagnitude < 18f * 18f || !NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                continue;
            }

            Wildlife animal = Instantiate(kind, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            animal.Settle(transform.position);
            living.Add(animal);
        }
    }

    private void Clear()
    {
        foreach (Wildlife animal in living)
        {
            if (animal != null)
            {
                Destroy(animal.gameObject);
            }
        }

        living.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.9f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
