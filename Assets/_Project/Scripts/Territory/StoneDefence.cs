using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Standing watch at a pushed region's boundary stone (Territory): what the forest sends comes out of the trees around it and
/// goes for the player. Beat them all while keeping near the stone and the push is broken (Territory.Defended); walk away from it
/// too long, fall, or run out of time and the watch is lost, and the push still comes. Only one watch at a time.
/// </summary>
public sealed class StoneDefence : MonoBehaviour
{
    private const int PlaceAttempts = 20;

    private readonly List<MonsterAI> attackers = new List<MonsterAI>();
    private BoundaryStone stone;
    private GameObject player;
    private float endsAt;
    private float awaySince = -1f;
    private int total;

    public static StoneDefence Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public int Left => attackers.Count;

    public static void Begin(BoundaryStone at, GameObject user)
    {
        if (Current != null || at == null || user == null || !Territory.CanStandWatch(out _))
        {
            return;
        }

        var watch = new GameObject("Stone Defence").AddComponent<StoneDefence>();
        Current = watch;
        watch.Run(at, user);
    }

    private void Run(BoundaryStone at, GameObject user)
    {
        TerritorySettings settings = TerritorySettings.Current;
        stone = at;
        player = user;
        endsAt = Time.time + settings.DefenceSeconds;
        TerritorySettings.RegionInfo info = settings.Info(stone.Region);
        int count = settings.DefenceCountFor(info != null ? info.danger : RegionDanger.Wary);
        IReadOnlyList<GameObject> kinds = settings.DefenceCreatures;
        IDamageable prey = user.GetComponent<IDamageable>();
        for (int i = 0; i < count && kinds.Count > 0; i++)
        {
            GameObject prefab = kinds[Random.Range(0, kinds.Count)];
            if (prefab == null || !TryPlace(stone.transform.position, settings.DefenceDistance, out Vector3 spot))
            {
                continue;
            }

            GameObject creature = Instantiate(prefab, spot, Quaternion.LookRotation(stone.transform.position - spot, Vector3.up));
            if (creature.TryGetComponent(out MonsterAI ai))
            {
                ai.KeepAwake = true;
                ai.Relentless = true;
                ai.PlayerOnly = true;
                if (prey != null)
                {
                    ai.Engage(prey);
                }

                attackers.Add(ai);
            }
        }

        total = attackers.Count;
        if (total == 0)
        {
            GameMessages.Post("Nothing came. The forest isn't ready to show itself here yet.", MessageKind.Warning);
            Destroy(gameObject);
            return;
        }

        GameMessages.Post($"They come for the stone: {total} of them. Hold it, and don't stray from it.", MessageKind.Warning);
    }

    private static bool TryPlace(Vector3 centre, Vector2 distance, out Vector3 at)
    {
        for (int attempt = 0; attempt < PlaceAttempts; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 wanted = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Random.Range(distance.x, distance.y);
            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                at = hit.position;
                return true;
            }
        }

        at = default;
        return false;
    }

    private void Update()
    {
        attackers.RemoveAll(ai => ai == null || ai.CurrentState == MonsterAI.State.Dead);
        if (stone == null || player == null)
        {
            End(false, "The watch is over.");
            return;
        }

        if (attackers.Count == 0)
        {
            End(true, null);
            return;
        }

        TerritorySettings settings = TerritorySettings.Current;
        bool fallen = player.TryGetComponent(out Damageable health) && !health.IsAlive;
        float distance = Vector3.Distance(player.transform.position, stone.transform.position);
        if (distance > settings.DefenceLeash)
        {
            if (awaySince < 0f)
            {
                awaySince = Time.time;
                GameMessages.Post("You are leaving the stone undefended. Go back to it!", MessageKind.Warning);
            }
        }
        else
        {
            awaySince = -1f;
        }

        if (fallen)
        {
            End(false, "You fell, and the stone was left to them. The push will still come.");
        }
        else if (awaySince >= 0f && Time.time - awaySince > settings.DefenceAway)
        {
            End(false, "The stone was left undefended. The push will still come.");
        }
        else if (Time.time > endsAt)
        {
            End(false, "Dawn of a sort: they melt back into the trees, unbeaten. The push will still come.");
        }
    }

    private void End(bool held, string words)
    {
        if (held && stone != null)
        {
            Territory.Defended(stone.Region);
        }
        else if (words != null)
        {
            GameMessages.Post(words, MessageKind.Warning);
        }

        // Whatever is left slinks off: they were sent for the stone, not to stay.
        foreach (MonsterAI ai in attackers)
        {
            if (ai != null)
            {
                Destroy(ai.gameObject, 2f);
            }
        }

        attackers.Clear();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }
}
