using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What dresses the land: boulders and lookouts from the cliff kit, landmarks seen from afar (scenery only), and the
/// fog of the map's low places. Part of RaidMapGenerator, in its own file.
/// </summary>
public static partial class RaidMapGenerator
{
    private const float BoulderSink = 0.2f;

    private const float LookoutWidest = 5f;

    private const float LookoutFromMiddle = 0.85f;

    private const float LookoutScale = 1.1f;

    private const float LookoutSeconds = 1.2f;

    private const float LookoutReach = 2.5f;

    // Solid boulders, set a little into the ground, and lookouts on the highest clearings. Kept out of Content, which is snapped to the ground.
    private static void PlaceBoulders(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        Transform old = map.transform.Find(BouldersName);
        if (old != null)
        {
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        CliffKit kit = rules.BoulderKit;
        if ((kit == null || kit.Pieces.Count == 0 || (plan.Boulders.Count == 0 && rules.Lookouts <= 0)) && plan.Landmarks.Count == 0)
        {
            return;
        }

        var holder = new GameObject(BouldersName).transform;
        holder.SetParent(map.transform, false);
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        var shapes = new Dictionary<GameObject, Vector3[]>();
        PlaceLandmarks(holder, plan, rules, terrain);
        if (kit == null || kit.Pieces.Count == 0)
        {
            return;
        }

        foreach (RaidLayoutBoulder boulder in plan.Boulders)
        {
            CliffKit.Piece piece = PickByShare(kit, boulder.Pick);
            if (piece != null)
            {
                Boulder(holder, piece, origin + new Vector3(boulder.Position.x, 0f, boulder.Position.y), boulder.Turn, Mathf.Min(boulder.Scale, Widest(piece, BoulderWidest)), terrain, shapes);
            }
        }

        PlaceLookouts(holder, plan, rules, kit, terrain, shapes);
    }

    // Tall things to find the way by, stripped of what could be gathered or used where the rule says so; solid.
    private static void PlaceLandmarks(Transform holder, RaidLayoutPlan plan, RaidLayoutRules rules, Terrain terrain)
    {
        Vector3 origin = terrain.transform.position;
        foreach (RaidLayoutLandmark landmark in plan.Landmarks)
        {
            RaidLandmarkRule rule = PickLandmark(rules, landmark.Pick);
            if (rule == null)
            {
                continue;
            }

            var at = origin + new Vector3(landmark.Position.x, 0f, landmark.Position.y);
            at.y = terrain.SampleHeight(at) + origin.y;
            GameObject made = Object.Instantiate(rule.prefab, at, Quaternion.Euler(0f, landmark.Turn, 0f), holder);
            made.name = "Landmark " + rule.prefab.name;
            made.transform.localScale = rule.prefab.transform.localScale * Mathf.Lerp(rule.scale.x, rule.scale.y, landmark.Scale);
            if (rule.onlyToSee)
            {
                OnlyToSee(made);
            }

            var bounds = new Bounds(at, Vector3.zero);
            foreach (Renderer renderer in made.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }

            made.transform.position += Vector3.up * (at.y - bounds.min.y - bounds.size.y * rule.sink);
        }
    }

    private static RaidLandmarkRule PickLandmark(RaidLayoutRules rules, float share)
    {
        float total = 0f;
        foreach (RaidLandmarkRule rule in rules.Landmarks)
        {
            total += rule != null && rule.prefab != null ? rule.weight : 0f;
        }

        float roll = share * total;
        foreach (RaidLandmarkRule rule in rules.Landmarks)
        {
            if (rule == null || rule.prefab == null)
            {
                continue;
            }

            roll -= rule.weight;
            if (roll <= 0f)
            {
                return rule;
            }
        }

        return null;
    }

    // What makes it a resource or a thing to use goes, dependants first; its bodies, colliders and looks stay.
    private static void OnlyToSee(GameObject made)
    {
        foreach (ChoppableTree tree in made.GetComponentsInChildren<ChoppableTree>(true)) Object.DestroyImmediate(tree);
        foreach (ResourceNode node in made.GetComponentsInChildren<ResourceNode>(true)) Object.DestroyImmediate(node);
        foreach (ResourceSource source in made.GetComponentsInChildren<ResourceSource>(true)) Object.DestroyImmediate(source);
        foreach (Interactable interactable in made.GetComponentsInChildren<Interactable>(true))
        {
            // What needs it goes first (a carved stone's reading), or Unity refuses to remove it and logs an error.
            foreach (MonoBehaviour user in interactable.GetComponents<MonoBehaviour>())
            {
                if (user != interactable && Requires(user.GetType(), typeof(Interactable)))
                {
                    Object.DestroyImmediate(user);
                }
            }

            Object.DestroyImmediate(interactable);
        }
    }

    private static bool Requires(System.Type type, System.Type component)
    {
        foreach (RequireComponent need in type.GetCustomAttributes(typeof(RequireComponent), true))
        {
            if (need.m_Type0 == component || need.m_Type1 == component || need.m_Type2 == component)
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject Boulder(Transform holder, CliffKit.Piece piece, Vector3 at, float turn, float scale, Terrain terrain, Dictionary<GameObject, Vector3[]> shapes)
    {
        at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
        GameObject made = Object.Instantiate(piece.prefab, holder);
        made.name = piece.prefab.name;
        made.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, turn, 0f) * RaidRavineCrags.StandUp(piece));
        made.transform.localScale = piece.prefab.transform.localScale * scale;
        RaidRavineCrags.Extent(made, piece.prefab, shapes, Vector3.up, out _, out _, out _, out float bottom, out float top);
        made.transform.position += Vector3.up * (at.y - bottom - (top - bottom) * BoulderSink);
        return made;
    }

    // The scale at which the piece's footprint is this many metres across.
    private static float Widest(CliffKit.Piece piece, float metres) => metres / Mathf.Max(0.5f, Mathf.Max(piece.size.x, piece.size.z));

    private static CliffKit.Piece PickByShare(CliffKit kit, float share)
    {
        float total = 0f;
        foreach (CliffKit.Piece piece in kit.Pieces)
        {
            total += piece.prefab != null ? piece.weight : 0f;
        }

        float roll = share * total;
        foreach (CliffKit.Piece piece in kit.Pieces)
        {
            if (piece.prefab == null)
            {
                continue;
            }

            roll -= piece.weight;
            if (roll <= 0f)
            {
                return piece;
            }
        }

        return kit.Pick(new System.Random(0));
    }

    // The highest plain clearings, each with a great rock at its edge to look out from.
    private static void PlaceLookouts(Transform holder, RaidLayoutPlan plan, RaidLayoutRules rules, CliffKit kit, Terrain terrain, Dictionary<GameObject, Vector3[]> shapes)
    {
        if (rules.Lookouts <= 0)
        {
            return;
        }

        Vector3 origin = terrain.transform.position;
        var rooms = plan.Rooms.FindAll(room => room.Role == RaidRoomRole.Plain);
        rooms.Sort((a, b) => terrain.SampleHeight(origin + new Vector3(b.Position.x, 0f, b.Position.y))
            .CompareTo(terrain.SampleHeight(origin + new Vector3(a.Position.x, 0f, a.Position.y))));
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Lookouts"));
        for (int i = 0; i < rules.Lookouts && i < rooms.Count; i++)
        {
            RaidLayoutRoom room = rooms[i];
            Vector2 away = (room.Position - new Vector2(plan.Size * 0.5f, plan.Size * 0.5f)).normalized;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Vector2.up;
            }

            Vector2 spot = room.Position + away * room.Radius * LookoutFromMiddle;
            CliffKit.Piece piece = PickByShare(kit, (float)random.NextDouble());
            GameObject rock = Boulder(holder, piece, origin + new Vector3(spot.x, 0f, spot.y), Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg,
                Mathf.Min(LookoutScale, Widest(piece, LookoutWidest)), terrain, shapes);
            rock.name = "Lookout Rock";
            var lookout = new GameObject("Lookout");
            lookout.transform.SetParent(rock.transform, true);
            Vector3 foot = origin + new Vector3(room.Position.x + away.x * (room.Radius * LookoutFromMiddle - 3.5f), 0f, room.Position.y + away.y * (room.Radius * LookoutFromMiddle - 3.5f));
            foot.y = terrain.SampleHeight(foot) + origin.y + 0.5f;
            lookout.transform.position = foot;
            lookout.AddComponent<Interactable>().Configure(InteractionAnimation.None, LookoutSeconds, LookoutReach, true);
            lookout.AddComponent<RaidLookout>().Configure(rules.LookoutReveal);
        }
    }

    private const string MoodFogSuffix = " (mood)";

    // The scene's banks of fog lie over clearings, plus as many more as the mood wants.
    private static void PlaceFog(RaidMap map, RaidLayoutPlan plan, System.Func<Vector2, Vector3> world)
    {
        // The scene's own banks only: the mood's extras from an earlier attempt at the layout go (a retry added more each time
        // and left them over the old map's clearings, on ridges and in ravines).
        var areas = new List<FogArea>();
        foreach (FogArea area in map.Content.GetComponentsInChildren<FogArea>(true))
        {
            if (area.name.EndsWith(MoodFogSuffix))
            {
                Object.Destroy(area.gameObject);
            }
            else
            {
                areas.Add(area);
            }
        }

        if (areas.Count == 0)
        {
            return;
        }

        var rooms = plan.Rooms.FindAll(room => room.Role == RaidRoomRole.Plain);
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Fog"));
        List<int> order = RaidLayout.PickIndices(random, rooms.Count, rooms.Count);
        int wanted = areas.Count + plan.Mood.fogBanks;
        for (int i = 0; i < wanted && i < order.Count; i++)
        {
            FogArea area = i < areas.Count ? areas[i] : Object.Instantiate(areas[0], areas[0].transform.parent);
            if (i >= areas.Count)
            {
                area.name = areas[0].name + MoodFogSuffix;
            }

            area.transform.position = world(rooms[order[i]].Position / plan.Size);
        }
    }
}
