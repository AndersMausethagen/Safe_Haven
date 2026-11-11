using System;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// No story step can hold a story for good: each waits at most so long (IStoryWait), waits for the player
/// ([WaitsForThePlayer]), or doesn't wait at all (listed here). Wait For Quiet once waited for ever on a monster nobody could
/// reach (2026-11-11). A new step type fails this until it is one of the three.
/// </summary>
public sealed class StoryWaitTests
{
    // Steps that do their thing and go on (or only wait a fixed moment of their own, like a camera move).
    private static readonly string[] DontWait =
    {
        nameof(HasHappenedNode), nameof(CheckQuestNode), nameof(RaiseQuestEventNode), nameof(StartQuestNode), nameof(QuestProgressNode),
        nameof(GiveItemsNode), nameof(CountAsDoneNode), "ShakeCameraNode", "FocusCameraNode", "LightningNode", "PlaySoundNode", "MusicNode",
        "DespawnEnemiesNode", "SetActiveNode", "CreatureHuntsNode", "MoveObjectNode", "SetUsableNode", "GlowNode", "WitherGroundNode",
        "ChangeWeatherNode", "ChangeLookNode", "RevealGroundNode", "InvokeEventNode",
    };

    [Test]
    public void EveryStep_WaitsAtMostSoLong_ForThePlayer_OrNotAtAll()
    {
        var unsorted = typeof(StoryNode).Assembly.GetTypes()
            .Where(t => typeof(StoryNode).IsAssignableFrom(t) && !t.IsAbstract && !typeof(StoryTrigger).IsAssignableFrom(t))
            .Where(t => !typeof(IStoryWait).IsAssignableFrom(t) && !Attribute.IsDefined(t, typeof(WaitsForThePlayerAttribute)) && !DontWait.Contains(t.Name))
            .Select(t => t.Name).ToList();
        CollectionAssert.IsEmpty(unsorted, "a story step that waits must give up (IStoryWait) or wait for the player ([WaitsForThePlayer]); one that doesn't goes in DontWait");
    }

    [Test]
    public void EveryWait_GivesUp_UnlessTheDesignerSaysOtherwise()
    {
        foreach (Type type in typeof(StoryNode).Assembly.GetTypes().Where(t => typeof(IStoryWait).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var step = (IStoryWait)Activator.CreateInstance(type);
            if (step is SpawnEnemiesNode spawn)
            {
                spawn.waitUntilDefeated = true;
            }

            Assert.IsTrue(step.MostSeconds > 0f && step.MostSeconds < float.PositiveInfinity, $"{type.Name} as it comes waits {step.MostSeconds} s: give it a time limit by default");
        }
    }
}
