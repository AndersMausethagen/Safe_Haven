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
    /// <summary>A friend in an open session of this game: their name and the lobby to walk in through.</summary>
    public readonly struct FriendHaven
    {
        /// <summary>Whose Haven it is (the lobby's host) when known, else a friend who is in it.</summary>
        public readonly string Name;
        public readonly ulong Lobby;
        /// <summary>True when <see cref="Name"/> is the host's: a friend may be a guest in a third player's Haven.</summary>
        public readonly bool IsHost;
        /// <summary>The friends in it, by name.</summary>
        public readonly string Friends;

        public FriendHaven(string name, ulong lobby, bool isHost = true, string friends = null)
        {
            Name = name;
            Lobby = lobby;
            IsHost = isHost;
            Friends = friends ?? name;
        }
    }

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

    /// <summary>Friends (from the Steam friends list) playing this game in an open session that can be joined.</summary>
    public System.Collections.Generic.List<FriendHaven> FriendsHosting()
    {
        // One entry a lobby, named for its host (the lobby's own data): listed by the friend found in it, "Bob's Haven" walked
        // into Alice's when Bob was her guest.
        var byLobby = new System.Collections.Generic.Dictionary<ulong, System.Collections.Generic.List<string>>();
        var order = new System.Collections.Generic.List<ulong>();
        AppId_t game = SteamUtils.GetAppID();
        int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        for (int i = 0; i < count; i++)
        {
            CSteamID friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            if (SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t playing) && playing.m_gameID.AppID() == game &&
                playing.m_steamIDLobby.IsValid() && playing.m_steamIDLobby.m_SteamID != 0)
            {
                ulong lobby = playing.m_steamIDLobby.m_SteamID;
                if (!byLobby.TryGetValue(lobby, out System.Collections.Generic.List<string> names))
                {
                    byLobby[lobby] = names = new System.Collections.Generic.List<string>();
                    order.Add(lobby);
                }

                names.Add(SteamFriends.GetFriendPersonaName(friend));
            }
        }

        var found = new System.Collections.Generic.List<FriendHaven>();
        foreach (ulong lobby in order)
        {
            string friends = string.Join(", ", byLobby[lobby]);
            var id = new CSteamID(lobby);
            string host = SteamMatchmaking.GetLobbyData(id, HostKey);
            if (ulong.TryParse(host, out ulong hostId) && hostId != 0)
            {
                found.Add(new FriendHaven(SteamFriends.GetFriendPersonaName(new CSteamID(hostId)), lobby, true, friends));
            }
            else
            {
                // Not heard yet: asked for, and named by the friend in it until it comes (the page is rebuilt while it shows).
                SteamMatchmaking.RequestLobbyData(id);
                found.Add(new FriendHaven(byLobby[lobby][0], lobby, false, friends));
            }
        }

        return found;
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
    public System.Collections.Generic.List<FriendHaven> FriendsHosting() => new System.Collections.Generic.List<FriendHaven>();
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
