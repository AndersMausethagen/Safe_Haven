using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paints the Terrain Moss layer in a circle around this object: <see cref="Bloom"/> spreads it outward with a sound
/// and particles, <see cref="BloomInstantly"/> jumps to the end state (use it as a listener's on-load response). By
/// default the circle follows the portal's ward and grows again when the ward levels up. In Play mode it paints a
/// runtime copy of the terrain data, so the terrain asset on disk is never changed and the moss is rebuilt each load.
/// </summary>
[AddComponentMenu("Safe Haven/Environment/Terrain Moss Bloom")]
public sealed class TerrainMossBloom : MonoBehaviour
{
    private const float StepSeconds = 0.05f;
    private const float GrowthThreshold = 0.25f;

    [Tooltip("Leave empty to use the first Terrain Moss in the scene.")]
    [SerializeField] private TerrainMoss moss;
    [Tooltip("Use the portal's ward radius (and grow when the ward levels up) instead of the fixed radius.")]
    [SerializeField] private bool followWardRadius = true;
    [SerializeField, Min(0.5f)] private float radius = 12f;
    [Tooltip("Width of the soft fade at the circle's edge, in metres.")]
    [SerializeField, Min(0f)] private float edgeWidth = 3f;
    [SerializeField, Range(0f, 1f)] private float strength = 1f;
    [SerializeField, Min(0f)] private float spreadSeconds = 3f;

    [Header("Feedback (Bloom only)")]
    [SerializeField] private SoundDefinition sound;
    [SerializeField] private ParticleSystem particles;
    [Tooltip("Grow the particle shape's radius with the spreading moss.")]
    [SerializeField] private bool fitParticlesToRadius = true;

    private static readonly HashSet<TerrainData> RuntimeCopies = new HashSet<TerrainData>();

    private Terrain terrain;
    private TerrainData ownedCopy;
    private Coroutine spreading;
    private bool bloomed;
    private float paintedRadius;

    public bool HasBloomed => bloomed;
    public float PaintedRadius => paintedRadius;

    /// <summary>The circle's full radius right now.</summary>
    public float TargetRadius => followWardRadius && HomePortal.Current != null ? HomePortal.Current.WardRadius : radius;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        RuntimeCopies.Clear();
    }

    /// <summary>Spreads the moss outward over Spread Seconds, with sound and particles.</summary>
    public void Bloom()
    {
        if (bloomed && paintedRadius >= TargetRadius - GrowthThreshold)
        {
            return;
        }

        bloomed = true;
        AudioManager.Play(sound, transform.position);
        if (particles != null)
        {
            particles.Play();
        }

        StartSpread(paintedRadius, TargetRadius, spreadSeconds);
    }

    /// <summary>Paints the whole circle at once with no sound or particles, e.g. when loading a game where it already bloomed.</summary>
    public void BloomInstantly()
    {
        bloomed = true;
        if (spreading != null)
        {
            StopCoroutine(spreading);
            spreading = null;
        }

        PaintTo(TargetRadius);
    }

    private void Update()
    {
        // The ward grew (levelled up): spread to the new edge.
        if (bloomed && spreading == null && followWardRadius && TargetRadius > paintedRadius + GrowthThreshold)
        {
            if (particles != null)
            {
                particles.Play();
            }

            StartSpread(paintedRadius, TargetRadius, spreadSeconds);
        }
    }

    private void StartSpread(float from, float to, float seconds)
    {
        if (spreading != null)
        {
            StopCoroutine(spreading);
        }

        spreading = StartCoroutine(Spread(from, to, seconds));
    }

    private IEnumerator Spread(float from, float to, float seconds)
    {
        if (!PrepareTerrain())
        {
            spreading = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            Paint(Mathf.Lerp(from, to, t), false);
            FitParticles();
            yield return new WaitForSeconds(StepSeconds);
            elapsed += StepSeconds;
        }

        Paint(to, true);
        FitParticles();
        spreading = null;
    }

    private void PaintTo(float target)
    {
        if (PrepareTerrain())
        {
            Paint(target, true);
        }
    }

    private void FitParticles()
    {
        if (particles != null && fitParticlesToRadius)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.radius = Mathf.Max(0.1f, paintedRadius);
        }
    }

    private bool PrepareTerrain()
    {
        if (moss == null)
        {
            moss = FindAnyObjectByType<TerrainMoss>();
        }

        if (moss == null || moss.MossLayer == null)
        {
            Debug.LogWarning($"{name}: Terrain Moss Bloom needs a Terrain Moss with a moss layer in the scene.", this);
            return false;
        }

        terrain = moss.GetComponent<Terrain>();
        if (Application.isPlaying && !RuntimeCopies.Contains(terrain.terrainData))
        {
            // Never write into the terrain asset during play: paint an instance that dies with the scene.
            ownedCopy = Instantiate(terrain.terrainData);
            RuntimeCopies.Add(ownedCopy);
            terrain.terrainData = ownedCopy;
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                terrainCollider.terrainData = ownedCopy;
            }

            moss.Rebuild();
        }

        return System.Array.IndexOf(terrain.terrainData.terrainLayers, moss.MossLayer) >= 0;
    }

    private void Paint(float toRadius, bool rebuildTufts)
    {
        TerrainData data = terrain.terrainData;
        int mossIndex = System.Array.IndexOf(data.terrainLayers, moss.MossLayer);
        int resolution = data.alphamapResolution;
        Vector3 local = transform.position - terrain.transform.position;
        float texelsPerMetre = resolution / data.size.x;
        Vector2 centre = new Vector2(local.x / data.size.x * resolution, local.z / data.size.z * resolution);
        float radiusTexels = toRadius * texelsPerMetre;

        int xMin = Mathf.Clamp(Mathf.FloorToInt(centre.x - radiusTexels) - 1, 0, resolution);
        int yMin = Mathf.Clamp(Mathf.FloorToInt(centre.y - radiusTexels) - 1, 0, resolution);
        int xMax = Mathf.Clamp(Mathf.CeilToInt(centre.x + radiusTexels) + 1, 0, resolution);
        int yMax = Mathf.Clamp(Mathf.CeilToInt(centre.y + radiusTexels) + 1, 0, resolution);
        if (xMax > xMin && yMax > yMin)
        {
            float[,,] alphas = data.GetAlphamaps(xMin, yMin, xMax - xMin, yMax - yMin);
            PaintCircle(alphas, mossIndex, centre - new Vector2(xMin, yMin), radiusTexels, edgeWidth * texelsPerMetre, strength);
            data.SetAlphamaps(xMin, yMin, alphas);
        }

        paintedRadius = toRadius;
        if (rebuildTufts)
        {
            moss.Rebuild();
        }
    }

    /// <summary>
    /// Raises the moss layer's weight inside a soft circle and scales the other layers down so every texel still sums to
    /// one. Only ever adds moss, so painting again (or painting a bigger circle) never removes earlier moss.
    /// </summary>
    /// <param name="alphas">Splat weights as returned by TerrainData.GetAlphamaps: [y, x, layer].</param>
    public static void PaintCircle(float[,,] alphas, int mossLayer, Vector2 centreTexel, float radiusTexels, float edgeTexels, float strength)
    {
        int height = alphas.GetLength(0);
        int width = alphas.GetLength(1);
        int layers = alphas.GetLength(2);
        float inner = Mathf.Max(0f, radiusTexels - edgeTexels);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centreTexel);
                if (distance >= radiusTexels)
                {
                    continue;
                }

                float fade = radiusTexels > inner ? Mathf.SmoothStep(0f, 1f, (distance - inner) / (radiusTexels - inner)) : 0f;
                float target = strength * (1f - (distance <= inner ? 0f : fade));
                float current = alphas[y, x, mossLayer];
                if (target <= current)
                {
                    continue;
                }

                float others = 1f - current;
                float scale = others > 1e-5f ? (1f - target) / others : 0f;
                for (int layer = 0; layer < layers; layer++)
                {
                    alphas[y, x, layer] = layer == mossLayer ? target : alphas[y, x, layer] * scale;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (ownedCopy != null)
        {
            RuntimeCopies.Remove(ownedCopy);
            Destroy(ownedCopy);
        }
    }
}
