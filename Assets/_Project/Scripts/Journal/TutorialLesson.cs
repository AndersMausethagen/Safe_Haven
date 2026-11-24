using System;
using System.Text.RegularExpressions;

/// <summary>
/// A tutorial learned by doing (Docs/QualityLevers_2026-11-24.md §1): a mechanics page's steps, each a few words with one key in it
/// ("[{key:Guard}] Hold to guard"), done in order as the player presses that key. TutorialNoticeHud shows it on its card while the
/// game goes on, so the page is learned in play instead of in a window; done, the page counts as read. Plain C#, tested in
/// TutorialTests.
/// </summary>
public sealed class TutorialLesson
{
    private static readonly Regex KeyToken = new Regex(@"\{key:([A-Za-z0-9/]+)\}");

    public TutorialLesson(JournalPage page)
    {
        Page = page;
    }

    public JournalPage Page { get; }

    /// <summary>How many of its steps are done, in order.</summary>
    public int Done { get; private set; }

    public int Count => Page != null && Page.steps != null ? Page.steps.Count : 0;

    public bool Finished => Count > 0 && Done >= Count;

    /// <summary>The key token of the step to do now ("Guard"), or null once finished.</summary>
    public string CurrentToken => Finished || Count == 0 ? null : TokenOf(Page.steps[Done]);

    /// <summary>A press of the key named <paramref name="token"/>: the step now is done when it is that one's key. True when it was.</summary>
    public bool Press(string token)
    {
        string wanted = CurrentToken;
        if (wanted == null || !string.Equals(wanted, token, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Done++;
        return true;
    }

    /// <summary>Whether <paramref name="page"/> has steps to do.</summary>
    public static bool HasSteps(JournalPage page) => page != null && page.steps != null && page.steps.Count > 0;

    /// <summary>The first key token in a step ("Attack" in "[{key:Attack}] Swing"), or null when it names none.</summary>
    public static string TokenOf(string step)
    {
        if (string.IsNullOrEmpty(step))
        {
            return null;
        }

        Match match = KeyToken.Match(step);
        return match.Success ? match.Groups[1].Value : null;
    }
}
