using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sizes a full-screen picture like AspectRatioFitter's Envelope Parent (fills the screen, cropping what sticks out),
/// but never crops more than <see cref="MaxCrop"/> of the picture: on a super-ultrawide screen an enveloped 16:9 or 3:2
/// picture lost over half its height. Past that the picture stops growing and the background shows at its sides.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
public sealed class ArtCover : UIBehaviour
{
    [Tooltip("Width / height of the picture.")]
    [SerializeField, Min(0.01f)] private float aspectRatio = 16f / 9f;
    [Tooltip("The most of the picture's width or height that may be cut off to fill the screen (0 = always show it all).")]
    [SerializeField, Range(0f, 0.6f)] private float maxCrop = 0.2f;

    public float AspectRatio
    {
        get => aspectRatio;
        set
        {
            aspectRatio = Mathf.Max(0.01f, value);
            Fit();
        }
    }

    public float MaxCrop { get => maxCrop; set { maxCrop = Mathf.Clamp(value, 0f, 0.6f); Fit(); } }

    /// <summary>The picture's size for a parent of <paramref name="parent"/>: covering it, cropping at most
    /// <paramref name="crop"/> of the picture on either axis.</summary>
    public static Vector2 Size(Vector2 parent, float aspect, float crop)
    {
        if (parent.x <= 0f || parent.y <= 0f)
        {
            return parent;
        }

        // Envelope: the side that runs short is stretched to the parent; the other overhangs.
        Vector2 size = parent.x / parent.y > aspect ? new Vector2(parent.x, parent.x / aspect) : new Vector2(parent.y * aspect, parent.y);
        float keep = 1f - Mathf.Clamp(crop, 0f, 0.99f);
        // Shown share of each axis = parent / size; keep it at least `keep` by shrinking the picture uniformly.
        float shown = Mathf.Min(parent.x / size.x, parent.y / size.y);
        return shown < keep ? size * (shown / keep) : size;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Fit();
    }

    protected override void OnRectTransformDimensionsChange() => Fit();

    protected override void OnTransformParentChanged() => Fit();

#if UNITY_EDITOR
    // Unity forbids resizing a RectTransform inside OnValidate (it warns "SendMessage cannot be called during ... OnValidate"),
    // so the refit waits for the Editor's next tick, as Unity's own layout components do.
    protected override void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                Fit();
            }
        };
    }
#endif

    private void LateUpdate()
    {
        // The parent's size changes with the screen; RectTransform only tells the child when its own size changes.
        Fit();
    }

    private void Fit()
    {
        var rect = (RectTransform)transform;
        if (!(rect.parent is RectTransform parent))
        {
            return;
        }

        Vector2 size = Size(parent.rect.size, aspectRatio, maxCrop);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        if (rect.sizeDelta != size)
        {
            rect.sizeDelta = size;
        }
    }
}
