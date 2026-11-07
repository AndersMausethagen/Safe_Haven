using Unity.Netcode;
using UnityEngine;

/// <summary>Where a player was at a moment of the shared (server) clock, and what their Animator was doing.</summary>
public struct PoseSnapshot : INetworkSerializable
{
    public double Time;
    public Vector3 Position;
    public float Yaw;
    public AnimatorFrame Animation;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Time);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Yaw);
        Animation.NetworkSerialize(serializer);
    }
}

/// <summary>
/// The last poses received from one friend, and where they are at a given moment: between the two poses around it,
/// held at the newest when nothing newer has come, moved at once across a gap (a portal). Friends are drawn a little in
/// the past (MultiplayerSettings.InterpolationDelay) so a next pose is usually there. Unreliable messages can arrive late
/// or out of order: older-than-newest ones are slotted in, duplicates dropped. Plain C#.
/// </summary>
public sealed class PoseBuffer
{
    private readonly PoseSnapshot[] poses;
    private int count;

    public PoseBuffer(int capacity = 32)
    {
        poses = new PoseSnapshot[Mathf.Max(2, capacity)];
    }

    public int Count => count;
    public double NewestTime => count > 0 ? poses[count - 1].Time : double.NegativeInfinity;

    public void Clear()
    {
        for (int i = 0; i < count; i++)
        {
            FramePool.Return(poses[i].Animation);
        }

        count = 0;
    }

    /// <summary>The newest pose kept, if any.</summary>
    public bool TryNewest(out PoseSnapshot pose)
    {
        pose = count > 0 ? poses[count - 1] : default;
        return count > 0;
    }

    public void Add(PoseSnapshot pose)
    {
        int at = count;
        while (at > 0 && poses[at - 1].Time > pose.Time)
        {
            at--;
        }

        if (at > 0 && poses[at - 1].Time == pose.Time)
        {
            FramePool.Return(pose.Animation);
            return;
        }

        if (count == poses.Length)
        {
            if (at == 0)
            {
                FramePool.Return(pose.Animation);
                return; // older than everything kept
            }

            // Drop the oldest; its arrays go back for the next pose received.
            FramePool.Return(poses[0].Animation);
            System.Array.Copy(poses, 1, poses, 0, count - 1);
            count--;
            at--;
        }

        System.Array.Copy(poses, at, poses, at + 1, count - at);
        poses[at] = pose;
        count++;
    }

    /// <summary>
    /// Where the player is at <paramref name="time"/>: position and facing between the poses around it, and the animation
    /// of the nearer one. False while nothing has arrived.
    /// </summary>
    public bool Sample(double time, float teleportDistance, out Vector3 position, out float yaw, out AnimatorFrame animation)
    {
        position = default;
        yaw = 0f;
        animation = default;
        if (count == 0)
        {
            return false;
        }

        if (time <= poses[0].Time || count == 1)
        {
            return Take(time <= poses[0].Time ? 0 : count - 1, out position, out yaw, out animation);
        }

        if (time >= poses[count - 1].Time)
        {
            return Take(count - 1, out position, out yaw, out animation);
        }

        int next = 1;
        while (poses[next].Time < time)
        {
            next++;
        }

        PoseSnapshot a = poses[next - 1];
        PoseSnapshot b = poses[next];
        if ((b.Position - a.Position).sqrMagnitude > teleportDistance * teleportDistance)
        {
            return Take(next, out position, out yaw, out animation);
        }

        float t = (float)((time - a.Time) / (b.Time - a.Time));
        position = Vector3.Lerp(a.Position, b.Position, t);
        yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t);
        animation = t < 0.5f ? a.Animation : b.Animation;
        return true;
    }

    private bool Take(int index, out Vector3 position, out float yaw, out AnimatorFrame animation)
    {
        position = poses[index].Position;
        yaw = poses[index].Yaw;
        animation = poses[index].Animation;
        return true;
    }
}
