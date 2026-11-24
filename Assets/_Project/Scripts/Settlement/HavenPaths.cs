using UnityEngine;

/// <summary>
/// What drawn paths do at home (the user, 2026-11-15: "paths speed up the player at home"): standing on one, the player walks
/// faster by its kind's Path Speed (a dirt path 10%, a stone path 15%), and so do the residents on their way. Looked at a few
/// times a second against the path pieces' footprints, never per frame. Added to the portal with HavenHomes.
/// </summary>
[DisallowMultipleComponent]
public sealed class HavenPaths : MonoBehaviour, LeaseWatch.ILastingHold
{
    private const float LookSeconds = 0.25f;

    private float nextLook;
    private PlayerMovement walking;
    private float shown = 1f;

    /// <summary>How much faster one walks at <paramref name="point"/>: the best path under it, else 1.</summary>
    public static float SpeedAt(Vector3 point)
    {
        float best = 1f;
        foreach (PathPiece piece in PathPiece.All)
        {
            if (piece.Speed > best && piece.Covers(point))
            {
                best = piece.Speed;
            }
        }

        return best;
    }

    /// <summary>True when a path piece lies under <paramref name="point"/>.</summary>
    public static bool OnPath(Vector3 point, bool pavedOnly = false)
    {
        foreach (PathPiece piece in PathPiece.All)
        {
            if ((!pavedOnly || piece.Paved) && piece.Covers(point))
            {
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextLook)
        {
            return;
        }

        nextLook = Time.unscaledTime + LookSeconds;
        Player player = Player.Current;
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (movement != walking)
        {
            Let(walking);
            walking = movement;
        }

        if (walking == null)
        {
            return;
        }

        // Only at home, in the Haven's land: the paths are the Haven's.
        bool home = HomePortal.Current != null && (!HomeLifeSettings.Current.PathsOnlyAtHome || HomePortal.Current.InBuildLand(walking.transform.position));
        float speed = home ? SpeedAt(walking.transform.position) : 1f;
        if (!Mathf.Approximately(speed, shown))
        {
            shown = speed;
            walking.SetSpeedScale(this, speed);
        }
    }

    private void Let(PlayerMovement movement)
    {
        if (movement != null)
        {
            movement.SetSpeedScale(this, 1f);
        }

        shown = 1f;
    }

    private void OnDisable() => Let(walking);
}
