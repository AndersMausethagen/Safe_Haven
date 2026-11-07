using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

/// <summary>Co-op: friends see each other's fishing line (FishingLineState, drawn by RemoteFishing).</summary>
public sealed class CoopFishingTests
{
    private static readonly FishingCast Cast = new FishingCast(new Vector3(1f, 2f, 3f), new Vector3(9f, 1.5f, 4f), 1.5f, 50.45f);

    private static FishingLineState RoundTrip(FishingLineState state, out int bytes)
    {
        using var writer = new FastBufferWriter(256, Unity.Collections.Allocator.Temp);
        writer.WriteNetworkSerializable(state);
        bytes = writer.Length;
        using var reader = new FastBufferReader(writer, Unity.Collections.Allocator.Temp);
        reader.ReadNetworkSerializable(out FishingLineState read);
        return read;
    }

    [Test]
    public void AFriendsLine_TravelsWhole()
    {
        FishingLineState line = FishingLineState.Of(PlayerFishing.Phase.Reeling, Cast, 0.5f, 1000.0);
        FishingLineState read = RoundTrip(line, out _);
        Assert.IsTrue(read.Equals(line));
        Assert.AreEqual(PlayerFishing.Phase.Reeling, read.FishingPhase);
        Assert.AreEqual(Cast.Landing, read.Landing);
        Assert.AreEqual(Cast.From, read.From);
        Assert.AreEqual(1.5f, read.WaterHeight);
        Assert.AreEqual(1000.0, read.CastAt);
        Assert.AreEqual(0.5f, read.CatchShare, 0.001f);
    }

    [Test]
    public void NoLine_IsOneByte()
    {
        foreach (PlayerFishing.Phase phase in new[] { PlayerFishing.Phase.None, PlayerFishing.Phase.Ready, PlayerFishing.Phase.Charging, PlayerFishing.Phase.Grabbed })
        {
            FishingLineState none = FishingLineState.Of(phase, Cast, 0.5f, 1000.0);
            Assert.IsFalse(none.IsOut, phase.ToString());
            Assert.IsTrue(none.Equals(default(FishingLineState)), "Nothing to send again while no line is out");
            Assert.IsFalse(RoundTrip(none, out int bytes).IsOut);
            Assert.AreEqual(1, bytes, "Only that there is no line");
        }
    }

    [Test]
    public void TheReel_IsToldInSteps_AndOnlyWhileReeling()
    {
        Assert.AreEqual(0, FishingLineState.Of(PlayerFishing.Phase.Waiting, Cast, 0.7f, 0.0).Catch);
        Assert.AreEqual(16, FishingLineState.Of(PlayerFishing.Phase.Reeling, Cast, 0.51f, 0.0).Catch);
        Assert.IsTrue(FishingLineState.Of(PlayerFishing.Phase.Reeling, Cast, 0.50f, 0.0).Equals(FishingLineState.Of(PlayerFishing.Phase.Reeling, Cast, 0.51f, 0.0)),
            "A small turn of the reel isn't worth a message");
    }

    [Test]
    public void TheCast_LandsAtTheSameMomentEverywhere()
    {
        // The owner casts at local 50.00 (landing at 50.45) while the server clock reads 1000.
        double castAt = FishingLineState.CastTime(50.45f, 1000.0, 50f);
        Assert.AreEqual(1000.0, castAt, 0.0001);
        // A friend reads it 0.2 s later on the server clock, at their own local 7.00: it lands 0.25 s from now there.
        FishingLineState line = FishingLineState.Of(PlayerFishing.Phase.Waiting, Cast, 0f, castAt);
        Assert.AreEqual(7.25f, line.CastHere(1000.2, 7f).LandsAt, 0.001f);
    }

    [Test]
    public void AFriendsFloat_IsDrawnWhereTheirLineLanded_AndGoesWithIt()
    {
        var body = new GameObject("Friend");
        var friend = new RemoteFishing(body.transform);
        try
        {
            // Cast ten seconds ago on the server clock; the view keeps time with Time.time, as the game passes it.
            FishingLineState line = FishingLineState.Of(PlayerFishing.Phase.Waiting, Cast, 0f, 90.0);
            friend.Show(line, 100.0, Time.time, 0.02f);
            Assert.IsTrue(friend.Bobber.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(friend.Bobber.position, Cast.Landing), 0.1f, "Landed long ago: on the water where it fell");

            friend.Show(FishingLineState.Of(PlayerFishing.Phase.Bite, Cast, 0f, 90.0), 100.0, Time.time, 0.02f);
            Assert.Less(friend.Bobber.position.y, Cast.Landing.y - 0.1f, "A bite pulls it under");

            friend.Show(default, 100.0, Time.time, 0.02f);
            Assert.IsFalse(friend.Bobber.gameObject.activeSelf, "The line is in: the float goes");
        }
        finally
        {
            if (friend.Bobber != null)
            {
                Object.DestroyImmediate(friend.Bobber.gameObject);
            }

            foreach (LineRenderer line in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (line.name == "Fishing Line")
                {
                    Object.DestroyImmediate(line.gameObject);
                }
            }

            Object.DestroyImmediate(body);
        }
    }
}
