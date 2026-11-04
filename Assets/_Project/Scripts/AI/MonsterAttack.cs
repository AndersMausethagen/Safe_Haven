using System;
using UnityEngine;

public enum MonsterAttackKind { Melee, Projectile, Charge, Howl, Summon }
public enum MonsterMovementTactic { Chase, KeepDistance }

[Flags]
public enum MonsterTemperament
{
    Aggressive = 1,
    Cowardly = 2,
    Defensive = 4,
}

/// <summary>One available action. Higher priority wins; list order breaks ties.</summary>
[Serializable]
public sealed class MonsterAttack
{
    public AttackDefinition attack;
    public MonsterAttackKind kind;
    [Min(0f)] public float damage = 8f;
    [Min(0f)] public float minimumRange;
    [Min(0.1f)] public float maximumRange = 2f;
    public int priority;
    [Min(0f)] public float cooldown = 1f;
    public bool interruptible;
    [Tooltip("Plays as this action starts, instead of the creature's Attack Sound: a howl, a call to the ground. Empty: the creature's own.")]
    public SoundDefinition sound;
    [Tooltip("How much a shot or a charge aims ahead of a moving target. 0: where it stands, so walking sideways is safe. 1: fully ahead. Keep it low: a dodge must still work.")]
    [Range(0f, 1f)] public float leadTarget;
    [Tooltip("The raid tier it learns this at: a tier II wolf pounces, a tier I wolf doesn't. 0: always. Above 0 it is only used in raids of that tier or higher.")]
    [Range(0, Tiers.Max)] public int fromTier;
    [Tooltip("For a boss: only from this stage of its fight on. 0 is from the start.")]
    [Min(0)] public int fromPhase;
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
    [Header("Howl")]
    [Min(0.1f)] public float buffRadius = 10f;
    [Min(0.1f)] public float buffSeconds = 8f;
    [Range(0f, 1f)] public float damageBonus = 0.05f;
    public bool packLeaderOnly;
    [Header("Summon")]
    [Tooltip("The creature called in. It appears near the summoner and goes straight for the player.")]
    public GameObject summonPrefab;
    [Min(1)] public int summonCount = 2;
    [Min(0.5f)] public float summonRadius = 3f;
    [Tooltip("The most of its summons alive at once.")]
    [Min(1)] public int summonLimit = 4;

    public bool IsAvailable(float distance, float now, float readyAt, bool isLeader, int tier = 0, int phase = 0)
    {
        return attack != null && KnownAt(tier) && fromPhase <= phase && distance >= minimumRange && distance <= maximumRange
            && now >= readyAt && (!packLeaderOnly || isLeader);
    }

    /// <summary>Whether a creature in a raid of this tier can use it (0: not in a raid).</summary>
    public bool KnownAt(int tier) => tier >= fromTier;

    /// <summary>True for an action that lands a blow, unlike a howl or a summon.</summary>
    public bool Strikes => attack != null && kind != MonsterAttackKind.Howl && kind != MonsterAttackKind.Summon;
}
