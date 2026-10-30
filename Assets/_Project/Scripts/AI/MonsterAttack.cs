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

    public bool IsAvailable(float distance, float now, float readyAt, bool isLeader)
    {
        return attack != null && distance >= minimumRange && distance <= maximumRange
            && now >= readyAt && (!packLeaderOnly || isLeader);
    }
}
