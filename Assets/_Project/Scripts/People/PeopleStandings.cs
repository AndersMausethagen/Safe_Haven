using System.Collections.Generic;
using UnityEngine;

/// <summary>How two residents stand with each other, by pair (PeopleStandings): -2 Rival … +2 Friend. Saved with the Haven.</summary>
public sealed class StandingsState
{
    // "idA|idB" (ids in order) → standing, -2.5 to 2.5; its whole number is the level shown.
    private readonly Dictionary<string, float> values = new Dictionary<string, float>();

    public IReadOnlyDictionary<string, float> Values => values;

    public static string Key(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

    public bool Has(string a, string b) => values.ContainsKey(Key(a, b));

    public float ValueOf(string a, string b) => a != null && b != null && values.TryGetValue(Key(a, b), out float had) ? had : 0f;

    public void Set(string a, string b, float value)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b)
        {
            return;
        }

        values[Key(a, b)] = Mathf.Clamp(value, -PeopleStandings.Most, PeopleStandings.Most);
    }

    /// <summary>Puts a saved pair back ("idA|idB" as saved).</summary>
    public void Restore(string key, float value)
    {
        if (!string.IsNullOrEmpty(key) && key.IndexOf('|') > 0)
        {
            values[key] = Mathf.Clamp(value, -PeopleStandings.Most, PeopleStandings.Most);
        }
    }

    public void Clear() => values.Clear();
}

/// <summary>
/// Friends and rivals (Docs/PeopleResearch_2026-11-23.md §3.5; RimWorld's opinion without the noise, Mount &amp; Blade's written likes):
/// each pair stands from Rival (-2) through Cool, Neutral and Friendly to Friend (+2). Each written person starts Friendly with one and
/// Cool with another (ResidentDefinition › Friend, Friction). Standings grow with small scenes, forays and days working near each other,
/// and swing either way when rivals argue at the fire. Friends near at work are quicker and brighter, friends on a foray stronger, and a
/// friend the forest takes is missed; rivals as neighbours sour a home. Plain rules (PeopleTests); the numbers are PeopleSettings'.
/// </summary>
public static class PeopleStandings
{
    /// <summary>The farthest a standing goes either way.</summary>
    public const float Most = 2.5f;

    /// <summary>The level of a standing: -2 Rival, -1 Cool, 0 Neutral, 1 Friendly, 2 Friend.</summary>
    public static int LevelOf(float value) => Mathf.Clamp(Mathf.RoundToInt(value), -2, 2);

    public static int LevelOf(IHavenView here, ResidentRecord a, ResidentRecord b) =>
        here == null || a?.Resident == null || b?.Resident == null || a == b ? 0 : LevelOf(here.StandingOf(a.Resident.Id, b.Resident.Id));

    public static string NameOf(int level) => level switch
    {
        -2 => "Rival",
        -1 => "Cool",
        1 => "Friendly",
        2 => "Friend",
        _ => "Neutral",
    };

    /// <summary>A newcomer: Friendly with their written friend and Cool with their written friction, both ways, once both live here.</summary>
    public static void Welcome(SettlementState home, ResidentRecord record)
    {
        if (home == null || record?.Resident == null)
        {
            return;
        }

        PeopleSettings settings = PeopleSettings.Current;
        foreach (ResidentRecord other in home.Residents)
        {
            if (other == record || other.Resident == null || home.Standings.Has(record.Resident.Id, other.Resident.Id))
            {
                continue;
            }

            ResidentDefinition a = record.Resident, b = other.Resident;
            if (a.Friend == b || b.Friend == a)
            {
                home.SetStanding(a.Id, b.Id, settings.WrittenFriend);
            }
            else if (a.Friction == b || b.Friction == a)
            {
                home.SetStanding(a.Id, b.Id, settings.WrittenFriction);
            }
        }
    }

    /// <summary>
    /// Changes how <paramref name="a"/> and <paramref name="b"/> stand by <paramref name="by"/>: a rise times what their traits make of
    /// growing close (Kind quicker, Gruff slower: StandingGain, the two averaged). Returns the new level.
    /// </summary>
    public static int Change(SettlementState home, ResidentRecord a, ResidentRecord b, float by)
    {
        if (home == null || a?.Resident == null || b?.Resident == null || a == b)
        {
            return 0;
        }

        if (by > 0f)
        {
            float gain = (ResidentTraits.Sum(a, TraitEffectKind.StandingGain) + ResidentTraits.Sum(b, TraitEffectKind.StandingGain)) * 0.5f;
            by *= Mathf.Max(0.1f, 1f + gain);
        }

        int before = LevelOf(home.Standings.ValueOf(a.Resident.Id, b.Resident.Id));
        float now = Banded(a, b, before, home.Standings.ValueOf(a.Resident.Id, b.Resident.Id) + by, scene: false);
        home.SetStanding(a.Resident.Id, b.Resident.Id, now);
        int after = LevelOf(home.Standings.ValueOf(a.Resident.Id, b.Resident.Id));
        Logged(a, b, before, after, by > 0f ? "closer" : "apart");
        return after;
    }

    // A standing's level changed: a playtest row, and news when two become Friends or Rivals.
    private static void Logged(ResidentRecord a, ResidentRecord b, int before, int after, string why)
    {
        if (before == after)
        {
            return;
        }

        PlaytestLogger.Note(PlaytestLog.Standing, StandingsState.Key(a.Resident.Id, b.Resident.Id), after, why);
        if (after == 2 && before < 2)
        {
            PeopleNews.Tell($"{a.Resident.DisplayName} and {b.Resident.DisplayName} are friends now.", MessageKind.Gain);
            if (PeopleBond.IsOwn(a) && PeopleBond.IsOwn(b))
            {
                GameEvents.Raise(new GameEvent(GameEventKind.BecameFriends, a.Resident, 1, detail: b.Resident));
            }
        }
        else if (after == -2 && before > -2)
        {
            PeopleNews.Tell($"{a.Resident.DisplayName} and {b.Resident.DisplayName} can't abide each other now.", MessageKind.Info);
        }
    }

    /// <summary>A small scene shared: they grow closer.</summary>
    public static void SharedAScene(SettlementState home, ResidentRecord a, ResidentRecord b) =>
        Change(home, a, b, PeopleSettings.Current.SceneGain);

    /// <summary>A foray walked together: every pair in the party grows closer.</summary>
    public static void WentTogether(SettlementState home, IReadOnlyList<ResidentRecord> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            for (int j = i + 1; j < party.Count; j++)
            {
                Change(home, party[i], party[j], PeopleSettings.Current.ForayGain);
            }
        }
    }

    /// <summary>
    /// Rivals argue at the fire (a rivalry scene): as RimWorld's social fight, it ends one of two ways (<paramref name="roll"/> 0-1):
    /// they laugh it off and are the better for it (Cleared Air), or it hardens (Hardened); never blows. A Sharp-Tongued one makes both
    /// bigger (BoldWords). True when it cleared the air.
    /// </summary>
    public static bool Argue(SettlementState home, ResidentRecord a, ResidentRecord b, float roll)
    {
        PeopleSettings settings = PeopleSettings.Current;
        float bold = 1f + Mathf.Max(ResidentTraits.Sum(a, TraitEffectKind.BoldWords), ResidentTraits.Sum(b, TraitEffectKind.BoldWords));
        bool cleared = roll < settings.ClearedAirChance;
        float by = cleared ? settings.ClearedAir * bold : -settings.Hardened * bold;
        int before = LevelOf(home.Standings.ValueOf(a.Resident.Id, b.Resident.Id));
        float now = Banded(a, b, before, home.Standings.ValueOf(a.Resident.Id, b.Resident.Id) + by, scene: true);
        home.SetStanding(a.Resident.Id, b.Resident.Id, now);
        Logged(a, b, before, LevelOf(home.Standings.ValueOf(a.Resident.Id, b.Resident.Id)), cleared ? "cleared the air" : "hardened");
        return cleared;
    }

    // Lowest a written friendship goes (Friendly), and highest anything but a scene may leave two people (Cool): rounding (to even) puts
    // 0.5 at Neutral and -1.5 at Rival, so the limits sit inside the levels.
    private const float FriendlyFloor = 0.6f;
    private const float CoolFloor = -1.4f;

    /// <summary>The Campaign's band (PeopleSettings › Keep To The Writing): a written friendship never sinks below Friendly, and only a
    /// rivals' <paramref name="scene"/> makes two people Rivals who weren't already.</summary>
    private static float Banded(ResidentRecord a, ResidentRecord b, int before, float now, bool scene)
    {
        if (!PeopleSettings.Current.KeepToTheWriting)
        {
            return now;
        }

        if (a.Resident.Friend == b.Resident || b.Resident.Friend == a.Resident)
        {
            now = Mathf.Max(now, FriendlyFloor);
        }

        if (!scene && before > -2 && now < CoolFloor)
        {
            now = CoolFloor;
        }

        return now;
    }

    /// <summary>A dawn: everyone who worked near a friend-to-be grows a little closer to them.</summary>
    public static void Dawn(SettlementState home)
    {
        PeopleSettings settings = PeopleSettings.Current;
        IReadOnlyList<ResidentRecord> people = home.Residents;
        for (int i = 0; i < people.Count; i++)
        {
            for (int j = i + 1; j < people.Count; j++)
            {
                if (!people[i].IsAway && !people[j].IsAway && WorkNear(people[i], people[j], settings.NearReach))
                {
                    Change(home, people[i], people[j], settings.NearGain);
                }
            }
        }

        near.Clear();
    }

    /// <summary>The forest took <paramref name="lost"/>: their friends miss them for Missed Days (their spirits; a word asking for them).</summary>
    public static void Lost(SettlementState home, ResidentDefinition lost)
    {
        if (home == null || lost == null)
        {
            return;
        }

        foreach (ResidentRecord other in home.Residents)
        {
            if (other.Resident != null && LevelOf(home.Standings.ValueOf(lost.Id, other.Resident.Id)) >= 1)
            {
                other.Feel(ResidentSpirits.LostFeeling + lost.Id, home.DaysPassed + PeopleSettings.Current.MissedDays);
            }
        }
    }

    /// <summary>Found again: nobody misses them any more.</summary>
    public static void Found(SettlementState home, ResidentDefinition found)
    {
        if (home == null || found == null)
        {
            return;
        }

        foreach (ResidentRecord other in home.Residents)
        {
            if (other.FeelingUntil(ResidentSpirits.LostFeeling + found.Id) >= 0)
            {
                other.ForgetFeeling(ResidentSpirits.LostFeeling + found.Id);
            }
        }
    }

    /// <summary>Who they miss now (a friend the forest has), for a word asking the player to bring them home; null when nobody.</summary>
    public static ResidentDefinition Misses(IHavenView here, ResidentRecord record)
    {
        if (here == null || record == null)
        {
            return null;
        }

        foreach (FallenRecord lost in here.Fallen)
        {
            if (lost.Resident != null && record.FeelingUntil(ResidentSpirits.LostFeeling + lost.Resident.Id) >= here.DaysPassed)
            {
                return lost.Resident;
            }
        }

        return null;
    }

    /// <summary>Friends (Friendly or better) going on a foray together: a share of the party's power for each pair, up to Most Foray Friends.</summary>
    public static float ForayBonus(IReadOnlyList<ResidentRecord> party, IHavenView here = null)
    {
        PeopleSettings settings = PeopleSettings.Current;
        CountPairs(party, here, out int friends, out int rivals);
        return Mathf.Min(friends, settings.MostForayFriends) * settings.ForayFriends - Mathf.Min(rivals, settings.MostForayFriends) * settings.ForayRivals;
    }

    /// <summary>Pairs in a party who are friends (Friendly or better) and who are rivals (Cool or worse): for the foray card.</summary>
    public static void CountPairs(IReadOnlyList<ResidentRecord> party, IHavenView here, out int friends, out int rivals)
    {
        here ??= GameSession.Here;
        friends = 0;
        rivals = 0;
        for (int i = 0; i < party.Count; i++)
        {
            for (int j = i + 1; j < party.Count; j++)
            {
                int level = LevelOf(here, party[i], party[j]);
                friends += level >= 1 ? 1 : 0;
                rivals += level <= -1 ? 1 : 0;
            }
        }
    }

    /// <summary>Others at home with Kind Words who don't stand badly with them (their spirits).</summary>
    public static int KindWordsFor(IHavenView here, ResidentRecord record)
    {
        if (here == null || record == null)
        {
            return 0;
        }

        int kind = 0;
        IReadOnlyList<ResidentRecord> people = here.Residents;
        for (int i = 0; i < people.Count; i++)
        {
            ResidentRecord other = people[i];
            if (other != record && !other.IsAway && LevelOf(here, record, other) >= 0)
            {
                kind += Mathf.RoundToInt(ResidentTraits.Sum(other, TraitEffectKind.KindWords));
            }
        }

        return kind;
    }

    // ----- Near at work, next door: worked out from the buildings every Recheck Seconds -----

    private const float RecheckSeconds = 10f;
    // By who, not by record: a guest's copy of a friend's Haven is rebuilt with new records many times a visit.
    private static readonly Dictionary<ResidentDefinition, (float until, int friends, bool rival, int rivalsAtWork)> near =
        new Dictionary<ResidentDefinition, (float, int, bool, int)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => near.Clear();

    /// <summary>Forgets what was worked out (a new game or a load: GameSession.Reset).</summary>
    public static void ForgetGame() => near.Clear();

    /// <summary>Friends (Friendly or better) whose work is within Near Reach of theirs, up to Most Near Friends.</summary>
    public static int FriendsNearWork(ResidentRecord record) => Near(record).friends;

    public static bool FriendNearWork(ResidentRecord record) => Near(record).friends > 0;

    /// <summary>A Rival lives within Next Door of their home.</summary>
    public static bool RivalNextDoor(ResidentRecord record) => Near(record).rival;

    /// <summary>Their work speed share from friends working near them, less what rivals working near them cost.</summary>
    public static float WorkShare(ResidentRecord record)
    {
        (float _, int friends, bool _, int rivals) = Near(record);
        PeopleSettings settings = PeopleSettings.Current;
        return friends * settings.FriendNearWork - rivals * settings.RivalNearWork;
    }

    /// <summary>Rivals (Cool or worse) whose work is within Near Reach of theirs, up to Most Near Friends.</summary>
    public static int RivalsNearWork(ResidentRecord record) => Near(record).rivalsAtWork;

    private static (float until, int friends, bool rival, int rivalsAtWork) Near(ResidentRecord record)
    {
        if (record?.Resident == null)
        {
            return default;
        }

        float now = Time.unscaledTime;
        if (near.TryGetValue(record.Resident, out (float until, int friends, bool rival, int rivalsAtWork) known) && now < known.until)
        {
            return known;
        }

        IHavenView here = GameSession.Here;
        PeopleSettings settings = PeopleSettings.Current;
        int friends = 0;
        int rivalsAtWork = 0;
        bool rival = false;
        if (here != null && !record.IsAway)
        {
            IReadOnlyList<ResidentRecord> people = here.Residents;
            for (int i = 0; i < people.Count; i++)
            {
                ResidentRecord other = people[i];
                if (other == record || other.IsAway)
                {
                    continue;
                }

                int level = LevelOf(here, record, other);
                bool working = record.Job != null && other.Job != null && level != 0 && WorkNear(record, other, settings.NearReach);
                if (level >= 1 && working)
                {
                    friends++;
                }
                else if (level <= -1 && working)
                {
                    rivalsAtWork++;
                }

                if (level <= -2 && HomesNear(record, other, settings.NextDoor))
                {
                    rival = true;
                }
            }
        }

        var found = (now + RecheckSeconds, Mathf.Min(friends, settings.MostNearFriends), rival, Mathf.Min(rivalsAtWork, settings.MostNearFriends));
        near[record.Resident] = found;
        return found;
    }

    private static bool WorkNear(ResidentRecord a, ResidentRecord b, float reach)
    {
        BuildingObject first = HavenHomes.WorkplaceOf(a, Vector3.zero);
        BuildingObject second = HavenHomes.WorkplaceOf(b, Vector3.zero);
        return first != null && second != null && (first.transform.position - second.transform.position).sqrMagnitude <= reach * reach;
    }

    /// <summary>A Rival of <paramref name="record"/> who lives in or next door to <paramref name="house"/> (the window's Move them), or null.</summary>
    public static ResidentRecord RivalBy(IHavenView here, ResidentRecord record, BuildingObject house)
    {
        if (here == null || record == null || house == null)
        {
            return null;
        }

        float reach = PeopleSettings.Current.NextDoor;
        IReadOnlyList<ResidentRecord> people = here.Residents;
        for (int i = 0; i < people.Count; i++)
        {
            ResidentRecord other = people[i];
            BuildingObject theirs = other != record ? HavenHomes.HomeOf(other) : null;
            if (theirs != null && LevelOf(here, record, other) <= -2 && (theirs == house || (theirs.transform.position - house.transform.position).sqrMagnitude <= reach * reach))
            {
                return other;
            }
        }

        return null;
    }

    private static bool HomesNear(ResidentRecord a, ResidentRecord b, float reach)
    {
        BuildingObject first = HavenHomes.HomeOf(a);
        BuildingObject second = HavenHomes.HomeOf(b);
        return first != null && second != null && first != second && (first.transform.position - second.transform.position).sqrMagnitude <= reach * reach;
    }
}
