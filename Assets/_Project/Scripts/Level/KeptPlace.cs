using UnityEngine;

/// <summary>
/// A place in the Haven's land that holds the forgetting back round it (Docs/HavenWorldStory_2026-11-26.md: the mist is the forgetting).
/// A worked place keeps the land in mind (Remembers): while it stands, HavenLand thins the mist and brings back some colour round it,
/// even before its region is remembered. Some places hold back the mist itself (Clears): the stones above the tarn, the spring, in low
/// ground by water where the mist pools thick even over remembered land. Stood by HavenPlaceScenes on a stage that smokes or clears;
/// gone with it.
/// </summary>
[DisallowMultipleComponent]
public sealed class KeptPlace : MonoBehaviour
{
    private HavenLand keptBy;
    private bool remembers = true;
    private float clears;

    /// <summary>Call before it first updates.</summary>
    public void Configure(bool keepsInMind, float holdsBack)
    {
        remembers = keepsInMind;
        clears = Mathf.Clamp01(holdsBack);
    }

    private void Update()
    {
        // HavenLand may come after it (or be made again): kept by whichever stands now.
        HavenLand land = HavenLand.Current;
        if (land != keptBy)
        {
            Release();
            if (land != null)
            {
                land.Keep(this, new Vector2(transform.position.x, transform.position.z), HavenRegionSettings.Current.KeptRadius, remembers, clears);
                keptBy = land;
            }
        }
    }

    private void OnDisable() => Release();

    private void Release()
    {
        if (keptBy != null)
        {
            keptBy.Release(this);
        }

        keptBy = null;
    }
}
