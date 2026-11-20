using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wears the weapon the player put away (PlayerEquipment.Stowed: Z, or a two-handed weapon put away for the torch) on the
/// body instead of nowhere: two-handed weapons, polearms, staffs and bows across the back, a one-handed weapon at the hip
/// (the user, 2026-11-15). The model is the item's held model, a visual copy; it is placed so that, whatever way the model
/// was made, it runs from its grip along the settings' tip direction with its flat against the body (StowedWeaponSettings).
/// Added to the local player by PlayerInputHandler.
/// </summary>
[DisallowMultipleComponent]
public sealed class StowedWeaponView : MonoBehaviour
{
    // Often enough that the worn model changes over with the hand's grab (PlayerEquipment.StowedShown).
    private const float CheckEvery = 0.05f;

    private PlayerEquipment equipment;
    private Animator animator;
    private ItemDefinition shown;
    private GameObject model;
    private float nextCheck;
    private readonly Dictionary<ItemDefinition, GameObject> made = new Dictionary<ItemDefinition, GameObject>();

    /// <summary>The weapon worn on the body now (null when nothing is put away).</summary>
    public ItemDefinition Shown => shown;

    /// <summary>The worn model (null when nothing is shown).</summary>
    public GameObject Model => model;

    /// <summary>Adds the view to a player (once).</summary>
    public static StowedWeaponView For(GameObject player)
    {
        if (player == null)
        {
            return null;
        }

        return player.TryGetComponent(out StowedWeaponView view) ? view : player.AddComponent<StowedWeaponView>();
    }

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck || equipment == null)
        {
            return;
        }

        nextCheck = Time.unscaledTime + CheckEvery;
        ItemDefinition wanted = equipment.StowedShown;
        if (wanted != null && StowedWeaponSettings.WornOnBack(wanted.Weapon) == null)
        {
            wanted = null;
        }

        if (wanted == shown)
        {
            return;
        }

        if (model != null)
        {
            model.SetActive(false);
            model = null;
        }

        shown = wanted;
        if (wanted != null)
        {
            model = ModelFor(wanted);
        }
    }

    private GameObject ModelFor(ItemDefinition item)
    {
        if (made.TryGetValue(item, out GameObject existing) && existing != null)
        {
            existing.SetActive(true);
            return existing;
        }

        GameObject worn = Wear(item, animator, transform, StowedWeaponSettings.Current);
        made[item] = worn;
        return worn;
    }

    /// <summary>
    /// A visual copy of <paramref name="item"/>'s held model on <paramref name="animator"/>'s back or hip bone, the grip at
    /// the settings' place and the length along their tip direction (directions in <paramref name="character"/>'s frame, as
    /// it stands now). Null when the item has no model, isn't worn, or the rig has no such bone.
    /// </summary>
    public static GameObject Wear(ItemDefinition item, Animator animator, Transform character, StowedWeaponSettings settings)
    {
        bool? back = item != null ? StowedWeaponSettings.WornOnBack(item.Weapon) : null;
        if (back == null || item.HeldModel == null || animator == null || !animator.isHuman)
        {
            return null;
        }

        Transform bone = animator.GetBoneTransform(back.Value ? settings.BackBone : settings.HipBone);
        if (bone == null && back.Value)
        {
            bone = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        if (bone == null)
        {
            return null;
        }

        // The model as it sits in the hand, under a grip of its own: the grip is the handle, so its far end is the tip.
        var grip = new GameObject(item.name + " (stowed)").transform;
        grip.SetParent(bone, false);
        GameObject held = HeldItemView.CreateModel(item, grip);
        if (held == null)
        {
            Destroy(grip.gameObject);
            return null;
        }

        Bounds local = BoundsIn(grip, held);
        // The length and the flat from the biggest mesh's own box (models are made along their own axes; a box taken in the
        // grip's space is skewed for one held at a slant, a bow), the length signed towards its end farther from the grip.
        LengthAndFlat(grip, held, local, out Vector3 tipLocal, out Vector3 flatLocal);
        if (tipLocal.sqrMagnitude < 1e-6f)
        {
            tipLocal = Vector3.up;
        }

        Vector3 tipWanted = character.TransformDirection((back.Value ? settings.BackTipDirection : settings.HipTipDirection).normalized);
        Vector3 flatWanted = character.TransformDirection((back.Value ? settings.BackFlatNormal : settings.HipFlatNormal).normalized);
        // Turn the grip so the model's length runs along the wanted way and its flat faces out from the body.
        Quaternion modelFrame = Quaternion.LookRotation(tipLocal.normalized, Vector3.ProjectOnPlane(flatLocal, tipLocal).sqrMagnitude > 1e-6f
            ? Vector3.ProjectOnPlane(flatLocal, tipLocal).normalized : Perpendicular(tipLocal));
        Quaternion wantedFrame = Quaternion.LookRotation(tipWanted, Vector3.ProjectOnPlane(flatWanted, tipWanted).sqrMagnitude > 1e-6f
            ? Vector3.ProjectOnPlane(flatWanted, tipWanted).normalized : Perpendicular(tipWanted));
        grip.rotation = wantedFrame * Quaternion.Inverse(modelFrame);
        if (back.Value)
        {
            // Its middle on the middle of the back.
            grip.position = bone.position + character.TransformVector(settings.BackCentre);
            grip.position += grip.position - grip.TransformPoint(local.center);
        }
        else
        {
            grip.position = bone.position + character.TransformVector(settings.HipGrip);
        }

        // Kept against the bone as it is at rest, not as it is posed now: put away mid-run, mid-roll or on a horse, the place
        // worked out above would have been tilted by that pose, and the model is kept for good (review 2026-11-16).
        Quaternion rest = character.rotation * RestRelativeTo(animator, bone, character);
        float scale = Mathf.Abs(bone.lossyScale.x) > 1e-6f ? bone.lossyScale.x : 1f;
        Vector3 offset = grip.position - bone.position;
        Quaternion placed = grip.rotation;
        grip.localRotation = Quaternion.Inverse(rest) * placed;
        grip.localPosition = Quaternion.Inverse(rest) * offset / scale;
        return grip.gameObject;
    }

    // The bone's rotation at rest relative to the character, from the avatar's skeleton (its rest pose); a bone the skeleton
    // doesn't name keeps its rotation now.
    private static Quaternion RestRelativeTo(Animator animator, Transform bone, Transform character)
    {
        var rest = new Dictionary<string, Quaternion>();
        if (animator.avatar != null)
        {
            foreach (SkeletonBone entry in animator.avatar.humanDescription.skeleton)
            {
                rest[entry.name] = entry.rotation;
            }
        }

        Quaternion relative = Quaternion.identity;
        for (Transform t = bone; t != null && t != character; t = t.parent)
        {
            if (t == animator.transform && t != character)
            {
                // The animator's own object stands as it is placed under the character.
                relative = t.localRotation * relative;
                continue;
            }

            relative = (rest.TryGetValue(t.name, out Quaternion local) ? local : t.localRotation) * relative;
        }

        return relative;
    }

    // The model's bounds in the grip's space, from every mesh's own bounds.
    private static Bounds BoundsIn(Transform space, GameObject model)
    {
        bool any = false;
        var bounds = new Bounds();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Bounds mesh = filter.sharedMesh.bounds;
            Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = toSpace.MultiplyPoint3x4(corner);
                if (!any)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return bounds;
    }

    private static void LengthAndFlat(Transform space, GameObject model, Bounds fallback, out Vector3 length, out Vector3 flat)
    {
        MeshFilter biggest = null;
        float most = 0f;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.lossyScale);
            float volume = size.x * size.y + size.y * size.z + size.x * size.z;
            if (volume > most)
            {
                most = volume;
                biggest = filter;
            }
        }

        if (biggest == null)
        {
            length = FarthestCorner(fallback);
            flat = ThinnestAxis(fallback);
            return;
        }

        Bounds own = biggest.sharedMesh.bounds;
        Vector3 scaled = Vector3.Scale(own.size, biggest.transform.lossyScale);
        int longAxis = scaled.x >= scaled.y && scaled.x >= scaled.z ? 0 : scaled.y >= scaled.z ? 1 : 2;
        int thinAxis = scaled.x <= scaled.y && scaled.x <= scaled.z ? 0 : scaled.y <= scaled.z ? 1 : 2;
        Matrix4x4 toSpace = space.worldToLocalMatrix * biggest.transform.localToWorldMatrix;
        Vector3 axis = Vector3.zero;
        axis[longAxis] = 1f;
        Vector3 thin = Vector3.zero;
        thin[thinAxis] = 1f;
        Vector3 endA = toSpace.MultiplyPoint3x4(own.center + Vector3.Scale(own.extents, axis));
        Vector3 endB = toSpace.MultiplyPoint3x4(own.center - Vector3.Scale(own.extents, axis));
        length = (endA.sqrMagnitude >= endB.sqrMagnitude ? endA - endB : endB - endA).normalized;
        flat = toSpace.MultiplyVector(thin).normalized;
    }

    // From the grip (the origin) to the far end of the model along its longest side.
    private static Vector3 FarthestCorner(Bounds bounds)
    {
        Vector3 size = bounds.size;
        Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
        float low = Vector3.Dot(bounds.min, axis), high = Vector3.Dot(bounds.max, axis);
        return Mathf.Abs(high) >= Mathf.Abs(low) ? axis : -axis;
    }

    private static Vector3 ThinnestAxis(Bounds bounds)
    {
        Vector3 size = bounds.size;
        return size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
    }

    private static Vector3 Perpendicular(Vector3 v) => Vector3.Cross(v, Mathf.Abs(v.y) < 0.9f ? Vector3.up : Vector3.right).normalized;

    private void OnDestroy()
    {
        foreach (GameObject worn in made.Values)
        {
            if (worn != null)
            {
                Destroy(worn);
            }
        }
    }
}
