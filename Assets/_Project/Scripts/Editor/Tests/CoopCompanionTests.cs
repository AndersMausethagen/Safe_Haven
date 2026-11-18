using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>Co-op: friends see the companion a player brought on a raid (CompanionLookState, drawn by RemoteCompanion) and can help them up.</summary>
public sealed class CoopCompanionTests
{
    private const string SigridPath = "Assets/_Project/Data/Prologue/Residents/Sigrid.asset";

    private sealed class Lookup : IContentLookup
    {
        public ResidentDefinition Resident;
        public ItemDefinition FindItem(string id) => null;
        public ResourceDefinition FindResource(string id) => null;
        public RecipeDefinition FindRecipe(string id) => null;
        public QuestDefinition FindQuest(string id) => null;
        public BuildingDefinition FindBuilding(string id) => null;
        public ResidentDefinition FindResident(string id) => Resident != null && Resident.Id == id ? Resident : null;
        public JobDefinition FindJob(string id) => null;
    }

    [Test]
    public void ACompanionsLook_Travels()
    {
        var look = new CompanionLookState { Resident = "expedition.sigrid", Downed = true };
        using var writer = new FastBufferWriter(256, Unity.Collections.Allocator.Temp);
        writer.WriteNetworkSerializable(look);
        using var reader = new FastBufferReader(writer, Unity.Collections.Allocator.Temp);
        reader.ReadNetworkSerializable(out CompanionLookState read);
        Assert.IsTrue(read.Equals(look));
        Assert.IsTrue(read.IsOut);
        Assert.IsFalse(CompanionLookState.Of(null).IsOut, "No companion: nothing to draw");
    }

    [Test]
    public void AFriendsCompanion_IsDrawnAsTheyAre_AndCanBeHelpedUp()
    {
        var sigrid = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(SigridPath);
        Assert.IsTrue(sigrid != null && sigrid.Model != null && sigrid.RaidCompanion != null, "Sigrid can come along");
        int helped = 0;
        var friend = new RemoteCompanion(null, () => helped++, 2f);
        var content = new Lookup { Resident = sigrid };
        try
        {
            friend.Show(new CompanionLookState { Resident = sigrid.Id }, content);
            GameObject body = friend.Body;
            Assert.IsNotNull(body, "Made from the resident's own model");
            Animator animator = body.GetComponentInChildren<Animator>();
            // As in the friend's game (CompanionBody): the definition's animations, else her model's own.
            RuntimeAnimatorController expected = sigrid.RaidCompanion.Animations != null ? sigrid.RaidCompanion.Animations
                : sigrid.Model.GetComponentInChildren<Animator>(true).runtimeAnimatorController;
            Assert.AreSame(expected, animator.runtimeAnimatorController, "With the animations the friend's game gives her");
            var tag = body.GetComponentInChildren<TMPro.TextMeshPro>();
            Assert.AreEqual(sigrid.DisplayName, tag.text);
            var revive = body.GetComponentInChildren<FriendRevive>(true);
            Assert.IsFalse(revive.gameObject.activeSelf, "Nothing to help while she stands");

            friend.Place(new Vector3(4f, 0f, 2f), 90f, default, 1f);
            Assert.AreEqual(new Vector3(4f, 0f, 2f), body.transform.position);
            Assert.AreEqual(90f, body.transform.eulerAngles.y, 0.01f);

            friend.Show(new CompanionLookState { Resident = sigrid.Id, Downed = true }, content);
            StringAssert.Contains("Down!", tag.text);
            Assert.IsTrue(revive.gameObject.activeSelf, "[E] Help up while she is down");
            typeof(FriendRevive).GetMethod("OnHelped", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(revive, null);
            Assert.AreEqual(1, helped, "Helping her up asks the friend's game to get her up");

            friend.Show(default, content);
            Assert.IsFalse(body.activeSelf, "Gone with the friend's companion");
        }
        finally
        {
            if (friend.Body != null)
            {
                Object.DestroyImmediate(friend.Body);
            }
        }
    }
}
