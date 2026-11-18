using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The fourth round of guards (Docs/BugPatterns.md): bugs that came from state no one owned, put together again at each place
/// that needed it. Each kind now has one owner, and these fail on code that goes around it:
/// <list type="bullet">
/// <item>where the player is (at home, on a raid, in a friend's Haven): asked of <see cref="Place"/>, not built from its parts;</item>
/// <item>the player's controls: held in an owner's name through <see cref="PlayerControl"/>, never switched by hand;</item>
/// <item>a stack: copied from itself (<see cref="ItemStack.WithCount"/> and the other With*), never re-made from its item.</item>
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

    [Test]
    public void TheControls_ComeBack_OnlyWhenNobodyHoldsThem()
    {
        var player = new GameObject("Held Player");
        var cinematic = new GameObject("A Cinematic");
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

    // The line without a trailing comment (RuntimeScripts drops whole comment lines). Strings holding "//" are rare in these lines.
    private static string CodePart(string line)
    {
        int comment = line.IndexOf("//", System.StringComparison.Ordinal);
        return comment >= 0 ? line.Substring(0, comment) : line;
    }
}
