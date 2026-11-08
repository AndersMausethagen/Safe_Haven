using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A player's fishing line as friends need it to draw the float (<see cref="RemoteFishing"/>): whether a line is out and
/// in what phase, where it was cast from and landed, the water's height there, when it was cast (server time, so the arc
/// plays the same everywhere) and how far the catch is reeled in. A network variable on <see cref="NetworkPlayerAvatar"/>,
/// written by its owner when it changes.
/// </summary>
public struct FishingLineState : INetworkSerializable, IEquatable<FishingLineState>
{
    /// <summary>How finely the reel's progress is told: enough for the float to move smoothly, few enough to send seldom.</summary>
    public const int CatchSteps = 32;

    /// <summary>PlayerFishing.Phase while a line is out (Waiting, Bite, Reeling); 0 when none is.</summary>
    public byte Phase;
    public Vector3 From;
    public Vector3 Landing;
    public float WaterHeight;
    /// <summary>Server time when the float left the rod.</summary>
    public double CastAt;
    /// <summary>The reel's catch in steps of 1/<see cref="CatchSteps"/>.</summary>
    public byte Catch;

    public bool IsOut => Phase != 0;

    public PlayerFishing.Phase FishingPhase => (PlayerFishing.Phase)Phase;

    public float CatchShare => Catch / (float)CatchSteps;

    /// <summary>The line of a player in <paramref name="phase"/>, cast as <paramref name="cast"/> at server time <paramref name="castAt"/>; none outside a line's phases.</summary>
    public static FishingLineState Of(PlayerFishing.Phase phase, in FishingCast cast, float reelCatch, double castAt)
    {
        // A landing shows to friends as a line reeled all the way in.
        if (phase == PlayerFishing.Phase.Landing)
        {
            phase = PlayerFishing.Phase.Reeling;
            reelCatch = 1f;
        }

        if (phase != PlayerFishing.Phase.Waiting && phase != PlayerFishing.Phase.Bite && phase != PlayerFishing.Phase.Reeling)
        {
            return default;
        }

        return new FishingLineState
        {
            Phase = (byte)phase,
            From = cast.From,
            Landing = cast.Landing,
            WaterHeight = cast.WaterHeight,
            CastAt = castAt,
            Catch = phase == PlayerFishing.Phase.Reeling ? (byte)Mathf.RoundToInt(Mathf.Clamp01(reelCatch) * CatchSteps) : (byte)0,
        };
    }

    /// <summary>The server time a cast that lands at local <paramref name="landsAt"/> left the rod, from this machine's clocks now.</summary>
    public static double CastTime(float landsAt, double serverNow, float localNow) =>
        serverNow - (localNow - (landsAt - FishingLine.FlightSeconds));

    /// <summary>The cast as this machine's clock sees it: it lands <see cref="FishingLine.FlightSeconds"/> after <see cref="CastAt"/>.</summary>
    public FishingCast CastHere(double serverNow, float localNow) =>
        new FishingCast(From, Landing, WaterHeight, localNow + (float)(CastAt + FishingLine.FlightSeconds - serverNow));

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Phase);
        if (Phase == 0)
        {
            // No line: nothing else to tell.
            return;
        }

        serializer.SerializeValue(ref From);
        serializer.SerializeValue(ref Landing);
        serializer.SerializeValue(ref WaterHeight);
        serializer.SerializeValue(ref CastAt);
        serializer.SerializeValue(ref Catch);
    }

    public bool Equals(FishingLineState other) =>
        Phase == other.Phase && (Phase == 0 || From == other.From && Landing == other.Landing && WaterHeight == other.WaterHeight
            && CastAt == other.CastAt && Catch == other.Catch);

    public override bool Equals(object obj) => obj is FishingLineState other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Phase, Landing, CastAt, Catch);
}
