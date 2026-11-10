using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Short-lived decals pressed onto the world (blood, footprints): a fixed pool of URP DecalProjectors, the oldest reused
/// when all are out, each fading over the last part of its life and optionally spreading (a pool of blood under the dead).
/// Screen-space decals: they land on whatever is inside their box. Made by
/// WorldRendering; cleared when a scene is left.
/// </summary>
public sealed class DecalPool : MonoBehaviour
{
    private sealed class Mark
    {
        public DecalProjector Projector;
        public float Born;
        public float Life;
        public float From;
        public float To;
        public float Grow;
        public Vector3 Size;
    }

    private const float FadeShare = 0.25f;
    private readonly List<Mark> marks = new List<Mark>();
    private int next;

    /// <summary>The pool in use, or null before the game systems start.</summary>
    public static DecalPool Current { get; private set; }

    public void Initialise(int size)
    {
        Current = this;
        for (int i = 0; i < size; i++)
        {
            var go = new GameObject("Decal");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var projector = go.AddComponent<DecalProjector>();
            projector.scaleMode = DecalScaleMode.ScaleInvariant;
            projector.drawDistance = 80f;
            projector.startAngleFade = 60f;
            projector.endAngleFade = 85f;
            marks.Add(new Mark { Projector = projector });
        }

        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    /// <summary>
    /// Presses a decal onto the surface at <paramref name="point"/> facing out along <paramref name="normal"/>, the top of its
    /// texture towards <paramref name="along"/>: <paramref name="size"/> metres, the atlas tile <paramref name="tile"/> (scale xy,
    /// offset zw), for <paramref name="seconds"/>, spreading to <paramref name="grow"/> times its size over <paramref name="growSeconds"/>.
    /// </summary>
    public void Press(Material material, Vector3 point, Vector3 normal, Vector3 along, Vector2 size, Vector4 tile, float seconds,
        float grow = 1f, float growSeconds = 1f)
    {
        if (material == null || marks.Count == 0)
        {
            return;
        }

        Mark mark = marks[next];
        next = (next + 1) % marks.Count;
        DecalProjector projector = mark.Projector;
        Vector3 up = Vector3.ProjectOnPlane(along, normal);
        if (up.sqrMagnitude < 1e-4f)
        {
            up = Vector3.ProjectOnPlane(Vector3.forward, normal);
        }

        Quaternion facing = Quaternion.LookRotation(-normal, up);
        projector.transform.SetPositionAndRotation(point + normal * 0.25f, facing);
        projector.material = material;
        projector.uvScale = new Vector2(tile.x, tile.y);
        projector.uvBias = new Vector2(tile.z, tile.w);
        projector.pivot = new Vector3(0f, 0f, 0.25f);
        mark.Size = new Vector3(size.x, size.y, 0.5f);
        projector.size = mark.Size;
        projector.fadeFactor = 1f;
        mark.Born = Time.time;
        mark.Life = Mathf.Max(0.5f, seconds);
        mark.From = 1f;
        mark.To = Mathf.Max(1f, grow);
        mark.Grow = Mathf.Max(0.05f, growSeconds);
        mark.Projector.gameObject.SetActive(true);
    }

    private void Update()
    {
        float now = Time.time;
        foreach (Mark mark in marks)
        {
            if (!mark.Projector.gameObject.activeSelf)
            {
                continue;
            }

            float age = now - mark.Born;
            if (age >= mark.Life)
            {
                mark.Projector.gameObject.SetActive(false);
                continue;
            }

            float fadeFrom = mark.Life * (1f - FadeShare);
            mark.Projector.fadeFactor = age > fadeFrom ? 1f - (age - fadeFrom) / (mark.Life - fadeFrom) : 1f;
            if (mark.To > 1f && age < mark.Grow)
            {
                float spread = Mathf.Lerp(mark.From, mark.To, 1f - (1f - age / mark.Grow) * (1f - age / mark.Grow));
                mark.Projector.size = new Vector3(mark.Size.x * spread, mark.Size.y * spread, mark.Size.z);
            }
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        foreach (Mark mark in marks)
        {
            mark.Projector.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (Current == this)
        {
            Current = null;
        }
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
    }
}
