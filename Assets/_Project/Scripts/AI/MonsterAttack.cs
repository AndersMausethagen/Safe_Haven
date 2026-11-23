using System;
using UnityEngine;

public enum MonsterAttackKind { Melee, Projectile, Charge, Howl, Summon, Lob }
public enum MonsterMovementTactic { Chase, KeepDistance }

[Flags]
public enum MonsterTemperament
{
    Aggressive = 1,
    Cowardly = 2,
    Defensive = 4,
}

/// <summary>How a creature turns when badly hurt (Monster Definition › When Hurt).</summary>
public enum HurtMood
{
    None,
    /// <summary>A cry, angry eyes and breath; its cooldowns shorten (wind-ups as they were).</summary>
    Fury,
    /// <summary>A cry and angry eyes; its Below Health strikes come out.</summary>
    Desperate,
}

/// <summary>What the player (a creature's target) is visibly doing, which a strike can be chosen against (Monster Attack › When Foe).</summary>
[Flags]
public enum FoeDoing
{
    None = 0,
    /// <summary>Has had the guard up a while, facing it.</summary>
    Guarding = 1,
    /// <summary>Walking or running away from it.</summary>
    BackingOff = 2,
    /// <summary>Drawing or aiming a bow.</summary>
    Shooting = 4,
    /// <summary>At its side or behind it.</summary>
    Behind = 8,
}

/// <summary>One available action. Higher priority wins; list order breaks ties. Actions given a Weight share their band and are
/// chosen among by chance instead (Monster Attacks › SelectAttack).</summary>
[Serializable]
public sealed class MonsterAttack
{
    public AttackDefinition attack;
    public MonsterAttackKind kind;
    [Min(0f)] public float damage = 8f;
    [Min(0f)] public float minimumRange;
    [Min(0.1f)] public float maximumRange = 2f;
    public int priority;
    [Tooltip("A stage's opener (a boss's set piece as a phase begins): used once, as soon as its From Phase has begun and it reaches, " +
             "and never again; its Cooldown isn't used, and it is never the answer to a backstab. Give it the highest priority, no weight, " +
             "and a Maximum Range that reaches anywhere in the arena. Broken off before it lands, it is spent all the same. Not used in its " +
             "stage (the boss went through it too fast), it is retired with it: it never opens a later stage, unless its Until Phase is set " +
             "past its From Phase's next.")]
    public bool opener;
    [Tooltip("0: chosen by priority alone, as always. Above 0: when the highest-priority ready action has a weight, every ready action with " +
             "a weight (and no weightless one above it) is a candidate, picked by chance in proportion to these, so a fight doesn't run in " +
             "the same order every time. The action just used counts half; cooldowns are kept.")]
    [Min(0f)] public float weight;
    [Tooltip("Picked more readily (Foe Bonus times its weight) while its target does one of these: a shield rush at a raised guard, " +
             "a lunge at someone backing off or shooting, a backhand at someone at its flank.")]
    public FoeDoing whenFoe;
    [Tooltip("A great blow, its big one (a stomp, a charge, a quake): its warning gets a dark edge and a low swell as it starts, so it " +
             "is told apart from its everyday strikes. An attack with Impact of Combat Feel's Heavy Impact or more counts anyway.")]
    public bool heavy;
    [Min(0f)] public float cooldown = 1f;
    public bool interruptible;
    [Tooltip("Seconds it reels when a blow breaks it off in its wind-up: open to blows and backstabs, as a staggered creature is, so " +
             "breaking a blue (Interruptible) strike pays instead of only stopping it (the Heartroot's Regrowth, its Call Roots). " +
             "0: it only waits a moment before its next strike, as always.")]
    [Min(0f)] public float reelWhenBroken;
    [Tooltip("Plays as this action starts, instead of the creature's Attack Sound: a howl, a call to the ground. Empty: the creature's own.")]
    public SoundDefinition sound;
    [Tooltip("How much a shot or a charge aims ahead of a moving target. 0: where it stands, so walking sideways is safe. 1: fully ahead. Keep it low: a dodge must still work.")]
    [Range(0f, 1f)] public float leadTarget;
    [Tooltip("The raid tier it learns this at: a tier II wolf pounces, a tier I wolf doesn't. 0: always. Above 0 it is only used in raids of that tier or higher.")]
    [Range(0, Tiers.Max)] public int fromTier;
    [Tooltip("For a boss: only from this stage of its fight on. 0 is from the start.")]
    [Min(0)] public int fromPhase;
    [Tooltip("For a boss: no longer used from this stage of its fight on, so each stage is its own (the Heartroot's Call Roots and " +
             "Thornwave sink with its walls at stage 3: 2). Stages count as From Phase does. 0: used to the end.")]
    [Min(0)] public int untilPhase;
    [Tooltip("Only once its own health is down to this share: a last resort, a creature hardening or calling for help when hurt. 1: always.")]
    [Range(0f, 1f)] public float belowHealth = 1f;
    [Tooltip("Left on the ground where its shot or lob lands, or along a charge's path: a pool of bile, a web, a trail of fire " +
             "(a prefab with a GroundHazard).")]
    public GameObject hazardPrefab;
    [Tooltip("Charge: metres between the hazards it leaves along its path. 0: one where it stops.")]
    [Min(0f)] public float hazardEvery;
    [Tooltip("Seconds it reels afterwards, spent by it and open to blows as a staggered creature is (the Heartroot after it " +
             "calls up its roots). 0: none.")]
    [Min(0f)] public float openAfter;
    [Tooltip("Its Open After is a topple: it goes down for that long (Knockdown, Down, Get Up), its heart open to the Topple Strike and a " +
             "piece of its armour breaking as a topple's does (the Heartroot at the end of its Hollow's Hold: the kill window). Needs Open After.")]
    public bool toppleAfter;
    [Header("Melee: combos and held blows")]
    [Tooltip("Strikes that can follow this one, in order. Each is aimed anew, with its own wind-up and warning.")]
    public AttackDefinition[] followUps;
    [Tooltip("Chance it goes on into its follow-ups.")]
    [Range(0f, 1f)] public float comboChance = 0.5f;
    [Tooltip("Seconds after a blow lands before the next winds up.")]
    [Min(0f)] public float comboGap = 0.2f;
    [Tooltip("The raid tier it learns the follow-ups at. 0: always. Above 0 they are only used in raids of that tier and up.")]
    [Range(0, Tiers.Max)] public int comboFromTier;
    [Tooltip("Chance it holds the wind-up before letting the blow go, tracking its target meanwhile: a roll on reflex comes too soon.")]
    [Range(0f, 1f)] public float holdChance;
    [Header("Melee: after the blow (Docs/EnemyAIReview_2026-11-19.md §1)")]
    [Tooltip("Metres it runs off from its target once this strike is over, landed or not, swung round to the side away from the others, " +
             "then holds ready: hit and run, not standing at the target's feet striking on cooldown. 0: it stays.")]
    [Min(0f)] public float breakAway;
    [Tooltip("Seconds it holds off after breaking away before it may start another close strike (least and most). A leap or charge it " +
             "now has room for is allowed.")]
    public Vector2 breakAwaySeconds = new Vector2(0.8f, 1.4f);
    [Tooltip("Seconds it holds for. Its warning stops filling as long.")]
    [Min(0f)] public float holdSeconds = 0.45f;
    [Header("Projectiles")]
    public GameObject projectilePrefab;
    [Min(1)] public int burstCount = 1;
    [Min(0.05f)] public float burstInterval = 0.2f;
    [Min(0.1f)] public float projectileSpeed = 12f;
    [Min(0.01f)] public float projectileRadius = 0.15f;
    [Header("Charge")]
    [Min(0.1f)] public float chargeSpeed = 12f;
    [Min(0.1f)] public float chargeDistance = 8f;
    [Min(0f)] public float exposedSeconds = 2f;
    [Tooltip("How high the body arcs through a charge, in metres: a pounce rather than a rush. Only its look: it can still be hit, and still hits, on the ground.")]
    [Min(0f)] public float leapHeight;
    [Header("Howl (a call to its side, or at its foe)")]
    [Min(0.1f)] public float buffRadius = 10f;
    [Min(0.1f)] public float buffSeconds = 8f;
    [Range(0f, 1f)] public float damageBonus = 0.05f;
    public bool packLeaderOnly;
    [Tooltip("Only itself, not its side around it (a troll turning to stone).")]
    public bool selfOnly;
    [Tooltip("Share of each one's health mended over Buff Seconds (a troll-wife's salve).")]
    [Range(0f, 1f)] public float healShare;
    [Tooltip("How much faster they move while it lasts, as a share (a fossegrim's tune).")]
    [Range(0f, 1f)] public float speedBonus;
    [Tooltip("How much sooner they strike again while it lasts, as a share of their waits.")]
    [Range(0f, 0.9f)] public float hasteBonus;
    [Tooltip("Resistance to every kind of harm while it lasts, as a share (stone skin). Its weaknesses stay as they are.")]
    [Range(0f, 0.95f)] public float toughness;
    [Tooltip("Every creature of its side within Buff Radius learns where its foe is and comes (a mistwing's screech).")]
    public bool alertsOthers;
    [Tooltip("Raise the alarm (Docs/EnemyAIReview_2026-11-19.md §8): the first of its group to spot the player calls this before it fights, " +
             "once a group in 20 s, and its group waits for the call. Completed, every creature of its side within Alarm Radius comes; " +
             "broken (make it Interruptible), only the suspicion of its spotting spreads. Turns an alarm into something to answer.")]
    public bool onSpotted;
    [Min(1f)] public float alarmRadius = 18f;
    [Tooltip("Laid on its foe when the foe is within Buff Radius and in sight (a huldra's call).")]
    public StatusEffectDefinition foeStatus;
    [Tooltip("Metres its foe is drawn toward it, with Foe Status (negative: thrown away).")]
    public float foePull;
    [Tooltip("What the message says, after the creature's name. Empty: the usual words for a rallying howl.")]
    public string announce;
    [Header("Summon")]
    [Tooltip("The creature called in. It appears near the summoner and goes straight for the player.")]
    public GameObject summonPrefab;
    [Min(1)] public int summonCount = 2;
    [Min(0.5f)] public float summonRadius = 3f;
    [Tooltip("The most of its summons alive at once.")]
    [Min(1)] public int summonLimit = 4;
    [Tooltip("Bound summons (the Heartroot's Rootlings; Docs/HeartrootPlaytest_2026-11-23.md P1): each one of this call that dies sends a " +
             "shock up the sap line that ties it to the summoner, taking this share of the summoner's own poise (a parry takes 0.4). 0: " +
             "the summons are free, with no line.")]
    [Range(0f, 1f)] public float bondPoiseShare;
    [Tooltip("When every summon of one call has died within Bond Within seconds of the call, the summoner topples for this long, its " +
             "heart open: the roots it called tear out of it. 0: no topple, only the shocks.")]
    [Min(0f)] public float bondToppleSeconds;
    [Tooltip("Seconds after the call within which killing all of its summons topples the summoner.")]
    [Min(0f)] public float bondWithin = 12f;
    [Header("Lob (aimed at the ground where its foe stands)")]
    [Tooltip("Seconds between the throw and the landing, while the ring on the ground fills: the time to step out. " +
             "The attack's Range is the ring's radius (give it an Arc of 360). Burst Count lobs fall one after another, each " +
             "aimed anew, Burst Interval apart; Lead Target aims them ahead. Projectile Prefab is what is seen flying.")]
    [Min(0.2f)] public float lobSeconds = 1.1f;
    [Tooltip("Metres the later lobs of a burst scatter around the foe.")]
    [Min(0f)] public float lobScatter = 2f;

    public bool IsAvailable(float distance, float now, float readyAt, bool isLeader, int tier = 0, int phase = 0, float health = 1f)
    {
        return attack != null && KnownAt(tier) && InPhase(phase) && distance >= minimumRange && distance <= maximumRange
            && now >= readyAt && (!packLeaderOnly || isLeader) && WhenHurt(health);
    }

    /// <summary>
    /// Whether a boss at this stage of its fight uses it: from its From Phase on, and before its Until Phase (0: to the end). An opener
    /// with no Until Phase belongs to its own stage only: one the boss went through too fast to use it doesn't open the next.
    /// </summary>
    public bool InPhase(int phase) => fromPhase <= phase && (untilPhase > 0 ? phase < untilPhase : !opener || phase == fromPhase);

    /// <summary>
    /// When it can be used again after it ends at <paramref name="now"/> (it landed, or was broken off): its cooldown, scaled; never for an
    /// opener, which is spent once used. Pure, for tests.
    /// </summary>
    public float ReadyAfter(float now, float cooldownScale = 1f) => opener ? float.PositiveInfinity : now + cooldown * cooldownScale;

    /// <summary>Whether its own health (a share) is low enough for it. Entries saved before Below Health existed read 0: always.</summary>
    public bool WhenHurt(float health) => belowHealth <= 0f || belowHealth >= 1f || health <= belowHealth + 1e-4f;

    /// <summary>Whether a creature in a raid of this tier can use it (0: not in a raid).</summary>
    public bool KnownAt(int tier) => tier >= fromTier;

    /// <summary>True for an action that lands a blow, unlike a howl or a summon.</summary>
    public bool Strikes => attack != null && kind != MonsterAttackKind.Howl && kind != MonsterAttackKind.Summon;
}
