using UnityEngine;

/// <summary>Marks an item lying in the world and says which ItemDefinition it is, for pickups and tooltips.</summary>
[DisallowMultipleComponent]
public sealed class ItemWorldObject : MonoBehaviour
{
    [SerializeField] private ItemDefinition definition;

    public ItemDefinition Definition => definition;
}
