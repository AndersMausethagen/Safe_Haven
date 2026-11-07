using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handing things over on a raid together: what a player drops (<see cref="DroppedItem.DroppedByPlayer"/>) lies on the
/// ground in every friend's game on the same run too (<c>SafeHaven.Drop</c>). Whoever picks it up first gets it: a pickup
/// asks the host (<c>DropClaim</c>), the host lets one player at a time take it (<c>DropGranted</c>/<c>DropDenied</c>), and
/// what is left (or that it is gone) goes to everyone (<c>DropResult</c>, <c>DropUpdate</c>). So nothing is taken twice.
/// Raid loot itself stays personal. Plain C#: <see cref="CoopSession"/> owns the connection.
/// </summary>
public sealed class CoopDrops : ICoopPart, IDisposable
{
    private const string DropMessage = "SafeHaven.Drop";
    private const string ClaimMessage = "SafeHaven.DropClaim";
    private const string GrantedMessage = "SafeHaven.DropGranted";
    private const string DeniedMessage = "SafeHaven.DropDenied";
    private const string ResultMessage = "SafeHaven.DropResult";
    private const string UpdateMessage = "SafeHaven.DropUpdate";

    private readonly Dictionary<string, DroppedItem> drops = new Dictionary<string, DroppedItem>();
    private readonly Dictionary<string, ulong> claimedBy = new Dictionary<string, ulong>();
    private CoopLink link;

    private NetworkManager network => link?.Network;
    private UnityEngine.SceneManagement.Scene scene;

    /// <summary>The session's, while one runs.</summary>
    public static CoopDrops Current { get; private set; }

    public CoopDrops()
    {
        DroppedItem.DroppedByPlayer += OnDroppedHere;
    }

    public void Dispose()
    {
        DroppedItem.DroppedByPlayer -= OnDroppedHere;
        if (Current == this)
        {
            Current = null;
        }
    }

    public void Begin(CoopLink session)
    {
        link = session;
        Current = this;
        Clear();
        link.On(DropMessage, OnDrop);
        link.On(UpdateMessage, OnUpdate);
        if (link.IsHost)
        {
            link.On(ClaimMessage, OnClaim);
            link.On(ResultMessage, OnResult);
        }
        else
        {
            link.On(GrantedMessage, OnGranted);
            link.On(DeniedMessage, (sender, _) => GameMessages.Post("Someone else is picking that up.", MessageKind.Warning));
        }
    }

    public void Tick()
    {
    }

    public void FriendLeft(ulong clientId) => Forget(clientId);

    public void End()
    {
        link = null;
        if (Current == this)
        {
            Current = null;
        }

        Clear();
    }

    /// <summary>A friend left: what they were picking up is free again.</summary>
    public void Forget(ulong clientId)
    {
        var freed = new List<string>();
        foreach (KeyValuePair<string, ulong> pair in claimedBy)
        {
            if (pair.Value == clientId)
            {
                freed.Add(pair.Key);
            }
        }

        foreach (string id in freed)
        {
            claimedBy.Remove(id);
        }
    }

    // Drops belong to one raid: a new scene starts the list again.
    private void KeepToThisScene()
    {
        UnityEngine.SceneManagement.Scene now = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (now != scene)
        {
            scene = now;
            Clear();
        }
    }

    private void Clear()
    {
        drops.Clear();
        claimedBy.Clear();
    }

    /// <summary>
    /// A player here picks up a shared drop: true when the pickup now waits for the host's word (it happens on
    /// <c>DropGranted</c>), false when it isn't shared and is picked up as usual.
    /// </summary>
    public bool Claim(DroppedItem item, GameObject user)
    {
        KeepToThisScene();
        if (network == null || item == null || string.IsNullOrEmpty(item.ShareId) || !drops.ContainsKey(item.ShareId))
        {
            return false;
        }

        if (network.IsServer)
        {
            if (claimedBy.ContainsKey(item.ShareId))
            {
                GameMessages.Post("Someone else is picking that up.", MessageKind.Warning);
                return true;
            }

            item.PickUp(user);
            Settle(item.ShareId, NetworkManager.ServerClientId);
            return true;
        }

        using var writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(item.ShareId);
        network.CustomMessagingManager.SendNamedMessage(ClaimMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        return true;
    }

    // ---- Dropping

    private void OnDroppedHere(DroppedItem item)
    {
        if (network == null || item == null || GameSession.ActiveRaid == null || (!network.IsServer && !network.IsConnectedClient))
        {
            return;
        }

        KeepToThisScene();
        item.ShareId = Guid.NewGuid().ToString("N");
        drops[item.ShareId] = item;
        SendDrop(item.ShareId, ((ISaveable)item).CaptureState(), item.transform.position, item.transform.rotation, CoopPlace.Key,
            link.IsHost ? link.Guests() : link.Only(NetworkManager.ServerClientId));
    }

    private void SendDrop(string id, string state, Vector3 at, Quaternion facing, FixedString128Bytes place, IReadOnlyList<ulong> to)
    {
        if (to.Count == 0)
        {
            return;
        }

        using var writer = new FastBufferWriter(1024, Allocator.Temp, 64 * 1024);
        writer.WriteValueSafe(place);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(state);
        writer.WriteValueSafe(at);
        writer.WriteValueSafe(facing);
        network.CustomMessagingManager.SendNamedMessage(DropMessage, to, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private void OnDrop(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out FixedString128Bytes place);
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out string state);
        reader.ReadValueSafe(out Vector3 at);
        reader.ReadValueSafe(out Quaternion facing);
        if (network.IsServer)
        {
            SendDrop(id, state, at, facing, place, link.Guests(sender));
        }

        KeepToThisScene();
        if (!place.Equals(CoopPlace.Key) || drops.ContainsKey(id))
        {
            return;
        }

        DroppedItem item = DroppedItem.PlaceAt(state, at, facing);
        if (item != null)
        {
            item.ShareId = id;
            drops[id] = item;
        }
    }

    // ---- Picking up (the host decides who)

    private void OnClaim(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        bool free = drops.TryGetValue(id, out DroppedItem item) && item != null && !claimedBy.ContainsKey(id);
        using var writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(id);
        if (free)
        {
            claimedBy[id] = sender;
        }

        network.CustomMessagingManager.SendNamedMessage(free ? GrantedMessage : DeniedMessage, sender, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private void OnGranted(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        string left = string.Empty;
        if (drops.TryGetValue(id, out DroppedItem item) && item != null && Player.Current != null)
        {
            item.PickUp(Player.Current.gameObject);
            left = item != null && !item.Stack.IsEmpty ? ((ISaveable)item).CaptureState() : string.Empty;
        }

        using var writer = new FastBufferWriter(1024, Allocator.Temp, 64 * 1024);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(left);
        network.CustomMessagingManager.SendNamedMessage(ResultMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private void OnResult(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out string left);
        if (!claimedBy.TryGetValue(id, out ulong claimer) || claimer != sender)
        {
            return;
        }

        if (drops.TryGetValue(id, out DroppedItem item))
        {
            Apply(item, id, left);
        }

        claimedBy.Remove(id);
        Broadcast(id, left, sender);
    }

    // The host picked up a shared drop itself: everyone hears what is left.
    private void Settle(string id, ulong except)
    {
        string left = drops.TryGetValue(id, out DroppedItem item) && item != null && !item.Stack.IsEmpty ? ((ISaveable)item).CaptureState() : string.Empty;
        if (string.IsNullOrEmpty(left))
        {
            drops.Remove(id);
        }

        Broadcast(id, left, except);
    }

    private void Broadcast(string id, string left, ulong except)
    {
        IReadOnlyList<ulong> to = link.Guests(except);
        if (to.Count == 0)
        {
            return;
        }

        using var writer = new FastBufferWriter(1024, Allocator.Temp, 64 * 1024);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(left);
        network.CustomMessagingManager.SendNamedMessage(UpdateMessage, to, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private void OnUpdate(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out string left);
        if (drops.TryGetValue(id, out DroppedItem item))
        {
            Apply(item, id, left);
        }
    }

    // What is left of a drop here: the rest of the stack, or nothing (it goes).
    private void Apply(DroppedItem item, string id, string left)
    {
        if (string.IsNullOrEmpty(left))
        {
            drops.Remove(id);
            if (item != null)
            {
                item.Vanish();
            }

            return;
        }

        if (item != null)
        {
            ((ISaveable)item).RestoreState(left);
        }
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
    }
}
