using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Looks out, while the game is played, for what is held and never let go: invulnerability (a dodge, a hop, a finishing blow),
/// the gameplay keys held off, the player slowed or sped up, a floor under someone's health. Each is held by an owner and let go
/// by it; when the let-go is missed (an interruption no one thought of) the player is stuck invulnerable, slow or without keys
/// for good. Held far longer than it ever should be, or by something that is gone, it is told once: a warning in the player's
/// log and a "Stuck" row in the playtest log (PlaytestLogger), so real runs find the leaks the reviews had to read the code for
/// (Docs/BugPatterns.md, second round: a creature staggered mid-hop stayed invulnerable, so did a player whose finishing blow's
/// creature was destroyed).
/// </summary>
public sealed class LeaseWatch
{
    /// <summary>What can be held, and how long it may be before it is a leak (seconds; 0: only a gone owner counts).</summary>
    public enum Kind
    {
        Invulnerable,
        GameplayKeysOff,
        SpeedChanged,
        HealthFloor,
    }

    private static float LongestFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Invulnerable:
                return 20f; // dodges, hops and finishing blows last a second or two; a cinematic's hold pauses the game
            case Kind.SpeedChanged:
                return 180f; // a swing, crafting by hand (a weapon takes 20 s), a status
            default:
                return 0f; // a window open, a Draug's floor: as long as they like, as long as their owner is there
        }
    }

    private readonly Dictionary<(Kind, object holder, object owner), float> since = new Dictionary<(Kind, object, object), float>();
    private readonly HashSet<(Kind, object, object)> told = new HashSet<(Kind, object, object)>();
    private readonly HashSet<(Kind, object, object)> seen = new HashSet<(Kind, object, object)>();
    private readonly List<(Kind, object, object)> gone = new List<(Kind, object, object)>();
    private readonly List<string> found = new List<string>();

    /// <summary>The leaks found since the last look, each told once. <paramref name="now"/> is unscaled time.</summary>
    public IReadOnlyList<string> Look(float now)
    {
        found.Clear();
        seen.Clear();
        Player player = Player.Current;
        if (player != null)
        {
            if (player.Health != null)
            {
                Judge(Kind.Invulnerable, player, player.Health.InvulnerabilityHolders, now);
                Judge(Kind.HealthFloor, player, player.Health.FloorHolders, now);
            }

            if (player.TryGetComponent(out PlayerMovement movement))
            {
                Judge(Kind.SpeedChanged, player, movement.SpeedScaleHolders, now);
            }
        }

        foreach (MonsterAI creature in MonsterAI.Active)
        {
            if (creature != null && creature.TryGetComponent(out Damageable health) && health.IsAlive)
            {
                Judge(Kind.Invulnerable, creature, health.InvulnerabilityHolders, now);
                Judge(Kind.HealthFloor, creature, health.FloorHolders, now);
            }
        }

        Judge(Kind.GameplayKeysOff, typeof(GameInput), GameInput.GameplaySuspenders, now);

        // Let go: forgotten, so a later hold counts from its own start.
        gone.Clear();
        foreach (var key in since.Keys)
        {
            if (!seen.Contains(key))
            {
                gone.Add(key);
            }
        }

        foreach (var key in gone)
        {
            since.Remove(key);
            told.Remove(key);
        }

        return found;
    }

    /// <summary>Weighs what <paramref name="holder"/> has held by <paramref name="owners"/> (public for tests: any holder, any owners).</summary>
    public void Judge(Kind kind, object holder, IEnumerable<object> owners, float now)
    {
        if (owners == null)
        {
            return;
        }

        foreach (object owner in owners)
        {
            var key = (kind, holder, owner);
            seen.Add(key);
            if (!since.TryGetValue(key, out float from))
            {
                since[key] = from = now;
            }

            bool ownerGone = owner is Object unityOwner && unityOwner == null;
            float longest = LongestFor(kind);
            if ((ownerGone || (longest > 0f && now - from > longest)) && told.Add(key))
            {
                found.Add($"{kind} on {NameOf(holder)} for {now - from:0} s, held by {(ownerGone ? "something gone (destroyed)" : NameOf(owner))}");
            }
        }
    }

    private static string NameOf(object thing) =>
        thing is Object unity ? (unity != null ? unity.name : "(destroyed)") : thing is System.Type type ? type.Name : thing?.GetType().Name ?? "nothing";
}
