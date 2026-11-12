using UnityEngine;

/// <summary>
/// Throwing a lasso (using the item, from the bag or the skill bar): it goes over the wild horse in reach that the
/// player faces most, if that horse hasn't bolted, and the struggle begins (HorseTaming). A horse stays put only for a
/// player it hasn't heard, so the way in is a crouched walk (HorseMotion). The lasso isn't used up.
/// </summary>
public static class HorseLasso
{
    private const float FacingDegrees = 75f;
    private const float NearbyHint = 30f;

    public static bool Throw(GameObject user)
    {
        Player player = user != null ? user.GetComponent<Player>() : null;
        PlayerRiding riding = PlayerRiding.For(user);
        if (player == null || riding == null || riding.IsRiding || riding.IsHoldingRope)
        {
            return false;
        }

        Horse best = null;
        float bestScore = float.MaxValue;
        bool anyNear = false;
        Vector3 at = user.transform.position;
        Vector3 facing = user.transform.forward;
        foreach (Horse horse in Horse.All)
        {
            if (horse == null || !horse.IsWild || !horse.IsAlive || horse.Taming == null)
            {
                continue;
            }

            Vector3 offset = horse.transform.position - at;
            offset.y = 0f;
            float distance = offset.magnitude;
            anyNear |= distance < NearbyHint;
            float angle = Vector3.Angle(facing, offset);
            if (distance > horse.Definition.LassoRange || angle > FacingDegrees)
            {
                continue;
            }

            float score = distance + angle / 25f;
            if (score < bestScore)
            {
                best = horse;
                bestScore = score;
            }
        }

        if (best == null)
        {
            GameMessages.Post(anyNear ? "Too far for the rope. Creep closer, crouched, and face it." : "There is nothing here to rope.", MessageKind.Warning);
            return false;
        }

        if (best.Motion.IsFleeing)
        {
            GameMessages.Post("It is running. Wait for it to settle, and come at it quietly.", MessageKind.Warning);
            return false;
        }

        // In co-op the roped horse is run by this game from now on (CoopHorses), unless a friend has it already.
        if (!CoopHorses.TakeInHand(best))
        {
            return false;
        }

        Vector3 toward = best.transform.position - at;
        toward.y = 0f;
        if (user.TryGetComponent(out PlayerMovement movement) && toward.sqrMagnitude > 0.01f)
        {
            movement.SnapFacing(toward.normalized);
        }

        best.Taming.BeginStruggle(player);
        return true;
    }
}
