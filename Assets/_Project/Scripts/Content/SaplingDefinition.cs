using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A sapling that can be planted at home (an item with one: ItemDefinition.Sapling): which trees it grows into (terrain
/// tree prototypes, by their prefab), how many in-game days it takes from a hand's height to full size, and how much room
/// it needs. PlantedTrees plants and grows it. Read-only at runtime.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Items/Sapling", fileName = "Sapling")]
public sealed class SaplingDefinition : ScriptableObject
{
    [Tooltip("The trees it can grow into (prefabs the terrain paints trees with); one at random that the terrain has.")]
    [SerializeField] private List<GameObject> growsInto = new List<GameObject>();
    [Tooltip("In-game days from planting to full size. A day is 24 minutes at the usual clock.")]
    [SerializeField, Min(0.1f)] private float growDays = 3f;
    [Tooltip("Its size when planted, as a share of the grown tree's.")]
    [SerializeField, Range(0.02f, 0.6f)] private float startScale = 0.15f;
    [Tooltip("Its size when grown, at random between these (a wood isn't all one height).")]
    [SerializeField] private Vector2 fullScale = new Vector2(0.85f, 1.15f);
    [Tooltip("Metres it needs clear of other trees.")]
    [SerializeField, Min(0.5f)] private float spacing = 2.5f;

    public IReadOnlyList<GameObject> GrowsInto => growsInto;
    public float GrowDays => growDays;
    public float StartScale => startScale;
    public Vector2 FullScale => fullScale;
    public float Spacing => spacing;

    /// <summary>Its size, as a share of the grown tree's, <paramref name="days"/> in-game days after planting: slow at
    /// first, quickest in the middle, settling at full size.</summary>
    public static float ScaleAt(float days, float growDays, float startScale, float fullScale)
    {
        float grown = Mathf.Clamp01(days / Mathf.Max(0.01f, growDays));
        return Mathf.Lerp(startScale, fullScale, Mathf.SmoothStep(0f, 1f, grown));
    }
}
