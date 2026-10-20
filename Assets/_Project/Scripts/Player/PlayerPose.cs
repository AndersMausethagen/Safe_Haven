using UnityEngine;

/// <summary>Where the player stands and which way they face, to put them back there after loading a save.</summary>
public readonly struct PlayerPose
{
    public readonly Vector3 Position;
    /// <summary>Rotation around the vertical axis, in degrees.</summary>
    public readonly float Yaw;

    public PlayerPose(Vector3 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
    }

    public static PlayerPose Of(Transform transform) => new PlayerPose(transform.position, transform.eulerAngles.y);
}
