using UnityEngine;

/// <summary>
/// The numbers of the techniques perks teach (PerkTechnique): how much each changes. Lives at Resources/TechniqueSettings;
/// built-in defaults apply when it is missing. Which perk teaches which is on the perks.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Technique Settings", fileName = "TechniqueSettings")]
public sealed class TechniqueSettings : GameSettings<TechniqueSettings>
{
    [Header("Gathering")]
    [Tooltip("Heartwood Strike and Shatter: how many swings a critical one counts as.")]
    [SerializeField, Min(1f)] private float criticalCountsAs = 2f;
    [Tooltip("Rich Seam: extra ore for each critical swing, on top of the usual bonus.")]
    [SerializeField, Min(0)] private int richSeamOre = 1;
    [Tooltip("Sweep: metres around a plant picked by hand in which the same plants are picked too.")]
    [SerializeField, Min(0.5f)] private float sweepRadius = 3f;
    [Tooltip("Sweep: most extra plants picked at once.")]
    [SerializeField, Min(0)] private int sweepMost = 3;
    [Tooltip("Forager's Haste: share faster for a while after picking or cutting a plant.")]
    [SerializeField, Range(0f, 1f)] private float hasteSpeed = 0.15f;
    [SerializeField, Min(1f)] private float hasteSeconds = 10f;
    [Tooltip("Quick Bite: how many times sooner fish bite.")]
    [SerializeField, Min(1f)] private float quickBite = 2f;
    [Tooltip("Double Catch: chance of a second fish.")]
    [SerializeField, Range(0f, 1f)] private float doubleCatchChance = 0.25f;

    [Header("Crafting")]
    [Tooltip("Sure Hand: plain pieces in a row after which the next is at least Fine.")]
    [SerializeField, Min(1)] private int sureHandRun = 4;
    [Tooltip("Hearty Meals: share of the player's health a meal heals.")]
    [SerializeField, Range(0f, 1f)] private float heartyHeal = 0.25f;
    [Tooltip("Potent Remedies: share more a healing draught heals.")]
    [SerializeField, Range(0f, 2f)] private float potentHeal = 0.3f;
    [Tooltip("Spare Dose: chance a draught or tonic isn't used up.")]
    [SerializeField, Range(0f, 1f)] private float spareDoseChance = 0.25f;
    [Tooltip("Patch Up: share of the usual repair cost.")]
    [SerializeField, Range(0f, 1f)] private float patchUpCost = 0.5f;
    [Tooltip("Raise in a Day: how many times faster the hammer works.")]
    [SerializeField, Min(1f)] private float raiseSpeed = 2f;

    [Header("Combat")]
    [Tooltip("Riposte: a parry's counter stays ready this many times longer, and steps in as far as the weapon's rush, so the parried " +
             "enemy, knocked back, is still reached. (Before 2026-11-15 the perk only gave the counter every parry already gives.)")]
    [SerializeField, Min(1f)] private float riposteWindow = 1.6f;
    [Tooltip("Second Wind: stamina back for a kill with a weapon swing.")]
    [SerializeField, Min(0f)] private float secondWindStamina = 20f;
    [Tooltip("Deadly Draw: how much harder a perfect shot hits.")]
    [SerializeField, Min(1f)] private float deadlyDraw = 1.5f;
    [Tooltip("Pinning Shot: the status a critical arrow puts on what it hits (Roots).")]
    [SerializeField] private StatusEffectDefinition pinning;
    [Tooltip("Spell Echo: chance a spell leaves no cooldown.")]
    [SerializeField, Range(0f, 1f)] private float spellEchoChance = 0.15f;
    [Tooltip("Soul Siphon: share of the player's health a spell kill heals.")]
    [SerializeField, Range(0f, 1f)] private float soulSiphon = 0.1f;
    [Tooltip("Silent Takedown: a blow from the shadows finishes a creature whose health is at most this many times the blow.")]
    [SerializeField, Min(1f)] private float takedownReach = 4f;
    [Tooltip("Shadowstep: how much faster sneaking is.")]
    [SerializeField, Min(1f)] private float shadowstep = 1.35f;

    [Header("Weapon flourishes (Melee)")]
    [Tooltip("The Melee level from which every Weapon Art gains the flourish of its weapon's kind.")]
    [SerializeField, Range(1, 50)] private int flourishLevel = 30;
    [Tooltip("One-handed (Flurry): how much harder the art hits.")]
    [SerializeField, Min(1f)] private float flurryDamage = 1.3f;
    [Tooltip("One-handed (Flurry): share of the art's Focus a kill with it gives back.")]
    [SerializeField, Range(0f, 1f)] private float flurryRefund = 0.5f;
    [Tooltip("Two-handed (Sunder): how many times the art wears down poise.")]
    [SerializeField, Min(1f)] private float sunderPoise = 2.5f;

    public float CriticalCountsAs => criticalCountsAs;
    public int RichSeamOre => richSeamOre;
    public float SweepRadius => sweepRadius;
    public int SweepMost => sweepMost;
    public float HasteSpeed => hasteSpeed;
    public float HasteSeconds => hasteSeconds;
    public float QuickBite => quickBite;
    public float DoubleCatchChance => doubleCatchChance;
    public int SureHandRun => sureHandRun;
    public float HeartyHeal => heartyHeal;
    public float PotentHeal => potentHeal;
    public float SpareDoseChance => spareDoseChance;
    public float PatchUpCost => patchUpCost;
    public float RaiseSpeed => raiseSpeed;
    public float SecondWindStamina => secondWindStamina;
    public float RiposteWindow => riposteWindow;
    public float DeadlyDraw => deadlyDraw;
    public StatusEffectDefinition Pinning => pinning;
    public float SpellEchoChance => spellEchoChance;
    public float SoulSiphon => soulSiphon;
    public float TakedownReach => takedownReach;
    public float Shadowstep => shadowstep;
    public int FlourishLevel => flourishLevel;
    public float FlurryDamage => flurryDamage;
    public float FlurryRefund => flurryRefund;
    public float SunderPoise => sunderPoise;
}
