using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The ground a player's skill leaves behind (Skill › Leaves: the Ash Lad's smouldering embers, Frost Rune's rime), seen by everyone in
/// the same place (<c>SafeHaven.GroundLeft</c>, passed on by the host, as pings are). Only its look: the player who cast it keeps the
/// real patch in their own game, whose touches reach the host's creatures as that player's blows (CreaturePuppets), so nothing is hurt
/// twice. Plain C#: <see cref="CoopSession"/> ticks it and owns the connection.
/// </summary>
public sealed class CoopGround : ICoopPart
{
    private const string GroundLeftMessage = "SafeHaven.GroundLeft";
    private const int MostPoints = 8;

    private CoopLink link;
    private ContentDatabase content;

    private NetworkManager network => link?.Network;

    public void Begin(CoopLink session)
    {
        link = session;
        content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        link.On(GroundLeftMessage, OnGroundLeft);
        PlayerSkills.GroundLeft += OnGroundLeftHere;
    }

    public void Tick()
    {
    }

    public void FriendLeft(ulong clientId)
    {
    }

    public void End()
    {
        PlayerSkills.GroundLeft -= OnGroundLeftHere;
        link = null;
    }

    // This player's skill left ground: the others in the same place see it.
    private void OnGroundLeftHere(SkillDefinition skill, IReadOnlyList<Vector3> points)
    {
        if (link == null || network == null || skill == null || points == null || points.Count == 0)
        {
            return;
        }

        IReadOnlyList<ulong> to = network.IsServer ? link.Guests() : HostOnly;
        Send(GroundLeftMessageData.Of(CoopPlace.Key, skill.Id, points), to);
    }

    private void OnGroundLeft(ulong sender, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out GroundLeftMessageData left);
        // The host passes a guest's on to the others.
        if (network.IsServer)
        {
            Send(left, link.Guests(sender));
        }

        if (!left.Place.Equals(CoopPlace.Key) || content == null)
        {
            return;
        }

        SkillDefinition skill = content.FindSkill(left.Skill.ToString());
        if (skill == null || skill.Leaves == null)
        {
            return;
        }

        for (int i = 0; i < left.Count && i < MostPoints; i++)
        {
            GroundHazard.SpawnLook(skill.Leaves, left.Point(i), Faction.Player);
        }
    }

    private static readonly ulong[] HostOnlyList = { NetworkManager.ServerClientId };
    private static IReadOnlyList<ulong> HostOnly => HostOnlyList;

    private void Send(GroundLeftMessageData left, IReadOnlyList<ulong> to)
    {
        if (to == null || to.Count == 0)
        {
            return;
        }

        using var writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteNetworkSerializable(left);
        network.CustomMessagingManager.SendNamedMessage(GroundLeftMessage, to, writer, NetworkDelivery.ReliableSequenced);
    }
}

/// <summary><c>GroundLeft</c>: where (CoopPlace.Key), which skill (by id), and up to eight points it left ground at.</summary>
public struct GroundLeftMessageData : INetworkSerializable
{
    public FixedString128Bytes Place;
    public FixedString64Bytes Skill;
    public byte Count;
    public Vector3 P0, P1, P2, P3, P4, P5, P6, P7;

    public static GroundLeftMessageData Of(FixedString128Bytes place, string skill, IReadOnlyList<Vector3> points)
    {
        var data = new GroundLeftMessageData { Place = place, Skill = new FixedString64Bytes(skill ?? string.Empty), Count = (byte)Mathf.Min(points.Count, 8) };
        for (int i = 0; i < data.Count; i++)
        {
            data.Set(i, points[i]);
        }

        return data;
    }

    public Vector3 Point(int i) => i switch { 0 => P0, 1 => P1, 2 => P2, 3 => P3, 4 => P4, 5 => P5, 6 => P6, _ => P7 };

    private void Set(int i, Vector3 value)
    {
        switch (i)
        {
            case 0: P0 = value; break;
            case 1: P1 = value; break;
            case 2: P2 = value; break;
            case 3: P3 = value; break;
            case 4: P4 = value; break;
            case 5: P5 = value; break;
            case 6: P6 = value; break;
            default: P7 = value; break;
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Place);
        serializer.SerializeValue(ref Skill);
        serializer.SerializeValue(ref Count);
        serializer.SerializeValue(ref P0);
        serializer.SerializeValue(ref P1);
        serializer.SerializeValue(ref P2);
        serializer.SerializeValue(ref P3);
        serializer.SerializeValue(ref P4);
        serializer.SerializeValue(ref P5);
        serializer.SerializeValue(ref P6);
        serializer.SerializeValue(ref P7);
    }
}
