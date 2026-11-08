using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A slow shine across the icon of something special in a slot (Epic and up, a Masterwork): a soft diagonal band of light
/// sweeps over it every few seconds, each slot out of step with the others. Kept inside the icon by a RectMask2D on a
/// holder the size of the icon. Added by ItemSlotView.
/// </summary>
public sealed class SlotShimmer : MonoBehaviour
{
    private const float Every = 4.5f;
    private const float Sweep = 0.7f;

    private RectTransform band;
    private RectTransform area;
    private Image image;
    private float offset;

    /// <summary>Adds a shimmer over the icon.</summary>
    public static SlotShimmer Add(RectTransform icon)
    {
        var holder = (RectTransform)new GameObject("Shimmer", typeof(RectTransform)).transform;
        holder.SetParent(icon, false);
        holder.anchorMin = Vector2.zero;
        holder.anchorMax = Vector2.one;
        holder.offsetMin = holder.offsetMax = Vector2.zero;
        holder.gameObject.AddComponent<RectMask2D>();
        var shimmer = holder.gameObject.AddComponent<SlotShimmer>();
        shimmer.area = holder;
        var bandRect = (RectTransform)new GameObject("Band", typeof(RectTransform)).transform;
        bandRect.SetParent(holder, false);
        bandRect.localRotation = Quaternion.Euler(0f, 0f, -30f);
        shimmer.band = bandRect;
        shimmer.image = bandRect.gameObject.AddComponent<Image>();
        shimmer.image.sprite = UIStyle.Current.Glow;
        shimmer.image.raycastTarget = false;
        shimmer.image.color = new Color(1f, 0.97f, 0.88f, 0f);
        shimmer.offset = Random.value * Every;
        return shimmer;
    }

    private void OnDisable()
    {
        if (image != null)
        {
            image.color = new Color(1f, 0.97f, 0.88f, 0f);
        }
    }

    private void Update()
    {
        if (band == null || area == null)
        {
            return;
        }

        float t = Mathf.Repeat(Time.unscaledTime + offset, Every);
        if (t > Sweep)
        {
            if (image.color.a > 0f)
            {
                image.color = new Color(1f, 0.97f, 0.88f, 0f);
            }

            return;
        }

        float share = t / Sweep;
        Vector2 size = area.rect.size;
        band.sizeDelta = new Vector2(size.x * 0.35f, size.y * 1.8f);
        band.anchoredPosition = new Vector2(Mathf.Lerp(-size.x * 0.9f, size.x * 0.9f, share), 0f);
        image.color = new Color(1f, 0.97f, 0.88f, Mathf.Sin(share * Mathf.PI) * 0.55f);
    }
}
