using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>What makes chopping feel right: the blow waits for the blade, the trunk shows the cut, a big tree sounds big and birds go up.</summary>
public sealed class FellingFeelTests
{
    private const string Controller = "Assets/_Project/Animation/Controllers/PlayerAnimator.controller";

    private static BirdProfile Haven => AssetDatabase.LoadAssetAtPath<BirdProfile>("Assets/_Project/Data/World/Birds - Haven.asset");

    [Test]
    public void TheSwings_HaveAContactInsideTheirClips()
    {
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        foreach (InteractionAnimation swing in new[] { InteractionAnimation.MineWall, InteractionAnimation.MineGround })
        {
            float contact = settings.SwingContact(swing);
            Assert.Greater(contact, settings.SwingWindUp, $"{swing} has room to rise before the blow");
            Assert.Less(contact, 0.97f, $"{swing} lands inside its clip");
        }

        Assert.AreEqual(0f, settings.SwingContact(InteractionAnimation.GatherLow), "picking flowers isn't a swing");
    }

    [Test]
    public void TheBlow_FollowsThePressClosely()
    {
        Assert.LessOrEqual(GatheringMinigameSettings.Current.SwingWindUp, 0.2f, "any longer and the press feels ignored");
    }

    [Test]
    public void TheSwingStates_CanBeHeld_ButPlayNormallyByDefault()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Controller);
        AnimatorControllerParameter speed = controller.parameters.FirstOrDefault(parameter => parameter.name == "WorkSpeed");
        Assert.IsNotNull(speed);
        Assert.AreEqual(1f, speed.defaultFloat, 0.001f);
        UnityEditor.Animations.AnimatorStateMachine work = controller.layers.First(layer => layer.name == "Interaction").stateMachine;
        foreach (string name in new[] { "Mine Wall Right", "Mine Wall Left", "Mine Ground Right", "Mine Ground Left" })
        {
            UnityEditor.Animations.AnimatorState state = work.states.First(child => child.state.name == name).state;
            Assert.IsTrue(state.speedParameterActive, name);
            Assert.AreEqual("WorkSpeed", state.speedParameter, name);
        }
    }

    [Test]
    public void AFallenTree_IsCutDownwards()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Systems/FelledTree.prefab");
        Assert.AreEqual(InteractionAnimation.MineGround, prefab.GetComponent<Interactable>().AnimationType);
    }

    [Test]
    public void FallingLeaves_AreGreen_OnEveryTree()
    {
        int trees = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<ChoppableTree>() == null || path.EndsWith("TerrainTreeProxy.prefab"))
            {
                continue;
            }

            TreeParts parts = TreeParts.Of(prefab, Vector3.zero, Quaternion.identity, Vector3.one);
            trees++;
            Assert.Less(Mathf.Min(parts.LeafTint.r, Mathf.Min(parts.LeafTint.g, parts.LeafTint.b)), 0.8f, $"{path} would drop white leaves");
        }

        Assert.Greater(trees, 5);
    }

    [Test]
    public void TheNotch_WidensAsTheCutDeepens()
    {
        Mesh start = TreeNotch.Build(0.2f, 0.1f);
        Mesh end = TreeNotch.Build(0.2f, 0.9f);
        try
        {
            Assert.Greater(end.bounds.size.y, start.bounds.size.y);
            Assert.Greater(end.bounds.size.x, start.bounds.size.x);
        }
        finally
        {
            Object.DestroyImmediate(start);
            Object.DestroyImmediate(end);
        }
    }

    [Test]
    public void TheNotch_SitsOnTheTrunk_FacingOut()
    {
        Mesh mesh = TreeNotch.Build(0.3f, 0.5f);
        try
        {
            foreach (Vector3 vertex in mesh.vertices)
            {
                Assert.GreaterOrEqual(new Vector2(vertex.x, vertex.z).magnitude, 0.3f, "outside the bark, or it can't be seen");
            }

            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 facing = Vector3.Cross(b - a, c - a);
                if (facing.sqrMagnitude > 1e-10f)
                {
                    Assert.Greater(Vector3.Dot(facing, (a + b + c) / 3f), 0f, "it faces away from the trunk");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void ABiggerThing_SoundsLower_AndNothingIsLouderThanItsSound()
    {
        Assert.AreEqual(1f, AudioManager.PitchForSize(1f), 0.001f, "size 1 is the sound as made");
        Assert.AreEqual(1f, AudioManager.VolumeForSize(1f), 0.001f);
        Assert.Less(AudioManager.PitchForSize(2.5f), AudioManager.PitchForSize(1f));
        Assert.Greater(AudioManager.PitchForSize(0.5f), AudioManager.PitchForSize(1f));
        Assert.Less(AudioManager.VolumeForSize(0.5f), AudioManager.VolumeForSize(1f));
        Assert.LessOrEqual(AudioManager.VolumeForSize(2.5f), 1f);
    }

    [Test]
    public void AnOddSize_StaysInAUsableRange()
    {
        foreach (float size in new[] { 0f, 0.01f, 100f })
        {
            Assert.That(AudioManager.PitchForSize(size), Is.InRange(0.7f, 1.35f));
            Assert.That(AudioManager.VolumeForSize(size), Is.InRange(0.5f, 1f));
        }
    }

    [Test]
    public void ALandingTree_IsLoudEnoughToFlushBirds_AndAChopIsNot()
    {
        Assert.IsTrue(Haven.FlushedBy(TreeChoppingSettings.Current.LandingNoiseRadius));
        Assert.IsFalse(Haven.FlushedBy(GatheringMinigameSettings.Current.NoiseRadius(SwingResult.Normal)));
    }

    [Test]
    public void AFlushedFlock_IsOnItsWayUpAtOnce()
    {
        var centre = new Vector3(0f, 9f, 0f);
        BirdFlock flock = BirdFlock.Flushed(Haven, centre, Vector3.zero, 5);
        Assert.AreEqual(5, flock.Birds.Length);
        Assert.IsTrue(flock.IsLeaving);
        Assert.IsFalse(flock.IsGone);
        Assert.IsTrue(flock.Birds.All(bird => bird.Velocity.y > 0f && bird.Position.y > 5f), "up out of the trees, not off the ground");
    }
}
