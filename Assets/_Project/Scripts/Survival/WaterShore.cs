using UnityEngine;

/// <summary>A water surface's edge, as what a WaterSource is reached across: the player can drink anywhere along a river or a
/// lake, or wading in. A swimmer's hands are taken, as for any interaction. Made by WaterSurface when it is Drinkable.</summary>
[DisallowMultipleComponent]
public sealed class WaterShore : MonoBehaviour, IInteractionShape
{
    private WaterSurface surface;

    public void Configure(WaterSurface water) => surface = water;

    public Vector3 ClosestPoint(Vector3 point) => surface != null ? surface.NearestPoint(point) : transform.position;
}
