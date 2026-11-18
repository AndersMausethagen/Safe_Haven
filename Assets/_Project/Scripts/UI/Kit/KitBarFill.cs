using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A kit bar's fill made alive (Hearthline 3, "Safe Haven/UI Kit Bar"): it glides to a new value instead of jumping,
/// pulses when it grows, and keeps its shader told its size so its ends stay round. Each bar has its own material (its
/// size and the phase of its shine are its own). Drives the fill through its right anchor: the fill is a stretched child
/// of the bar's track.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class KitBarFill : MonoBehaviour
{
    private static readonly int SizeId = Shader.PropertyToID("_Size");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");

    private Image image;
    private RectTransform rect;
    private Material material;
    private float shown = -1f;
    private float target;
    private float pulse;
    private bool drives;
    private Vector2 lastSize;

    /// <summary>Only the look (the bar shader, its size kept): for bars whose own code moves the fill (the vitals, with
    /// their trail).</summary>
    public static KitBarFill Look(Image fill)
    {
        KitBarFill bar = On(fill);
        bar?.Init();
        return bar;
    }

    /// <summary>Gives a fill image the kit bar's look and life (once).</summary>
    public static KitBarFill On(Image fill)
    {
        if (fill == null)
        {
            return null;
        }

        KitBarFill bar = fill.GetComponent<KitBarFill>();
        return bar != null ? bar : fill.gameObject.AddComponent<KitBarFill>();
    }

    private void Awake() => Init();

    // Also from Show: a window built hidden sets its bars before they wake.
    private void Init()
    {
        if (image != null)
        {
            return;
        }

        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        Material shared = UIStyle.Current.BarMaterial;
        if (shared != null)
        {
            material = new Material(shared) { name = shared.name + " (bar)" };
            material.SetFloat(PhaseId, Random.value);
            image.material = material;
            image.sprite = null;
            image.type = Image.Type.Simple;
        }
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }

    /// <summary>Glides to <paramref name="fraction"/> in <paramref name="colour"/>; the first value is shown at once.</summary>
    public void Show(float fraction, Color colour)
    {
        Init();
        drives = true;
        fraction = Mathf.Clamp01(fraction);
        if (shown < 0f)
        {
            shown = fraction;
        }
        else if (fraction > target + 0.005f)
        {
            pulse = 1f;
        }

        target = fraction;
        image.color = colour;
        Apply();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (drives && !Mathf.Approximately(shown, target))
        {
            // Quick at first, easing in: a swing of the whole bar takes about half a second.
            shown = Mathf.MoveTowards(Mathf.Lerp(shown, target, 1f - Mathf.Exp(-10f * dt)), target, dt * 0.25f);
            Apply();
        }

        if (pulse > 0f)
        {
            pulse = Mathf.Max(0f, pulse - dt * 2.5f);
            if (material != null)
            {
                material.SetFloat(PulseId, pulse * pulse);
            }
        }

        Vector2 size = rect.rect.size;
        if (material != null && size != lastSize)
        {
            lastSize = size;
            material.SetVector(SizeId, new Vector4(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y), 0f, 0f));
        }
    }

    private void Apply()
    {
        rect.anchorMax = new Vector2(shown, rect.anchorMax.y);
        image.enabled = shown > 0.001f;
    }
}
