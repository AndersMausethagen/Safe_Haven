using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The player carries the fight (2026-11-14): blood on the chest and arms from blows taken and dealt up close, mud up the
/// legs from wet ground and rolls through it, and blood on the blade from what it cut, in that creature's colour. Rain
/// slowly washes it off; wading or swimming washes it off fast. Drawn as small decals that ride on the body's bones and on
/// the held weapon, projected from the side so the ground under them isn't painted. Added by Player.
/// </summary>
[DisallowMultipleComponent]
public sealed class CarriedGrime : MonoBehaviour
{
    private const float Tick = 0.2f;

    private sealed class Spot
    {
        public DecalProjector Projector;
        public bool Mud;
        public float Weight;
    }

    private readonly List<Spot> spots = new List<Spot>();
    private Animator animator;
    private Damageable health;
    private Combatant combatant;
    private HeldItemView held;
    private DecalProjector blade;
    private GameObject bladeModel;
    private Color bladeColour = new Color(0.35f, 0.02f, 0.02f, 1f);
    private float body;
    private float legs;
    private float weapon;
    private float nextTick;
    private Vector3 lastPosition;
    private bool built;

    /// <summary>Blood on the body, mud on the legs, blood on the blade: each 0-1.</summary>
    public Vector3 Amounts => new Vector3(body, legs, weapon);

    public static void AddTo(GameObject player)
    {
        if (!player.TryGetComponent(out CarriedGrime _))
        {
            player.AddComponent<CarriedGrime>();
        }
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Damageable>();
        combatant = GetComponent<Combatant>();
        held = GetComponentInChildren<HeldItemView>();
        lastPosition = transform.position;
    }

    private void OnEnable()
    {
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    private void OnDisable()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
    }

    /// <summary>Adds to it (a cheat, a cut-scene): blood on the body and blade, mud on the legs.</summary>
    public void Add(float blood, float mud, float blade)
    {
        body = Mathf.Clamp01(body + blood);
        legs = Mathf.Clamp01(legs + mud);
        weapon = Mathf.Clamp01(weapon + blade);
    }

    // Blows taken bloody the body; blows dealt up close spatter it and bloody the blade.
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (result.Damage <= 0f || damage.IsOverTime || !GameOptions.Blood.On)
        {
            return;
        }

        WoundSettings settings = WoundSettings.Current;
        if (victim == health)
        {
            if (!result.Blocked)
            {
                body = Mathf.Clamp01(body + settings.BloodPerHitTaken * Mathf.Clamp(result.Damage / Mathf.Max(1f, health.MaxHealth) * 5f, 0.3f, 2f));
            }

            return;
        }

        if (damage.Source != combatant || !victim.TryGetComponent(out MonsterAI monster) || monster.Definition == null || !monster.Definition.Bleeds)
        {
            return;
        }

        bool close = (victim.transform.position - transform.position).sqrMagnitude < 9f;
        if (!close)
        {
            return;
        }

        weapon = Mathf.Clamp01(weapon + settings.BloodPerHitDealt);
        body = Mathf.Clamp01(body + settings.BloodPerHitDealt * (result.Killed ? 1.5f : 0.3f));
        if (bladeColour != monster.Definition.BloodColor)
        {
            bladeColour = monster.Definition.BloodColor;
            if (blade != null)
            {
                blade.material = BloodDecals.MaterialFor(bladeColour);
            }
        }
    }

    private void Update()
    {
        if (Time.time < nextTick)
        {
            return;
        }

        nextTick = Time.time + Tick;
        if (!built)
        {
            Build();
        }

        Weather(Tick);
        Walk(Tick);
        Show();
    }

    private void Weather(float seconds)
    {
        WoundSettings settings = WoundSettings.Current;
        float wash = 0f;
        WeatherSystem weather = WeatherSystem.Active;
        if (weather != null)
        {
            wash = Mathf.Max(wash, weather.Wetness * settings.RainWash);
        }

        if (WaterSurface.At(transform.position + Vector3.up * 0.3f, 0.2f, out float height) != null && height > transform.position.y + 0.25f)
        {
            wash = settings.WaterWash;
        }

        if (wash <= 0f)
        {
            return;
        }

        body = Mathf.Max(0f, body - wash * seconds);
        legs = Mathf.Max(0f, legs - wash * seconds);
        weapon = Mathf.Max(0f, weapon - wash * seconds);
    }

    // Walking wet ground or mud splashes the legs, more the faster; a roll through it, a lot at once.
    private void Walk(float seconds)
    {
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        moved.y = 0f;
        float speed = moved.magnitude / seconds;
        if (speed < 0.8f || speed > 20f)
        {
            return;
        }

        GroundImpactBurst.Ground ground = GroundImpacts.SurfaceAt(transform.position, out bool water);
        if (water || (!ground.Wet && (WeatherSystem.Active == null || WeatherSystem.Active.Wetness < 0.3f)))
        {
            return;
        }

        float mud = WoundSettings.Current.MudPerMetre * moved.magnitude * (ground.Wet ? 1.5f : 0.6f) * (speed > 7f ? 3f : 1f);
        legs = Mathf.Clamp01(legs + mud);
    }

    private void Show()
    {
        foreach (Spot spot in spots)
        {
            float amount = (spot.Mud ? legs : body) * spot.Weight;
            spot.Projector.fadeFactor = Mathf.Clamp01(amount);
            spot.Projector.enabled = amount > 0.02f;
        }

        GameObject model = held != null ? held.ShownModel : null;
        if (model != bladeModel)
        {
            BuildBlade(model);
        }

        if (blade != null)
        {
            blade.fadeFactor = Mathf.Clamp01(weapon);
            blade.enabled = weapon > 0.02f && bladeModel != null && bladeModel.activeInHierarchy;
        }
    }

    // Blood on the chest front and back and on the forearms; mud on the shins, front and back.
    private void Build()
    {
        built = true;
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        Material blood = BloodDecals.MaterialFor(WoundSettings.Current.PlayerBlood);
        // Mud: the splatter shapes in the colour of wet earth, unless a mud decal of its own is set.
        Material mud = WoundSettings.Current.MudDecal != null ? WoundSettings.Current.MudDecal : BloodDecals.MaterialFor(WoundSettings.Current.Mud);
        Vector3 forward = transform.forward, right = transform.right;
        Add(HumanBodyBones.Chest, blood, false, forward, new Vector3(0.36f, 0.4f, 0.5f), 1f);
        Add(HumanBodyBones.Chest, blood, false, -forward, new Vector3(0.36f, 0.4f, 0.5f), 0.6f);
        Add(HumanBodyBones.LeftLowerArm, blood, false, -right, new Vector3(0.22f, 0.28f, 0.3f), 0.9f);
        Add(HumanBodyBones.RightLowerArm, blood, false, right, new Vector3(0.22f, 0.28f, 0.3f), 1f);
        Add(HumanBodyBones.LeftLowerLeg, mud, true, forward, new Vector3(0.2f, 0.45f, 0.35f), 1f);
        Add(HumanBodyBones.RightLowerLeg, mud, true, forward, new Vector3(0.2f, 0.45f, 0.35f), 1f);
        Add(HumanBodyBones.LeftLowerLeg, mud, true, -forward, new Vector3(0.2f, 0.45f, 0.35f), 0.8f);
        Add(HumanBodyBones.RightLowerLeg, mud, true, -forward, new Vector3(0.2f, 0.45f, 0.35f), 0.8f);
    }

    // A projector on <bone>, seen from <from> (the side it faces out to), painting back onto the body.
    private void Add(HumanBodyBones boneName, Material material, bool isMud, Vector3 from, Vector3 size, float weight)
    {
        Transform bone = animator.GetBoneTransform(boneName);
        if (bone == null || material == null)
        {
            return;
        }

        var go = new GameObject((isMud ? "Mud " : "Blood ") + boneName);
        // A limb's spot sits halfway down it (a bone starts at its joint): the shin between knee and ankle, the forearm
        // between elbow and wrist.
        Transform next = boneName == HumanBodyBones.LeftLowerLeg ? animator.GetBoneTransform(HumanBodyBones.LeftFoot)
            : boneName == HumanBodyBones.RightLowerLeg ? animator.GetBoneTransform(HumanBodyBones.RightFoot)
            : boneName == HumanBodyBones.LeftLowerArm ? animator.GetBoneTransform(HumanBodyBones.LeftHand)
            : boneName == HumanBodyBones.RightLowerArm ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        Vector3 centre = next != null ? Vector3.Lerp(bone.position, next.position, isMud ? 0.6f : 0.5f)
            : bone.position + (boneName == HumanBodyBones.Chest ? Vector3.up * 0.08f : Vector3.zero);
        go.transform.SetPositionAndRotation(centre + from * (size.z * 0.5f), Quaternion.LookRotation(-from, Vector3.up));
        go.transform.SetParent(bone, true);
        DecalProjector projector = go.AddComponent<DecalProjector>();
        projector.material = material;
        projector.size = size;
        projector.pivot = new Vector3(0f, 0f, size.z * 0.5f);
        int tile = Random.Range(0, 4);
        bool atlas = !(isMud && WoundSettings.Current.MudDecal != null);
        projector.uvScale = atlas ? new Vector2(0.5f, 0.5f) : Vector2.one;
        projector.uvBias = atlas ? new Vector2((tile % 2) * 0.5f, (tile / 2) * 0.5f) : Vector2.zero;
        projector.startAngleFade = 50f;
        projector.endAngleFade = 80f;
        projector.fadeFactor = 0f;
        projector.enabled = false;
        spots.Add(new Spot { Projector = projector, Mud = isMud, Weight = weight });
    }

    // The blood on a blade: the half toward its tip, painted across its flat.
    private void BuildBlade(GameObject model)
    {
        bladeModel = model;
        if (blade != null)
        {
            Destroy(blade.gameObject);
            blade = null;
        }

        if (model == null || held == null || !HeldWeaponShape.Measure(model, held.Grip, out Vector3 tip, out Vector3 butt))
        {
            return;
        }

        Material material = BloodDecals.MaterialFor(bladeColour);
        if (material == null)
        {
            return;
        }

        // The model's thinnest direction is across the blade's flat; paint along it.
        Bounds bounds = LocalBounds(model);
        Vector3 along = (tip - butt).normalized;
        Vector3 across = bounds.size.x <= bounds.size.y && bounds.size.x <= bounds.size.z ? Vector3.right
            : bounds.size.y <= bounds.size.z ? Vector3.up : Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(across, along)) > 0.9f)
        {
            across = Vector3.Cross(along, Vector3.up).sqrMagnitude > 1e-3f ? Vector3.Cross(along, Vector3.up).normalized : Vector3.right;
        }

        float length = Vector3.Distance(tip, butt);
        var go = new GameObject("Blood on the Blade");
        go.transform.SetParent(model.transform, false);
        go.transform.localPosition = Vector3.Lerp(butt, tip, 0.7f);
        go.transform.localRotation = Quaternion.LookRotation(across, along);
        blade = go.AddComponent<DecalProjector>();
        blade.material = material;
        float width = Mathf.Max(0.08f, Mathf.Min(bounds.size.x + bounds.size.y + bounds.size.z - length - Mathf.Min(bounds.size.x, Mathf.Min(bounds.size.y, bounds.size.z)), 0.4f));
        blade.size = new Vector3(width * 1.2f, length * 0.6f, 0.3f);
        blade.pivot = Vector3.zero;
        blade.uvScale = new Vector2(0.5f, 0.5f);
        blade.uvBias = new Vector2(0.5f, 0f);
        blade.fadeFactor = 0f;
        blade.enabled = false;
    }

    private static Bounds LocalBounds(GameObject model)
    {
        var bounds = new Bounds();
        bool found = false;
        Matrix4x4 toModel = model.transform.worldToLocalMatrix;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 matrix = toModel * filter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 point = matrix.MultiplyPoint3x4(local.center + offset);
                if (!found)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return bounds;
    }
}
