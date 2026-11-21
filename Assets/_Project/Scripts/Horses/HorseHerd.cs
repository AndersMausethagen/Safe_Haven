using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Wild horses round the Haven. As an in-game hour turns, each kind that roams (HorseSettings: the wild horse, and special
/// kinds such as the bog's toxic horse) may come, while there are fewer of it than its most: the wild horse somewhere in the
/// ring of land round the Haven's heart, the toxic horse in the bog and only at its hours (HorseDefinition › In the World),
/// always out of the player's sight. And every Haven is promised a first horse: with no horse of the player's by
/// HorseSettings › First Horse By Day, a calm one comes to graze near the yard, a resident says so, and the journal opens
/// its page on horses. Wild horses aren't saved; one not caught is simply gone after a load, and another may come. Added by
/// itself to the Haven scene; not in a friend's Haven (theirs to catch).
/// </summary>
public sealed class HorseHerd : MonoBehaviour
{
    private const string HavenScene = "SafeHaven";
    private const string FirstHorseFlag = "horse:first";
    private const string FirstHorseToldFlag = "horse:first:told";
    private Horse promised;
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

    // Raises a horse's story event for the player once a game (A Horse of Your Own starts on Wild Horse Met).
    private static void Tell(GameEventDefinition happened)
    {
        Player player = Player.Current;
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : null;
        if (happened != null && player != null && log != null && !HavenVisit.IsVisiting && !log.HasHappened(happened))
        {
            GameEvents.Raise(new GameEvent(GameEventKind.Custom, happened, 1, player.gameObject));
        }
    }

    // The first wild horse near the player at home starts the Stable's tale (HorseSettings › Wild Horse Met), once a game.
    private static void NoteWildHorseMet()
    {
        GameEventDefinition met = HorseSettings.Current.WildHorseMet;
        Player player = Player.Current;
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : null;
        if (met == null || player == null || log == null || HavenVisit.IsVisiting || log.HasHappened(met))
        {
            return;
        }

        float within = HorseSettings.Current.MetWithin;
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsWild && (horse.transform.position - player.transform.position).sqrMagnitude <= within * within)
            {
                GameEvents.Raise(new GameEvent(GameEventKind.Custom, met, 1, player.gameObject));
                return;
            }
        }
    }

    // Every wild horse that comes near the player is marked on the map from then on (Horse.Sighted), as the told-of first one is.
    private static void MarkSighted()
    {
        Player player = Player.Current;
        if (player == null)
        {
            return;
        }

        float within = HorseSettings.Current.MetWithin;
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsWild && !horse.Sighted && (horse.transform.position - player.transform.position).sqrMagnitude <= within * within)
            {
                horse.Sighted = true;
            }
        }
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + 2f;
        NoteWildHorseMet();
        MarkSighted();
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
        if (first || HavenVisit.IsVisiting)
        {
            return;
        }

        if (PromiseFirstHorse())
        {
            return;
        }

        foreach (HorseDefinition kind in HorseSettings.Current.AllKinds())
        {
            if (kind.Prefab != null && kind.IsAbroadAt(clock.Hour) && WildCount(kind) < kind.MostWild && random.NextDouble() < kind.SpawnChancePerHour)
            {
                SpawnWild(kind);
            }
        }
    }

    private static int WildCount(HorseDefinition kind)
    {
        int count = 0;
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsAlive && !horse.IsTamed && horse.Definition == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasHorse()
    {
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsAlive && horse.IsTamed)
            {
                return true;
            }
        }

        return false;
    }

    // With no horse by the promised day, a calm one comes near the yard (once a game).
    private bool PromiseFirstHorse()
    {
        HorseSettings settings = HorseSettings.Current;
        JournalRecord journal = GameSession.Progress.Journal;
        if (journal.HasTried(FirstHorseFlag))
        {
            return false;
        }

        if (HasHorse())
        {
            journal.NoteTried(FirstHorseFlag);
            return false;
        }

        if (PlantedTrees.Today < settings.FirstHorseByDay)
        {
            return false;
        }

        // The calm horse brought this visit is still about, waiting to be tamed.
        if (promised != null && promised.IsAlive && !promised.IsTamed)
        {
            return false;
        }

        Horse horse = SpawnInRing(settings.WildHorse, settings.FirstHorseRing, 25f);
        if (horse == null)
        {
            return false;
        }

        HorseStats stats = horse.Stats;
        stats.temperament = HorseTemperament.Calm;
        horse.Give(stats);
        // The promise is kept once a horse is tamed (HasHorse above), not when this one comes: a wild horse isn't saved, so
        // one left untamed at a raid or a reload must come again on the next visit.
        promised = horse;
        // Told of, it is on the map (the user's playtest, 2026-11-21: "When the notification about a nearby horse comes, it should
        // mark it on the map").
        horse.Sighted = true;
        if (!journal.HasTried(FirstHorseToldFlag))
        {
            journal.NoteTried(FirstHorseToldFlag);
            GameMessages.Post($"{settings.FirstHorseTeller}: \"{settings.FirstHorseLine}\"", MessageKind.Info);
            if (!string.IsNullOrWhiteSpace(settings.FirstHorseHowTo))
            {
                GameMessages.Post(settings.FirstHorseHowTo, MessageKind.Info);
            }

            JournalNotices.Tried(ProficiencyTraining.Horsemanship);
            // Its tale starts now, not only once the horse is near: getting the lasso and the barley is its first steps.
            Tell(settings.WildHorseMet);
        }

        return true;
    }

    /// <summary>Brings a wild horse of a kind to its home (the ring round the Haven, or the bog), out of sight. Null when no ground was found.</summary>
    public Horse SpawnWild(HorseDefinition kind)
    {
        return kind.Home == HorseHome.Bog ? SpawnInBog(kind) : SpawnInRing(kind, kind.SpawnRing, NotNearer);
    }

    /// <summary>The common wild horse in its ring (the cheat menu).</summary>
    public Horse SpawnWild() => SpawnWild(HorseSettings.Current.WildHorse);

    private Horse SpawnInRing(HorseDefinition kind, Vector2 ring, float notNearer)
    {
        Vector3 centre = HavenHeart();
        Vector3 player = Player.Current != null ? Player.Current.transform.position : centre;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(ring.x, ring.y, (float)random.NextDouble());
            Vector3 point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 6f, NavMesh.AllAreas) || (hit.position - player).sqrMagnitude < notNearer * notNearer)
            {
                continue;
            }

            return SpawnAt(kind, hit.position);
        }

        return null;
    }

    private Horse SpawnInBog(HorseDefinition kind)
    {
        Vector3 centre = HavenHeart() + kind.HomeOffset;
        Vector3 player = Player.Current != null ? Player.Current.transform.position : centre;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 step = new Vector2((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f) * kind.HomeRadius * 0.8f;
            Vector3 point = centre + new Vector3(step.x, 0f, step.y);
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 8f, NavMesh.AllAreas) || (hit.position - player).sqrMagnitude < NotNearer * NotNearer)
            {
                continue;
            }

            return SpawnAt(kind, hit.position);
        }

        return null;
    }

    private static Vector3 HavenHeart() => HomePortal.Current != null ? HomePortal.Current.transform.position : Vector3.zero;

    /// <summary>A wild horse here, now (the cheat menu, tests): the common one.</summary>
    public static Horse SpawnAt(Vector3 position) => SpawnAt(HorseSettings.Current.WildHorse, position);

    /// <summary>A wild horse of a kind here, now.</summary>
    public static Horse SpawnAt(HorseDefinition kind, Vector3 position)
    {
        if (kind == null || kind.Prefab == null)
        {
            return null;
        }

        // A plain copy: it has no save id, so a wild horse isn't kept. Taming makes a saved one (HorseTaming).
        Horse horse = Instantiate(kind.Prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        horse.name = kind.DisplayName;
        return horse;
    }
}
