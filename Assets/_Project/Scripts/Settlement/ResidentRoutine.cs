/// <summary>What a resident is about.</summary>
public enum ResidentActivity
{
    Work,
    Meal,
    Fire,
    Sleep,
    Shelter,
    /// <summary>Out for a while in the working day: to a waystone, the lakeshore (ResidentRoutine.Outing).</summary>
    Outing,
}

/// <summary>The hours of a resident's day (Settlement Settings › Daily Life).</summary>
public readonly struct ResidentHours
{
    public readonly float Wake;
    public readonly float WorkFrom;
    public readonly float MealFrom;
    public readonly float MealUntil;
    public readonly float WorkUntil;
    public readonly float SleepFrom;

    public ResidentHours(float wake, float workFrom, float mealFrom, float mealUntil, float workUntil, float sleepFrom)
    {
        Wake = wake;
        WorkFrom = workFrom;
        MealFrom = mealFrom;
        MealUntil = mealUntil;
        WorkUntil = workUntil;
        SleepFrom = sleepFrom;
    }
}

/// <summary>
/// The rule of a resident's day, kept pure so it can be tested: danger first, then sleep, meals and work by the clock, and the fire for
/// the rest of the day and for anyone without a workplace. ResidentLife carries it out.
/// </summary>
public static class ResidentRoutine
{
    /// <summary>
    /// <paramref name="record"/>'s own hours (Docs/PeopleResearch_2026-11-23.md §3.3): an early riser wakes and starts work earlier
    /// (StartEarlier; a late starter, negative, later), one who works late ends work and goes to bed later (WorkLater); meals stay
    /// when everyone eats.
    /// </summary>
    public static ResidentHours HoursOf(ResidentRecord record, in ResidentHours hours)
    {
        float earlier = UnityEngine.Mathf.Clamp(ResidentTraits.Sum(record, TraitEffectKind.StartEarlier), -4f, 4f);
        float later = UnityEngine.Mathf.Clamp(ResidentTraits.Sum(record, TraitEffectKind.WorkLater), -4f, 4f);
        if (earlier == 0f && later == 0f)
        {
            return hours;
        }

        return new ResidentHours(hours.Wake - earlier, hours.WorkFrom - earlier, hours.MealFrom, hours.MealUntil, hours.WorkUntil + later,
            UnityEngine.Mathf.Max(hours.WorkUntil + later + 1f, hours.SleepFrom + UnityEngine.Mathf.Max(0f, later)));
    }

    /// <summary>
    /// Whether someone goes out on a day (about one day in three each), and when: a start
    /// between ten and two, for an hour or so. Fixed by who they are and the day, so it holds however often it is asked.
    /// </summary>
    public static bool Outing(string who, int day, out float start, out float hours)
    {
        int hash = 17;
        foreach (char c in who ?? "")
        {
            hash = hash * 31 + c;
        }

        hash = hash * 31 + day * 7919;
        var random = new System.Random(hash);
        start = 10f + (float)random.NextDouble() * 4f;
        hours = 0.9f + (float)random.NextDouble() * 0.8f;
        return random.NextDouble() < 0.33;
    }

    /// <summary>
    /// The day in the cold (PlayerWarmth.ColdNow, 0-1): in hard cold the afternoon's outdoor work gives way to the fire,
    /// and in a bitter one the whole working day is short, from mid-morning to early afternoon.
    /// </summary>
    public static ResidentActivity InTheCold(ResidentActivity planned, float hour, float cold)
    {
        if (planned != ResidentActivity.Work || cold < 0.5f)
        {
            return planned;
        }

        bool bitter = cold >= 0.85f;
        return hour >= (bitter ? 13.5f : 15f) || (bitter && hour < 9.5f) ? ResidentActivity.Fire : planned;
    }

    public static ResidentActivity Decide(float hour, in ResidentHours hours, bool hasWorkplace, bool danger)
    {
        if (danger)
        {
            return ResidentActivity.Shelter;
        }

        if (hour < hours.Wake || hour >= hours.SleepFrom)
        {
            return ResidentActivity.Sleep;
        }

        if (hour >= hours.MealFrom && hour < hours.MealUntil)
        {
            return ResidentActivity.Meal;
        }

        return hasWorkplace && hour >= hours.WorkFrom && hour < hours.WorkUntil ? ResidentActivity.Work : ResidentActivity.Fire;
    }
}
