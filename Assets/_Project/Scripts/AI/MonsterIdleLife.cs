using UnityEngine;

/// <summary>
/// What a monster does while nothing is going on, so a forest looks lived in: it wanders a little around home, stands, grazes or
/// sleeps (MonsterDefinition › Idle Life). A sleeper notices less, so it can be crept past. Plain C#, owned by a MonsterAI, which
/// carries it out only while idle and drops it at once for anything else. What it does next is decided here, so it can be tested.
/// </summary>
public sealed class MonsterIdleLife
{
    public enum Doing
    {
        Standing,
        Walking,
        Grazing,
        Sleeping,
    }

    private const float ArriveDistance = 0.8f;
    private const float LongestWalk = 12f;

    private readonly MonsterDefinition definition;

    public MonsterIdleLife(MonsterDefinition definition)
    {
        this.definition = definition;
    }

    public Doing Now { get; private set; }
    public Vector3 GoingTo { get; private set; }

    /// <summary>When it moves on to what comes next.</summary>
    public float Until { get; private set; } = -1f;

    /// <summary>True when this kind of creature does anything at all while idle.</summary>
    public bool IsOn => definition.IdleWanderRadius > 0f || definition.IdleGrazeChance > 0f || definition.IdleSleepChance > 0f;

    /// <summary>How much of what it sees gets through: a sleeper is hard to wake.</summary>
    public float Attention => Now == Doing.Sleeping ? definition.IdleSleepAttention : 1f;

    /// <summary>What it settles into after a walk, from a random 0 to 1.</summary>
    public static Doing Settle(float roll, float graze, float sleep)
    {
        if (roll < sleep)
        {
            return Doing.Sleeping;
        }

        return roll < sleep + graze ? Doing.Grazing : Doing.Standing;
    }

    /// <summary>Somewhere to wander to around <paramref name="home"/>, from two random 0 to 1s: spread evenly over the circle.</summary>
    public static Vector3 WanderPoint(Vector3 home, float radius, float angleRoll, float distanceRoll)
    {
        float angle = angleRoll * Mathf.PI * 2f;
        float distance = radius * Mathf.Sqrt(distanceRoll);
        return home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
    }

    /// <summary>It stops whatever it was doing, and takes a moment before it starts on something else.</summary>
    public void Reset()
    {
        Now = Doing.Standing;
        Until = Time.time + Random.Range(definition.IdlePauseSeconds.x, definition.IdlePauseSeconds.y);
    }

    public void Walk(Vector3 point)
    {
        Now = Doing.Walking;
        GoingTo = point;
        Until = Time.time + LongestWalk;
    }

    public bool HasArrived(Vector3 at)
    {
        at.y = GoingTo.y;
        return (at - GoingTo).sqrMagnitude <= ArriveDistance * ArriveDistance || Time.time >= Until;
    }

    public void Settle()
    {
        Now = Settle(Random.value, definition.IdleGrazeChance, definition.IdleSleepChance);
        // Sleep lasts longer than a pause in the walk.
        float scale = Now == Doing.Sleeping ? 4f : 1f;
        Until = Time.time + Random.Range(definition.IdlePauseSeconds.x, definition.IdlePauseSeconds.y) * scale;
    }
}
