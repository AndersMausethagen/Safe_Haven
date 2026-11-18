using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Reuses short-lived copies of prefabs (projectiles, damage numbers) instead of creating and destroying one per use,
/// built on Unity's ObjectPool. The pool lives in the active scene, so leaving a scene clears it along with every copy;
/// copies are kept under it while in use and while waiting. Release a copy with <see cref="Release"/> rather than
/// Destroy; anything not made by the pool is simply destroyed.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrefabPool : MonoBehaviour
{
    // More than this many idle copies of one prefab are destroyed instead of kept.
    private const int MaxIdlePerPrefab = 64;

    private static PrefabPool instance;
    private readonly Dictionary<GameObject, ObjectPool<PooledObject>> pools = new Dictionary<GameObject, ObjectPool<PooledObject>>();
    private Transform staging;

    /// <summary>A ready copy of the prefab at the given pose, active and in the scene.</summary>
    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        GameObject copy = Spawn(prefab.gameObject, position, rotation);
        return copy.GetComponent<T>();
    }

    /// <inheritdoc cref="Spawn{T}"/>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        PooledObject pooled = Instance.PoolFor(prefab).Get();
        pooled.transform.SetPositionAndRotation(position, rotation);
        pooled.gameObject.SetActive(true);
        return pooled.gameObject;
    }

    /// <summary>Returns a copy to its pool; copies that didn't come from one are destroyed.</summary>
    public static void Release(GameObject copy)
    {
        if (copy == null)
        {
            return;
        }

        if (copy.TryGetComponent(out PooledObject pooled) && pooled.Pool != null && pooled.Owner != null && !pooled.IsReleased)
        {
            pooled.Pool.Release(pooled);
            return;
        }

        if (pooled == null || !pooled.IsReleased)
        {
            Destroy(copy);
        }
    }

    /// <summary>Idle copies waiting for reuse, for tests and diagnostics.</summary>
    public static int CountIdle(GameObject prefab)
    {
        return instance != null && prefab != null && instance.pools.TryGetValue(prefab, out ObjectPool<PooledObject> pool) ? pool.CountInactive : 0;
    }

    private static PrefabPool Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject("Prefab Pool").AddComponent<PrefabPool>();
            }

            return instance;
        }
    }

    private ObjectPool<PooledObject> PoolFor(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<PooledObject> pool))
        {
            return pool;
        }

        pool = null;
        pool = new ObjectPool<PooledObject>(
            () => Create(prefab, pool),
            pooled => pooled.IsReleased = false,
            pooled =>
            {
                pooled.IsReleased = true;
                pooled.gameObject.SetActive(false);
            },
            pooled =>
            {
                if (pooled != null)
                {
                    Destroy(pooled.gameObject);
                }
            },
            collectionCheck: false,
            defaultCapacity: 8,
            maxSize: MaxIdlePerPrefab);
        pools.Add(prefab, pool);
        return pool;
    }

    private PooledObject Create(GameObject prefab, ObjectPool<PooledObject> pool)
    {
        // Made under an inactive parent so the copy's Awake and OnEnable run only once it is placed and activated.
        if (staging == null)
        {
            staging = new GameObject("Staging").transform;
            staging.gameObject.SetActive(false);
            staging.SetParent(transform, false);
        }
        GameObject copy = Instantiate(prefab, staging);
        copy.SetActive(false);
        copy.transform.SetParent(transform, false);
        copy.name = prefab.name;
        PooledObject pooled = copy.AddComponent<PooledObject>();
        pooled.Pool = pool;
        pooled.Owner = this;
        return pooled;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        instance = null;
    }
}
