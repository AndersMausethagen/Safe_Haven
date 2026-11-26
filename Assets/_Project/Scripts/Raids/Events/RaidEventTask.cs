using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Something a raid event's choice sets the player to do at its site, instead of handing the reward over at once
/// (Docs/RaidEventsPlan_2026-11-26.md E3): hold a circle while waves come (<see cref="DefendTask"/>), carry a heavy thing to a way out
/// (<see cref="CarriedBurden"/>). Each shows one line on the raid HUD under the companions while it runs. Made by the event's outcome
/// (DefendOutcome, CarryOutcome), never placed by hand.
/// </summary>
public abstract class RaidEventTask : MonoBehaviour
{
    private static readonly List<RaidEventTask> active = new List<RaidEventTask>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    public static IReadOnlyList<RaidEventTask> Active => active;

    /// <summary>What the HUD says of it now: "Hold the bell: 0:25".</summary>
    public abstract string HudLine { get; }

    private static readonly StringBuilder Lines = new StringBuilder();

    /// <summary>Every running task's line, one a row, for the HUD.</summary>
    public static string HudLines()
    {
        if (active.Count == 0)
        {
            return "";
        }

        Lines.Clear();
        foreach (RaidEventTask task in active)
        {
            if (task != null)
            {
                if (Lines.Length > 0)
                {
                    Lines.Append('\n');
                }

                Lines.Append(task.HudLine);
            }
        }

        return Lines.ToString();
    }

    protected virtual void OnEnable() => active.Add(this);

    protected virtual void OnDisable() => active.Remove(this);

    /// <summary>Flat distance from the player, for the HUD lines.</summary>
    protected static float FlatDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;
        return offset.magnitude;
    }

    /// <summary>Carries out <paramref name="outcomes"/> at <paramref name="at"/> with the event's context.</summary>
    protected static void Apply(IReadOnlyList<RaidEventOutcome> outcomes, RaidEventContext context, Vector3 at)
    {
        if (outcomes == null || context == null)
        {
            return;
        }

        var here = new RaidEventContext
        {
            Session = context.Session,
            Director = context.Director,
            Player = context.Player,
            Position = at,
            Random = context.Random,
            Site = context.Site,
        };
        foreach (RaidEventOutcome outcome in outcomes)
        {
            outcome?.Apply(here);
        }
    }
}
