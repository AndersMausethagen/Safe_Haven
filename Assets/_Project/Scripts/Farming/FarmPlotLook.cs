using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A Farm Plot that shows its field (Fields): the bed bare with nothing sown, the crop rising from a sprout to full as it grows and
/// full while the harvest waits, and the soil darker while it is watered. A crop with a look of its own (FarmSettings › Crop Looks:
/// barley, flax, herbs) grows its own plant in each of the bed's places; any other shows the plot's own plants. Added by the Farm Plot
/// itself (CraftingStation); reads its queue twice a second. The crop parts are the Model's children other than the bed (named
/// "Farm Bed").
/// </summary>
[DisallowMultipleComponent]
public sealed class FarmPlotLook : MonoBehaviour
{
    private const float LookSeconds = 0.5f;
    private const string BedName = "Farm Bed";
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private readonly List<Transform> crops = new List<Transform>();
    private readonly List<Vector3> grownScales = new List<Vector3>();
    // A crop's own plants in the bed's places (FarmSettings › Crop Looks), while it is sown; empty for the plot's own.
    private readonly List<Transform> ownPlants = new List<Transform>();
    private FarmSettings.CropLook shownLook;
    private RecipeDefinition shownFor;
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
        float growth = queue == null ? -1f
            : queue.OutputAmount > 0 ? 1f
            : queue.Current != null ? Mathf.Clamp01(queue.Progress / Mathf.Max(1f, queue.Current.CraftSeconds)) : -1f;
        // What grows: the crop under way, or the one whose harvest waits.
        if (queue != null && queue.Current != null)
        {
            shownFor = queue.Current;
        }
        else if (growth < 0f)
        {
            shownFor = null;
        }

        ShowLook(FarmSettings.Current.LookFor(shownFor));
        ShowCrop(growth);
        ShowSoil(queue != null && queue.Water > 0f);
    }

    // The crop's own plants in place of the plot's (one in each of its places, turned a little each), or the plot's own again.
    private void ShowLook(FarmSettings.CropLook look)
    {
        if (look == shownLook)
        {
            return;
        }

        shownLook = look;
        shownGrowth = -2f;
        foreach (Transform plant in ownPlants)
        {
            if (plant != null)
            {
                Destroy(plant.gameObject);
            }
        }

        ownPlants.Clear();
        if (look == null)
        {
            return;
        }

        for (int i = 0; i < crops.Count; i++)
        {
            if (crops[i] == null)
            {
                continue;
            }

            crops[i].gameObject.SetActive(false);
            Transform plant = Instantiate(look.model, crops[i].parent, false).transform;
            plant.name = look.model.name + " (Crop)";
            plant.localPosition = crops[i].localPosition;
            plant.localRotation = Quaternion.Euler(0f, (i * 137.5f) % 360f, 0f);
            plant.localScale = Vector3.one * look.scale;
            ownPlants.Add(plant);
        }
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
        if (shownLook != null)
        {
            foreach (Transform plant in ownPlants)
            {
                if (plant != null)
                {
                    plant.gameObject.SetActive(growth >= 0f);
                    plant.localScale = Vector3.one * (shownLook.scale * scale);
                }
            }

            return;
        }

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

    // Wet: the bed's colour darkened, set again each look (the building's own shading, ConstructionLook, clears the block when it
    // refreshes). Dry: the block cleared once, so the bed batches as before.
    private void ShowSoil(bool wet)
    {
        int now = wet ? 1 : 0;
        if (bed.Length == 0 || (!wet && now == shownWet))
        {
            return;
        }

        shownWet = now;
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        float darker = 1f - FarmSettings.Current.WetDarkening;
        foreach (Renderer part in bed)
        {
            if (part == null)
            {
                continue;
            }

            if (!wet)
            {
                part.SetPropertyBlock(null);
                continue;
            }

            part.GetPropertyBlock(block);
            block.SetColor(BaseColor, new Color(bedColour.r * darker, bedColour.g * darker, bedColour.b * darker, bedColour.a));
            part.SetPropertyBlock(block);
        }
    }
}
