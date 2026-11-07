#if SAFEHAVEN_STEAM && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define STEAM_ON
#endif
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if STEAM_ON
using Steamworks;
#endif

/// <summary>
/// Steam, when it is there: starts the Steam API for <see cref="MultiplayerSettings.SteamAppId"/>, keeps its callbacks
/// running every frame and shuts it down with the game. Steam is optional: without the Steam client (or on a platform
/// without Steamworks) <see cref="IsReady"/> stays false and co-op falls back to direct (LAN or IP) sessions.
/// <para>
/// App 480 (Valve's test app) is started through the SteamAppId environment variable, so neither the Editor nor a
/// development build needs a steam_appid.txt. With the game's own app id, a build not started by Steam restarts
/// through Steam (RestartAppIfNecessary), as Steam requires.
/// </para>
/// </summary>
public sealed class SteamPlatform : MonoBehaviour, IGameSystem
{
    public const uint TestAppId = 480;

    public static SteamPlatform Current { get; private set; }

    /// <summary>True once the Steam API is running: lobbies, invites and Steam's relay can be used.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Why Steam isn't available (for logs and the menu), or empty.</summary>
    public string Problem { get; private set; } = string.Empty;

    /// <summary>This player's Steam id (0 without Steam).</summary>
    public ulong LocalId { get; private set; }

    /// <summary>This player's Steam name (empty without Steam).</summary>
    public string PersonaName { get; private set; } = string.Empty;

    /// <summary>Raised just before the Steam API shuts down: last chance to leave lobbies and drop callbacks.</summary>
    public event Action ShuttingDown;

    public void Initialise(MultiplayerSettings settings)
    {
        Current = this;
#if STEAM_ON
        uint appId = settings.SteamAppId;
        try
        {
            if (appId == TestAppId || Application.isEditor)
            {
                Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
                Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
            }
            else if (SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
            {
                Application.Quit();
                return;
            }

            ESteamAPIInitResult result = SteamAPI.InitEx(out string message);
            IsReady = result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            if (!IsReady)
            {
                Problem = string.IsNullOrEmpty(message) ? $"Steam isn't available ({result})." : message;
                Debug.Log($"Steam: not started ({Problem}). Co-op uses direct sessions.");
                return;
            }

            LocalId = SteamUser.GetSteamID().m_SteamID;
            PersonaName = SteamFriends.GetPersonaName();
            Debug.Log($"Steam: started for app {appId} as {PersonaName}.");
        }
        catch (Exception exception)
        {
            // A missing steam_api64.dll or similar: play on without Steam.
            IsReady = false;
            Problem = exception.Message;
            Debug.LogWarning($"Steam: couldn't start ({exception.Message}). Co-op uses direct sessions.");
        }
#else
        Problem = "This build has no Steam.";
#endif
    }

    public void OnSceneEntered(Scene scene)
    {
    }

#if STEAM_ON
    private void Update()
    {
        if (IsReady)
        {
            SteamAPI.RunCallbacks();
        }
    }
#endif

    private void OnDestroy()
    {
#if STEAM_ON
        if (IsReady)
        {
            try
            {
                ShuttingDown?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            IsReady = false;
            SteamAPI.Shutdown();
        }
#endif
        ShuttingDown = null;
        if (Current == this)
        {
            Current = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;
}
