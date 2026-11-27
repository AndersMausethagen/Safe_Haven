using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Effects from the Game VFX Mega Pack (URP), made ours (the user, 2026-11-27: "start with frost, earth, fire and blood. Tone down the
/// color ... Make them more grounded and dark or realistic"): each is copied into Art/Effects/Skills/Prefabs/Pack as a prefab of its
/// own, its colours drawn toward grey and darker (the pack is bright and stylised), sized to our scale, its materials copied where they
/// carry a colour of their own; then the looks that used our older effect of that part use it (a cue's prefab, a status's look or its
/// burst), each cue kept long enough for the effect to play out. The pack itself is never changed and never in git (Plastic holds it).
/// Run at the end of Dress the Moments (so Dress All keeps it) and safe to run again: the copies are remade from the pack each time.
/// </summary>
public static partial class SkillFxPrefabs
{
    private const string PackRoot = "Assets/Game VFX Mega Pack(URP)/";
    private const string PackPrefabs = "Assets/_Project/Art/Effects/Skills/Prefabs/Pack";
    private const string PackMaterials = "Assets/_Project/Art/Effects/Skills/Pack Materials";

    // How a part of the world is toned: how much colour stays (0 grey, 1 as made) and how bright (1 as made).
    private readonly struct Tone
    {
        public readonly float Saturation;
        public readonly float Brightness;

        public Tone(float saturation, float brightness)
        {
            Saturation = saturation;
            Brightness = brightness;
        }
    }

    private static readonly Tone Frost = new Tone(0.55f, 0.45f);
    private static readonly Tone Earth = new Tone(0.35f, 0.65f);
    private static readonly Tone Fire = new Tone(0.65f, 0.6f);
    // Blood stays red, but darker and less candy: toward a deep, brownish red.
    private static readonly Tone Blood = new Tone(0.85f, 0.62f);

    [MenuItem("Safe Haven/Tools/Effects/Dress From the VFX Pack (Frost, Earth, Fire, Blood)")]
    private static void DressFromPackMenu()
    {
        Debug.Log(DressFromPack());
    }

    /// <summary>Makes our copies of the pack's effects and points the looks at them. A summary for the log.</summary>
    public static string DressFromPack()
    {
        if (!AssetDatabase.IsValidFolder(PackRoot.TrimEnd('/')))
        {
            return "The Game VFX Mega Pack (URP) isn't in the project: nothing taken from it.";
        }

        Directory.CreateDirectory(PackPrefabs);
        Directory.CreateDirectory(PackMaterials);
        var log = new System.Text.StringBuilder("From the VFX pack:");

        // Every copy made first, then the looks pointed at them: saving a prefab between edits of the status assets left the later
        // copies unsaved, their materials missing.
        var swaps = new List<(string ours, GameObject pack, int most, SkillFxPlace? place)>();
        void Plan(string ours, GameObject pack, int most = int.MaxValue, SkillFxPlace? place = null) => swaps.Add((ours, pack, most, place));

        // Frost. Whole compositions, not pieces: a few of them where our ring placed a dozen pieces.
        Plan("Ult Giant Ice", MakeFromPack("Pack Ice Spikes", "FX - Six Elements Pack/FX_Ice Skill/Prefabs/FX_Ice_Crack.prefab", 0.42f, Frost), 5);
        Plan("Ult Frost Hit", MakeFromPack("Pack Frost Hit", "FX - Six Elements Pack/FX_Ice Skill/Prefabs/FX_Snow_Hit.prefab", 0.45f, Frost));
        Plan("Ult Blizzard", MakeFromPack("Pack Snowstorm", "FX - Six Elements Pack/FX_Ice Skill/Prefabs/FX_Snowstorm.prefab", 1f, Frost));
        Plan("Frost Spikes", MakeFromPack("Pack Ice Burst", "FX - Six Elements Pack/FX_Ice Skill/Prefabs/FX_IceSlash_Chop.prefab", 0.5f, Frost), 5);
        GameObject shards = MakeFromPack("Pack Ice Shards", "FX - Six Elements Pack/FX_Ice Skill/Prefabs/FX_Icearrow_Hit.prefab", 0.5f, Frost);
        Plan("Frost Hit", shards);
        Plan("Status Shatter", shards);

        // Earth. The rock burst is a ring of spikes of its own: once, under the caster.
        Plan("Ult Rock Spikes", MakeFromPack("Pack Rock Burst", "FX - Explosion & Crack/Prefabs/FX_Crack_RockAOE.prefab", 0.6f, Earth), 1, SkillFxPlace.CasterGround);
        GameObject splitting = MakeFromPack("Pack Earth Split", "FX - Explosion & Crack/Prefabs/FX_Carck_Splitting.prefab", 0.5f, Earth);
        Plan("Ult Earth Cracks", splitting);
        Plan("Phys Rock Spikes", splitting, 3);
        Plan("Phys Small Cracks", MakeFromPack("Pack Crack Dust", "FX - Explosion & Crack/Prefabs/FX_Crack_Dust.prefab", 0.45f, Earth));
        GameObject dust = MakeFromPack("Pack Dust Cloud", "FX - Explosion & Crack/Prefabs/FX_Dust_02.prefab", 0.4f, Earth);
        Plan("Ult Dust Wall", dust, 5);
        Plan("Wrath Dust", dust, 5);
        Plan("Earth Dust", dust, 4);

        // Fire.
        Plan("Ult Lava Cracks", MakeFromPack("Pack Lava Cracks", "FX - Explosion & Crack/Prefabs/FX_Crack_Lava.prefab", 0.7f, Fire));
        GameObject flames = MakeFromPack("Pack Flames", "FX - Six Elements Pack/FX_Fire Skill/Prefabs/FX_Dragonfire.prefab", 0.28f, Fire);
        Plan("Wildfire Flames", flames, 5);
        Plan("Ember Flames", flames, 3);
        GameObject fireHit = MakeFromPack("Pack Fire Hit", "FX - Six Elements Pack/FX_Fire Skill/Prefabs/FX_FireHit_02.prefab", 0.4f, Fire);
        Plan("Ult Fire Hit", fireHit);
        Plan("Ember Hit", fireHit);
        Plan("Payoff Burn", MakeFromPack("Pack Fire Burst", "FX - Six Elements Pack/FX_Fire Skill/Prefabs/FX_Floor_explosion.prefab", 0.45f, Fire));

        // Blood.
        Plan("Phys Blood Spray", MakeFromPack("Pack Blood Spurt", "FX - BloodSkill/Prefabs/FX_Blood_Hit_04.prefab", 0.55f, Blood));
        Plan("Phys Blood Hit", MakeFromPack("Pack Blood Gush", "FX - BloodSkill/Prefabs/FX_Spurting blood.prefab", 0.6f, Blood));
        Plan("Status Rend", MakeFromPack("Pack Bleeding", "FX - BloodSkill/Prefabs/FX_Spurblood_Loop.prefab", 0.4f, Blood));
        Plan("Payoff Bleed", MakeFromPack("Pack Blood Slash", "FX - BloodSkill/Prefabs/FX_BloodSlash.prefab", 0.5f, Blood));

        foreach ((string ours, GameObject pack, int most, SkillFxPlace? place) in swaps)
        {
            Swap(log, ours, pack, most, place);
        }

        // Our own layers of the same parts, toned the same way: the flares, waves, rune circles and cracks were made bright on purpose
        // for the ultimates (2026-11-27) and outshone the grounded look asked for.
        log.Append(ToneOurs("Ult Frost", Frost)).Append(ToneOurs("Ult Snow", Frost)).Append(ToneOurs("Ult Cold", Frost)).Append(ToneOurs("Ult Ice", Frost))
            .Append(ToneOurs("Ult Shard", Frost)).Append(ToneOurs("Frost ", Frost)).Append(ToneOurs("Hoarfrost", Frost)).Append(ToneOurs("Status Frost", Frost))
            .Append(ToneOurs("Status Frozen", Frost))
            .Append(ToneOurs("Ult Fire", Fire)).Append(ToneOurs("Ult Ember", Fire)).Append(ToneOurs("Ult Heat", Fire)).Append(ToneOurs("Ult Flame", Fire))
            .Append(ToneOurs("Ult Fireball", Fire)).Append(ToneOurs("Ember ", Fire)).Append(ToneOurs("Wildfire", Fire)).Append(ToneOurs("Kindle", Fire))
            .Append(ToneOurs("Status Burning", Fire))
            .Append(ToneOurs("Ult Earth", Earth)).Append(ToneOurs("Ult Pebbles", Earth)).Append(ToneOurs("Wrath ", Earth)).Append(ToneOurs("Earth ", Earth));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // Tones the colours of our own VFX Graph prefabs whose names begin so (VfxValues' "...Color" vectors, HDR colours), once: the values
    // toned are noted (a hash) in the prefab's import data, so running again leaves them, and a look remade bright is toned anew.
    private static string ToneOurs(string prefix, Tone tone)
    {
        int toned = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Art/Effects/Skills/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith(prefix) || path.Contains("/Pack/"))
            {
                continue;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            VfxValues values = prefab != null ? prefab.GetComponentInChildren<VfxValues>(true) : null;
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (values == null || importer == null || importer.userData == "toned:" + Hash(values))
            {
                continue;
            }

            bool changed = false;
            for (int i = 0; i < values.Vectors.Count; i++)
            {
                VfxValues.VectorValue entry = values.Vectors[i];
                if (entry.name != null && entry.name.Contains("Color"))
                {
                    Color c = Toned(new Color(entry.value.x, entry.value.y, entry.value.z, 1f), tone);
                    entry.value = new Vector3(c.r, c.g, c.b);
                    values.Vectors[i] = entry;
                    changed = true;
                }
            }

            if (!changed)
            {
                continue;
            }

            EditorUtility.SetDirty(values);
            PrefabUtility.SavePrefabAsset(prefab);
            importer.userData = "toned:" + Hash(values);
            importer.SaveAndReimport();
            toned++;
        }

        return toned > 0 ? $" toned {toned} of ours ({prefix.Trim()});" : "";
    }

    private static string Hash(VfxValues values)
    {
        var text = new System.Text.StringBuilder();
        foreach (VfxValues.VectorValue entry in values.Vectors)
        {
            text.Append(entry.name).Append(entry.value.ToString("F3"));
        }

        return text.ToString().GetHashCode().ToString("x");
    }

    // Our copy of a pack effect: unpacked, toned, scaled through the hierarchy, its coloured materials copied. Null when the pack lacks it.
    private static GameObject MakeFromPack(string name, string packPath, float scale, Tone tone)
    {
        string source = FindInPack(packPath);
        var prefab = source != null ? AssetDatabase.LoadAssetAtPath<GameObject>(source) : null;
        if (prefab == null)
        {
            Debug.LogWarning($"VFX pack: no {packPath}");
            return null;
        }

        var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copy.name = name;
            copy.transform.localScale = Vector3.one * scale;
            int made = 0;
            foreach (ParticleSystem system in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                // Sized by the root's scale, every part alike.
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startColor = Toned(main.startColor, tone);
                ParticleSystem.ColorOverLifetimeModule over = system.colorOverLifetime;
                if (over.enabled)
                {
                    over.color = Toned(over.color, tone);
                }
            }

            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && TonedMaterial(materials[i], tone, $"{name} {made}", out Material toned))
                    {
                        materials[i] = toned;
                        changed = true;
                        made++;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }

            // The toned materials on disk before the prefab that names them.
            AssetDatabase.SaveAssets();
            string path = $"{PackPrefabs}/{name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(copy, path, out bool success);
            if (!success)
            {
                Debug.LogError($"VFX pack: {name} wasn't saved to {path}");
            }

            return saved;
        }
        finally
        {
            Object.DestroyImmediate(copy);
        }
    }

    // The pack keeps its prefabs in folders of varied names (Prefab, Prefabs): found by the file's name under its collection.
    private static string FindInPack(string packPath)
    {
        string full = PackRoot + packPath;
        if (File.Exists(full))
        {
            return full;
        }

        string file = Path.GetFileNameWithoutExtension(packPath);
        // The collection: the folder above "Prefabs" (or "Prefab").
        string collection = (PackRoot + Path.GetDirectoryName(Path.GetDirectoryName(packPath))).Replace('\\', '/');
        foreach (string guid in AssetDatabase.FindAssets(file + " t:Prefab", new[] { PackRoot.TrimEnd('/') }))
        {
            string found = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(found) == file && found.StartsWith(collection))
            {
                return found;
            }
        }

        return null;
    }

    // A material with a colour of its own (a crystal's, a dissolve's) is copied with that colour toned; one that takes its colour from
    // the particles (the pack's additive and blended ones) is left shared.
    private static bool TonedMaterial(Material material, Tone tone, string name, out Material toned)
    {
        toned = null;
        Shader shader = material.shader;
        bool any = false;
        for (int i = 0; shader != null && i < shader.GetPropertyCount(); i++)
        {
            any |= shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color || shader.GetPropertyName(i) == "_Emission";
        }

        if (!any)
        {
            return false;
        }

        string path = $"{PackMaterials}/{name}.mat";
        // Made again in place, its ID kept: deleted and created anew at the same path, the prefab's references to it went missing.
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader = material.shader;
            existing.CopyPropertiesFromMaterial(material);
            toned = existing;
        }
        else
        {
            toned = new Material(material) { name = name };
            AssetDatabase.CreateAsset(toned, path);
        }
        // Every colour the shader has, whatever its name (a Shader Graph's are hashed), and an additive one's emission: a glow is what
        // makes the pack read as bright and stylised.
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string property = shader.GetPropertyName(i);
            if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color)
            {
                toned.SetColor(property, Toned(toned.GetColor(property), tone));
            }
            else if (property == "_Emission")
            {
                toned.SetFloat(property, toned.GetFloat(property) * tone.Brightness);
            }
        }

        EditorUtility.SetDirty(toned);
        return true;
    }

    private static Color Toned(Color colour, Tone tone)
    {
        float most = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
        float intensity = most > 1f ? most : 1f;
        Color.RGBToHSV(colour / intensity, out float h, out float s, out float v);
        // An HDR colour's glow damped too: kept, it bloomed to white whatever its hue (review in Play, 2026-11-27).
        float glow = intensity > 1f ? Mathf.Lerp(1f, intensity, tone.Brightness * 0.35f) : 1f;
        Color result = Color.HSVToRGB(h, s * tone.Saturation, v * tone.Brightness) * glow;
        result.a = colour.a;
        return result;
    }

    private static Gradient Toned(Gradient gradient, Tone tone)
    {
        if (gradient == null)
        {
            return null;
        }

        GradientColorKey[] keys = gradient.colorKeys;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].color = Toned(keys[i].color, tone);
        }

        var result = new Gradient { mode = gradient.mode };
        result.SetKeys(keys, gradient.alphaKeys);
        return result;
    }

    private static ParticleSystem.MinMaxGradient Toned(ParticleSystem.MinMaxGradient colour, Tone tone)
    {
        switch (colour.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(Toned(colour.color, tone));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(Toned(colour.colorMin, tone), Toned(colour.colorMax, tone));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(Toned(colour.gradient, tone));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(Toned(colour.gradientMin, tone), Toned(colour.gradientMax, tone));
            default:
                var random = new ParticleSystem.MinMaxGradient(Toned(colour.gradient, tone)) { mode = ParticleSystemGradientMode.RandomColor };
                return random;
        }
    }

    // Every look that used our older effect of this name uses the pack's now: a skill's cues (kept long enough for it to play out) and a
    // status's look or burst.
    // mostCopies: a cue that spread our pieces (a ring, an arc, along the reach) places at most this many of the pack's whole effects.
    // place: where it goes instead, when the pack's effect is already the whole shape (a ring of spikes of its own).
    private static void Swap(System.Text.StringBuilder log, string ours, GameObject pack, int mostCopies = int.MaxValue, SkillFxPlace? place = null)
    {
        if (pack == null)
        {
            log.Append($" {ours}: no pack effect;");
            return;
        }

        float length = PlayLength(pack);
        int swapped = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            SkillLook look = skill != null ? skill.Look : null;
            if (look == null)
            {
                continue;
            }

            bool changed = false;
            foreach (List<SkillFxCue> cues in new[] { look.cast, look.release, look.struck })
            {
                foreach (SkillFxCue cue in cues)
                {
                    if (cue.prefab != null && (cue.prefab.name == ours || cue.prefab == pack))
                    {
                        cue.prefab = pack;
                        cue.seconds = Mathf.Max(cue.seconds, length);
                        cue.count = Mathf.Min(cue.count, mostCopies);
                        if (place.HasValue)
                        {
                            cue.place = place.Value;
                        }
                        changed = true;
                        swapped++;
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(skill);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:StatusEffectDefinition"))
        {
            var status = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            var data = new SerializedObject(status);
            foreach (string field in new[] { "look", "consumeLook" })
            {
                SerializedProperty property = data.FindProperty(field);
                if (property != null && property.objectReferenceValue is GameObject used && (used.name == ours || used == pack))
                {
                    property.objectReferenceValue = pack;
                    swapped++;
                }
            }

            data.ApplyModifiedPropertiesWithoutUndo();
        }

        log.Append($" {ours} -> {pack.name} ({swapped});");
    }

    // How long the effect plays: its drawing systems' delay, duration and particle life (a loop, a few seconds of it).
    private static float PlayLength(GameObject effect)
    {
        float length = 0.5f;
        foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = system.main;
            length = Mathf.Max(length, main.startDelay.constantMax + (main.loop ? 2f : main.duration) + main.startLifetime.constantMax);
        }

        return Mathf.Min(length, 6f);
    }
}
