using UnityEngine;

/// <summary>
/// The player's own sounds: footsteps that match the ground and the thump of landing. A step plays when a foot plants
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

    private PlayerMovement movement;
    private PlayerAnimationController animationController;
    private Animator animator;
    private float lastEventStep = float.NegativeInfinity;
    private float lastStep = float.NegativeInfinity;
    private float distanceSinceStep;
    private float airborne;
    private readonly Foot left = new Foot();
    private readonly Foot right = new Foot();

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
        animationController = GetComponent<PlayerAnimationController>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            left.Bone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            right.Bone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
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
        Step();
    }

    private void OnLanded() => AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.Land : null, transform.position);

    // After the animator has posed the feet this frame.
    private void LateUpdate()
    {
        if (movement == null || AudioManager.Sounds == null)
        {
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
            distanceSinceStep = Mathf.Min(distanceSinceStep, AudioManager.Sounds.StrideLength * 0.5f);
            return;
        }

        distanceSinceStep += movement.HorizontalSpeed * Time.deltaTime;
        if (distanceSinceStep >= AudioManager.Sounds.StrideLength)
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
                Step();
            }

            foot.Peak = height;
        }

        foot.Falling = falling;
        foot.Height = height;
    }

    private void Step()
    {
        lastStep = Time.time;
        AudioManager.Play(SurfaceSound(transform.position), transform.position);
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
            if (hit.collider is TerrainCollider && hit.collider.TryGetComponent(out Terrain terrain))
            {
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
        if (data == null || data.detailPrototypes.Length == 0 || data.detailResolution <= 0)
        {
            return 0;
        }

        Vector3 local = point - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.detailWidth), 0, data.detailWidth - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.detailHeight), 0, data.detailHeight - 1);
        int total = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
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

        TerrainLayer layer = best < data.terrainLayers.Length ? data.terrainLayers[best] : null;
        return layer != null ? layer.name : null;
    }
}
