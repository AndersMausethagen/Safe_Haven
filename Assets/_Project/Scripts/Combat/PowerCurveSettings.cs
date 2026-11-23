using UnityEngine;

/// <summary>
/// The bounds the player's power curve is held to (Docs/PowerCurveResearch_2026-11-22.md #13; PowerCurveTests): how far a fully stacked
/// kit may reach past its tier's plain kit, how far a mounted blow and a draught may go, and how even a co-op party's share must stay
/// from tier to tier. Design numbers, not code: raise one here, and the test says what it now lets through.
/// </summary>
[CreateAssetMenu(fileName = "PowerCurveSettings", menuName = "Safe Haven/Settings/Power Curve")]
public sealed class PowerCurveSettings : GameSettings<PowerCurveSettings>
{
    [Header("A fully stacked kit (every food slot under the family rule, perks, enchantments, Well Rested)")]
    [Tooltip("Toughness at most this many times the tier's plain kit.")]
    [SerializeField, Min(1f)] private float stackedToughness = 1.5f;
    [Tooltip("Damage at most this many times the tier's plain kit, tier by tier (I first; the last holds beyond). Tier V's is higher: " +
             "its stack carries Melee 50's perks, the top tier's mastery (perks alone x1.24 there, 2026-11-22).")]
    [SerializeField] private float[] stackedDamageByTier = { 1.35f, 1.35f, 1.35f, 1.35f, 1.45f };
    [Tooltip("A weapon coating's own budget (Ember Pitch), as enchantments have theirs: the stacked kit with its best coating does at most " +
             "this many times its damage without. Kept apart from the food's reach so a coating can be felt (the user, 2026-11-23: don't " +
             "let new effects be squeezed into doing almost nothing).")]
    [SerializeField, Min(1f)] private float coatingDamage = 1.35f;

    [Header("From the saddle")]
    [Tooltip("A mounted blow at most this many times a blow on foot, steady.")]
    [SerializeField, Min(1f)] private float mounted = 1.35f;
    [Tooltip("And a charge's first blow at a gallop.")]
    [SerializeField, Min(1f)] private float charge = 1.7f;

    [Header("Evenness from tier to tier")]
    [Tooltip("A co-op party's extra creature health, as a share of a solo creature's, may differ this much between tiers.")]
    [SerializeField, Range(0f, 0.5f)] private float partyShareTolerance = 0.02f;
    [Tooltip("A draught's heal, as a share of the tier's entry kit health, may differ this much between tiers.")]
    [SerializeField, Range(0f, 0.5f)] private float healShareTolerance = 0.1f;

    public float StackedToughness => stackedToughness;
    public float CoatingDamage => coatingDamage;
    /// <summary>The damage reach at <paramref name="tier"/>.</summary>
    public float StackedDamageAt(int tier) => stackedDamageByTier == null || stackedDamageByTier.Length == 0 ? 1.35f
        : stackedDamageByTier[Mathf.Clamp(tier - 1, 0, stackedDamageByTier.Length - 1)];
    public float Mounted => mounted;
    public float Charge => charge;
    public float PartyShareTolerance => partyShareTolerance;
    public float HealShareTolerance => healShareTolerance;
}
