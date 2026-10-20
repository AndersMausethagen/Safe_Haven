using UnityEngine;

/// <summary>
/// A family of raw resources and its tab in the resource inventory, such as Wood, Stone, Ore, Essence or Fiber.
/// Create one per family; no code changes are needed to add more. New resources in the category start with its
/// gathering tool and animation.
/// </summary>
[CreateAssetMenu(fileName = "NewResourceCategory", menuName = "Safe Haven/Resources/Resource Category")]
public sealed class ResourceCategory : ScriptableObject
{
    [Tooltip("Shown in the UI. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [Tooltip("Accent color for this category's tab and heading in the UI.")]
    [SerializeField] private Color color = Color.white;
    [Tooltip("Categories with lower values are listed first.")]
    [SerializeField] private int sortOrder;

    [Header("Defaults For New Resources")]
    [Tooltip("The tool new resources in this category need, e.g. an Axe for Wood.")]
    [SerializeField] private ToolType defaultGatherTool;
    [Tooltip("The animation new resources in this category use while being gathered.")]
    [SerializeField] private InteractionAnimation defaultGatherAnimation = InteractionAnimation.GatherLow;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Color Color => color;
    public int SortOrder => sortOrder;
    public ToolType DefaultGatherTool => defaultGatherTool;
    public InteractionAnimation DefaultGatherAnimation => defaultGatherAnimation;
}
