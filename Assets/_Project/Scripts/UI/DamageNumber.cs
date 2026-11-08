using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A floating damage number: it pops, rises, and fades out, colored by damage type with critical hits larger. It shows
/// what it is given and returns itself to the PrefabPool when its time is up.
/// </summary>
public sealed class DamageNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;
    [Tooltip("Text color for each damage type, in DamageType order: Physical, Fire, Frost, Holy.")]
    [SerializeField] private Color[] typeColors =
    {
        Color.white,
        new Color(1f, 0.55f, 0.15f),
        new Color(0.55f, 0.85f, 1f),
        new Color(1f, 0.9f, 0.4f),
    };
    [SerializeField] private Color criticalColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField, Min(0.1f)] private float lifetime = 1f;
    [SerializeField] private float riseSpeed = 1.2f;
    [SerializeField, Min(1f)] private float criticalScale = 1.5f;
    [Tooltip("Meters per canvas unit.")]
    [SerializeField, Min(0.0001f)] private float unitSize = 0.01f;

    private Camera viewCamera;
    private float age;
    private float sizeMultiplier = 1f;
    private Vector3 drift;
    private float tilt;
    private bool critical;

    public void Show(DamageResult result)
    {
        label.text = Mathf.RoundToInt(result.Damage) + (result.IsCritical ? "!" : "");
        label.color = result.IsCritical ? criticalColor : ColorFor(result.Type);
        sizeMultiplier = result.IsCritical ? criticalScale : 1f;
        critical = result.IsCritical;
        Throw();
        Apply();
    }

    /// <summary>Shows a word instead of a number, such as "Dodged".</summary>
    public void ShowText(string text, Color color)
    {
        label.text = text;
        label.color = color;
        sizeMultiplier = 0.8f;
        critical = false;
        Throw();
        Apply();
    }

    private void OnEnable()
    {
        age = 0f;
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            PrefabPool.Release(gameObject);
            return;
        }

        // Thrown up and a little aside, slowing as it rises, so numbers from quick blows fan out instead of stacking.
        float slow = 1f - Mathf.Clamp01(age / lifetime);
        transform.position += (Vector3.up * riseSpeed * (0.4f + 1.2f * slow) + drift * slow) * Time.deltaTime;
        Apply();
    }

    private void Apply()
    {
        float t = Mathf.Clamp01(age / lifetime);

        // Starts oversized and settles quickly, so a hit reads as a pop; a critical punches harder and longer.
        float pop = 1f + (critical ? 1.1f : 0.5f) * Mathf.Clamp01(1f - age / (critical ? 0.22f : 0.15f));
        transform.localScale = Vector3.one * (unitSize * sizeMultiplier * pop);

        // Fully visible for the first half, then fades out.
        group.alpha = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);

        viewCamera = viewCamera != null ? viewCamera : Camera.main;
        if (viewCamera != null)
        {
            transform.rotation = viewCamera.transform.rotation * Quaternion.Euler(0f, 0f, tilt * Mathf.Clamp01(1f - age / 0.3f));
        }
    }

    private void Throw()
    {
        Vector2 aside = Random.insideUnitCircle.normalized * Random.Range(0.25f, 0.6f);
        drift = new Vector3(aside.x, 0f, aside.y);
        tilt = critical ? Random.Range(-12f, 12f) : 0f;
    }

    private Color ColorFor(DamageType type)
    {
        int index = (int)type;
        return index >= 0 && index < typeColors.Length ? typeColors[index] : Color.white;
    }
}
