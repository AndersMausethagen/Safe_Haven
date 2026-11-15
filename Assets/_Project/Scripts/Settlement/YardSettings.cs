using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How fenced yards are found and what they can be (Yards, Docs/BuildingSystemResearch.md §3.4.4): the roles in order (a
/// tie goes to the one listed first), how near fence ends must be to join, how big a yard may be, whether a house's wall may
/// close a side (the user, 2026-11-15: yes), and what makes a yard better. Lives at Resources/YardSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Yard Settings", fileName = "YardSettings")]
public sealed class YardSettings : GameSettings<YardSettings>
{
    [Tooltip("What a yard can be, in order: a tie goes to the one listed first.")]
    [SerializeField] private List<YardRoleDefinition> roles = new List<YardRoleDefinition>();
    [Tooltip("Fence ends this close (metres) are one post. The build placer snaps ends together within about a metre.")]
    [SerializeField, Min(0.05f)] private float joinDistance = 0.9f;
    [Tooltip("Square metres: smaller rings are just fences round a post, larger ones the whole village.")]
    [SerializeField, Min(1f)] private float minArea = 12f;
    [SerializeField, Min(10f)] private float maxArea = 900f;
    [Tooltip("A house's walls may close one side of a yard (a farmstead of a house and three fences).")]
    [SerializeField] private bool houseWallsClose = true;
    [Tooltip("Quality: one a kind of decoration inside counts, up to this many.")]
    [SerializeField, Range(0, 5)] private int mostDressing = 3;
    [Tooltip("Quality: a path piece within this many metres of a gate counts as a path to it.")]
    [SerializeField, Min(0f)] private float pathReach = 2.5f;
    [Tooltip("The overlay's colour for a ring without a gate, or a yard without a role yet.")]
    [SerializeField] private Color plainColour = new Color(0.75f, 0.72f, 0.66f, 1f);

    public IReadOnlyList<YardRoleDefinition> Roles => roles;
    public float JoinDistance => joinDistance;
    public float MinArea => minArea;
    public float MaxArea => maxArea;
    public bool HouseWallsClose => houseWallsClose;
    public int MostDressing => mostDressing;
    public float PathReach => pathReach;
    public Color PlainColour => plainColour;

    /// <summary>Sets the roles (tests and editor tools).</summary>
    public void Configure(List<YardRoleDefinition> yardRoles) => roles = yardRoles ?? new List<YardRoleDefinition>();
}
