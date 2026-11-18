using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A biome's sound bed (Docs/LookAndFeelResearch_2026-11-18.md A1: "a biome is half its sound"): loops under the scene's own ambience
/// (SceneAudio), each faded in and out by the hour and the weather, so the marsh hums with insects by day and croaks with frogs from
/// dusk, and the woods' canopy sighs by day and stills by night. Flat (not placed), on the Ambience volume, and dipped with it while
/// someone speaks; it keeps playing through a pause, as ambience does.
/// <para>Where it belongs: one per scene, beside its SceneAudio (raid scenes). Its sounds are SoundDefinitions holding a looping clip.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class AmbienceBed : MonoBehaviour
{
    [Serializable]
    public sealed class Layer
    {
        [Tooltip("The loop (its first clip; a stereo clip keeps its width).")]
        public SoundDefinition sound;
        [Tooltip("How loud at each hour (0-24 across, 0-1 up). Empty: always.")]
        public AnimationCurve byHour = new AnimationCurve();
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("How loud in full rain, as a share (insects and frogs go quiet; drips grow): 1 unchanged.")]
        [Range(0f, 2f)] public float inRain = 1f;
    }

    [SerializeField] private List<Layer> layers = new List<Layer>();
    [Tooltip("Seconds a layer takes to come in or go out as the hour or weather turns.")]
    [SerializeField, Min(0.1f)] private float fadeSeconds = 4f;

    private readonly List<AudioSource> sources = new List<AudioSource>();

    public IReadOnlyList<Layer> Layers => layers;

    /// <summary>For tools: the bed's layers, replaced.</summary>
    public void Configure(IEnumerable<Layer> with)
    {
        layers = new List<Layer>(with);
    }

    /// <summary>How loud a layer is meant to be (0-1, before the player's volume) at <paramref name="hour"/> in <paramref name="rain"/>. Pure, for tests.</summary>
    public static float LevelOf(Layer layer, float hour, float rain)
    {
        if (layer == null || layer.sound == null)
        {
            return 0f;
        }

        float byHour = layer.byHour != null && layer.byHour.length > 0 ? Mathf.Clamp01(layer.byHour.Evaluate(hour)) : 1f;
        return layer.volume * layer.sound.Volume * byHour * Mathf.Lerp(1f, layer.inRain, Mathf.Clamp01(rain));
    }

    private void Start()
    {
        foreach (Layer layer in layers)
        {
            var go = new GameObject("Bed " + (layer?.sound != null ? layer.sound.name : "empty"));
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            // Flat beds: kept out of the place's reverb (AudioSpace), which is for the world's sounds.
            source.bypassReverbZones = true;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            AudioClip clip = layer?.sound != null ? layer.sound.PickClip() : null;
            if (clip != null)
            {
                source.clip = clip;
                // Each starts somewhere along its loop, so two beds never pulse together.
                source.time = UnityEngine.Random.Range(0f, clip.length * 0.9f);
                source.Play();
            }

            sources.Add(source);
        }
    }

    private void Update()
    {
        float hour = AtmosphereConditions.Hour;
        WeatherSystem weather = WeatherSystem.Active;
        float rain = weather != null && !weather.Snowing ? weather.Current.Rain : 0f;
        // Held for a cinematic, as the scene's own ambience is (AudioManager › Music Held).
        float channel = AudioManager.MusicHeld ? 0f : AudioManager.ChannelVolume(AudioChannel.Ambience);
        float step = Time.unscaledDeltaTime / fadeSeconds;
        for (int i = 0; i < sources.Count && i < layers.Count; i++)
        {
            AudioSource source = sources[i];
            if (source == null || source.clip == null)
            {
                continue;
            }

            float target = LevelOf(layers[i], hour, rain) * channel;
            // Down within half a second (speech's dip, already eased by the channel; a cinematic starting), up over the fade as the hour turns.
            float rate = target < source.volume ? Time.unscaledDeltaTime * 2f : step * Mathf.Max(0.05f, layers[i].volume);
            source.volume = Mathf.MoveTowards(source.volume, target, rate);
        }
    }
}
