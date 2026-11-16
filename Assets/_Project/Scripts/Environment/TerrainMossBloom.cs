using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paints the Terrain Moss layer in a circle around this object: <see cref="Bloom"/> spreads it outward with a sound
/// and particles, <see cref="BloomInstantly"/> jumps to the end state (use it as a listener's on-load response). By
/// default the circle follows the portal's ward and grows again when the ward levels up. In Play mode it paints a
/// runtime copy of the terrain data, so the terrain asset on disk is never changed and the moss is rebuilt each load.
/// With a Paint Layer and Turn Back layers it paints that layer instead, out of those layers only: the Haven's portal
/// ground starts as brown earth (Cedar Soil and Litter, HavenWorldGenerator.TurnMossToEarth) and the first raid turns
/// it back into the moss that lies round it (Haven Lush Moss), leaving the meadow and the paths as they are.
/// </summary>
[AddComponentMenu("Safe Haven/Environment/Terrain Moss Bloom")]
public sealed class TerrainMossBloom : MonoBehaviour
{
    private const float StepSeconds = 0.05f;
    private const float GrowthThreshold = 0.25f;

    [Tooltip("Leave empty to use the first Terrain Moss in the scene.")]
    [SerializeField] private TerrainMoss moss;
    [Tooltip("The layer painted. Empty: the Terrain Moss's own layer.")]
    [SerializeField] private TerrainLayer paintLayer;
    [Tooltip("Only these layers turn into the painted one (what the ground was turned into); every other layer stays. " +
             "Empty: every layer gives way to it.")]
    [SerializeField] private TerrainLayer[] turnBack = new TerrainLayer[0];
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

    private Terrain terrain;
    private TerrainData ownedCopy;
    private Coroutine spreading;
    private bool bloomed;
    private float paintedRadius;

    public bool HasBloomed => bloomed;
    public float PaintedRadius => paintedRadius;

    /// <summary>The circle's full radius right now.</summary>
    public float TargetRadius => followWardRadius && HomePortal.Current != null ? HomePortal.Current.WardRadius : radius;


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
        // The paint right after rebuilds the moss on the copy anyway: not twice on loading (each reads the whole alphamap).
        if (PrepareTerrain(rebuildOnCopy: false))
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

    private bool PrepareTerrain(bool rebuildOnCopy = true)
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
        // Never write into the terrain asset during play: paint the runtime copy every changer shares (RuntimeTerrain), made
        // here if none is yet, dying with the scene.
        TerrainData made = Application.isPlaying ? RuntimeTerrain.Own(terrain) : null;
        if (made != null)
        {
            ownedCopy = made;
            if (rebuildOnCopy)
            {
                moss.Rebuild();
            }
        }

        return System.Array.IndexOf(terrain.terrainData.terrainLayers, PaintedLayer) >= 0;
    }

    private TerrainLayer PaintedLayer => paintLayer != null ? paintLayer : moss.MossLayer;

    private void Paint(float toRadius, bool rebuildTufts)
    {
        TerrainData data = terrain.terrainData;
        int mossIndex = System.Array.IndexOf(data.terrainLayers, PaintedLayer);
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
            Vector2 at = centre - new Vector2(xMin, yMin);
            if (turnBack != null && turnBack.Length > 0)
            {
                var from = new List<int>(turnBack.Length);
                foreach (TerrainLayer layer in turnBack)
                {
                    int index = System.Array.IndexOf(data.terrainLayers, layer);
                    if (index >= 0 && index != mossIndex)
                    {
                        from.Add(index);
                    }
                }

                TurnBackCircle(alphas, mossIndex, from, at, radiusTexels, edgeWidth * texelsPerMetre, strength);
            }
            else
            {
                PaintCircle(alphas, mossIndex, at, radiusTexels, edgeWidth * texelsPerMetre, strength);
            }

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

    /// <summary>
    /// Inside a soft circle, gives the <paramref name="fromLayers"/>' weight to <paramref name="toLayer"/>: of their weight and
    /// its own together, at least the circle's share (full inside, fading over the edge) becomes it. Other layers keep their
    /// weight, so every texel still sums to one. Only ever adds, so painting again or wider never takes any back.
    /// </summary>
    public static void TurnBackCircle(float[,,] alphas, int toLayer, IReadOnlyList<int> fromLayers, Vector2 centreTexel, float radiusTexels,
        float edgeTexels, float strength)
    {
        int height = alphas.GetLength(0);
        int width = alphas.GetLength(1);
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

                float fade = distance <= inner || radiusTexels <= inner ? 0f : Mathf.SmoothStep(0f, 1f, (distance - inner) / (radiusTexels - inner));
                float share = strength * (1f - fade);
                float from = 0f;
                foreach (int layer in fromLayers)
                {
                    from += alphas[y, x, layer];
                }

                float current = alphas[y, x, toLayer];
                float pool = from + current;
                float target = pool * share;
                if (from <= 1e-5f || target <= current)
                {
                    continue;
                }

                // Taken from each in proportion, so soil and leaves give way alike.
                float keep = (pool - target) / from;
                foreach (int layer in fromLayers)
                {
                    alphas[y, x, layer] *= keep;
                }

                alphas[y, x, toLayer] = target;
            }
        }
    }

    private void OnDestroy()
    {
        if (ownedCopy != null)
        {
            Destroy(ownedCopy);
        }
    }
}
