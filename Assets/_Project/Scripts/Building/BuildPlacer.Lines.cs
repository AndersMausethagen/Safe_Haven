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
    // A drawn curve may run further than a straight drag: a ring round the Haven is 40-60 pieces.
    private const int CurvePieces = 80;

    // Metres along a drawn way.
    private static float PathLength(System.Collections.Generic.List<Vector3> path)
    {
        float total = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 step = path[i] - path[i - 1];
            step.y = 0f;
            total += step.magnitude;
        }

        return total;
    }

    private void UpdateLine(Vector3 cursor, bool free)
    {
        Vector3 end = cursor;
        bool joined = !free && TryFindLineEnd(cursor, lineStart, out end);
        if (!joined)
        {
            end = cursor;
        }

        // A ring, as Gord draws a palisade (the user, 2026-11-21): drawn round and back to where it began, the wall closes on its start.
        Vector3 back = cursor - lineStart;
        back.y = 0f;
        float pieceLength = BuildPlacement.PieceLength(LocalBounds);
        if (!free && linePath.Count > 6 && back.magnitude <= Mathf.Max(2.5f, pieceLength * 0.6f) && PathLength(linePath) >= pieceLength * 4f)
        {
            end = lineStart;
            joined = true;
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
            BuildPlacement.LayPath(linePath, length, Mathf.Max(maxLinePieces, CurvePieces), lineCenters, lineDirections);
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

        NoteLine(lineCenters.Count, paid);
    }

    /// <summary>
    /// While a line is drawn: its pieces, what they cost together, and how many can be paid for now ("14 pieces: 28 Wood, 14 Sticks.
    /// You can pay for 9"), shown by the Haven's HUD (the systems review, 2026-11-21: see the whole ring's cost before the first post).
    /// Empty when no line is being drawn.
    /// </summary>
    public static string LineSummary => Time.frameCount - lineSummaryFrame <= 2 ? lineSummary : "";

    private static string lineSummary = "";
    private static int lineSummaryFrame = -10;
    // What the summary was made for (pieces, affordable, the piece): it is made again only when that changes.
    private static int lineKey = -1;

    private void NoteLine(int pieces, int affordable)
    {
        int key = (pieces * 1000 + affordable) * 31 + (placing != null ? placing.GetHashCode() : 0);
        lineSummaryFrame = Time.frameCount;
        if (key == lineKey)
        {
            return;
        }

        lineKey = key;
        var items = new System.Collections.Generic.List<ItemAmount>();
        foreach (ItemAmount cost in placing.ConstructionItems)
        {
            if (cost.Item != null)
            {
                items.Add(new ItemAmount(cost.Item, cost.Amount * pieces));
            }
        }

        var essence = new System.Collections.Generic.List<ResourceAmount>();
        foreach (ResourceAmount cost in placing.ConstructionCost)
        {
            if (cost.Resource != null)
            {
                essence.Add(new ResourceAmount(cost.Resource, cost.Amount * pieces));
            }
        }

        string total = AmountText.Describe(items) + (essence.Count > 0 ? (items.Count > 0 ? ", " : "") + AmountText.Describe(essence) : "");
        lineSummary = $"{pieces} {placing.DisplayName} piece{(pieces == 1 ? "" : "s")}: {total}."
                      + (affordable < pieces ? $" You can pay for {affordable}." : "");
        lineSummaryFrame = Time.frameCount;
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

        // Fences, walls and paths are laid one run after another (the user, 2026-11-20: "do not cancel the placement after we place
        // 1 fence"): placing goes on while another piece can be paid for, until right-click or Escape ends it.
        if (CanAfford(building, 1, out _))
        {
            lineRound = false;
            linePath.Clear();
            ghost.Show(1);
            return;
        }

        Cancel();
    }
}
