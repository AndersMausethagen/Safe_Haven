using UnityEngine;

/// <summary>A generated pond's shore, as what a WaterSource is reached across: the player can drink anywhere along it, or wading in. A swimmer's hands are taken, as for any interaction.</summary>
[DisallowMultipleComponent]
public sealed class RaidPondShore : MonoBehaviour, IInteractionShape
{
    private RaidLayoutPond pond;
    private Vector3 origin;

    /// <summary>Says which pond, and where the corner of the map its metres count from lies in the world.</summary>
    public void Configure(RaidLayoutPond shore, Vector3 mapOrigin)
    {
        pond = shore;
        origin = mapOrigin;
    }

    public Vector3 ClosestPoint(Vector3 point)
    {
        Vector2 nearest = pond.Shore(new Vector2(point.x - origin.x, point.z - origin.z));
        return new Vector3(origin.x + nearest.x, origin.y + pond.Level, origin.z + nearest.y);
    }
}
