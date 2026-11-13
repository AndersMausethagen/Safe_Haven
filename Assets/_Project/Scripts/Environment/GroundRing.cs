using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A ring of light pressed onto the ground round a special place (its Mark, BuildStructureModels.Marks). A decal paints
/// everything inside its box, so a tall box painted the player walking through it and the place's own stones too. When it
/// wakes it fits its box to the ground under the ring instead: from a hand above the highest ground to a hand below the
/// lowest, measured on the terrain where the place was put down (raids are generated, so only then is the ground known).
/// </summary>
[RequireComponent(typeof(DecalProjector))]
public sealed class GroundRing : MonoBehaviour
{
    private const int Samples = 16;
    private const float Above = 0.2f;
    private const float Below = 0.3f;

    private void Start()
    {
        var decal = GetComponent<DecalProjector>();
        float radius = Mathf.Max(decal.size.x, decal.size.y) * 0.5f;
        Vector3 centre = transform.position;
        float low = float.MaxValue, high = float.MinValue;
        for (int i = 0; i <= Samples; i++)
        {
            // The middle, then round the rim.
            float angle = i * Mathf.PI * 2f / Samples;
            Vector3 at = i == Samples ? centre : centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!GroundAt(at, out float height))
            {
                continue;
            }

            low = Mathf.Min(low, height);
            high = Mathf.Max(high, height);
        }

        if (low > high)
        {
            return; // nothing under it to measure: keep the box it was made with
        }

        // The box hangs from the decal's own position along its forward (down): top at the highest ground and a hand.
        transform.position = new Vector3(centre.x, high + Above, centre.z);
        float depth = high - low + Above + Below;
        decal.size = new Vector3(decal.size.x, decal.size.y, depth);
        decal.pivot = new Vector3(0f, 0f, depth * 0.5f);
    }

    private static bool GroundAt(Vector3 at, out float height)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 local = at - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
            {
                height = terrain.SampleHeight(at) + terrain.transform.position.y;
                return true;
            }
        }

        height = 0f;
        return false;
    }
}
