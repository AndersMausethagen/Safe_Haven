using UnityEngine;

/// <summary>
/// A resident out and about after dark carries a little light (a lantern, a lit splinter): the Haven at night is people
/// moving between warm windows, not an empty dark. Off once they are in bed. Added by ResidentNpc.
/// </summary>
[DisallowMultipleComponent]
public sealed class ResidentLantern : MonoBehaviour
{
    private const float Intensity = 1.4f;
    private Light lantern;
    private ResidentLife life;
    private float flicker;

    private void Awake()
    {
        life = GetComponent<ResidentLife>();
        var holder = new GameObject("Lantern");
        holder.transform.SetParent(transform, false);
        // Held at hip height to one side, as a lantern is carried.
        holder.transform.localPosition = new Vector3(0.35f, 0.9f, 0.25f);
        lantern = holder.AddComponent<Light>();
        lantern.type = LightType.Point;
        lantern.range = 5f;
        lantern.color = new Color(1f, 0.7f, 0.38f);
        lantern.shadows = LightShadows.None;
        lantern.intensity = 0f;
        lantern.enabled = false;
        flicker = Random.value * 10f;
    }

    private void Update()
    {
        // Lit on the way home after dark, out once they are in bed.
        bool awake = life == null || !life.IsResting;
        float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.85f, NightSettings.Darkness));
        flicker += Time.deltaTime * 6f;
        float wanted = awake ? dark * Intensity * (0.85f + 0.15f * Mathf.PerlinNoise(flicker, 0.5f)) : 0f;
        lantern.intensity = Mathf.MoveTowards(lantern.intensity, wanted, Time.deltaTime * 2f);
        lantern.enabled = lantern.intensity > 0.01f;
    }
}
