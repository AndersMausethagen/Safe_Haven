using System.Collections.Generic;

/// <summary>
/// The raid HUD's words, apart from Unity so they can be tested: the one line that says where to go, and how an exit and a rumour
/// read on the world map.
/// </summary>
public static class RaidHudText
{
    /// <summary>Seconds left on an exit at which the way line starts to warn of it.</summary>
    public const float ClosingSoon = 60f;

    /// <summary>"West Beacon  214 m", then only what changes the choice: that it is closing, or what it costs.</summary>
    public static string Way(string exit, float metres, float closesIn, string cost)
    {
        string line = $"{exit}  {UnityEngine.Mathf.RoundToInt(metres)} m";
        if (closesIn >= 0f && closesIn <= ClosingSoon)
        {
            line += $"  <color={UIStyle.Current.WarningHex}>closes in {RaidStats.FormatTime(closesIn)}</color>";
        }

        return string.IsNullOrEmpty(cost) ? line : line + $"  <color={UIStyle.Current.AccentHex}>needs {cost}</color>";
    }

    /// <summary>An exit on the world map, where there is room for all of it.</summary>
    public static string MapLabel(string exit, ExtractionStatus status, float secondsUntilChange, string cost)
    {
        string state = status == ExtractionStatus.NotYetOpen ? $", opens in {RaidStats.FormatTime(secondsUntilChange)}"
            : status == ExtractionStatus.Closed ? ", gone dark"
            : secondsUntilChange >= 0f ? $", closes in {RaidStats.FormatTime(secondsUntilChange)}"
            : "";
        return string.IsNullOrEmpty(cost) || status == ExtractionStatus.Closed ? exit + state : $"{exit}{state}, needs {cost}";
    }

    /// <summary>A rumour on the world map: where it is said to be, not where it is.</summary>
    public static string RumourLabel(string rumour, string place)
    {
        return string.IsNullOrEmpty(place) ? $"{rumour}, somewhere here" : $"{rumour}, at the {place}";
    }
}

/// <summary>
/// One line of news at a time under the way line: each stays long enough to read, the next waits its turn, and a backlog drops
/// its oldest rather than telling the player about what is long past.
/// </summary>
public sealed class RaidAlertLine
{
    private readonly struct Alert
    {
        public readonly string Text;
        public readonly float Seconds;

        public Alert(string text, float seconds)
        {
            Text = text;
            Seconds = seconds;
        }
    }

    private const int MostWaiting = 3;

    private readonly Queue<Alert> waiting = new Queue<Alert>();
    private string shown = "";
    private float shownUntil = float.NegativeInfinity;

    public void Post(string text, float seconds = 6f)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (waiting.Count == MostWaiting)
        {
            waiting.Dequeue();
        }

        waiting.Enqueue(new Alert(text, seconds));
    }

    /// <summary>What to show at this time; empty when there is no news.</summary>
    public string Current(float now)
    {
        if (now < shownUntil)
        {
            return shown;
        }

        if (waiting.Count == 0)
        {
            shown = "";
            return shown;
        }

        Alert next = waiting.Dequeue();
        shown = next.Text;
        shownUntil = now + next.Seconds;
        return shown;
    }
}
