using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A floating damage number: it pops, rises, and fades out, colored by damage type with critical hits larger. It shows
/// what it is given and returns itself to the PrefabPool when its time is up.
/// <para>Whose blow it was and how much it mattered show too (Docs/CombatFeelReview_2026-11-19.md §9): blows taken are red and small
/// with no pop, a companion's or a friend's are small and faint, and the size follows the share of the target's health. Blows in quick
/// succession on one target add up in one number (<see cref="Add"/>).</para>
/// </summary>
public sealed class DamageNumber : MonoBehaviour
{
    /// <summary>Whose blow a number shows.</summary>
    public enum Kind
    {
        /// <summary>This game's player struck it.</summary>
        Mine = 0,
        /// <summary>A companion, a friend, a trap, a burn: someone else's.</summary>
        Others = 1,
        /// <summary>The player was struck.</summary>
        Taken = 2,
    }

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
    [Header("Whose blow, how big (§9)")]
    [SerializeField] private Color takenColor = new Color(0.9f, 0.25f, 0.2f);
    [SerializeField, Min(0.1f)] private float takenScale = 0.85f;
    [SerializeField, Min(0.1f)] private float othersScale = 0.7f;
    [SerializeField, Range(0.1f, 1f)] private float othersAlpha = 0.6f;

    private Camera viewCamera;
    private float age;
    private float sizeMultiplier = 1f;
    private Vector3 drift;
    private float tilt;
    private bool critical;
    private bool popless;
    private float alpha = 1f;
    private float total;
    private Kind kind;
    private float share;

    /// <summary>Who this number is over while it floats (the spawner merging blows into it); none once it is back in the pool.</summary>
    public Object Owner { get; private set; }

    /// <summary>The size a number takes for the share of its target's maximum health a blow took: 0.85 under 5%, 1 at 10%, 1.3 from 30%.</summary>
    public static float SizeForShare(float share)
    {
        share = Mathf.Max(0f, share);
        return share <= 0.05f ? 0.85f : share <= 0.1f ? Mathf.Lerp(0.85f, 1f, (share - 0.05f) / 0.05f)
            : Mathf.Lerp(1f, 1.3f, Mathf.Clamp01((share - 0.1f) / 0.2f));
    }

    public void Show(DamageResult result) => Show(result, Kind.Mine, 0.1f, null);

    /// <summary>Shows a blow: whose it was (<paramref name="whose"/>) and the share of its target's health it took.</summary>
    public void Show(DamageResult result, Kind whose, float healthShare, Object owner)
    {
        Owner = owner;
        kind = whose;
        total = result.Damage;
        share = healthShare;
        critical = result.IsCritical && whose != Kind.Taken;
        Style(result);
        Throw();
        Apply();
    }

    /// <summary>Another blow of the same kind on the same target close after: the number counts it in and pops again.</summary>
    public void Add(DamageResult result, float healthShare)
    {
        total += result.Damage;
        share += healthShare;
        critical |= result.IsCritical && kind != Kind.Taken;
        Style(result);
        age = 0f;
        Apply();
    }

    private void Style(DamageResult result)
    {
        label.text = Mathf.RoundToInt(total) + (critical ? "!" : "");
        label.color = kind == Kind.Taken ? takenColor : critical ? criticalColor : ColorFor(result.Type);
        float size = kind == Kind.Taken ? takenScale : kind == Kind.Others ? othersScale : SizeForShare(share);
        sizeMultiplier = size * (critical ? criticalScale : 1f);
        popless = kind == Kind.Taken;
        alpha = kind == Kind.Others ? othersAlpha : 1f;
    }

    /// <summary>Shows a word instead of a number, such as "Dodged".</summary>
    public void ShowText(string text, Color color)
    {
        label.text = text;
        label.color = color;
        sizeMultiplier = 0.8f;
        critical = false;
        popless = false;
        alpha = 1f;
        Owner = null;
        Throw();
        Apply();
    }

    private void OnEnable()
    {
        age = 0f;
    }

    private void OnDisable() => Owner = null;

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
        float pop = popless ? 1f : 1f + (critical ? 1.1f : 0.5f) * Mathf.Clamp01(1f - age / (critical ? 0.22f : 0.15f));
        transform.localScale = Vector3.one * (unitSize * sizeMultiplier * pop);

        // Fully visible for the first half, then fades out.
        group.alpha = alpha * (1f - Mathf.Clamp01((t - 0.5f) / 0.5f));

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
