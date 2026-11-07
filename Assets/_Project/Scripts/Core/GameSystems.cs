using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// A game-wide system that lives across scene changes, such as the audio or quest manager. GameSystems creates it once
/// when the game starts and tells it about every scene the player enters.
/// </summary>
public interface IGameSystem
{
    /// <summary>Called once per scene, after every object in it has woken up (Awake) but before the first Update.</summary>
    void OnSceneEntered(Scene scene);
}

/// <summary>
/// Starts the game's persistent systems in one place: it creates a single "Game Systems" object that survives scene
/// loads, adds each system to it and tells them whenever a scene is entered. Scenes need no setup for these systems.
/// <para>
/// It also hides an editor quirk: with Enter Play Mode Options skipping the scene reload, Unity doesn't report the open
/// scene as loaded, so systems would miss the first scene. GameSystems reports every scene exactly once either way.
/// </para>
/// <para>
/// Extending: to add a persistent system, write a MonoBehaviour implementing IGameSystem with an Initialise method and
/// add it in <see cref="CreateSystems"/>. Systems that need authored settings load them from a Resources folder there.
/// </para>
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class GameSystems : MonoBehaviour
{
    private readonly List<IGameSystem> systems = new List<IGameSystem>();
    private Scene lastScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOnStart()
    {
        var host = new GameObject("Game Systems");
        DontDestroyOnLoad(host);
        host.AddComponent<GameSystems>().CreateSystems();
    }

    private void CreateSystems()
    {
        var audioSettings = Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName);
        if (audioSettings != null)
        {
            Add<AudioManager>(audio => audio.Initialise(audioSettings));
        }
        else
        {
            Debug.LogWarning($"No '{AudioSettingsDefinition.ResourceName}' asset in a Resources folder, so the game has no music or sound effects.");
        }

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (content != null)
        {
            Add<QuestManager>(quests => quests.Initialise(content));
        }
        else
        {
            Debug.LogWarning($"No '{ContentDatabase.ResourceName}' asset in a Resources folder, so quests are off.");
        }

        // Falls back to built-in defaults when the asset is missing, so combat always has feedback.
        Add<CombatFeedback>(feedback => feedback.Initialise(CombatFeelSettings.Current));

        Add<SettlementSystem>(settlement => settlement.Initialise(SettlementSettings.Current));

        Add<PlaytestLogger>(logger => logger.Initialise(PlaytestSettings.Current));

        Add<ExperienceTracker>(experience => experience.Initialise(LevelingSettings.Current));
        Add<OptionsSystem>(options => options.Initialise());
        Add<SoundCaptions>(captions => captions.Initialise());
        Add<CheatSystem>(cheats => cheats.Initialise());
        Add<SteamPlatform>(steam => steam.Initialise(MultiplayerSettings.Current));
        Add<CoopSession>(coop => coop.Initialise(MultiplayerSettings.Current, SteamPlatform.Current));

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Each system starts in isolation, as it is told about scenes in isolation: one that fails is left out, and the others still run.
    private void Add<T>(Action<T> initialise) where T : Component, IGameSystem
    {
        var system = gameObject.AddComponent<T>();
        try
        {
            initialise(system);
            systems.Add(system);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, system);
            Destroy(system);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Covers the first scene when Unity doesn't report it as loaded; Start runs after the scene's objects have woken up.
    private void Start()
    {
        EnterScene(SceneManager.GetActiveScene());
    }

    // sceneLoaded comes after the new scene's Awake and OnEnable, which is when systems can find what is in it.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            EnterScene(scene);
        }
    }

    private void EnterScene(Scene scene)
    {
        // Scenes compare by their load handle, which is new for every load, so a scene reported by both Start and
        // sceneLoaded is only entered once while a reloaded scene is entered again.
        if (scene == lastScene)
        {
            return;
        }

        lastScene = scene;
        foreach (IGameSystem system in systems)
        {
            try
            {
                system.OnSceneEntered(scene);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, (Object)system);
            }
        }
    }
}
