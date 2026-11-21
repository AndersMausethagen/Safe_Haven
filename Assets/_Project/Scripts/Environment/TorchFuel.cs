using UnityEngine;

/// <summary>
/// A torch that burns charcoal (the user, 2026-11-21: "Torches should need coal to work, it can automatically take from player or haven
/// inventory and automatically stops burning during the day. They start again when it gets dark"). At dusk each torch takes its night's
/// fuel (Per Night of Fuel) from the bag and the chests at home (MaterialPool), and burns until dawn: its light, its glow and the lit ground
/// night creatures keep out of (VisibilitySource). By day it is out. With nothing to burn it stays dark, and the Haven says how many stand
/// dark once a night. A scaffold or a ruin stays dark too. Which night it was fed for is saved, so a load at night doesn't charge it twice.
/// In a friend's Haven the torches burn as they look: the fuel is the host's to pay.
/// </summary>
[DisallowMultipleComponent]
public sealed class TorchFuel : MonoBehaviour, ISaveable
{
    private const float CheckSeconds = 0.5f;
    private const float Hysteresis = 0.06f;
    // An unfed torch asks the stores again only now and then: a ring of wall torches with no charcoal searched the bag and every
    // chest twice a second all night.
    private const float RetrySeconds = 8f;

    [Tooltip("What it burns: Charcoal.")]
    [SerializeField] private ItemDefinition fuel;
    [Tooltip("How much a night of burning takes.")]
    [SerializeField, Min(1)] private int perNight = 1;
    [Tooltip("The flame's light (switched off while out).")]
    [SerializeField] private GameObject flame;
    [Tooltip("The glowing flame mesh (hidden while out).")]
    [SerializeField] private Renderer glow;
    [Tooltip("Darkness (0 day, 1 night) at which it is lit; it goes out a little below it, so a wavering dusk doesn't blink it.")]
    [SerializeField, Range(0f, 1f)] private float lightAt = 0.35f;

    private static int warnedNight = -1;
    private static int darkCount;
    private static int darkCountNight = -1;

    private int fedNight = -1;
    private int countedDarkNight = -1;
    private float nextFeedTry;
    private bool lit = true;
    private float nextCheck;
    private VisibilitySource sight;
    private Construction building;

    // Entering Play doesn't reload scripts in this project: the last session's night counts go with it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        warnedNight = -1;
        darkCount = 0;
        darkCountNight = -1;
    }

    /// <summary>True while it burns.</summary>
    public bool IsLit => lit;

    /// <summary>True when tonight's fuel has been taken.</summary>
    public bool IsFed => fedNight == Night;

    public ItemDefinition Fuel => fuel;
    public int PerNight => perNight;

    /// <summary>The night it was last fed for (a wall torch's wall saves it: WallFittings).</summary>
    public int FedNight
    {
        get => fedNight;
        set => fedNight = value;
    }

    /// <summary>
    /// Which night it is: counted from dusk, not from dawn. The day count turns at 06:00 while a torch still burns into the morning,
    /// which charged it again at dawn (the review of 2026-11-21).
    /// </summary>
    public static int Night => GameSession.Here.DaysPassed - (AtmosphereConditions.Hour < 12f ? 1 : 0);

    private void Awake()
    {
        sight = GetComponent<VisibilitySource>();
        building = GetComponent<Construction>();
        nextCheck = Time.time + Random.value * CheckSeconds;
    }

    private void Start() => Show(Wanted());

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + CheckSeconds;
        Show(Wanted());
    }

    // Lit at night, standing, and fed for this night (fed now if it can be).
    private bool Wanted()
    {
        float dark = NightSettings.Darkness;
        bool night = lit ? dark >= lightAt - Hysteresis : dark >= lightAt;
        if (!night || (building != null && !building.IsStanding))
        {
            return false;
        }

        if (HavenVisit.InHostsHaven)
        {
            return true;
        }

        if (IsFed)
        {
            return true;
        }

        if (Time.time < nextFeedTry)
        {
            return false;
        }

        nextFeedTry = Time.time + RetrySeconds;
        return TryFeed();
    }

    /// <summary>Takes tonight's fuel from the bag and the chests at home. False (and a word once a night) when there is none.</summary>
    public bool TryFeed()
    {
        if (fuel == null)
        {
            fedNight = Night;
            return true;
        }

        Player player = Player.Current;
        if (player != null && MaterialPool.For(player.gameObject).TrySpend(null, new[] { new ItemAmount(fuel, perNight) }))
        {
            fedNight = Night;
            return true;
        }

        if (darkCountNight != Night)
        {
            darkCountNight = Night;
            darkCount = 0;
        }

        // Each torch counts itself once a night, however often it asks again.
        if (countedDarkNight != Night)
        {
            countedDarkNight = Night;
            darkCount++;
        }

        if (warnedNight != Night && player != null && Place.AtHome)
        {
            // A moment later, so every torch has counted itself.
            warnedNight = Night;
            Invoke(nameof(SayDark), 1f);
        }

        return false;
    }

    private void SayDark()
    {
        GameMessages.Post($"{darkCount} torch{(darkCount == 1 ? "" : "es")} stand{(darkCount == 1 ? "s" : "")} dark tonight: they burn {fuel.DisplayName}, taken from your bag or chests at dusk. Leave oak in the campfire to make more.",
            MessageKind.Warning);
    }

    private void Show(bool on)
    {
        if (on == lit)
        {
            return;
        }

        lit = on;
        if (flame != null)
        {
            flame.SetActive(on);
        }

        if (glow != null)
        {
            glow.enabled = on;
        }

        if (sight != null)
        {
            sight.enabled = on;
        }
    }

    public string SaveKey => "torch-fuel";

    [System.Serializable]
    private struct SavedState
    {
        public int fedNight;
    }

    public string CaptureState() => JsonUtility.ToJson(new SavedState { fedNight = fedNight });

    public void RestoreState(string state)
    {
        if (!string.IsNullOrEmpty(state))
        {
            fedNight = JsonUtility.FromJson<SavedState>(state).fedNight;
        }
    }
}
