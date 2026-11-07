using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// The companion a player brought on a raid, as friends need it to draw them (<see cref="RemoteCompanion"/>): who they
/// are (a resident id, so each game makes the same body) and whether they are down. Empty when none came along or they
/// were lost. A network variable on <see cref="NetworkPlayerAvatar"/>; where they stand and how they move go with the
/// player's poses.
/// </summary>
public struct CompanionLookState : INetworkSerializable, IEquatable<CompanionLookState>
{
    public FixedString64Bytes Resident;
    public bool Downed;

    public bool IsOut => Resident.Length > 0;

    /// <summary>The look of <paramref name="companion"/>; none when there is no companion or the forest has them.</summary>
    public static CompanionLookState Of(Companion companion)
    {
        if (companion == null || companion.IsLost || companion.Resident == null)
        {
            return default;
        }

        return new CompanionLookState { Resident = companion.Resident.Id, Downed = companion.IsDowned };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Resident);
        serializer.SerializeValue(ref Downed);
    }

    public bool Equals(CompanionLookState other) => Resident.Equals(other.Resident) && Downed == other.Downed;

    public override bool Equals(object obj) => obj is CompanionLookState other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Resident, Downed);
}
