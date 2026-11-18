using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The fourth round of guards (Docs/BugPatterns.md): bugs that came from state no one owned, put together again at each place
/// that needed it. Each kind now has one owner, and these fail on code that goes around it:
/// <list type="bullet">
/// <item>where the player is (at home, on a raid, in a friend's Haven): asked of <see cref="Place"/>, not built from its parts;</item>
/// <item>the player's controls: held in an owner's name through <see cref="PlayerControl"/>, never switched by hand;</item>
/// <item>a stack: copied from itself (<see cref="ItemStack.WithCount"/> and the other With*), never re-made from its item;</item>
/// <item>what a game being played keeps in statics: forgotten when another begins (every ForgetGame called by GameSession);</item>
/// <item>time away: counted on the day's own clock (<see cref="DayNightCycle.SecondsAwaySince"/>), never on one a saved state picks.</item>
/// </list>
/// </summary>
public sealed class SharedStateTests
{
    // ----- Where the player is -----

    private static readonly Dictionary<string, Regex> PlaceParts = new Dictionary<string, Regex>
    {
        ["GameSession.ActiveRaid"] = new Regex(@"GameSession\.ActiveRaid\b"),
        ["RaidDirector.Current"] = new Regex(@"RaidDirector\.Current\b"),
        ["HavenVisit.IsVisiting"] = new Regex(@"HavenVisit\.IsVisiting\b"),
        ["HavenVisit.MayUseStash"] = new Regex(@"HavenVisit\.MayUseStash\b"),
        ["HomePortal.Current"] = new Regex(@"HomePortal\.Current\b"),
    };

    // "The raid is decided" or "still running", from the director by hand (Place.RaidDecided, Place.RaidRunning).
    private static readonly Regex RaidStateByHand =
        new Regex(@"RaidDirector\.Current\s*(?:!=|==)\s*null\s*(?:&&|\|\|)\s*!?RaidDirector\.Current\.Session\.IsOver");

    // Lines that read two of the parts for something other than where the player is, and why (file: a piece of the line).
    private static readonly Dictionary<string, string> PlacePartsKept = new Dictionary<string, string>
    {
        ["Multiplayer/CreaturePuppets.cs: bool settled = raid ? RaidDirector.Current != null"] =
            "whether a scene's creatures have settled: a raid's once its director runs it, a Haven's once the visit's copy is up",
        ["Multiplayer/HavenShare.cs: if (!travel && HavenVisit.IsVisiting && RaidDirector.Current != null)"] =
            "a guest leaving mid-raid ends the director it checks for (RaidDirector.Current.Abandon)",
        ["Raids/Map/RaidMap.cs: RaidDefinition raid = RaidDirector.Current != null"] =
            "which raid's map this is, from the director or the session: not where the player is",
    };

    [Test]
    public void WhereThePlayerIs_IsAskedOfPlace()
    {
        var built = new List<string>();
        var kept = new HashSet<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            if (file == "Core/Place.cs")
            {
                continue;
            }

            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string code = CodePart(lines[i]);
                int parts = PlaceParts.Values.Count(part => part.IsMatch(code));
                if (parts < 2 && !RaidStateByHand.IsMatch(code))
                {
                    continue;
                }

                string allowed = PlacePartsKept.Keys.FirstOrDefault(key => key.StartsWith(file + ": ") && code.Contains(key.Substring(file.Length + 2)));
                if (allowed != null)
                {
                    kept.Add(allowed);
                }
                else
                {
                    built.Add($"{file}:{i + 1}: {code.Trim()}");
                }
            }
        }

        CollectionAssert.IsEmpty(built, "ask Place (OnRaid, InRaidScene, RaidRunning, RaidDecided, AtHome, InFriendsHaven, FollowingAHost, " +
            "InAHaven, StashUsable), or add a meaning there: \"at home\" built by hand had four meanings");
        CollectionAssert.IsEmpty(PlacePartsKept.Keys.Except(kept).ToList(), "these lines are gone or changed: take them off the list");
    }

    // ----- The player's controls -----

    [Test]
    public void ThePlayersControls_AreHeldInAnOwnersName()
    {
        var switched = new List<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            if (file == "Player/PlayerControl.cs")
            {
                continue;
            }

            // The handler, by whatever name the script gives it: a field, a local, an out variable, or a GetComponent result.
            var names = new HashSet<string>();
            foreach (Match declared in Regex.Matches(source, @"\bPlayerInputHandler\s+(\w+)\b"))
            {
                names.Add(declared.Groups[1].Value);
            }

            foreach (Match declared in Regex.Matches(source, @"\bvar\s+(\w+)\s*=[^;\n]*<PlayerInputHandler>\s*\(\)"))
            {
                names.Add(declared.Groups[1].Value);
            }

            foreach (Match declared in Regex.Matches(source, @"<PlayerInputHandler>\s*\(\s*out\s+var\s+(\w+)"))
            {
                names.Add(declared.Groups[1].Value);
            }

            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string code = CodePart(lines[i]);
                bool direct = Regex.IsMatch(code, @"<PlayerInputHandler>\s*\(\)\s*\.enabled\s*=(?!=)");
                if (direct || names.Any(name => Regex.IsMatch(code, $@"\b{name}\s*\.\s*enabled\s*=(?!=)")))
                {
                    switched.Add($"{file}:{i + 1}: {code.Trim()}");
                }
            }
        }

        CollectionAssert.IsEmpty(switched, "hold the controls in your own name (PlayerControl.Hold/Release/Set): switched by hand, one system gave back " +
            "what another still held (a downed player with full controls, a cinematic the scene change ended)");
    }

    // The Player map's keys as they are now, put back afterwards: holding the controls switches them (GameInput.SetPlayerKeys), and in
    // edit mode no OnEnable gives them back, so a test left them off for every test after it.
    private static System.Action KeepPlayerMap()
    {
        InputActionMap map = GameInput.PlayerMap;
        if (map == null)
        {
            return () => { };
        }

        bool[] on = map.actions.Select(action => action.enabled).ToArray();
        return () =>
        {
            GameInput.SetPlayerKeys(false);
            for (int i = 0; i < on.Length && i < map.actions.Count; i++)
            {
                if (on[i])
                {
                    map.actions[i].Enable();
                }
                else
                {
                    map.actions[i].Disable();
                }
            }
        };
    }

    [Test]
    public void TheControls_ComeBack_OnlyWhenNobodyHoldsThem()
    {
        var player = new GameObject("Held Player");
        var cinematic = new GameObject("A Cinematic");
        System.Action putBack = KeepPlayerMap();
        try
        {
            PlayerInputHandler input = player.AddComponent<PlayerInputHandler>();
            object scene = new object();
            Assert.IsTrue(input.enabled);

            PlayerControl.Hold(player, scene);
            PlayerControl.Hold(player, cinematic);
            Assert.IsFalse(input.enabled, "held");

            PlayerControl.Release(player, scene);
            Assert.IsFalse(input.enabled, "the cinematic still holds them");
            Assert.IsTrue(PlayerControl.HeldBy(player, cinematic));

            PlayerControl.Release(player, scene);
            Assert.IsFalse(input.enabled, "letting go twice lets go of nothing more");

            // A holder destroyed without letting go holds nothing: the next change lets go of it.
            Object.DestroyImmediate(cinematic);
            PlayerControl.Set(player, scene, false);
            Assert.IsTrue(input.enabled, "nobody holds them");
            Assert.IsFalse(PlayerControl.HeldOn(player));
        }
        finally
        {
            Object.DestroyImmediate(player);
            if (cinematic != null)
            {
                Object.DestroyImmediate(cinematic);
            }

            putBack();
        }
    }

    // ----- Stacks -----

    [Test]
    public void AStack_IsCopiedFromItself_NeverRemadeFromItsItem()
    {
        var remade = new List<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            if (file == "Items/ItemAmount.cs")
            {
                continue;
            }

            // Stacks by name in this script: ItemStack x, ItemStack[] xs, List<ItemStack> xs, out/in/ref ItemStack x.
            var names = new HashSet<string>();
            foreach (Match declared in Regex.Matches(source, @"\b(?:ItemStack(?:\[\])?|(?:I?List|IReadOnlyList)<ItemStack>)\s+(\w+)\b"))
            {
                names.Add(declared.Groups[1].Value);
            }

            // var locals that are stacks: from a slot, a lure's or a drop's stack.
            foreach (Match declared in Regex.Matches(source, @"\bvar\s+(\w+)\s*=\s*[^;\n]*(?:GetSlot\s*\(|\.Stack\b)"))
            {
                names.Add(declared.Groups[1].Value);
            }

            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string code = CodePart(lines[i]);
                foreach (Match copy in Regex.Matches(code, @"new\s+ItemStack\s*\(\s*(\w+)(?:\s*\[[^\]]*\])?\s*\.\s*Item\s*,"))
                {
                    if (names.Contains(copy.Groups[1].Value))
                    {
                        remade.Add($"{file}:{i + 1}: {code.Trim()}");
                    }
                }

                if (Regex.IsMatch(code, @"new\s+ItemStack\s*\([^,;]*GetSlot\s*\([^)]*\)\s*\.\s*Item\s*,"))
                {
                    remade.Add($"{file}:{i + 1}: {code.Trim()}");
                }
            }
        }

        CollectionAssert.IsEmpty(remade, "copy a stack from itself (stack.WithCount(n), WithSize, WithDurability...): re-made from its item, it " +
            "lost its inscription, size, quality and maker (Docs/BugPatterns.md)");
    }

    [Test]
    public void SplittingAndMerging_KeepWhatAStackIs()
    {
        var parchment = ScriptableObject.CreateInstance<ItemDefinition>();
        var enchantment = ScriptableObject.CreateInstance<EnchantmentDefinition>();
        try
        {
            var data = new SerializedObject(parchment);
            data.FindProperty("stackable").boolValue = true;
            data.FindProperty("maximumStack").intValue = 20;
            data.FindProperty("inscription").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();

            var bag = new ItemContainer(4, 1);
            bag.SetSlot(0, new ItemStack(parchment, 6, 1f, 0, enchantment, size: 12f));
            Assert.IsTrue(bag.SplitHalf(0));
            for (int slot = 0; slot < 2; slot++)
            {
                Assert.AreEqual(3, bag.GetSlot(slot).Count);
                Assert.AreSame(enchantment, bag.GetSlot(slot).Enchantment, $"half {slot} keeps its inscription");
                Assert.AreEqual(12f, bag.GetSlot(slot).Size, $"half {slot} keeps its size");
            }

            Assert.IsTrue(bag.MoveTo(1, bag, 0));
            Assert.AreEqual(6, bag.GetSlot(0).Count);
            Assert.AreSame(enchantment, bag.GetSlot(0).Enchantment, "merged, still inscribed");

            // Blank ones don't join the inscribed stack (and make it blank): they take a slot of their own.
            Assert.AreEqual(2, bag.Add(parchment, 2));
            Assert.AreEqual(6, bag.GetSlot(0).Count);
            Assert.AreSame(enchantment, bag.GetSlot(0).Enchantment);
            Assert.AreEqual(2, bag.GetSlot(1).Count);
            Assert.IsNull(bag.GetSlot(1).Enchantment);
        }
        finally
        {
            Object.DestroyImmediate(parchment);
            Object.DestroyImmediate(enchantment);
        }
    }

    // ----- Fifth round: one switch for the Player map, one copy of a save slot, one cleanup for a failed load, a guard on every text field -----

    [Test]
    public void ThePlayerMap_IsSwitchedOnlyByGameInput()
    {
        var switched = new List<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            if (file == "Core/GameInput.cs")
            {
                continue;
            }

            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string code = CodePart(lines[i]);
                if (Regex.IsMatch(code, @"GameInput\.\w+\s*\??\s*\.\s*(Enable|Disable)\s*\(\s*\)"))
                {
                    switched.Add($"{file}:{i + 1}: {code.Trim()}");
                }
            }
        }

        CollectionAssert.IsEmpty(switched, "the Player map's keys are switched in one place (GameInput.SetPlayerKeys; a hold keeps a few through " +
            "PlayerControl.Kept, a suspension through SuspendGameplay(owner, keep)): switched by hand, the Nøkken's struggle key was off for the whole grab");
    }

    // Slots made field by field where the save mapper doesn't (and why).
    private static readonly Dictionary<string, string> SlotsMadeByHand = new Dictionary<string, string>
    {
        ["Multiplayer/StashShare.cs"] = "FromKey: a slot read back from a kind's key, every field the key holds",
    };

    [Test]
    public void ASaveSlot_IsMadeBySaveMapper_OrCopiedWithWith()
    {
        var madeByHand = new HashSet<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            if (file != "Save/SaveMapper.cs" && file != "Save/SaveData.cs" && Regex.IsMatch(source, @"new\s+SlotSaveData\s*(\{|\()"))
            {
                madeByHand.Add(file);
            }
        }

        CollectionAssert.IsEmpty(madeByHand.Except(SlotsMadeByHand.Keys).ToList(), "a slot comes from SaveMapper.SlotOf(stack) or slot.With(slot, count): " +
            "made field by field, a co-op stash change left out the fish's size");
        CollectionAssert.IsEmpty(SlotsMadeByHand.Keys.Except(madeByHand).ToList(), "these no longer make slots by hand: take them off the list");
    }

    // A load whose result is tested without ifItFails, and why that is enough.
    private static readonly Dictionary<string, string> LoadsTestedOnly = new Dictionary<string, string>
    {
        ["UI/PauseMenu.cs"] = "the menu's leaving undoes itself on SceneTransition.Abandoned (OnLeavingAbandoned), raised on every break-off",
    };

    [Test]
    public void ALoadThatCanFail_SaysWhatUndoesIt()
    {
        var tested = new HashSet<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            foreach (string line in source.Split('\n'))
            {
                string code = CodePart(line);
                if (Regex.IsMatch(code, @"if\s*\(\s*!?\s*SceneLoader\.Load\(") && !code.Contains("ifItFails"))
                {
                    tested.Add(file);
                }
            }
        }

        CollectionAssert.IsEmpty(tested.Except(LoadsTestedOnly.Keys).ToList(), "pass SceneLoader.Load's ifItFails: a load can also break off after it " +
            "started, and undoing only a refusal left a player frozen at the portal with a raid that never came");
        CollectionAssert.IsEmpty(LoadsTestedOnly.Keys.Except(tested).ToList(), "these no longer test a load's result: take them off the list");
    }

    // ----- What a game being played keeps in statics -----

    [Test]
    public void EveryForgetGame_IsCalledWhenAnotherGameBegins()
    {
        // A static outlives a scene and is not reset by quitting to the menu (only by starting the program, or Play in the Editor,
        // where the bugs therefore never showed): a game left by the menu kept its quest clock, its builders' names and its rowing
        // lesson into the next game played. What belongs to the game is forgotten in a ForgetGame, called by GameSession.Reset.
        string session = File.ReadAllText("Assets/_Project/Scripts/Core/GameSession.cs");
        var unwired = new List<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            foreach (Match forget in Regex.Matches(source, @"public\s+static\s+void\s+ForgetGame\s*\(\s*\)"))
            {
                MatchCollection classes = Regex.Matches(source.Substring(0, forget.Index), @"\bclass\s+(\w+)");
                string owner = classes.Count > 0 ? classes[classes.Count - 1].Groups[1].Value : Path.GetFileNameWithoutExtension(path);
                if (!Regex.IsMatch(session, @"\b" + owner + @"\.ForgetGame\s*\(\s*\)"))
                {
                    unwired.Add(owner + " (" + Path.GetFileName(path) + ")");
                }
            }
        }

        CollectionAssert.IsEmpty(unwired, "a ForgetGame no one calls: add it to GameSession.Reset, or the next game played keeps it");
    }

    [Test]
    public void TimeAway_IsCountedOnTheDaysOwnClock()
    {
        // The day was moved to game time (an hour in the pause menu on a raid was two dawns at home) and the nodes' regrowth, which
        // worked out its own time away, kept real time: the pause menu still grew every node back. One clock, DayNightCycle's.
        var ownClocks = new List<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            if (file == "Environment/DayNightCycle.cs" || !source.Contains("CaptureState("))
            {
                continue;
            }

            foreach (string line in source.Split('\n'))
            {
                if (Regex.IsMatch(CodePart(line), @"\bTime\.(?:realtimeSinceStartup\w*|timeAsDouble|unscaledTime\w*)"))
                {
                    ownClocks.Add(file + ": " + line.Trim());
                }
            }
        }

        CollectionAssert.IsEmpty(ownClocks, "a saved state counting time on a clock of its own: keep DayNightCycle.AwayStamp with " +
            "DayNightCycle.Run and read DayNightCycle.SecondsAwaySince");
    }

    // Plain object keys that own no lease, and why.
    private static readonly Dictionary<string, string> UnnamedKeys = new Dictionary<string, string>
    {
        ["UI/ResourceInventoryPanel.cs"] = "the bag window's tabs (all, other): keys of a list, never a lease's owner",
    };

    [Test]
    public void ALeasesOwner_HasAName()
    {
        // LeaseWatch tells a lease held too long by its owner's name; a plain new object() read "Object" (80 of the playtests' Stuck
        // rows), so nobody could tell an agent's probe from a finishing blow left on. A static owner is a LeaseKey with a name.
        var unnamed = new List<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            if (!UnnamedKeys.ContainsKey(file) && Regex.IsMatch(source, @"static\s+readonly\s+object\s+\w+\s*=\s*new\s+object\s*\(\s*\)"))
            {
                unnamed.Add(file);
            }
        }

        CollectionAssert.IsEmpty(unnamed, "a static key made as new object(): make it a LeaseKey with a name (or list it if it owns no lease)");
    }

    // Text fields typed in where no gameplay key can act, and why.
    private static readonly Dictionary<string, string> UnguardedFields = new Dictionary<string, string>
    {
        ["UI/Options/CharacterCreationScreen.cs"] = "the main menu's new game: no player, no gameplay keys",
    };

    [Test]
    public void EveryTextField_HoldsTheGameplayKeysWhileTyped()
    {
        var unguarded = new HashSet<string>();
        foreach ((string path, string source) in ConventionTests.RuntimeScripts())
        {
            string file = path.Substring("Assets/_Project/Scripts/".Length);
            if (source.Contains("AddComponent<TMP_InputField>") && !source.Contains("KitSearchGuard") && !source.Contains("SuspendGameplay"))
            {
                unguarded.Add(file);
            }
        }

        CollectionAssert.IsEmpty(unguarded.Except(UnguardedFields.Keys).ToList(), "guard a text field (KitSearchGuard.On(field), or SuspendGameplay while typing): " +
            "the cheat window's search flipped the camera at every \"v\"");
        CollectionAssert.IsEmpty(UnguardedFields.Keys.Except(unguarded).ToList(), "these are guarded now: take them off the list");
    }

    [Test]
    public void AHoldsKeptKeys_WorkOnlyWhileEveryHolderKeepsThem()
    {
        var player = new GameObject("Held Player");
        System.Action putBack = KeepPlayerMap();
        try
        {
            player.AddComponent<PlayerInputHandler>();
            object downed = new object();
            object cinematic = new object();
            PlayerControl.Hold(player, downed, PlayerControl.Kept.Move);
            Assert.IsTrue(GameInput.Move == null || GameInput.Move.enabled, "down: the crawl's key works");

            PlayerControl.Hold(player, cinematic);
            Assert.IsTrue(GameInput.Move == null || !GameInput.Move.enabled, "a cinematic holds too: no crawling under it");

            PlayerControl.Release(player, cinematic);
            Assert.IsTrue(GameInput.Move == null || GameInput.Move.enabled, "the cinematic is over: crawling again");
        }
        finally
        {
            Object.DestroyImmediate(player);
            putBack();
        }
    }

    // The line without a trailing comment (RuntimeScripts drops whole comment lines). Strings holding "//" are rare in these lines.
    private static string CodePart(string line)
    {
        int comment = line.IndexOf("//", System.StringComparison.Ordinal);
        return comment >= 0 ? line.Substring(0, comment) : line;
    }
}
