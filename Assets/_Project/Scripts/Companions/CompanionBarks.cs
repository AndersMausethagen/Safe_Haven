using System;
using UnityEngine;

/// <summary>What someone says in a fight. A line is picked at random; a moment with none says nothing.</summary>
[Serializable]
public sealed class FightLines
{
    [Tooltip("Enemies are on them.")]
    [TextArea(1, 2)] public string[] spotted = new string[0];
    [Tooltip("Something is about to hit the player from behind.")]
    [TextArea(1, 2)] public string[] behindYou = new string[0];
    [Tooltip("They are badly hurt.")]
    [TextArea(1, 2)] public string[] hurt = new string[0];
    [Tooltip("The fight is over.")]
    [TextArea(1, 2)] public string[] won = new string[0];
    [Tooltip("The player has just helped them up.")]
    [TextArea(1, 2)] public string[] helpedUp = new string[0];
}

/// <summary>
/// A companion's voice in a fight (Companion's FightLines, those of the person first): when enemies come, when something is behind the
/// player, when they are badly hurt and when it is over. Only one speaks at a time, nobody talks over a conversation or a line of the
/// story, and the timing rules are here so they can be tested. Plain C#, owned by a Companion.
/// </summary>
public sealed class CompanionBarks
{
    public enum Moment
    {
        None,
        Spotted,
        BehindYou,
        Hurt,
        Won,
    }

    private const float QuietAfterALine = 12f;
    // A warning can't wait as long as a remark.
    private const float QuietAfterAWarning = 5f;
    private const float HurtBelow = 0.3f;
    private const float FightOverAfter = 2.5f;

    private static float nextLineAt;
    private static float nextWarningAt;

    private bool fighting;
    private bool saidHurt;
    private float lastFoughtAt;

    /// <summary>What this calls for, the most urgent first. It remembers what it has seen, so each is said once.</summary>
    public Moment Think(bool enemiesOnUs, bool blowFromBehind, float healthShare, float now)
    {
        if (healthShare >= HurtBelow * 1.5f)
        {
            saidHurt = false;
        }

        if (blowFromBehind)
        {
            return Moment.BehindYou;
        }

        if (healthShare < HurtBelow && healthShare > 0f && !saidHurt)
        {
            saidHurt = true;
            return Moment.Hurt;
        }

        if (enemiesOnUs)
        {
            lastFoughtAt = now;
            if (!fighting)
            {
                fighting = true;
                return Moment.Spotted;
            }

            return Moment.None;
        }

        if (fighting && now - lastFoughtAt >= FightOverAfter)
        {
            fighting = false;
            return Moment.Won;
        }

        return Moment.None;
    }

    public static bool MaySpeak(Moment moment, float now, bool someoneIsSpeaking)
    {
        if (moment == Moment.None || someoneIsSpeaking)
        {
            return false;
        }

        return moment == Moment.BehindYou ? now >= nextWarningAt : now >= nextLineAt;
    }

    public static void Spoke(Moment moment, float now)
    {
        nextLineAt = now + QuietAfterALine;
        nextWarningAt = now + QuietAfterAWarning;
    }

    public static string[] LinesFor(Moment moment, FightLines own, FightLines kind)
    {
        string[] Of(FightLines lines) => lines == null ? null : moment switch
        {
            Moment.Spotted => lines.spotted,
            Moment.BehindYou => lines.behindYou,
            Moment.Hurt => lines.hurt,
            Moment.Won => lines.won,
            _ => null,
        };

        string[] theirs = Of(own);
        return theirs != null && theirs.Length > 0 ? theirs : Of(kind);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        nextLineAt = 0f;
        nextWarningAt = 0f;
    }
}
