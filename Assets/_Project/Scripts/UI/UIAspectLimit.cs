using UnityEngine;

/// <summary>
/// Keeps a layout no wider than an aspect (16:9 by default), centred in its parent: on a super-ultrawide screen the menu's
/// buttons, title and notes stay near the middle instead of being pinned to the far edges, while the picture behind them
/// still fills the screen (ArtCover). Put it on a stretched container that holds the layout; the children keep their own
/// anchors inside it. Runs in the editor too, so the layout can be seen at any game-view size.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class UIAspectLimit : MonoBehaviour
{
    [Tooltip("Widest the layout may be, as width over height (16:9 = 1.778).")]
    [SerializeField, Min(0.5f)] private float widestAspect = 16f / 9f;

    private RectTransform self;
    private Vector2 lastParentSize = new Vector2(-1f, -1f);

    /// <summary>The width a layout gets inside a parent of this size.</summary>
    public static float WidthFor(Vector2 parentSize, float widestAspect) => Mathf.Min(parentSize.x, parentSize.y * widestAspect);

    private void OnEnable()
    {
        lastParentSize = new Vector2(-1f, -1f);
        Apply();
    }

    private void Update() => Apply();

    private void Apply()
    {
        if (self == null)
        {
            self = (RectTransform)transform;
        }

        var parent = self.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Vector2 size = parent.rect.size;
        if (size == lastParentSize)
        {
            return;
        }

        lastParentSize = size;
        float width = WidthFor(size, widestAspect);
        self.anchorMin = new Vector2(0.5f, 0f);
        self.anchorMax = new Vector2(0.5f, 1f);
        self.pivot = new Vector2(0.5f, 0.5f);
        self.sizeDelta = new Vector2(width, 0f);
        self.anchoredPosition = Vector2.zero;
    }
}
