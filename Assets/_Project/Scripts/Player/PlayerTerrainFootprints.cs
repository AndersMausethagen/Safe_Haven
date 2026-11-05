using UnityEngine;

/// <summary>Routes planted feet to the terrain actually hit, never through floors, bridges or water.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(PlayerAudio))]
public sealed class PlayerTerrainFootprints : MonoBehaviour
{
    private PlayerAudio footsteps;

    private void Awake() => footsteps = GetComponent<PlayerAudio>();
    private void OnEnable()
    {
        if (footsteps == null) footsteps = GetComponent<PlayerAudio>();
        footsteps.FootPlanted += OnFootPlanted;
    }

    private void OnDisable()
    {
        if (footsteps != null) footsteps.FootPlanted -= OnFootPlanted;
    }

    public static bool TryStamp(Vector3 position, Vector3 forward) => TryStamp(position, forward, true);

    /// <param name="player">The player's prints move the footprint window with them; anyone else's only land inside it.</param>
    public static bool TryStamp(Vector3 position, Vector3 forward, bool player)
    {
        if (!Physics.Raycast(position + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit,
            0.8f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        if (!(hit.collider is TerrainCollider) || !hit.collider.TryGetComponent(out TerrainFootprints footprints))
            return false;
        if (WaterSurface.TryFind(hit.point + Vector3.up * 0.1f, 0.3f, out _))
            return false;
        if (player) footprints.Stamp(hit.point, forward);
        else footprints.StampInWindow(hit.point, forward);
        return true;
    }

    private void OnFootPlanted(Vector3 position, Vector3 forward) => TryStamp(position, forward);
}
