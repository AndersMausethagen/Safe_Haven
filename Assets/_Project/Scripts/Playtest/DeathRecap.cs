using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The player's last seconds, for the DeathRecap row (the user's playtest, 2026-11-25: blows under 5% of health left no row, so how fast
/// a crowd brought them down could only be guessed). Keeps every blow of the last <see cref="Seconds"/>, small ones and ticks too; plain
/// C#, PlaytestLogger's. Tested in CombatSafetyTests.
/// </summary>
public sealed class DeathRecap
{
    public const float Seconds = 10f;
    // Under this share of health a blow is "small" (the Hurt rows' threshold).
    private const float SmallShare = 0.05f;

    private struct Blow
    {
        public float At;
        public string By;
        public float Lost;
        public float HealthBefore;
        public float MaxHealth;
        public bool Tick;
    }

    private readonly List<Blow> blows = new List<Blow>();

    /// <summary>A blow on the player at <paramref name="at"/>: who, the health it took, the health before it and the most.</summary>
    public void Note(float at, string by, float lost, float healthBefore, float maxHealth, bool tick)
    {
        Prune(at);
        if (lost <= 0f)
        {
            return;
        }

        blows.Add(new Blow { At = at, By = string.IsNullOrEmpty(by) ? "unknown" : by, Lost = lost, HealthBefore = healthBefore, MaxHealth = maxHealth, Tick = tick });
    }

    public void Clear() => blows.Clear();

    /// <summary>Health lost in the last <see cref="Seconds"/> before <paramref name="now"/>.</summary>
    public float LostWithin(float now)
    {
        Prune(now);
        float lost = 0f;
        foreach (Blow blow in blows)
        {
            lost += blow.Lost;
        }

        return lost;
    }

    /// <summary>"10 s: 9 blows (4 small, 1 tick) over 6.2 s; ForestBittenWolf 69, MarshLight 30; from 120/182; crowd 6". Names lose any
    /// ',' or ';' so the list reads apart.</summary>
    public string Describe(float now, int crowd)
    {
        Prune(now);
        if (blows.Count == 0)
        {
            return $"{Seconds:0} s: no blows; crowd {crowd}";
        }

        int small = 0, ticks = 0;
        var by = new Dictionary<string, float>();
        var order = new List<string>();
        foreach (Blow blow in blows)
        {
            if (blow.Tick) ticks++;
            else if (blow.MaxHealth > 0f && blow.Lost < blow.MaxHealth * SmallShare) small++;
            if (!by.ContainsKey(blow.By))
            {
                by[blow.By] = 0f;
                order.Add(blow.By);
            }

            by[blow.By] += blow.Lost;
        }

        order.Sort((a, b) => by[b].CompareTo(by[a]));
        var builder = new StringBuilder();
        builder.Append($"{Seconds:0} s: {Count(blows.Count, "blow")} ({small} small, {Count(ticks, "tick")}) over {blows[blows.Count - 1].At - blows[0].At:0.0} s; ");
        for (int i = 0; i < order.Count; i++)
        {
            builder.Append(i > 0 ? ", " : "").Append(order[i].Replace(';', '_').Replace(',', '_')).Append(' ').Append(Mathf.RoundToInt(by[order[i]]));
        }

        builder.Append($"; from {blows[0].HealthBefore:0}/{blows[0].MaxHealth:0}; crowd {crowd}");
        return builder.ToString();
    }

    private static string Count(int n, string what) => n == 1 ? $"1 {what}" : $"{n} {what}s";

    private void Prune(float now)
    {
        int old = 0;
        while (old < blows.Count && now - blows[old].At > Seconds)
        {
            old++;
        }

        if (old > 0)
        {
            blows.RemoveRange(0, old);
        }
    }
}
