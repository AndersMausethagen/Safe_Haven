using System;
using System.Globalization;

/// <summary>
/// One saved game as a list shows it: its file, its name and what is known about it, read from the file without loading
/// it. A file that can't be used still gets a row, with <see cref="Problem"/> saying why, so the player can delete it.
/// </summary>
public sealed class SaveSlotInfo
{
    public SaveSlotInfo(string slot, SaveData data, string problem, DateTime lastWritten)
    {
        Slot = slot;
        Problem = problem;
        SavedAt = lastWritten;
        Name = NameFor(slot, data);
        if (data == null)
        {
            return;
        }

        Place = PlaceName(data.scene);
        RaidCutShort = data.raidUnderWay;
        // The character level went with the skills (it is no longer written, so it read 1 for every save): the day says how far.
        Day = data.settlement != null ? Math.Max(0, data.settlement.daysPassed) + 1 : 1;
        PlayedSeconds = data.playtest != null ? data.playtest.playedSeconds : 0f;
        if (DateTime.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime saved))
        {
            SavedAt = saved.ToLocalTime();
        }
    }

    /// <summary>The file's name without its extension. What SaveGameService calls a slot.</summary>
    public string Slot { get; }

    /// <summary>The raid this save never came home from, or empty.</summary>
    public string RaidCutShort { get; }

    public string Name { get; }
    public string Place { get; }
    /// <summary>The day of the journey it was saved on, from 1.</summary>
    public int Day { get; } = 1;
    public float PlayedSeconds { get; }
    public DateTime SavedAt { get; }
    /// <summary>Why it can't be loaded, or null when it can.</summary>
    public string Problem { get; }
    public bool IsReadable => Problem == null;

    /// <summary>"Safe Haven  ·  Day 4  ·  3 h 20 min played  ·  30 Oct 2026, 15:39".</summary>
    public string Details
    {
        get
        {
            string when = SavedAt.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
            return IsReadable ? $"{Place}  ·  Day {Day}  ·  {DescribePlayed(PlayedSeconds)}  ·  {when}" : $"{Problem}  ·  {when}";
        }
    }

    /// <summary>The save's own name; saves from before names existed go by their file.</summary>
    public static string NameFor(string slot, SaveData data)
    {
        if (data != null && !string.IsNullOrWhiteSpace(data.displayName))
        {
            return data.displayName;
        }

        return slot == SaveStorage.DefaultSlot ? "Journey 1" : slot.Replace('_', ' ');
    }

    public static string PlaceName(string scene)
    {
        switch (scene)
        {
            case "SafeHaven":
                return "Safe Haven";
            case "Prologue_MistyWoods":
                return "The Unquiet Forest";
            case "Raid_MistyWoods":
                return "Misty Woods";
            case "Raid_TrollValley":
                return "Troll Valley";
            default:
                return string.IsNullOrEmpty(scene) ? "Unknown place" : scene;
        }
    }

    public static string DescribePlayed(float seconds)
    {
        int minutes = (int)(seconds / 60f);
        return minutes < 60 ? $"{minutes} min played" : $"{minutes / 60} h {minutes % 60} min played";
    }
}
