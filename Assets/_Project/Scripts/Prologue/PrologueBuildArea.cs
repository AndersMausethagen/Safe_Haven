using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the player may build in the prologue: only the expedition's campfire, beside the shelter, when the leader asks
/// for it (<see cref="PrologueDirector.CanBuild"/>). Placement follows the forest's real terrain rather than a flat plane.
/// Plain C#, made and entered as the build area by PrologueDirector.
/// </summary>
public sealed class PrologueBuildArea : IBuildArea
{
    private readonly PrologueDirector director;
    private readonly Transform camp;
    private readonly Terrain terrain;

    public PrologueBuildArea(PrologueDirector director, Transform camp, Terrain terrain)
    {
        this.director = director;
        this.camp = camp;
        this.terrain = terrain;
    }

    private PrologueDefinition Definition => director.Definition;

    public string WhyClosed => director.CanBuild ? null : Definition.WaitingForLeaderText;

    public string Guidance => "Build the expedition's campfire beside the shelter. ";

    public bool AllowsChanges => false;

    public float GroundHeight => camp.position.y;

    public IEnumerable<BuildingDefinition> Buildable(PlayerCrafting crafting)
    {
        yield return Definition.Campfire;
    }

    /// <summary>True when something <paramref name="reach"/> metres across at <paramref name="point"/> stands beside the shelter, while building is open.</summary>
    public bool IsBuildSpot(Vector3 point, float reach)
    {
        Vector3 offset = point - camp.position;
        offset.y = 0f;
        return director.CanBuild && offset.magnitude + reach <= Definition.BuildRadius;
    }

    public bool TryGetGroundPoint(Camera view, out Vector3 point)
    {
        point = default;
        if (!CameraPointer.TryGetRay(view, out Ray ray) || !terrain.GetComponent<TerrainCollider>().Raycast(ray, out RaycastHit hit, 200f))
        {
            return false;
        }

        point = hit.point;
        return true;
    }

    public Vector3 OnGround(Vector3 point)
    {
        point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        return point;
    }

    public string CheckSpot(FootprintRect footprint, Vector3 point)
    {
        if (!IsBuildSpot(footprint.WorldCenter(point.y), Mathf.Max(footprint.HalfSize.x, footprint.HalfSize.y)))
        {
            return "Build the fire beside the shelter.";
        }

        Vector2 x = footprint.AxisX * footprint.HalfSize.x;
        Vector2 z = footprint.AxisZ * footprint.HalfSize.y;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 sample = footprint.Center + ((corner & 1) == 0 ? x : -x) + ((corner & 2) == 0 ? z : -z);
            if (Mathf.Abs(OnGround(new Vector3(sample.x, 0f, sample.y)).y - point.y) > 0.2f)
            {
                return "The ground is too uneven. Try flatter ground beside the shelter.";
            }
        }

        return null;
    }

    public string CheckBuilding(BuildingDefinition building) => director.CanBuild && building == Definition.Campfire
        ? null : "Only the expedition's campfire can be built here, when the leader asks for it.";
}
