using NUnit.Framework;
using UnityEngine;

/// <summary>The in-game cinematic's camera maths (CameraShot) and its story nodes.</summary>
public sealed class InGameCinematicTests
{
    [Test]
    public void OffsetsInTheSubjectsFacing_TurnWithIt()
    {
        var shot = new CameraShot { startOffset = new Vector3(0f, 2f, 6f), endOffset = new Vector3(0f, 2f, 6f), lookOffset = Vector3.up * 2f };
        // Facing east (yaw 90): six metres ahead of it is six metres east.
        var east = new CameraShot.Anchor(new Vector3(10f, 0f, 0f), 90f);
        CameraShot.Pose pose = shot.Evaluate(east, east, 0f);
        Assert.That(Vector3.Distance(pose.Position, new Vector3(16f, 2f, 0f)), Is.LessThan(0.001f));
        // And it looks back at the subject's look point.
        Assert.That(Vector3.Angle(pose.Rotation * Vector3.forward, new Vector3(-1f, 0f, 0f)), Is.LessThan(0.5f));
    }

    [Test]
    public void OffsetsInTheWorld_IgnoreWhichWayItFaces()
    {
        var shot = new CameraShot { space = CameraShot.Space.World, startOffset = new Vector3(0f, 3f, -5f), endOffset = new Vector3(0f, 3f, -5f) };
        var facing = new CameraShot.Anchor(Vector3.zero, 137f);
        Assert.That(Vector3.Distance(shot.Evaluate(facing, facing, 0.5f).Position, new Vector3(0f, 3f, -5f)), Is.LessThan(0.001f));
    }

    // A top-down game's cinematic frames in the player's own view: the map keeps the way it lies on screen (the user, 2026-11-19).
    [Test]
    public void OffsetsInTheGameView_TurnWithThePlayersCamera_NotTheSubject()
    {
        var shot = new CameraShot { space = CameraShot.Space.GameView, startOffset = new Vector3(0f, 8f, -6f), endOffset = new Vector3(0f, 8f, -6f),
            lookOffset = Vector3.zero };
        var troll = new CameraShot.Anchor(new Vector3(10f, 0f, 0f), 200f);
        // The player's camera faces east (yaw 90): six metres back from the view is six metres west, whichever way the troll faces.
        CameraShot.Pose pose = shot.Evaluate(troll, troll, 0f, 90f);
        Assert.That(Vector3.Distance(pose.Position, new Vector3(4f, 8f, 0f)), Is.LessThan(0.001f));
        Vector3 forward = pose.Rotation * Vector3.forward;
        Assert.That(Vector3.Angle(new Vector3(forward.x, 0f, forward.z), Vector3.right), Is.LessThan(0.5f), "looking the way the player was");
        Assert.That(Vector3.Angle(forward, Vector3.down), Is.LessThan(45f), "from above, not across the ground");
    }

    [Test]
    public void AShot_MovesFromStartToEnd_WithItsFieldOfView()
    {
        var shot = new CameraShot
        {
            startOffset = new Vector3(0f, 1f, 10f), endOffset = new Vector3(0f, 1f, 5f), startFieldOfView = 50f, endFieldOfView = 30f,
            ease = CameraShot.Ease.EaseInOut,
        };
        var still = new CameraShot.Anchor(Vector3.zero, 0f);
        Assert.AreEqual(10f, shot.Evaluate(still, still, 0f).Position.z, 0.001f);
        Assert.AreEqual(5f, shot.Evaluate(still, still, 1f).Position.z, 0.001f);
        Assert.AreEqual(50f, shot.Evaluate(still, still, 0f).FieldOfView, 0.001f);
        Assert.AreEqual(30f, shot.Evaluate(still, still, 1f).FieldOfView, 0.001f);
        // Eased: a quarter of the way in time is less than a quarter of the way.
        Assert.Greater(shot.Evaluate(still, still, 0.25f).Position.z, 10f - 5f * 0.25f);
    }

    [Test]
    public void LookAt_FramesSomethingElse_FromBesideTheSubject()
    {
        var shot = new CameraShot { startOffset = new Vector3(0f, 2f, -4f), endOffset = new Vector3(0f, 2f, -4f), lookOffset = Vector3.zero };
        var subject = new CameraShot.Anchor(Vector3.zero, 0f);
        var fire = new CameraShot.Anchor(new Vector3(0f, 2f, 40f), 0f);
        CameraShot.Pose pose = shot.Evaluate(subject, fire, 0f);
        Assert.That(Vector3.Distance(pose.Position, new Vector3(0f, 2f, -4f)), Is.LessThan(0.001f));
        Assert.That(Vector3.Angle(pose.Rotation * Vector3.forward, Vector3.forward), Is.LessThan(0.5f), "it looks past the subject at the fire");
    }

    [Test]
    public void TheNodes_SayWhatIsMissing()
    {
        Assert.IsNotNull(new PlayCinematicNode().FindProblem());
        Assert.IsNotNull(new PlayAnimationNode().FindProblem());
        // Kept out of the open scene, so the test doesn't leave it with unsaved changes.
        GameObject go = UnityEditor.EditorUtility.CreateGameObjectWithHideFlags("Cinematic", HideFlags.HideAndDontSave);
        try
        {
            var cinematic = go.AddComponent<InGameCinematic>();
            Assert.IsEmpty(cinematic.FindProblems(), "a new one has a shot that works");
            Assert.IsNull(new PlayCinematicNode { cinematic = cinematic }.FindProblem());
            Assert.Greater(new PlayCinematicNode { cinematic = cinematic }.MostSeconds, cinematic.Seconds, "the story waits no longer than it plays");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
