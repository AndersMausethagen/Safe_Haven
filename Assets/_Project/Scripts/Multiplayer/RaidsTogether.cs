using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Raids together, the part of co-op between the portal and the way home:
/// <list type="bullet">
/// <item>The host going through the portal sends visiting friends the run (<c>SafeHaven.RaidOpen</c>, a
/// <see cref="RaidInvite"/>); a friend who joins later hears of it after the Haven (<see cref="TellOfRaidUnderWay"/>).
/// The portal offers to follow while a minute or more is left (<see cref="RaidToFollow"/>, <see cref="FollowIntoRaid"/>);
/// the run's end closes it (<c>RaidClosed</c>).</item>
/// <item>Each player's raid ending is news for the others (<c>RaidNews</c>, passed on by the host): who got out, who fell.
/// Getting out beside a friend, or within the minute after them, pays experience.</item>
/// </list>
/// </summary>
public sealed class RaidsTogether : ICoopPart, IDisposable
{
    private const string OpenMessage = "SafeHaven.RaidOpen";
    private const string ClosedMessage = "SafeHaven.RaidClosed";
    private const string NewsMessage = "SafeHaven.RaidNews";

    /// <summary>Seconds a host's raid must still have for friends to follow.</summary>
    private const float FollowUntilSecondsLeft = 60f;
    private const float TogetherSeconds = 60f;
    private const float TogetherReach = 25f;
    private const float NewsLasts = 300f;

    private readonly MultiplayerSettings settings;
    private readonly Action followed;
    private readonly Dictionary<string, (RaidOutcome outcome, string raid, float at)> news = new Dictionary<string, (RaidOutcome, string, float)>();
    private CoopLink link;
    private RaidInvite openRaid;
    private float openRaidSince;
    private RaidInvite hostRaid;

    /// <param name="onFollowed">Called once this player has set off after the host (the creatures then come from the host).</param>
    public RaidsTogether(MultiplayerSettings multiplayer, Action onFollowed)
    {
        settings = multiplayer;
        followed = onFollowed;
        RaidLauncher.Launched += OnRaidLaunched;
        GameEvents.Raised += OnGameEvent;
    }

    public void Dispose()
    {
        RaidLauncher.Launched -= OnRaidLaunched;
        GameEvents.Raised -= OnGameEvent;
    }

    public void Begin(CoopLink session)
    {
        link = session;
        openRaid = null;
        link.On(NewsMessage, OnNews);
        if (!link.IsHost)
        {
            link.On(OpenMessage, OnRaidOpen);
            link.On(ClosedMessage, (sender, _) => openRaid = null);
        }
    }

    public void Tick()
    {
    }

    public void FriendLeft(ulong clientId)
    {
    }

    public void End()
    {
        openRaid = null;
        news.Clear();
        link = null;
    }

    // ---- Following the host

    /// <summary>The raid a visiting guest can follow the host into now, among a portal's destinations (null when none), with the seconds left.</summary>
    public RaidDefinition RaidToFollow(IEnumerable<RaidDefinition> destinations, out float secondsLeft)
    {
        secondsLeft = 0f;
        if (openRaid == null || link == null || link.IsHost || !HavenVisit.IsVisiting || GameSession.ActiveRaid != null)
        {
            return null;
        }

        secondsLeft = openRaid.totalSeconds - (Time.unscaledTime - openRaidSince);
        return secondsLeft >= FollowUntilSecondsLeft ? openRaid.FindRaid(destinations) : null;
    }

    /// <summary>Follows the host into their raid (see <see cref="RaidLauncher.TryFollow"/>).</summary>
    public void FollowIntoRaid(RaidDefinition raid, GameObject player)
    {
        RaidInvite invite = openRaid;
        float elapsed = invite != null ? Time.unscaledTime - openRaidSince : 0f;
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (RaidLauncher.TryFollow(invite, raid, player, content, elapsed, out string problem))
        {
            followed?.Invoke();
        }
        else
        {
            GameMessages.Post(problem, MessageKind.Warning);
        }
    }

    private void OnRaidLaunched(RaidInvite invite)
    {
        // Kept while the run lasts, for friends who join while the host is out.
        hostRaid = invite;
        if (link == null || !link.IsHost)
        {
            return;
        }

        using var writer = Packed(invite);
        link.ToGuests(OpenMessage, writer);
    }

    /// <summary>A friend who joined while the host is on a raid hears of it after the Haven, and can still follow.</summary>
    public void TellOfRaidUnderWay(ulong clientId)
    {
        RaidSession run = GameSession.ActiveRaid;
        if (link == null || hostRaid == null || run == null || run.Seed != hostRaid.seed || run.IsOver)
        {
            return;
        }

        hostRaid.elapsed = run.Elapsed;
        using var writer = Packed(hostRaid);
        link.Send(OpenMessage, clientId, writer);
    }

    private static FastBufferWriter Packed(RaidInvite invite)
    {
        byte[] packet = WorldTransfer.Pack(invite);
        var writer = CoopLink.Writer(packet.Length + 16);
        writer.WriteValueSafe(packet);
        return writer;
    }

    private void OnRaidOpen(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte[] packet);
        if (!HavenVisit.IsVisiting)
        {
            return;
        }

        openRaid = WorldTransfer.Unpack<RaidInvite>(packet);
        openRaidSince = Time.unscaledTime - Mathf.Max(0f, openRaid.elapsed);
        GameMessages.Post($"{HavenVisit.HostName} went through the portal. Use the portal to follow them.");
    }

    // ---- How raids ended

    // This player's raid is over: the host's closes the way for friends; everyone hears how it ended.
    private void OnGameEvent(GameEvent gameEvent)
    {
        if (gameEvent.Kind != GameEventKind.RaidFinished)
        {
            return;
        }

        hostRaid = null;
        if (link == null)
        {
            return;
        }

        TellNews(gameEvent);
        if (link.IsHost)
        {
            using var writer = CoopLink.Writer(4);
            link.ToGuests(ClosedMessage, writer);
        }
    }

    /// <summary>How a friend's last raid ended, if word came in the last few minutes (the party panel).</summary>
    public bool TryNews(string friend, out RaidOutcome outcome)
    {
        outcome = RaidOutcome.InProgress;
        if (!news.TryGetValue(friend, out var heard) || Time.unscaledTime - heard.at > NewsLasts)
        {
            return false;
        }

        outcome = heard.outcome;
        return true;
    }

    private void TellNews(GameEvent gameEvent)
    {
        string raid = gameEvent.Subject is RaidDefinition definition ? definition.DisplayName : "the raid";
        string me = GameSession.Appearance != null ? GameSession.Appearance.DisplayName : CharacterAppearance.DefaultName;
        if (gameEvent.Outcome == RaidOutcome.Extracted)
        {
            RewardTogether(raid);
        }

        using var writer = News(me, (byte)gameEvent.Outcome, raid);
        link.ToOthers(NewsMessage, writer);
    }

    private static FastBufferWriter News(string who, byte outcome, string raid)
    {
        var writer = CoopLink.Writer(256);
        writer.WriteValueSafe(who);
        writer.WriteValueSafe(outcome);
        writer.WriteValueSafe(raid);
        return writer;
    }

    private void OnNews(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string who);
        reader.ReadValueSafe(out byte outcome);
        reader.ReadValueSafe(out string raid);
        // The host passes a guest's news on to the others.
        if (link.IsHost)
        {
            using var writer = News(who, outcome, raid);
            link.ToGuests(NewsMessage, writer, sender);
        }

        news[who] = ((RaidOutcome)outcome, raid, Time.unscaledTime);
        switch ((RaidOutcome)outcome)
        {
            case RaidOutcome.Extracted:
                GameMessages.Post($"{who} got out of {raid} with their haul.", MessageKind.Gain);
                break;
            case RaidOutcome.Died:
                GameMessages.Post($"{who} fell in {raid}.", MessageKind.Warning);
                break;
            case RaidOutcome.TimeExpired:
                GameMessages.Post($"{who} was caught as {raid} closed.", MessageKind.Warning);
                break;
        }
    }

    // Getting out together pays: friends beside this player as they extract, or who got out of the same raid within the minute.
    private void RewardTogether(string raid)
    {
        var together = new HashSet<string>();
        FixedString128Bytes place = CoopPlace.Key;
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && !friend.IsOwner && friend.IsVisible && friend.Body != null && friend.Look.Health > 0 && friend.Look.Place.Equals(place)
                && Player.Current != null && Vector3.Distance(friend.Body.transform.position, Player.Current.transform.position) <= TogetherReach)
            {
                together.Add(friend.Look.DisplayName);
            }
        }

        foreach (KeyValuePair<string, (RaidOutcome outcome, string raid, float at)> heard in news)
        {
            if (heard.Value.outcome == RaidOutcome.Extracted && heard.Value.raid == raid && Time.unscaledTime - heard.Value.at <= TogetherSeconds)
            {
                together.Add(heard.Key);
            }
        }

        int friends = Mathf.Min(3, together.Count);
        if (friends > 0 && settings != null && settings.TogetherExperience > 0)
        {
            ExperienceTracker.Grant(settings.TogetherExperience * friends);
            GameMessages.Post($"Out together with {string.Join(", ", together)}: +{settings.TogetherExperience * friends} experience.", MessageKind.Gain);
        }
    }
}
