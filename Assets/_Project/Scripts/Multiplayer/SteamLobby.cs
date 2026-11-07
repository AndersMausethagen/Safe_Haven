#if SAFEHAVEN_STEAM && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define STEAM_ON
#endif
using System;
#if STEAM_ON
using Steamworks;
#endif

/// <summary>
/// The Steam side of a co-op session: a friends-only lobby the host opens, holding who to connect to (their Steam id)
/// and the game's protocol version. Friends reach it three ways, all ending in <see cref="HostFound"/>: an invite from
/// the Steam overlay, "Join Game" on the host in their friends list (rich presence "connect"), or Steam starting the
/// game with "+connect_lobby &lt;id&gt;". The connection itself then goes through Steam's relay
/// (SteamNetworkingSocketsTransport), so nobody's IP address is shown. Plain C#, owned by <see cref="CoopSession"/>.
/// </summary>
public sealed class SteamLobby : IDisposable
{
    public const string ConnectCommand = "+connect_lobby";
    private const string HostKey = "host";
    private const string VersionKey = "version";

    /// <summary>A friend's lobby was entered: connect to this host (Steam id), who plays this protocol version.</summary>
    public event Action<ulong, string> HostFound;
    /// <summary>A friend asked to join through Steam: join their lobby when ready (lobby id).</summary>
    public event Action<ulong> JoinAsked;
    /// <summary>Something went wrong, for the player.</summary>
    public event Action<string> Failed;

    /// <summary>The open lobby (0 when none).</summary>
    public ulong LobbyId { get; private set; }
    public bool IsHosting { get; private set; }

#if STEAM_ON
    private readonly Callback<GameLobbyJoinRequested_t> lobbyJoinRequested;
    private readonly Callback<GameRichPresenceJoinRequested_t> presenceJoinRequested;
    private readonly Callback<LobbyEnter_t> lobbyEntered;
    private readonly CallResult<LobbyCreated_t> lobbyCreated;
    private string version = string.Empty;

    public SteamLobby()
    {
        lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
        presenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnPresenceJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
    }

    /// <summary>Opens a friends-only lobby for <paramref name="maxPlayers"/> (the host is already in a session).</summary>
    public void Open(int maxPlayers, string protocolVersion)
    {
        Close();
        IsHosting = true;
        version = protocolVersion;
        lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers));
    }

    /// <summary>Opens Steam's invite dialog for the open lobby. False when there is none yet.</summary>
    public bool Invite()
    {
        if (LobbyId == 0)
        {
            return false;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(LobbyId));
        return true;
    }

    /// <summary>Enters a friend's lobby; <see cref="HostFound"/> follows.</summary>
    public void Enter(ulong lobbyId)
    {
        Close();
        IsHosting = false;
        SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
    }

    public void Close()
    {
        if (LobbyId != 0)
        {
            SteamMatchmaking.LeaveLobby(new CSteamID(LobbyId));
            LobbyId = 0;
        }

        SteamFriends.ClearRichPresence();
        IsHosting = false;
    }

    public void Dispose()
    {
        Close();
        lobbyJoinRequested.Dispose();
        presenceJoinRequested.Dispose();
        lobbyEntered.Dispose();
        lobbyCreated.Dispose();
    }

    private void OnLobbyCreated(LobbyCreated_t created, bool ioFailure)
    {
        if (ioFailure || created.m_eResult != EResult.k_EResultOK)
        {
            IsHosting = false;
            Failed?.Invoke($"Steam couldn't open a lobby ({created.m_eResult}). Friends can't join through Steam.");
            return;
        }

        LobbyId = created.m_ulSteamIDLobby;
        var lobby = new CSteamID(LobbyId);
        SteamMatchmaking.SetLobbyData(lobby, HostKey, SteamUser.GetSteamID().m_SteamID.ToString());
        SteamMatchmaking.SetLobbyData(lobby, VersionKey, version);
        // "Join Game" in a friend's Steam list starts (or tells) their game with this.
        SteamFriends.SetRichPresence("connect", $"{ConnectCommand} {LobbyId}");
    }

    private void OnLobbyJoinRequested(GameLobbyJoinRequested_t request) => JoinAsked?.Invoke(request.m_steamIDLobby.m_SteamID);

    private void OnPresenceJoinRequested(GameRichPresenceJoinRequested_t request)
    {
        if (TryParseConnect(request.m_rgchConnect, out ulong lobby))
        {
            JoinAsked?.Invoke(lobby);
        }
    }

    private void OnLobbyEntered(LobbyEnter_t entered)
    {
        // The host enters their own lobby when it is made: nothing to join.
        if (IsHosting)
        {
            return;
        }

        if (entered.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Failed?.Invoke("That session is gone or full.");
            return;
        }

        LobbyId = entered.m_ulSteamIDLobby;
        var lobby = new CSteamID(LobbyId);
        string host = SteamMatchmaking.GetLobbyData(lobby, HostKey);
        if (!ulong.TryParse(host, out ulong hostId) || hostId == 0)
        {
            Failed?.Invoke("That session has no host.");
            Close();
            return;
        }

        HostFound?.Invoke(hostId, SteamMatchmaking.GetLobbyData(lobby, VersionKey));
    }
#else
    public void Open(int maxPlayers, string protocolVersion) => Failed?.Invoke("This build has no Steam.");
    public bool Invite() => false;
    public void Enter(ulong lobbyId) => Failed?.Invoke("This build has no Steam.");
    public void Close() { }
    public void Dispose() { }
#endif

    /// <summary>Finds the lobby id in a Steam connect string or command line ("+connect_lobby 1234").</summary>
    public static bool TryParseConnect(string text, out ulong lobbyId)
    {
        lobbyId = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string[] parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == ConnectCommand)
            {
                return ulong.TryParse(parts[i + 1], out lobbyId) && lobbyId != 0;
            }
        }

        return false;
    }
}
