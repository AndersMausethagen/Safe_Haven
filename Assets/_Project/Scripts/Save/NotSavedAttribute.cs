using System;

/// <summary>
/// A field of saved state (SettlementState, QuestLog, CharacterProgress and what they hold) that is left out of the save
/// on purpose, with why: worked out again on loading, only what this game sees, a cache. SaveCoverageTests gives every
/// other field a value, saves and loads it, and fails when it doesn't come back (Docs/BugPatterns.md, guard 1), so a
/// field added and forgotten in SaveMapper is caught. On an auto-property: <c>[field: NotSaved("...")]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class NotSavedAttribute : Attribute
{
    public NotSavedAttribute(string why)
    {
        Why = why;
    }

    public string Why { get; }
}

/// <summary>
/// A SettlementState field of what this game sees standing where the player is (buildings, beds, modules, scaffolds), noted from the
/// scene: a guest taking on the host's word (SettlementState.TakeOn) keeps its own. Declared on the field, not kept as a list of names
/// beside it, which left the modules out, so a guest lost the host's fitted modules at the host's first word (review 2026-11-27).
/// <c>[field: SeenHere]</c> on an auto-property.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class SeenHereAttribute : Attribute
{
}
