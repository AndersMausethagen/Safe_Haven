using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Guard 6 of Docs/BugPatterns.md: the player's activities hear of interruptions in one place (PlayerInterrupts), death ends
/// every one of them, and none listens for the player's death, a pause or a flinch itself (each used to know only the
/// interruptions someone had thought of).
/// </summary>
public sealed class PlayerInterruptsTests
{
    private sealed class Activity : IPlayerActivity
    {
        public Activity(PlayerInterrupt endsOn) => EndsOn = endsOn;

        public PlayerInterrupt EndsOn { get; }
        public readonly List<PlayerInterrupt> Told = new List<PlayerInterrupt>();

        public void Interrupt(PlayerInterrupt reason) => Told.Add(reason);
    }

    [Test]
    public void DeathEndsEveryActivity_TheRestOnlyThoseThatSaySo()
    {
        var player = new GameObject("Player");
        try
        {
            PlayerInterrupts interrupts = PlayerInterrupts.For(player);
            Assert.AreSame(interrupts, PlayerInterrupts.For(player), "one a player");
            var fishing = new Activity(PlayerInterrupt.None);
            var building = new Activity(PlayerInterrupt.Paused);
            interrupts.Add(fishing);
            interrupts.Add(building);
            interrupts.Add(building);

            interrupts.Raise(PlayerInterrupt.Paused);
            CollectionAssert.IsEmpty(fishing.Told, "a pause doesn't end what doesn't say so");
            CollectionAssert.AreEqual(new[] { PlayerInterrupt.Paused }, building.Told, "and is told once, added twice or not");

            interrupts.Raise(PlayerInterrupt.Died);
            CollectionAssert.AreEqual(new[] { PlayerInterrupt.Died }, fishing.Told, "a death ends every activity");
            CollectionAssert.AreEqual(new[] { PlayerInterrupt.Paused, PlayerInterrupt.Died }, building.Told);

            interrupts.Remove(fishing);
            interrupts.Raise(PlayerInterrupt.Died);
            Assert.AreEqual(1, fishing.Told.Count, "one gone isn't told");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    private static readonly Regex Activity_ = new Regex(@"\bclass\s+\w+[^{]*\bIPlayerActivity\b");
    private static readonly Regex OwnListening = new Regex(@"\.Died\s*\+=|GamePause\.Changed\s*\+=|\.Flinched\s*\+=|AnyWindowOpenChanged\s*\+=");

    [Test]
    public void NoActivity_ListensForInterruptionsItself()
    {
        var listening = new List<string>();
        foreach (string file in Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            if (path.Contains("/Editor/") || path.EndsWith("/PlayerInterrupts.cs"))
            {
                continue;
            }

            string source = File.ReadAllText(path);
            if (Activity_.IsMatch(source) && OwnListening.IsMatch(source))
            {
                listening.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        CollectionAssert.IsEmpty(listening, "a player activity hears of a death, a pause, a flinch or a window through PlayerInterrupts (EndsOn, Interrupt)");
        Assert.Greater(Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories)
            .Count(file => Activity_.IsMatch(File.ReadAllText(file))), 5, "the activities were found");
    }
}
