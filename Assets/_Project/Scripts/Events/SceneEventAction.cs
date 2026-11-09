using System;
using UnityEngine;
using UnityEngine.Events;

// Saved by position: add new kinds at the end.
public enum SceneEventActionKind { PlayMusic, SilenceMusic, ResumeMusic, ChangeWeather, SetObjectActive, Parameters, ChangeTimeOfDay, ChangeLook }

/// <summary>Scene-owned responses; definitions and quests never hold references into a scene.</summary>
[Serializable]
public sealed class SceneEventAction
{
    [SerializeField] private SceneEventActionKind kind;
    [SerializeField] private SceneAudio sceneAudio;
    [SerializeField] private MusicPlaylist music = new MusicPlaylist();
    [Tooltip("Play Music: combat music takes over while the player fights, and this comes back after (a place's mood " +
        "music). Off: it plays through fights too (a boss's or a scripted moment's music).")]
    [SerializeField] private bool fightsInterrupt;
    [SerializeField] private WeatherSystem weatherSystem;
    [SerializeField] private WeatherDefinition weather;
    [SerializeField, Min(0f)] private float transitionSeconds = 3f;
    [SerializeField] private GameObject target;
    [SerializeField] private bool active = true;
    [Tooltip("Use the standard property/method picker for float, int, bool, string or object arguments. " +
        "Examples: Light.intensity, Animator.SetTrigger, AudioSource.volume, component.enabled.")]
    [SerializeField] private UnityEvent parameters = new UnityEvent();
    [SerializeField] private DayNightCycle dayNight;
    [Tooltip("The hour the clock moves forward to (7.5 is early morning, 19 sunset, 23 night).")]
    [SerializeField, Range(0f, 24f)] private float hour = 21f;
    [SerializeField] private SceneLook look;
    [Tooltip("0: the scene's first look, 1: the second.")]
    [SerializeField, Range(0f, 1f)] private float lookAmount = 1f;

    public string FindProblem()
    {
        switch (kind)
        {
            case SceneEventActionKind.PlayMusic:
                if (sceneAudio == null) return "Music action needs this scene's SceneAudio.";
                if (!music.HasTracks) return "Music action needs at least one audio clip.";
                break;
            case SceneEventActionKind.SilenceMusic:
            case SceneEventActionKind.ResumeMusic:
                if (sceneAudio == null) return "Music action needs this scene's SceneAudio.";
                break;
            case SceneEventActionKind.ChangeWeather:
                if (weatherSystem == null || weather == null) return "Weather action needs a WeatherSystem and WeatherDefinition.";
                break;
            case SceneEventActionKind.SetObjectActive:
                if (target == null) return "Object action needs a target GameObject.";
                break;
            case SceneEventActionKind.Parameters:
                if (parameters.GetPersistentEventCount() == 0) return "Parameter action has no configured property or method.";
                break;
            case SceneEventActionKind.ChangeTimeOfDay:
                if (dayNight == null) return "Time of day action needs this scene's DayNightCycle.";
                break;
            case SceneEventActionKind.ChangeLook:
                if (look == null) return "Look action needs this scene's Scene Look.";
                break;
        }
        return null;
    }

    /// <summary>Carries out the reaction. <paramref name="atOnce"/> skips its transition: the scene is loading into a state that
    /// came about earlier, so the storm is simply there.</summary>
    public void Execute(UnityEngine.Object context, bool atOnce = false)
    {
        string problem = FindProblem();
        if (problem != null)
        {
            Debug.LogError("Quest scene reaction: " + problem, context);
            return;
        }
        switch (kind)
        {
            case SceneEventActionKind.PlayMusic: sceneAudio.PlayEventMusic(music, fightsInterrupt); break;
            case SceneEventActionKind.SilenceMusic: sceneAudio.SilenceMusic(); break;
            case SceneEventActionKind.ResumeMusic: sceneAudio.ResumeMusic(); break;
            case SceneEventActionKind.ChangeWeather: weatherSystem.SetWeather(weather, atOnce ? 0f : transitionSeconds); break;
            case SceneEventActionKind.SetObjectActive: target.SetActive(active); break;
            case SceneEventActionKind.Parameters: parameters.Invoke(); break;
            case SceneEventActionKind.ChangeTimeOfDay: dayNight.GoToHour(hour, atOnce ? 0f : transitionSeconds); break;
            case SceneEventActionKind.ChangeLook: look.FadeTo(lookAmount, atOnce ? 0f : transitionSeconds); break;
        }
    }
}
