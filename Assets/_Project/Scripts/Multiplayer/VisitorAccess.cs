using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>What a visiting friend may do in the host's Haven once the host says yes.</summary>
public enum VisitorRight : byte
{
    Build = 1,
    Stash = 2,
}

/// <summary>A friend's question waiting for the host's answer.</summary>
public readonly struct FriendAsk
{
    public FriendAsk(ulong clientId, string name, VisitorRight right)
    {
        ClientId = clientId;
        Name = name;
        Right = right;
    }

    public ulong ClientId { get; }
    public string Name { get; }
    public VisitorRight Right { get; }

    public string Question => Right == VisitorRight.Build ? $"{Name} asks to build in your Haven." : $"{Name} asks to use your stash.";
}

/// <summary>
/// The host's say over their Haven in co-op. A visiting guest asks (<see cref="Ask"/>) to build, or to use the stash; the
/// host is told and answers from the Escape menu (<see cref="Asks"/>, <see cref="Answer"/>). A yes lasts while the guest
/// stays. Building needs nothing more: what a guest builds reaches the host through <see cref="HavenSync"/>. The stash
/// goes through <see cref="StashShare"/>: the host sends it to those allowed, and they send back what they put in or take.
/// Plain C#: registers its own messages on the session's NetworkManager.
/// </summary>
public sealed class VisitorAccess
{
    private const string AskMessage = "SafeHaven.Ask";
    private const string AnswerMessage = "SafeHaven.Answer";
    private const string StashMessage = "SafeHaven.Stash";
    private const string StashChangeMessage = "SafeHaven.StashChange";

    private readonly List<FriendAsk> asks = new List<FriendAsk>();
    private readonly Dictionary<ulong, HashSet<VisitorRight>> allowed = new Dictionary<ulong, HashSet<VisitorRight>>();
    private readonly Dictionary<ulong, int> stashApplied = new Dictionary<ulong, int>();
    private readonly HashSet<VisitorRight> waiting = new HashSet<VisitorRight>();
    private readonly StashShare stash = new StashShare();
    private readonly List<ulong> stashUsers = new List<ulong>();
    private NetworkManager network;

    /// <summary>Questions waiting for the host's answer, oldest first.</summary>
    public IReadOnlyList<FriendAsk> Asks => asks;

    /// <summary>Raised when a question arrives or is answered.</summary>
    public event Action AsksChanged;

    public void StartHosting(NetworkManager manager)
    {
        network = manager;
        Reset();
        network.CustomMessagingManager.RegisterNamedMessageHandler(AskMessage, OnAsk);
        network.CustomMessagingManager.RegisterNamedMessageHandler(StashChangeMessage, OnStashChange);
    }

    public void StartGuest(NetworkManager manager)
    {
        network = manager;
        Reset();
        network.CustomMessagingManager.RegisterNamedMessageHandler(AnswerMessage, OnAnswer);
        network.CustomMessagingManager.RegisterNamedMessageHandler(StashMessage, OnStash);
    }

    public void Reset()
    {
        bool hadAsks = asks.Count > 0;
        asks.Clear();
        allowed.Clear();
        stashApplied.Clear();
        waiting.Clear();
        stash.Reset();
        if (hadAsks)
        {
            AsksChanged?.Invoke();
        }
    }

    /// <summary>A friend left: their questions and what they were allowed go with them.</summary>
    public void Forget(ulong clientId)
    {
        allowed.Remove(clientId);
        stashApplied.Remove(clientId);
        if (asks.RemoveAll(ask => ask.ClientId == clientId) > 0)
        {
            AsksChanged?.Invoke();
        }
    }

    /// <summary>A couple of times a second: the host sends its stash to those using it when it changed; a guest sends what they changed in it.</summary>
    public void Tick(bool isHost)
    {
        if (network == null)
        {
            return;
        }

        if (isHost)
        {
            StashUsers();
            if (stashUsers.Count > 0 && stash.HostStashChanged(GameSession.Stash))
            {
                foreach (ulong user in stashUsers)
                {
                    SendSnapshot(user);
                }
            }
        }
        else if (HavenVisit.MayUseStash)
        {
            StashShare.Change change = stash.CollectChange(GameSession.Stash);
            if (change != null)
            {
                Send(StashChangeMessage, NetworkManager.ServerClientId, WorldTransfer.Pack(change));
            }
        }
    }

    // ---- Guest

    /// <summary>Asks the host to build, or to use the stash. Says so, or that the answer is still awaited.</summary>
    public void Ask(VisitorRight right)
    {
        if (network == null || !network.IsConnectedClient || !HavenVisit.IsVisiting)
        {
            GameMessages.Post(HavenVisit.Refusal, MessageKind.Warning);
            return;
        }

        if (right == VisitorRight.Stash && HavenVisit.StashAllowed)
        {
            GameMessages.Post("The stash is still on its way. Try again in a moment.");
            return;
        }

        if (!waiting.Add(right))
        {
            GameMessages.Post($"Still waiting for {HavenVisit.HostName}'s answer.");
            return;
        }

        using (var writer = new FastBufferWriter(4, Allocator.Temp))
        {
            writer.WriteValueSafe((byte)right);
            network.CustomMessagingManager.SendNamedMessage(AskMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        GameMessages.Post(right == VisitorRight.Build ? $"You asked {HavenVisit.HostName} to let you build here."
            : $"You asked {HavenVisit.HostName} to let you use the stash.");
    }

    private void OnAnswer(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte rightByte);
        reader.ReadValueSafe(out bool yes);
        var right = (VisitorRight)rightByte;
        waiting.Remove(right);
        if (!HavenVisit.IsVisiting)
        {
            return;
        }

        HavenVisit.Allow(right, yes);
        string what = right == VisitorRight.Build ? "build here" : "use the stash";
        GameMessages.Post(yes ? $"{HavenVisit.HostName} lets you {what}." : $"{HavenVisit.HostName} would rather you didn't {what}.",
            yes ? MessageKind.Gain : MessageKind.Warning);
    }

    private void OnStash(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte[] packed);
        if (!HavenVisit.IsVisiting || !HavenVisit.StashAllowed)
        {
            return;
        }

        if (stash.TakeSnapshot(WorldTransfer.Unpack<StashShare.Snapshot>(packed), GameSession.Stash, Content()))
        {
            HavenVisit.StashArrived();
        }
    }

    // ---- Host

    /// <summary>The host's answer: a yes lasts while the friend stays.</summary>
    public void Answer(FriendAsk ask, bool yes)
    {
        asks.RemoveAll(waitingAsk => waitingAsk.ClientId == ask.ClientId && waitingAsk.Right == ask.Right);
        AsksChanged?.Invoke();
        if (network == null || !IsConnected(ask.ClientId))
        {
            return;
        }

        if (yes)
        {
            if (!allowed.TryGetValue(ask.ClientId, out HashSet<VisitorRight> rights))
            {
                allowed[ask.ClientId] = rights = new HashSet<VisitorRight>();
            }

            rights.Add(ask.Right);
        }

        SendAnswer(ask.ClientId, ask.Right, yes);
    }

    private void OnAsk(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte rightByte);
        var right = (VisitorRight)rightByte;
        if (right != VisitorRight.Build && right != VisitorRight.Stash)
        {
            return;
        }

        // Asked again after a yes (a dropped answer): the same yes.
        if (allowed.TryGetValue(sender, out HashSet<VisitorRight> rights) && rights.Contains(right))
        {
            SendAnswer(sender, right, true);
            return;
        }

        if (asks.Exists(ask => ask.ClientId == sender && ask.Right == right))
        {
            return;
        }

        var question = new FriendAsk(sender, NameOf(sender), right);
        asks.Add(question);
        GameMessages.Post($"{question.Question} Answer from the Escape menu.");
        AsksChanged?.Invoke();
    }

    private void SendAnswer(ulong clientId, VisitorRight right, bool yes)
    {
        using (var writer = new FastBufferWriter(8, Allocator.Temp))
        {
            writer.WriteValueSafe((byte)right);
            writer.WriteValueSafe(yes);
            network.CustomMessagingManager.SendNamedMessage(AnswerMessage, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        if (yes && right == VisitorRight.Stash)
        {
            stashApplied[clientId] = stashApplied.TryGetValue(clientId, out int applied) ? applied : 0;
            SendSnapshot(clientId);
        }
    }

    private void OnStashChange(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte[] packed);
        if (!allowed.TryGetValue(sender, out HashSet<VisitorRight> rights) || !rights.Contains(VisitorRight.Stash))
        {
            return;
        }

        // Always answered, even when nothing could be done: the guest waits for this number before taking the stash on.
        try
        {
            StashShare.Change change = WorldTransfer.Unpack<StashShare.Change>(packed);
            stashApplied[sender] = Mathf.Max(stashApplied.TryGetValue(sender, out int applied) ? applied : 0, change.number);
            StashShare.ApplyChange(change, GameSession.Stash, Content());
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            SendSnapshot(sender);
        }
    }

    private void SendSnapshot(ulong clientId)
    {
        int applied = stashApplied.TryGetValue(clientId, out int number) ? number : 0;
        Send(StashMessage, clientId, WorldTransfer.Pack(StashShare.SnapshotOf(GameSession.Stash, applied)));
    }

    private void StashUsers()
    {
        stashUsers.Clear();
        foreach (KeyValuePair<ulong, HashSet<VisitorRight>> pair in allowed)
        {
            if (pair.Value.Contains(VisitorRight.Stash))
            {
                stashUsers.Add(pair.Key);
            }
        }
    }

    private bool IsConnected(ulong clientId)
    {
        foreach (ulong client in network.ConnectedClientsIds)
        {
            if (client == clientId)
            {
                return true;
            }
        }

        return false;
    }

    private static string NameOf(ulong clientId)
    {
        foreach (NetworkPlayerAvatar avatar in NetworkPlayerAvatar.All)
        {
            if (avatar != null && avatar.OwnerClientId == clientId && !string.IsNullOrEmpty(avatar.DisplayName))
            {
                return avatar.DisplayName;
            }
        }

        return "A friend";
    }

    // Same channel as the world and its changes, so everything arrives in the order it was sent.
    private void Send(string message, ulong to, byte[] packed)
    {
        using var writer = new FastBufferWriter(packed.Length + 16, Allocator.Temp);
        writer.WriteValueSafe(packed);
        network.CustomMessagingManager.SendNamedMessage(message, to, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private static IContentLookup Content() => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
}
