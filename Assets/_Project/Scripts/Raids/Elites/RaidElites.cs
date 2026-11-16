using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which creatures of a raid become elites, and with what. Elites at the start are picked from the raid's seed, so a run is the same for the
/// same seed; hunters sent later roll a chance. The choosing is plain C#, so it can be tested. RaidDirector calls it.
/// </summary>
public static class RaidElites
{
    private const float KeptFromStart = 25f;

    /// <summary>Picks up to <paramref name="count"/> different affixes usable at a tier, by weight.</summary>
    public static List<EliteAffixDefinition> PickAffixes(IReadOnlyList<EliteAffixDefinition> pool, int tier, int count, System.Random random)
    {
        var usable = new List<EliteAffixDefinition>();
        var weights = new List<float>();
        foreach (EliteAffixDefinition affix in pool)
        {
            if (affix != null && affix.FromTier <= tier && affix.Weight > 0f)
            {
                usable.Add(affix);
                weights.Add(affix.Weight);
            }
        }

        var chosen = new List<EliteAffixDefinition>();
        while (chosen.Count < count && usable.Count > 0)
        {
            int index = RaidLayout.PickWeighted(random, weights);
            if (index < 0)
            {
                break;
            }

            chosen.Add(usable[index]);
            usable.RemoveAt(index);
            weights.RemoveAt(index);
        }

        return chosen;
    }

    /// <summary>Whether a creature can be made an elite: a fighter of the enemy side, and not one its definition rules out.</summary>
    public static bool CanPromote(MonsterAI creature)
    {
        // Never a friend's copy of the host's creature (co-op): it is only an elite where the host's game made it one.
        return creature != null && !creature.IsRemote && creature.Definition != null && !creature.Definition.NeverElite
            && creature.gameObject.activeInHierarchy && creature.CurrentState != MonsterAI.State.Dead
            && creature.TryGetComponent(out Combatant side) && side.Faction == Faction.Enemy && creature.GetComponent<EliteCreature>() == null;
    }

    /// <summary>Promotes the elites a run starts with. Returns how many.</summary>
    public static int PromoteAtStart(RaidSession session, Vector3? start, float healthScale, float powerScale)
    {
        RaidDifficulty tier = session.Difficulty;
        if (tier == null || tier.eliteCount.y <= 0 || session.Definition.EliteAffixes.Count == 0)
        {
            return 0;
        }

        var random = new System.Random(RaidLayout.SeedFor(session.Seed, "Elites"));
        var candidates = new List<MonsterAI>();
        foreach (MonsterAI creature in Object.FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            bool nearStart = start.HasValue && (creature.transform.position - start.Value).sqrMagnitude < KeptFromStart * KeptFromStart;
            if (CanPromote(creature) && !nearStart)
            {
                candidates.Add(creature);
            }
        }

        // Found in no fixed order, so sorted for the seed to mean the same run every time.
        candidates.Sort((a, b) =>
        {
            int byName = string.CompareOrdinal(a.Definition.name, b.Definition.name);
            return byName != 0 ? byName : a.transform.position.sqrMagnitude.CompareTo(b.transform.position.sqrMagnitude);
        });
        int wanted = random.Next(tier.eliteCount.x, tier.eliteCount.y + 1);
        List<int> picks = RaidLayout.PickIndices(random, candidates.Count, Mathf.Min(wanted, candidates.Count));
        foreach (int pick in picks)
        {
            EliteCreature.Make(candidates[pick], PickAffixes(session.Definition.EliteAffixes, tier.tier, tier.eliteAffixes, random), tier, healthScale, powerScale);
        }

        return picks.Count;
    }

    /// <summary>Rolls whether one creature of a group sent later is an elite. Returns it, or null.</summary>
    public static EliteCreature RollHunter(RaidSession session, GameObject root, float healthScale, float powerScale)
    {
        RaidDifficulty tier = session.Difficulty;
        if (tier == null || tier.hunterEliteChance <= 0f || session.Definition.EliteAffixes.Count == 0 || Random.value >= tier.hunterEliteChance)
        {
            return null;
        }

        foreach (MonsterAI creature in root.GetComponentsInChildren<MonsterAI>(false))
        {
            if (CanPromote(creature))
            {
                var random = new System.Random(Random.Range(0, int.MaxValue));
                return EliteCreature.Make(creature, PickAffixes(session.Definition.EliteAffixes, tier.tier, tier.eliteAffixes, random), tier, healthScale, powerScale);
            }
        }

        return null;
    }
}
