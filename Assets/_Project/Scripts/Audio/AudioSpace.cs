using UnityEngine;

/// <summary>
/// The space sounds are heard in (Docs/LookAndFeelResearch_2026-11-18.md A2-A4), without an AudioMixer: Unity's reverb zone held on the
/// listener (so the world's placed sounds take the place's reverb: forest, open valley, a gorge's slap echo, a room under a roof,
/// muffled while swimming), and a low-pass on every effect voice that closes with distance (air takes the highs first: far is dull,
/// near bright) and further when the ground or a building stands between the sound and the ears (a troll behind the ridge sounds
/// behind it, which also tells the sneaking player where it is). Music, ambience and the interface are flat and untouched.
/// <para>Owned by AudioManager: one per game, updated after the camera; a scene's own place is a <see cref="PlaceAcoustics"/>.</para>
/// </summary>
public sealed class AudioSpace
{
    // Hz: no filtering at all, the most air takes at a sound's audible distance, and how much a wall between leaves of that.
    public const float OpenCutoff = 22000f;
    public const float FarCutoff = 6000f;
    public const float OccludedShare = 0.16f;
    // A voice behind the ground or a building is this much quieter as well.
    public const float OccludedVolume = 0.63f;
    // Swimming: the ears are at the water, so the world is dull.
    public const float SwimmingCutoff = 1600f;
    // Within this many metres a sound is never occluded (it is at the player's feet).
    private const float NoOcclusionWithin = 4f;
    // A roof within this many metres above the ears means inside.
    private const float RoofReach = 7f;
    private const float PlaceEvery = 0.4f;

    private readonly AudioReverbZone reverb;
    private AudioReverbPreset current = AudioReverbPreset.Off;
    private float nextPlace;

    public AudioSpace(Transform parent)
    {
        var holder = new GameObject("Audio Space");
        holder.transform.SetParent(parent, false);
        reverb = holder.AddComponent<AudioReverbZone>();
        reverb.minDistance = 5000f;
        reverb.maxDistance = 6000f;
        reverb.reverbPreset = AudioReverbPreset.Off;
    }

    /// <summary>The place's reverb (what is around the ears), kept on the listener. Called after the camera has moved.</summary>
    public void Update(Vector3 ears)
    {
        reverb.transform.position = ears;
        if (Time.unscaledTime < nextPlace)
        {
            return;
        }

        nextPlace = Time.unscaledTime + PlaceEvery;
        AudioReverbPreset wanted = PlaceAt(ears, out float level);
        if (wanted == current)
        {
            return;
        }

        current = wanted;
        reverb.reverbPreset = wanted;
        if (wanted != AudioReverbPreset.Off && level < 1f)
        {
            // A preset is fixed; as a user zone its room level can come down (a top-down game's reverb wants to sit under the sound).
            int room = reverb.room;
            reverb.reverbPreset = AudioReverbPreset.User;
            reverb.room = Mathf.Clamp(room + Mathf.RoundToInt(2000f * Mathf.Log10(Mathf.Max(level, 0.01f))), -10000, 0);
        }
    }

    // Swimming, then under a roof, then the scene's own place.
    private static AudioReverbPreset PlaceAt(Vector3 ears, out float level)
    {
        level = 1f;
        Player player = Player.Current;
        if (player != null && player.TryGetComponent(out PlayerSwimming swimming) && swimming.IsSwimming)
        {
            return AudioReverbPreset.Underwater;
        }

        if (Physics.Raycast(ears + Vector3.up * 0.3f, Vector3.up, out RaycastHit roof, RoofReach, ~0, QueryTriggerInteraction.Ignore)
            && roof.collider.GetComponentInParent<BuildingObject>() != null)
        {
            level = 0.6f;
            return AudioReverbPreset.Livingroom;
        }

        PlaceAcoustics place = PlaceAcoustics.Current;
        if (place == null)
        {
            return AudioReverbPreset.Off;
        }

        level = place.Level;
        return place.Preset;
    }

    /// <summary>Shapes an effect voice as it starts: air with distance, the ground or a building between, the water while swimming.</summary>
    public static void Shape(AudioSource source, SoundDefinition sound, Vector3 position, Vector3 ears)
    {
        if (!source.TryGetComponent(out AudioLowPassFilter filter))
        {
            return;
        }

        if (!sound.Positional)
        {
            filter.cutoffFrequency = OpenCutoff;
            return;
        }

        float distance = Vector3.Distance(position, ears);
        bool occluded = distance > NoOcclusionWithin && Occluded(ears, position);
        Player player = Player.Current;
        bool swimming = player != null && player.TryGetComponent(out PlayerSwimming swim) && swim.IsSwimming;
        filter.cutoffFrequency = Cutoff(distance, sound.AudibleDistance, occluded, swimming);
        if (occluded)
        {
            source.volume *= OccludedVolume;
        }
    }

    /// <summary>The low-pass cutoff (Hz) for a sound this far off (of its audible distance), behind something or not, while swimming or not. Pure, for tests.</summary>
    public static float Cutoff(float distance, float audible, bool occluded, bool swimming)
    {
        float share = Mathf.Clamp01(distance / Mathf.Max(1f, audible));
        // Geometric: each step out takes the same share of the highs.
        float cutoff = OpenCutoff * Mathf.Pow(FarCutoff / OpenCutoff, share);
        if (occluded)
        {
            cutoff *= OccludedShare;
        }

        if (swimming)
        {
            cutoff = Mathf.Min(cutoff, SwimmingCutoff);
        }

        return Mathf.Clamp(cutoff, 400f, OpenCutoff);
    }

    // The ground (terrain) or a building between the ears and the sound; creatures, plants and the player don't count. The line ends
    // a metre up the sound, so a bump at a ground-level sound's own feet doesn't muffle it.
    private static bool Occluded(Vector3 ears, Vector3 position)
    {
        if (!Physics.Linecast(ears, position + Vector3.up * 1f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider is TerrainCollider ground)
        {
            Terrain terrain = ground.GetComponent<Terrain>();
            return terrain == null || CountsAsGround(hit.point.y, terrain.SampleHeight(hit.point) + terrain.transform.position.y);
        }

        return hit.collider.GetComponentInParent<BuildingObject>() != null;
    }

    /// <summary>Whether a hit on the terrain's collider is the ground itself: a tree's trunk is reported as the terrain's collider too,
    /// and stands well above the ground under it. Pure, for tests.</summary>
    public static bool CountsAsGround(float hitHeight, float groundHeight) => hitHeight <= groundHeight + 0.5f;

    /// <summary>No player to hear for (death, the menu, a loading screen): the place's reverb lets go, so nothing plays in the last
    /// place's room. The next Update picks the place again.</summary>
    public void Clear()
    {
        nextPlace = 0f;
        if (current == AudioReverbPreset.Off)
        {
            return;
        }

        current = AudioReverbPreset.Off;
        reverb.reverbPreset = AudioReverbPreset.Off;
    }
}
