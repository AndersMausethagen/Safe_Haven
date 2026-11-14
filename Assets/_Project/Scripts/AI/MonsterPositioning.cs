using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where a monster goes in a fight, apart from whether it goes: its own side of a target others are also closing on, somewhere with a line for a shot,
/// and how fast its target is moving, for a shot aimed ahead. A plain C# part of MonsterAI, which decides what it is doing.
/// </summary>
public sealed class MonsterPositioning
{
    private const float FlankStartsWithin = 12f;
    private const float FlankStandOff = 2.2f;
    private const float FlankRefreshSeconds = 0.3f;
    private const float ShotCheckSeconds = 0.25f;
    private const float ShotSideSeconds = 2.5f;
    private const float FastestTarget = 12f;
    private const float TargetSpeedEase = 0.2f;

    private readonly Transform self;
    private readonly MonsterMovement movement;
    private readonly MonsterSenses senses;
    private readonly MonsterDefinition definition;
    private readonly List<Vector3> flankers = new List<Vector3>();

    private bool shotIsClear = true;
    private float nextShotCheckAt;
    private float shotSide = 1f;
    private float nextShotSideAt;
    private Vector3 targetWasAt;
    private Vector3 targetVelocity;
    private Vector3 chaseSpot;
    private float nextChaseSpotAt;

    public MonsterPositioning(Transform self, MonsterMovement movement, MonsterSenses senses, MonsterDefinition definition)
    {
        this.self = self;
        this.movement = movement;
        this.senses = senses;
        this.definition = definition;
    }

    /// <summary>How its target is moving, worked out from where it has been. A teleport isn't a speed.</summary>
    public Vector3 TrackTarget(Vector3 at, float deltaTime)
    {
        Vector3 moved = (at - targetWasAt) / deltaTime;
        moved.y = 0f;
        targetWasAt = at;
        targetVelocity = Vector3.Lerp(targetVelocity, Vector3.ClampMagnitude(moved, FastestTarget), TargetSpeedEase);
        return targetVelocity;
    }

    /// <summary>Whether something stands in the way of a shot. Checked now and then.</summary>
    public bool ShotIsClear(Vector3 target)
    {
        if (Time.time >= nextShotCheckAt)
        {
            nextShotCheckAt = Time.time + ShotCheckSeconds;
            shotIsClear = senses.HasClearShotTo(target);
        }

        return shotIsClear;
    }

    /// <summary>Round to where there is a line, and a little closer, instead of shooting the trees.</summary>
    public void WalkToALine(Vector3 target, Vector3 toTarget, float distance)
    {
        if (Time.time >= nextShotSideAt)
        {
            nextShotSideAt = Time.time + ShotSideSeconds;
            shotSide = Random.value < 0.5f ? -1f : 1f;
        }

        Vector3 spot = AttackTurns.CircleSpot(self.position, toTarget, shotSide, distance * 0.85f);
        movement.MoveTo(movement.TryFindGround(spot, 2f, out Vector3 ground) ? ground : target);
    }

    /// <summary>Straight at its target, until it is near enough and others are coming too: then round to its own side of it.</summary>
    public Vector3 ChaseSpot(MonsterAI owner, IDamageable target, Vector3 at, float distance, bool atThePlayer)
    {
        float spread = definition.FlankSpread;
        if (spread <= 0f || distance > FlankStartsWithin || distance <= FlankStandOff + 1f || !atThePlayer)
        {
            return at;
        }

        if (Time.time >= nextChaseSpotAt)
        {
            nextChaseSpotAt = Time.time + FlankRefreshSeconds;
            flankers.Clear();
            foreach (MonsterAI other in MonsterAI.Active)
            {
                if (other != owner && other.IsClosingOn(target))
                {
                    flankers.Add(other.transform.position);
                }
            }

            chaseSpot = AttackTurns.FlankSpot(self.position, at, flankers, spread, FlankStandOff);
        }

        return flankers.Count > 0 ? chaseSpot : at;
    }
}
