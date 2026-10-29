using UnityEngine;

/// <summary>
/// The player a companion goes along with, and what that player last hit: "attack my target" means that enemy for a
/// while after the hit. Plain C#, owned by a Companion.
/// </summary>
public sealed class CompanionLeader
{
    private const float TargetMemory = 15f;

    private MonsterAI target;
    private float targetAt;

    public Player Player { get; private set; }

    /// <summary>What the leader last hit, while it is remembered; null otherwise.</summary>
    public MonsterAI Target => target != null && Time.time - targetAt <= TargetMemory ? target : null;

    /// <summary>Follows <paramref name="player"/> (null lets go), listening to what they hit.</summary>
    public void Follow(Player player)
    {
        if (player == Player)
        {
            return;
        }

        if (Player != null && Player.Combatant != null)
        {
            Player.Combatant.DamageDealt -= OnHit;
        }

        Player = player;
        target = null;
        if (Player != null && Player.Combatant != null)
        {
            Player.Combatant.DamageDealt += OnHit;
        }
    }

    private void OnHit(IDamageable hit, DamageInfo damage, DamageResult result)
    {
        if (hit is Component body && body.TryGetComponent(out MonsterAI monster))
        {
            target = monster;
            targetAt = Time.time;
        }
    }
}
