using UnityEngine;

/// <summary>
/// Sets a transport down from its kit item (right-click in the bag): a land transport on the ground ahead, a boat on the
/// nearest water deep enough to float it. The kit is what lets a boat built at a yard reach a pond far from it.
/// </summary>
public static class TransportDeployment
{
    private const float GroundReach = 2.6f;
    private const float RoomRadius = 0.7f;
    private const float WaterReach = 1.5f;
    private const float MinimumDepth = 0.45f;
    private const float HullLength = 1.8f;
    private const float HullBeam = 0.7f;
    private const float FarthestLaunch = 12f;

    public static bool Deploy(GameObject user, ItemContainer from, int slot)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        TransportDefinition definition = !stack.IsEmpty ? stack.Item.Transport : null;
        if (definition == null || user == null)
        {
            return false;
        }

        Vector3 forward = user.transform.forward;
        forward.y = 0f;
        Quaternion facing = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward : Vector3.forward, Vector3.up);
        bool boat = definition.Prefab != null && definition.Prefab.GetComponent<Rowboat>() != null;
        if (!(boat ? FindWater(user.transform, ref facing, out Vector3 spot) : FindGround(user.transform, out spot)))
        {
            GameMessages.Post(boat ? "Stand at the water's edge to launch it." : "There's no room to set it down here.", MessageKind.Warning);
            return false;
        }

        SaveableEntity.Spawn(definition.Prefab, spot, facing);
        from.RemoveAt(slot, 1);
        GameMessages.Post($"You set down the {definition.DisplayName}.");
        return true;
    }

    /// <summary>Clear ground beside the player, trying all the way round them. False when they are hemmed in.</summary>
    public static bool FindGroundAround(Transform player, out Vector3 spot)
    {
        for (int angle = 180; angle < 540; angle += 45)
        {
            if (FindGround(player, Quaternion.Euler(0f, angle, 0f) * player.forward, out spot))
            {
                return true;
            }
        }

        spot = default;
        return false;
    }

    private static bool FindGround(Transform player, out Vector3 spot) => FindGround(player, player.forward, out spot);

    private static bool FindGround(Transform player, Vector3 direction, out Vector3 spot)
    {
        spot = default;
        Vector3 ahead = player.position + direction * GroundReach + Vector3.up * 3f;
        if (!Physics.Raycast(ahead, Vector3.down, out RaycastHit ground, 8f, Transport.GroundMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        spot = ground.point + Vector3.up * 0.15f;
        bool deep = WaterSurface.TryFind(ground.point + Vector3.up * WaterReach, WaterReach, out float surface) && surface - ground.point.y > MinimumDepth;
        return ground.normal.y > 0.7f && !deep && !Physics.CheckSphere(ground.point + Vector3.up * (RoomRadius + 0.3f), RoomRadius, Transport.GroundMask, QueryTriggerInteraction.Ignore);
    }

    // Rings out from the player until the whole hull fits over water deep enough; the nearest wins. Only the middle being
    // deep isn't enough: a bow set down in the bank is thrown clear by the physics, boat and all. A boat is long and narrow,
    // so it is tried at several headings: in a small river it has to lie along the water.
    private static bool FindWater(Transform player, ref Quaternion facing, out Vector3 spot)
    {
        spot = default;
        for (float radius = 2f; radius <= FarthestLaunch; radius += 1f)
        {
            for (int angle = 0; angle < 360; angle += 20)
            {
                Vector3 point = player.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                for (int turn = 0; turn < 180; turn += 30)
                {
                    Quaternion heading = facing * Quaternion.Euler(0f, turn, 0f);
                    if (HullFits(point, heading, out float surface))
                    {
                        spot = new Vector3(point.x, surface + 0.1f, point.z);
                        facing = heading;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HullFits(Vector3 point, Quaternion heading, out float surface)
    {
        Vector3 along = heading * Vector3.forward * HullLength;
        Vector3 across = heading * Vector3.right * HullBeam;
        return DeepAt(point, out surface) && DeepAt(point + along, out _) && DeepAt(point - along, out _)
            && DeepAt(point + along * 0.5f + across, out _) && DeepAt(point + along * 0.5f - across, out _)
            && DeepAt(point - along * 0.5f + across, out _) && DeepAt(point - along * 0.5f - across, out _);
    }

    private static bool DeepAt(Vector3 point, out float surface)
    {
        surface = 0f;
        return Physics.Raycast(point + Vector3.up * 4f, Vector3.down, out RaycastHit bottom, 12f, Transport.GroundMask, QueryTriggerInteraction.Ignore)
            && WaterSurface.TryFind(bottom.point + Vector3.up * WaterReach, WaterReach, out surface)
            && surface - bottom.point.y >= MinimumDepth;
    }
}
