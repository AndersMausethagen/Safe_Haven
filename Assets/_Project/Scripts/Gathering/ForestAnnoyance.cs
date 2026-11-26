using System;
using UnityEngine;

/// <summary>How annoyed the forest is with the player's gathering, from quiet to its one big wave.</summary>
public enum AnnoyanceLevel
{
    Calm = 0,
    /// <summary>More creatures roam, and they come more often.</summary>
    Stirring = 1,
    /// <summary>Hunters are sent toward the player at intervals.</summary>
    Angry = 2,
    /// <summary>A wave of creatures is unleashed, then the forest settles back to Angry.</summary>
    Wrath = 3,
}

/// <summary>The tuning of a raid's ForestAnnoyanceSystem, edited in the Systems Editor (Raids > Raid Systems).</summary>
[Serializable]
public sealed class ForestAnnoyanceSettings
{
    [Header("Annoyance per swing")]
    [Tooltip("Annoyance from a critical swing (quiet, clean cut).")]
    [SerializeField, Min(0f)] private float perCritical = 2.5f;
    [Tooltip("Annoyance from a normal swing.")]
    [SerializeField, Min(0f)] private float perNormal = 10f;
    [Tooltip("Annoyance from a glancing blow.")]
    [SerializeField, Min(0f)] private float perGlancing = 15f;

    [Header("Calming down")]
    [Tooltip("Seconds without gathering before annoyance starts to fall.")]
    [SerializeField, Min(0f)] private float quietDelay = 5f;
    [Tooltip("Annoyance lost per second while quiet.")]
    [SerializeField, Min(0f)] private float decayPerSecond = 0.5f;

    [Header("Thresholds")]
    [SerializeField, Min(0f)] private float stirringAt = 30f;
    [SerializeField, Min(0f)] private float angryAt = 60f;
    [SerializeField, Min(0f)] private float wrathAt = 100f;

    [Header("Stirring")]
    [Tooltip("Extra roaming creatures allowed alive at once.")]
    [SerializeField, Min(0)] private int stirringExtraRoamers = 2;
    [Tooltip("Multiplies the time between roaming spawns (0.6 = 40% faster).")]
    [SerializeField, Range(0.1f, 1f)] private float stirringIntervalScale = 0.6f;

    [Header("Angry")]
    [Tooltip("Seconds between hunts: every roamer, and idle creatures within the call radius, head for the player.")]
    [SerializeField, Min(1f)] private float huntInterval = 20f;
    [Tooltip("Metres around the player in which idle creatures answer a hunt.")]
    [SerializeField, Min(0f)] private float huntRadius = 60f;

    [Header("Wrath")]
    [Tooltip("Groups the wrath wave spawns around the player (needs Roaming Spawns on the raid).")]
    [SerializeField, Range(1, 10)] private int wrathGroups = 3;
    [Tooltip("What the wave is made of. Empty: the roaming spawn table.")]
    [SerializeField] private SpawnTable wrathTable;
    [Tooltip("Waves per raid. Once used up, annoyance stays capped at the wrath threshold.")]
    [SerializeField, Range(0, 5)] private int maxWraths = 1;

    public float QuietDelay => quietDelay;
    public float DecayPerSecond => decayPerSecond;
    public float StirringAt => stirringAt;
    public float AngryAt => Mathf.Max(angryAt, stirringAt);
    public float WrathAt => Mathf.Max(wrathAt, AngryAt);
    public int StirringExtraRoamers => stirringExtraRoamers;
    public float StirringIntervalScale => stirringIntervalScale;
    public float HuntInterval => huntInterval;
    public float HuntRadius => huntRadius;
    public int WrathGroups => wrathGroups;
    public SpawnTable WrathTable => wrathTable;
    public int MaxWraths => maxWraths;

    public float AnnoyanceFor(SwingResult result)
    {
        switch (result)
        {
            case SwingResult.Critical: return perCritical;
            case SwingResult.Normal: return perNormal;
            default: return perGlancing;
        }
    }
}

/// <summary>
/// The forest's annoyance as plain logic: swings raise it, quiet lowers it after a delay, thresholds set the level.
/// Reaching the wrath threshold fires one wave (up to the raid's limit) and drops back to the angry threshold; with no
/// waves left it is capped there. The wrath is a moment, not a level to stay at: after it the forest is Angry again without
/// that being announced as a change (it would read as a second warning and send a second hunt). Driven by ForestAnnoyanceDriver.
/// </summary>
public sealed class ForestAnnoyance
{
    private readonly ForestAnnoyanceSettings settings;
    private float quietTime;

    public ForestAnnoyance(ForestAnnoyanceSettings settings)
    {
        this.settings = settings ?? new ForestAnnoyanceSettings();
    }

    public float Value { get; private set; }
    /// <summary>The highest annoyance reached this raid (the wrath threshold if a wave fired).</summary>
    public float Peak { get; private set; }
    public int WrathsUsed { get; private set; }
    public AnnoyanceLevel Level { get; private set; }

    /// <summary>0-1 toward the wrath threshold, for the meter.</summary>
    public float Fill => FillOf(Value);

    /// <summary>The peak as 0-1 of the wrath threshold, for the raid summary.</summary>
    public float PeakFill => FillOf(Peak);

    public ForestAnnoyanceSettings Settings => settings;

    public event Action<AnnoyanceLevel> LevelChanged;
    public event Action Wrath;

    public void Add(SwingResult result, float loudness = 1f)
    {
        Add(settings.AnnoyanceFor(result) * Mathf.Max(0f, loudness));
    }

    public void Add(float amount)
    {
        quietTime = 0f;
        if (amount <= 0f)
        {
            return;
        }

        Value += amount;
        Peak = Mathf.Max(Peak, Mathf.Min(Value, settings.WrathAt));
        if (Value >= settings.WrathAt)
        {
            if (WrathsUsed < settings.MaxWraths)
            {
                WrathsUsed++;
                Value = settings.AngryAt;
                SetLevel(AnnoyanceLevel.Wrath);
                Wrath?.Invoke();
                // Settles back quietly: "The forest is angry" right after "The forest's wrath is upon you!" read as a new warning,
                // and the driver hunted a second time two frames after the wrath's own hunt (Docs/SystemReview2_2026-11-17.md S2).
                Level = LevelFor(Value);
            }
            else
            {
                Value = settings.WrathAt;
            }
        }

        UpdateLevel();
    }

    /// <summary>Takes annoyance away at once (a raid event's offering, Docs/RaidEventsPlan_2026-11-26.md E5), never below calm.</summary>
    public void Calm(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Value = Mathf.Max(0f, Value - amount);
        UpdateLevel();
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f || Value <= 0f)
        {
            return;
        }

        quietTime += deltaTime;
        if (quietTime > settings.QuietDelay)
        {
            Value = Mathf.Max(0f, Value - settings.DecayPerSecond * deltaTime);
            UpdateLevel();
        }
    }

    public AnnoyanceLevel LevelFor(float value)
    {
        if (value >= settings.AngryAt)
        {
            return AnnoyanceLevel.Angry;
        }

        return value >= settings.StirringAt && settings.StirringAt > 0f ? AnnoyanceLevel.Stirring : AnnoyanceLevel.Calm;
    }

    private float FillOf(float value)
    {
        return settings.WrathAt > 0f ? Mathf.Clamp01(value / settings.WrathAt) : 0f;
    }

    private void UpdateLevel()
    {
        SetLevel(LevelFor(Value));
    }

    private void SetLevel(AnnoyanceLevel level)
    {
        if (level == Level)
        {
            return;
        }

        Level = level;
        LevelChanged?.Invoke(level);
    }
}

/// <summary>
/// Gathering annoys the forest: every swing in this raid raises annoyance (crits barely), and the forest answers in
/// steps: Stirring (more roamers), Angry (hunters sent at the player), Wrath (one big wave). It calms down while the
/// player is quiet. Adds a ForestAnnoyanceDriver to the RaidDirector.
/// </summary>
[Serializable]
public sealed class ForestAnnoyanceSystem : RaidSystem
{
    [SerializeField] private ForestAnnoyanceSettings settings = new ForestAnnoyanceSettings();

    public ForestAnnoyanceSettings Settings => settings;
    public override string DisplayName => "Forest Annoyance";
    public override string Description => "Gathering noise annoys the forest: Stirring brings more roamers, Angry sends " +
                                          "hunters at the player, Wrath unleashes one wave. Calms down while quiet.";

    public override void Install(RaidDirector director)
    {
        if (!director.TryGetComponent(out ForestAnnoyanceDriver driver))
        {
            driver = director.gameObject.AddComponent<ForestAnnoyanceDriver>();
        }

        driver.Configure(settings);
    }
}
