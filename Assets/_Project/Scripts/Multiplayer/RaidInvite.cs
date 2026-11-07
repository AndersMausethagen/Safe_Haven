using System;
using System.Collections.Generic;

/// <summary>
/// A host's raid, as friends need it to follow (co-op): everything that decides the run, so a guest's game builds the
/// same session and generates the same map from the same seed. What the host paid (Portal Sparks, modifier items) was
/// paid once, by the host; followers bring only their own gear. Sent by <see cref="CoopSession"/> when the host goes
/// through the portal.
/// </summary>
[Serializable]
public sealed class RaidInvite
{
    public string raid;
    public int seed;
    /// <summary>The difficulty's tier, or 0 for a raid without tiers.</summary>
    public int tier;
    public List<string> modifiers = new List<string>();
    public int sparks;
    public bool large;
    /// <summary>Seconds on top of the raid's rules: sparks, residents' jobs, readiness, a large raid.</summary>
    public float extraSeconds;
    public float extractionCut;
    public int foretoldEvents;
    /// <summary>The run's whole length, for how long friends can still follow.</summary>
    public float totalSeconds;
    /// <summary>Seconds of the run already gone when this was sent (a friend joining while the host is out).</summary>
    public float elapsed;

    public static RaidInvite Of(RaidSession session, float extraSeconds, float extractionCut, int foretoldEvents)
    {
        var invite = new RaidInvite
        {
            raid = session.Definition.Id,
            seed = session.Seed,
            tier = session.Difficulty != null ? session.Difficulty.tier : 0,
            sparks = session.SparksUsed,
            large = session.IsLarge,
            extraSeconds = extraSeconds,
            extractionCut = extractionCut,
            foretoldEvents = foretoldEvents,
            totalSeconds = session.TotalSeconds,
        };
        foreach (RaidModifierDefinition modifier in session.Modifiers)
        {
            if (modifier != null)
            {
                invite.modifiers.Add(modifier.Id);
            }
        }

        return invite;
    }

    /// <summary>The raid among those a portal leads to, or null.</summary>
    public RaidDefinition FindRaid(IEnumerable<RaidDefinition> destinations)
    {
        foreach (RaidDefinition candidate in destinations)
        {
            if (candidate != null && candidate.Id == raid)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The run's difficulty in <paramref name="definition"/>, or its lowest.</summary>
    public RaidDifficulty DifficultyIn(RaidDefinition definition)
    {
        foreach (RaidDifficulty difficulty in definition.Difficulties)
        {
            if (difficulty != null && difficulty.tier == tier)
            {
                return difficulty;
            }
        }

        return definition.LowestDifficulty;
    }

    /// <summary>The run's modifiers, found through the items that carry them.</summary>
    public List<RaidModifierDefinition> ModifiersFrom(ContentDatabase content)
    {
        var found = new List<RaidModifierDefinition>();
        foreach (ItemDefinition item in content.Items)
        {
            RaidModifierDefinition modifier = item != null && item.IsRaidModifier ? item.RaidModifier : null;
            if (modifier != null && modifiers.Contains(modifier.Id) && !found.Contains(modifier))
            {
                found.Add(modifier);
            }
        }

        return found;
    }
}
