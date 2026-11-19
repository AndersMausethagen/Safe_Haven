using UnityEngine;

/// <summary>
/// Buildings placed in lines (walls, palisades) are drawn by dragging: pieces are laid end to end, start and finish
/// snap to the ends of others like them, and only the pieces that fit and can be paid for are built. Part of
/// BuildPlacer, in its own file.
/// </summary>
public sealed partial class BuildPlacer
{
    // The nearest free end of a placed piece of the same kind (a palisade end to join), or of another line piece as long
    // (a gate in a skigard), other than except.
    private bool TryFindLineEnd(Vector3 cursor, Vector3? except, out Vector3 end)
    {
        end = cursor;
        float radius = Mathf.Max(snapDistance, 1f);
        float best = radius * radius;
        bool found = false;
        foreach (BuildingObject other in BuildingObject.Active)
        {
            if (other.Definition != placing && !Joins(other))
            {
                continue;
            }

            BuildPlacement.LineEnds(other.transform.position, other.transform.eulerAngles.y, other.LocalBounds, out Vector3 a, out Vector3 b);
            foreach (Vector3 candidate in new[] { a, b })
            {
                Vector3 flat = new Vector3(candidate.x, cursor.y, candidate.z);
                if (except.HasValue && (flat - except.Value).sqrMagnitude < 0.01f)
                {
                    continue;
                }

                float distance = (flat - cursor).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    end = flat;
                    found = true;
                }
            }
        }

        return found;
    }

    // Another kind of line piece the one being placed can carry on from: drawn in lines too, and as long.
    private bool Joins(BuildingObject other) =>
        other.Definition != null && other.Definition.PlaceInLines && placing.PlaceInLines
        && Mathf.Abs(BuildPlacement.PieceLength(other.LocalBounds) - BuildPlacement.PieceLength(LocalBounds)) < 0.25f;

    // Metres the cursor moves before its way gets another point, and how far the way must bend off the straight line to be a curve.
    private const float PathStep = 0.6f;
    private const float PathBend = 1f;

    private void UpdateLine(Vector3 cursor, bool free)
    {
        Vector3 end = cursor;
        bool joined = !free && TryFindLineEnd(cursor, lineStart, out end);
        if (!joined)
        {
            end = cursor;
        }

        Vector3 delta = end - lineStart;
        delta.y = 0f;
        float length = BuildPlacement.PieceLength(LocalBounds);
        if (delta.magnitude < length * 0.5f)
        {
            ghost.Show(1);
            string problem = CheckSpot(pressPoint, pressYaw);
            if (problem == null && !CanAfford(placing, 1, out string missing))
            {
                problem = missing;
            }

            ghost.Place(0, pressPoint, pressYaw, problem, Kind);
            return;
        }

        bool alongX = BuildPlacement.RunsAlongX(LocalBounds);
        lineDirections.Clear();
        // The cursor's way, a point every PathStep metres: bent more than PathBend off the straight line, the wall follows it.
        Vector3 last = linePath.Count > 0 ? linePath[linePath.Count - 1] : lineStart;
        Vector3 moved = cursor - last;
        moved.y = 0f;
        if (linePath.Count == 0 || moved.magnitude >= PathStep)
        {
            linePath.Add(cursor);
        }

        bool curved = !lineRound && linePath.Count > 2 && BuildPlacement.Bend(linePath, joined ? end : cursor) > Mathf.Max(PathBend, length * 0.3f);
        if (curved)
        {
            // To the cursor (or the free end it joins), so the last piece ends where it is drawn to.
            int keep = linePath.Count;
            linePath.Add(joined ? end : cursor);
            BuildPlacement.LayPath(linePath, length, maxLinePieces, lineCenters, lineDirections);
            linePath.RemoveRange(keep, linePath.Count - keep);
        }
        else if (lineRound && HomePortal.Current != null)
        {
            // To the end it was dragged to: a free end of the fence it joins (closing a ring), else the cursor.
            BuildPlacement.LayArc(HomePortal.Current.transform.position, lineStart, joined ? end : cursor, length, maxLinePieces, lineCenters, lineDirections);
        }

        if (lineDirections.Count == 0)
        {
            if (!joined && !free)
            {
                end = lineStart + BuildPlacement.SnapDirection(delta, lineAngleStep) * delta.magnitude;
            }

            Vector3 direction = BuildPlacement.LayLine(lineStart, end, length, maxLinePieces, lineCenters);
            for (int i = 0; i < lineCenters.Count; i++)
            {
                lineDirections.Add(direction);
            }
        }

        ghost.Show(lineCenters.Count);
        Overlay.Here(Kind, pressPoint, pressYaw, LocalBounds);
        int paid = 0;
        for (int i = 0; i < lineCenters.Count; i++)
        {
            float pieceYaw = BuildPlacement.LineYaw(lineDirections[i], alongX);
            // Each piece on the ground where it stands, as a single piece is (Spots): laid at the press point's height, a line
            // down a slope floated and one up it ran into the hill.
            Vector3 center = Area != null ? Area.OnGround(lineCenters[i]) : lineCenters[i];
            Vector3 position = BuildPlacement.PivotFor(center, pieceYaw, LocalBounds);
            string problem = CheckSpot(position, pieceYaw);
            if (problem == null)
            {
                if (CanAfford(placing, paid + 1, out string missing))
                {
                    paid++;
                }
                else
                {
                    problem = missing;
                }
            }

            ghost.Place(i, position, pieceYaw, problem, Kind);
        }
    }

    private void ConfirmLine()
    {
        drawingLine = false;
        BuildingDefinition building = placing;
        int built = 0;
        int standing = 0;
        int blocked = 0;
        string firstProblem = null;
        for (int i = 0; i < ghost.Count; i++)
        {
            string problem = ghost.ProblemAt(i);
            if (problem != null)
            {
                blocked++;
                firstProblem ??= problem;
                continue;
            }

            Transform piece = ghost.PieceAt(i);
            GameObject placed = Build(building, piece.position, piece.rotation);
            if (placed == null)
            {
                break;
            }

            built++;
            standing += StandsAtOnce(placed) ? 1 : 0;
        }

        if (built == 0)
        {
            GameMessages.Post(firstProblem ?? "Nothing could be built there.", MessageKind.Warning);
            return;
        }

        // As a single building is (Confirm): only what stands at once is built now; a scaffold is Built when it is hammered up
        // (Construction.Finish), so it isn't counted twice.
        string name = built == 1 ? $"the {building.DisplayName}" : $"{built} × {building.DisplayName}";
        string done = standing == built ? $"Built {name}." : $"The scaffolds for {name} are up. Hold {{key:Hammer}} at them to build.";
        GameMessages.Post(blocked > 0 ? $"{done} {blocked} spot(s) were left: {firstProblem}" : done, MessageKind.Gain);
        if (standing > 0)
        {
            GameEvents.Raise(new GameEvent(GameEventKind.Built, building, standing, gameObject));
        }

        Cancel();
    }
}
