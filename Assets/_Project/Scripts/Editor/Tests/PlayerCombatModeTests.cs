using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>PlayerCombatMode: fighting keeps the game in Combat, and a quiet stretch with no creature after the player returns it to Exploration.</summary>
public sealed class PlayerCombatModeTests
{
    private const float Timeout = 5f;

    private GameObject host;
    private GameObject player;
    private GameStateManager manager;
    private PlayerCombatMode mode;
    private float now;
    // Whether a creature is after the player's side, and where the mode asked about.
    private bool hunting;
    private Vector3 askedAbout;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Game State");
        manager = host.AddComponent<GameStateManager>();
        Invoke(manager, "Awake");
        player = new GameObject("Player");
        now = 100f;
        hunting = false;
        mode = new PlayerCombatMode(player.transform, () => now, (point, range) =>
        {
            askedAbout = point;
            return hunting;
        });
        mode.Attach(null);
    }

    [TearDown]
    public void TearDown()
    {
        mode.Detach();
        Invoke(manager, "OnDestroy");
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(player);
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    [Test]
    public void Fighting_EntersCombat()
    {
        Assert.AreEqual(GameState.Exploration, manager.CurrentState);
        mode.Enter();
        Assert.AreEqual(GameState.Combat, manager.CurrentState);
    }

    [Test]
    public void AQuietStretch_ReturnsToExploration()
    {
        mode.Enter();
        now += Timeout - 1f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Combat, manager.CurrentState, "Not yet");
        now += 1.5f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Exploration, manager.CurrentState);
    }

    [Test]
    public void ABlowLanded_StartsTheQuietStretchAgain()
    {
        mode.Enter();
        now += Timeout - 1f;
        mode.NoteActivity();
        now += Timeout - 1f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Combat, manager.CurrentState, "Quiet only since the last blow");
        now += 1.5f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Exploration, manager.CurrentState);
    }

    [Test]
    public void NoTimeout_StaysInCombat()
    {
        mode.Enter();
        now += 1000f;
        mode.Tick(0f);
        Assert.AreEqual(GameState.Combat, manager.CurrentState);
    }

    [Test]
    public void CombatEnteredAnotherWay_CountsTheQuietFromThen()
    {
        now += 1000f;
        Assert.IsTrue(manager.TrySetState(GameState.Combat), "A manual toggle (the cheat menu, a story)");
        now += Timeout - 1f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Combat, manager.CurrentState, "The quiet counts from entering Combat, not from the last blow long ago");
        now += 1.5f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Exploration, manager.CurrentState);
    }

    [Test]
    public void ACreatureAfterThePlayersSide_HoldsTheFight()
    {
        // After the player, a companion, or a friend fighting beside them: the quiet doesn't end it.
        player.transform.position = new Vector3(3f, 0f, 7f);
        mode.Enter();
        hunting = true;
        now += 1000f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Combat, manager.CurrentState);
        Assert.AreEqual(player.transform.position, askedAbout, "Asked about creatures near the player");

        hunting = false;
        now += 1f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Exploration, manager.CurrentState, "Once none is, the long quiet ends it");
    }

    [Test]
    public void Detached_LeavesTheModeAlone()
    {
        mode.Detach();
        manager.TrySetState(GameState.Combat);
        now += 1000f;
        mode.Tick(Timeout);
        Assert.AreEqual(GameState.Combat, manager.CurrentState);
    }
}
