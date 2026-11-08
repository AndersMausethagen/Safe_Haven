using UnityEngine;

/// <summary>
/// Where a monster belongs: its home and the way it first faced, roaming somewhere new, giving up a chase that led it
/// too far (the leash) and walking back. Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    /// <summary>A story's hold on where it belongs: meeting someone on the way to a roam's end doesn't make that spot its
    /// home, and (with <see cref="LeashOverride"/>) it chases no further than that from home: the old troll that walks to
    /// the camp and stays there.</summary>
    public bool HoldsHome { get; set; }

    /// <summary>Metres it chases from home before giving up; 0 uses its definition's leash.</summary>
    public float LeashOverride { get; set; }

    private float Leash => LeashOverride > 0f ? LeashOverride : definition.LeashRange;

    /// <summary>
    /// Makes where it stands, facing the way it faces, its home (the raid map turned under it). A roaming monster keeps
    /// the place it walks to.
    /// </summary>
    public void ResetHome()
    {
        if (!roaming)
        {
            home = transform.position;
        }

        Vector3 facing = transform.forward;
        facing.y = 0f;
        homeForward = glanceHeading = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
    }

    /// <summary>
    /// Walks to a point and makes it the new home, attacking anything it notices on the way (roaming spawns). Getting
    /// a target ends the roam: the place it was found becomes home, so the normal chase and leash rules apply.
    /// </summary>
    public void Roam(Vector3 destination)
    {
        if (definition == null || CurrentState == State.Dead || CurrentState == State.Attacking)
        {
            return;
        }

        ClearTarget();
        home = destination;
        roaming = true;
        BeginReturn();
        ApplySpeed();
    }

    private void UpdateReturn()
    {
        if (HorizontalDistance(home) <= HomeTolerance || !movement.MoveTo(home))
        {
            roaming = false;
            movement.Stop();
            SetState(State.Idle);
        }
    }

    private void GoHomeOrIdle()
    {
        if (Leash > 0f && HorizontalDistance(home) > HomeTolerance)
        {
            BeginReturn();
            return;
        }

        movement.Stop();
        SetState(State.Idle);
    }

    private void BeginReturn()
    {
        movement.RepathNow();
        senses?.Awareness.Calm();
        SetState(State.Returning);
        RefreshAmbush();
    }

    private bool IsBeyondLeash()
    {
        return Leash > 0f && HorizontalDistance(home) > Leash;
    }
}
