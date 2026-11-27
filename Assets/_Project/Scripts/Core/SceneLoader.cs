using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Changes scenes for gameplay (raids, returning home, loading a save) through <see cref="SceneTransition"/>: behind a
/// loading screen, with the new scene set up before it is shown. Before the current scene unloads it records the state
/// of every saveable object while the scene is still intact, so leaving and coming back finds things as they were. Use
/// it instead of SceneManager.LoadSceneAsync for scene changes during play.
/// </summary>
public static class SceneLoader
{
    /// <summary>True from the moment the old scene's state is recorded until the next scene has loaded.</summary>
    public static bool IsLeaving { get; private set; }

    /// <summary>Returns false if a scene change is already under way.</summary>
    /// <param name="caption">Shown on the loading screen, e.g. "Entering the Misty Woods".</param>
    /// <param name="ifItFails">Undoes what was got ready for the trip, however the load fails: refused now (false is returned too), or
    /// broken off later, with the game going on where it stands (Docs/BugPatterns.md, fifth round: a caller that undid only the first
    /// left a player frozen at the portal with a raid under way that never came).</param>
    /// <param name="asItCommits">Called once the load can no longer break off, just before the new scene comes in: what the trip swaps
    /// into the game (a loaded save) goes in then, not while the old scene still plays on it (SceneTransition).</param>
    public static bool Load(string scene, string caption = null, System.Action ifItFails = null, System.Action asItCommits = null)
    {
        return SceneTransition.Load(scene, caption, ifItFails, asItCommits);
    }

    public static bool Load(int buildIndex, string caption = null, System.Action ifItFails = null, System.Action asItCommits = null)
    {
        return SceneTransition.Load(buildIndex, caption, ifItFails, asItCommits);
    }

    /// <summary>Records every saveable object and marks the scene as leaving. Called by SceneTransition just before it loads.</summary>
    internal static void BeginLeaving()
    {
        // A line being spoken ends with the scene: its voice lives across loads, and ran on into the next scene or the menu.
        VoicePlayer.Stop();
        SaveableEntity.CaptureAll();
        IsLeaving = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>Undoes <see cref="BeginLeaving"/> when the load couldn't start, so the scene keeps saving normally.</summary>
    internal static void CancelLeaving()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        IsLeaving = false;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        IsLeaving = false;
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        IsLeaving = false;
    }
}
