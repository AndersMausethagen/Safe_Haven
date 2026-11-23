using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// One boss try in numbers, for its PlaytestLog.BossTry row (BossTryLog; tested in BossTryTallyTests): how long it ran, how far it
/// got, what opened it up (topples by cause, heart strikes, interrupts, bound summons killed) and what the player's damage came from.
/// The first hand try of the Heartroot left none of this behind (Docs/HeartrootPlaytest_2026-11-23.md P0).
/// </summary>
public sealed class BossTryTally
{
    /// <summary>The kinds of blow the player's damage is split into, in the row's order.</summary>
    public static readonly string[] Kinds = { "light", "heavy", "charged", "counter", "art", "backstab", "heart", "fire", "arrow", "spell", "other" };

    private readonly Dictionary<string, float> damage = new Dictionary<string, float>();
    private readonly int[] topples = new int[5];

    public float WokeAt = -1f;
    public int Stage;
    public int HeartStrikes;
    public int Interrupts;
    public int Calls;
    public int SummonsKilled;
    public int Draughts;
    public string LastBlowOnPlayer = "-";

    public bool Woke => WokeAt >= 0f;

    public int ToppledBy(ToppleCause cause) => topples[(int)cause];

    public void Toppled(ToppleCause cause) => topples[Mathf.Clamp((int)cause, 0, topples.Length - 1)]++;

    public float DamageOf(string kind) => damage.TryGetValue(kind, out float amount) ? amount : 0f;

    /// <summary>The kind of blow a hit on the boss was, for the damage split.</summary>
    public static string KindOf(in DamageInfo hit)
    {
        HitTechnique technique = hit.Technique;
        if (hit.Type == DamageType.Fire)
        {
            return "fire";
        }

        // A toppled boss's heart strike: a finisher of its own, with no attack (MonsterAI.HeartStrike).
        if ((technique & HitTechnique.Finisher) != 0 && hit.Attack == null)
        {
            return "heart";
        }

        if ((technique & HitTechnique.Backstab) != 0) return "backstab";
        if ((technique & HitTechnique.WeaponArt) != 0) return "art";
        if ((technique & HitTechnique.Charged) != 0) return "charged";
        if ((technique & HitTechnique.Heavy) != 0) return "heavy";
        if ((technique & (HitTechnique.Counter | HitTechnique.GuardCounter)) != 0) return "counter";
        if ((technique & HitTechnique.Shot) != 0) return "arrow";
        if ((technique & HitTechnique.Spell) != 0) return "spell";
        if ((technique & HitTechnique.Melee) != 0) return "light";
        return "other";
    }

    /// <summary>The player's hit took <paramref name="healthLost"/> from the boss.</summary>
    public void Dealt(in DamageInfo hit, float healthLost)
    {
        if (healthLost <= 0f)
        {
            return;
        }

        string kind = KindOf(hit);
        damage[kind] = DamageOf(kind) + healthLost;
        if (kind == "heart")
        {
            HeartStrikes++;
        }
    }

    /// <summary>The row's detail: "died; 250 s; stage 1; topples blows 0 parry 1 ...; damage light 520 ...".</summary>
    public string Detail(string outcome, float now)
    {
        var text = new StringBuilder();
        text.Append(outcome).Append("; ").Append(Woke ? Mathf.RoundToInt(now - WokeAt) : 0).Append(" s; stage ").Append(Stage + 1);
        text.Append("; topples");
        for (int i = 0; i < topples.Length; i++)
        {
            text.Append(' ').Append(((ToppleCause)i).ToString().ToLowerInvariant()).Append(' ').Append(topples[i]);
        }

        text.Append("; heart ").Append(HeartStrikes).Append("; interrupts ").Append(Interrupts).Append("; calls ").Append(Calls)
            .Append("; summons killed ").Append(SummonsKilled).Append("; damage");
        foreach (string kind in Kinds)
        {
            text.Append(' ').Append(kind).Append(' ').Append(Mathf.RoundToInt(DamageOf(kind)));
        }

        text.Append("; draughts ").Append(Draughts).Append("; last blow ").Append(LastBlowOnPlayer);
        return text.ToString();
    }
}
