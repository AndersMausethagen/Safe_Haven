using System.Collections.Generic;
using UnityEngine;

/// <summary>How a person is doing, in three readable states (Docs/PeopleResearch_2026-11-23.md §3.4). Saved as numbers nowhere.</summary>
public enum Spirits
{
    Low = 0,
    Steady = 1,
    Bright = 2,
}

/// <summary>One reason for their spirits, shown as a picture with a word (their card) and said in a line on hover.</summary>
public readonly struct SpiritReason
{
    public SpiritReason(string words, string icon, int steps)
    {
        Words = words;
        Icon = icon;
        Steps = steps;
    }

    public string Words { get; }
    public string Icon { get; }
    /// <summary>+1 lifts, -1 lowers.</summary>
    public int Steps { get; }
}

/// <summary>
/// A person's spirits (§3.4): worked out each time from what already is (their meal, their home and its wish, a scene's cheer, a
/// foray's bruises, passion work today, friends and rivals, a friend lost, a hard mark, an inspired day) — there is no meter to keep
/// topped up and no list of small numbers to manage. Bright gives a chance of an inspired day each dawn (Inspirations); Low costs a
/// little work (PeopleSettings › Low Work) and no inspired days, and nothing more: no breaks, no refusals, nobody leaves (the user's
/// answer, 2026-11-23). Plain rules (PeopleTests).
/// </summary>
public static class ResidentSpirits
{
    /// <summary>Their spirits now.</summary>
    public static Spirits Of(ResidentRecord record) => Of(record, GameSession.Here);

    public static Spirits Of(ResidentRecord record, IHavenView here)
    {
        int steps = 0;
        foreach (SpiritReason reason in ReasonsOf(record, here))
        {
            steps += reason.Steps;
        }

        PeopleSettings settings = PeopleSettings.Current;
        return steps >= settings.BrightAt ? Spirits.Bright : steps <= settings.LowAt ? Spirits.Low : Spirits.Steady;
    }

    /// <summary>What lifts and lowers them now, biggest first (lifts before lowers on a tie).</summary>
    public static List<SpiritReason> ReasonsOf(ResidentRecord record) => ReasonsOf(record, GameSession.Here);

    public static List<SpiritReason> ReasonsOf(ResidentRecord record, IHavenView here)
    {
        var reasons = new List<SpiritReason>();
        if (record == null || record.Resident == null)
        {
            return reasons;
        }

        int today = here != null ? here.DaysPassed : 0;
        if (ResidentMeals.Expected(here, HomeLifeSettings.Current))
        {
            if (record.Meal == ResidentMeal.WellFed)
            {
                reasons.Add(new SpiritReason("Ate well this morning", "Pot", 1));
            }
            else if (record.Meal == ResidentMeal.Hungry)
            {
                reasons.Add(new SpiritReason("Went hungry this morning", "Pot", -1));
            }
        }

        if (HomeLife.IsContent(record))
        {
            reasons.Add(new SpiritReason("Content at home", "House", 1));
        }

        if (record.FeelingUntil(ResidentMeals.FavouriteFeeling) >= today)
        {
            ItemDefinition dish = record.Resident.FavouriteDish;
            reasons.Add(new SpiritReason(dish != null ? $"{dish.DisplayName}, a favourite" : "Had their favourite dish", "Pot", 1));
        }

        if (record.FeelingUntil(Festivals.Feeling) >= today)
        {
            reasons.Add(new SpiritReason("A festival", "Sun", 1));
        }

        if (SmallScenes.CheerOf(record.Resident) > 0f)
        {
            reasons.Add(new SpiritReason("Cheered by a gathering", "Bench", 1));
        }

        if (record.Bruised)
        {
            reasons.Add(new SpiritReason("Bruised from a foray", "Cross", -1));
        }

        ProficiencyDefinition job = ResidentTalents.JobSkill(record);
        if (record.Job != null && !record.WorkPaused && ResidentTalents.PassionOf(record, job) != Passion.None)
        {
            reasons.Add(new SpiritReason($"Loves the work ({job.DisplayName})", ResidentTalents.PassionIcon, 1));
        }

        if (record.Inspiration != Inspiration.None)
        {
            reasons.Add(new SpiritReason("Inspired", "Spark", 1));
        }

        if (PeopleGifts.Glad(record, today))
        {
            reasons.Add(new SpiritReason("A gift they loved", "Heart", 1));
        }

        int kind = PeopleStandings.KindWordsFor(here, record);
        if (kind > 0)
        {
            reasons.Add(new SpiritReason("A kind word from a friend", "Bond", Mathf.Min(1, kind)));
        }

        if (PeopleStandings.FriendNearWork(record))
        {
            reasons.Add(new SpiritReason("A friend works nearby", "Bond", 1));
        }

        if (PeopleStandings.RivalNextDoor(record) && !ResidentTraits.Has(record, TraitEffectKind.RivalsDontBother))
        {
            reasons.Add(new SpiritReason("A rival next door", "House", -1));
        }

        if (record.MissesSomeone(today))
        {
            reasons.Add(new SpiritReason("Misses a friend the forest took", "Skull", -1));
        }

        if (ResidentMarks.HardCount(record) > 0)
        {
            reasons.Add(new SpiritReason("Carries a hard mark", "Cross", -1));
        }

        int own = Mathf.RoundToInt(ResidentTraits.Sum(record, TraitEffectKind.Spirits));
        if (own != 0)
        {
            reasons.Add(new SpiritReason(own > 0 ? "In good heart by nature" : "Gloomy by nature", "Star", own));
        }

        reasons.Sort((a, b) => b.Steps.CompareTo(a.Steps));
        return reasons;
    }

    /// <summary>Their spirits' share of work speed: Low costs PeopleSettings › Low Work; Steady and Bright nothing. Asked every frame by
    /// station speeds, so their spirits are worked out at most every Recheck Seconds.</summary>
    public static float WorkShare(ResidentRecord record) =>
        record != null && Cached(record) == Spirits.Low ? -PeopleSettings.Current.LowWork : 0f;

    private const float RecheckSeconds = 2f;
    // By who, not by record: a guest's copy of a friend's Haven is rebuilt with new records many times a visit.
    private static readonly Dictionary<ResidentDefinition, (float until, Spirits spirits)> cache = new Dictionary<ResidentDefinition, (float, Spirits)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => cache.Clear();

    /// <summary>Forgets what was worked out (a new game or a load: GameSession.Reset).</summary>
    public static void ForgetGame() => cache.Clear();

    // Their spirits, worked out again once Recheck Seconds have passed since the last time, by counting the steps (StepsOf) without
    // building the reasons' list and words (asked every frame by station speeds).
    public static Spirits Cached(ResidentRecord record)
    {
        float now = Time.unscaledTime;
        ResidentDefinition who = record?.Resident;
        if (who != null && cache.TryGetValue(who, out (float until, Spirits spirits) known) && now < known.until)
        {
            return known.spirits;
        }

        PeopleSettings settings = PeopleSettings.Current;
        int steps = StepsOf(record, GameSession.Here);
        Spirits spirits = steps >= settings.BrightAt ? Spirits.Bright : steps <= settings.LowAt ? Spirits.Low : Spirits.Steady;
        if (who != null)
        {
            cache[who] = (now + RecheckSeconds, spirits);
        }

        return spirits;
    }

    /// <summary>The sum of their reasons' steps, as <see cref="ReasonsOf(ResidentRecord, IHavenView)"/> gives them, without allocating.</summary>
    public static int StepsOf(ResidentRecord record, IHavenView here)
    {
        if (record == null || record.Resident == null)
        {
            return 0;
        }

        int steps = 0;
        if (ResidentMeals.Expected(here, HomeLifeSettings.Current))
        {
            steps += record.Meal == ResidentMeal.WellFed ? 1 : record.Meal == ResidentMeal.Hungry ? -1 : 0;
        }

        steps += HomeLife.IsContent(record) ? 1 : 0;
        steps += record.FeelingUntil(ResidentMeals.FavouriteFeeling) >= (here != null ? here.DaysPassed : 0) ? 1 : 0;
        steps += record.FeelingUntil(Festivals.Feeling) >= (here != null ? here.DaysPassed : 0) ? 1 : 0;
        steps += SmallScenes.CheerOf(record.Resident) > 0f ? 1 : 0;
        steps -= record.Bruised ? 1 : 0;
        steps += record.Job != null && !record.WorkPaused && ResidentTalents.PassionOf(record, ResidentTalents.JobSkill(record)) != Passion.None ? 1 : 0;
        steps += record.Inspiration != Inspiration.None ? 1 : 0;
        steps += PeopleGifts.Glad(record, here != null ? here.DaysPassed : 0) ? 1 : 0;
        steps += PeopleStandings.KindWordsFor(here, record) > 0 ? 1 : 0;
        steps += PeopleStandings.FriendNearWork(record) ? 1 : 0;
        steps -= PeopleStandings.RivalNextDoor(record) && !ResidentTraits.Has(record, TraitEffectKind.RivalsDontBother) ? 1 : 0;
        steps -= record.MissesSomeone(here != null ? here.DaysPassed : 0) ? 1 : 0;
        steps -= ResidentMarks.HardCount(record) > 0 ? 1 : 0;
        steps += Mathf.RoundToInt(ResidentTraits.Sum(record, TraitEffectKind.Spirits));
        return steps;
    }

    /// <summary>A feeling's id for missing a friend the forest took ("lost:" + their id).</summary>
    public const string LostFeeling = "lost:";

    /// <summary>The kit icon for a state.</summary>
    public static string IconOf(Spirits spirits) => spirits == Spirits.Bright ? "Sun" : spirits == Spirits.Low ? "Drop" : "Lantern";

    public static string NameOf(Spirits spirits) => spirits == Spirits.Bright ? "Bright" : spirits == Spirits.Low ? "Low" : "Steady";
}
