using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// The Haven's lands in co-op (Docs/Territory.md). A guest's copy of the host's lands only mirrors them (they come with the
/// settlement, <see cref="HavenShare"/>), so what a guest does for them is sent here to the host's game, which applies it
/// (<see cref="Territory.FromFriend"/>): a Pact Rune offered at a lit stone, a held region's task work (hunting, gathering,
/// fishing there), a place found in a held region, a rot's heart burned on the host's run (once a run, however many burn it).
/// The result comes back to everyone with the settlement. Lighting stones and standing watch are the host's; friends fight
/// beside them, and the watch's creatures are the host's like any other (<see cref="CreatureHost"/>).
/// It carries the guest's help for the host's Haven from a shared raid too (<see cref="HavenHelp"/>): a breach source struck,
/// one of the host's lost led home.
/// Plain C#: <see cref="CoopSession"/> owns the connection.
/// </summary>
public sealed class CoopLands : ICoopPart
{
    private const string WorkMessage = "SafeHaven.LandWork";
    private const string HelpMessage = "SafeHaven.HavenHelp";

    private CoopLink link;

    public void Begin(CoopLink session)
    {
        link = session;
        if (link.IsHost)
        {
            link.On(WorkMessage, OnWork);
            link.On(HelpMessage, OnHelp);
        }
        else
        {
            Territory.ForHost = Send;
            HavenHelp.ForHost = SendHelp;
        }
    }

    public void FriendLeft(ulong clientId)
    {
    }

    public void End()
    {
        if (Territory.ForHost == (System.Action<LandWork, string, int>)Send)
        {
            Territory.ForHost = null;
        }

        if (HavenHelp.ForHost == (System.Action<HavenHelpKind, string, float, float, int>)SendHelp)
        {
            HavenHelp.ForHost = null;
        }

        link = null;
    }

    public void Tick()
    {
    }

    private void Send(LandWork work, string region, int detail)
    {
        if (link == null || string.IsNullOrEmpty(region))
        {
            return;
        }

        using FastBufferWriter writer = CoopLink.Writer(128);
        Write(writer, work, region, detail);
        link.ToHost(WorkMessage, writer);
    }

    private void OnWork(ulong sender, FastBufferReader reader)
    {
        Read(reader, out LandWork work, out string region, out int detail);
        Territory.FromFriend(work, region, detail, NetworkPlayerAvatar.NameOf(sender));
    }

    private void SendHelp(HavenHelpKind kind, string id, float share, float most, int run)
    {
        if (link == null)
        {
            return;
        }

        using FastBufferWriter writer = CoopLink.Writer(128);
        WriteHelp(writer, kind, id, share, most, run);
        link.ToHost(HelpMessage, writer);
    }

    private void OnHelp(ulong sender, FastBufferReader reader)
    {
        ReadHelp(reader, out HavenHelpKind kind, out string id, out float share, out float most, out int run);
        HavenHelp.FromFriend(kind, id, share, most, run, NetworkPlayerAvatar.NameOf(sender), UnityEngine.Resources.Load<ContentDatabase>(ContentDatabase.ResourceName));
    }

    /// <summary>The help message: what, who (a resident's id), the share and most a breach loses, and the run.</summary>
    public static void WriteHelp(FastBufferWriter writer, HavenHelpKind kind, string id, float share, float most, int run)
    {
        writer.WriteValueSafe((byte)kind);
        writer.WriteValueSafe(new FixedString64Bytes(id ?? string.Empty));
        writer.WriteValueSafe(share);
        writer.WriteValueSafe(most);
        writer.WriteValueSafe(run);
    }

    public static void ReadHelp(FastBufferReader reader, out HavenHelpKind kind, out string id, out float share, out float most, out int run)
    {
        reader.ReadValueSafe(out byte what);
        reader.ReadValueSafe(out FixedString64Bytes who);
        reader.ReadValueSafe(out share);
        reader.ReadValueSafe(out most);
        reader.ReadValueSafe(out run);
        kind = (HavenHelpKind)what;
        id = who.ToString();
    }

    /// <summary>The message: what was done, where, and the task's kind or the run.</summary>
    public static void Write(FastBufferWriter writer, LandWork work, string region, int detail)
    {
        writer.WriteValueSafe((byte)work);
        writer.WriteValueSafe(new FixedString64Bytes(region));
        writer.WriteValueSafe(detail);
    }

    public static void Read(FastBufferReader reader, out LandWork work, out string region, out int detail)
    {
        reader.ReadValueSafe(out byte kind);
        reader.ReadValueSafe(out FixedString64Bytes name);
        reader.ReadValueSafe(out detail);
        work = (LandWork)kind;
        region = name.ToString();
    }
}
