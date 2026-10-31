using UnityEngine;

/// <summary>
/// Tells a generated map where something placed by hand belongs: a story object, a stone, the raid's great troll. It takes a
/// site of its kind (each anchor its own, in name order), or moves along with another object such as the exit a quest zone
/// lies around. On a painted map it does nothing.
/// <para>
/// Where it belongs: on anything in a raid scene that must stand somewhere sensible whatever the map looks like. Spawn
/// groups and exits don't need one.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMapAnchor : MonoBehaviour
{
    [Tooltip("The kind of site this takes on a generated map.")]
    [SerializeField] private RaidSiteKind site = RaidSiteKind.Side;
    [Tooltip("Follow this instead of taking a site, keeping the same distance from it.")]
    [SerializeField] private Transform follows;
    [Tooltip("Children of this are set in a ring around the site: a patrol's waypoints, which must stay on open ground.")]
    [SerializeField] private Transform ring;
    [Tooltip("Radius of that ring, in metres.")]
    [SerializeField, Min(0f)] private float ringRadius = 9f;

    public RaidSiteKind Site => site;
    public Transform Follows => follows;
    public Transform Ring => ring;
    public float RingRadius => ringRadius;

#if UNITY_EDITOR
    /// <summary>For builders.</summary>
    public void Configure(RaidSiteKind kind, Transform follow, Transform waypoints, float radius)
    {
        site = kind;
        follows = follow;
        ring = waypoints;
        ringRadius = radius;
    }
#endif
}
