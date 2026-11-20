using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// What use and weather leave on the ground round buildings, as lasting decals on each standing building: trampled earth
/// under and round it (its yard too), damp and moss creeping up the foot of its walls, and ash round a campfire. Added
/// once a building stands (a scaffold has none yet) as children of it, so they move and go with it. Fences and walls drawn
/// in lines, and things too small to walk round, get none. Made by WorldRendering; numbers in RenderingSettings.
/// </summary>
public sealed class GroundWear : MonoBehaviour
{
    private const string WearName = "Ground Wear";
    private const float CheckEvery = 1.5f;

    private RenderingSettings settings;
    private readonly HashSet<BuildingObject> dressed = new HashSet<BuildingObject>();
    private float nextCheck;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
    }

    /// <summary>A new scene: its buildings are looked at afresh.</summary>
    public void Clear()
    {
        dressed.Clear();
        nextCheck = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck || settings.TrampledDecal == null)
        {
            return;
        }

        nextCheck = Time.unscaledTime + CheckEvery;
        dressed.RemoveWhere(building => building == null);
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building == null || dressed.Contains(building))
            {
                continue;
            }

            Construction construction = building.GetComponent<Construction>();
            if (construction != null && !construction.IsStanding)
            {
                continue;
            }

            dressed.Add(building);
            Dress(building);
        }
    }

    private void Dress(BuildingObject building)
    {
        BuildingDefinition definition = building.Definition;
        Bounds bounds = building.LocalBounds;
        if (definition == null || definition.PlaceInLines || bounds.size.x < 1.2f || bounds.size.z < 1.2f
            || building.transform.Find(WearName) != null)
        {
            return;
        }

        var root = new GameObject(WearName).transform;
        root.SetParent(building.transform, false);
        Vector3 scale = building.transform.lossyScale;
        Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        float margin = settings.TrampledMargin;

        // Trampled ground: a thin box down onto the ground round the footprint, so the building's own floors and benches stay
        // clean; its walls are spared by the angle fade.
        Add(root, settings.TrampledDecal, foot + Vector3.down * 0.1f, Quaternion.LookRotation(Vector3.down, Vector3.forward),
            new Vector3(bounds.size.x * scale.x + margin * 2f, bounds.size.z * scale.z + margin * 2f, 0.8f), 40f, 70f, true);

        if (definition.Station == CraftingStationType.Campfire && settings.ScorchDecal != null)
        {
            float across = Mathf.Max(1.6f, Mathf.Min(bounds.size.x, bounds.size.z) * 1.3f);
            Add(root, settings.ScorchDecal, foot + Vector3.down * 0.1f, Quaternion.LookRotation(Vector3.down, Vector3.forward),
                new Vector3(across, across, 0.8f), 40f, 70f, true);
        }

        // Grime up the foot of the walls: one thin slab on each side, round the wall's face, so it catches the wall and a
        // hand's width of ground at its foot (URP's angle fade needs decal layers, which the terrain can't take).
        // Only houses and halls: on a low bench or stall it would coat the whole side.
        if (settings.GrimeDecal != null && bounds.size.y > 2.2f)
        {
            float height = settings.GrimeHeight;
            Vector3 half = bounds.extents;
            for (int side = 0; side < 4; side++)
            {
                Vector3 outward = side switch { 0 => Vector3.forward, 1 => Vector3.back, 2 => Vector3.right, _ => Vector3.left };
                float along = side < 2 ? bounds.size.x : bounds.size.z;
                float reach = side < 2 ? half.z : half.x;
                Vector3 at = foot + outward * (reach + 0.02f) + Vector3.up * (height * 0.5f - 0.05f);
                Add(root, settings.GrimeDecal, at, Quaternion.LookRotation(-outward, Vector3.up),
                    new Vector3(along + 0.2f, height, 0.2f), 50f, 80f, false);
            }
        }
    }

    private static void Add(Transform root, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 size,
        float fadeStart, float fadeEnd, bool onGround)
    {
        var go = new GameObject(material.name);
        go.transform.SetParent(root, false);
        go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
        var projector = go.AddComponent<DecalProjector>();
        projector.material = material;
        projector.scaleMode = DecalScaleMode.ScaleInvariant;
        projector.size = size;
        projector.pivot = Vector3.zero;
        projector.drawDistance = 70f;
        projector.startAngleFade = fadeStart;
        projector.endAngleFade = fadeEnd;
        // Trampled ground and scorch paint only the ground (GroundDecals); the grime is for the wall, so it keeps Default.
        if (onGround)
        {
            GroundDecals.Paint(projector);
        }
    }
}
