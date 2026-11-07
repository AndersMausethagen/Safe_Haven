using UnityEngine;

/// <summary>
/// Boot prints in wet ground: each of the player's footsteps (PlayerAnimationController.Footstep) presses a print on the
/// terrain once the rain has soaked it (WeatherSystem's wetness above RenderingSettings › Footprint Wetness), left and
/// right in turn, pointing the way they walk; they fade after a while. Not in snow, where the terrain's own footprint
/// field already presses them. Made by WorldRendering.
/// </summary>
public sealed class FootprintDecals : MonoBehaviour
{
    private static readonly Vector4 LeftTile = new Vector4(0.5f, 1f, 0f, 0f);
    private static readonly Vector4 RightTile = new Vector4(0.5f, 1f, 0.5f, 0f);
    private static readonly int SnowId = Shader.PropertyToID("_SafeHavenSnow");

    private RenderingSettings settings;
    private PlayerAnimationController bound;
    private bool left;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
    }

    private void Update()
    {
        // The player is made again in every scene: follow whichever one there is.
        PlayerAnimationController current = Player.Current != null ? Player.Current.GetComponentInChildren<PlayerAnimationController>() : null;
        if (current == bound)
        {
            return;
        }

        if (bound != null)
        {
            bound.Footstep -= OnFootstep;
        }

        bound = current;
        if (bound != null)
        {
            bound.Footstep += OnFootstep;
        }
    }

    private void OnFootstep()
    {
        WeatherSystem weather = WeatherSystem.Active;
        if (settings.FootprintDecal == null || DecalPool.Current == null || bound == null || weather == null
            || weather.Wetness < settings.FootprintWetness || Shader.GetGlobalFloat(SnowId) > 0.1f)
        {
            return;
        }

        Transform body = bound.transform;
        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward) * (left ? -0.11f : 0.11f);
        Vector3 at = body.position + side + Vector3.up * 0.6f;
        if (!Physics.Raycast(at, Vector3.down, out RaycastHit hit, 1.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || !(hit.collider is TerrainCollider))
        {
            return;
        }

        DecalPool.Current.Press(settings.FootprintDecal, hit.point, hit.normal, forward, new Vector2(0.22f, 0.44f),
            left ? LeftTile : RightTile, settings.FootprintSeconds * Mathf.Lerp(0.6f, 1f, weather.Wetness));
        left = !left;
    }

    private void OnDestroy()
    {
        if (bound != null)
        {
            bound.Footstep -= OnFootstep;
        }
    }
}
