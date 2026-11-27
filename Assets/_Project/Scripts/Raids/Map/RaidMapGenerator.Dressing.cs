using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The objects of the set dressing (Docs/WorldDesignReview_2026-11-17.md G4, G9): the short flags beside the roads and the heroes that stand
/// near walkable ground, solid with their own colliders as the boulders are (heroes farther off are terrain trees, placed by
/// RaidMapSurface.Scatter). Under their own holder, made anew each attempt at the map, which ClearBlockedTrails can clear as it clears
/// boulders. Part of RaidMapGenerator, in its own file.
/// </summary>
public static partial class RaidMapGenerator
{
    public const string DressingName = "Dressing";

    // Share of a flag's height set into the ground, so it stands in it rather than on it.
    private const float FlagSink = 0.05f;

    /// <summary>Stands the plan's short flags and the heroes Scatter left as objects. Nothing (no holder) when there are none.</summary>
    public static void PlaceDressing(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, IReadOnlyList<RaidMapDefinition.HeroObject> heroes)
    {
        RemoveOld(map.transform, DressingName);

        bool flags = plan.Flags.Count > 0 && rules.ShortFlags.Count > 0;
        if (!flags && (heroes == null || heroes.Count == 0))
        {
            return;
        }

        var holder = new GameObject(DressingName).transform;
        holder.SetParent(map.transform, false);
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        if (flags)
        {
            foreach (RaidLayoutFlag flag in plan.Flags)
            {
                GameObject prefab = rules.ShortFlags[Mathf.Min(rules.ShortFlags.Count - 1, (int)(flag.Pick * rules.ShortFlags.Count))];
                if (prefab == null)
                {
                    continue;
                }

                GameObject made = Stand(holder, prefab, origin + new Vector3(flag.Position.x, 0f, flag.Position.y), flag.Turn, 1f, FlagSink, terrain);
                made.name = "Flag " + prefab.name;
            }
        }

        if (heroes == null)
        {
            return;
        }

        foreach (RaidMapDefinition.HeroObject hero in heroes)
        {
            if (hero.Hero?.prefab == null)
            {
                continue;
            }

            GameObject made = Stand(holder, hero.Hero.prefab, origin + new Vector3(hero.Position.x, 0f, hero.Position.y), hero.Turn, hero.Scale,
                hero.Hero.sink, terrain);
            made.name = "Hero " + hero.Hero.prefab.name;
            if (hero.Hero.onlyToSee)
            {
                OnlyToSee(made);
            }
        }
    }

    // A prefab on the ground, turned and scaled, with this share of its drawn height sunk into it.
    private static GameObject Stand(Transform holder, GameObject prefab, Vector3 at, float turn, float scale, float sink, Terrain terrain)
    {
        at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
        GameObject made = Object.Instantiate(prefab, at, Quaternion.Euler(0f, turn, 0f), holder);
        made.transform.localScale = prefab.transform.localScale * scale;
        var bounds = new Bounds(at, Vector3.zero);
        bool any = false;
        foreach (Renderer renderer in made.GetComponentsInChildren<Renderer>())
        {
            if (any)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                any = true;
            }
        }

        if (any)
        {
            made.transform.position += Vector3.up * (at.y - bounds.min.y - bounds.size.y * sink);
        }

        return made;
    }
}
