using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The VFX pack's third group (the user, 2026-11-27: "go ahead with the rest from the pack"): the guard's sparks (block, parry,
/// stagger), the spells' cast circles, the bolts Kindling Bolt and Elf-Shot fly, the fire arrow's flames, the Draug rising and the
/// Lost vanishing. The pack's dark set is purple and cartoonish (faces, skulls): only its smoke is used, toned to a grey-black.
/// The pack's sparkle trails were left: arrows draw their own streak (ArrowProjectile's trail).
/// </summary>
public static partial class SkillFxPrefabs
{
    private static readonly Tone Grave = new Tone(0.15f, 0.4f);
    private static readonly Tone Root = new Tone(0.4f, 0.6f);
    // A warm, bright white-gold: the guard's rewards must catch the eye in a fight.
    private static readonly Tone Flash = new Tone(0.45f, 1f);
    private const string BoltBase = "Assets/_Project/Prefabs/Enemies/WispBolt.prefab";
    private const string BoltFolder = "Assets/_Project/Prefabs/Effects/Combat";

    private static List<(string ours, GameObject pack, int most, SkillFxPlace? place)> PlanTheRest(System.Text.StringBuilder log)
    {
        var swaps = new List<(string ours, GameObject pack, int most, SkillFxPlace? place)>();

        // The guard: a steel ring and star where a blow is caught or turned, a burst where something reels. The parry and the stagger
        // are the player's rewards: bigger and brighter than the rest (the user, 2026-11-27: "I dont notice the parry and stagger vfx at
        // all"; they were grey and half the size).
        log.Append(SetFeel("blockEffect", MakeFromPack("Pack Block", "FX - Hit/Prefab/FX_Blue_Hit_03.prefab", 0.45f, Steel, pooled: true)));
        // Their parts lived 0.16-0.3 s, a blink under the hit stop: they linger two and a half times as long.
        log.Append(SetFeel("parryEffect", Linger(MakeFromPack("Pack Parry", "FX - Hit/Prefab/FX_Slash_Hit.prefab", 1.7f, Flash, pooled: true), GuardLinger)));
        log.Append(SetFeel("staggerEffect", Linger(MakeFromPack("Pack Stagger", "FX - Hit/Prefab/FX_Yellow_Hit_06.prefab", 1.2f, Flash, pooled: true), GuardLinger)));

        // The spells' cast circles.
        swaps.Add(("Frost Cast Circle", MakeFromPack("Pack Frost Circle", "FX - Dissolving Magic Circle/Prefabs/FX_DMagicCircle_IceRune.prefab", 1.2f, Frost), int.MaxValue, null));
        swaps.Add(("Ember Cast Circle", MakeFromPack("Pack Ember Circle", "FX - Dissolving Magic Circle/Prefabs/FX_DMagicCircle_Swirl.prefab", 1.2f, Fire), int.MaxValue, null));
        swaps.Add(("Root Cast Circle", MakeFromPack("Pack Root Circle", "FX - Dissolving Magic Circle/Prefabs/FX_DMagicCircle_GreenSeal.prefab", 1.2f, Root), int.MaxValue, null));

        // The bolts: our WispBolt (its flight and blow) with the pack's bullet in place of its glowing ball, and the pack's hit where it lands.
        log.Append(MakeBolt("Ember Bolt", "KindlingBolt",
            MakeFromPack("Pack Ember Bullet", "FX - Magic Projectiles Vol.1/Prefabs/FX_Fire_Bullet.prefab", 0.5f, Fire),
            MakeFromPack("Pack Ember Impact", "FX - Magic Projectiles Vol.1/Prefabs/FX_Fire_Hit.prefab", 0.4f, Fire, pooled: true),
            new Color(1f, 0.55f, 0.25f)));
        log.Append(MakeBolt("Elf Bolt", "ElfShot",
            MakeFromPack("Pack Elf Bullet", "FX - Magic Projectiles Vol.1/Prefabs/FX_GoldArrow_Bullet.prefab", 0.5f, Wyrd),
            MakeFromPack("Pack Elf Impact", "FX - Magic Projectiles Vol.1/Prefabs/FX_GoldArrow_Hit.prefab", 0.4f, Wyrd, pooled: true),
            new Color(1f, 0.85f, 0.5f)));

        log.Append(AddArrowFlames("Assets/_Project/Prefabs/Effects/Combat/FireArrowProjectile.prefab",
            MakeFromPack("Pack Arrow Flames", "FX - Particle Trails/Prefab/FX_ParticleTrail_Fire.prefab", 0.2f, Fire)));

        // The dead: grave smoke where a Draug stands again, and where a Lost One slips away.
        log.Append(SetPrefabField("Assets/_Project/Prefabs/Enemies/Draug.prefab", typeof(RisesAgain), "riseEffect",
            MakeFromPack("Pack Grave Smoke", "FX - Six Elements Pack/FX_Dark Skill/Prefabs/FX_DarkSkill_SmokeExplosion.prefab", 0.5f, Grave, pooled: true)));
        GameObject vanish = MakeFromPack("Pack Vanish Smoke", "FX - Six Elements Pack/FX_Dark Skill/Prefabs/FX_DarkSkill_Hit02.prefab", 0.6f, Grave);
        // Without the pack (a checkout from git alone) nothing is made: the effect set before is kept, not cleared.
        foreach (string guid in vanish != null ? AssetDatabase.FindAssets("t:LostOneSettings") : new string[0])
        {
            var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<LostOneSettings>(AssetDatabase.GUIDToAssetPath(guid)));
            data.FindProperty("vanishEffect").objectReferenceValue = vanish;
            data.ApplyModifiedPropertiesWithoutUndo();
            log.Append(" the Lost vanish in ").Append(vanish != null ? vanish.name : "nothing").Append(';');
        }

        return swaps;
    }

    private const float GuardLinger = 2.5f;

    // Makes a pooled burst's particles live <paramref name="factor"/> times as long (and their delays wait as long), its pool lifetime
    // with them. Applied to the made copy, so running the dressing again gives the same result.
    private static GameObject Linger(GameObject effect, float factor)
    {
        if (effect == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(effect);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            float longest = 0f;
            foreach (ParticleSystem system in contents.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.startLifetimeMultiplier *= factor;
                main.startDelayMultiplier *= factor;
                var drawn = system.GetComponent<ParticleSystemRenderer>();
                if (drawn != null && drawn.enabled && drawn.sharedMaterial != null)
                {
                    longest = Mathf.Max(longest, main.startDelay.constantMax + main.startLifetime.constantMax);
                }
            }

            if (contents.TryGetComponent(out PooledEffect pooled))
            {
                var data = new SerializedObject(pooled);
                data.FindProperty("lifetime").floatValue = Mathf.Max(0.5f, longest + 0.1f);
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // A bolt of our own made from WispBolt (a prefab variant: its flight and blow stay WispBolt's), its ball hidden, the pack's bullet
    // flying in its place, the pack's hit where it lands, its light in the bolt's colour; the attack named flies it.
    private static string MakeBolt(string name, string attackName, GameObject bullet, GameObject impact, Color light)
    {
        var baseBolt = AssetDatabase.LoadAssetAtPath<GameObject>(BoltBase);
        AttackDefinition attack = FindAttack(attackName);
        if (baseBolt == null || bullet == null || attack == null)
        {
            return $" {name}: not made;";
        }

        UnityEngine.SceneManagement.Scene stage = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var bolt = (GameObject)PrefabUtility.InstantiatePrefab(baseBolt, stage);
        try
        {
            foreach (MeshRenderer ball in bolt.GetComponentsInChildren<MeshRenderer>(true))
            {
                ball.enabled = false;
            }

            foreach (Light glow in bolt.GetComponentsInChildren<Light>(true))
            {
                glow.color = light;
            }

            var flying = (GameObject)PrefabUtility.InstantiatePrefab(bullet, stage);
            flying.transform.SetParent(bolt.transform, false);
            var data = new SerializedObject(bolt.GetComponent<MonsterProjectile>());
            data.FindProperty("impactEffect").objectReferenceValue = impact;
            data.ApplyModifiedPropertiesWithoutUndo();
            string path = $"{BoltFolder}/{name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(bolt, path, out bool success);
            if (!success)
            {
                return $" {name}: not saved;";
            }

            var attackData = new SerializedObject(attack);
            attackData.FindProperty("castProjectile").objectReferenceValue = saved;
            attackData.ApplyModifiedPropertiesWithoutUndo();
            return $" {attack.name} flies {name};";
        }
        finally
        {
            Object.DestroyImmediate(bolt);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(stage);
        }
    }

    private static AttackDefinition FindAttack(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:AttackDefinition"))
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (attack != null && attack.name == name)
            {
                return attack;
            }
        }

        return null;
    }

    // The fire arrow streams the pack's flames behind it (ArrowProjectile starts and stops them with its trail). Made once: a second
    // run finds them there.
    private static string AddArrowFlames(string arrowPath, GameObject flames)
    {
        if (flames == null)
        {
            return " arrow flames: no pack effect;";
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(arrowPath);
        try
        {
            Transform model = contents.transform.childCount > 0 ? contents.transform.GetChild(0) : contents.transform;
            if (model.Find(flames.name) != null)
            {
                return " the fire arrow already streams flames;";
            }

            var streaming = (GameObject)PrefabUtility.InstantiatePrefab(flames, contents.scene);
            streaming.transform.SetParent(model, false);
            PrefabUtility.SaveAsPrefabAsset(contents, arrowPath);
            return " the fire arrow streams flames;";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // A field on a component of a prefab's root (a Draug's RisesAgain › Rise Effect).
    private static string SetPrefabField(string prefabPath, System.Type component, string field, GameObject value)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Component target = prefab != null ? prefab.GetComponent(component) : null;
        if (target == null || value == null)
        {
            return $" {component.Name}.{field}: not set;";
        }

        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
        return $" {prefab.name}'s {field} -> {value.name};";
    }
}
