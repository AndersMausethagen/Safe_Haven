using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Where a piece may stand: on the grid, flush beside a nearby building and turned to match it (or freely with Alt),
/// clear of other buildings, yards and anything solid, inside the build area, and paid for from what is carried and
/// stored. Part of BuildPlacer, in its own file.
/// </summary>
public sealed partial class BuildPlacer
{
    private Vector3 Grid(Vector3 point)
    {
        if (gridSize > 0f)
        {
            point.x = Mathf.Round(point.x / gridSize) * gridSize;
            point.z = Mathf.Round(point.z / gridSize) * gridSize;
        }

        return point;
    }

    // Free placement follows the cursor; otherwise snap flush beside the nearest building, or to the grid.
    private void ResolveSingle(Vector3 cursor, bool free, out Vector3 point, out float pieceYaw)
    {
        point = cursor;
        pieceYaw = yaw;
        if (free)
        {
            return;
        }

        if (snapDistance > 0f && TrySnapBesideNeighbour(cursor, out Vector3 snapped, out float snappedYaw))
        {
            point = Area.OnGround(snapped);
            pieceYaw = snappedYaw;
            return;
        }

        point = Area.OnGround(Grid(cursor));
    }

    private bool TrySnapBesideNeighbour(Vector3 cursor, out Vector3 point, out float pieceYaw)
    {
        point = cursor;
        pieceYaw = yaw;
        float bestGap = float.MaxValue;
        float ownReach = Mathf.Max(LocalBounds.extents.x, LocalBounds.extents.z);
        foreach (BuildingObject other in BuildingObject.Active)
        {
            FootprintRect footprint = other.Footprint;
            float reach = Mathf.Max(footprint.HalfSize.x, footprint.HalfSize.y) + ownReach + snapDistance;
            if ((new Vector2(cursor.x, cursor.z) - footprint.Center).sqrMagnitude > reach * reach)
            {
                continue;
            }

            if (BuildPlacement.TrySnapBeside(cursor, yaw, LocalBounds, footprint, snapDistance, out Vector3 snapped, out float snappedYaw, out float gap)
                && gap < bestGap)
            {
                bestGap = gap;
                point = snapped;
                pieceYaw = snappedYaw;
            }
        }

        return bestGap < float.MaxValue;
    }

    /// <summary>Why a building can't stand here, in words for the player, or null when it can. Doesn't check the cost.</summary>
    private string CheckSpot(Vector3 point, float pieceYaw)
    {
        FootprintRect footprint = BuildPlacement.Footprint(point, pieceYaw, LocalBounds);
        // A path follows the ground, slopes and all, and may cross yards: only its middle must be in the land and dry.
        bool path = Kind != null && Kind.IsPath;
        string outside = Area.CheckSpot(path ? new FootprintRect(footprint.Center, Vector2.one * 0.05f, footprint.Yaw) : footprint, point);
        if (outside != null)
        {
            return outside;
        }

        Quaternion rotation = Quaternion.Euler(0f, pieceYaw, 0f);
        Vector3 extents = new Vector3(LocalBounds.extents.x * 0.92f, Mathf.Max(0.05f, LocalBounds.extents.y - GroundLift * 0.5f), LocalBounds.extents.z * 0.92f);
        Vector3 center = point + rotation * (LocalBounds.center + Vector3.up * GroundLift * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, extents, Overlaps, rotation, blockingLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!ghost.Contains(Overlaps[i]))
            {
                return "Something is in the way.";
            }
        }

        foreach (BuildingObject other in BuildingObject.Active)
        {
            if (!path && other.TryGetYard(out FootprintRect otherYard) && BuildPlacement.Overlaps(footprint, otherYard))
            {
                return $"That's in front of the {other.Definition.DisplayName}. Keep its entrance clear.";
            }
        }

        BuildingDefinition kind = Kind;
        if (kind != null && BuildPlacement.TryGetYard(point, pieceYaw, LocalBounds, kind.YardSide, kind.YardDepth, out FootprintRect yard))
        {
            float height = Mathf.Max(0.5f, LocalBounds.size.y);
            Vector3 yardCenter = yard.WorldCenter(point.y + GroundLift + height * 0.5f);
            Vector3 yardExtents = new Vector3(yard.HalfSize.x * 0.92f, height * 0.5f, yard.HalfSize.y * 0.92f);
            count = Physics.OverlapBoxNonAlloc(yardCenter, yardExtents, Overlaps, rotation, blockingLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!ghost.Contains(Overlaps[i]) && !IsMoving(Overlaps[i]))
                {
                    return $"Its entrance is blocked. Keep {kind.YardDepth:0.#} m clear in front of it.";
                }
            }
        }

        return null;
    }

    // People and monsters walk out of the way, so they don't block a yard.
    private static bool IsMoving(Collider collider)
    {
        return collider is CharacterController || collider.attachedRigidbody != null || collider.GetComponentInParent<NavMeshAgent>() != null;
    }

    private bool CanAfford(BuildingDefinition building, int copies, out string missing)
    {
        MaterialPool pool = MaterialPool.For(gameObject);
        if (copies <= 1)
        {
            return pool.CanAfford(building.ConstructionCost, building.ConstructionItems, out missing);
        }

        scaledResources.Clear();
        foreach (ResourceAmount cost in building.ConstructionCost)
        {
            scaledResources.Add(new ResourceAmount(cost.Resource, cost.Amount * copies));
        }

        scaledItems.Clear();
        foreach (ItemAmount cost in building.ConstructionItems)
        {
            scaledItems.Add(new ItemAmount(cost.Item, cost.Amount * copies));
        }

        return pool.CanAfford(scaledResources, scaledItems, out missing);
    }
}
