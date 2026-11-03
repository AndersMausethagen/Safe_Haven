/// <summary>What a resident is about.</summary>
public enum ResidentActivity
{
    Work,
    Meal,
    Fire,
    Sleep,
    Shelter,
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
