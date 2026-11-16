using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Guard 3 of Docs/BugPatterns.md: what a visitor may do in a friend's Haven is declared where each interaction is written
/// (InAFriendsHaven), and what is the host's alone is refused in one place (Interactable) instead of a check in each; what each
/// elite trick does on a friend's copy is declared too (OnAFriendsCopy).
/// </summary>
public sealed class InteractionRuleTests
{
    private static readonly Regex AnswersAnInteractable = new Regex(@"\.(?:Completed|Started)\.AddListener\(");
    private static readonly Regex Declared = new Regex(@"\[InAFriendsHaven\(HavenUse\.\w+");

    [Test]
    public void EveryInteraction_SaysWhatAVisitorMayDo()
    {
        var undeclared = new List<string>();
        int answering = 0;
        foreach (string file in Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            if (path.Contains("/Editor/"))
            {
                continue;
            }

            string source = File.ReadAllText(path);
            if (!AnswersAnInteractable.IsMatch(source))
            {
                continue;
            }

            answering++;
            if (!Declared.IsMatch(source))
            {
                undeclared.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        Assert.Greater(answering, 30, "the interactions were found");
        CollectionAssert.IsEmpty(undeclared, "say what a visitor may do with it in a friend's Haven: [InAFriendsHaven(HavenUse..., \"how\")]");
    }

    [InAFriendsHaven(HavenUse.HostOnly, "a test's thing, the host's alone")]
    private sealed class HostsAlone : MonoBehaviour
    {
    }

    private static void Visiting(bool visiting) => typeof(HavenVisit).GetProperty(nameof(HavenVisit.IsVisiting)).SetValue(null, visiting);

    [Test]
    public void WhatIsTheHostsAlone_IsRefusedToAVisitor_InOnePlace()
    {
        var thing = new GameObject("The host's thing");
        var player = new GameObject("Visitor");
        try
        {
            var interactable = thing.AddComponent<Interactable>();
            typeof(Interactable).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(interactable, null);
            thing.AddComponent<HostsAlone>();
            Assert.IsTrue(interactable.CanInteract(player, out _), "at home it is anyone's");

            Visiting(true);
            Assert.IsFalse(interactable.CanInteract(player, out string reason), "in a friend's Haven it is the host's");
            Assert.AreEqual(HavenVisit.Refusal, reason);
        }
        finally
        {
            Visiting(false);
            HavenVisit.Forget();
            Object.DestroyImmediate(thing);
            Object.DestroyImmediate(player);
        }
    }

    // Second round: a choice that is the host's says so where it is made, and every window greys it for a visitor, saying whose it is.
    [Test]
    public void AChoiceThatIsTheHosts_IsGreyedForAVisitor_WithWhose()
    {
        var trade = new MenuAction("Buy 5 Iron Ore", "10 coins", true, () => { }) { HostOnly = true };
        var talk = new MenuAction("Ask about the road", "", true, () => { });
        Assert.IsTrue(trade.Enabled, "at home it is the player's");
        try
        {
            Visiting(true);
            Assert.IsFalse(trade.Enabled);
            Assert.AreEqual(HavenVisit.Refusal, trade.Detail);
            Assert.IsTrue(talk.Enabled, "what isn't the host's stays open");
        }
        finally
        {
            Visiting(false);
            HavenVisit.Forget();
        }
    }

    [Test]
    public void EveryEliteTrick_SaysWhatItDoesOnAFriendsCopy()
    {
        IEnumerable<string> undeclared = typeof(EliteBehaviour).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(EliteBehaviour).IsAssignableFrom(type) && type.GetCustomAttribute<OnAFriendsCopyAttribute>() == null)
            .Select(type => type.Name);
        CollectionAssert.IsEmpty(undeclared, "an elite trick says what it does on a friend's copy: [OnAFriendsCopy(CopyRule..., \"how\")]");
    }
}
