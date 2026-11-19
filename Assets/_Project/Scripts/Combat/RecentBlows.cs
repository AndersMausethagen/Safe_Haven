using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What brought the player down, blow by blow (Docs/SystemsGapReview_2026-11-19.md §7a; research G3's other half): the last three blows
/// on this game's player that took at least a twentieth of their health, each with who struck, with what and for how much, and whether
/// it was a great blow, one no guard stops, from behind or from out of view. The raid's report shows them under "Brought down by", so a
/// death says what to do differently. Kept from the raid's start (cleared then) to its end (taken then).
/// </summary>
public static class RecentBlows
{
    public const int Kept = 3;
    private const float LeastShare = 0.05f;
    private static readonly List<string> blows = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        blows.Clear();
        Damageable.AnyDamaged -= OnAnyDamaged;
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    /// <summary>Forgets what was kept (a raid starts, another game is loaded).</summary>
    public static void Clear() => blows.Clear();

    /// <summary>The blows kept, oldest first, and forgets them.</summary>
    public static List<string> Take()
    {
        var taken = new List<string>(blows);
        blows.Clear();
        return taken;
    }

    private static void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        Player player = Player.Current;
        if (player == null || victim != player.Health || result.Blocked || result.Damage < victim.MaxHealth * LeastShare)
        {
            return;
        }

        Note(Describe(damage, result, player.transform, Camera.main));
    }

    /// <summary>Keeps a blow's line, the oldest going past <see cref="Kept"/>.</summary>
    public static void Note(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        blows.Add(line);
        while (blows.Count > Kept)
        {
            blows.RemoveAt(0);
        }
    }

    /// <summary>One blow in words: "A Draug's Cleave, 34, a great blow, from behind".</summary>
    public static string Describe(in DamageInfo damage, in DamageResult result, Transform player, Camera view)
    {
        string who = damage.Source == null ? "Something"
            : damage.Source.TryGetComponent(out MonsterAI monster) && monster.Definition != null ? $"A {monster.Definition.DisplayName}"
            : damage.Source.name;
        string with = damage.Attack != null ? $"'s {damage.Attack.DisplayName}" : "";
        var how = new List<string>();
        if (damage.Attack != null && damage.Attack.Impact >= CombatFeelSettings.Current.HeavyImpact)
        {
            how.Add("a great blow");
        }

        if (damage.Attack != null && damage.Attack.Unblockable)
        {
            how.Add("no guard stops it");
        }

        if (player != null && damage.Source != null)
        {
            Vector3 toStriker = damage.Source.transform.position - player.position;
            toStriker.y = 0f;
            if (toStriker.sqrMagnitude > 0.01f && Vector3.Dot(player.forward, toStriker.normalized) < -0.5f)
            {
                how.Add("from behind");
            }

            if (view != null && OffScreenWarnings.IsOffScreen(view.WorldToViewportPoint(damage.Source.transform.position + Vector3.up * 1.2f)))
            {
                how.Add("from out of view");
            }
        }

        return $"{who}{with}, {Mathf.RoundToInt(result.Damage)}{(how.Count > 0 ? ", " + string.Join(", ", how) : "")}";
    }
}
