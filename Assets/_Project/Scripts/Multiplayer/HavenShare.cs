using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Visiting a friend's Haven, the part of co-op that carries it (<see cref="HavenVisit"/> holds the rules):
/// <list type="bullet">
/// <item>A guest, once connected, asks for the Haven (<c>SafeHaven.WorldPlease</c>); the host sends its world, packed and
/// in pieces (<c>SafeHaven.World</c>, <see cref="WorldTransfer"/>), and the guest's visit begins.</item>
/// <item>Twice a second both sides send what changed in the Haven (<c>SafeHaven.Entities</c>, <see cref="HavenSync"/>);
/// the host's game has the last word. Every couple of seconds the host sends its settlement when it changed
/// (<c>SafeHaven.Settlement</c>), every five its hour and weather (<c>SafeHaven.Clock</c>).</item>
/// <item>A visit ends when the session does: straight home, or after a raid under way (<see cref="EndVisit"/>).</item>
/// </list>
/// </summary>
public sealed class HavenShare : ICoopPart, IDisposable
{
    private const string WorldMessage = "SafeHaven.World";
    private const string WorldRequest = "SafeHaven.WorldPlease";
    private const string ClockMessage = "SafeHaven.Clock";
    private const string EntitiesMessage = "SafeHaven.Entities";
    private const string SettlementMessage = "SafeHaven.Settlement";
    private const int SettlementEvery = 4;
    private const float ClockInterval = 5f;

    private readonly HavenSync sync = new HavenSync();
    private readonly WorldTransfer.Receiver worldPieces = new WorldTransfer.Receiver();
    private CoopLink link;
    private ContentDatabase content;
    private DayNightCycle clockHere;
    private int transfers;
    private int looks;
    private float nextSync;
    private float nextClock;
    private bool homeAfterRaid;

    /// <summary>The host sent its Haven to a friend (who may now hear of a raid under way).</summary>
    public event Action<ulong> WorldSent;

    /// <summary>A friend's Haven couldn't be visited: the session should end, with this reason.</summary>
    public event Action<string> VisitFailed;

    public void Begin(CoopLink session)
    {
        link = session;
        content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        sync.Reset();
        if (link.IsHost)
        {
            // Friends ask once fully connected: sent any earlier, the Haven could arrive before they listen.
            link.On(WorldRequest, (sender, _) => SendWorld(sender));
            link.On(EntitiesMessage, OnEntitiesFromGuest);
        }
        else
        {
            link.On(WorldMessage, OnWorldPiece);
            link.On(ClockMessage, OnClock);
            link.On(EntitiesMessage, OnEntitiesFromHost);
            link.On(SettlementMessage, OnSettlement);
        }
    }

    /// <summary>A guest's connection is complete: ask for the Haven.</summary>
    public void AskForHaven()
    {
        using var ask = CoopLink.Writer(4);
        link?.ToHost(WorldRequest, ask);
    }

    public void Tick()
    {
        if (link == null)
        {
            return;
        }

        if (Time.unscaledTime >= nextSync)
        {
            nextSync = Time.unscaledTime + HavenSync.Interval;
            ShareChanges();
        }

        if (link.IsHost && Time.unscaledTime >= nextClock)
        {
            nextClock = Time.unscaledTime + ClockInterval;
            ShareClock();
        }
    }

    public void FriendLeft(ulong clientId)
    {
    }

    public void End()
    {
        sync.Reset();
        link = null;
    }

    public void Dispose() => sync.Dispose();

    // ---- The world, once

    private void SendWorld(ulong clientId)
    {
        if (Player.Current == null || Player.Current.Persistence == null)
        {
            Debug.LogWarning("Co-op: no player to send a world from.");
            return;
        }

        // Packed on the main thread on purpose: it and the changes after it go out in order on one channel, and packing it
        // elsewhere would let those changes overtake it.
        SaveData world = HavenVisit.WorldOf(SaveGameService.Capture(), HavenVisit.HostIsHome());
        byte[] packed = WorldTransfer.Pack(world);
        var pieces = WorldTransfer.Split(packed);
        int transfer = ++transfers;
        string hostName = GameSession.Appearance != null ? GameSession.Appearance.DisplayName : CharacterAppearance.DefaultName;
        for (int index = 0; index < pieces.Count; index++)
        {
            byte[] piece = pieces[index];
            using var writer = CoopLink.Writer(piece.Length + 256);
            writer.WriteValueSafe(transfer);
            writer.WriteValueSafe(index);
            writer.WriteValueSafe(pieces.Count);
            writer.WriteValueSafe(hostName);
            writer.WriteValueSafe(piece);
            link.Send(WorldMessage, clientId, writer);
        }

        Debug.Log($"Co-op: sent the Haven to player {clientId} ({packed.Length / 1024f:0.0} KB in {pieces.Count} pieces).");
        WorldSent?.Invoke(clientId);
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
            VisitFailed?.Invoke("Your friend's Haven didn't arrive whole.");
            return;
        }

        if (!HavenVisit.Begin(world, hostName, content, out string problem))
        {
            VisitFailed?.Invoke(problem);
        }
    }

    // ---- What changes in it (HavenSync)

    // The host shares what changed in its Haven with every guest; a visiting guest shares what changed in their copy.
    private void ShareChanges()
    {
        if (link.IsHost)
        {
            if (link.Guests().Count == 0)
            {
                return;
            }

            foreach (byte[] packet in sync.Collect(true))
            {
                SendPacket(EntitiesMessage, packet, CoopLink.NoOne);
            }

            byte[] settlement = ++looks % SettlementEvery == 0 ? sync.CollectSettlement() : null;
            if (settlement != null)
            {
                SendPacket(SettlementMessage, settlement, CoopLink.NoOne);
            }
        }
        else if (HavenVisit.IsVisiting)
        {
            foreach (byte[] packet in sync.Collect(false))
            {
                using var writer = CoopLink.Writer(packet.Length + 16);
                writer.WriteValueSafe(packet);
                link.ToHost(EntitiesMessage, writer);
            }
        }
    }

    // Host to guests (all but the one it came from).
    private void SendPacket(string message, byte[] packet, ulong except)
    {
        using var writer = CoopLink.Writer(packet.Length + 16);
        writer.WriteValueSafe(packet);
        link.ToGuests(message, writer, except);
    }

    // A guest's change: taken on here (the host's next look sends the result to everyone), and passed to the other guests.
    private void OnEntitiesFromGuest(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte[] packet);
        try
        {
            sync.Apply(packet, true, noteMarks: false);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return;
        }

        SendPacket(EntitiesMessage, packet, sender);
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

    private void OnSettlement(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte[] packet);
        if (!HavenVisit.IsVisiting)
        {
            return;
        }

        try
        {
            HavenSync.ApplySettlement(packet, content);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    // ---- The hour

    // The host's time of day and weather while at home, to the guests: a visit shares the sky.
    private void ShareClock()
    {
        DayNightCycle clock = HavenVisit.HostIsHome() ? ClockHere() : null;
        if (clock == null || link.Guests().Count == 0)
        {
            return;
        }

        WeatherSystem weather = WeatherSystem.Active;
        string weatherName = weather != null && weather.Weather != null ? weather.Weather.name : string.Empty;
        using var writer = CoopLink.Writer(16 + weatherName.Length * 2);
        writer.WriteValueSafe(clock.Hour);
        writer.WriteValueSafe(weatherName);
        link.ToGuests(ClockMessage, writer, CoopLink.NoOne, NetworkDelivery.Reliable);
    }

    private void OnClock(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out float hour);
        reader.ReadValueSafe(out string weatherName);
        // A raid keeps its own hour and weather (from its seed), wherever the host is.
        DayNightCycle clock = HavenVisit.IsVisiting && GameSession.ActiveRaid == null ? ClockHere() : null;
        if (clock == null)
        {
            return;
        }

        clock.Hour = hour;
        // The host's weather too: this game's climate stops choosing its own while here.
        WeatherSystem weather = WeatherSystem.Active;
        if (weather != null && !string.IsNullOrEmpty(weatherName))
        {
            weather.ChangesByItself = false;
            if (weather.Weather == null || weather.Weather.name != weatherName)
            {
                WeatherDefinition hosts = weather.FindWeather(weatherName);
                if (hosts != null)
                {
                    weather.SetWeather(hosts);
                }
            }
        }
    }

    // The scene's clock, found once per scene.
    private DayNightCycle ClockHere()
    {
        if (clockHere == null || clockHere.gameObject.scene != SceneManager.GetActiveScene())
        {
            clockHere = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        }

        return clockHere;
    }

    // ---- The visit's end

    /// <summary>
    /// The session is over: a guest goes home with what their character gained (when <paramref name="travel"/>), or, on a
    /// raid, once back from it, so a friend leaving or the host going doesn't cut a run short.
    /// </summary>
    public void EndVisit(bool travel)
    {
        if (travel && HavenVisit.IsVisiting && GameSession.ActiveRaid != null)
        {
            homeAfterRaid = true;
            GameMessages.Post("You'll head home once this raid is over.");
            return;
        }

        homeAfterRaid = false;
        HavenVisit.End(content != null ? content : Resources.Load<ContentDatabase>(ContentDatabase.ResourceName), travel);
    }

    /// <summary>A scene with a player in it: back from the raid a session ended during, now home. True when it left for home.</summary>
    public bool HeadHomeIfDue()
    {
        if (!homeAfterRaid || GameSession.ActiveRaid != null)
        {
            return false;
        }

        homeAfterRaid = false;
        HavenVisit.End(content != null ? content : Resources.Load<ContentDatabase>(ContentDatabase.ResourceName));
        return true;
    }
}
