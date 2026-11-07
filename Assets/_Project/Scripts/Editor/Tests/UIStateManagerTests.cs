using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>The window manager belongs to its scene: a new scene must not find the last one's.</summary>
public sealed class UIStateManagerTests
{
    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    [Test]
    public void ADestroyedManager_LeavesNothingBehind()
    {
        var host = new GameObject("UI State");
        var manager = host.AddComponent<UIStateManager>();
        try
        {
            Invoke(manager, "Awake");
            Assert.AreSame(manager, UIStateManager.Current);
            Invoke(manager, "OnDestroy");
            Assert.IsTrue(ReferenceEquals(UIStateManager.Current, null), "a destroyed manager must not keep answering");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
