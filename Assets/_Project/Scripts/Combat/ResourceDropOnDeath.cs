using UnityEngine;

/// <summary>
/// Grants the resources a destructible object carries to whoever destroys it, using the ResourceSource on the same
/// object: a crate of stone, a breakable ore cart. Only a kill by a combatant counts, and the resources go to whoever has
/// a ResourceInventory (the player). Monsters drop items instead, through LootOnDeath. Whatever doesn't fit is reported
/// by the source rather than lost, as with gathering.
/// </summary>
[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(ResourceSource))]
public sealed class ResourceDropOnDeath : MonoBehaviour
{
    private Damageable damageable;
    private ResourceSource source;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        source = GetComponent<ResourceSource>();
    }

    private void OnEnable()
    {
        damageable.Died += OnDied;
    }

    private void OnDisable()
    {
        damageable.Died -= OnDied;
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        if (killingBlow.Source != null && killingBlow.Source.GetComponentInParent<ResourceInventory>() != null)
        {
            source.GrantTo(killingBlow.Source.gameObject);
        }
    }
}
