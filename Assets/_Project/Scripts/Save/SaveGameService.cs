using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts, saves and loads games. Saving gathers the in-memory state (the live player and GameSession's stash, world and
/// quests), turns it into a SaveData through SaveMapper and has SaveStorage write it. Loading reads it back, rebuilds the
/// runtime objects, hands them to GameSession and loads the saved scene, where PlayerPersistence and the saveable
/// objects apply them. Saving and loading are refused during a raid, so a raid can't be replayed from the middle or
/// undone by going back to a save.
/// <para>
/// A game can have any number of saves, each its own file (a slot). The game being played writes to <see cref="CurrentSlot"/>:
/// the slot it was loaded from, or a new one the first time a new game saves, so starting over never touches another game.
/// </para>
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
    private const int MaximumNameLength = 32;
    private const string RaidRefusal = "You can't save or load during a raid. Extract first.";

    private static SaveStorage storage;
    private static bool loading;
    private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> pendingArrival;

    /// <summary>Where saves are read from and written to. Replace it to use another folder, for example in tests.</summary>
    public static SaveStorage Storage
    {
        get => storage ??= new SaveStorage();
        set
        {
            storage = value;
            CurrentSlot = null;
            CurrentName = null;
        }
    }

    /// <summary>The save the game being played belongs to. Null until a new game first saves.</summary>
    public static string CurrentSlot { get; private set; }

    /// <summary>The name of the save being played, or null before it has one.</summary>
    public static string CurrentName { get; private set; }

    public static bool HasSave => Storage.ListSlots().Count > 0;

    /// <summary>Every save, newest first. Reads each file, so call it when a list opens, not every frame.</summary>
    public static List<SaveSlotInfo> ListSaves()
    {
        var saves = new List<SaveSlotInfo>();
        foreach (string slot in Storage.ListSlots())
        {
            TryReadSave(slot, out SaveData data, out string problem);
            saves.Add(new SaveSlotInfo(slot, data, problem, Storage.LastWritten(slot)));
        }

        saves.Sort((a, b) => b.SavedAt.CompareTo(a.SavedAt));
        return saves;
    }

    /// <summary>The newest save that can be loaded, or null.</summary>
    public static SaveSlotInfo LatestSave()
    {
        foreach (SaveSlotInfo save in ListSaves())
        {
            if (save.IsReadable)
            {
                return save;
            }
        }

        return null;
    }

    /// <summary>Why the game can't be saved right now, in words for the player, or null when it can.</summary>
    public static string CheckSave()
    {
        if (loading)
        {
            return "The game is still loading.";
        }

        if (GameSession.ActiveRaid != null)
        {
            return RaidRefusal;
        }

        if (PrologueDirector.Current != null && !PrologueDirector.Current.CanSave)
        {
            return "Finish this prologue encounter first. Safe phases save checkpoints; loading restarts the current phase.";
        }

        return Player.Current == null || Player.Current.Persistence == null ? "There is nothing to save here." : null;
    }

    /// <summary>Why a save can't be loaded right now, in words for the player, or null when one can.</summary>
    public static string CheckLoad()
    {
        if (loading)
        {
            return "The game is already loading.";
        }

        return GameSession.ActiveRaid != null ? RaidRefusal : null;
    }

    /// <summary>Saves the game being played to its own save, making one if it has none yet.</summary>
    public static bool TrySave(out string problem)
    {
        return TryWrite(CurrentSlot ?? Storage.NewSlot(DateTime.Now), CurrentName ?? NextName(), out problem);
    }

    /// <summary>Saves the game being played as a new save, which it then carries on in.</summary>
    public static bool TrySaveAsNew(out string problem)
    {
        return TryWrite(Storage.NewSlot(DateTime.Now), NextName(), out problem);
    }

    /// <summary>Replaces another save with the game being played, which then carries on in it under that name.</summary>
    public static bool TryOverwrite(string slot, out string problem)
    {
        Storage.TryRead(slot, out SaveData existing, out _);
        return TryWrite(slot, SaveSlotInfo.NameFor(slot, existing), out problem);
    }

    private static bool TryWrite(string slot, string name, out string problem)
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
        data.displayName = name;
        data.appearance = GameSession.Appearance.Clone();
        try
        {
            Storage.Write(slot, data);
            CurrentSlot = slot;
            CurrentName = name;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogException(exception);
            problem = "The game couldn't be saved: " + exception.Message;
            return false;
        }
    }

    /// <summary>Gives a save a new name. The name is trimmed; an empty one is refused.</summary>
    public static bool TryRename(string slot, string name, out string problem)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0)
        {
            problem = "A save needs a name.";
            return false;
        }

        if (name.Length > MaximumNameLength)
        {
            name = name.Substring(0, MaximumNameLength);
        }

        if (!TryReadSave(slot, out SaveData data, out problem))
        {
            return false;
        }

        data.displayName = name;
        try
        {
            Storage.Write(slot, data);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogException(exception);
            problem = "The name couldn't be saved: " + exception.Message;
            return false;
        }

        if (slot == CurrentSlot)
        {
            CurrentName = name;
        }

        return true;
    }

    /// <summary>Deletes a save and its backup. The game being played, if that was its save, gets a new one when it next saves.</summary>
    public static bool TryDelete(string slot, out string problem)
    {
        problem = null;
        try
        {
            Storage.Delete(slot);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogException(exception);
            problem = "The save couldn't be deleted: " + exception.Message;
            return false;
        }

        if (slot == CurrentSlot)
        {
            CurrentSlot = null;
            CurrentName = null;
        }

        return true;
    }

    /// <summary>The first free "Journey N", or "Name's Journey (N)" for a character with a chosen name.</summary>
    private static string NextName()
    {
        var used = new HashSet<string>();
        foreach (SaveSlotInfo save in ListSaves())
        {
            used.Add(save.Name);
        }

        string chosen = GameSession.Appearance.name;
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            string journey = $"{CharacterAppearance.CleanName(chosen)}'s Journey";
            int count = 1;
            while (used.Contains(count == 1 ? journey : $"{journey} {count}"))
            {
                count++;
            }

            return count == 1 ? journey : $"{journey} {count}";
        }

        int number = used.Count + 1;
        while (used.Contains($"Journey {number}"))
        {
            number++;
        }

        return $"Journey {number}";
    }

    /// <summary>Reads the save being played, or the newest one, without loading it. False, with a reason, when it can't be used.</summary>
    public static bool TryReadSave(out SaveData data, out string problem)
    {
        return TryReadSave(SlotToLoad(), out data, out problem);
    }

    /// <summary>Reads a save without loading it, for showing what it holds. False, with a reason, when it can't be used.</summary>
    public static bool TryReadSave(string slot, out SaveData data, out string problem)
    {
        if (!Storage.TryRead(slot, out data, out problem))
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

    // Quick load: back to the save being played, or else the newest.
    private static string SlotToLoad()
    {
        return CurrentSlot != null && Storage.Exists(CurrentSlot) ? CurrentSlot : LatestSave()?.Slot ?? SaveStorage.DefaultSlot;
    }

    /// <summary>Loads the save being played, or the newest one (quick load).</summary>
    public static bool TryLoad(IContentLookup content, string fallbackScene, out string problem)
    {
        return TryLoad(SlotToLoad(), content, fallbackScene, out problem);
    }

    /// <summary>
    /// Loads a save and goes to its scene, or to fallbackScene if that scene no longer exists. False, with a reason
    /// for the player, when there is nothing to load or a raid is under way. The game then carries on in that save.
    /// </summary>
    public static bool TryLoad(string slot, IContentLookup content, string fallbackScene, out string problem)
    {
        problem = CheckLoad();
        if (problem != null)
        {
            return false;
        }

        if (!TryReadSave(slot, out SaveData data, out problem))
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
        CurrentSlot = slot;
        CurrentName = SaveSlotInfo.NameFor(slot, data);
        Go(scene, notice, "Returning to your journey");
        return true;
    }

    /// <summary>Forgets the current game and starts over in the given scene. Every save stays: the new game makes its own.</summary>
    public static bool TryStartNewGame(string scene, out string problem) => TryStartNewGame(scene, null, out problem);

    /// <summary>Starts over in the given scene as the character made on the character creation screen.</summary>
    public static bool TryStartNewGame(string scene, CharacterAppearance look, out string problem)
    {
        problem = loading ? "The game is already loading." : !CanLoad(scene) ? $"The scene '{scene}' isn't in the build." : null;
        if (problem != null)
        {
            return false;
        }

        GameSession.StartNewGame(look);
        CurrentSlot = null;
        CurrentName = null;
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
        CurrentSlot = null;
        CurrentName = null;
    }
}
