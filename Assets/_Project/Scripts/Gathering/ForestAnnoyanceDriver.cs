using UnityEngine;

/// <summary>
/// Runs a raid's ForestAnnoyance: feeds it the player's gathering swings and makes the forest answer. Stirring and
/// above raise the RoamingSpawner's cap and pace; Angry periodically wakes and sends creatures around the player toward
/// them; Wrath spawns one wave. Added and configured by ForestAnnoyanceSystem; the raid HUD shows its meter.
/// </summary>
[RequireComponent(typeof(RaidDirector))]
public sealed class ForestAnnoyanceDriver : MonoBehaviour
{
    private RaidDirector director;
    private RoamingSpawner spawner;
    private CreatureSleep sleep;
    private float nextHunt;
    private ForestAnnoyanceView view;

    public ForestAnnoyance Annoyance { get; private set; }

    private void Start()
    {
        view = ForestAnnoyanceView.Attach(this);
        RecordStats();
    }

    private void OnDestroy()
    {
        if (view != null)
        {
            Destroy(view.gameObject);
        }
    }

    public void Configure(ForestAnnoyanceSettings settings)
    {
        if (Annoyance != null)
        {
            Annoyance.LevelChanged -= OnLevelChanged;
            Annoyance.Wrath -= OnWrath;
        }

        Annoyance = new ForestAnnoyance(settings);
        Annoyance.LevelChanged += OnLevelChanged;
        Annoyance.Wrath += OnWrath;
    }

    private void Awake()
    {
        director = GetComponent<RaidDirector>();
        if (Annoyance == null)
        {
            Configure(null);
        }
    }

    private void OnEnable()
    {
        PlayerGathering.Swung += OnSwung;
    }

    private void OnDisable()
    {
        PlayerGathering.Swung -= OnSwung;
    }

    private void Update()
    {
        Annoyance.Tick(Time.deltaTime);
        if (Annoyance.Level >= AnnoyanceLevel.Angry && Time.time >= nextHunt)
        {
            nextHunt = Time.time + Annoyance.Settings.HuntInterval;
            Hunt();
        }
    }

    private void OnSwung(GatheringSwing swing)
    {
        Annoyance.Add(swing.Result, swing.Loudness);
        RecordStats();
    }

    private void RecordStats()
    {
        if (director != null && director.Session != null)
        {
            director.Session.Gathering.RecordAnnoyance(Annoyance.PeakFill, Annoyance.WrathsUsed);
        }
    }

    private void OnLevelChanged(AnnoyanceLevel level)
    {
        RoamingSpawner roaming = Spawner();
        if (roaming != null)
        {
            bool stirred = level >= AnnoyanceLevel.Stirring;
            roaming.ExtraCap = stirred ? Annoyance.Settings.StirringExtraRoamers : 0;
            roaming.IntervalScale = stirred ? Annoyance.Settings.StirringIntervalScale : 1f;
        }

        switch (level)
        {
            case AnnoyanceLevel.Stirring:
                GameMessages.Post("The forest stirs.", MessageKind.Warning);
                break;
            case AnnoyanceLevel.Angry:
                GameMessages.Post("The forest is angry. Something is hunting you.", MessageKind.Warning);
                nextHunt = Time.time;
                break;
        }
    }

    private void OnWrath()
    {
        GameMessages.Post("The forest's wrath is upon you!", MessageKind.Warning);
        RoamingSpawner roaming = Spawner();
        if (roaming != null)
        {
            roaming.SpawnWave(Annoyance.Settings.WrathGroups, Annoyance.Settings.WrathTable);
        }

        Hunt();
    }

    private void Hunt()
    {
        if (director == null || director.Player == null)
        {
            return;
        }

        Vector3 player = director.Player.transform.position;
        float radius = Annoyance.Settings.HuntRadius;
        if (sleep == null)
        {
            sleep = GetComponent<CreatureSleep>();
        }

        if (sleep != null)
        {
            sleep.WakeWithin(player, radius);
        }

        GatheringNoise.Investigate(player, radius);
        RoamingSpawner roaming = Spawner();
        if (roaming != null)
        {
            roaming.HuntNow();
        }
    }

    // Systems install in list order, so the spawner may be added after this driver.
    private RoamingSpawner Spawner()
    {
        if (spawner == null)
        {
            spawner = GetComponent<RoamingSpawner>();
        }

        return spawner;
    }
}
