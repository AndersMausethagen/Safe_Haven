using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Corruption from the Misty Woods (playtest 2026-11-05): at each nightfall there is a chance (CorruptionSettings, rare at
/// first and growing with the Haven's rank) that it creeps in. The player is warned, and a little later a band of creatures
/// comes out of the dark from one direction and goes for the buildings, turning on anyone close (HomeAssault, as a ward
/// breach does). They keep their loot. Survivors slink off at dawn. Unlike Night Prowlers they do walk into the light.
/// Never during a ward breach. Not saved: a night loaded mid-way is not rolled again. Lives in SafeHaven, beside
/// NightProwlers.
/// </summary>
[DisallowMultipleComponent]
public sealed class CorruptionNights : MonoBehaviour
{
    private const float CheckInterval = 1f;
    private const float DirectInterval = 2f;
    private const int PlaceAttempts = 12;

    private readonly List<MonsterAI> attackers = new List<MonsterAI>();
    private DayNightCycle cycle;
    private bool wasNight;
    private float comeAt = -1f;
    private float leaveAt = -1f;
    private float nextCheck;
    private float nextDirect;
    private bool fought;
    private float bearing;
    private int bandSize;
    private WeatherSystem weatherSystem;
    private WeatherDefinition weatherBefore;

    /// <summary>The corruption in the loaded scene, or null.</summary>
    public static CorruptionNights Current { get; private set; }

    /// <summary>True from the warning until the last of the band is gone.</summary>
    public bool IsActive => comeAt >= 0f || attackers.Count > 0;

    public IReadOnlyList<MonsterAI> Attackers => attackers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }

    private void Start()
    {
        cycle = FindAnyObjectByType<DayNightCycle>();
        // A game loaded at night doesn't roll that night again.
        wasNight = cycle != null && cycle.IsNight;
    }

    private void Update()
    {
        // A friend's Haven: its nights are theirs (co-op raids and fights come later).
        if (Time.time < nextCheck || HavenVisit.IsVisiting)
        {
            return;
        }

        nextCheck = Time.time + CheckInterval;
        bool night = cycle != null && cycle.IsNight;
        if (night && !wasNight)
        {
            Nightfall();
        }
        else if (!night && wasNight)
        {
            Dawn();
        }

        wasNight = night;
        if (comeAt >= 0f && Time.time >= comeAt)
        {
            comeAt = -1f;
            Spawn();
        }

        Watch();
    }

    /// <summary>Starts a corruption now, warning first (cheats and tests).</summary>
    public void Begin(float afterSeconds = -1f)
    {
        if (IsActive)
        {
            return;
        }

        CorruptionSettings settings = CorruptionSettings.Current;
        GameMessages.Post(settings.Warning, MessageKind.Warning);
        comeAt = Time.time + (afterSeconds >= 0f ? afterSeconds : settings.Delay);
        fought = false;
        // The night's direction is chosen now, so the warning comes from where the band will.
        bearing = Random.Range(0f, 360f);
        Vector3 edge = Centre() + Direction(bearing) * settings.Distance.x;
        AudioManager.Play(settings.WarningSound, edge);
        BringWeather(settings);
    }

    private Vector3 Centre() => HomePortal.Current != null ? HomePortal.Current.transform.position
        : Player.Current != null ? Player.Current.transform.position : transform.position;

    private static Vector3 Direction(float degrees) => new Vector3(Mathf.Sin(degrees * Mathf.Deg2Rad), 0f, Mathf.Cos(degrees * Mathf.Deg2Rad));

    // The corruption brings the game's own fog; the weather before it comes back when it is beaten or at dawn.
    private void BringWeather(CorruptionSettings settings)
    {
        WeatherSystem system = FindAnyObjectByType<WeatherSystem>();
        if (system == null || settings.Weather == null || system.Weather == settings.Weather)
        {
            return;
        }

        weatherSystem = system;
        weatherBefore = system.Weather;
        system.SetWeather(settings.Weather, settings.WeatherSeconds);
    }

    private void ReturnWeather()
    {
        if (weatherSystem != null && weatherBefore != null)
        {
            weatherSystem.SetWeather(weatherBefore, CorruptionSettings.Current.WeatherSeconds);
        }

        weatherSystem = null;
        weatherBefore = null;
    }

    private void Nightfall()
    {
        if (IsActive || (WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive))
        {
            return;
        }

        float chance = CorruptionSettings.Current.ChanceAt(GameSession.Settlement.HavenRank);
        if (chance > 0f && Random.value < chance)
        {
            Begin();
        }
    }

    // The band comes from one direction, out of sight in the dark beyond the clearing.
    private void Spawn()
    {
        CorruptionSettings settings = CorruptionSettings.Current;
        if (settings.Creatures.Count == 0)
        {
            return;
        }

        Vector3 centre = Centre();
        int count = settings.CountAt(GameSession.Settlement.HavenRank);
        float chaseLimit = WardBreachSettings.Current.ChaseLimit;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = settings.Creatures[Random.Range(0, settings.Creatures.Count)];
            if (prefab == null || !TryPlace(centre, bearing, settings, out Vector3 at))
            {
                continue;
            }

            GameObject creature = Instantiate(prefab, at, Quaternion.LookRotation(centre - at, Vector3.up));
            if (creature.TryGetComponent(out MonsterAI ai))
            {
                ai.Relentless = true;
                ai.KeepAwake = true;
                attackers.Add(ai);
                HomeAssault.Direct(ai, chaseLimit);
            }
        }

        fought = attackers.Count > 0;
        bandSize = attackers.Count;
        if (!fought)
        {
            Debug.LogWarning("Corruption night: no place found for its creatures.", this);
        }
    }

    private static bool TryPlace(Vector3 centre, float bearing, CorruptionSettings settings, out Vector3 at)
    {
        for (int attempt = 0; attempt < PlaceAttempts; attempt++)
        {
            float angle = (bearing + Random.Range(-settings.Spread, settings.Spread)) * Mathf.Deg2Rad;
            float distance = Random.Range(settings.Distance.x, settings.Distance.y);
            Vector3 wanted = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 8f, NavMesh.AllAreas) && !VisibilitySource.IsLit(hit.position, 4f))
            {
                at = hit.position;
                return true;
            }
        }

        at = default;
        return false;
    }

    private void Watch()
    {
        attackers.RemoveAll(ai => ai == null || ai.CurrentState == MonsterAI.State.Dead);
        if (fought && attackers.Count == 0 && comeAt < 0f)
        {
            fought = false;
            leaveAt = -1f;
            ReturnWeather();
            GameMessages.Post(CorruptionSettings.Current.Cleared, MessageKind.Gain);
            GameSession.Settlement.ChangeBlight(-0.1f); // beaten back: the woods heal a little
            int reward = CorruptionSettings.Current.ExperienceFor(bandSize);
            if (reward > 0)
            {
                ExperienceTracker.Grant(reward);
            }

            return;
        }

        if (leaveAt >= 0f)
        {
            if (Time.time >= leaveAt)
            {
                foreach (MonsterAI ai in attackers) Destroy(ai.gameObject);
                attackers.Clear();
                leaveAt = -1f;
                fought = false;
            }

            return;
        }

        if (Time.time >= nextDirect)
        {
            nextDirect = Time.time + DirectInterval;
            float chaseLimit = WardBreachSettings.Current.ChaseLimit;
            foreach (MonsterAI ai in attackers) HomeAssault.Direct(ai, chaseLimit);
        }
    }

    // At dawn the band gives up and slinks back the way it came.
    private void Dawn()
    {
        comeAt = -1f;
        ReturnWeather();
        if (attackers.Count == 0)
        {
            return;
        }

        Vector3 centre = HomePortal.Current != null ? HomePortal.Current.transform.position : transform.position;
        foreach (MonsterAI ai in attackers)
        {
            ai.Relentless = false;
            Vector3 away = ai.transform.position + (ai.transform.position - centre).normalized * 40f;
            if (NavMesh.SamplePosition(away, out NavMeshHit hit, 10f, NavMesh.AllAreas)) ai.Roam(hit.position);
        }

        fought = false;
        leaveAt = Time.time + CorruptionSettings.Current.DawnSeconds;
        // They got away with it: the blight they carry spreads in the woods round home.
        GameSession.Settlement.ChangeBlight(0.08f + 0.04f * attackers.Count);
    }
}
