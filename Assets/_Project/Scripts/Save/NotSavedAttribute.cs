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
