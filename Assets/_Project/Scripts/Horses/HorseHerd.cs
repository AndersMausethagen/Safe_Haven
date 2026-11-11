using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Wild horses round the Haven: rarely, as an in-game hour turns and while there are fewer than the definition's most, one
/// comes to graze somewhere in the ring of land round the Haven's heart (HorseDefinition › In the World), out of the
/// player's sight. They aren't saved; one not caught is simply gone after a load, and another may come. Added by itself to
/// the Haven scene; not in a friend's Haven (theirs to catch).
/// </summary>
public sealed class HorseHerd : MonoBehaviour
{
    private const string HavenScene = "SafeHaven";
    private const float NotNearer = 45f;

    private DayNightCycle clock;
    private int lastHour = -1;
    private float nextCheck;
    private readonly System.Random random = new System.Random();

    public static HorseHerd Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != HavenScene || Current != null || HorseSettings.Current.WildHorse == null || HorseSettings.Current.WildHorse.Prefab == null)
        {
            return;
        }

        var holder = new GameObject("Horse Herd");
        SceneManager.MoveGameObjectToScene(holder, scene);
        Current = holder.AddComponent<HorseHerd>();
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + 2f;
        if (clock == null)
        {
            clock = FindAnyObjectByType<DayNightCycle>();
            if (clock == null)
            {
                return;
            }
        }

        int hour = Mathf.FloorToInt(clock.Hour);
        if (hour == lastHour)
        {
            return;
        }

        bool first = lastHour < 0;
        lastHour = hour;
        HorseDefinition horse = HorseSettings.Current.WildHorse;
        if (first || HavenVisit.IsVisiting || WildCount() >= horse.MostWild || random.NextDouble() >= horse.SpawnChancePerHour)
        {
            return;
        }

        SpawnWild();
    }

    private static int WildCount()
    {
        int count = 0;
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsAlive && !horse.IsTamed)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Brings a wild horse to the ring round the Haven, out of sight. Null when no ground was found.</summary>
    public Horse SpawnWild()
    {
        HorseDefinition definition = HorseSettings.Current.WildHorse;
        Vector3 centre = HomePortal.Current != null ? HomePortal.Current.transform.position : Vector3.zero;
        Vector3 player = Player.Current != null ? Player.Current.transform.position : centre;
        Vector2 ring = definition.SpawnRing;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(ring.x, ring.y, (float)random.NextDouble());
            Vector3 point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 6f, NavMesh.AllAreas) || (hit.position - player).sqrMagnitude < NotNearer * NotNearer)
            {
                continue;
            }

            return SpawnAt(hit.position);
        }

        return null;
    }

    /// <summary>A wild horse here, now (the cheat menu, tests).</summary>
    public static Horse SpawnAt(Vector3 position)
    {
        HorseDefinition definition = HorseSettings.Current.WildHorse;
        if (definition == null || definition.Prefab == null)
        {
            return null;
        }

        // A plain copy: it has no save id, so a wild horse isn't kept. Taming makes a saved one (HorseTaming).
        Horse horse = Instantiate(definition.Prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        horse.name = definition.DisplayName;
        return horse;
    }
}
