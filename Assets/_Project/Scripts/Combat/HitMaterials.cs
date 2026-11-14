using UnityEngine;

/// <summary>What a creature (or a thing struck) is made of, which decides what a blow on it throws off and sounds like.</summary>
public enum HitMaterial
{
    /// <summary>Flesh and fur: blood (the usual).</summary>
    Flesh,
    /// <summary>Stone (the stone trolls): grit and sparks, a stony crack. The look says "use a hammer".</summary>
    Stone,
    /// <summary>Plate and mail (the knights, the dead in their war gear): sparks and a clang.</summary>
    Metal,
    /// <summary>Bark and root (rootlings, the guardians): chips and sap, a dull thunk.</summary>
    Wood,
}

/// <summary>
/// Blows that sound and look like what they hit (2026-11-14): sparks and a ring off stone, a clang off plate, chips and a
/// thunk off bark, on top of the usual hit (CombatFeedback calls <see cref="Struck"/>). A swing that misses everyone but
/// strikes a boulder or a tree glances off it the same way (<see cref="Glance"/>, from PlayerCombat). Effects and sounds per
/// material are in Hit Material Settings.
/// </summary>
public static class HitMaterials
{
    private static readonly Collider[] Overlaps = new Collider[16];
    private static float nextGlance;

    /// <summary>A blow landed on <paramref name="victim"/>: its material's effect and sound, if it isn't flesh.</summary>
    public static void Struck(Damageable victim, in DamageInfo damage)
    {
        HitMaterial material = Of(victim);
        if (material == HitMaterial.Flesh)
        {
            return;
        }

        Play(material, damage.HitPoint, damage.Direction, false);
    }

    /// <summary>A swing that hit nobody: if its tip met a rock or a tree, it glances off with that material's effect.</summary>
    public static void Glance(Vector3 origin, Vector3 forward, float reach, GameObject swinger)
    {
        if (Time.time < nextGlance)
        {
            return;
        }

        forward.y = 0f;
        forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        Vector3 tip = origin + Vector3.up + forward * Mathf.Max(0.8f, reach * 0.85f);
        int count = Physics.OverlapSphereNonAlloc(tip, 0.55f, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = Overlaps[i];
            if (hit is TerrainCollider || (swinger != null && hit.transform.IsChildOf(swinger.transform)) || hit.GetComponentInParent<IDamageable>() != null)
            {
                continue;
            }

            HitMaterial material = OfWorld(hit);
            if (material == HitMaterial.Flesh)
            {
                continue;
            }

            Vector3 point = hit.ClosestPoint(origin + Vector3.up);
            Play(material, point, forward, true);
            nextGlance = Time.time + 0.3f;
            return;
        }
    }

    private static void Play(HitMaterial material, Vector3 point, Vector3 direction, bool glance)
    {
        HitMaterialSettings.Entry entry = HitMaterialSettings.Current.For(material);
        if (entry == null)
        {
            return;
        }

        Quaternion facing = direction.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(-direction) : Quaternion.identity;
        foreach (GameObject effect in glance ? entry.GlanceEffects : entry.Effects)
        {
            if (effect != null)
            {
                PrefabPool.Spawn(effect, point, facing);
            }
        }

        AudioManager.Play(glance ? entry.GlanceSound : entry.Sound, point);
    }

    /// <summary>What a struck creature is made of: its definition's Hit Material; flesh for anything else.</summary>
    public static HitMaterial Of(Damageable victim)
    {
        return victim != null && victim.TryGetComponent(out MonsterAI monster) && monster.Definition != null ? monster.Definition.HitMaterial : HitMaterial.Flesh;
    }

    // A thing in the world: trees are wood; boulders, cliffs and stones are stone, read from their names and materials.
    private static HitMaterial OfWorld(Collider hit)
    {
        if (hit.GetComponentInParent<ChoppableTree>() != null)
        {
            return HitMaterial.Wood;
        }

        string name = hit.transform.root.name.ToLowerInvariant() + " " + hit.name.ToLowerInvariant();
        Renderer renderer = hit.GetComponentInParent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            name += " " + renderer.sharedMaterial.name.ToLowerInvariant();
        }

        if (Has(name, "rock", "stone", "boulder", "cliff", "crag", "cairn", "menhir", "slab", "ore"))
        {
            return HitMaterial.Stone;
        }

        if (Has(name, "tree", "trunk", "log", "stump", "bark", "wood", "timber", "plank", "fence", "post"))
        {
            return HitMaterial.Wood;
        }

        return Has(name, "iron", "anvil", "metal", "steel") ? HitMaterial.Metal : HitMaterial.Flesh;
    }

    private static bool Has(string name, params string[] words)
    {
        foreach (string word in words)
        {
            if (name.Contains(word))
            {
                return true;
            }
        }

        return false;
    }
}
