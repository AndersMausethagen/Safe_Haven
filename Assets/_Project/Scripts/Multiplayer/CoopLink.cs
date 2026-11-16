using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// One part of co-op (visiting, creatures, downed, pings, drops...). <see cref="CoopSession"/> owns the connection and runs
/// every part through the same life: <see cref="Begin"/> when a session starts (host or guest), <see cref="Tick"/> every
/// frame while connected, <see cref="FriendLeft"/> on the host when a guest goes, <see cref="End"/> when the session is
/// over. A new part needs nothing changed in the session but adding it to its list.
/// </summary>
public interface ICoopPart
{
    void Begin(CoopLink link);
    void Tick();
    void FriendLeft(ulong clientId);
    void End();
}

/// <summary>
/// The session's named messages, for the parts of co-op: to the host, to a guest, to every guest but one (what the host
/// passes on). Everything goes on one reliable, ordered channel unless a part asks otherwise, so nothing a part sends can
/// overtake what was sent before it (the Haven before its changes, a drop before its pickup).
/// </summary>
public sealed class CoopLink
{
    /// <summary>No client: "every guest" with nobody left out.</summary>
    public const ulong NoOne = ulong.MaxValue;

    public const NetworkDelivery Ordered = NetworkDelivery.ReliableFragmentedSequenced;

    private readonly List<ulong> scratch = new List<ulong>();
    private readonly List<ulong> one = new List<ulong>(1);

    public CoopLink(NetworkManager network)
    {
        Network = network;
    }

    public NetworkManager Network { get; }
    public bool IsHost => Network.IsServer;
    public ulong LocalId => Network.LocalClientId;

    /// <summary>The session's shared clock (seconds).</summary>
    public double Now => Network.ServerTime.Time;

    public void On(string message, CustomMessagingManager.HandleNamedMessageDelegate handler) =>
        Network.CustomMessagingManager.RegisterNamedMessageHandler(message, handler);

    /// <summary>The guests connected now, but <paramref name="except"/>. The list is reused: read it before the next call.</summary>
    public IReadOnlyList<ulong> Guests(ulong except = NoOne)
    {
        scratch.Clear();
        if (Network.IsServer)
        {
            foreach (ulong client in Network.ConnectedClientsIds)
            {
                if (client != NetworkManager.ServerClientId && client != except)
                {
                    scratch.Add(client);
                }
            }
        }

        return scratch;
    }

    public bool IsConnected(ulong clientId)
    {
        foreach (ulong client in Network.ConnectedClientsIds)
        {
            if (client == clientId)
            {
                return true;
            }
        }

        return false;
    }

    public void Send(string message, ulong to, FastBufferWriter writer, NetworkDelivery delivery = Ordered) =>
        Network.CustomMessagingManager.SendNamedMessage(message, to, writer, delivery);

    public void Send(string message, IReadOnlyList<ulong> to, FastBufferWriter writer, NetworkDelivery delivery = Ordered)
    {
        if (to.Count > 0)
        {
            Network.CustomMessagingManager.SendNamedMessage(message, to, writer, delivery);
        }
    }

    public void ToHost(string message, FastBufferWriter writer, NetworkDelivery delivery = Ordered) =>
        Send(message, NetworkManager.ServerClientId, writer, delivery);

    /// <summary>From the host to every guest but <paramref name="except"/> (the one it came from, when passing it on).</summary>
    public void ToGuests(string message, FastBufferWriter writer, ulong except = NoOne, NetworkDelivery delivery = Ordered) =>
        Send(message, Guests(except), writer, delivery);

    /// <summary>
    /// To everyone else: from the host, every guest; from a guest, the host (who passes it on with <see cref="ToGuests"/>).
    /// </summary>
    public void ToOthers(string message, FastBufferWriter writer, NetworkDelivery delivery = Ordered)
    {
        if (IsHost)
        {
            ToGuests(message, writer, NoOne, delivery);
        }
        else
        {
            ToHost(message, writer, delivery);
        }
    }

    /// <summary>
    /// For a message meant for one player (<paramref name="target"/>): from the host straight to them, from a guest through
    /// the host. True when this is the host and the message is for someone else, which it has now passed on.
    /// </summary>
    public bool PassOn(string message, ulong target, FastBufferWriter writer)
    {
        if (!IsHost || target == NetworkManager.ServerClientId)
        {
            return false;
        }

        Send(message, target, writer);
        return true;
    }

    /// <summary>A small message written by <paramref name="write"/>, in a buffer that is let go of at once.</summary>
    public static FastBufferWriter Writer(int size = 256, int maxSize = 64 * 1024) => new FastBufferWriter(size, Allocator.Temp, maxSize);

    /// <summary>One recipient, as a list (reused).</summary>
    public IReadOnlyList<ulong> Only(ulong client)
    {
        one.Clear();
        one.Add(client);
        return one;
    }
}

/// <summary>
/// Where a player is, as friends' avatars report it: the scene, and in a raid its run ("Raid_MistyWoods#777"), so friends
/// meet only on the same map. Worked out again only when the scene or the run changes.
/// </summary>
public static class CoopPlace
{
    private static FixedString128Bytes key;
    private static string text = string.Empty;
    private static UnityEngine.SceneManagement.Scene scene;
    private static int seed;
    private static bool wasArrived;
    // Not yet anywhere: this game's own, so it is never the same place as another player's.
    private static readonly string Nowhere = "~" + System.Guid.NewGuid().ToString("N").Substring(0, 12);

    public static string Here
    {
        get
        {
            Refresh();
            return text;
        }
    }

    /// <summary><see cref="Here"/> as looks carry it, for comparing without making strings.</summary>
    public static FixedString128Bytes Key
    {
        get
        {
            Refresh();
            return key;
        }
    }

    private static void Refresh()
    {
        UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int run = GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Seed : 0;
        // Nowhere yet while a scene is still being set up, or for a guest still in their own Haven before the host's arrives (both
        // are "SafeHaven" by name): the host's creatures told meanwhile were dropped or built into the scene going away, and the
        // place never changing on arrival, the host never told them again.
        bool arrived = !SceneTransition.IsBusy
                       && !(CoopSession.Current != null && !CoopSession.Current.IsHost && !HavenVisit.IsVisiting && GameSession.ActiveRaid == null);
        if (active == scene && run == seed && arrived == wasArrived && text.Length > 0)
        {
            return;
        }

        scene = active;
        seed = run;
        wasArrived = arrived;
        text = !arrived ? Nowhere : GameSession.ActiveRaid != null ? active.name + "#" + run : active.name;
        key = default;
        key.CopyFromTruncated(text);
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        text = string.Empty;
        key = default;
        seed = 0;
    }
}
