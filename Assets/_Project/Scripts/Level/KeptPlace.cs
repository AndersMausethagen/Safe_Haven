using UnityEngine;

/// <summary>
/// A worked place in the Haven's land keeps the land round it in mind (Docs/HavenWorldStory_2026-11-26.md: the mist is the forgetting,
/// and someone working there remembers): while it stands, HavenLand thins the mist and brings back some colour round it, even before
/// its region is remembered. Stood by HavenPlaceScenes on a stage that smokes; gone with it.
/// </summary>
[DisallowMultipleComponent]
public sealed class KeptPlace : MonoBehaviour
{
    private HavenLand keptBy;

    private void Update()
    {
        // HavenLand may come after it (or be made again): kept by whichever stands now.
        HavenLand land = HavenLand.Current;
        if (land != keptBy)
        {
            Release();
            if (land != null)
            {
                land.Keep(this, new Vector2(transform.position.x, transform.position.z), HavenRegionSettings.Current.KeptRadius);
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
