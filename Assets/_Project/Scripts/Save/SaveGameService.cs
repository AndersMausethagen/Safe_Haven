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

    private static string raidUnderWay;

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
        // Behind a loading screen the old scene is still alive and half recorded.
        if (loading || SceneTransition.IsBusy)
        {
            return "The game is still loading.";
        }

        // The raid's scene stays a moment after its outcome (ActiveRaid is cleared then): a save there would start a raid.
        if (Place.OnRaid)
        {
            return RaidRefusal;
        }

        if (PrologueDirector.Current != null && !PrologueDirector.Current.CanSave)
        {
            return "Finish this prologue encounter first. Safe phases save checkpoints; loading restarts the current phase.";
        }

        return Player.Current == null || Player.Current.Persistence == null ? "There is nothing to save here." : null;
    }

    /// <summary>
    /// Why the player can't save right now (F5, the Escape menu, the saves screen), or null: CheckSave, and not mid-conversation
    /// or mid-cinematic, where a story has begun but not done what it is for and a once-only one wouldn't start again after
    /// loading. Not in CheckSave itself: the prologue's checkpoints are taken while people talk. One rule for every way the
    /// player saves (review 2026-11-16: only F5 had it).
    /// </summary>
    public static string CheckPlayerSave()
    {
        string problem = CheckSave();
        if (problem != null)
        {
            return problem;
        }

        return DialogueBox.IsOpen || CinematicScreen.IsPlaying || InGameCinematic.IsPlaying ? "Finish the conversation first." : null;
    }

    /// <summary>Why a save can't be loaded right now, in words for the player, or null when one can.</summary>
    public static string CheckLoad()
    {
        // A load during a scene change would replace the game while the change carries on to its own scene.
        if (loading || SceneTransition.IsBusy)
        {
            return "The game is already loading.";
        }

        if (HavenVisit.IsVisiting)
        {
            return "You're in a friend's Haven. Leave the session first (Escape menu).";
        }

        // Hosting: nothing would send the loaded world to the friends, who'd go on in the old Haven while the host's changed
        // under them (and their changes landed in the loaded one).
        if (CoopSession.Current != null && CoopSession.Current.IsHost && Player.Current != null)
        {
            return "Your Haven is open to friends. Close it to them first (Escape menu).";
        }

        if (CinematicScreen.IsPlaying)
        {
            return "Let the scene finish first.";
        }

        return Place.OnRaid ? RaidRefusal : null;
    }

    /// <summary>
    /// The save a raid sets out from (GDD §20.6), so a crash has something to go back to: the sparks and runes are paid, the gear is
    /// in the bag and the player is at the portal. Nothing is saved during the raid, so a raid that never comes home simply didn't
    /// happen: its haul is gone and nothing can be doubled. That is the policy, and the player is told when they next load.
    /// </summary>
    public static void SaveBeforeRaid(RaidDefinition raid)
    {
        if (!ShouldAutosave)
        {
            return;
        }

        raidUnderWay = raid.DisplayName;
        if (!TrySave(out string problem))
        {
            Debug.LogWarning("Couldn't save before the raid: " + problem);
        }

        raidUnderWay = null;
    }

    /// <summary>
    /// Writes the raid's end at once (RaidDirector.Finish), before the results screen: home in the Haven with what was kept, at the
    /// portal. The save on coming home (SaveOnceHome) waits for a scene with a player, and the results screen has none: a crash or
    /// a quit there lost an extraction's haul, and the next load said the raid was cut short. Once home it is saved again, with
    /// what came home with the player (a cart, a horse).
    /// </summary>
    public static void SaveAtRaidEnd(PlayerSnapshot keeps)
    {
        if (!ShouldAutosave || keeps == null || CurrentSlot == null)
        {
            return;
        }

        try
        {
            // A guest (following the host) writes their own journey with the character as the raid left it, never the host's
            // world they stand in: skipping the save left the launch save, and a quit or crash after a fall undid it.
            SaveData data = FromKept(keeps);
            if (HavenVisit.IsVisiting)
            {
                data = HavenVisit.ForOwnSave(data);
            }

            data.raidUnderWay = null;
            data.arriving = Arriving();
            Storage.Write(CurrentSlot, data);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Couldn't save the raid's end: " + exception.Message);
        }
    }

    /// <summary>The game as a raid's end leaves it: the character as <paramref name="keeps"/> (what the raid let them keep), home
    /// in the Haven, at the portal.</summary>
    public static SaveData FromKept(PlayerSnapshot keeps)
    {
        SaveData data = SaveMapper.ToSaveData(keeps, GameSession.Stash, HavenVisit.HavenScene, null, DateTime.UtcNow,
            GameSession.World, GameSession.Quests, GameSession.HereItself, GameSession.Playtest, GameSession.Progress);
        data.displayName = CurrentName;
        data.appearance = GameSession.Appearance.Clone();
        data.raidUnderWay = null;
        return data;
    }

    /// <summary>Saves as soon as the player stands somewhere that can be saved: home from a raid, past its results screen. One
    /// save waits, however often it is asked for.</summary>
    public static void SaveOnceHome()
    {
        if (!ShouldAutosave)
        {
            return;
        }

        SceneTransition.SceneReady -= SaveOnReady;
        SceneTransition.SceneReady += SaveOnReady;
        IsSaveWaitingForHome = true;
    }

    /// <summary>True while a save waits for the player to stand somewhere it can be made (SaveOnceHome).</summary>
    public static bool IsSaveWaitingForHome { get; private set; }

    /// <summary>
    /// Forgets the save waiting for home when another journey begins (a save loaded, a new game): it belonged to the one left. Quit
    /// to the menu from a raid, it waited through the menu (no player there) and was then written in the next game played. Not on
    /// TryPlay, which goes on with the same journey: coming home from a friend's Haven, it is what saves the guest's own.
    /// </summary>
    private static void ForgetWaitingSave()
    {
        SceneTransition.SceneReady -= SaveOnReady;
        IsSaveWaitingForHome = false;
    }

    private static void SaveOnReady(Scene scene)
    {
        if (CheckSave() != null)
        {
            return;
        }

        ForgetWaitingSave();
        if (!TrySave(out string problem))
        {
            Debug.LogWarning("Couldn't save on coming home: " + problem);
        }
    }

    /// <summary>Saves again once what came home with the player (a cart, a horse) has been set down, after the save on
    /// coming home, which is written before they arrive.</summary>
    public static void SaveAfterArrival()
    {
        if (ShouldAutosave && RaidDirector.Current == null && CheckSave() == null && !TrySave(out string problem))
        {
            Debug.LogWarning("Couldn't save after arriving: " + problem);
        }
    }

    // A test session in the Editor, playing a scene with no game loaded, doesn't leave saves behind for Continue to find.
    private static bool ShouldAutosave => Application.isPlaying && (CurrentSlot != null || !Application.isEditor);

    /// <summary>What the player is told on loading a save, in words for them.</summary>
    public static string NoticeFor(SaveData data, int missing)
    {
        if (!string.IsNullOrEmpty(data.raidUnderWay))
        {
            return $"Your raid into {data.raidUnderWay} was cut short. You are back at the portal with what you took in. Nothing from that raid came home.";
        }

        return missing > 0 ? $"Game loaded. {missing} saved entries no longer exist and were skipped." : "Game loaded.";
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

        // Crafts under way go on through a save at home (F5, autosaves stopped them with "You stop crafting" each time); in a
        // friend's Haven they stop as before, the save going into another journey.
        SaveData data = Capture(keepCrafts: !HavenVisit.IsVisiting);
        // In a friend's Haven (co-op) only the character is this player's: it goes into their own journey, whose world stays as they left it.
        if (HavenVisit.IsVisiting)
        {
            data = HavenVisit.ForOwnSave(data);
        }

        data.displayName = name;
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

    /// <summary>The game being played, as a save held in memory (nothing is written). Needs a player.</summary>
    /// <param name="forWorldOnly">Only the world is wanted (the Haven sent to a friend joining, HavenShare): the player's side
    /// is thrown away, so crafts under way go on instead of being stopped and refunded at every join.</param>
    /// <param name="keepCrafts">A save written to disk (F5, an autosave): crafts by hand under way go on, written into the save
    /// already paid, so loading it puts them back (HandCraftQueue.CaptureState). Every other capture is a handover elsewhere (a
    /// friend's Haven, home from one), where they stop and their materials go back first, or the copy would keep them paid
    /// for and never made.</param>
    public static SaveData Capture(bool forWorldOnly = false, bool keepCrafts = false)
    {
        PlayerPersistence player = Player.Current.Persistence;
        Player.Current.TryGetComponent(out HandCraftQueue crafts);
        if (!forWorldOnly && !keepCrafts && crafts != null)
        {
            crafts.Stop();
        }

        // Objects keep their own state; it is gathered into memory first, then copied out.
        SaveableEntity.CaptureAll();
        SaveData data = SaveMapper.ToSaveData(player.Capture(), GameSession.Stash, SceneManager.GetActiveScene().name, player.Pose, DateTime.UtcNow,
            GameSession.World, GameSession.Quests, GameSession.HereItself, GameSession.Playtest, GameSession.Progress);
        if (keepCrafts && crafts != null && data.player != null)
        {
            data.player.handCrafts = crafts.CaptureState();
        }

        data.displayName = CurrentName;
        data.appearance = GameSession.Appearance.Clone();
        data.raidUnderWay = raidUnderWay;
        data.arriving = Arriving();
        return data;
    }

    // The horse and the cart on their way home (from a raid's end until they are set down): kept in the save, they are not lost to a
    // quit or a crash in between. Never a guest's: what they lead stays in the host's Haven.
    private static ArrivingSaveData Arriving()
    {
        var arriving = new ArrivingSaveData();
        if (HavenVisit.IsVisiting)
        {
            return arriving;
        }

        if (HorseTravel.OnItsWayHome(out string horse, out string state, out float health))
        {
            arriving.horse = horse;
            arriving.horseState = state;
            arriving.horseHealth = health;
        }

        if (GameSession.PeekTransport(out GameSession.CarriedTransport cart) && cart.Definition != null && cart.Definition.Prefab != null)
        {
            arriving.cart = cart.Definition.Prefab.PrefabId;
            arriving.cartState = cart.State;
        }

        return arriving;
    }

    // A loaded save's horse and cart still on their way home arrive in the scene it goes to.
    private static void SendOnTheirWay(ArrivingSaveData arriving)
    {
        if (arriving == null || arriving.IsEmpty)
        {
            return;
        }

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (!string.IsNullOrEmpty(arriving.horse))
        {
            HorseTravel.ComeHome(arriving.horse, arriving.horseState, arriving.horseHealth);
        }

        SaveableEntity cart = !string.IsNullOrEmpty(arriving.cart) && content != null ? content.FindPrefab(arriving.cart) : null;
        if (cart != null && cart.TryGetComponent(out Transport transport) && transport.Definition != null)
        {
            GameSession.CarryTransport(new GameSession.CarriedTransport(transport.Definition, arriving.cartState));
        }
    }

    /// <summary>
    /// Plays a save held in memory (co-op: a friend's Haven, or coming home from one) as a loaded save is played, without
    /// changing which journey saving writes to. False, with a reason for the player, when it can't be played now.
    /// </summary>
    public static bool TryPlay(SaveData data, IContentLookup content, string caption, out string problem)
    {
        problem = loading || SceneTransition.IsBusy ? "The game is already loading." : GameSession.ActiveRaid != null ? RaidRefusal : null;
        if (problem != null)
        {
            return false;
        }

        if (!CanLoad(data.scene))
        {
            problem = $"The place '{data.scene}' can't be loaded.";
            return false;
        }

        GameSession.LoadGame(SaveMapper.FromSaveData(data, content));
        Go(data.scene, null, caption);
        return true;
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

        string notice = NoticeFor(data, game.MissingContent.Count);
        if (game.MissingContent.Count > 0)
        {
            Debug.LogWarning("The save mentions content that no longer exists and was skipped: " + string.Join(", ", game.MissingContent));
        }

        // A position only makes sense in the scene it was saved in.
        if (!sameScene)
        {
            game.Pose = null;
        }

        ForgetWaitingSave();
        GameSession.LoadGame(game);
        SendOnTheirWay(data.arriving);
        CurrentSlot = slot;
        CurrentName = SaveSlotInfo.NameFor(slot, data);
        Go(scene, notice, "Returning to your journey");
        if (!string.IsNullOrEmpty(data.raidUnderWay))
        {
            // Once told, the save stops saying so.
            SaveOnceHome();
        }

        return true;
    }

    /// <summary>Forgets the current game and starts over in the given scene. Every save stays: the new game makes its own.</summary>
    public static bool TryStartNewGame(string scene, out string problem) => TryStartNewGame(scene, null, out problem);

    /// <summary>Starts over in the given scene as the character made on the character creation screen.</summary>
    public static bool TryStartNewGame(string scene, CharacterAppearance look, out string problem)
    {
        problem = loading || SceneTransition.IsBusy ? "The game is already loading." : !CanLoad(scene) ? $"The scene '{scene}' isn't in the build." : null;
        if (problem != null)
        {
            return false;
        }

        ForgetWaitingSave();
        GameSession.StartNewGame(look);
        CurrentSlot = null;
        CurrentName = null;
        Go(scene, null, "A new journey begins");
        return true;
    }

    private static void Go(string scene, string notice, string caption)
    {
        // The player stays in the old scene until the new one is ready, so keep them still meanwhile.
        GameObject held = Player.Current != null ? Player.Current.gameObject : null;
        PlayerControl.Hold(held, Loading);

        loading = true;
        // However the load fails (refused now, or broken off later): SceneLoader.Load's ifItFails.
        void OnAbandoned()
        {
            if (pendingArrival != null)
            {
                SceneManager.sceneLoaded -= pendingArrival;
                pendingArrival = null;
            }

            loading = false;
            PlayerControl.Release(held, Loading);
        }

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
        SceneLoader.Load(scene, caption, ifItFails: OnAbandoned);
    }

    // The load's own hold on the player's controls while the next scene comes up (PlayerControl).
    private static readonly object Loading = new object();

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
        raidUnderWay = null;
        IsSaveWaitingForHome = false;
    }
}
