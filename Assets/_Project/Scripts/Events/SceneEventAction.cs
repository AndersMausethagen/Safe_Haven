using System;
using UnityEngine;
using UnityEngine.Events;

public enum SceneEventActionKind { PlayMusic, SilenceMusic, ResumeMusic, ChangeWeather, SetObjectActive, Parameters }

/// <summary>Scene-owned responses; definitions and quests never hold references into a scene.</summary>
[Serializable]
public sealed class SceneEventAction
{
    [SerializeField] private SceneEventActionKind kind;
    [SerializeField] private SceneAudio sceneAudio;
    [SerializeField] private MusicPlaylist music = new MusicPlaylist();
    [SerializeField] private WeatherSystem weatherSystem;
    [SerializeField] private WeatherDefinition weather;
    [SerializeField, Min(0f)] private float transitionSeconds = 3f;
    [SerializeField] private GameObject target;
    [SerializeField] private bool active = true;
    [Tooltip("Use the standard property/method picker for float, int, bool, string or object arguments. " +
        "Examples: Light.intensity, Animator.SetTrigger, AudioSource.volume, component.enabled.")]
    [SerializeField] private UnityEvent parameters = new UnityEvent();

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
        }
        return null;
    }

    public void Execute(UnityEngine.Object context)
    {
        string problem = FindProblem();
        if (problem != null)
        {
            Debug.LogError("Quest scene reaction: " + problem, context);
            return;
        }
        switch (kind)
        {
            case SceneEventActionKind.PlayMusic: sceneAudio.PlayEventMusic(music); break;
            case SceneEventActionKind.SilenceMusic: sceneAudio.SilenceMusic(); break;
            case SceneEventActionKind.ResumeMusic: sceneAudio.ResumeMusic(); break;
            case SceneEventActionKind.ChangeWeather: weatherSystem.SetWeather(weather, transitionSeconds); break;
            case SceneEventActionKind.SetObjectActive: target.SetActive(active); break;
            case SceneEventActionKind.Parameters: parameters.Invoke(); break;
        }
    }
}
