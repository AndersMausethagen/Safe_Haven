using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Readies a raid's creatures: every enemy gets the raid's and the current danger stage's strength (at the start, at
/// each new stage, and as they are spawned later), hunters are sent at the player (or at a friend, in co-op), and
/// waiting spawn groups are woken (an extraction alarm, a scripted event). Owned by the <see cref="RaidDirector"/>,
/// whose multipliers it applies; plain C#.
/// </summary>
public sealed class RaidCreatures
{
    private readonly RaidDirector raid;

    public RaidCreatures(RaidDirector raid)
    {
        this.raid = raid;
    }

    /// <summary>How many alarm spawn groups have been woken this raid (extraction alarms). Roaming spawns grow with it.</summary>
    public int AlarmsRaised { get; private set; }

    /// <summary>
    /// Fills a waiting spawn group now (an extraction alarm, a scripted event) with this raid's seed and rules: its
    /// creatures get the raid's and the current stage's strength, and hunt the player if the group says so. Returns how
    /// many were spawned; 0 when the group had already spawned or the scene has no RaidGenerator. Co-op, on the host's
    /// run, the host's game fills it (<see cref="CreatureSource.Remote"/>), after this player; the least it rolls is returned.
    /// <paramref name="prey"/>: whom a hunting group goes for (a friend's body in the host's game); the player by default.
    /// </summary>
    public int Wake(SpawnGroup group, IDamageable prey = null)
    {
        RaidGenerator generator = raid.Generator;
        if (group == null || group.HasSpawned || generator == null || raid.Session.IsOver)
        {
            return 0;
        }

        if (CreatureSource.Remote != null)
        {
            group.MarkSpawned();
            return CreatureSource.Remote.Wake(group) ? Mathf.Max(1, group.MinCount) : 0;
        }

        // A group filled by a raid spawn list fills the whole list, possibly onto other groups' points too.
        IReadOnlyList<GameObject> spawned = generator.SpawnNow(group);
        if (group.Trigger == SpawnTrigger.Alarm)
        {
            AlarmsRaised++;
        }

        int count = 0;
        foreach (GameObject root in spawned)
        {
            if (root != null)
            {
                count++;
                Prepare(root, group.HuntPlayer, true, prey);
            }
        }

        return count;
    }

    /// <summary>Readies what a stage's spawn group placed (with the group's hunting).</summary>
    public void PrepareGroup(SpawnGroup group)
    {
        foreach (GameObject spawned in group.Spawned)
        {
            if (spawned != null)
            {
                Prepare(spawned, group.HuntPlayer);
            }
        }
    }

    /// <summary>
    /// Gives creatures spawned mid-raid (a pack root and all its members included) the raid's and the current stage's
    /// strength at full health, and when <paramref name="huntPlayer"/> is set sends them straight at the player.
    /// </summary>
    /// <param name="mayBeElite">Off for what a creature summons: a swarm of elites would be no fun, and no reward.</param>
    /// <param name="prey">Whom they hunt instead of the player (co-op: a friend's body, for what the friend woke).</param>
    public void Prepare(GameObject root, bool huntPlayer, bool mayBeElite = true, IDamageable prey = null)
    {
        if (root == null)
        {
            return;
        }

        foreach (Combatant combatant in root.GetComponentsInChildren<Combatant>(true))
        {
            if (combatant.Faction == Faction.Enemy)
            {
                Strengthen(combatant, true);
            }
        }

        if (huntPlayer && mayBeElite)
        {
            RaidElites.RollHunter(raid.Session, root, raid.HealthMultiplier, raid.PowerMultiplier);
        }

        IDamageable hunted = prey is Object body && body != null ? prey : raid.PlayerHealth;
        if (!huntPlayer || hunted == null)
        {
            return;
        }

        foreach (MonsterAI ai in root.GetComponentsInChildren<MonsterAI>())
        {
            ai.PlayerOnly = true;
            ai.Relentless = true;
            ai.Engage(hunted);
        }
    }

    /// <param name="fillHealth">
    /// At the start, living enemies are topped up to their new maximum. When a stage strengthens them mid-fight, they only
    /// gain the extra maximum, so a wounded wolf stays wounded.
    /// </param>
    public void StrengthenAll(bool fillHealth)
    {
        if (Mathf.Approximately(raid.HealthMultiplier, 1f) && Mathf.Approximately(raid.PowerMultiplier, 1f) && raid.StageIndex == 0)
        {
            return;
        }

        // Inactive enemies (waiting for a trigger, say) are included. Enemies spawned later by this raid get the same
        // treatment through Prepare; anything else that spawns enemies needs to apply it itself.
        foreach (Combatant combatant in Object.FindObjectsByType<Combatant>(FindObjectsInactive.Include))
        {
            if (combatant.Faction == Faction.Enemy)
            {
                Strengthen(combatant, fillHealth);
            }
        }
    }

    private void Strengthen(Combatant combatant, bool fillHealth)
    {
        float health = raid.HealthMultiplier;
        float power = raid.PowerMultiplier;
        combatant.TryGetComponent(out Damageable damageable);
        float oldMax = damageable != null ? damageable.MaxHealth : 0f;

        // Percent bonuses on top of each creature's own stats, added by this raid and gone with the scene.
        StatSheet stats = combatant.Stats;
        stats.RemoveModifiersFrom(raid);
        stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, health - 1f, raid));
        stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, power - 1f, raid));
        // An elite's own bonuses multiply the raid's, at its strength now (a stage change, the dark).
        if (combatant.TryGetComponent(out EliteCreature elite))
        {
            elite.Rescale(health, power);
        }

        // Enemies that have not woken yet start at their new maximum when they do; the rest are healed.
        if (damageable != null && damageable.CurrentHealth > 0f)
        {
            float gain = fillHealth ? damageable.MaxHealth : damageable.MaxHealth - oldMax;
            if (gain > 0f)
            {
                damageable.Heal(gain);
            }
        }
    }
}
