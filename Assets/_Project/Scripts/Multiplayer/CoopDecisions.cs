using Unity.Netcode;

/// <summary>
/// A visitor's decisions for the host's Haven (<see cref="HavenDecisions"/>), made in the host's game: a guest asks
/// (<c>SafeHaven.Decision</c>: the decision's name, what it is about, and what they paid), the host's game makes it as its own
/// and answers (<c>SafeHaven.DecisionAnswer</c>: made or not, what to say, and the payment to give back when not). The host
/// hears what their friend did; the settlement mirror brings the guest the result. Plain C#.
/// </summary>
public sealed class CoopDecisions : ICoopPart
{
    private const string AskMessage = "SafeHaven.Decision";
    private const string AnswerMessage = "SafeHaven.DecisionAnswer";

    private CoopLink link;

    /// <summary>The session's, while one runs.</summary>
    public static CoopDecisions Current { get; private set; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public void Begin(CoopLink session)
    {
        link = session;
        Current = this;
        if (link.IsHost)
        {
            link.On(AskMessage, OnAsk);
            // Found now rather than at the first friend's word.
            _ = HavenDecisions.All;
        }
        else
        {
            link.On(AnswerMessage, OnAnswer);
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
        link = null;
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>A guest's game asks the host's to make a decision. False when it can't be sent (not a guest now).</summary>
    public bool Ask(string name, string args, string paid)
    {
        if (link == null || link.IsHost)
        {
            return false;
        }

        using FastBufferWriter writer = CoopLink.Writer(256, 16 * 1024);
        writer.WriteValueSafe(name ?? "");
        writer.WriteValueSafe(args ?? "");
        writer.WriteValueSafe(paid ?? "");
        link.ToHost(AskMessage, writer);
        return true;
    }

    private void OnAsk(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string name);
        reader.ReadValueSafe(out string args);
        reader.ReadValueSafe(out string paid);
        HavenDecisions.Outcome outcome = HavenDecisions.Make(name, args);
        if (outcome.Done && !string.IsNullOrEmpty(outcome.Text))
        {
            GameMessages.Post($"{NetworkPlayerAvatar.NameOf(sender)}, for your Haven: {outcome.Text}", MessageKind.Info);
        }

        using FastBufferWriter writer = CoopLink.Writer(256, 16 * 1024);
        writer.WriteValueSafe(outcome.Done);
        writer.WriteValueSafe(outcome.Text ?? "");
        writer.WriteValueSafe(outcome.Done ? "" : paid ?? "");
        link.Send(AnswerMessage, sender, writer);
    }

    private static void OnAnswer(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out bool done);
        reader.ReadValueSafe(out string text);
        reader.ReadValueSafe(out string paid);
        HavenDecisions.Say(done ? HavenDecisions.Outcome.Made(text) : HavenDecisions.Outcome.Refused(text));
        if (!done)
        {
            HavenDecisions.GiveBack(paid);
        }
    }
}
