using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A Farm Plot that shows its field (Fields): the bed bare with nothing sown, the crop rising from a sprout to full as it grows and
/// full while the harvest waits, and the soil darker while it is watered. Added by the Farm Plot itself (CraftingStation); reads
/// its queue twice a second. The crop parts are the Model's children other than the bed (named "Farm Bed").
/// </summary>
[DisallowMultipleComponent]
public sealed class FarmPlotLook : MonoBehaviour
{
    private const float LookSeconds = 0.5f;
    private const string BedName = "Farm Bed";
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private readonly List<Transform> crops = new List<Transform>();
    private readonly List<Vector3> grownScales = new List<Vector3>();
    private CraftingStation station;
    private Construction construction;
    private Renderer[] bed = System.Array.Empty<Renderer>();
    private Color bedColour = Color.white;
    private MaterialPropertyBlock block;
    private float nextLook;
    private float shownGrowth = -1f;
    private int shownWet = -1;

    private void Awake()
    {
        station = GetComponent<CraftingStation>();
        construction = GetComponent<Construction>();
        Transform model = transform.Find("Model");
        if (model == null)
        {
            return;
        }

        foreach (Transform part in model)
        {
            if (part.name == BedName)
            {
                bed = part.GetComponentsInChildren<Renderer>(true);
                continue;
            }

            crops.Add(part);
            grownScales.Add(part.localScale);
        }

        if (bed.Length > 0 && bed[0].sharedMaterial != null && bed[0].sharedMaterial.HasProperty(BaseColor))
        {
            bedColour = bed[0].sharedMaterial.GetColor(BaseColor);
        }
    }

    private void Update()
    {
        if (Time.time < nextLook || station == null)
        {
            return;
        }

        nextLook = Time.time + LookSeconds;
        // A scaffold rises as it is: the field shows once the plot stands.
        if (construction != null && !construction.IsStanding)
        {
            return;
        }

        StationQueue queue = GameSession.Here.Workshops.Find(station.Id);
        float growth = queue == null ? 0f
            : queue.OutputAmount > 0 ? 1f
            : queue.Current != null ? Mathf.Clamp01(queue.Progress / Mathf.Max(1f, queue.Current.CraftSeconds)) : -1f;
        ShowCrop(growth);
        ShowSoil(queue != null && queue.Water > 0f);
    }

    // -1: nothing sown (the bed bare); 0..1 sown to grown.
    private void ShowCrop(float growth)
    {
        if (Mathf.Abs(growth - shownGrowth) < 0.01f)
        {
            return;
        }

        shownGrowth = growth;
        float scale = Mathf.Lerp(FarmSettings.Current.SownScale, 1f, Mathf.Max(0f, growth));
        for (int i = 0; i < crops.Count; i++)
        {
            if (crops[i] == null)
            {
                continue;
            }

            crops[i].gameObject.SetActive(growth >= 0f);
            crops[i].localScale = grownScales[i] * scale;
        }
    }

    private void ShowSoil(bool wet)
    {
        int now = wet ? 1 : 0;
        if (now == shownWet || bed.Length == 0)
        {
            return;
        }

        shownWet = now;
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        // Only the colour is changed (anything else on the bed's block, a wound's say, is kept).
        float darker = wet ? 1f - FarmSettings.Current.WetDarkening : 1f;
        foreach (Renderer part in bed)
        {
            if (part == null)
            {
                continue;
            }

            part.GetPropertyBlock(block);
            block.SetColor(BaseColor, new Color(bedColour.r * darker, bedColour.g * darker, bedColour.b * darker, bedColour.a));
            part.SetPropertyBlock(block);
        }
    }
}
