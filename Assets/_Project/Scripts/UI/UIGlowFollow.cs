using UnityEngine;

/// <summary>
/// Keeps a glow (HearthlineArt.AddGlow) on the control it lights: the same place and size, a little larger, shown while the
/// control is. It is the control's sibling, drawn just before it, so the control stays on top and no layout is changed.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class UIGlowFollow : MonoBehaviour
{
    private RectTransform target;
    private RectTransform self;
    private UnityEngine.UI.Image image;
    private float reach;

    public void Follow(RectTransform control, float margin)
    {
        target = control;
        reach = margin;
        self = (RectTransform)transform;
        image = GetComponent<UnityEngine.UI.Image>();
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        bool shown = target.gameObject.activeInHierarchy;
        if (image != null && image.enabled != shown)
        {
            image.enabled = shown;
        }

        if (!shown)
        {
            return;
        }

        // From the control's real rectangle, whatever its anchors: a stretched control's sizeDelta isn't its size.
        self.anchorMin = self.anchorMax = self.pivot = new Vector2(0.5f, 0.5f);
        self.position = target.TransformPoint(target.rect.center);
        self.sizeDelta = target.rect.size + Vector2.one * (reach * 2f);
        self.localScale = target.localScale;
    }
}
