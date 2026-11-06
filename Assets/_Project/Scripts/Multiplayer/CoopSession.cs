using System;
using System.IO;
using System.Text;
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
/// NetworkManager (made here, in code, on the Game Systems object's side) and its transport: Unity Transport for LAN and
/// direct sessions today, Steam's relay once Steam is added (sessions are addressed by Steam id then, not IP).
/// <para>
/// While a session runs, the Escape menu no longer stops the world (<see cref="GamePause.FreezesTime"/>): one player
/// can't pause their friends. Each player keeps their own <see cref="Player"/> and save; friends appear beside them as
/// <see cref="NetworkPlayerAvatar"/>s. Netcode's own scene management is off, so the game's scene changes are untouched.
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

    private MultiplayerSettings settings;
    private NetworkManager network;
    private UnityTransport transport;
    private CoopState state;
    private string autoStart;

    public static CoopSession Current { get; private set; }

    public CoopState State => state;
    public bool IsActive => state != CoopState.Offline;
    public bool IsHost => state == CoopState.Hosting;
    /// <summary>Players in the session now, you included (1 while offline).</summary>
    public int PlayerCount => IsActive ? Mathf.Max(1, NetworkPlayerAvatar.All.Count) : 1;
    /// <summary>Why the last attempt or session ended, for the menu (empty when it ended normally).</summary>
    public string LastProblem { get; private set; } = string.Empty;

    public event Action<CoopState> StateChanged;

    public void Initialise(MultiplayerSettings multiplayer)
    {
        settings = multiplayer;
        Current = this;
        NetworkPlayerAvatar.FriendArrived += OnFriendArrived;
        NetworkPlayerAvatar.FriendLeft += OnFriendLeft;
#if UNITY_EDITOR
        PrepareTestInstance();
#endif
    }

    public void OnSceneEntered(Scene scene)
    {
        // A tagged test instance starts once it has a player to bring.
        if (!string.IsNullOrEmpty(autoStart) && Player.Current != null && !IsActive)
        {
            string role = autoStart;
            autoStart = null;
            bool started = role == HostTag ? Host(out string problem) : Join(LocalAddress, out problem);
            Debug.Log(started ? $"Co-op test instance: {role} started." : $"Co-op test instance: {role} failed: {problem}");
        }
    }

    /// <summary>Opens this game to friends. False (with the reason) when it can't.</summary>
    public bool Host(out string problem)
    {
        if (!Prepare(out problem))
        {
            return false;
        }

        transport.SetConnectionData(LocalAddress, settings.Port, "0.0.0.0");
        network.ConnectionApprovalCallback = Approve;
        if (!network.StartHost())
        {
            problem = $"Couldn't open a session on port {settings.Port}.";
            return false;
        }

        SetState(CoopState.Hosting);
        GameMessages.Post("Your Haven is open to friends.");
        return true;
    }

    /// <summary>Joins the session at <paramref name="address"/> (an IP, optionally with ":port").</summary>
    public bool Join(string address, out string problem)
    {
        if (!Prepare(out problem))
        {
            return false;
        }

        if (!TryParseAddress(address, settings.Port, out string ip, out ushort port))
        {
            problem = $"'{address}' isn't an address to join.";
            return false;
        }

        transport.SetConnectionData(ip, port);
        network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(settings.ProtocolVersion);
        if (!network.StartClient())
        {
            problem = $"Couldn't reach {ip}:{port}.";
            return false;
        }

        SetState(CoopState.Joining);
        return true;
    }

    /// <summary>Ends the session (hosting: for everyone).</summary>
    public void Leave()
    {
        if (network != null && (network.IsListening || network.IsClient || network.IsServer))
        {
            network.Shutdown();
        }

        if (IsActive)
        {
            GameMessages.Post(IsHost ? "Your Haven is closed to friends." : "You left the session.");
        }

        SetState(CoopState.Offline);
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

    // Built inactive so Netcode's Awake finds its configuration in place.
    private void CreateNetwork()
    {
        var holder = new GameObject("Coop Network");
        holder.SetActive(false);
        DontDestroyOnLoad(holder);
        transport = holder.AddComponent<UnityTransport>();
        network = holder.AddComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
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
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Only our own connection ending ends our session (the host sees friends leave through their avatars).
        if (network == null || network.IsServer || (clientId != network.LocalClientId && state != CoopState.Joining))
        {
            return;
        }

        string reason = network.DisconnectReason;
        LastProblem = !string.IsNullOrEmpty(reason) ? reason : state == CoopState.Joining ? "Couldn't reach the host." : "The host closed the session.";
        GameMessages.Post(LastProblem, MessageKind.Warning);
        network.Shutdown();
        SetState(CoopState.Offline);
    }

    private void OnTransportFailure()
    {
        LastProblem = "The connection failed.";
        GameMessages.Post(LastProblem, MessageKind.Warning);
        SetState(CoopState.Offline);
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
        // Online, the Escape menu still opens but the world keeps running.
        GamePause.FreezesTime = state == CoopState.Offline;
        StateChanged?.Invoke(state);
    }

    private void OnDestroy()
    {
        NetworkPlayerAvatar.FriendArrived -= OnFriendArrived;
        NetworkPlayerAvatar.FriendLeft -= OnFriendLeft;
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
