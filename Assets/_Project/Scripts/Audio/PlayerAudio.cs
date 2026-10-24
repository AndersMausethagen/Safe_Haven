using UnityEngine;

/// <summary>
/// The player's own sounds: footsteps that match the ground (the terrain layer underfoot by name, wood on buildings,
/// splashes in water; see Audio Settings › Footsteps) and the thump of landing. Steps follow the animation's footstep
/// events; if the clips have none, one plays every stride while moving. AudioManager adds it to the player in every
/// scene, so the prefab needs no setup.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAudio : MonoBehaviour
{
    // Without footstep events for this long while moving, steps are timed by distance instead.
    private const float EventTimeout = 1.2f;
    private const float MinimumMovingSpeed = 0.6f;
    private const float LandAfterAirborne = 0.35f;

    private PlayerMovement movement;
    private PlayerAnimationController animationController;
    private float lastEventStep = float.NegativeInfinity;
    private float distanceSinceStep;
    private float airborne;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        animationController = GetComponent<PlayerAnimationController>();
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

    private void Update()
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
        float speed = movement.HorizontalSpeed;
        if (speed < MinimumMovingSpeed || movement.IsDashing || Time.time - lastEventStep < EventTimeout)
        {
            distanceSinceStep = Mathf.Min(distanceSinceStep, AudioManager.Sounds.StrideLength * 0.5f);
            return;
        }

        distanceSinceStep += speed * Time.deltaTime;
        if (distanceSinceStep >= AudioManager.Sounds.StrideLength)
        {
            distanceSinceStep = 0f;
            Step();
        }
    }

    private void Step()
    {
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
                return sounds.FootstepOn(LayerUnder(terrain, hit.point));
            }

            if (sounds.FootstepWood != null && hit.collider.GetComponentInParent<BuildingObject>() != null)
            {
                return sounds.FootstepWood;
            }
        }

        return sounds.Footstep;
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
