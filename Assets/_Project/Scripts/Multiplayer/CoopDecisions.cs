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
        writer.WriteNetworkSerializable(new DecisionAskMessage { Name = name ?? "", Args = args ?? "", Paid = paid ?? "" });
        link.ToHost(AskMessage, writer);
        return true;
    }

    private void OnAsk(ulong sender, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out DecisionAskMessage ask);
        HavenDecisions.Outcome outcome = HavenDecisions.Make(ask.Name, ask.Args);
        if (outcome.Done && !string.IsNullOrEmpty(outcome.Text))
        {
            GameMessages.Post($"{NetworkPlayerAvatar.NameOf(sender)}, for your Haven: {outcome.Text}", MessageKind.Info);
        }

        using FastBufferWriter writer = CoopLink.Writer(256, 16 * 1024);
        writer.WriteNetworkSerializable(new DecisionAnswerMessage { Done = outcome.Done, Text = outcome.Text ?? "", Paid = outcome.Done ? "" : ask.Paid ?? "" });
        link.Send(AnswerMessage, sender, writer);
    }

    private static void OnAnswer(ulong sender, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out DecisionAnswerMessage answer);
        HavenDecisions.Say(answer.Done ? HavenDecisions.Outcome.Made(answer.Text) : HavenDecisions.Outcome.Refused(answer.Text));
        if (!answer.Done)
        {
            HavenDecisions.GiveBack(answer.Paid);
        }
    }

    // A guest's ask: the decision's name, what it is about, and what they paid.
    private struct DecisionAskMessage : INetworkSerializable
    {
        public string Name;
        public string Args;
        public string Paid;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Args);
            serializer.SerializeValue(ref Paid);
        }
    }

    // The host's answer: made or not, what to say, and the payment to give back when not.
    private struct DecisionAnswerMessage : INetworkSerializable
    {
        public bool Done;
        public string Text;
        public string Paid;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Done);
            serializer.SerializeValue(ref Text);
            serializer.SerializeValue(ref Paid);
        }
    }
}
