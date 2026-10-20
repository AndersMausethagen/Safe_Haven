using UnityEngine;
using UnityEngine.Pool;

/// <summary>Marks a copy made by <see cref="PrefabPool"/> and resets its trails each time it is reused.</summary>
[DisallowMultipleComponent]
public sealed class PooledObject : MonoBehaviour
{
    internal ObjectPool<PooledObject> Pool { get; set; }
    internal PrefabPool Owner { get; set; }
    internal bool IsReleased { get; set; }

    private void OnEnable()
    {
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>())
        {
            trail.Clear();
        }
    }
}
