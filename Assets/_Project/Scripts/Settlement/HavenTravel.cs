using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Going straight to a building or person from the Haven window: the player steps out beside them, on the side they are coming
/// from, facing them. Runs the clearing without walking it, and never during a breach, when where the player stands matters.
/// </summary>
public static class HavenTravel
{
    private const float StandOff = 1.4f;
    private const float GroundSearch = 6f;
    private const float RayHeight = 30f;
    // How near a following horse must be to come along.
    private const float FollowerReach = 20f;

    /// <summary>True while going somewhere is allowed (<see cref="WhyShut"/>).</summary>
    public static bool IsOpen => WhyShut() == null;

    /// <summary>
    /// Why the player can't go across the Haven now, in words for them, or null when they can: not during a breach, nor with something at
    /// their heels. The one rule for every way across (the Haven window, the journal, a resident's card, the portal's old ways, a stone):
    /// only the stones and the portal refused a fight, so "Go there" from the Haven window escaped one (review 2026-11-27).
    /// </summary>
    public static string WhyShut()
    {
        if (WardBreachDirector.UnderWay)
        {
            return "Not during a breach.";
        }

        return GameStateManager.InCombat ? "Not with something at your heels." : null;
    }

    public static void AddGoTo(HavenEntry entry, Transform target)
    {
        string shut = WhyShut();
        entry.Actions.Add(new HavenAction(shut == null ? "Go there" : shut.TrimEnd('.'), () => GoTo(target), shut == null, primary: false));
    }

    public static void GoTo(Transform target)
    {
        Player player = Player.Current;
        if (target == null || player == null || !player.TryGetComponent(out PlayerPersistence persistence))
        {
            return;
        }

        if (WhyShut() is string shut)
        {
            GameMessages.Post(shut, MessageKind.Warning);
            return;
        }

        // A horse under the player goes with them (the user, 2026-11-27: the horse is the answer to the long walks, so it mustn't
        // be left behind at the stone); a boat's seat would put the body back the next frame: get down first.
        Horse ridden = null;
        if (player.TryGetComponent(out PlayerMovement movement) && movement.IsSeated)
        {
            if (!CanTakeHorse(player, out ridden, out string why))
            {
                GameMessages.Post(why, MessageKind.Warning);
                return;
            }
        }

        Vector3 from = player.transform.position;
        List<Horse> following = Following(player, from);
        if (ridden != null)
        {
            player.GetComponent<PlayerRiding>().Dismount(thrown: true);
        }

        Vector3 spot = SpotBeside(target, from);
        Vector3 facing = target.position - spot;
        facing.y = 0f;
        float yaw = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing).eulerAngles.y : player.transform.eulerAngles.y;
        persistence.MoveTo(new PlayerPose(spot, yaw));
        Quaternion turned = Quaternion.Euler(0f, yaw, 0f);
        if (ridden != null && ridden.Motion.PutAt(HorseSpot(spot, turned, 0), turned))
        {
            PlayerRiding.For(player.gameObject).Mount(ridden, animate: false);
        }

        for (int i = 0; i < following.Count; i++)
        {
            following[i].Motion.PutAt(HorseSpot(spot, turned, i + 1), turned);
        }

        Arrived?.Invoke(from, spot, ridden != null);
    }

    /// <summary>Someone went somewhere this way: where from, where to, and whether in the saddle (PlaytestLogger).</summary>
    public static event System.Action<Vector3, Vector3, bool> Arrived;

    // Entering Play doesn't reload scripts in this project: a listener left from the last session would carry over.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Arrived = null;

    // Seated: on a tamed horse of theirs, settled, with nothing hitched behind it, it comes along; anything else (a boat, a horse
    // being broken in, a cart behind) and they get down first.
    private static bool CanTakeHorse(Player player, out Horse horse, out string why)
    {
        horse = null;
        why = "Get down first to go there.";
        if (!player.TryGetComponent(out PlayerRiding riding) || riding.Riding == null || riding.IsMounting)
        {
            return false;
        }

        Horse under = riding.Riding;
        if (!under.IsAlive || !under.IsTamed || !under.IsMine || under.Taming != null && under.Taming.IsBreaking || under.Motion.IsSwimming)
        {
            return false;
        }

        if (HorseHitch.Pulling(under) != null)
        {
            why = "Unhitch the cart first to go there.";
            return false;
        }

        horse = under;
        return true;
    }

    // The player's own horses following them nearby come too: on foot, so the horse is there when they arrive.
    private static List<Horse> Following(Player player, Vector3 from)
    {
        var found = new List<Horse>();
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsAlive && horse.IsTamed && horse.IsMine && horse.Rider == null && !horse.IsStabled
                && horse.Order == Horse.Orders.Follow && HorseHitch.Pulling(horse) == null && CoopHorses.HeldByFriend(horse) == null
                && (horse.transform.position - from).sqrMagnitude <= FollowerReach * FollowerReach)
            {
                found.Add(horse);
            }
        }

        return found;
    }

    // Beside the player on walkable ground: the ridden one (0) at their side, followers a step behind it.
    private static Vector3 HorseSpot(Vector3 spot, Quaternion facing, int index)
    {
        Vector3 at = spot + facing * new Vector3(index == 0 ? 1.6f : -2f, 0f, -2.5f * index);
        return NavMesh.SamplePosition(at, out NavMeshHit hit, GroundSearch, NavMesh.AllAreas) ? hit.position : spot;
    }

    /// <summary>The spot just outside what stands at the target, towards the player, on walkable ground when there is some.</summary>
    public static Vector3 SpotBeside(Transform target, Vector3 from)
    {
        Vector3 away = from - target.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : -target.forward;
        float radius = StandOff;
        if (target.TryGetComponent(out BuildingObject building))
        {
            Bounds bounds = building.LocalBounds;
            radius += Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        Vector3 spot = target.position + away * radius;
        if (NavMesh.SamplePosition(spot, out NavMeshHit hit, GroundSearch, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return Physics.Raycast(spot + Vector3.up * RayHeight, Vector3.down, out RaycastHit ground, RayHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? ground.point : spot;
    }
}
