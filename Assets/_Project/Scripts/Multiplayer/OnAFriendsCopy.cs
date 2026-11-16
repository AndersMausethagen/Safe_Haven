using System;

/// <summary>
/// What a creature's component does on a friend's copy of the host's creature (co-op: friends' games show the host's creatures as
/// puppets, MonsterAI.MakeRemote). Every component a creature carries says which (<see cref="OnAFriendsCopyAttribute"/>), and
/// CopyRuleTests fails on one that doesn't: a new part of a creature is thought through for co-op once, where it is written, instead
/// of a check in each system (Docs/BugPatterns.md, guard 5: a friend's poison ticked on the copy and on the host's creature).
/// </summary>
public enum CopyRule
{
    /// <summary>Runs alike in every game: how it looks and sounds, its stats, or rules that read what the host sends (poses, health,
    /// strikes).</summary>
    Same,
    /// <summary>What it does here is this player's own and goes to the host's creature (CreaturePuppets): their blows, their fire.</summary>
    Forwarded,
    /// <summary>The host's game decides it; on a copy it stands down. It must be an <see cref="ICopyAware"/>, told when its creature
    /// becomes a copy and when it is taken back.</summary>
    HostOnly,
    /// <summary>The creature itself (MonsterAI): on a copy it thinks nothing and follows the host's poses.</summary>
    FollowsTheHost,
}

/// <summary>What this creature component does on a friend's copy of the host's creature, and how (see <see cref="CopyRule"/>).</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class OnAFriendsCopyAttribute : Attribute
{
    public OnAFriendsCopyAttribute(CopyRule rule, string how)
    {
        Rule = rule;
        How = how;
    }

    public CopyRule Rule { get; }

    public string How { get; }
}

/// <summary>
/// A creature component the host's game decides (<see cref="CopyRule.HostOnly"/>): MonsterAI tells it when its creature becomes a
/// friend's copy (MakeRemote) and when it is taken back into this game (MakeLocal, the host left), so it never asks IsRemote itself.
/// </summary>
public interface ICopyAware
{
    void OnCopyChanged(bool isCopy);
}
