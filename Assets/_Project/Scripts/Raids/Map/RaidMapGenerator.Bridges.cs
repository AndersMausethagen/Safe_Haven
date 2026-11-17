using UnityEngine;

/// <summary>
/// Bridges over a Valley map's river (RaidLayoutCrossing.Bridge): the rules' bridge prefab laid across each bridged crossing, square to
/// the river, its deck level with the banks at both ends. Placed with the rivers, before the navmesh is built, so its deck collider
/// becomes walkable ground. The bridge nearest the valley's head can be a set piece of its own (Valley › Boss Bridge: the Brukam's): its
/// guards come out on it when the raid begins (RaidSetPieceSite) and its prefab stands on the far bank. Part of RaidMapGenerator, in its
/// own file.
/// </summary>
public static partial class RaidMapGenerator
{
    public const string BridgesName = "Bridges";

    /// <summary>Metres a bridge reaches past the water's edge onto each bank.</summary>
    private const float BridgeOntoBank = 4f;

    private const float BridgeScaleLeast = 0.8f;
    private const float BridgeScaleMost = 1.4f;

    public static void PlaceBridges(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        Transform old = map.transform.Find(BridgesName);
        if (old != null)
        {
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        RaidValleyRule rule = rules.Valley;
        if (plan.Valley == null || rule.bridgePrefab == null || plan.Rivers.Count == 0 || !plan.Crossings.Exists(c => c.Bridge))
        {
            return;
        }

        var holder = new GameObject(BridgesName).transform;
        holder.SetParent(map.transform, false);
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        RaidLayoutRiver river = plan.Rivers[0];
        int index = 0;
        RaidLayoutCrossing head = null;
        foreach (RaidLayoutCrossing crossing in plan.Crossings)
        {
            if (crossing.Bridge && (head == null || plan.Valley.ShareOf(crossing.Position) > plan.Valley.ShareOf(head.Position)))
            {
                head = crossing;
            }
        }

        foreach (RaidLayoutCrossing crossing in plan.Crossings)
        {
            if (!crossing.Bridge)
            {
                continue;
            }

            river.Nearest(crossing.Position, out int segment, out float along, out _);
            float span = 2f * (RaidLayoutRavine.At(river.HalfWidths, segment, along) + BridgeOntoBank);
            float scale = Mathf.Clamp(span / Mathf.Max(1f, rule.bridgeLength), BridgeScaleLeast, BridgeScaleMost);
            float half = rule.bridgeLength * scale * 0.5f;
            Vector3 Ground(Vector2 point) => RaidMapSurface.Ground(terrain, origin + new Vector3(point.x, 0f, point.y));
            Vector3 near = Ground(crossing.Position - crossing.Direction * half);
            Vector3 far = Ground(crossing.Position + crossing.Direction * half);
            float deck = Mathf.Max(near.y, far.y);

            GameObject bridge = Object.Instantiate(rule.bridgePrefab, holder);
            bridge.name = $"Bridge {++index}";
            bridge.transform.SetPositionAndRotation(
                new Vector3(origin.x + crossing.Position.x, deck - rule.bridgeDeck * scale, origin.z + crossing.Position.y),
                Quaternion.LookRotation(new Vector3(crossing.Direction.x, 0f, crossing.Direction.y), Vector3.up));
            bridge.transform.localScale = Vector3.one * scale;
            // Every bridge asks its toll, the head's too: since the Brukam left for his own bridge (2026-11-17) the head keeps only his
            // old camp across it (The Toll's place), and its toll-taker pays him in turn.
            if (rule.bridgeToll != null)
            {
                AskToll(bridge, rule.bridgeToll, plan, terrain, origin, crossing, half);
            }

            if (crossing == head && rule.bossBridge != null)
            {
                MakeBossBridge(bridge, rule.bossBridge, plan, rules, terrain, origin, crossing, half, rule.bridgeDeck * scale);
            }
        }
    }

    /// <summary>Metres past the end of a bridge where its toll-taker sits, on the bank.</summary>
    private const float TollFromBridge = 2.5f;

    // The toll is asked at the end a raid comes to first along the trails, its back to the bridge, watching the player come.
    private static void AskToll(GameObject bridge, RaidEventDefinition toll, RaidLayoutPlan plan, Terrain terrain, Vector3 origin, RaidLayoutCrossing crossing, float half)
    {
        Vector2 near = TollSpot(plan, crossing, half);
        Vector2 toComer = (near - crossing.Position).normalized;
        var spot = new GameObject("Toll");
        spot.transform.SetParent(bridge.transform, true);
        spot.transform.SetPositionAndRotation(RaidMapSurface.Ground(terrain, origin + new Vector3(near.x, 0f, near.y)),
            Quaternion.LookRotation(new Vector3(toComer.x, 0f, toComer.y), Vector3.up));
        spot.AddComponent<RaidBridgeToll>().Bind(toll);
    }

    /// <summary>
    /// Where a bridge's toll-taker sits, in metres from the map's corner: <see cref="TollFromBridge"/> past the end of the bridge
    /// (<paramref name="half"/> metres from its middle) on the bank the trail comes from on the way up (RaidLayoutPlanner.ArrivalRoom).
    /// The valley's bend used to choose it (review 2026-11-17, R4): both ends sit almost as far up the valley, so on some stretches
    /// the player crossed the whole bridge before meeting the toll.
    /// </summary>
    internal static Vector2 TollSpot(RaidLayoutPlan plan, RaidLayoutCrossing crossing, float half)
    {
        Vector2 side = crossing.Direction * (half + TollFromBridge);
        Vector2 ahead = crossing.Position + side, behind = crossing.Position - side;
        if (plan.Valley == null)
        {
            return behind;
        }

        int bank = plan.Valley.BankOf(plan.Rooms[RaidLayoutPlanner.ArrivalRoom(plan, crossing)].Position);
        return plan.Valley.BankOf(ahead) == bank ? ahead : behind;
    }

    /// <summary>Metres from the end of the boss bridge to its camp on the bank.</summary>
    private const float CampFromBridge = 5f;

    // The head bridge's guards wait on its deck; its camp stands on the higher bank, facing the bridge. The site sits on the deck's top
    // (a ray onto the bridge's own collider): at the pivot, under the arch, the guards were found the nearest ground, which was the river
    // bank under the bridge (the Brukam stood in the water, 2026-11-17). Keep the piece's Radius small, so its guard ring stays on the deck.
    private static void MakeBossBridge(GameObject bridge, RaidSetPieceDefinition piece, RaidLayoutPlan plan, RaidLayoutRules rules, Terrain terrain,
        Vector3 origin, RaidLayoutCrossing crossing, float half, float deckAbovePivot)
    {
        bridge.name = piece.DisplayName;
        Physics.SyncTransforms();
        // Without a hit (a gap in a regenerated deck), the deck's own height, never the pivot under the arch.
        Vector3 deck = bridge.transform.position + Vector3.up * deckAbovePivot;
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(bridge.transform.position + Vector3.up * 30f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(bridge.transform) && (!found || hit.point.y > deck.y))
            {
                deck = hit.point;
                found = true;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"{piece.DisplayName}: no deck under the bridge's middle, the guards wait at its deck height.", bridge);
        }

        var ground = new GameObject("Deck");
        ground.transform.SetParent(bridge.transform, false);
        ground.transform.position = deck;
        ground.AddComponent<RaidSetPieceSite>().Bind(piece);
        if (piece.Prefab == null || !CampSpot(plan, rules, terrain, origin, crossing, half, out Vector2 spot))
        {
            return;
        }

        Vector2 toBridge = (crossing.Position - spot).normalized;
        GameObject camp = Object.Instantiate(piece.Prefab, RaidMapSurface.Ground(terrain, origin + new Vector3(spot.x, 0f, spot.y)),
            Quaternion.LookRotation(new Vector3(toBridge.x, 0f, toBridge.y), Vector3.up), bridge.transform.parent);
        camp.name = piece.DisplayName + " Camp";
        foreach (Transform part in camp.transform)
        {
            Vector3 at = part.position;
            part.position = new Vector3(at.x, RaidMapSurface.Ground(terrain, at).y, at.z);
        }
    }

    /// <summary>Metres the boss's camp keeps round its middle (its props reach about this far).</summary>
    private const float CampRadius = 4.5f;

    // The camp stands beside the trail, along the river from a bridge end, never on the trail itself (review 2026-11-17: straight past the
    // end it sat on the main way up); the higher bank first, then the other, either side of the trail; it must stand on the valley's floor
    // and out of the water. No spot: no camp (the boss is still on its bridge).
    private static bool CampSpot(RaidLayoutPlan plan, RaidLayoutRules rules, Terrain terrain, Vector3 origin, RaidLayoutCrossing crossing, float half, out Vector2 spot)
    {
        var along = new Vector2(-crossing.Direction.y, crossing.Direction.x);
        float aside = rules.TrailHalfWidth * plan.Mood.trails + CampRadius;
        Vector2 ahead = crossing.Direction * (half + CampFromBridge);
        float Height(Vector2 point) => RaidMapSurface.Ground(terrain, origin + new Vector3(point.x, 0f, point.y)).y;
        float first = Height(crossing.Position + ahead) >= Height(crossing.Position - ahead) ? 1f : -1f;
        foreach (float end in new[] { first, -first })
        {
            foreach (float side in new[] { 1f, -1f })
            {
                spot = crossing.Position + ahead * end + along * (side * aside);
                if ((plan.Valley == null || plan.Valley.Holds(spot, CampRadius)) && !plan.InRiver(spot, CampRadius))
                {
                    return true;
                }
            }
        }

        spot = default;
        return false;
    }
}
