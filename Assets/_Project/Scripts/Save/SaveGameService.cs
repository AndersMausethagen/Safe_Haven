using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts, saves and loads games. Saving gathers the in-memory state (the live player and GameSession's stash, world and
/// quests), turns it into a SaveData through SaveMapper and has SaveStorage write it. Loading reads it back, rebuilds the
/// runtime objects, hands them to GameSession and loads the saved scene, where PlayerPersistence and the saveable
/// objects apply them. There is one save slot for now; slots can be added by passing SaveStorage a slot name. Saving is
/// refused during a raid, so a raid can't be replayed from the middle.
/// <para>
/// The save system in layers, each in its own file (Assets/_Project/Scripts/Save):
/// GameSession (what lives in memory) → SaveMapper (memory ↔ file format) → SaveData (the file format) →
/// SaveStorage (reading and writing files). This service is the only one that drives all of them.
/// </para>
/// <para>
/// Extending, to save a new system's state: 1) keep its runtime state in GameSession, 2) add a [Serializable] section to
/// SaveData, 3) convert both ways in SaveMapper (and a field on LoadedGame), 4) hand it over in GameSession.LoadGame.
/// For state that belongs to objects in a scene, implement ISaveable on a component instead; SaveableEntity does the rest.
/// </para>
/// </summary>
public static class SaveGameService
{
    private static SaveStorage storage;
    private static bool loading;
    private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> pendingArrival;

    /// <summary>Where saves are read from and written to. Replace it to use another folder, for example in tests.</summary>
    public static SaveStorage Storage
    {
        get => storage ??= new SaveStorage();
        set => storage = value;
    }

    public static bool HasSave => Storage.Exists(SaveStorage.DefaultSlot);

    /// <summary>Why the game can't be saved right now, in words for the player, or null when it can.</summary>
    public static string CheckSave()
    {
        if (loading)
        {
            return "The game is still loading.";
        }

        if (GameSession.ActiveRaid != null)
        {
            return "You can't save during a raid. Extract first.";
        }

        if (PrologueDirector.Current != null && !PrologueDirector.Current.CanSave)
        {
            return "Finish this prologue encounter first. Safe phases save checkpoints; loading restarts the current phase.";
        }

        return Player.Current == null || Player.Current.Persistence == null ? "There is nothing to save here." : null;
    }

    public static bool TrySave(out string problem)
    {
        problem = CheckSave();
        if (problem != null)
        {
            return false;
        }

        PlayerPersistence player = Player.Current.Persistence;
        // Objects keep their own state; it is gathered into memory first, then copied to disk.
        SaveableEntity.CaptureAll();
        SaveData data = SaveMapper.ToSaveData(player.Capture(), GameSession.Stash, SceneManager.GetActiveScene().name, player.Pose, DateTime.UtcNow,
            GameSession.World, GameSession.Quests, GameSession.Settlement, GameSession.Playtest, GameSession.Progress);
        try
        {
            Storage.Write(SaveStorage.DefaultSlot, data);
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogException(exception);
            problem = "The game couldn't be saved: " + exception.Message;
            return false;
        }
    }

    /// <summary>Reads the save without loading it, for showing what it holds. False, with a reason, when it can't be used.</summary>
    public static bool TryReadSave(out SaveData data, out string problem)
    {
        if (!Storage.TryRead(SaveStorage.DefaultSlot, out data, out problem))
        {
            return false;
        }

        if (data.version < 1 || data.version > SaveMapper.CurrentVersion)
        {
            problem = data.version > SaveMapper.CurrentVersion
                ? "This save was made by a newer version of the game."
                : "This isn't a Safe Haven save.";
            data = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Loads the save and goes to its scene, or to fallbackScene if that scene no longer exists. False, with a reason
    /// for the player, when there is nothing to load.
    /// </summary>
    public static bool TryLoad(IContentLookup content, string fallbackScene, out string problem)
    {
        if (loading)
        {
            problem = "The game is already loading.";
            return false;
        }

        if (!TryReadSave(out SaveData data, out problem))
        {
            return false;
        }

        LoadedGame game = SaveMapper.FromSaveData(data, content);
        bool sameScene = CanLoad(game.Scene);
        string scene = sameScene ? game.Scene : fallbackScene;
        if (!CanLoad(scene))
        {
            problem = $"The saved place '{game.Scene}' can't be loaded.";
            return false;
        }

        string notice = "Game loaded.";
        if (game.MissingContent.Count > 0)
        {
            Debug.LogWarning("The save mentions content that no longer exists and was skipped: " + string.Join(", ", game.MissingContent));
            notice = $"Game loaded. {game.MissingContent.Count} saved entries no longer exist and were skipped.";
        }

        // A position only makes sense in the scene it was saved in.
        if (!sameScene)
        {
            game.Pose = null;
        }

        GameSession.LoadGame(game);
        Go(scene, notice, "Returning to your journey");
        return true;
    }

    /// <summary>Forgets the current game and starts over in the given scene. The save on disk is kept until the next save.</summary>
    public static bool TryStartNewGame(string scene, out string problem)
    {
        problem = loading ? "The game is already loading." : !CanLoad(scene) ? $"The scene '{scene}' isn't in the build." : null;
        if (problem != null)
        {
            return false;
        }

        GameSession.StartNewGame();
        Go(scene, null, "A new journey begins");
        return true;
    }

    private static void Go(string scene, string notice, string caption)
    {
        // The player stays in the old scene until the new one is ready, so keep them still meanwhile.
        if (Player.Current != null && Player.Current.TryGetComponent(out PlayerInputHandler input))
        {
            input.enabled = false;
        }

        loading = true;
        void OnLoaded(Scene loaded, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnLoaded;
            pendingArrival = null;
            loading = false;
            // Posted once the loading screen has lifted, so the player sees it.
            if (notice != null)
            {
                SceneTransition.WhenReady(() => GameMessages.Post(notice, MessageKind.Info));
            }
        }

        pendingArrival = OnLoaded;
        SceneManager.sceneLoaded += pendingArrival;
        if (!SceneLoader.Load(scene, caption))
        {
            SceneManager.sceneLoaded -= pendingArrival;
            pendingArrival = null;
            loading = false;
        }
    }

    private static bool CanLoad(string scene) => !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        if (pendingArrival != null)
        {
            SceneManager.sceneLoaded -= pendingArrival;
            pendingArrival = null;
        }

        loading = false;
        storage = null;
    }
}
