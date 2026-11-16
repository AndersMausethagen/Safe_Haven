using System;
using UnityEngine;

/// <summary>
/// A hooked fish fighting the line, in the manner of the realistic fishing games but kinder. The fish rests, gives a
/// moment's warning (the float jerks), then runs: it pulls hard, to one side or straight away, and takes line. The player
/// holds reel to bring it in and lets go to give line, and leans the rod against the side it runs to (side pressure),
/// which turns it back and tires it faster. Reeling against a run builds tension; held at the top too long, the line
/// snaps. Left slack too long, a fresh fish shakes the hook. Steady pressure tires it; a spent fish runs no more, comes
/// in to the bank on the reel and is landed. Plain C#, so it is testable: PlayerFishing drives it, FishingHud draws it. Angles are degrees off
/// the line of the cast, positive to its right.
/// </summary>
public sealed class FishFight
{
    public enum Outcome { Fighting, Landed, Snapped, ThrewHook, Spooled }

    /// <summary>What the fish is doing: resting, about to run (the float jerks), or running.</summary>
    public enum Mood { Resting, Warning, Running, Spent }

    private readonly FishingSettings.FightTuning tuning;
    private readonly float fight;
    private readonly float tireSeconds;
    private readonly float pullScale = 1f;
    private readonly float strainSeconds;
    private readonly float slackSeconds;
    private readonly System.Random random;
    private float moodLeft;

    /// <param name="fight">The fish's fight, 0 (lazy) to 1 (a fighter): how hard and how often it runs.</param>
    /// <param name="size">Where this one falls in its kind's size range, 0 to 1: bigger fish last longer.</param>
    /// <param name="control">The angler's line control (1 = plain; the Fishing skill and perks raise it): the line holds
    /// longer at full strain and slack, and fish tire sooner.</param>
    /// <param name="distance">Metres of line out when it bites.</param>
    /// <param name="heft">How much harder its length makes it than a 40 cm fish (FishSizes.Difficulty, eased by the bait): it
    /// tires that much slower and pulls a little harder on its runs.</param>
    public FishFight(FishingSettings.FightTuning tuning, float fight, float size, float control, float distance, int seed, float heft = 1f)
    {
        this.tuning = tuning;
        this.fight = Mathf.Clamp01(fight);
        control = Mathf.Max(0.2f, control);
        heft = Mathf.Max(0.3f, heft);
        pullScale = Mathf.Clamp(1f + 0.3f * (heft - 1f), 0.8f, 1.6f);
        tireSeconds = Mathf.Lerp(tuning.tireSeconds.x, tuning.tireSeconds.y, this.fight) * Mathf.Lerp(0.85f, 1.3f, Mathf.Clamp01(size)) * heft / control;
        strainSeconds = tuning.strainSeconds * control;
        slackSeconds = tuning.slackSeconds * control;
        random = new System.Random(seed);
        Distance = Mathf.Max(tuning.landDistance, distance);
        Stamina = 1f;
        Tension = 0.35f;
        mood = Mood.Resting;
        // The first run comes soon after the strike: the fish feels the hook.
        moodLeft = 0.6f + (float)random.NextDouble() * 0.6f;
    }

    private Mood mood;

    public Mood Current => Spent ? Mood.Spent : mood;

    /// <summary>Metres of line out, from the angler to the fish.</summary>
    public float Distance { get; private set; }

    /// <summary>Degrees the fish is off the line of the cast (positive: right of it, as the angler looks).</summary>
    public float Angle { get; private set; }

    /// <summary>0 (worn out) to 1 (fresh).</summary>
    public float Stamina { get; private set; }

    /// <summary>The fight left in it, 1 (fresh) to 0 (spent: tired enough to be landed).</summary>
    public float Strength => Mathf.Clamp01((Stamina - tuning.landStamina) / Mathf.Max(0.01f, 1f - tuning.landStamina));

    /// <summary>0 (slack) to 1 (at breaking point); above 1 the line is straining.</summary>
    public float Tension { get; private set; }

    /// <summary>0 to 1: how close the straining line is to snapping.</summary>
    public float Strain { get; private set; }

    /// <summary>0 to 1: how close a slack line is to letting the hook slip.</summary>
    public float Slack { get; private set; }

    /// <summary>The side a run pulls to: -1 left, 1 right, 0 straight away (or not running).</summary>
    public int RunSide { get; private set; }

    /// <summary>The side that turns the fish: the rod held against its run (0 when the side doesn't matter).</summary>
    public int Counter => Current == Mood.Running || Current == Mood.Warning ? -RunSide : 0;

    /// <summary>How well the rod is held against the run, -1 (with it: the line takes the worst) to 1 (against it).</summary>
    public float Leverage { get; private set; }

    public Outcome Result { get; private set; }
    public bool Over => Result != Outcome.Fighting;
    /// <summary>Tired enough to land: it runs no more, and comes in to the bank on the reel.</summary>
    public bool Spent => Stamina <= tuning.landStamina;

    /// <summary>
    /// Moves the fight on by <paramref name="seconds"/>. <paramref name="reeling"/> is the reel being turned (attack held);
    /// <paramref name="rodSide"/> is where the rod is leaned, -1 (left of the line) to 1 (right).
    /// </summary>
    public void Tick(float seconds, bool reeling, float rodSide)
    {
        if (Over || seconds <= 0f)
        {
            return;
        }

        UpdateMood(seconds);
        Mood now = Current;
        bool running = now == Mood.Running;
        rodSide = Mathf.Clamp(rodSide, -1f, 1f);
        Leverage = running && RunSide != 0 ? -rodSide * RunSide : 0f;
        float against = Mathf.Max(0f, Leverage);
        float with = Mathf.Max(0f, -Leverage);
        float pullShare = 1f - tuning.sideGrip * against + (tuning.wrongSidePull - 1f) * with;
        float tireShare = 1f + (tuning.sideTire - 1f) * against - (1f - tuning.wrongSideTire) * with;

        float effort = Pull(now);
        float pull = effort * pullShare;
        // Turning the reel against a hard run takes the line over its limit; a lazy fish can simply be reeled.
        float target = reeling ? 0.32f + pull : pull * 0.6f;
        Tension = Mathf.MoveTowards(Tension, Mathf.Min(target, 1.2f), (target > Tension ? 2.2f : 3f) * seconds);

        // Line: a running fish takes it through the drag when the reel isn't turning, or slips it when the line is at
        // the top; turning the reel brings in what the fish doesn't pull back.
        if (running && (!reeling || Tension >= 0.99f))
        {
            Distance += tuning.runTake * pull * (reeling ? 0.5f : 1f) * seconds;
        }

        if (reeling)
        {
            float speed = now == Mood.Spent ? tuning.spentReelSpeed : tuning.reelSpeed * Mathf.Clamp01(1f - pull * 1.2f);
            Distance -= speed * seconds;
        }

        if (Distance <= tuning.landDistance)
        {
            Distance = tuning.landDistance;
            if (Spent)
            {
                Result = Outcome.Landed;
                return;
            }
        }

        if (Distance >= tuning.maxLine)
        {
            Result = Outcome.Spooled;
            return;
        }

        UpdateAngle(now, rodSide, pullShare, against, seconds);

        // Steady pressure tires it, and so does its own running (the more so against a rod held across it); a slack line
        // lets it rest a little.
        float pressure = Mathf.Clamp01((Tension - 0.12f) / 0.6f);
        float drain = (pressure + (running ? effort : 0f)) * Mathf.Max(0.1f, tireShare) / tireSeconds;
        Stamina = Mathf.Clamp01(Stamina - drain * seconds + (Tension < 0.1f && !Spent ? 0.02f * seconds : 0f));

        Strain = Tension >= 0.99f ? Strain + seconds / strainSeconds : Mathf.Max(0f, Strain - seconds / (strainSeconds * 1.5f));
        if (Strain >= 1f)
        {
            Result = Outcome.Snapped;
            return;
        }

        bool slack = Tension < 0.1f && !Spent && now != Mood.Running;
        Slack = slack ? Slack + seconds / slackSeconds : Mathf.Max(0f, Slack - seconds * 0.5f);
        if (Slack >= 1f)
        {
            Result = Outcome.ThrewHook;
        }
    }

    // How hard the fish pulls now, before the rod's side: hardest on a run, weaker the more tired it is.
    private float Pull(Mood now)
    {
        switch (now)
        {
            case Mood.Running:
                return Mathf.Lerp(tuning.runPull.x, tuning.runPull.y, fight) * pullScale * (0.5f + 0.5f * Stamina);
            case Mood.Warning:
                return 0.25f * Stamina;
            case Mood.Spent:
                return 0.04f;
            default:
                return 0.08f + 0.1f * Stamina;
        }
    }

    private void UpdateMood(float seconds)
    {
        if (Spent)
        {
            RunSide = 0;
            return;
        }

        moodLeft -= seconds;
        if (moodLeft > 0f)
        {
            return;
        }

        float roll = (float)random.NextDouble();
        switch (mood)
        {
            case Mood.Resting:
                mood = Mood.Warning;
                moodLeft = Mathf.Lerp(tuning.warnSeconds.x, tuning.warnSeconds.y, fight);
                // Most runs go to a side, where the rod can turn them; some go straight out and must simply be given line.
                RunSide = roll < tuning.straightRuns ? 0 : random.NextDouble() < 0.5 ? -1 : 1;
                break;
            case Mood.Warning:
                mood = Mood.Running;
                moodLeft = Mathf.Lerp(tuning.runSeconds.x, tuning.runSeconds.y, fight) * (0.7f + 0.6f * roll) * (0.5f + 0.5f * Stamina);
                break;
            default:
                mood = Mood.Resting;
                RunSide = 0;
                moodLeft = Mathf.Lerp(tuning.restSeconds.x, tuning.restSeconds.y, fight) * (0.7f + 0.6f * roll) * Mathf.Lerp(1.6f, 1f, Stamina);
                break;
        }
    }

    private void UpdateAngle(Mood now, float rodSide, float pullShare, float against, float seconds)
    {
        float limit = tuning.maxAngle;
        if (now == Mood.Running && RunSide != 0)
        {
            Angle = Mathf.MoveTowards(Angle, RunSide * limit, 22f * pullShare * seconds);
            // The rod held against it turns it back.
            Angle = Mathf.MoveTowards(Angle, 0f, 18f * against * seconds);
        }
        else
        {
            float drift = now == Mood.Spent ? 14f : 6f;
            Angle = Mathf.MoveTowards(Angle, rodSide * limit * 0.2f, drift * seconds);
        }

        Angle = Mathf.Clamp(Angle, -limit, limit);
    }

    /// <summary>The fish has been brought to the shore: it is landed, tired or not.</summary>
    public void Beach()
    {
        if (!Over)
        {
            Result = Outcome.Landed;
        }
    }

    /// <summary>The far bank stops a run: the fish gets no further out than <paramref name="distance"/> metres.</summary>
    public void HoldAt(float distance)
    {
        Distance = Mathf.Min(Distance, Mathf.Max(0f, distance));
    }

    /// <summary>Keeps the fish within <paramref name="degrees"/> of the cast (the water is narrower there).</summary>
    public void Confine(float degrees)
    {
        degrees = Mathf.Max(0f, degrees);
        Angle = Mathf.Clamp(Angle, -degrees, degrees);
    }
}
