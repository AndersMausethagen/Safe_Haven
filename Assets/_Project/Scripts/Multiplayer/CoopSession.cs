#if SAFEHAVEN_STEAM && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define STEAM_ON
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum CoopState
{
    Offline,
    Hosting,
    Joining,
    Joined,
}

/// <summary>
/// Co-op: one player hosts, up to <see cref="MultiplayerSettings.MaxPlayers"/> play together. Owns the Netcode
/// NetworkManager (made here, in code) and picks its transport per session:
/// <list type="bullet">
/// <item>With Steam running (<see cref="SteamPlatform"/>): Steam's relay (SteamNetworkingSocketsTransport), addressed by
/// Steam id. Hosting opens a friends-only <see cref="SteamLobby"/>; friends join from an invite, "Join Game" in their
/// friends list, or a +connect_lobby launch. Nobody's IP address is shown and no ports need opening.</item>
/// <item>Without Steam: Unity Transport, joined by IP (LAN, or a forwarded port).</item>
/// </list>
/// <para>
/// While a session runs, the Escape menu no longer stops the world (<see cref="GamePause.FreezesTime"/>): one player
/// can't pause their friends. Each player keeps their own <see cref="Player"/> and save; friends appear beside them as
/// <see cref="NetworkPlayerAvatar"/>s. Netcode's own scene management is off, so the game's scene changes are untouched.
/// A Steam join asked for where there is no player yet (the main menu) waits until a game is loaded.
/// </para>
/// <para>
/// Testing in the Editor (Multiplayer Play Mode): an additional instance tagged "Host" hosts and one tagged "Guest" joins
/// 127.0.0.1 as soon as it has a player; additional instances always save to a throwaway folder.
/// </para>
/// </summary>
public sealed class CoopSession : MonoBehaviour, IGameSystem
{
    public const string HostTag = "Host";
    public const string GuestTag = "Guest";
    public const string LocalAddress = "127.0.0.1";
    private const string WorldMessage = "SafeHaven.World";
    private const string WorldRequest = "SafeHaven.WorldPlease";
    private const string ClockMessage = "SafeHaven.Clock";
    private const string EntitiesMessage = "SafeHaven.Entities";
    private const float ClockInterval = 5f;

    private MultiplayerSettings settings;
    private SteamPlatform steam;
    private SteamLobby lobby;
    private NetworkManager network;
    private UnityTransport directTransport;
    private NetworkTransport steamTransport;
    private CoopState state;
    private string autoStart;
    private ulong pendingLobby;
    private readonly WorldTransfer.Receiver worldPieces = new WorldTransfer.Receiver();
    private int transfers;
    private float nextClock;
    private float nextSync;
    private HavenSync sync;
    private readonly VisitorAccess access = new VisitorAccess();
    private readonly List<ulong> recipients = new List<ulong>();

    public static CoopSession Current { get; private set; }

    public CoopState State => state;
    public bool IsActive => state != CoopState.Offline;
    public bool IsHost => state == CoopState.Hosting;
    /// <summary>True when this session runs through Steam (friends are invited, not given an address).</summary>
    public bool ViaSteam { get; private set; }
    /// <summary>True when sessions can go through Steam now.</summary>
    public bool SteamReady => steam != null && steam.IsReady && (network == null || steamTransport != null);
    /// <summary>Players in the session now, you included (1 while offline).</summary>
    public int PlayerCount => IsActive ? Mathf.Max(1, NetworkPlayerAvatar.All.Count) : 1;
    /// <summary>Why the last attempt or session ended, for the menu (empty when it ended normally).</summary>
    public string LastProblem { get; private set; } = string.Empty;

    public event Action<CoopState> StateChanged;

    /// <summary>Friends' questions to the host (building, the stash) and the answers.</summary>
    public VisitorAccess Access => access;

    public void Initialise(MultiplayerSettings multiplayer, SteamPlatform platform)
    {
        settings = multiplayer;
        steam = platform;
        Current = this;
        sync = new HavenSync();
        NetworkPlayerAvatar.FriendArrived += OnFriendArrived;
        NetworkPlayerAvatar.FriendLeft += OnFriendLeft;
        if (steam != null && steam.IsReady)
        {
            lobby = new SteamLobby();
            lobby.JoinAsked += OnSteamJoinAsked;
            lobby.HostFound += OnSteamHostFound;
            lobby.Failed += OnSteamFailed;
            steam.ShuttingDown += DropLobby;
            // Started by Steam to join a friend: "+connect_lobby <id>" on the command line.
            if (SteamLobby.TryParseConnect(string.Join(" ", Environment.GetCommandLineArgs()), out ulong launchLobby))
            {
                pendingLobby = launchLobby;
            }
        }
#if UNITY_EDITOR
        PrepareTestInstance();
#endif
    }

    public void OnSceneEntered(Scene scene)
    {
        if (Player.Current == null)
        {
            return;
        }

        // A Steam join asked for before there was a game to bring into it.
        if (pendingLobby != 0 && lobby != null)
        {
            ulong id = pendingLobby;
            pendingLobby = 0;
            OnSteamJoinAsked(id);
        }

        // A tagged test instance starts once it has a player to bring.
        if (!string.IsNullOrEmpty(autoStart) && !IsActive)
        {
            string role = autoStart;
            autoStart = null;
            bool started = role == HostTag ? Host(out string problem) : Join(LocalAddress, out problem);
            Debug.Log(started ? $"Co-op test instance: {role} started." : $"Co-op test instance: {role} failed: {problem}");
        }
    }

    /// <summary>Opens this game to friends: through Steam when it runs, else on the LAN/IP port.</summary>
    public bool Host(out string problem)
    {
        if (!Prepare(out problem))
        {
            return false;
        }

        bool viaSteam = SteamReady;
        if (!viaSteam)
        {
            directTransport.SetConnectionData(LocalAddress, settings.Port, "0.0.0.0");
        }

        Use(viaSteam);
        network.ConnectionApprovalCallback = Approve;
        if (!network.StartHost())
        {
            problem = viaSteam ? "Couldn't open a session through Steam." : $"Couldn't open a session on port {settings.Port}.";
            return false;
        }

        // Friends ask for the Haven once they are fully connected (sent any earlier, it could arrive before they listen).
        network.CustomMessagingManager.RegisterNamedMessageHandler(WorldRequest, (sender, _) => SendWorld(sender));
        network.CustomMessagingManager.RegisterNamedMessageHandler(EntitiesMessage, OnEntitiesFromGuest);
        access.StartHosting(network);
        SetState(CoopState.Hosting);
        if (viaSteam)
        {
            lobby.Open(settings.MaxPlayers, settings.ProtocolVersion);
            GameMessages.Post("Your Haven is open to friends. Invite them from the Escape menu or Steam.");
        }
        else
        {
            GameMessages.Post($"Your Haven is open to friends on port {settings.Port}.");
        }

        return true;
    }

    /// <summary>Opens Steam's invite dialog while hosting through Steam.</summary>
    public bool InviteFriends()
    {
        return IsHost && ViaSteam && lobby != null && lobby.Invite();
    }

    /// <summary>Joins the session at <paramref name="address"/> (an IP, optionally with ":port"), without Steam.</summary>
    public bool Join(string address, out string problem)
    {
        if (!TryParseAddress(address, settings.Port, out string ip, out ushort port))
        {
            problem = $"'{address}' isn't an address to join.";
            return false;
        }

        if (!Prepare(out problem))
        {
            return false;
        }

        directTransport.SetConnectionData(ip, port);
        Use(false);
        return StartClient($"Couldn't reach {ip}:{port}.", out problem);
    }

    /// <summary>
    /// Ends the session (hosting: for everyone). A guest goes home to their own Haven with what their character gained,
    /// unless <paramref name="headHome"/> is false (the game is going to the main menu).
    /// </summary>
    public void Leave(bool headHome = true)
    {
        if (network != null && (network.IsListening || network.IsClient || network.IsServer))
        {
            network.Shutdown();
        }

        lobby?.Close();
        if (IsActive)
        {
            GameMessages.Post(IsHost ? "Your Haven is closed to friends." : "You left the session.");
        }

        SetState(CoopState.Offline);
        HavenVisit.End(Content(), headHome);
    }

    /// <summary>Splits "host:port" (port optional).</summary>
    public static bool TryParseAddress(string address, ushort defaultPort, out string ip, out ushort port)
    {
        ip = null;
        port = defaultPort;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        string text = address.Trim();
        int colon = text.LastIndexOf(':');
        if (colon > 0)
        {
            if (!ushort.TryParse(text.Substring(colon + 1), out port) || port == 0)
            {
                return false;
            }

            text = text.Substring(0, colon);
        }

        if (!System.Net.IPAddress.TryParse(text, out System.Net.IPAddress parsed) ||
            parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        ip = text;
        return true;
    }

    /// <summary>Whether a joining player may come in: same version, and room for them.</summary>
    public static bool MayJoin(string theirVersion, string ourVersion, int playersNow, int maxPlayers, out string reason)
    {
        if (theirVersion != ourVersion)
        {
            reason = "The host is playing a different version of Safe Haven.";
            return false;
        }

        if (playersNow >= maxPlayers)
        {
            reason = $"The session is full ({maxPlayers} players).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    // ---- Steam

    // A friend's invite or "Join Game": join them, leaving any session we are in, once there is a game to bring.
    private void OnSteamJoinAsked(ulong lobbyId)
    {
        if (Player.Current == null)
        {
            pendingLobby = lobbyId;
            GameMessages.Post("Load a game to join your friend.");
            return;
        }

        if (IsActive)
        {
            Leave();
        }

        GameMessages.Post("Joining your friend...");
        lobby.Enter(lobbyId);
    }

    private void OnSteamHostFound(ulong hostId, string hostVersion)
    {
        if (!MayJoin(settings.ProtocolVersion, hostVersion ?? string.Empty, 0, 1, out string reason))
        {
            LastProblem = reason;
            GameMessages.Post(reason, MessageKind.Warning);
            lobby.Close();
            return;
        }

        if (!JoinSteam(hostId, out string problem))
        {
            LastProblem = problem;
            GameMessages.Post(problem, MessageKind.Warning);
            lobby.Close();
        }
    }

    // Leaves the lobby and drops its Steam callbacks while Steam still runs (whichever of us goes first).
    private void DropLobby()
    {
        if (lobby == null)
        {
            return;
        }

        if (steam != null && steam.IsReady)
        {
            lobby.Dispose();
        }

        lobby = null;
    }

    private void OnSteamFailed(string problem)
    {
        LastProblem = problem;
        GameMessages.Post(problem, MessageKind.Warning);
    }

    private bool JoinSteam(ulong hostId, out string problem)
    {
        if (!Prepare(out problem))
        {
            return false;
        }

        if (!SteamReady)
        {
            problem = "Steam isn't running.";
            return false;
        }

#if STEAM_ON
        ((Netcode.Transports.SteamNetworkingSocketsTransport)steamTransport).ConnectToSteamID = hostId;
#endif
        Use(true);
        return StartClient("Couldn't reach your friend through Steam.", out problem);
    }

    // ---- Netcode

    private bool StartClient(string failure, out string problem)
    {
        network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(settings.ProtocolVersion);
        if (!network.StartClient())
        {
            problem = failure;
            return false;
        }

        problem = string.Empty;
        // Guests hear the host's world and clock.
        network.CustomMessagingManager.RegisterNamedMessageHandler(WorldMessage, OnWorldPiece);
        network.CustomMessagingManager.RegisterNamedMessageHandler(ClockMessage, OnClock);
        network.CustomMessagingManager.RegisterNamedMessageHandler(EntitiesMessage, OnEntitiesFromHost);
        access.StartGuest(network);
        SetState(CoopState.Joining);
        return true;
    }

    private void Use(bool viaSteam)
    {
        ViaSteam = viaSteam;
        network.NetworkConfig.NetworkTransport = viaSteam ? steamTransport : directTransport;
    }

    private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // The host's own connection is always let in.
        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            return;
        }

        string theirs = request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : string.Empty;
        response.Approved = MayJoin(theirs, settings.ProtocolVersion, network.ConnectedClientsIds.Count, settings.MaxPlayers, out string reason);
        response.CreatePlayerObject = response.Approved;
        response.Reason = reason;
    }

    private bool Prepare(out string problem)
    {
        problem = string.Empty;
        LastProblem = string.Empty;
        if (IsActive)
        {
            problem = "Already in a session.";
            return false;
        }

        if (GameSession.ActiveRaid != null)
        {
            problem = "Finish your raid first.";
            return false;
        }

        if (settings.PlayerPrefab == null)
        {
            problem = "Multiplayer Settings has no Network Player prefab.";
            return false;
        }

        if (network != null && network.ShutdownInProgress)
        {
            problem = "The last session is still closing. Try again in a moment.";
            return false;
        }

        if (network == null)
        {
            CreateNetwork();
        }

        return true;
    }

    // Built inactive so Netcode's Awake finds its configuration in place. Both transports live on it; each session uses one.
    private void CreateNetwork()
    {
        var holder = new GameObject("Coop Network");
        holder.SetActive(false);
        DontDestroyOnLoad(holder);
        directTransport = holder.AddComponent<UnityTransport>();
#if STEAM_ON
        if (steam != null && steam.IsReady)
        {
            steamTransport = holder.AddComponent<Netcode.Transports.SteamNetworkingSocketsTransport>();
        }
#endif
        network = holder.AddComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = directTransport,
            PlayerPrefab = settings.PlayerPrefab.gameObject,
            EnableSceneManagement = false,
            ConnectionApproval = true,
            TickRate = 30,
        };
        network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = settings.PlayerPrefab.gameObject });
        network.RunInBackground = true;
        holder.SetActive(true);
        network.OnClientConnectedCallback += OnClientConnected;
        network.OnClientDisconnectCallback += OnClientDisconnected;
        network.OnTransportFailure += OnTransportFailure;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (state == CoopState.Joining && clientId == network.LocalClientId)
        {
            SetState(CoopState.Joined);
            using var ask = new FastBufferWriter(4, Allocator.Temp);
            network.CustomMessagingManager.SendNamedMessage(WorldRequest, NetworkManager.ServerClientId, ask, NetworkDelivery.Reliable);
        }
    }

    // ---- The host's world, for visiting guests (HavenVisit)

    private void SendWorld(ulong clientId)
    {
        if (Player.Current == null || Player.Current.Persistence == null)
        {
            Debug.LogWarning("Co-op: no player to send a world from.");
            return;
        }

        SaveData world = HavenVisit.WorldOf(SaveGameService.Capture(), HavenVisit.HostIsHome());
        byte[] packed = WorldTransfer.Pack(world);
        var pieces = WorldTransfer.Split(packed);
        int transfer = ++transfers;
        string hostName = GameSession.Appearance != null ? GameSession.Appearance.DisplayName : CharacterAppearance.DefaultName;
        for (int index = 0; index < pieces.Count; index++)
        {
            byte[] piece = pieces[index];
            using var writer = new FastBufferWriter(piece.Length + 256, Allocator.Temp);
            writer.WriteValueSafe(transfer);
            writer.WriteValueSafe(index);
            writer.WriteValueSafe(pieces.Count);
            writer.WriteValueSafe(hostName);
            writer.WriteValueSafe(piece);
            network.CustomMessagingManager.SendNamedMessage(WorldMessage, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        Debug.Log($"Co-op: sent the Haven to player {clientId} ({packed.Length / 1024f:0.0} KB in {pieces.Count} pieces).");
    }

    private void OnWorldPiece(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int transfer);
        reader.ReadValueSafe(out int index);
        reader.ReadValueSafe(out int count);
        reader.ReadValueSafe(out string hostName);
        reader.ReadValueSafe(out byte[] piece);
        byte[] packed = worldPieces.Add(transfer, index, count, piece);
        if (packed == null)
        {
            return;
        }

        SaveData world;
        try
        {
            world = WorldTransfer.Unpack(packed);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            OnVisitFailed("Your friend's Haven didn't arrive whole.");
            return;
        }

        if (!HavenVisit.Begin(world, hostName, Content(), out string problem))
        {
            OnVisitFailed(problem);
        }
    }

    private void OnVisitFailed(string problem)
    {
        LastProblem = problem;
        GameMessages.Post(problem, MessageKind.Warning);
        Leave(false);
    }

    private void Update()
    {
        if (network == null || (state != CoopState.Hosting && state != CoopState.Joined))
        {
            return;
        }

        if (Time.unscaledTime >= nextSync)
        {
            nextSync = Time.unscaledTime + HavenSync.Interval;
            ShareChanges();
            access.Tick(IsHost);
        }

        if (IsHost)
        {
            ShareClock();
        }
    }

    // ---- Keeping the Haven in step (HavenSync)

    // The host shares what changed in its Haven with every guest; a visiting guest shares what changed in their copy.
    private void ShareChanges()
    {
        if (IsHost)
        {
            if (Guests(NetworkManager.ServerClientId).Count == 0)
            {
                return;
            }

            foreach (byte[] packet in sync.Collect(true))
            {
                SendPacket(EntitiesMessage, packet, Guests(NetworkManager.ServerClientId));
            }
        }
        else if (HavenVisit.IsVisiting)
        {
            foreach (byte[] packet in sync.Collect(false))
            {
                SendPacket(EntitiesMessage, packet, ToHost());
            }
        }
    }

    // A guest's change: taken on here, and passed to the other guests.
    private void OnEntitiesFromGuest(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte[] packet);
        try
        {
            sync.Apply(packet, true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return;
        }

        SendPacket(EntitiesMessage, packet, Guests(sender));
    }

    private void OnEntitiesFromHost(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte[] packet);
        if (!HavenVisit.IsVisiting)
        {
            return;
        }

        try
        {
            sync.Apply(packet, false);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    // Sent in order with the world itself (same channel), so no change can arrive before the Haven it belongs to.
    private void SendPacket(string message, byte[] packet, IReadOnlyList<ulong> to)
    {
        if (to.Count == 0)
        {
            return;
        }

        using var writer = new FastBufferWriter(packet.Length + 16, Allocator.Temp);
        writer.WriteValueSafe(packet);
        network.CustomMessagingManager.SendNamedMessage(message, to, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    // Every guest but one (the sender of what is passed on, or nobody: the host's own id).
    private IReadOnlyList<ulong> Guests(ulong except)
    {
        recipients.Clear();
        foreach (ulong client in network.ConnectedClientsIds)
        {
            if (client != NetworkManager.ServerClientId && client != except)
            {
                recipients.Add(client);
            }
        }

        return recipients;
    }

    private IReadOnlyList<ulong> ToHost()
    {
        recipients.Clear();
        recipients.Add(NetworkManager.ServerClientId);
        return recipients;
    }

    // The host's time of day, every few seconds while at home: a visit shares the hour.
    private void ShareClock()
    {
        if (network.ConnectedClientsIds.Count < 2 || Time.unscaledTime < nextClock)
        {
            return;
        }

        nextClock = Time.unscaledTime + ClockInterval;
        DayNightCycle clock = HavenVisit.HostIsHome() ? FindFirstObjectByType<DayNightCycle>() : null;
        if (clock == null)
        {
            return;
        }

        using var writer = new FastBufferWriter(16, Allocator.Temp);
        writer.WriteValueSafe(clock.Hour);
        network.CustomMessagingManager.SendNamedMessageToAll(ClockMessage, writer, NetworkDelivery.Reliable);
    }

    private void OnClock(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out float hour);
        DayNightCycle clock = HavenVisit.IsVisiting ? FindFirstObjectByType<DayNightCycle>() : null;
        if (clock != null)
        {
            clock.Hour = hour;
        }
    }

    private static IContentLookup Content() => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    private void OnClientDisconnected(ulong clientId)
    {
        if (network != null && network.IsServer && clientId != network.LocalClientId)
        {
            access.Forget(clientId);
            return;
        }

        // Only our own connection ending ends our session (the host sees friends leave through their avatars).
        if (network == null || network.IsServer || (clientId != network.LocalClientId && state != CoopState.Joining))
        {
            return;
        }

        string reason = network.DisconnectReason;
        LastProblem = !string.IsNullOrEmpty(reason) ? reason : state == CoopState.Joining ? "Couldn't reach the host." : "The host closed the session.";
        GameMessages.Post(LastProblem, MessageKind.Warning);
        network.Shutdown();
        lobby?.Close();
        SetState(CoopState.Offline);
        HavenVisit.End(Content());
    }

    private void OnTransportFailure()
    {
        LastProblem = "The connection failed.";
        GameMessages.Post(LastProblem, MessageKind.Warning);
        lobby?.Close();
        SetState(CoopState.Offline);
        HavenVisit.End(Content());
    }

    private void OnFriendArrived(NetworkPlayerAvatar friend) => GameMessages.Post($"{friend.DisplayName} is here.");

    private void OnFriendLeft(NetworkPlayerAvatar friend) => GameMessages.Post($"{friend.DisplayName} left.");

    private void SetState(CoopState next)
    {
        if (state == next)
        {
            return;
        }

        state = next;
        if (state == CoopState.Offline || state == CoopState.Hosting || state == CoopState.Joining)
        {
            sync?.Reset();
        }

        if (state == CoopState.Offline)
        {
            access.Reset();
        }

        // Online, the Escape menu still opens but the world keeps running.
        GamePause.FreezesTime = state == CoopState.Offline;
        StateChanged?.Invoke(state);
    }

    private void OnDestroy()
    {
        NetworkPlayerAvatar.FriendArrived -= OnFriendArrived;
        NetworkPlayerAvatar.FriendLeft -= OnFriendLeft;
        sync?.Dispose();
        DropLobby();
        if (Current == this)
        {
            Current = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

#if UNITY_EDITOR
    // Multiplayer Play Mode: additional Editor instances never touch the real saves, and start by their tag.
    private void PrepareTestInstance()
    {
        if (!Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor)
        {
            SaveGameService.Storage = new SaveStorage(Path.Combine(Application.temporaryCachePath, "Coop Test Instance"));
            Application.runInBackground = true;
        }

        foreach (string tag in Unity.Multiplayer.PlayMode.CurrentPlayer.ReadOnlyTags())
        {
            if (tag == HostTag || tag == GuestTag)
            {
                autoStart = tag;
            }
        }
    }
#endif
}
