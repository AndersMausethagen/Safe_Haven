using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>The game's Exploration or Combat mode, and that every scene the player walks in has someone keeping it.</summary>
public sealed class GameStateManagerTests
{
    private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
    private const string ManagerScript = "Assets/_Project/Scripts/Core/GameStateManager.cs";

    private GameObject host;

    private GameStateManager Manager()
    {
        host = new GameObject("Game State");
        var manager = host.AddComponent<GameStateManager>();
        Invoke(manager, "Awake");
        return manager;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null)
        {
            Invoke(host.GetComponent<GameStateManager>(), "OnDestroy");
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void EveryScene_WithAPlayer_HasAManager()
    {
        string player = AssetDatabase.AssetPathToGUID(PlayerPrefab);
        string manager = AssetDatabase.AssetPathToGUID(ManagerScript);
        Assert.IsNotEmpty(player);
        Assert.IsNotEmpty(manager);
        int withPlayer = 0;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            string text = File.ReadAllText(scene.path);
            if (text.Contains(player))
            {
                withPlayer++;
                Assert.IsTrue(text.Contains(manager), $"{scene.path} has a player but no GameStateManager, so Combat mode won't work there");
            }
        }

        Assert.Greater(withPlayer, 0);
    }

    [Test]
    public void InCombat_FollowsTheMode_AndIsFalseWithoutAManager()
    {
        Assert.IsFalse(GameStateManager.InCombat);
        GameStateManager manager = Manager();
        Assert.IsFalse(GameStateManager.InCombat);
        Assert.IsTrue(manager.TrySetState(GameState.Combat));
        Assert.IsTrue(GameStateManager.InCombat);
    }

    [Test]
    public void ADestroyedManager_LeavesNothingBehind()
    {
        GameStateManager manager = Manager();
        Assert.AreSame(manager, GameStateManager.Current);
        Invoke(manager, "OnDestroy");
        Assert.IsTrue(ReferenceEquals(GameStateManager.Current, null), "a destroyed manager must not keep answering");
    }

    [Test]
    public void AListenerCannotChangeTheMode_WhileItIsChanging()
    {
        GameStateManager manager = Manager();
        bool? second = null;
        manager.GameStateChanged += (previous, current) => second = manager.TrySetState(GameState.Exploration);
        LogAssert.Expect(LogType.Error, new Regex("cannot be changed from a GameStateChanged handler"));
        manager.TrySetState(GameState.Combat);
        Assert.AreEqual(false, second);
        Assert.AreEqual(GameState.Combat, manager.CurrentState);
    }

    [Test]
    public void AFailingListener_DoesNotStopTheOthers()
    {
        GameStateManager manager = Manager();
        bool heard = false;
        manager.GameStateChanged += (previous, current) => throw new System.InvalidOperationException("listener broke");
        manager.GameStateChanged += (previous, current) => heard = true;
        LogAssert.Expect(LogType.Exception, new Regex("listener broke"));
        manager.TrySetState(GameState.Combat);
        Assert.IsTrue(heard);
    }
}
