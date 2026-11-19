using UnityEngine;

/// <summary>
/// A named place heard before it is seen (Docs/HavenExplorationReview_2026-11-19.md §3): now and then its sound is played from it (crows
/// over the barrow by day, the den's howl at dusk and night, wind round a cairn), as far as the sound carries. From the top-down camera
/// nothing 14 m off is in frame, so a place must carry by ear. Two at most are heard at once, so the land never turns into a din; none
/// in a fight. Stood at its place by HavenPlaceVerbs.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlaceBeacon : MonoBehaviour
{
    private const int MostAtOnce = 2;
    // When the beacons sounding now end (real time): the cap on how many are heard together.
    private static readonly float[] sounding = new float[MostAtOnce];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => System.Array.Clear(sounding, 0, sounding.Length);

    private SoundDefinition sound;
    private HavenPlaceVerbs.When when;
    private Vector2 every;
    private float due;

    public void Configure(SoundDefinition played, HavenPlaceVerbs.When hours, Vector2 seconds)
    {
        sound = played;
        when = hours;
        every = new Vector2(Mathf.Max(1f, Mathf.Min(seconds.x, seconds.y)), Mathf.Max(1f, Mathf.Max(seconds.x, seconds.y)));
        due = Time.time + Random.Range(0f, every.y);
    }

    /// <summary>Whether a sound kept to these hours plays at <paramref name="hour"/> (0-24): night from 20 to 6.</summary>
    public static bool PlaysAt(HavenPlaceVerbs.When when, float hour)
    {
        bool night = hour >= 20f || hour < 6f;
        return when == HavenPlaceVerbs.When.Always || (when == HavenPlaceVerbs.When.Night) == night;
    }

    private void Update()
    {
        if (sound == null || Time.time < due)
        {
            return;
        }

        due = Time.time + Random.Range(every.x, every.y);
        Player player = Player.Current;
        if (player == null || GameStateManager.InCombat || !PlaysAt(when, AtmosphereConditions.Hour)
            || (player.transform.position - transform.position).sqrMagnitude > sound.AudibleDistance * sound.AudibleDistance)
        {
            return;
        }

        int free = -1;
        for (int i = 0; i < sounding.Length; i++)
        {
            if (Time.unscaledTime >= sounding[i])
            {
                free = i;
                break;
            }
        }

        if (free < 0)
        {
            return;
        }

        sounding[free] = Time.unscaledTime + 3f;
        AudioManager.Play(sound, transform.position + Vector3.up * 2f);
    }
}
