using UnityEngine;

/// <summary>
/// The walkable ice on a frozen pond or lake: a collider at the water's surface, made by <see cref="WinterIce"/> and
/// switched on while the water is frozen. Footsteps on it sound of ice (PlayerAudio).
/// </summary>
[DisallowMultipleComponent]
public sealed class IceSheet : MonoBehaviour
{
    private MeshCollider surface;

    public static IceSheet Make(WaterSurface water)
    {
        var sheet = new GameObject("Ice").AddComponent<IceSheet>();
        sheet.transform.SetParent(water.transform, false);
        sheet.gameObject.layer = 0; // Default: what the player and creatures stand on
        sheet.surface = sheet.gameObject.AddComponent<MeshCollider>();
        MeshFilter filter = water.GetComponent<MeshFilter>();
        sheet.surface.sharedMesh = filter != null ? filter.sharedMesh : null;
        sheet.surface.enabled = false;
        return sheet;
    }

    public bool Solid
    {
        get => surface != null && surface.enabled;
        set
        {
            if (surface != null)
            {
                surface.enabled = value;
            }
        }
    }
}
