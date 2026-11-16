using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Horses through the portal. Setting out on a raid, the horse the player rides (or, on foot, one of theirs following
/// them) goes too, with its gear, its load, its bond and its hurts; it leaves the Haven meanwhile. In the raid it is set
/// down beside the player (back in the saddle if they rode in). It comes home at an extraction if it is ridden or within
/// reach of the player (TransportSettings › Extraction Range), as a cart does; or always, failed raid or not, if it was
/// tied at the portal it came through, where nothing touches it. Otherwise it is lost to the forest, and the report says
/// so. Lives for the whole game, made by itself.
/// </summary>
public sealed class HorseTravel : MonoBehaviour
{
    private const float FollowerReach = 20f;
    private const float TieReach = 18f;
    private const float ArrivalSeconds = 8f;

    private readonly struct Carried
    {
        public readonly string PrefabId;
        public readonly string State;
        public readonly float Health;
        public readonly bool Ridden;

        public Carried(string prefabId, string state, float health, bool ridden)
        {
            PrefabId = prefabId;
            State = state;
            Health = health;
            Ridden = ridden;
        }
    }

    private static Carried? pending;
    // The game the horse belongs to: one carried out of a game since replaced (New Game, Load) has nowhere to go.
    private static WorldState pendingWorld;
    private static HorseTravel current;

    /// <summary>The horse that came through the portal into this raid, while it is here; null otherwise.</summary>
    public static Horse InRaid { get; private set; }
    /// <summary>Where the raid's horse arrived: tied near it, it is safe and always comes home.</summary>
    public static Vector3 ArrivedAt { get; private set; }
    /// <summary>True while the raid's horse is tied at the portal.</summary>
    public static bool IsTied { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        pending = null;
        pendingWorld = null;
        InRaid = null;
        IsTied = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Make()
    {
        if (current != null)
        {
            return;
        }

        var holder = new GameObject("Horse Travel");
        DontDestroyOnLoad(holder);
        current = holder.AddComponent<HorseTravel>();
        // Only where there is a player (woken by now): the results screen between a raid and home has none, and the horse
        // waits through it.
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (pending.HasValue && current != null && Player.Current != null)
            {
                current.StartCoroutine(current.Arrive());
            }
        };
    }

    /// <summary>The horse that would go through the portal with this player now: the one they ride, or one following them.</summary>
    public static Horse Coming(GameObject player)
    {
        if (player == null || HavenVisit.IsVisiting)
        {
            return null;
        }

        if (player.TryGetComponent(out PlayerRiding riding) && riding.Riding != null && riding.Riding.IsTamed)
        {
            return riding.Riding;
        }

        Horse best = null;
        float bestDistance = FollowerReach * FollowerReach;
        foreach (Horse horse in Horse.All)
        {
            float distance = horse != null ? (horse.transform.position - player.transform.position).sqrMagnitude : float.MaxValue;
            // Not one in its stall (stabling keeps the last order), nor a friend's horse following them.
            if (horse != null && horse.IsAlive && horse.IsTamed && horse.Order == Horse.Orders.Follow && !horse.IsStabled && horse.IsMine
                && distance < bestDistance)
            {
                best = horse;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>What the portal's briefing says about the horse coming along. Empty without one.</summary>
    public static string RaidWarning(GameObject player)
    {
        Horse horse = Coming(player);
        return horse == null ? "" : $"{horse.HorseName} comes with you. Fail, or leave it more than {TransportSettings.Current.ExtractionRange:0} m away when you extract, and it is lost: unless you tie it at the portal.";
    }

    /// <summary>Setting out: the horse goes through with the player, and leaves the Haven meanwhile.</summary>
    public static void LeaveForRaid(GameObject player)
    {
        // A visitor's horse (or the host's they borrowed) stays in the host's Haven: off it before the portal.
        if (HavenVisit.IsVisiting && player != null && player.TryGetComponent(out PlayerRiding visitor) && visitor.Riding != null)
        {
            visitor.Dismount(thrown: true);
            return;
        }

        Horse horse = Coming(player);
        if (horse == null || !horse.TryGetComponent(out SaveableEntity entity) || string.IsNullOrEmpty(entity.PrefabId))
        {
            return;
        }

        bool ridden = horse.Rider != null;
        pending = new Carried(entity.PrefabId, horse.CaptureState(), horse.Health.HealthFraction, ridden);
        pendingWorld = GameSession.World;
        if (horse.Rider != null)
        {
            horse.Rider.Dismount(thrown: true);
        }

        HorseHitch.Unhitch(horse);
        // Gone from the Haven's world, as a cart is (Transport.Remove), or it comes back on returning besides the one the raid
        // sends home.
        SaveableEntity.RemoveForGood(horse.gameObject);
    }

    /// <summary>The raid is over: the horse comes home if it was ridden, near the player at an extraction, or tied at the
    /// portal; otherwise it stays in the forest. The report says which.</summary>
    public static void LeaveRaid(RaidReport report, bool extracted)
    {
        Horse horse = InRaid;
        InRaid = null;
        if (horse == null || !horse.IsAlive || !horse.TryGetComponent(out SaveableEntity entity))
        {
            if (horse != null && report != null)
            {
                report.TransportLine = Join(report.TransportLine, $"{horse.HorseName} died in the forest.");
            }

            IsTied = false;
            return;
        }

        Player player = Player.Current;
        bool ridden = horse.Rider != null;
        bool near = player != null && Vector3.Distance(player.transform.position, horse.transform.position) <= TransportSettings.Current.ExtractionRange;
        bool home = IsTied || extracted && (ridden || near);
        if (home)
        {
            if (IsTied)
            {
                horse.SetStabled("");
            }

            pending = new Carried(entity.PrefabId, horse.CaptureState(), horse.Health.HealthFraction, ridden && extracted);
            pendingWorld = GameSession.World;
        }

        if (report != null)
        {
            report.TransportLine = Join(report.TransportLine, home ? $"{horse.HorseName} came home." : $"{horse.HorseName} stayed behind in the forest, and is lost.");
        }

        IsTied = false;
    }

    /// <summary>Ties the raid's horse at the portal it came through (or unties it): safe there, and home whatever happens.</summary>
    public static bool Tie(Horse horse, bool tie)
    {
        if (horse == null || horse != InRaid)
        {
            return false;
        }

        if (tie && Vector3.Distance(horse.transform.position, ArrivedAt) > TieReach)
        {
            GameMessages.Post("Lead it back to the portal you came through to tie it there.", MessageKind.Warning);
            return false;
        }

        IsTied = tie;
        // Tied, it is out of the fight as a stabled horse is: not hunted, not hurt.
        horse.SetStabled(tie ? "raid-portal" : "");
        horse.Command(tie ? Horse.Orders.Stay : Horse.Orders.Graze);
        if (tie)
        {
            GameMessages.Post($"{horse.HorseName} is tied at the portal: safe, and it comes home whatever happens.", MessageKind.Info);
        }

        return true;
    }

    /// <summary>True when this horse is the raid's and near enough the portal to be tied there.</summary>
    public static bool CanTieHere(Horse horse) => horse != null && horse == InRaid && Vector3.Distance(horse.transform.position, ArrivedAt) <= TieReach;

    private static string Join(string line, string more) => string.IsNullOrEmpty(line) ? more : line + " " + more;

    // On the other side: set down beside the player once they stand, back in the saddle if they rode through.
    private IEnumerator Arrive()
    {
        Carried carried = pending.Value;
        pending = null;
        if (GameSession.World != pendingWorld)
        {
            yield break;
        }

        // A generated raid moves the player to its start while it is laid out, behind the loading screen.
        bool ready = false;
        SceneTransition.WhenReady(() => ready = true);
        while (!ready)
        {
            yield return null;
        }

        Player player = null;
        for (float waited = 0f; waited < ArrivalSeconds; waited += Time.unscaledDeltaTime)
        {
            player = Player.Current;
            if (player != null && player.TryGetComponent(out PlayerMovement feet) && feet.IsGrounded)
            {
                break;
            }

            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.4f);
        player = player != null ? player : Player.Current;
        if (player == null)
        {
            // Nobody here to come to (a scene with no player): it waits for the next one, if the game is still the same.
            if (!pending.HasValue && GameSession.World == pendingWorld)
            {
                pending = carried;
            }

            yield break;
        }

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SaveableEntity prefab = content != null ? content.FindPrefab(carried.PrefabId) : null;
        if (prefab == null)
        {
            yield break;
        }

        Vector3 at = player.transform.position + player.transform.right * 2.5f;
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
        {
            at = hit.position;
        }

        SaveableEntity spawned = SaveableEntity.Spawn(prefab, at, player.transform.rotation);
        Horse horse = spawned.GetComponent<Horse>();
        horse.RestoreState(carried.State);
        horse.Health.SetHealthFraction(carried.Health);
        bool raid = RaidDirector.Current != null;
        InRaid = raid ? horse : null;
        ArrivedAt = horse.transform.position;
        IsTied = false;
        if (raid && horse.IsStabled)
        {
            horse.SetStabled("");
        }

        if (carried.Ridden)
        {
            PlayerRiding.For(player.gameObject).Mount(horse, animate: false);
        }

        // The save on coming home was written before the horse was here.
        if (!raid)
        {
            SaveGameService.SaveAfterArrival();
        }
    }
}
