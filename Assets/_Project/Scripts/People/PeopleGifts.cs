/// <summary>
/// Gifts (Docs/PeopleSystemReview_2026-11-23.md §1.3, the user's go-ahead): each person loves two things (ResidentDefinition › Loves). One
/// given in talk lifts their spirits for Gift Days and grows their bond with the player; one a day counts (another the same day is
/// thanked for, nothing more). Plain rules (PeopleTests).
/// </summary>
public static class PeopleGifts
{
    /// <summary>The feeling a gift leaves ("gift", lasting Gift Days) and the day the last one was given ("gave").</summary>
    public const string GiftFeeling = "gift";
    private const string GaveFeeling = "gave";

    /// <summary>True when they love <paramref name="item"/>.</summary>
    public static bool Loves(ResidentRecord record, ItemDefinition item)
    {
        if (record?.Resident == null || item == null)
        {
            return false;
        }

        foreach (ItemDefinition loved in record.Resident.Loves)
        {
            if (loved == item)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a gift today would still count (none given yet today).</summary>
    public static bool CountsToday(ResidentRecord record, int today) => record != null && record.FeelingUntil(GaveFeeling) != today;

    /// <summary>
    /// They are given <paramref name="item"/> on <paramref name="today"/>: loved and the first today, it lifts their spirits for Gift Days and
    /// grows their bond. True when it counted.
    /// </summary>
    public static bool Give(ResidentRecord record, ItemDefinition item, int today)
    {
        if (!Loves(record, item) || !CountsToday(record, today))
        {
            return false;
        }

        record.Feel(GaveFeeling, today);
        record.Feel(GiftFeeling, today + PeopleSettings.Current.GiftDays - 1);
        PeopleBond.Grow(record, PeopleSettings.Current.BondGift, "gift");
        return true;
    }

    /// <summary>True while a gift still lifts their spirits.</summary>
    public static bool Glad(ResidentRecord record, int today) => record != null && record.FeelingUntil(GiftFeeling) >= today;
}
