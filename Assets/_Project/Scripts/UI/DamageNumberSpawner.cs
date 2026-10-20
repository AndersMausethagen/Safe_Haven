using UnityEngine;

/// <summary>
/// Shows a floating number whenever the Damageable on this object is hit. The numbers come from the scene's
/// PrefabPool rather than living under the target, so they finish their animation even when the hit destroys it.
/// </summary>
[RequireComponent(typeof(Damageable))]
public sealed class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private DamageNumber prefab;
    [Tooltip("Height above the target's pivot where numbers appear, in meters.")]
    [SerializeField] private float height = 2.2f;
    [Tooltip("Random sideways offset so quick hits don't stack on top of each other.")]
    [SerializeField, Min(0f)] private float spread = 0.4f;

    private Damageable damageable;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        damageable.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        damageable.Damaged -= OnDamaged;
    }

    private void OnDamaged(DamageInfo info, DamageResult result)
    {
        if (prefab == null || result.Damage <= 0f)
        {
            return;
        }

        PrefabPool.Spawn(prefab, SpawnPoint(), Quaternion.identity).Show(result);
    }

    /// <summary>Floats a word over the target, such as "Dodged".</summary>
    public void ShowText(string text, Color color)
    {
        if (prefab != null)
        {
            PrefabPool.Spawn(prefab, SpawnPoint(), Quaternion.identity).ShowText(text, color);
        }
    }

    private Vector3 SpawnPoint()
    {
        Vector2 offset = Random.insideUnitCircle * spread;
        return transform.position + Vector3.up * height + new Vector3(offset.x, 0f, offset.y);
    }
}
