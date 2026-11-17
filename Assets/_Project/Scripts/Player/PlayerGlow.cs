using UnityEngine;

/// <summary>
/// A soft light around the player in the dark (the user, 2026-11-17: "we should have the player emit some light"): a pale point
/// light above their shoulders that grows with the night (NightSettings.Darkness) and is gone by day, lighting the ground a few
/// metres round them so they read from the top-down camera without a torch. It gives the fog round them a faint glow too
/// (AtmosphereLight). A lit torch or lantern is brighter and warmer; this only keeps the player from vanishing.
/// Added to the player by Player.Awake (AddTo).
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerGlow : MonoBehaviour
{
    [Tooltip("Where the light hangs above the player's feet.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.6f, 0f);
    [Tooltip("Its brightness at full dark; it fades out as day comes.")]
    [SerializeField, Min(0f)] private float intensity = 2.4f;
    [SerializeField, Min(1f)] private float range = 8.5f;
    [SerializeField] private Color colour = new Color(0.92f, 0.94f, 1f);
    [Tooltip("Darkness (0 day, 1 night) below which it is off.")]
    [SerializeField, Range(0f, 1f)] private float from = 0.2f;

    private Light glow;

    public static void AddTo(GameObject player)
    {
        if (player != null && !player.TryGetComponent(out PlayerGlow _))
        {
            player.AddComponent<PlayerGlow>();
        }
    }

    private void Awake()
    {
        var holder = new GameObject("Player Glow");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = offset;
        glow = holder.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.shadows = LightShadows.None;
        glow.range = range;
        glow.color = colour;
        glow.intensity = 0f;
        glow.enabled = false;
        // A faint glow in the fog, and no clearing of its own: the player already thins the fog round them (a visibility source).
        holder.AddComponent<AtmosphereLight>().Configure(0.35f, 0f);
    }

    private void LateUpdate()
    {
        float dark = Mathf.InverseLerp(from, 1f, NightSettings.Darkness);
        float now = intensity * dark;
        glow.intensity = now;
        glow.enabled = now > 0.01f;
    }

    private void OnDestroy()
    {
        if (glow != null)
        {
            Destroy(glow.gameObject);
        }
    }
}
