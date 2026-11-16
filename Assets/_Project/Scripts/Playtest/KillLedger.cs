using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps, for each creature being fought, the blows it has taken: how many and how much from the player, how much from anyone
/// else (companions, residents, other players, hazards) and from damage over time. When it dies, PlaytestLogger writes a Kill
/// row from the record, so "how many blows does a wolf take" is read from the log instead of worked out from the data
/// (the user, 2026-11-16: the per-minute Combat rows could only give a range). Plain C#: the logger feeds it hits.
/// </summary>
public sealed class KillLedger
{
    /// <summary>One creature's fight, from its first blow taken to its death.</summary>
    public sealed class Record
    {
        public string Name;
        public float MaxHealth;
        public float FirstHitTime;
        public int PlayerHits;
        public float PlayerDamage;
        public float PlayerBiggestHit;
        public int PlayerCriticals;
        public int OtherHits;
        public float OtherDamage;
        public float OverTimeDamage;
        public string Killer = "";
        public float Seconds;

        /// <summary>The row's detail: who made the kill, then the player's share, everyone else's, and how long it took.</summary>
        public string Detail =>
            $"by {Killer}; player {PlayerHits} hits {PlayerDamage:0} dmg (biggest {PlayerBiggestHit:0} crits {PlayerCriticals}); " +
            $"others {OtherHits} hits {OtherDamage:0} dmg; over time {OverTimeDamage:0}; health {MaxHealth:0}; {Seconds:0.0} s";
    }

    public const string ByPlayer = "player";

    private readonly Dictionary<object, Record> fights = new Dictionary<object, Record>();

    public int Open => fights.Count;

    /// <summary>
    /// Notes one blow on a creature. <paramref name="healthLost"/> is what it really cost (an overkill counts only what was left).
    /// Returns the finished record when the blow killed it, else null.
    /// </summary>
    public Record Note(object victim, string name, float maxHealth, bool fromPlayer, string sourceName, bool overTime, bool critical,
        float healthLost, bool killed, float time)
    {
        if (victim == null)
        {
            return null;
        }

        if (!fights.TryGetValue(victim, out Record record))
        {
            record = new Record { Name = name, FirstHitTime = time };
            fights[victim] = record;
        }

        record.MaxHealth = Mathf.Max(record.MaxHealth, maxHealth);
        if (overTime)
        {
            record.OverTimeDamage += healthLost;
        }
        else if (fromPlayer)
        {
            record.PlayerHits++;
            record.PlayerDamage += healthLost;
            record.PlayerBiggestHit = Mathf.Max(record.PlayerBiggestHit, healthLost);
            if (critical) record.PlayerCriticals++;
        }
        else
        {
            record.OtherHits++;
            record.OtherDamage += healthLost;
        }

        if (!killed)
        {
            return null;
        }

        fights.Remove(victim);
        record.Killer = fromPlayer ? ByPlayer : string.IsNullOrEmpty(sourceName) ? "unknown" : sourceName;
        if (overTime && fromPlayer) record.Killer += " (over time)";
        record.Seconds = time - record.FirstHitTime;
        return record;
    }

    /// <summary>Forgets every open fight (a new scene: what was being fought is gone).</summary>
    public void Clear() => fights.Clear();
}
