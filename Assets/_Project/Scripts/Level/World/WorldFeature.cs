using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// A piece of the world drawn as a spline (river, path, cliff, forest area): the World Builder (Editor/Level/World)
/// shapes the terrain, paints it and places objects from it, and builds again whenever the spline or a setting changes.
/// The component is only data; nothing runs in the game. What it builds lives under its "Generated" child and in the
/// terrain, so deleting the feature (and building) gives the ground back as it was.
/// </summary>
[RequireComponent(typeof(SplineContainer))]
[DisallowMultipleComponent]
public abstract class WorldFeature : MonoBehaviour
{
    public const string GeneratedName = "Generated";

    [Tooltip("Features build in this order, lowest first (defaults: cliffs 0, paths 10, rivers 20, areas 30), so a " +
             "river cuts through a path and a forest grows around both.")]
    [SerializeField] private int order = -1;
    [Tooltip("Random variation (rocks, bank plants, frayed edges). Change it for another version of the same shape.")]
    [SerializeField] private int seed = 1;
    [Tooltip("Keep the generated objects (rocks, water, plants) as they are, so you can move single pieces by hand. " +
             "The terrain still follows the spline; turn this off to generate them again.")]
    [SerializeField] private bool freezeObjects;

    private SplineContainer container;

    /// <summary>Editor only: a setting changed in the Inspector (the World Builder rebuilds).</summary>
    public static event Action<WorldFeature> Edited;

    public int Order => order >= 0 ? order : DefaultOrder;
    public int Seed => seed;
    public bool FreezeObjects => freezeObjects;

    public SplineContainer Container => container != null ? container : container = GetComponent<SplineContainer>();

    public Spline Spline => Container != null ? Container.Spline : null;

    /// <summary>How far from the line the feature reaches, in metres (its footprint).</summary>
    public abstract float Reach { get; }

    /// <summary>Per-knot values that must follow the knots when knots are added or removed.</summary>
    public virtual IEnumerable<KnotValues> PerKnot
    {
        get { yield break; }
    }

    /// <summary>Whether the feature changes the terrain (heights, paint, plants) or only places objects.</summary>
    public virtual bool ShapesTerrain => true;

    protected abstract int DefaultOrder { get; }

    /// <summary>The child that holds what this feature generated (created when <paramref name="create"/>).</summary>
    public Transform GeneratedRoot(bool create)
    {
        Transform root = transform.Find(GeneratedName);
        if (root == null && create)
        {
            root = new GameObject(GeneratedName).transform;
            root.SetParent(transform, false);
        }

        return root;
    }

    /// <summary>World-space box around the feature's footprint (empty when the spline has no knots).</summary>
    public Bounds WorldBounds()
    {
        Spline spline = Spline;
        if (spline == null || spline.Count == 0)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        Bounds bounds = spline.GetBounds(transform.localToWorldMatrix);
        bounds.Expand(new Vector3(Reach * 2f + 2f, 0f, Reach * 2f + 2f));
        return bounds;
    }

    protected virtual void Reset()
    {
        order = DefaultOrder;
        seed = UnityEngine.Random.Range(1, 100000);
    }

    protected virtual void OnValidate() => Edited?.Invoke(this);
}
