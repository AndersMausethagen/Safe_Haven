using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Every guard that reads the game's scripts is seen to fail (Docs/BugPatterns.md, sixth round): its bad case is planted as one more
/// runtime script (ConventionTests.Planted), and the guard must turn red on it and stay green without it. A scan whose pattern no longer
/// matches the code passes for ever and looks exactly like a clean codebase: tonight a guard the test framework never ran was green, and
/// the smoke test listened where the renderer's errors never came.
/// </summary>
public sealed class GuardCanaryTests
{
    private const string PlantedPath = "Assets/_Project/Scripts/Canary/Planted.cs";

    private static IEnumerable<(string guard, Action run, string bad)> Canaries()
    {
        var state = new SharedStateTests();
        var convention = new ConventionTests();
        var engine = new EngineHazardTests();

        yield return ("where the player is, built by hand", state.WhereThePlayerIs_IsAskedOfPlace,
            "public class Planted { bool F() => GameSession.ActiveRaid != null && HavenVisit.IsVisiting; }");
        yield return ("the controls switched by hand", state.ThePlayersControls_AreHeldInAnOwnersName,
            "public class Planted { PlayerInputHandler handler; void F() { handler.enabled = false; } }");
        yield return ("a stack re-made from its item", state.AStack_IsCopiedFromItself_NeverRemadeFromItsItem,
            "public class Planted { void F(ItemStack stack) { var copy = new ItemStack(stack.Item, 2); } }");
        yield return ("the Player map switched by hand", state.ThePlayerMap_IsSwitchedOnlyByGameInput,
            "public class Planted { void F() { GameInput.PlayerMap.Disable(); } }");
        yield return ("a save slot made field by field", state.ASaveSlot_IsMadeBySaveMapper_OrCopiedWithWith,
            "public class Planted { object F() => new SlotSaveData { }; }");
        yield return ("a load tested with no cleanup", state.ALoadThatCanFail_SaysWhatUndoesIt,
            "public class Planted { void F() { if (SceneLoader.Load(\"SafeHaven\")) { } } }");
        yield return ("a ForgetGame nobody calls", state.EveryForgetGame_IsCalledWhenAnotherGameBegins,
            "public static class Planted { public static void ForgetGame() { } }");
        yield return ("time away on a clock of its own", state.TimeAway_IsCountedOnTheDaysOwnClock,
            "public class Planted { public string CaptureState() => Time.realtimeSinceStartup.ToString(); }");
        yield return ("a lease owner with no name", state.ALeasesOwner_HasAName,
            "public static class Planted { private static readonly object Key = new object(); }");
        yield return ("a text field with no guard", state.EveryTextField_HoldsTheGameplayKeysWhileTyped,
            "public class Planted { void F(UnityEngine.GameObject go) { go.AddComponent<TMP_InputField>(); } }");

        yield return ("a singleton reached as Instance", convention.EverySingleton_IsReachedThroughCurrent,
            "public class Planted { public static Planted Instance; }");
        yield return ("a singleton never reset", convention.EverySingleton_IsResetWhenPlayStarts,
            "public class Planted : UnityEngine.MonoBehaviour { public static Planted Current { get; private set; } }");
        yield return ("a static collection never cleared", convention.EveryStaticCollection_IsClearedWhenPlayStarts,
            "public class Planted { private static readonly List<int> things = new List<int>(); }");
        yield return ("a blow from the shadows worked out by hand", convention.BlowsFromTheShadows_AreWorkedOutInPlayerBlows,
            "public class Planted { float F() => PlayerProficiencies.AmbushScale(null); }");
        yield return ("the old settlement name", convention.TheHavenHere_IsRead_AndOnesOwn_IsChanged,
            "public class Planted { object F() => GameSession.Settlement; }");
        yield return ("a static event never left", convention.EveryStaticEventSubscription_IsUndone,
            "public class Planted { void OnEnable() { GameEvents.Raised += OnRaised; } void OnRaised(GameEvent e) { } }");

        yield return ("a mesh made and never destroyed", engine.WhatIsMadeInCode_IsDestroyedByItsOwner,
            "public class Planted { UnityEngine.Mesh mesh = new Mesh(); }");
        yield return ("a subscription to a longer-lived system never undone", engine.EverySubscriptionToALongerLivedSystem_IsUndone,
            "public class Planted { void OnEnable() { QuestManager.Current.Changed += OnChanged; } void OnChanged() { } }");
    }

    [Test]
    public void EveryGuard_FailsOnItsBadCase_AndOnlyOnIt()
    {
        var blind = new List<string>();
        var red = new List<string>();
        foreach ((string guard, Action run, string bad) in Canaries())
        {
            ConventionTests.Planted.Clear();
            if (Fails(run, out string why))
            {
                red.Add($"{guard}: fails on the code as it is ({why})");
                continue;
            }

            ConventionTests.Planted.Add((PlantedPath, bad));
            try
            {
                if (!Fails(run, out _))
                {
                    blind.Add(guard);
                }
            }
            finally
            {
                ConventionTests.Planted.Clear();
            }
        }

        CollectionAssert.IsEmpty(red, "a guard that is red already: fix that first");
        CollectionAssert.IsEmpty(blind, "these guards stayed green with their bad case planted: they can't see what they are for");
    }

    private static bool Fails(Action run, out string why)
    {
        try
        {
            run();
            why = null;
            return false;
        }
        catch (AssertionException failed)
        {
            why = failed.Message.Split('\n')[0];
            return true;
        }
        catch (Exception broken)
        {
            why = broken.GetType().Name + ": " + broken.Message;
            return true;
        }
    }
}
