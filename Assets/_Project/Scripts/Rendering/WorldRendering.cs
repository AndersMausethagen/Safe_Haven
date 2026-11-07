using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The world's runtime rendering helpers, made by GameSystems so scenes need no setup: the reflection probe that follows the
/// player (FollowReflectionProbe) and the cloud shadows on the sun (CloudShadows). Each scene entered re-binds them to
/// that scene's sun and player. Numbers in RenderingSettings.
/// </summary>
public sealed class WorldRendering : MonoBehaviour, IGameSystem
{
    private RenderingSettings settings;
    private FollowReflectionProbe reflections;
    private CloudShadows clouds;
    private GroundWear wear;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
        if (settings.Reflections)
        {
            var probe = new GameObject("World Reflections");
            probe.transform.SetParent(transform, false);
            reflections = probe.AddComponent<FollowReflectionProbe>();
            reflections.Initialise(settings);
        }

        var decals = new GameObject("World Decals");
        decals.transform.SetParent(transform, false);
        decals.AddComponent<DecalPool>().Initialise(settings.DecalPoolSize);
        gameObject.AddComponent<FootprintDecals>().Initialise(settings);
        wear = gameObject.AddComponent<GroundWear>();
        wear.Initialise(settings);

        if (settings.CloudShadows)
        {
            clouds = gameObject.AddComponent<CloudShadows>();
            clouds.Initialise(settings);
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        reflections?.Refresh();
        wear?.Clear();
        clouds?.Bind(RenderSettings.sun, GameSession.ActiveRaid == null);
    }
}
