using UnityEngine;

/// <summary>
/// A one-shot particle effect spawned through PrefabPool: it restarts its particles each time it is spawned and returns
/// itself to the pool when they are done. Put it on the root of effect prefabs used by harvest feedback.
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledEffect : MonoBehaviour
{
    [Tooltip("Seconds before the effect returns to the pool. 0 = the longest particle system's duration plus lifetime.")]
    [SerializeField, Min(0f)] private float lifetime;

    private ParticleSystem[] systems;
    private float releaseAt;

    private void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
        if (lifetime <= 0f)
        {
            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            }
        }
    }

    private void OnEnable()
    {
        foreach (ParticleSystem system in systems)
        {
            system.Clear(true);
            system.Play(true);
        }

        releaseAt = Time.time + Mathf.Max(0.1f, lifetime);
    }

    private void Update()
    {
        if (Time.time >= releaseAt)
        {
            PrefabPool.Release(gameObject);
        }
    }
}
