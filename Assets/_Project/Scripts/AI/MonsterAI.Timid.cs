using UnityEngine;

/// <summary>
/// Game: a creature that runs instead of fighting (MonsterDefinition › Game › Timid). It bolts when it is struck, and when
/// the player comes into its sight within its Detection Range or steps inside its Close Sense Range, so it is hunted by
/// sneaking close or with a bow, not by walking up to it. It runs from where the fright came from for Bolt Seconds, then
/// settles where it has got to and goes back to grazing. Meant for Neutral creatures (which never pick a target), but it
/// asks nothing of the faction. Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    private const float StartleInterval = 0.35f;
    private const float BoltStep = 0.5f;
    private const float BoltStride = 9f;

    private float boltUntil = float.NegativeInfinity;
    private float nextStartleCheck;
    private float nextBoltStep;
    private bool wasBolting;
    private Vector3 boltFrom;

    /// <summary>True while it is running from something.</summary>
    public bool IsBolting => Time.time < boltUntil;

    // Run by Update before anything a calm creature does. True while it is running (and nothing else should move it).
    private bool UpdateTimid()
    {
        if (!definition.Timid)
        {
            return false;
        }

        if (Time.time >= nextStartleCheck)
        {
            nextStartleCheck = Time.time + StartleInterval;
            Player player = Player.Current;
            if (player != null)
            {
                float distance = HorizontalDistance(player.transform.position);
                // Seen as any creature sees: a sneaking hunter is seen from closer, one in cover not at all.
                if (distance < definition.CloseSenseRange || senses.CanNotice(player.transform))
                {
                    Bolt(player.transform.position);
                }
            }
        }

        if (!IsBolting)
        {
            if (wasBolting)
            {
                wasBolting = false;
                SettleAfterBolt();
            }

            return false;
        }

        wasBolting = true;
        if (Time.time >= nextBoltStep)
        {
            nextBoltStep = Time.time + BoltStep;
            Vector3 away = transform.position - boltFrom;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            movement.SetSpeed(definition.MoveSpeed * HeldScale);
            // Straight away if there is ground that way, else to one side or the other: a cliff or the water turns it, it does not stop.
            foreach (float turn in new[] { 0f, 50f, -50f, 100f, -100f })
            {
                Vector3 destination = transform.position + Quaternion.Euler(0f, turn, 0f) * away * BoltStride;
                if (movement.TryFindGround(destination, 3f, out Vector3 ground) && movement.MoveTo(ground))
                {
                    break;
                }
            }
        }

        return true;
    }

    /// <summary>Sends it running from a point for the definition's Bolt Seconds (struck, or frightened).</summary>
    public void Bolt(Vector3 from)
    {
        if (definition == null || !definition.Timid || CurrentState == State.Dead)
        {
            return;
        }

        bool already = IsBolting;
        boltFrom = from;
        boltUntil = Time.time + definition.BoltSeconds;
        if (already)
        {
            return;
        }

        // Up from its grazing or its sleep, and off.
        nextBoltStep = 0f;
        idleLife?.Reset();
        if (pose != null)
        {
            pose.Rest = 0f;
        }

        if (CurrentState != State.Idle && CurrentState != State.Staggered)
        {
            SetState(State.Idle);
        }
    }

    // Where it stops is where it lives now: it does not walk back to where it was frightened.
    private void SettleAfterBolt()
    {
        home = OnWalkableGround(transform.position);
        ApplySpeed();
    }
}
