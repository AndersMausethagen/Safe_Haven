using UnityEngine;

/// <summary>
/// Plays a pooled particle effect fitted to one tree: the same leaf-fall prefab covers a slim spruce and a wide oak, in
/// each one's colours. The prefab's first burst and box shape are set at each play.
/// </summary>
public static class TreeEffects
{
    public static void Play(GameObject prefab, Vector3 centre, Quaternion rotation, Vector3 size, Color tint, int particles)
    {
        if (prefab == null || !Application.isPlaying)
        {
            return;
        }

        GameObject effect = PrefabPool.Spawn(prefab, centre, rotation);
        if (effect == null || !effect.TryGetComponent(out ParticleSystem system))
        {
            return;
        }

        ParticleSystem.ShapeModule shape = system.shape;
        shape.scale = size;
        ParticleSystem.MainModule main = system.main;
        main.startColor = new ParticleSystem.MinMaxGradient(tint * 0.75f, tint * 1.15f);
        system.emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)Mathf.Clamp(particles, 1, 300)));
    }
}
