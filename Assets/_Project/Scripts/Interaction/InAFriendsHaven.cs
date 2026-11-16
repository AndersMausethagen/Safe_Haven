using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// What a visitor may do with a thing in a friend's Haven (co-op). Every component that answers an Interactable says which
/// (<see cref="InAFriendsHavenAttribute"/>), and InteractionRuleTests fails on one that doesn't: a new interaction is thought
/// through for visitors where it is written (Docs/BugPatterns.md, guard 3: a guest collected the host's workshop jobs and
/// duplicated items, because nothing asked).
/// </summary>
public enum HavenUse
{
    /// <summary>Theirs to do anywhere: gathering, picking up, eating, talking, their own gear and horses.</summary>
    Anyone,
    /// <summary>Allowed with the host's yes (HavenVisit: building, the stash, carts' cargo).</summary>
    AsksTheHost,
    /// <summary>Opens for a visitor, and each choice that is the host's refuses itself (HavenVisit.LeftToHost): a station's jobs, a
    /// trade, the portal's raids.</summary>
    ItsChoicesDecide,
    /// <summary>The host's alone: refused to a visitor at once, by Interactable (HavenVisit.Refusal), without a check of its own.</summary>
    HostOnly,
    /// <summary>Never in a Haven (a raid's or the prologue's): the raid's own co-op rules hold.</summary>
    NotInAHaven,
}

/// <summary>What a visitor may do with this in a friend's Haven, and how (see <see cref="HavenUse"/>).</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class InAFriendsHavenAttribute : Attribute
{
    public InAFriendsHavenAttribute(HavenUse use, string how)
    {
        Use = use;
        How = how;
    }

    public HavenUse Use { get; }

    public string How { get; }

    // Each type's rule, read once.
    private static readonly Dictionary<Type, HavenUse> Rules = new Dictionary<Type, HavenUse>();

    /// <summary>The rule declared on <paramref name="type"/> (Anyone when it declares none: InteractionRuleTests keeps that from
    /// happening for anything that answers an Interactable).</summary>
    public static HavenUse Of(Type type)
    {
        if (!Rules.TryGetValue(type, out HavenUse use))
        {
            use = type.GetCustomAttribute<InAFriendsHavenAttribute>()?.Use ?? HavenUse.Anyone;
            Rules[type] = use;
        }

        return use;
    }

    // Entering Play doesn't reload scripts in this project: statics carry over unless cleared.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Rules.Clear();
}
