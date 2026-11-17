using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rules of the Lost, in plain C# so they can be tested: when they may rise, who is born and what it is called, which one walks a
/// raid (from the raid's seed, so the briefing can tell it before the portal opens), how one grows when it kills the player, what the
/// player learns when one flees, what one leaves when it is laid to rest, and which of the player's things it keeps. Nothing here reads
/// the scene or the session: RaidLostOnes gives it what it needs.
/// </summary>
public static class LostOneRules
{
    /// <summary>Whether the Lost may rise and walk: switched on, past the story gate and after enough raids come home from.</summary>
    public static bool GateOpen(LostOneSettings settings, QuestLog quests, int extractions)
    {
        if (settings == null || !settings.Enabled || extractions < settings.MinimumExtractions)
        {
            return false;
        }

        QuestCondition gate = settings.StoryGate;
        return gate.Quest == null || (quests != null && gate.IsMetBy(quests));
    }

    /// <summary>
    /// A homecoming's rising: fills the roster up to its size with new ones, while the gate is open. A place freed by one laid to rest
    /// stays empty for Homecomings Before A New One. <paramref name="freshSeed"/> becomes the roster's seed when it has none yet.
    /// Returns the ones that rose.
    /// </summary>
    public static List<LostOneRecord> Refill(LostOneRoster roster, LostOneSettings settings, bool gateOpen, int freshSeed)
    {
        var risen = new List<LostOneRecord>();
        if (roster == null || settings == null || !gateOpen)
        {
            return risen;
        }

        if (roster.Seed == 0)
        {
            roster.Seed = freshSeed != 0 ? freshSeed : 1;
        }

        int heldOpen = 0;
        foreach (LostOneRecord record in roster.All)
        {
            if (!record.IsWalking && roster.Homecomings - record.RestedAtHomecoming < settings.HomecomingsBeforeANewOne)
            {
                heldOpen++;
            }
        }

        int open = settings.RosterSize - roster.WalkingCount - heldOpen;
        for (int i = 0; i < open; i++)
        {
            LostOneRecord record = Generate(roster, settings);
            roster.Add(record);
            risen.Add(record);
        }

        return risen;
    }

    /// <summary>
    /// The roster's next one, at rank 1 with its first traits, born from the roster's seed and its number (the same roster makes the same
    /// one). Its given name is one no other of the Lost has had while one is left; its epithet and past life none walking shares.
    /// Counts it as risen; the caller adds it.
    /// </summary>
    public static LostOneRecord Generate(LostOneRoster roster, LostOneSettings settings)
    {
        int number = roster.Risen + 1;
        var random = new System.Random(RaidLayout.SeedFor(roster.Seed, "Lost " + number));
        var names = new List<string>();
        var epithets = new List<string>();
        var pasts = new List<string>();
        foreach (LostOneRecord other in roster.All)
        {
            names.Add(other.GivenName);
            if (other.IsWalking)
            {
                epithets.Add(other.Epithet);
                pasts.Add(other.PastLife);
            }
        }

        var record = new LostOneRecord
        {
            Id = "lost-" + number,
            GivenName = Clean(PickUnused(settings.GivenNames, names, random) ?? settings.Nameless),
            Epithet = Clean(PickUnused(settings.Epithets, epithets, random) ?? ""),
            PastLife = Clean(PickUnused(settings.PastLives, pasts, random) ?? ""),
            Rank = 1,
        };
        GainTraits(record, settings, random);
        roster.Risen = number;
        return record;
    }

    /// <summary>
    /// Which of the walking ones walks a raid, or null: one is chosen (a higher rank likelier), then rolls the raid tier's chance (more
    /// for a higher rank). Both from the raid's seed, so the briefing and the raid agree. Never one in a boss encounter, never two.
    /// </summary>
    public static LostOneRecord WhoWalks(LostOneRoster roster, LostOneSettings settings, int raidSeed, int tier, bool isEncounter)
    {
        if (roster == null || settings == null || !settings.Enabled || isEncounter || !settings.HasABody)
        {
            return null;
        }

        var walking = new List<LostOneRecord>();
        foreach (LostOneRecord record in roster.All)
        {
            if (record.IsWalking)
            {
                walking.Add(record);
            }
        }

        if (walking.Count == 0)
        {
            return null;
        }

        // Kept in the order they rose, which a save keeps: the same seed picks the same one.
        var random = new System.Random(RaidLayout.SeedFor(raidSeed, "The Lost"));
        var weights = new List<float>();
        foreach (LostOneRecord record in walking)
        {
            weights.Add(1f + settings.RankWeight * (Mathf.Clamp(record.Rank, 1, settings.MaxRank) - 1));
        }

        int index = RaidLayout.PickWeighted(random, weights);
        if (index < 0)
        {
            return null;
        }

        LostOneRecord chosen = walking[index];
        // One with no body for its rank (no grade reaches it and no fallback) can't walk: the briefing mustn't promise it.
        return random.NextDouble() < settings.ChanceFor(tier, chosen.Rank) && settings.BodyFor(chosen) != null ? chosen : null;
    }

    /// <summary>
    /// It killed the player: it rises a rank (to the highest), gains the traits its new rank has room for, and keeps the most valuable of
    /// what it took with what it already carried. True when it rose.
    /// </summary>
    public static bool RiseOnKill(LostOneRecord record, IEnumerable<ItemStack> taken, LostOneSettings settings, System.Random random)
    {
        record.Kills++;
        bool rose = record.Rank < settings.MaxRank;
        record.Rank = Mathf.Clamp(record.Rank + 1, 1, settings.MaxRank);
        GainTraits(record, settings, random);
        var all = new List<ItemStack>(record.Carried);
        if (taken != null)
        {
            all.AddRange(taken);
        }

        record.Carried.Clear();
        record.Carried.AddRange(MostValuable(all, settings.CarriedCap));
        return rose;
    }

    /// <summary>Whether it may flee this raid: not twice, never from the highest rank (it fights to the end), and only when fleeing is on.</summary>
    public static bool CanFlee(LostOneRecord record, LostOneSettings settings, bool fledThisRaid) =>
        record != null && record.IsWalking && !fledThisRaid && settings.FleeBelow > 0f && record.Rank < settings.MaxRank;

    /// <summary>It fled: one more escape (a scar it comes back with) and the player learns one of its traits not yet seen through. Returns that trait's name, or null.</summary>
    public static string Escape(LostOneRecord record, System.Random random)
    {
        record.Escapes++;
        var unknown = record.Traits.FindAll(trait => !record.Knows(trait));
        if (unknown.Count == 0)
        {
            return null;
        }

        string learned = unknown[random.Next(unknown.Count)];
        record.Learned.Add(learned);
        return learned;
    }

    /// <summary>The share of health its scars add (Escapes × Scar Per Escape, up to Most Scar).</summary>
    public static float ScarShare(LostOneRecord record, LostOneSettings settings) =>
        Mathf.Min(settings.MostScar, Mathf.Max(0, record.Escapes) * settings.ScarPerEscape);

    /// <summary>
    /// It is laid to rest: it walks no more, every trait is known, the journal has its epitaph and its place is held open from this
    /// homecoming. Returns what it carried (now dropped where it fell), and carries nothing.
    /// </summary>
    public static List<ItemStack> LayToRest(LostOneRecord record, int homecomings, string raidName, LostOneSettings settings)
    {
        var dropped = new List<ItemStack>(record.Carried);
        record.Carried.Clear();
        record.Status = LostOneStatus.LaidToRest;
        record.RestedAtHomecoming = homecomings;
        foreach (string trait in record.Traits)
        {
            if (!record.Knows(trait))
            {
                record.Learned.Add(trait);
            }
        }

        record.Epitaph = Say(settings.Epitaph, record, raidName);
        return dropped;
    }

    /// <summary>
    /// What a death took from the player's hands, stack by stack: what they held at the end (<paramref name="final"/>) beyond what comes
    /// home (<paramref name="keeps"/>: the safe pocket and the deployed gear). Bag stacks are taken before worn ones, so a worn piece that
    /// is kept isn't taken for a copy found out there. Used-up things aren't in it.
    /// </summary>
    public static List<ItemStack> TakenStacks(PlayerSnapshot final, PlayerSnapshot keeps)
    {
        var taken = new List<ItemStack>();
        if (final == null)
        {
            return taken;
        }

        Dictionary<ItemDefinition, int> kept = keeps != null ? keeps.CountItems() : new Dictionary<ItemDefinition, int>();
        var lost = new Dictionary<ItemDefinition, int>();
        foreach (KeyValuePair<ItemDefinition, int> held in final.CountItems())
        {
            kept.TryGetValue(held.Key, out int stays);
            if (held.Value > stays)
            {
                lost[held.Key] = held.Value - stays;
            }
        }

        void Take(IReadOnlyList<PlayerSnapshot.SlotRecord> slots)
        {
            foreach (PlayerSnapshot.SlotRecord slot in slots)
            {
                ItemStack stack = slot.Stack;
                if (stack.IsEmpty || !lost.TryGetValue(stack.Item, out int left) || left <= 0)
                {
                    continue;
                }

                int count = Math.Min(left, stack.Count);
                taken.Add(stack.WithCount(count));
                lost[stack.Item] = left - count;
            }
        }

        Take(final.Slots);
        Take(final.Gear);
        return taken;
    }

    /// <summary>The <paramref name="cap"/> most valuable stacks (value × count, then rarity), most valuable first.</summary>
    public static List<ItemStack> MostValuable(IEnumerable<ItemStack> stacks, int cap)
    {
        var all = new List<ItemStack>();
        foreach (ItemStack stack in stacks)
        {
            if (!stack.IsEmpty && stack.Count > 0)
            {
                all.Add(stack);
            }
        }

        all.Sort((a, b) =>
        {
            int byValue = (b.Item.Value * b.Count).CompareTo(a.Item.Value * a.Count);
            return byValue != 0 ? byValue : b.Item.Rarity.CompareTo(a.Item.Rarity);
        });
        if (all.Count > Math.Max(0, cap))
        {
            all.RemoveRange(Math.Max(0, cap), all.Count - Math.Max(0, cap));
        }

        return all;
    }

    /// <summary>The briefing's line for a run it walks, or null when the player hasn't met it (it comes unannounced).</summary>
    public static string BriefingLine(LostOneRecord record, string raidName, LostOneSettings settings)
    {
        if (record == null || !record.Met)
        {
            return null;
        }

        string line = Say(settings.BriefingLine, record, raidName);
        return record.Carried.Count > 0 && !string.IsNullOrWhiteSpace(settings.BriefingCarriesLine) ? $"{line} {Say(settings.BriefingCarriesLine, record, raidName)}" : line;
    }

    /// <summary>One of a list of lines, or empty when there are none.</summary>
    public static string Line(IReadOnlyList<string> lines, System.Random random)
    {
        if (lines == null || lines.Count == 0)
        {
            return "";
        }

        return lines[random.Next(lines.Count)] ?? "";
    }

    /// <summary>A line with its words filled in: {name}, {past}, {raid} ("the Misty Woods") and {trait}.</summary>
    public static string Say(string template, LostOneRecord record, string raidName, string trait = "")
    {
        if (string.IsNullOrEmpty(template))
        {
            return "";
        }

        return template.Replace("{name}", record.FullName).Replace("{past}", record.PastLife).Replace("{raid}", Place(raidName)).Replace("{trait}", trait ?? "");
    }

    /// <summary>A raid's name as a place in a sentence: "the Misty Woods" ("The ..." names stay as they are).</summary>
    public static string Place(string raidName)
    {
        if (string.IsNullOrWhiteSpace(raidName))
        {
            return "the mist";
        }

        return raidName.StartsWith("The ", StringComparison.Ordinal) || raidName.StartsWith("the ", StringComparison.Ordinal) ? raidName : "the " + raidName;
    }

    // Up to its rank's count, from those usable at its rank as a tier that it hasn't got, by weight (RaidElites.PickAffixes).
    private static void GainTraits(LostOneRecord record, LostOneSettings settings, System.Random random)
    {
        int wanted = settings.RankAt(record.Rank).traits - record.Traits.Count;
        if (wanted <= 0)
        {
            return;
        }

        var pool = new List<EliteAffixDefinition>();
        foreach (EliteAffixDefinition trait in settings.Traits)
        {
            if (trait != null && !record.Traits.Contains(trait.name))
            {
                pool.Add(trait);
            }
        }

        foreach (EliteAffixDefinition trait in RaidElites.PickAffixes(pool, Mathf.Clamp(record.Rank, 1, Tiers.Max), wanted, random))
        {
            record.Traits.Add(trait.name);
        }
    }

    // A random one not in use, or any when all are; null from an empty list.
    private static string PickUnused(IReadOnlyList<string> options, List<string> used, System.Random random)
    {
        var free = new List<string>();
        var any = new List<string>();
        foreach (string option in options)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                continue;
            }

            any.Add(option);
            if (!used.Contains(option))
            {
                free.Add(option);
            }
        }

        List<string> from = free.Count > 0 ? free : any;
        return from.Count > 0 ? from[random.Next(from.Count)] : null;
    }

    // Names travel to friends packed with the affixes (LostOne.Pack): their marks can't be in one.
    private static string Clean(string text) => text.Replace("|", "").Replace(LostOne.NameMark.ToString(), "").Trim();
}
