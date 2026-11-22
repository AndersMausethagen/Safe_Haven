using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The Haven's look while a ward breach is under way (the user, 2026-11-22: "the postprocessing should change to reflect the danger"):
/// a global Volume with WardBreachSettings › Breach Look, faded in as the ward gives way and out when it is over. Over the scene's own
/// grade, not instead of it. On the WardBreachDirector (added in its Awake).
/// </summary>
[DisallowMultipleComponent]
public sealed class BreachMood : MonoBehaviour
{
    private const float GivingWayWeight = 0.6f;

    private WardBreachDirector director;
    private Volume volume;

    /// <summary>How far the look is in now (0-1).</summary>
    public float Weight => volume != null ? volume.weight : 0f;

    private void Awake() => director = GetComponent<WardBreachDirector>();

    private void Update()
    {
        WardBreachSettings settings = WardBreachSettings.Current;
        float wanted = director == null ? 0f : director.IsActive ? 1f : director.IsUnderWay ? GivingWayWeight : 0f;
        if (volume == null)
        {
            if (wanted <= 0f || settings.BreachLook == null)
            {
                return;
            }

            var holder = new GameObject("Breach Look");
            holder.transform.SetParent(transform, false);
            volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.sharedProfile = settings.BreachLook;
            volume.weight = 0f;
        }

        volume.weight = Mathf.MoveTowards(volume.weight, wanted, Time.deltaTime / Mathf.Max(0.1f, settings.LookFadeSeconds));
        volume.enabled = volume.weight > 0.001f;
    }
}
