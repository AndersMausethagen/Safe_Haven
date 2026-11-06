using UnityEngine;

/// <summary>
/// The spirits answer how they are treated (saved on SettlementState).
/// <list type="bullet">
/// <item>The Nøkk: stand quietly at the edge of still, open water at night and it plays to you from the dark; stay to
/// the end of its air and it teaches it to you, for good. Fish then come to your line half again as fast, and the
/// Nøkken in the deep leaves you be (PlayerFishing).</item>
/// <item>The Huldra: let her watch you and go in her own time, twice, and she leaves a gift where she stood. Chase her
/// and she is gone, and won't show herself for days.</item>
/// </list>
/// <para>Where it belongs: once in the Haven scene (set up by AgentScripts/BuildSeasonalLife.cs).</para>
/// </summary>
public sealed class SpiritBonds : MonoBehaviour
{
    public const string NokkGift = "nokk";
    public const string Huldra = "huldra";

    [Header("The Nøkk")]
    [SerializeField] private SoundDefinition nokkAir;
    [Tooltip("Metres from still water's edge the player stands to be heard by it.")]
    [SerializeField, Min(1f)] private float nearWater = 5f;
    [Tooltip("Seconds of standing there before it begins to play, and until its air is learnt.")]
    [SerializeField] private Vector2 listen = new Vector2(6f, 28f);
    [Header("The Huldra")]
    [Tooltip("Times she must be let be before she leaves a gift.")]
    [SerializeField, Min(1)] private int respectForGift = 2;
    [Tooltip("Days she stays away once chased.")]
    [SerializeField, Min(0)] private int awayDays = 3;
    [SerializeField] private ItemDefinition gift;
    [SerializeField] private Vector2Int giftCount = new Vector2Int(2, 4);
    [SerializeField] private SoundDefinition giftSound;

    private Vector3 standing;
    private float stoodFor;
    private bool playing;
    private float nextCheck;

    public void Configure(SoundDefinition air, ItemDefinition left, SoundDefinition leftSound)
    {
        nokkAir = air;
        gift = left;
        giftSound = leftSound;
    }

    /// <summary>Whether a glimpse may show itself now: a spirit that was chased keeps away for a while.</summary>
    public static bool Welcome(Glimpse glimpse) =>
        glimpse == null || glimpse.Spirit != Huldra || GameSession.Settlement.DaysPassed >= GameSession.Settlement.HuldraAwayUntil;

    /// <summary>The Huldra's memory after a glimpse of her ends: (respect, away until, leaves a gift now).</summary>
    public static (int respect, int awayUntil, bool gift) AfterHuldra(int respect, int awayUntil, bool chased, int today, int needed, int away)
    {
        if (chased)
        {
            return (0, today + away, false);
        }

        respect++;
        return respect >= needed ? (0, awayUntil, true) : (respect, awayUntil, false);
    }

    private void OnEnable() => Glimpse.Left += OnGlimpseLeft;

    private void OnDisable() => Glimpse.Left -= OnGlimpseLeft;

    private void OnGlimpseLeft(Glimpse glimpse, bool chased)
    {
        if (glimpse == null || glimpse.Spirit != Huldra)
        {
            return;
        }

        SettlementState settlement = GameSession.Settlement;
        (int respect, int awayUntil, bool leaves) = AfterHuldra(settlement.HuldraRespect, settlement.HuldraAwayUntil, chased,
            settlement.DaysPassed, respectForGift, awayDays);
        settlement.SetHuldra(respect, awayUntil);
        if (chased)
        {
            GameMessages.Post("She is gone between the trees, and the woods feel colder for it.");
            return;
        }

        if (leaves && gift != null)
        {
            Transform at = glimpse.transform;
            AudioManager.Play(giftSound, at.position + Vector3.up);
            DroppedItem.Drop(at, new ItemStack(gift, Random.Range(giftCount.x, giftCount.y + 1)));
            GameMessages.Post("Where the Huldra stood, something has been left for you.", MessageKind.Gain);
        }
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        const float tick = 0.5f;
        nextCheck = Time.time + tick;
        Player player = Player.Current;
        if (player == null || GameSession.Settlement.HasSpiritGift(NokkGift))
        {
            return;
        }

        Vector3 at = player.transform.position;
        WaterSurface water = OpenStillWaterBeside(at, out Vector3 edge);
        bool night = NightSettings.Darkness > 0.6f;
        if (water == null || !night || (at - standing).sqrMagnitude > 1.5f * 1.5f)
        {
            standing = at;
            stoodFor = 0f;
            playing = false;
            return;
        }

        stoodFor += tick;
        if (!playing && stoodFor >= listen.x)
        {
            playing = true;
            AudioManager.Play(nokkAir, edge + (edge - at).normalized * 6f);
        }

        if (stoodFor >= listen.y)
        {
            GameSession.Settlement.GiveSpiritGift(NokkGift);
            GameMessages.Post("A tune rises out of the dark water, and stays with you. The Nøkk has taught you its air: "
                + "fish will come to your line, and the Nøkken leaves you be.", MessageKind.Gain);
        }
    }

    private WaterSurface OpenStillWaterBeside(Vector3 at, out Vector3 edge)
    {
        edge = default;
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water == null || !water.IsStill || water.IsFrozen)
            {
                continue;
            }

            // The nearest point of its outline: on the player themselves when they stand in it (Contains only knows
            // the mesh's box, which takes in the shore round an oval pond).
            Vector3 point = water.NearestPoint(at);
            float away = new Vector2(point.x - at.x, point.z - at.z).sqrMagnitude;
            if (away > 0.4f * 0.4f && away <= nearWater * nearWater)
            {
                edge = point;
                return water;
            }
        }

        return null;
    }
}
