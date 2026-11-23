using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Forays: a few residents sent through the portal to gather in a raid's land on their own while the Haven gets on without them (the
/// user, 2026-11-22: "being able to send NPC's on a gathering raid themselves, like Palworld does. With % chance of success, based in
/// their level, equipement, number of NPC's etc." and "use palworld as an inspiration"). As Palworld's expeditions: a place opens once
/// the portal opens its tier (Palworld: a boss beaten); it asks for a party power, which the party's own power is set against (each
/// resident brings Base Power, more for their worker rank, the raids they went along on, the worker gear they were given and a good
/// breakfast); more power than asked is a quicker trip and a bigger haul, less a slower one and a smaller haul, and the odds shown
/// before sending. The haul is chances, not promises. Away, they are gone from the Haven (ResidentRecord.IsAway: no station, no
/// building, no party, never along on a raid) for the minutes it takes, counted in played time at home or raiding; each takes food
/// for the road from the chests. Gone wrong, a share of the haul comes home and they are bruised (slower at work) until the next
/// dawn; nobody is lost. One foray at a time. Started from the Haven window's Lands tab; the rules are plain (ForaysTests).
/// </summary>
public static class Forays
{
    // What came home while the player was on a raid: told on coming home. (A foray's clock stops in a friend's Haven, as the
    // workshops do: SettlementSystem runs only in one's own.)
    private static readonly List<string> waiting = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => waiting.Clear();

    /// <summary>Forgets news held for the game being played (a new game, or a load: GameSession).</summary>
    public static void ForgetGame() => waiting.Clear();

    // ----- Who, and how strong -----

    /// <summary>The power one resident brings: the rank of their level in the land's work (its Skill; their job's, with none: ResidentTalents),
    /// their raids along, their gear and their breakfast.</summary>
    public static float PowerOf(ResidentRecord record, ForaySettings settings, ForaySettings.Destination place = null)
    {
        if (record == null)
        {
            return 0f;
        }

        ProficiencyDefinition skill = place != null && place.skill != null ? place.skill : ResidentTalents.JobSkill(record);
        int rank = Mathf.Max(0, ResidentTalents.RankIndexAt(ResidentTalents.LevelOf(record, skill)));
        int raids = Mathf.Min(record.RaidsAlong, settings.MostRaidsCounted);
        int gear = record.WorkGear != null ? Mathf.Max(1, record.WorkGear.Tier) : 0;
        return settings.BasePower + rank * settings.PowerPerRank + raids * settings.PowerPerRaid + gear * settings.PowerPerGearTier
            + (record.Meal == ResidentMeal.WellFed ? settings.WellFedPower : 0f);
    }

    /// <summary>True when <paramref name="record"/> could go: living here, not paused or away, and given no standing order elsewhere
    /// (a party to lead, an outpost to keep) that a foray would leave undone.</summary>
    public static bool CanGo(SettlementState home, ResidentRecord record, TerritorySettings lands)
    {
        return record != null && record.Resident != null && !record.WorkPaused && !Outposts.Keeps(home, record) && !LandParties.Leads(home, record, lands);
    }

    /// <summary>The <paramref name="count"/> strongest who could go, strongest first.</summary>
    public static List<ResidentRecord> Strongest(SettlementState home, int count, ForaySettings settings, TerritorySettings lands,
        ForaySettings.Destination place = null)
    {
        var free = new List<ResidentRecord>();
        foreach (ResidentRecord record in home.Residents)
        {
            if (CanGo(home, record, lands))
            {
                free.Add(record);
            }
        }

        free.Sort((a, b) => PowerOf(b, settings, place).CompareTo(PowerOf(a, settings, place)));
        return free.GetRange(0, Mathf.Min(count, free.Count));
    }

    /// <summary>
    /// A party's power: each one's own (PowerOf), times what their traits make of a party this size (a Loner alone or in a pair, less in
    /// a crowd), plus what a born leader among them adds to everyone else's, times what friends going together add (PeopleStandings).
    /// </summary>
    public static float PowerOf(IEnumerable<ResidentRecord> party, ForaySettings settings, ForaySettings.Destination place = null, IHavenView here = null)
    {
        var members = new List<ResidentRecord>(party);
        float led = 0f;
        foreach (ResidentRecord record in members)
        {
            led += ResidentTraits.Sum(record, TraitEffectKind.LeadsParty);
        }

        float power = 0f;
        foreach (ResidentRecord record in members)
        {
            float own = ResidentTraits.Sum(record, TraitEffectKind.LeadsParty);
            power += PowerOf(record, settings, place) * Mathf.Max(0f, 1f + ResidentTraits.ForayShare(record, members.Count) + (led - own));
        }

        return power * (1f + PeopleStandings.ForayBonus(members, here));
    }

    // ----- The odds, the time, the haul -----

    /// <summary>The odds of a foray with <paramref name="power"/> to a place asking <paramref name="asked"/>.</summary>
    public static float Chance(float power, float asked, ForaySettings settings)
    {
        float ratio = asked > 0f ? power / asked : 1f;
        return Mathf.Clamp(settings.LeastChance + (settings.ChanceAtPower - settings.LeastChance) * ratio, settings.LeastChance, settings.MostChance);
    }

    /// <summary>Seconds away: the place's minutes, shorter with more power than asked, longer with less.</summary>
    public static float Seconds(float power, ForaySettings.Destination place, ForaySettings settings)
    {
        float ratio = place.power > 0f ? Mathf.Max(0.01f, power / place.power) : 1f;
        return place.minutes * 60f * Mathf.Clamp(1f / Mathf.Sqrt(ratio), settings.Shortest, settings.Longest);
    }

    /// <summary>How much of each find the haul holds: power over what was asked, within Least and Most Haul.</summary>
    public static float HaulScale(float power, float asked, ForaySettings settings) =>
        Mathf.Clamp(asked > 0f ? power / asked : 1f, settings.LeastHaul, settings.MostHaul);

    /// <summary>What a foray brings, each find rolled against its chance (<paramref name="roll"/>, 0-1) and its amount
    /// (<paramref name="amount"/>: least to most, inclusive), scaled by <paramref name="scale"/>.</summary>
    public static List<ItemAmount> Haul(ForaySettings.Destination place, float scale, System.Func<float> roll, System.Func<int, int, int> amount)
    {
        var haul = new List<ItemAmount>();
        foreach (ForaySettings.Loot loot in place.loot)
        {
            if (loot == null || loot.item == null || loot.most <= 0 || roll() >= loot.chance)
            {
                continue;
            }

            // Rounded by chance (a quarter of three is one, three times in four), so a small share isn't always nothing.
            float exact = amount(Mathf.Min(loot.least, loot.most), loot.most) * scale;
            int count = Mathf.FloorToInt(exact) + (roll() < exact - Mathf.Floor(exact) ? 1 : 0);
            if (count > 0)
            {
                haul.Add(new ItemAmount(loot.item, count));
            }
        }

        return haul;
    }

    /// <summary>"2-4 Iron Ore (often), 1 Elderwood (now and then)": what a place may give, for the Lands tab.</summary>
    public static string Finds(ForaySettings.Destination place)
    {
        var parts = new List<string>();
        foreach (ForaySettings.Loot loot in place.loot)
        {
            if (loot != null && loot.item != null && loot.most > 0)
            {
                int least = Mathf.Min(loot.least, loot.most);
                string how = loot.chance >= 0.95f ? "" : loot.chance >= 0.6f ? " (often)" : loot.chance >= 0.3f ? " (sometimes)" : " (rarely)";
                parts.Add((least == loot.most ? $"{loot.most}" : $"{least}-{loot.most}") + $" {loot.item.DisplayName}{how}");
            }
        }

        return string.Join(", ", parts);
    }

    // ----- Where they can go -----

    /// <summary>Why a foray can't go to <paramref name="place"/> with <paramref name="party"/> now, or null.</summary>
    public static string WhyNot(SettlementState home, ForaySettings.Destination place, IReadOnlyList<ResidentRecord> party, ItemContainer chests,
        bool visiting, ForaySettings settings)
    {
        if (visiting)
        {
            return "Forays set out from your own Haven.";
        }

        if (home.Foray != null)
        {
            return "A foray is already out.";
        }

        if (PortalTiers.Of(home) < place.tier)
        {
            return $"The portal doesn't open Tier {Tiers.ToRoman(place.tier)} yet.";
        }

        if (party.Count == 0)
        {
            return "Nobody is free to go.";
        }

        float food = party.Count * settings.RoadHunger;
        float have = chests != null ? ResidentMeals.HungerIn(chests, HomeLifeSettings.Current) : 0f;
        return food > 0f && have < food ? $"Not enough food for the road: {party.Count} need {food:0} hunger's worth, the chests hold {have:0}." : null;
    }

    /// <summary>Sends <paramref name="party"/> to <paramref name="place"/>: food for the road from the chests, the odds and the time fixed
    /// now (as shown). False with the reason posted when it can't.</summary>
    public static bool Send(SettlementState home, ForaySettings.Destination place, IReadOnlyList<ResidentRecord> party, ItemContainer chests, ForaySettings settings)
    {
        string why = WhyNot(home, place, party, chests, HavenVisit.IsVisiting, settings);
        if (why != null)
        {
            GameMessages.Post(why, MessageKind.Warning);
            return false;
        }

        ResidentMeals.TakeFood(chests, party.Count * settings.RoadHunger, HomeLifeSettings.Current);
        float power = PowerOf(party, settings, place, home);
        var ids = new List<string>();
        foreach (ResidentRecord record in party)
        {
            ids.Add(record.Resident.Id);
        }

        float seconds = Seconds(power, place, settings);
        // A feeling about the woods (an inspired morning: Inspirations) brings more home.
        float find = 1f;
        foreach (ResidentRecord record in party)
        {
            // One feeling is enough: the others keep theirs for another foray.
            if (Inspirations.Use(record, Inspiration.GoodFind))
            {
                find = 1f + PeopleSettings.Current.GoodFind;
                break;
            }
        }

        home.SetForay(new ForayRecord(place.id, ids, seconds, seconds, Chance(power, place.power, settings), HaulScale(power, place.power, settings) * find));
        GameMessages.Post($"{Names(party)} set out through the portal for {Where(place)}: back in about {Mathf.CeilToInt(seconds / 60f)} minutes.", MessageKind.Info);
        return true;
    }

    /// <summary>Calls the foray home now: they come back with nothing, unhurt.</summary>
    public static void CallHome(SettlementState home)
    {
        if (home.Foray == null)
        {
            return;
        }

        string names = Names(MembersOf(home, home.Foray));
        home.SetForay(null);
        GameMessages.Post($"{names} came back through the portal empty-handed.", MessageKind.Info);
    }

    /// <summary>Played time passes (SettlementSystem, at home and raiding): the foray comes home once its time is up.</summary>
    public static void Tick(SettlementState home, float seconds, ItemContainer chests, ForaySettings settings, System.Func<float> roll,
        System.Func<int, int, int> amount)
    {
        ForayRecord foray = home.Foray;
        if (foray == null || seconds <= 0f)
        {
            return;
        }

        foray.SecondsLeft -= seconds;
        if (foray.SecondsLeft > 0f)
        {
            return;
        }

        ForaySettings.Destination place = settings.Find(foray.Destination);
        List<ResidentRecord> party = MembersOf(home, foray);
        bool won = roll() < foray.Chance;
        List<ItemAmount> haul = place != null ? Haul(place, foray.HaulScale * (won ? 1f : settings.FailedHaul), roll, amount) : new List<ItemAmount>();
        var brought = new List<string>();
        foreach (ItemAmount found in haul)
        {
            int added = chests != null ? chests.Add(found.Item, found.Amount) : 0;
            PlaytestLogger.Note(PlaytestLog.CameHome, "Foray", added, found.Item.Id);
            if (added > 0)
            {
                brought.Add($"{added} {found.Item.DisplayName}");
            }
        }

        home.SetForay(null);
        foreach (ResidentRecord record in party)
        {
            record.Bruised = !won;
            // A foray gone wrong may leave its mark (Lost in the Mire, for the marsh).
            if (!won)
            {
                foreach (MarkDefinition mark in ResidentMarks.AfterEvent(record, MarkCause.ForayFailed, place != null ? place.id : "", roll))
                {
                    PeopleNews.Tell($"{record.Resident.DisplayName} came home from the foray carrying something: {mark.DisplayName}.", MessageKind.Info);
                }
            }

            // What they did out there, they got better at (the land's work: ResidentTalents).
            ProficiencyDefinition skill = place != null && place.skill != null ? place.skill : ResidentTalents.JobSkill(record);
            int tier = place != null ? place.tier : 1;
            ResidentTalents.Practise(record, skill, tier, LevelingSettings.Current.TierExperience(tier) * PeopleSettings.Current.ForayPractice, atHome: false);
        }

        // A road walked together (PeopleStandings).
        PeopleStandings.WentTogether(home, party);

        string what = brought.Count > 0 ? string.Join(", ", brought) : "nothing";
        string where = place != null ? Where(place) : "the woods";
        Tell(won ? $"{Names(party)} came home from {where} with {what}."
            : $"The foray to {where} went badly: {Names(party)} came home bruised with {what}. They work slower until the next dawn.");
    }

    /// <summary>Who is out on <paramref name="foray"/>, by their records (those who still live here).</summary>
    public static List<ResidentRecord> MembersOf(SettlementState home, ForayRecord foray)
    {
        var party = new List<ResidentRecord>();
        if (foray == null)
        {
            return party;
        }

        foreach (ResidentRecord record in home.Residents)
        {
            if (record.Resident != null && foray.Members.Contains(record.Resident.Id))
            {
                party.Add(record);
            }
        }

        return party;
    }

    /// <summary>"The Misty Woods, Tier II".</summary>
    public static string Where(ForaySettings.Destination place) =>
        $"{(place.raid != null ? place.raid.DisplayName : place.id)}, Tier {Tiers.ToRoman(place.tier)}";

    public static string Names(IReadOnlyList<ResidentRecord> party)
    {
        var names = new List<string>();
        foreach (ResidentRecord record in party)
        {
            names.Add(record.Resident.DisplayName);
        }

        return names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
    }

    // Told now in one's own Haven, else on coming home (PostWaiting).
    private static void Tell(string news)
    {
        if (Place.InAHaven && Place.AtHome)
        {
            GameMessages.Post(news, MessageKind.Gain);
        }
        else
        {
            waiting.Add(news);
        }
    }

    /// <summary>What came home while the player was away, told now that they are home (SettlementSystem).</summary>
    public static void PostWaiting()
    {
        foreach (string news in waiting)
        {
            GameMessages.Post(news, MessageKind.Gain);
        }

        waiting.Clear();
    }
}

/// <summary>A foray under way (Forays): where, who, how long is left, and the odds and haul fixed when it set out. Saved.</summary>
public sealed class ForayRecord
{
    public ForayRecord(string destination, IEnumerable<string> members, float secondsLeft, float seconds, float chance, float haulScale)
    {
        Destination = destination;
        Members = new List<string>(members);
        SecondsLeft = secondsLeft;
        Seconds = seconds;
        Chance = chance;
        HaulScale = haulScale;
    }

    public string Destination { get; }
    public List<string> Members { get; }
    public float SecondsLeft { get; set; }
    public float Seconds { get; }
    public float Chance { get; }
    public float HaulScale { get; }
}

/// <summary>
/// Who is out of the Haven now, asked in one place (the review of 2026-11-22: stations, builders, helpers and rings each asked it their
/// own way, so a Hunter could hunt, keep an outpost and add building hours in the same raid): on a foray (always away: ResidentRecord.IsAway),
/// or, on a raid long enough for parties to set out, leading a party or keeping a working outpost. Everything that counts a resident's
/// work while the player is away asks this.
/// </summary>
public static class ResidentsOut
{
    /// <summary>Whether anyone could be out on this raid: a party set, or an outpost raised (cheap, before asking for each).</summary>
    public static bool Any(SettlementState home) => home.Parties.Count > 0 || home.Outposts.Count > 0 || home.Foray != null;

    /// <summary>Those at their own work in the Haven now: not the one along, not paused or away, not out (Now). What the Haven gets done
    /// while the player is away is counted over these (the fields watered, a homecoming gift; builders and rings ask Now themselves).</summary>
    public static IEnumerable<ResidentRecord> Working(SettlementState home, ResidentDefinition along)
    {
        foreach (ResidentRecord record in home.Residents)
        {
            if (record != null && record.Resident != null && record.Resident != along && !record.WorkPaused && !Now(home, record, along))
            {
                yield return record;
            }
        }
    }

    public static bool Now(SettlementState home, ResidentRecord record, ResidentDefinition along)
    {
        if (record == null)
        {
            return false;
        }

        if (record.IsAway)
        {
            return true;
        }

        TerritorySettings lands = TerritorySettings.Current;
        return (home.Parties.Count > 0 || home.Outposts.Count > 0) && LandParties.SetOut(lands) && LandParties.IsOut(home, record, along, lands);
    }
}
