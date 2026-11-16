using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Marks a static method <c>HavenDecisions.Outcome Name(string args)</c> as one of the Haven's decisions, by name.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HavenDecisionAttribute : Attribute
{
    public HavenDecisionAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

/// <summary>
/// The Haven's own decisions, made where the Haven is kept: its residents' work, its ward, its portal, its altar. A decision is
/// one static method marked <see cref="HavenDecisionAttribute"/>, changing <see cref="GameSession.Here"/> by its own
/// rules and saying how it went. A feature pays what it costs from the player's own materials, then calls <see cref="Do"/>:
/// <list type="bullet">
/// <item>in this player's own Haven (alone, or hosting) the decision is made at once;</item>
/// <item>visiting a friend, it is made in the host's game for them (<see cref="CoopDecisions"/>): the same method runs there,
/// the settlement mirror brings them the result, and what they paid comes back if the host's game turns it down (someone got
/// there first).</item>
/// </list>
/// So a feature that changes the Haven needs no co-op code of its own. Plain C#.
/// </summary>
public static class HavenDecisions
{
    /// <summary>How a decision went: made (with what to tell the player) or refused (with why).</summary>
    public readonly struct Outcome
    {
        public readonly bool Done;
        public readonly string Text;

        private Outcome(bool done, string text)
        {
            Done = done;
            Text = text;
        }

        public static Outcome Made(string text = null) => new Outcome(true, text);

        public static Outcome Refused(string text) => new Outcome(false, text);
    }

    public delegate Outcome Decision(string args);

    private static Dictionary<string, Decision> decisions;

    /// <summary>Every decision there is, by name (found once, by its attribute).</summary>
    public static IReadOnlyDictionary<string, Decision> All => decisions ??= FindAll();

    /// <summary>
    /// Makes a decision for the Haven this player stands in, after they paid <paramref name="resources"/> and
    /// <paramref name="items"/> for it (given back if it is refused). True when it was made, or (visiting) asked of the host.
    /// </summary>
    public static bool Do(string name, string args, IEnumerable<ResourceAmount> resources = null, IEnumerable<ItemAmount> items = null)
    {
        if (!All.TryGetValue(name, out Decision decision))
        {
            Debug.LogError($"There is no Haven decision called '{name}' (mark its method [HavenDecision(\"{name}\")]).");
            return false;
        }

        string paid = Paid(resources, items);
        if (!HavenVisit.IsVisiting)
        {
            Outcome outcome = Run(decision, args);
            Say(outcome);
            if (!outcome.Done)
            {
                GiveBack(paid);
            }

            return outcome.Done;
        }

        if (CoopDecisions.Current == null || !CoopDecisions.Current.Ask(name, args, paid))
        {
            HavenVisit.LeftToHost();
            GiveBack(paid);
            return false;
        }

        return true;
    }

    /// <summary>Makes a decision a friend asked for, in this game (the host's).</summary>
    public static Outcome Make(string name, string args) =>
        All.TryGetValue(name, out Decision decision) ? Run(decision, args) : Outcome.Refused("Your friend's game doesn't know how to do that.");

    /// <summary>Tells the player how a decision went.</summary>
    public static void Say(Outcome outcome)
    {
        if (!string.IsNullOrEmpty(outcome.Text))
        {
            GameMessages.Post(outcome.Text, outcome.Done ? MessageKind.Gain : MessageKind.Warning);
        }
    }

    private static Outcome Run(Decision decision, string args)
    {
        try
        {
            return decision(args ?? "");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return Outcome.Refused("That didn't work.");
        }
    }

    private static Dictionary<string, Decision> FindAll()
    {
        var found = new Dictionary<string, Decision>();
        foreach (Type type in typeof(HavenDecisions).Assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!method.IsDefined(typeof(HavenDecisionAttribute), false))
                {
                    continue;
                }

                string name = method.GetCustomAttribute<HavenDecisionAttribute>().Name;
                if (found.ContainsKey(name))
                {
                    Debug.LogError($"Two Haven decisions are called '{name}'.");
                    continue;
                }

                found[name] = (Decision)Delegate.CreateDelegate(typeof(Decision), method);
            }
        }

        return found;
    }

    // ---- What was paid, as text: it travels with a friend's decision and comes back if the host's game refuses it.

    /// <summary>A payment as text ("r:wood:12;i:iron_nail:3"); empty for none.</summary>
    public static string Paid(IEnumerable<ResourceAmount> resources, IEnumerable<ItemAmount> items)
    {
        var text = new StringBuilder();
        foreach (ResourceAmount amount in resources ?? Array.Empty<ResourceAmount>())
        {
            if (amount.Resource != null && amount.Amount > 0)
            {
                text.Append(text.Length > 0 ? ";" : "").Append("r:").Append(amount.Resource.Id).Append(':').Append(amount.Amount.ToString(CultureInfo.InvariantCulture));
            }
        }

        foreach (ItemAmount amount in items ?? Array.Empty<ItemAmount>())
        {
            if (amount.Item != null && amount.Amount > 0)
            {
                text.Append(text.Length > 0 ? ";" : "").Append("i:").Append(amount.Item.Id).Append(':').Append(amount.Amount.ToString(CultureInfo.InvariantCulture));
            }
        }

        return text.ToString();
    }

    /// <summary>A payment read back (<see cref="Paid"/>); what the content no longer knows is left out.</summary>
    public static void ReadPaid(string paid, IContentLookup content, List<ResourceAmount> resources, List<ItemAmount> items)
    {
        foreach (string part in (paid ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] bits = part.Split(':');
            if (bits.Length != 3 || !int.TryParse(bits[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count <= 0)
            {
                continue;
            }

            if (bits[0] == "r" && content.FindResource(bits[1]) is ResourceDefinition resource)
            {
                resources.Add(new ResourceAmount(resource, count));
            }
            else if (bits[0] == "i" && content.FindItem(bits[1]) is ItemDefinition item)
            {
                items.Add(new ItemAmount(item, count));
            }
        }
    }

    /// <summary>Gives the player back a payment for a decision that was refused.</summary>
    public static void GiveBack(string paid)
    {
        Player player = Player.Current;
        if (string.IsNullOrEmpty(paid) || player == null)
        {
            return;
        }

        var resources = new List<ResourceAmount>();
        var items = new List<ItemAmount>();
        ReadPaid(paid, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName), resources, items);
        MaterialPool.For(player.gameObject).GiveBack(resources, items, player.transform);
    }
}
