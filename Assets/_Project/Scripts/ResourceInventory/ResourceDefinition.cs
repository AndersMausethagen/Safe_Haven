using System.Text;
using UnityEngine;

/// <summary>
/// One essence, such as a Life Mote, Cursed Essence or a Portal Spark (GDD §12): the weightless, metered magic resources
/// shown as bars. Materials (wood, ore, hides, berries) are bag items (ItemDefinition) with weight instead. An essence
/// belongs to a category (its tab in the essence panel) and a tier. Where it is gathered is a GatheringNodeDefinition.
/// This is authored data only. Quantities and capacity belong to the inventory (ResourceStorage), so nothing here
/// changes while playing.
/// </summary>
[CreateAssetMenu(fileName = "NewResource", menuName = "Safe Haven/Resources/Resource Definition")]
public sealed class ResourceDefinition : ScriptableObject, IDisplayName
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for lookups and saves. Leave empty to use the asset name, " +
             "but set it explicitly if the asset may be renamed later.")]
    [SerializeField] private string id;
    [Tooltip("Shown in the UI. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [Tooltip("The inventory tab this resource is listed under.")]
    [SerializeField] private ResourceCategory category;
    [Tooltip("Progression tier. Higher tiers need better tools, take longer to gather and grow in more dangerous places.")]
    [SerializeField, Range(Tiers.Min, Tiers.Max)] private int tier = 1;
    [Tooltip("One or two sentences for tooltips: what it is and what it is good for.")]
    [SerializeField, TextArea] private string description;

    [Header("Inventory UI")]
    [Tooltip("Small picture shown next to the resource's bar.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Color of this resource's bar in the UI.")]
    [SerializeField] private Color color = Color.white;
    [Tooltip("Resources with lower values are listed first within the same tier.")]
    [SerializeField] private int sortOrder;

    public string Id => string.IsNullOrWhiteSpace(id) ? MakeId(name) : id.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public ResourceCategory Category => category;
    public int Tier => tier;
    public string Description => description;
    public Sprite Icon => icon;
    public Color Color => color;
    public int SortOrder => sortOrder;

    private static string MakeId(string source)
    {
        var builder = new StringBuilder(source.Length);
        foreach (char character in source)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
        }

        return builder.ToString();
    }
}
