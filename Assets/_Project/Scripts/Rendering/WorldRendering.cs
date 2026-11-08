using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The world's runtime rendering helpers, made by GameSystems so scenes need no setup: the reflection probe that follows the
/// player (FollowReflectionProbe), the sky (SkyDome) and its clouds (CloudLayer) with their shadows on the sun (CloudShadows). Each scene entered re-binds them to
/// that scene's sun and player. Numbers in RenderingSettings.
/// </summary>
public sealed class WorldRendering : MonoBehaviour, IGameSystem
{
    private RenderingSettings settings;
    private FollowReflectionProbe reflections;
    private CloudLayer cloudLayer;
    private CloudShadows clouds;
    private SkyDome sky;
    private RaidGradeVolume raidGrade;
    private WorldLights lights;
    private GroundWear wear;
    private SkyOcclusionMap occlusion;

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

        if (settings.SkyOcclusion)
        {
            occlusion = gameObject.AddComponent<SkyOcclusionMap>();
            occlusion.Initialise(settings);
        }

        var grade = new GameObject("Raid Grade");
        grade.transform.SetParent(transform, false);
        raidGrade = grade.AddComponent<RaidGradeVolume>();
        raidGrade.Initialise();
        lights = gameObject.AddComponent<WorldLights>();
        lights.Initialise(settings);
        if (settings.FireShadows)
        {
            gameObject.AddComponent<LightShadowBudget>().Initialise(settings, lights);
        }

        gameObject.AddComponent<CharacterLook>().Initialise(settings);

        cloudLayer = gameObject.AddComponent<CloudLayer>();
        cloudLayer.Initialise(settings);
        sky = gameObject.AddComponent<SkyDome>();
        sky.Initialise(settings);
        if (settings.CloudShadows)
        {
            clouds = gameObject.AddComponent<CloudShadows>();
            clouds.Initialise(settings, cloudLayer);
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        sky?.Bind();
        // Painted trees sway in their vertices (Tree Wind's motion vectors): their own pass, not the camera's guess, so
        // TAA and STP don't smear them.
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            terrain.treeMotionVectorModeOverride = TreeMotionVectorModeOverride.PerObjectMotion;
        }

        lights.Rescan();
        reflections?.Refresh();
        wear?.Clear();
        RaidDefinition raid = GameSession.ActiveRaid?.Definition;
        raidGrade.Apply(raid != null ? raid.Grade : null);
        occlusion?.Bind(raid == null ? 1f : settings.SkyOcclusionInRaids);
        clouds?.Bind(RenderSettings.sun, raid == null || settings.CloudShadowsInRaids);
    }
}
