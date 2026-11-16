using UnityEngine;

/// <summary>
/// The player's own sounds: footsteps that match the ground, the thump of landing and splashes while swimming (the water
/// footstep, once per stroke's worth of distance; treading water is quiet). A step plays when a foot plants
/// in the animation (it reaches its lowest point near the ground), so steps stay in time with whatever body and clip is
/// playing; clips with Footstep events use those instead, and without a humanoid rig one plays every stride. The ground
/// decides the sound (Audio Settings › Footsteps): water, grass where grass and plants grow on the terrain, the ground
/// layer's name, wood on buildings. AudioManager adds it to the player in every scene, so the prefab needs no setup.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAudio : MonoBehaviour
{
    // Without footstep events for this long while moving, steps come from the feet (or distance) instead.
    private const float EventTimeout = 1.2f;
    private const float MinimumMovingSpeed = 0.6f;
    private const float LandAfterAirborne = 0.35f;
    // A foot counts as planted at its lowest point, if that is this close to the ground (metres above the player's feet).
    private const float PlantHeight = 0.2f;
    private const float MinimumStepGap = 0.25f;
    // A plant only counts after the foot has come down at least this far from its last high point (a lifted step).
    private const float MinimumFall = 0.05f;
    // Metres swum per splash.
    private const float StrokeLength = 1.1f;

    private PlayerMovement movement;
    private PlayerSwimming swimming;
    private WaterInteractor water;
    private PlayerAnimationController animationController;
    private Animator animator;
    private float lastEventStep = float.NegativeInfinity;
    private float lastStep = float.NegativeInfinity;
    private float distanceSinceStep;
    private float airborne;
    private readonly Foot left = new Foot();
    private readonly Foot right = new Foot();
    private bool fallbackLeft;

    /// <summary>A grounded moving foot contact, shared by sound and terrain impressions.</summary>
    public event System.Action<Vector3, Vector3> FootPlanted;

    private sealed class Foot
    {
        public Transform Bone;
        public float Height;
        public bool Falling;
        public float Peak;
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        swimming = GetComponent<PlayerSwimming>();
        water = GetComponent<WaterInteractor>();
        animationController = GetComponent<PlayerAnimationController>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            left.Bone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            right.Bone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        if (!TryGetComponent(out PlayerTerrainFootprints _))
            gameObject.AddComponent<PlayerTerrainFootprints>();
    }

    private void OnEnable()
    {
        if (animationController != null)
        {
            animationController.Footstep += OnFootstepEvent;
            animationController.Landed += OnLanded;
        }
    }

    private void OnDisable()
    {
        if (animationController != null)
        {
            animationController.Footstep -= OnFootstepEvent;
            animationController.Landed -= OnLanded;
        }
    }

    private void OnFootstepEvent()
    {
        lastEventStep = Time.time;
        Step(left.Bone != null && right.Bone != null
            ? (left.Bone.position.y <= right.Bone.position.y ? left.Bone : right.Bone) : null);
    }

    private void OnLanded() => AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.Land : null, transform.position);

    // After the animator has posed the feet this frame.
    private void LateUpdate()
    {
        if (movement == null)
        {
            return;
        }

        // Floating isn't falling: standing up in the shallows makes no landing thump.
        if (swimming != null && swimming.IsSwimming)
        {
            airborne = 0f;
            Swim();
            return;
        }

        if (!movement.IsGrounded)
        {
            airborne += Time.deltaTime;
            return;
        }

        if (airborne >= LandAfterAirborne && Time.time - lastEventStep > EventTimeout)
        {
            OnLanded();
        }

        airborne = 0f;
        bool walking = movement.HorizontalSpeed >= MinimumMovingSpeed && !movement.IsDashing;
        bool eventsDrive = Time.time - lastEventStep < EventTimeout;
        if (left.Bone != null && right.Bone != null)
        {
            Plant(left, walking && !eventsDrive);
            Plant(right, walking && !eventsDrive);
            return;
        }

        if (!walking || eventsDrive)
        {
            distanceSinceStep = Mathf.Min(distanceSinceStep, StrideLength * 0.5f);
            return;
        }

        distanceSinceStep += movement.HorizontalSpeed * Time.deltaTime;
        if (distanceSinceStep >= StrideLength)
        {
            distanceSinceStep = 0f;
            Step();
        }
    }

    // A step at the bottom of a foot's fall: it was coming down and has stopped, near the ground.
    private void Plant(Foot foot, bool counts)
    {
        float height = foot.Bone.position.y - transform.position.y;
        bool falling = height < foot.Height - 0.0005f;
        foot.Peak = Mathf.Max(foot.Peak, height);
        if (foot.Falling && !falling)
        {
            // The bottom of a fall: a step if the foot was really lifted and came down near the ground.
            if (counts && foot.Peak - height >= MinimumFall && height < PlantHeight && Time.time - lastStep >= MinimumStepGap)
            {
                Step(foot.Bone);
            }

            foot.Peak = height;
        }

        foot.Falling = falling;
        foot.Height = height;
    }

    private void Swim()
    {
        distanceSinceStep += movement.HorizontalSpeed * Time.deltaTime;
        if (distanceSinceStep >= StrokeLength)
        {
            distanceSinceStep = 0f;
            Vector3 at = new Vector3(transform.position.x, swimming.SurfaceHeight, transform.position.z);
            AudioManager.Play(SurfaceSound(at), at);
        }
    }

    private float StrideLength => AudioManager.Sounds != null ? AudioManager.Sounds.StrideLength : 0.7f;

    private void Step(Transform foot = null)
    {
        lastStep = Time.time;
        Vector3 position = foot != null ? foot.position : transform.position;
        AudioManager.Play(SurfaceSound(position), position);
        water?.SplashFootstep(position);
        if (movement != null && movement.IsGrounded && !movement.IsDashing && movement.HorizontalSpeed >= MinimumMovingSpeed)
        {
            fallbackLeft = !fallbackLeft;
            Vector3 plantedAt = foot != null ? position : position + transform.right * (fallbackLeft ? -0.1f : 0.1f);
            FootPlanted?.Invoke(plantedAt, transform.forward);
        }
    }

    /// <summary>The footstep for the ground at <paramref name="position"/>.</summary>
    public static SoundDefinition SurfaceSound(Vector3 position)
    {
        AudioSettingsDefinition sounds = AudioManager.Sounds;
        if (sounds == null)
        {
            return null;
        }

        if (sounds.FootstepWater != null && WaterSurface.TryFind(position + Vector3.up * 0.1f, 0.3f, out _))
        {
            return sounds.FootstepWater;
        }

        if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (sounds.FootstepIce != null && hit.collider.GetComponentInParent<IceSheet>() != null)
            {
                return sounds.FootstepIce;
            }

            if (hit.collider is TerrainCollider && hit.collider.TryGetComponent(out Terrain terrain))
            {
                WeatherSystem weather = WeatherSystem.Active;
                if (sounds.FootstepSnow != null && weather != null && weather.Snow >= sounds.SnowForSnowSteps)
                {
                    return sounds.FootstepSnow;
                }

                if (sounds.FootstepWater != null && TerrainPuddles.Contains(terrain, hit.point))
                {
                    return sounds.FootstepWater;
                }

                if (sounds.FootstepGrass != null && GrassUnder(terrain, hit.point) >= sounds.GrassForGrassSteps)
                {
                    return sounds.FootstepGrass;
                }

                return sounds.FootstepOn(LayerUnder(terrain, hit.point));
            }

            if (sounds.FootstepWood != null && hit.collider.GetComponentInParent<BuildingObject>() != null)
            {
                return sounds.FootstepWood;
            }
        }

        return sounds.Footstep;
    }

    /// <summary>How much grass and plant cover the terrain has at <paramref name="point"/>: detail instances in its cell.</summary>
    public static int GrassUnder(Terrain terrain, Vector3 point)
    {
        TerrainData data = terrain.terrainData;
        // detailPrototypes makes a new array of class objects each time it is read: once a step, not once a layer.
        int layers = data != null ? data.detailPrototypes.Length : 0;
        if (layers == 0 || data.detailResolution <= 0)
        {
            return 0;
        }

        Vector3 local = point - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.detailWidth), 0, data.detailWidth - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.detailHeight), 0, data.detailHeight - 1);
        int total = 0;
        for (int layer = 0; layer < layers; layer++)
        {
            total += data.GetDetailLayer(x, z, 1, 1, layer)[0, 0];
        }

        return total;
    }

    // The name of the strongest painted layer under the point.
    private static string LayerUnder(Terrain terrain, Vector3 point)
    {
        TerrainData data = terrain.terrainData;
        if (data == null || data.alphamapLayers == 0)
        {
            return null;
        }

        Vector3 local = point - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
        float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
        int best = 0;
        for (int i = 1; i < data.alphamapLayers; i++)
        {
            if (weights[0, 0, i] > weights[0, 0, best])
            {
                best = i;
            }
        }

        TerrainLayer[] painted = data.terrainLayers;
        TerrainLayer layer = best < painted.Length ? painted[best] : null;
        return layer != null ? layer.name : null;
    }
}
