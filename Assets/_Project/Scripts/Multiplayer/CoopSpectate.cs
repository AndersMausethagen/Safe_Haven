using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Watching friends after falling on a raid together. When this player dies while a friend is still standing on the same
/// run, the raid's ending waits (<see cref="RaidDirector"/> hands over its finish) and the camera follows a friend
/// (<see cref="PlayerCameraController.Watch"/>): the Dodge key moves to the next, Interact heads home. It ends by itself
/// when no friend is left standing there, or the session ends.
/// </summary>
public static class CoopSpectate
{
    private const float Grace = 0.75f;

    private static Action finish;
    private static int watching;
    private static float since;
    private static readonly List<NetworkPlayerAvatar> standing = new List<NetworkPlayerAvatar>();
    // The watching's own hold on the fallen player's controls (PlayerControl), and whose they are.
    private static readonly object Watching = new object();
    private static GameObject held;

    /// <summary>True while this player, fallen, is watching their friends.</summary>
    public static bool IsWatching => finish != null;

    /// <summary>The friend being watched, or null.</summary>
    public static NetworkPlayerAvatar Watched { get; private set; }

    /// <summary>From the raid when this player dies: true when it now waits for the watching to end (it calls <paramref name="finishRaid"/> then).</summary>
    public static bool TryBegin(Action finishRaid)
    {
        if (finishRaid == null || CoopSession.Current == null || !CoopSession.Current.IsActive || GameSession.ActiveRaid == null || Standing() == 0)
        {
            return false;
        }

        finish = finishRaid;
        watching = 0;
        since = Time.unscaledTime;
        // Nothing else acts for the fallen player; only the two keys watching needs stay on (while nothing else holds them).
        held = Player.Current != null ? Player.Current.gameObject : null;
        PlayerControl.Hold(held, Watching, PlayerControl.Kept.Interact | PlayerControl.Kept.Dodge);
        Point();
        GameMessages.Post("You fell. Watching your friends: Dodge for the next, Interact to head home.", MessageKind.Warning);
        return true;
    }

    /// <summary>Every frame, whatever the session's state.</summary>
    public static void Update()
    {
        if (finish == null)
        {
            return;
        }

        bool sessionOn = CoopSession.Current != null && CoopSession.Current.IsActive;
        bool ready = Time.unscaledTime - since >= Grace;
        if (!sessionOn || Standing() == 0 || (ready && GameInput.Interact != null && GameInput.Interact.WasPressedThisFrame()))
        {
            End();
            return;
        }

        if (ready && GameInput.Dodge != null && GameInput.Dodge.WasPressedThisFrame())
        {
            watching++;
        }

        Point();
    }

    private static void Point()
    {
        if (standing.Count == 0)
        {
            return;
        }

        watching = (watching % standing.Count + standing.Count) % standing.Count;
        NetworkPlayerAvatar friend = standing[watching];
        if (friend != Watched && PlayerCameraController.Current != null)
        {
            Watched = friend;
            PlayerCameraController.Current.Watch(friend.Body.transform);
            GameMessages.Post($"Watching {friend.Look.DisplayName}.");
        }
    }

    /// <summary>Stops watching and lets the raid end for this player now (quitting while watching). False when not watching.</summary>
    public static bool FinishNow()
    {
        if (!IsWatching)
        {
            return false;
        }

        End();
        return true;
    }

    private static void End()
    {
        Action finishing = finish;
        finish = null;
        Watched = null;
        if (PlayerCameraController.Current != null)
        {
            PlayerCameraController.Current.Watch(null);
        }

        // The raid's end takes the controls first (RaidDirector.StopPlayer), then the watching lets go: given back between the two,
        // the whole map came on for a moment.
        finishing?.Invoke();
        PlayerControl.Release(held, Watching);
        held = null;
    }

    // Friends on this run who are up and about.
    private static int Standing()
    {
        standing.Clear();
        Unity.Collections.FixedString128Bytes place = CoopPlace.Key;
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && !friend.IsOwner && friend.IsVisible && friend.Body != null && !friend.Look.Downed && friend.Look.Health > 0
                && friend.Look.Place.Equals(place))
            {
                standing.Add(friend);
            }
        }

        return standing.Count;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        finish = null;
        Watched = null;
        held = null;
        standing.Clear();
        since = 0f;
        watching = 0;
    }
}
