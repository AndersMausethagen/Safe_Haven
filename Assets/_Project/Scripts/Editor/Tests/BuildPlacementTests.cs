using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Edit Mode tests for BuildPlacement: footprints, yards, overlap, snapping beside a building and laying lines.</summary>
public sealed class BuildPlacementTests
{
    private static readonly Bounds Box = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 2f));
    private static readonly Bounds Wall = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 0.6f));

    private sealed class TestArea : IBuildArea
    {
        public string WhyClosed => null;
        public string Guidance => "";
        public bool AllowsChanges => true;
        public float GroundHeight => 0f;
        public IEnumerable<BuildingDefinition> Buildable(PlayerCrafting crafting) => new BuildingDefinition[0];
        public bool TryGetGroundPoint(Camera view, out Vector3 point) { point = Vector3.zero; return false; }
        public Vector3 OnGround(Vector3 point) => point;
        public string CheckSpot(FootprintRect footprint, Vector3 point) => null;
        public string CheckBuilding(BuildingDefinition building) => null;
    }

    [Test]
    public void BuildArea_TheLatestToEnterIsCurrent()
    {
        IBuildArea before = BuildArea.Current;
        var home = new TestArea();
        var camp = new TestArea();
        try
        {
            BuildArea.Enter(home);
            BuildArea.Enter(camp);
            BuildArea.Enter(home);
            Assert.AreSame(camp, BuildArea.Current, "Entering again doesn't move an area to the front.");
            BuildArea.Leave(camp);
            Assert.AreSame(home, BuildArea.Current);
        }
        finally
        {
            BuildArea.Leave(home);
            BuildArea.Leave(camp);
        }

        Assert.AreSame(before, BuildArea.Current);
    }

    [Test]
    public void Footprint_FollowsPositionAndBoundsCentre()
    {
        var offCentre = new Bounds(new Vector3(1f, 0.5f, 0f), new Vector3(2f, 1f, 4f));
        FootprintRect rect = BuildPlacement.Footprint(new Vector3(10f, 0f, 5f), 90f, offCentre);

        Assert.That(rect.Center.x, Is.EqualTo(10f).Within(0.001f));
        Assert.That(rect.Center.y, Is.EqualTo(4f).Within(0.001f), "local +x turned a quarter is world -z");
        Assert.That(rect.HalfSize, Is.EqualTo(new Vector2(1f, 2f)));
    }

    [Test]
    public void Yard_SitsInFrontOfTheEntranceSide()
    {
        Assert.IsFalse(BuildPlacement.TryGetYard(Vector3.zero, 0f, Box, YardSide.Front, 0f, out _), "no depth, no yard");

        Assert.IsTrue(BuildPlacement.TryGetYard(Vector3.zero, 0f, Box, YardSide.Front, 2f, out FootprintRect front));
        Assert.That(front.Center.y, Is.EqualTo(2f).Within(0.001f));
        Assert.That(front.HalfSize, Is.EqualTo(new Vector2(1f, 1f)));

        BuildPlacement.TryGetYard(Vector3.zero, 0f, Box, YardSide.Left, 1f, out FootprintRect left);
        Assert.That(left.Center.x, Is.EqualTo(-1.5f).Within(0.001f));

        BuildPlacement.TryGetYard(Vector3.zero, 180f, Box, YardSide.Front, 2f, out FootprintRect turned);
        Assert.That(turned.Center.y, Is.EqualTo(-2f).Within(0.001f), "a building turned around faces the other way");
    }

    [Test]
    public void Overlaps_CountsSharedAreaButNotTouchingEdges()
    {
        FootprintRect a = BuildPlacement.Footprint(Vector3.zero, 0f, Box);

        Assert.IsTrue(BuildPlacement.Overlaps(a, BuildPlacement.Footprint(new Vector3(1.5f, 0f, 0f), 0f, Box)));
        Assert.IsFalse(BuildPlacement.Overlaps(a, BuildPlacement.Footprint(new Vector3(2f, 0f, 0f), 0f, Box)), "touching");
        Assert.IsFalse(BuildPlacement.Overlaps(a, BuildPlacement.Footprint(new Vector3(3f, 0f, 0f), 0f, Box)));
        Assert.IsTrue(BuildPlacement.Overlaps(a, BuildPlacement.Footprint(new Vector3(2.3f, 0f, 0f), 45f, Box)), "a corner turned into it");
    }

    [Test]
    public void SnapBeside_TouchesTheNearSideAndTakesTheNeighboursFacing()
    {
        FootprintRect neighbour = BuildPlacement.Footprint(Vector3.zero, 0f, Box);

        Assert.IsTrue(BuildPlacement.TrySnapBeside(new Vector3(2.4f, 0f, 0.1f), 10f, Box, neighbour, 0.75f,
            out Vector3 point, out float yaw, out float gap));
        Assert.That(point.x, Is.EqualTo(2f).Within(0.001f), "flush against the +x side");
        Assert.That(point.z, Is.EqualTo(0f).Within(0.001f), "lined up with its centre");
        Assert.That(yaw, Is.EqualTo(0f).Within(0.001f));
        Assert.That(gap, Is.EqualTo(0.4f).Within(0.001f));

        Assert.IsFalse(BuildPlacement.TrySnapBeside(new Vector3(4f, 0f, 0f), 0f, Box, neighbour, 0.75f, out _, out _, out _), "too far away");
    }

    [Test]
    public void SnapBeside_WorksAroundATurnedNeighbourAndQuarterTurnedPiece()
    {
        FootprintRect neighbour = BuildPlacement.Footprint(Vector3.zero, 90f, Wall);

        Assert.IsTrue(BuildPlacement.TrySnapBeside(new Vector3(0f, 0f, 3.6f), 80f, Wall, neighbour, 0.75f,
            out Vector3 point, out float yaw, out _));
        Assert.That(yaw, Is.EqualTo(90f).Within(0.001f));
        Assert.That(point.z, Is.EqualTo(4f).Within(0.001f), "end to end with the turned wall");
        Assert.That(point.x, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void LayLine_FitsWholePiecesFromTheStart()
    {
        var centers = new List<Vector3>();
        Vector3 direction = BuildPlacement.LayLine(Vector3.zero, new Vector3(9f, 0f, 0f), 4f, 20, centers);

        Assert.That(direction, Is.EqualTo(Vector3.right));
        Assert.That(centers.Count, Is.EqualTo(2));
        Assert.That(centers[0], Is.EqualTo(new Vector3(2f, 0f, 0f)));
        Assert.That(centers[1], Is.EqualTo(new Vector3(6f, 0f, 0f)));

        BuildPlacement.LayLine(Vector3.zero, new Vector3(0f, 0f, 400f), 4f, 5, centers);
        Assert.That(centers.Count, Is.EqualTo(5), "capped");
    }

    [Test]
    public void LineYawAndEnds_LineUpAPieceWithItsDirection()
    {
        float yaw = BuildPlacement.LineYaw(Vector3.forward, alongX: true);
        BuildPlacement.LineEnds(Vector3.zero, yaw, Wall, out Vector3 a, out Vector3 b);

        Assert.That(Mathf.Abs(a.x) + Mathf.Abs(b.x), Is.LessThan(0.001f));
        Assert.That(Mathf.Abs(a.z - b.z), Is.EqualTo(4f).Within(0.001f));
        Assert.That(BuildPlacement.LineYaw(Vector3.forward, alongX: false), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void SnapDirection_RoundsToTheStep()
    {
        Vector3 snapped = BuildPlacement.SnapDirection(new Vector3(1f, 0f, 0.1f), 15f);

        Assert.That(snapped.x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(snapped.z, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void PivotFor_CentresAnOffCentreFootprint()
    {
        var offCentre = new Bounds(new Vector3(1f, 0f, 0f), new Vector3(2f, 1f, 2f));
        Vector3 pivot = BuildPlacement.PivotFor(new Vector3(5f, 0f, 5f), 0f, offCentre);

        Assert.That(pivot, Is.EqualTo(new Vector3(4f, 0f, 5f)));
    }

    // A wall drawn round a curve follows it (the user, 2026-11-19: dragged walls came out as squares): pieces end to end along the
    // drawn way, each turned a little from the last, with no gap between them.
    [Test]
    public void LayPath_FollowsADrawnCurve_PieceEndToPieceStart()
    {
        var path = new List<Vector3>();
        for (int degrees = 0; degrees <= 90; degrees += 5)
        {
            float a = degrees * Mathf.Deg2Rad;
            path.Add(new Vector3(Mathf.Cos(a) * 10f, 0f, Mathf.Sin(a) * 10f));
        }

        var centers = new List<Vector3>();
        var directions = new List<Vector3>();
        BuildPlacement.LayPath(path, 2f, 50, centers, directions);
        // A quarter circle of radius 10 is 15.7 m: seven 2 m pieces and a last one over the 1.7 m left (more than half a piece).
        Assert.AreEqual(8, centers.Count);
        Vector3 end = path[0];
        for (int i = 0; i < centers.Count; i++)
        {
            Vector3 start = centers[i] - directions[i] * 1f;
            if (i < centers.Count - 1)
            {
                Assert.Less(Vector3.Distance(start, end), 0.05f, $"piece {i} starts where the last ended");
            }

            end = centers[i] + directions[i] * 1f;
            Assert.AreEqual(10f, new Vector2(centers[i].x, centers[i].z).magnitude, 0.2f, "on the drawn curve");
            if (i > 0)
            {
                float turn = Vector3.Angle(directions[i - 1], directions[i]);
                Assert.That(turn, Is.InRange(5f, 20f), "each a little turned from the last, not a square corner");
            }
        }

        Assert.Greater(BuildPlacement.Bend(path, path[path.Count - 1]), 2f, "a curve reads as bent");
        Assert.Less(BuildPlacement.Bend(new List<Vector3> { Vector3.zero, new Vector3(5f, 0f, 0.2f) }, new Vector3(10f, 0f, 0f)), 0.5f, "a straight drag doesn't");
    }
}
