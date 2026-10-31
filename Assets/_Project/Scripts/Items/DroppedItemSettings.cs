using UnityEngine;

/// <summary>Where stacks dropped from the bag land, and what they become there (DroppedItem).</summary>
[CreateAssetMenu(fileName = "DroppedItemSettings", menuName = "Safe Haven/Settings/Dropped Items")]
public sealed class DroppedItemSettings : GameSettings<DroppedItemSettings>
{
    [Tooltip("The object a dropped stack becomes: one prefab for every item, which shows the item's model itself.")]
    [SerializeField] private SaveableEntity prefab;
    [Tooltip("How far in front of the player a stack lands, in metres.")]
    [SerializeField, Min(0f)] private float reach = 1.1f;
    [Tooltip("How far from that spot it can land, so several drops don't lie in one heap.")]
    [SerializeField, Min(0f)] private float scatter = 0.45f;

    public SaveableEntity Prefab => prefab;
    public float Reach => reach;
    public float Scatter => scatter;
}
