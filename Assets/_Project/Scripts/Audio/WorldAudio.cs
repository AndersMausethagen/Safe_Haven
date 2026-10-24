using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The scene's ambient loops and interface sounds, owned by AudioManager: rain and wind loops that follow the
/// WeatherSystem (as loud as the rain is heavy and the wind strong), thunder a moment after each lightning flash, a
/// stream loop at every water surface (placed at the surface's point nearest the listener, so a long river sounds
/// right along its length), a click when a button is pressed and a sound when windows open or close. Sounds are the
/// Audio Settings' Weather and Water and Interface slots; empty slots play nothing.
/// </summary>
public sealed class WorldAudio
{
    private const float WaterRefresh = 0.2f;

    private readonly GameObject owner;
    private readonly AudioSource rain;
    private readonly AudioSource wind;
    private readonly List<(WaterSurface surface, AudioSource source)> water = new List<(WaterSurface, AudioSource)>();
    private readonly List<float> thunderAt = new List<float>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private WeatherSystem weather;
    private UIStateManager uiState;
    private float nextWaterRefresh;
    private float nextWaterScan;

    public WorldAudio(GameObject owner)
    {
        this.owner = owner;
        rain = Loop("Rain");
        wind = Loop("Wind");
    }

    private AudioSource Loop(string name)
    {
        var go = new GameObject(name + " Loop");
        go.transform.SetParent(owner.transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        return source;
    }

    public void OnSceneEntered()
    {
        if (weather != null)
        {
            weather.Struck -= OnStruck;
        }

        weather = Object.FindAnyObjectByType<WeatherSystem>();
        if (weather != null)
        {
            weather.Struck += OnStruck;
        }

        if (uiState != null)
        {
            uiState.AnyWindowOpenChanged -= OnWindowsChanged;
        }

        uiState = UIStateManager.Instance;
        if (uiState != null)
        {
            uiState.AnyWindowOpenChanged += OnWindowsChanged;
        }

        foreach ((WaterSurface _, AudioSource source) in water)
        {
            if (source != null)
            {
                Object.Destroy(source.gameObject);
            }
        }

        water.Clear();
        thunderAt.Clear();
        nextWaterScan = 0f;
    }

    public void Update(AudioSettingsDefinition sounds)
    {
        WeatherState state = weather != null && weather.isActiveAndEnabled ? weather.Current : default;
        Follow(rain, sounds.RainLoop, state.Rain);
        Follow(wind, sounds.WindLoop, Mathf.Clamp01((state.Wind - 0.8f) / 2.2f + state.Gusts * 0.4f));
        for (int i = thunderAt.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime >= thunderAt[i])
            {
                thunderAt.RemoveAt(i);
                AudioManager.Play(sounds.Thunder, owner.transform.position);
            }
        }

        UpdateWater(sounds.WaterLoop);
        UpdateClicks(sounds.Click);
    }

    // A loop plays while its amount is above nothing, at the sound's volume times the amount.
    private static void Follow(AudioSource source, SoundDefinition sound, float amount)
    {
        AudioClip clip = sound != null ? sound.Clips.Count > 0 ? sound.Clips[0] : null : null;
        if (clip == null || amount <= 0.01f || AudioListener.pause)
        {
            if (source.isPlaying)
            {
                source.volume = Mathf.MoveTowards(source.volume, 0f, Time.unscaledDeltaTime);
                if (source.volume <= 0f)
                {
                    source.Stop();
                }
            }

            return;
        }

        if (source.clip != clip)
        {
            source.clip = clip;
        }

        if (!source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }

        float target = sound.Volume * Mathf.Clamp01(amount) * AudioManager.ChannelVolume(sound.Channel);
        source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 0.5f);
    }

    private void OnStruck()
    {
        // Light arrives first; the roll follows after a moment, as if from a few hundred metres to a kilometre away.
        thunderAt.Add(Time.unscaledTime + Random.Range(0.6f, 2.8f));
    }

    private void UpdateWater(SoundDefinition sound)
    {
        AudioClip clip = sound != null ? sound.PickClip() : null;
        if (clip == null)
        {
            return;
        }

        if (water.Count == 0 && Time.unscaledTime >= nextWaterScan)
        {
            // Look again later for surfaces that wake up after the scene starts.
            nextWaterScan = Time.unscaledTime + 2f;
            foreach (WaterSurface surface in WaterSurface.Surfaces)
            {
                if (surface == null || !surface.TakesRipples)
                {
                    continue;
                }

                AudioSource source = Loop("Water " + surface.name);
                AudioManager.Configure(source, sound);
                source.spatialBlend = 1f;
                source.clip = clip;
                source.time = Random.Range(0f, clip.length);
                source.Play();
                water.Add((surface, source));
            }

        }

        if (Time.unscaledTime < nextWaterRefresh)
        {
            return;
        }

        nextWaterRefresh = Time.unscaledTime + WaterRefresh;
        AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
        Vector3 ear = listener != null ? listener.transform.position : owner.transform.position;
        foreach ((WaterSurface surface, AudioSource source) in water)
        {
            if (surface != null && source != null)
            {
                source.transform.position = surface.NearestPoint(ear);
                source.volume = sound.Volume * AudioManager.ChannelVolume(sound.Channel);
            }
        }
    }

    private void OnWindowsChanged(bool open)
    {
        AudioSettingsDefinition sounds = AudioManager.Sounds;
        if (sounds != null)
        {
            AudioManager.Play(open ? sounds.WindowOpen : sounds.WindowClose, owner.transform.position);
        }
    }

    // A click on an interactable button, toggle or slider under the pointer, or the gamepad's submit on a selected one.
    private void UpdateClicks(SoundDefinition click)
    {
        if (click == null || EventSystem.current == null)
        {
            return;
        }

        GameObject pressed = null;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
            uiHits.Clear();
            EventSystem.current.RaycastAll(pointer, uiHits);
            pressed = uiHits.Count > 0 ? uiHits[0].gameObject : null;
        }
        else if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            pressed = EventSystem.current.currentSelectedGameObject;
        }

        Selectable selectable = pressed != null ? pressed.GetComponentInParent<Selectable>() : null;
        if (selectable != null && selectable.IsInteractable())
        {
            AudioManager.Play(click, owner.transform.position);
        }
    }
}
