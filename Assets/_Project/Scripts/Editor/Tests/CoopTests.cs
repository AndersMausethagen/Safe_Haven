using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>Co-op: where friends are drawn between poses, who may join, and that their bodies are set up like yours.</summary>
public sealed class CoopTests
{
    private static PoseSnapshot At(double time, float x, float yaw = 0f) =>
        new PoseSnapshot { Time = time, Position = new Vector3(x, 0f, 0f), Yaw = yaw };

    [Test]
    public void Poses_AreDrawnBetweenTheTwoAroundTheMoment()
    {
        var poses = new PoseBuffer();
        poses.Add(At(1.0, 0f, 0f));
        poses.Add(At(1.1, 1f, 90f));
        Assert.IsTrue(poses.Sample(1.05, 6f, out Vector3 position, out float yaw, out _));
        Assert.AreEqual(0.5f, position.x, 0.001f);
        Assert.AreEqual(45f, yaw, 0.001f);
    }

    [Test]
    public void Poses_ArrivingOutOfOrder_AreSlottedIn_AndRepeatsDropped()
    {
        var poses = new PoseBuffer();
        poses.Add(At(1.0, 0f));
        poses.Add(At(1.2, 2f));
        poses.Add(At(1.1, 1f)); // late
        poses.Add(At(1.1, 9f)); // a repeat
        Assert.AreEqual(3, poses.Count);
        poses.Sample(1.15, 6f, out Vector3 position, out _, out _);
        Assert.AreEqual(1.5f, position.x, 0.001f);
    }

    [Test]
    public void Poses_HoldTheNewest_AndJumpAcrossAPortal()
    {
        var poses = new PoseBuffer();
        poses.Add(At(1.0, 0f));
        poses.Add(At(1.1, 1f));
        poses.Sample(5.0, 6f, out Vector3 late, out _, out _);
        Assert.AreEqual(1f, late.x, 0.001f, "Nothing newer: stays where last seen, no guessing.");

        poses.Add(At(1.2, 40f));
        poses.Sample(1.15, 6f, out Vector3 jumped, out _, out _);
        Assert.AreEqual(40f, jumped.x, 0.001f, "Forty metres in a tenth of a second is a portal, not a slide.");
    }

    [Test]
    public void Poses_KeepOnlyTheNewest_WhenFull()
    {
        var poses = new PoseBuffer(4);
        for (int i = 0; i < 10; i++)
        {
            poses.Add(At(i, i));
        }

        Assert.AreEqual(4, poses.Count);
        Assert.AreEqual(9.0, poses.NewestTime);
        poses.Add(At(0.5, 0f));
        Assert.AreEqual(4, poses.Count, "Older than everything kept: dropped.");
    }

    [Test]
    public void Joining_NeedsTheSameVersion_AndRoom()
    {
        Assert.IsTrue(CoopSession.MayJoin("1", "1", 3, 4, out _));
        Assert.IsFalse(CoopSession.MayJoin("1", "1", 4, 4, out string full));
        StringAssert.Contains("full", full);
        Assert.IsFalse(CoopSession.MayJoin("0", "1", 1, 4, out string version));
        StringAssert.Contains("version", version);
    }

    [Test]
    public void Addresses_AreAnIp_WithAnOptionalPort()
    {
        Assert.IsTrue(CoopSession.TryParseAddress("192.168.1.20", 7777, out string ip, out ushort port));
        Assert.AreEqual(("192.168.1.20", (ushort)7777), (ip, port));
        Assert.IsTrue(CoopSession.TryParseAddress(" 10.0.0.5:9000 ", 7777, out ip, out port));
        Assert.AreEqual(("10.0.0.5", (ushort)9000), (ip, port));
        Assert.IsFalse(CoopSession.TryParseAddress("", 7777, out _, out _));
        Assert.IsFalse(CoopSession.TryParseAddress("my friend's house", 7777, out _, out _));
        Assert.IsFalse(CoopSession.TryParseAddress("10.0.0.5:0", 7777, out _, out _));
    }

    [Test]
    public void AFriendsGear_DressesThemAsItDressesYou()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ItemDefinition armour = null;
        foreach (ItemDefinition item in content.Items)
        {
            if (item.WornLook != null)
            {
                armour = item;
                break;
            }
        }

        Assert.IsNotNull(armour, "Some item has a worn look.");
        var look = new PlayerLookState { Gear = new Unity.Collections.FixedString512Bytes($"|{armour.Id}||") };
        Assert.AreEqual(armour, look.Worn((GearSlot)1, content));
        Assert.IsNull(look.Worn((GearSlot)0, content));
        Assert.IsNull(look.Worn((GearSlot)40, content), "A slot past the list is empty, not an error.");

        var outfit = new List<GearLookDefinition>();
        PlayerLook.Outfit(slot => look.Worn(slot, content), 0, outfit);
        Assert.Contains(armour.WornLook, outfit);
        Assert.Contains(CharacterCatalog.Current.PlainShoes, outfit, "Bare feet wear plain shoes, as at home.");
    }

    [Test]
    public void TheNetworkPlayer_IsSetUp()
    {
        MultiplayerSettings settings = MultiplayerSettings.Current;
        Assert.AreEqual(4, settings.MaxPlayers);
        Assert.IsNotNull(settings.PlayerPrefab, "Resources/MultiplayerSettings points at the Network Player prefab.");
        Assert.IsNotNull(settings.PlayerPrefab.GetComponent<NetworkPlayerAvatar>());
        Assert.IsFalse(settings.PlayerPrefab.SynchronizeTransform, "Poses go in the avatar's own messages.");

        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");
        Assert.AreEqual(player.GetComponent<Animator>().runtimeAnimatorController, settings.BodyAnimations,
            "A friend's body runs the player's own Animator Controller, so the states sent are states it has.");
    }

    [Test]
    public void AFriendsBody_IsNotAPlayer()
    {
        var holder = new GameObject("Coop Test");
        try
        {
            RemotePlayerBody body = RemotePlayerBody.Create(holder.transform, MultiplayerSettings.Current);
            Assert.IsNull(body.GetComponentInParent<Player>(), "Nothing in the game may take a friend for you.");
            Animator animator = body.GetComponent<Animator>();
            Assert.IsFalse(animator.fireEvents, "Clip events call into player components a friend's body doesn't have.");
            Assert.IsFalse(animator.applyRootMotion);
            Assert.IsNotNull(body.GetComponent<ModularCharacter>());
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }
}
