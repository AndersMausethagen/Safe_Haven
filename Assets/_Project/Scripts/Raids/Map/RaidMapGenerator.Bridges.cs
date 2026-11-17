using UnityEngine;

/// <summary>
/// Bridges over a Valley map's river (RaidLayoutCrossing.Bridge): the rules' bridge prefab laid across each bridged crossing, square to
/// the river, its deck level with the banks at both ends. Placed with the rivers, before the navmesh is built, so its deck collider
/// becomes walkable ground. Part of RaidMapGenerator, in its own file.
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
        }
    }
}
