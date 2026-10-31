using UnityEngine;

/// <summary>
/// The load made visible: sacks, crates and logs that fill a cart or boat as its cargo gets heavier and heap up over the rim
/// when it is overfilled, so a glance says how full it is without the HUD. The pieces are in the order they appear, bottom
/// layer first; those past Full Pieces are the heap. They grow in rather than pop.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Transport))]
public sealed class CargoPile : MonoBehaviour
{
    private const float GrowSeconds = 0.25f;

    [Tooltip("In the order they appear.")]
    [SerializeField] private Transform[] pieces = System.Array.Empty<Transform>();
    [Tooltip("How many of them show at the rated capacity. The rest are for overfilling.")]
    [SerializeField, Min(0)] private int fullPieces = 9;

    private Transport transport;
    private Vector3[] sizes;
    private float[] grown;
    private int shown;

    /// <summary>How many pieces are out.</summary>
    public int Shown => shown;

    /// <summary>
    /// How many pieces a load shows: at least one for anything at all, Full Pieces at the capacity, and all of them at the
    /// overload limit.
    /// </summary>
    public static int PiecesFor(float weight, float capacity, float limit, int full, int total)
    {
        if (weight <= 0f || total <= 0)
        {
            return 0;
        }

        if (weight <= capacity)
        {
            return Mathf.Clamp(Mathf.CeilToInt(weight / capacity * full), 1, Mathf.Min(full, total));
        }

        return Mathf.Clamp(full + Mathf.CeilToInt(Mathf.InverseLerp(capacity, limit, weight) * (total - full)), 1, total);
    }

    private void Awake()
    {
        transport = GetComponent<Transport>();
        sizes = new Vector3[pieces.Length];
        grown = new float[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
        {
            sizes[i] = pieces[i].localScale;
            pieces[i].gameObject.SetActive(false);
        }
    }

    // For its whole life, not OnEnable: it switches itself off when settled, and a change of load has to wake it.
    private void Start()
    {
        transport.Cargo.WeightChanged += Count;
        Count();
    }

    private void OnDestroy()
    {
        if (transport != null && transport.Cargo != null)
        {
            transport.Cargo.WeightChanged -= Count;
        }
    }

    private void Count()
    {
        TransportDefinition definition = transport.Definition;
        shown = PiecesFor(transport.Weight, definition.Capacity, definition.OverloadLimit, fullPieces, pieces.Length);
        enabled = true;
    }

    private void Update()
    {
        bool settled = true;
        for (int i = 0; i < pieces.Length; i++)
        {
            float wanted = i < shown ? 1f : 0f;
            grown[i] = Mathf.MoveTowards(grown[i], wanted, Time.deltaTime / GrowSeconds);
            settled &= Mathf.Approximately(grown[i], wanted);
            pieces[i].gameObject.SetActive(grown[i] > 0f);
            pieces[i].localScale = sizes[i] * grown[i];
        }

        // Nothing to do until the load changes again.
        enabled = !settled;
    }
}
