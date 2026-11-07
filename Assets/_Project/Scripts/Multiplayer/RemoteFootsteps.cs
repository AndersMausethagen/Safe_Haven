using UnityEngine;

/// <summary>
/// A friend's footsteps in co-op: one every stride of ground their body covers, with the ground's own sound
/// (<see cref="PlayerAudio.SurfaceSound"/>: grass, snow, water, ice, wood), so a friend is heard coming as the player is.
/// Counted by distance, not from the feet: a friend's body isn't animated while off-screen, which is when hearing them
/// matters most. Added by <see cref="RemotePlayerBody"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class RemoteFootsteps : MonoBehaviour
{
    private const float MinimumMovingSpeed = 0.6f;
    // Faster than any run is the body being put right or taken somewhere, not steps.
    private const float MaximumStepSpeed = 12f;

    private Vector3 last;
    private bool placed;
    private float distance;

    private static float Stride => AudioManager.Sounds != null ? AudioManager.Sounds.StrideLength : 0.7f;

    private void OnEnable() => placed = false;

    private void LateUpdate()
    {
        Vector3 now = transform.position;
        if (!placed)
        {
            last = now;
            placed = true;
            return;
        }

        Vector3 moved = now - last;
        moved.y = 0f;
        last = now;
        float metres = moved.magnitude;
        float speed = Time.deltaTime > 0f ? metres / Time.deltaTime : 0f;
        if (speed < MinimumMovingSpeed || speed > MaximumStepSpeed)
        {
            distance = Mathf.Min(distance, Stride * 0.5f);
            return;
        }

        distance += metres;
        if (distance >= Stride)
        {
            distance = 0f;
            AudioManager.Play(PlayerAudio.SurfaceSound(now), now);
        }
    }
}
