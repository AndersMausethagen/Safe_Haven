using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The birds: their sprite sheet, material, sounds, one profile per place and the Birds prefab, put into the scenes beside the
/// fireflies. Run with unity command run_script --file AgentScripts/BuildBirds.cs --entry BuildBirds.Assets, then
/// BuildBirds.Scenes once the assets are in. Run Tools/Audio/birds.py first. Rebuilding changes nothing it needn't.
/// </summary>
public static class BuildBirds
{
    private const string Art = "Assets/_Project/Art/Birds/";
    private const string SheetPath = Art + "Bird Sheet.png";
    private const string MaterialPath = Art + "Birds.mat";
    private const string PrefabPath = "Assets/_Project/Prefabs/Environment/Birds.prefab";
    private const string Data = "Assets/_Project/Data/World/";
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Birds/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";
    private const int Cell = 128;
    private const int Smooth = 3;

    private static readonly (string scene, string profile)[] Places =
    {
        ("Assets/_Project/Scenes/SafeHaven.unity", "Birds - Haven.asset"),
        ("Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity", "Birds - Misty Woods.asset"),
        ("Assets/_Project/Scenes/Prologue_MistyWoods.unity", "Birds - Misty Woods.asset"),
    };

    public static string Assets()
    {
        Directory.CreateDirectory(Art);
        DrawSheet();
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Bird"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath));
        EditorUtility.SetDirty(material);
        Profiles();
        BuildPrefab(material);
        AssetDatabase.SaveAssets();
        return "assets built";
    }

    public static string Scenes()
    {
        Scene start = SceneManager.GetActiveScene();
        string done = "";
        foreach ((string scene, string profile) in Places)
        {
            Scene open = SceneManager.GetSceneByPath(scene);
            bool wasOpen = open.isLoaded;
            if (wasOpen && open.isDirty)
            {
                return $"{scene} has unsaved changes: stopped.";
            }

            if (!wasOpen)
            {
                open = EditorSceneManager.OpenScene(scene, OpenSceneMode.Additive);
            }

            done += Place(open, profile) + "; ";
            EditorSceneManager.SaveScene(open);
            if (!wasOpen)
            {
                EditorSceneManager.CloseScene(open, true);
            }
        }

        SceneManager.SetActiveScene(start);
        return done;
    }

    private static string Place(Scene scene, string profileFile)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<Birds>(true) != null)
            {
                return $"{scene.name} already has birds";
            }
        }

        Transform fireflies = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Fireflies found = root.GetComponentInChildren<Fireflies>(true);
            if (found != null)
            {
                fireflies = found.transform;
            }
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        if (fireflies != null)
        {
            instance.transform.SetParent(fireflies.parent, false);
            instance.transform.SetSiblingIndex(fireflies.GetSiblingIndex() + 1);
        }

        var so = new SerializedObject(instance.GetComponent<Birds>());
        so.FindProperty("profile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BirdProfile>(Data + profileFile);
        so.ApplyModifiedProperties();
        return $"{scene.name} placed under {(fireflies != null && fireflies.parent != null ? fireflies.parent.name : "the root")}";
    }

    private static void BuildPrefab(Material material)
    {
        var root = new GameObject("Birds");
        var system = root.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = true;
        main.loop = true;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.None;
        root.AddComponent<Birds>();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void Profiles()
    {
        SoundDefinition smallWings = Sound("Small Bird Wings", 0.3f, 0.2f, new Vector2(0.92f, 1.12f), 4f, 26f, 2, "Small Wings 1", "Small Wings 2", "Small Wings 3");
        SoundDefinition crowWings = Sound("Crow Wings", 0.4f, 0.2f, new Vector2(0.9f, 1.08f), 4f, 32f, 2, "Crow Wings 1", "Crow Wings 2", "Crow Wings 3");
        SoundDefinition chirp = Sound("Bird Chirp", 0.13f, 0.3f, new Vector2(0.9f, 1.15f), 5f, 28f, 2, "Bird Chirp 1", "Bird Chirp 2", "Bird Chirp 3", "Bird Chirp 4");
        SoundDefinition caw = Sound("Crow Caw", 0.3f, 0.25f, new Vector2(0.92f, 1.06f), 6f, 45f, 2, "Crow Caw 1", "Crow Caw 2", "Crow Caw 3", "Crow Caw 4");

        Profile("Birds - Haven.asset", new[]
        {
            new Color(0.62f, 0.47f, 0.32f), new Color(0.78f, 0.66f, 0.3f), new Color(0.42f, 0.56f, 0.72f),
            new Color(0.72f, 0.36f, 0.24f), new Color(0.82f, 0.78f, 0.68f),
        }, new Vector2(0.42f, 0.56f), 3, new Vector2Int(3, 7), new Vector2(5f, 13f), 0f, 0.8f, 0.25f, new Vector2(7f, 10f), 15f, 0.04f, 3.5f,
            smallWings, chirp, new Vector2(3f, 9f), false);
        Profile("Birds - Misty Woods.asset", new[]
        {
            new Color(0.075f, 0.075f, 0.09f), new Color(0.1f, 0.095f, 0.11f), new Color(0.06f, 0.065f, 0.085f),
        }, new Vector2(0.78f, 1f), 3, new Vector2Int(3, 6), new Vector2(7f, 18f), 0.34f, 0.6f, 0.35f, new Vector2(5.5f, 7.5f), 7f, 0.4f, 4.5f,
            crowWings, caw, new Vector2(7f, 18f), true);
    }

    private static void Profile(string file, Color[] colours, Vector2 size, int flocks, Vector2Int flock, Vector2 arrival, float night, float rainShy, float flyover,
        Vector2 speed, float flapRate, float glide, float flee, SoundDefinition takeOff, SoundDefinition call, Vector2 calls, bool alarm)
    {
        string path = Data + file;
        var profile = AssetDatabase.LoadAssetAtPath<BirdProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<BirdProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var so = new SerializedObject(profile);
        SerializedProperty list = so.FindProperty("colours");
        list.arraySize = colours.Length;
        for (int i = 0; i < colours.Length; i++)
        {
            list.GetArrayElementAtIndex(i).colorValue = colours[i];
        }

        so.FindProperty("size").vector2Value = size;
        so.FindProperty("mostFlocks").intValue = flocks;
        so.FindProperty("flockSize").vector2IntValue = flock;
        so.FindProperty("arrivalSeconds").vector2Value = arrival;
        so.FindProperty("nightShare").floatValue = night;
        so.FindProperty("rainShyness").floatValue = rainShy;
        so.FindProperty("flyoverChance").floatValue = flyover;
        so.FindProperty("flySpeed").vector2Value = speed;
        so.FindProperty("flapRate").floatValue = flapRate;
        so.FindProperty("glide").floatValue = glide;
        so.FindProperty("fleeSeconds").floatValue = flee;
        so.FindProperty("takeOff").objectReferenceValue = takeOff;
        so.FindProperty("call").objectReferenceValue = call;
        so.FindProperty("callSeconds").vector2Value = calls;
        so.FindProperty("callsOnTakeOff").boolValue = alarm;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static SoundDefinition Sound(string name, float volume, float variation, Vector2 pitch, float full, float audible, int atOnce, params string[] clips)
    {
        string path = Sounds + name + ".asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var so = new SerializedObject(sound);
        SerializedProperty list = so.FindProperty("clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Clips + clips[i] + ".wav");
            list.GetArrayElementAtIndex(i).objectReferenceValue = clip != null ? clip : throw new System.Exception("No clip " + clips[i]);
        }

        so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("volumeVariation").floatValue = variation;
        so.FindProperty("pitchRange").vector2Value = pitch;
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = full;
        so.FindProperty("audibleDistance").floatValue = audible;
        so.FindProperty("maxAtOnce").intValue = atOnce;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    // ----- The sheet: eight pictures of a bird from above, head up, drawn in shades of grey for the particle's colour to tint -----

    private static void DrawSheet()
    {
        var sheet = new Texture2D(Cell * 4, Cell * 2, TextureFormat.RGBA32, false);
        for (int frame = 0; frame < 8; frame++)
        {
            int column = frame % 4;
            int row = 1 - frame / 4;
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    sheet.SetPixel(column * Cell + x, row * Cell + y, Pixel(frame, x, y));
                }
            }
        }

        sheet.Apply();
        File.WriteAllBytes(SheetPath, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
    }

    private static Color Pixel(int frame, int x, int y)
    {
        float shade = 0f;
        float cover = 0f;
        for (int sy = 0; sy < Smooth; sy++)
        {
            for (int sx = 0; sx < Smooth; sx++)
            {
                var point = new Vector2((x + (sx + 0.5f) / Smooth) / Cell * 2f - 1f, (y + (sy + 0.5f) / Smooth) / Cell * 2f - 1f);
                float grey = Shade(frame, point);
                if (grey > 0f)
                {
                    cover += 1f;
                    shade += grey;
                }
            }
        }

        float samples = Smooth * Smooth;
        return cover > 0f ? new Color(shade / cover, shade / cover, shade / cover, cover / samples) : new Color(0.6f, 0.6f, 0.6f, 0f);
    }

    // 0 is empty; otherwise how light that part of the bird is.
    private static float Shade(int frame, Vector2 p)
    {
        bool perched = frame <= 1;
        float headForward = frame == 1 ? 0.1f : 0f;
        if (Ellipse(p, new Vector2(0f, 0.44f + headForward), 0.13f, 0.14f))
        {
            return 0.92f;
        }

        if (Triangle(p, new Vector2(-0.05f, 0.54f + headForward), new Vector2(0.05f, 0.54f + headForward), new Vector2(0f, 0.7f + headForward)))
        {
            return 0.45f;
        }

        if (Ellipse(p, new Vector2(0f, 0.04f), perched ? 0.2f : 0.15f, 0.4f))
        {
            return 1f;
        }

        if (Triangle(p, new Vector2(-0.08f, -0.3f), new Vector2(0.08f, -0.3f), new Vector2(perched ? -0.1f : -0.17f, -0.72f))
            || Triangle(p, new Vector2(0.08f, -0.3f), new Vector2(perched ? 0.1f : 0.17f, -0.72f), new Vector2(perched ? -0.1f : -0.17f, -0.72f)))
        {
            return 0.72f;
        }

        if (perched)
        {
            // Folded wings: long and narrow along the back.
            return Ellipse(new Vector2(Mathf.Abs(p.x), p.y), new Vector2(0.14f, -0.08f), 0.075f, 0.36f) ? 0.68f : 0f;
        }

        // A wing beat from above: nearly edge-on at the top and bottom of the stroke, widest in the middle.
        float beat = (frame - Bird.FirstFlapFrame) / (float)Bird.FlapFrames;
        float span = Mathf.Lerp(0.42f, 0.96f, 0.5f + 0.5f * Mathf.Sin(beat * Mathf.PI * 2f));
        var side = new Vector2(Mathf.Abs(p.x), p.y);
        float sweep = Mathf.Lerp(0.22f, 0.02f, span);
        bool wing = Triangle(side, new Vector2(0.08f, 0.26f), new Vector2(0.08f, -0.14f), new Vector2(span, 0.14f - sweep * 2f))
                    || Triangle(side, new Vector2(0.08f, -0.14f), new Vector2(span * 0.72f, -0.2f - sweep), new Vector2(span, 0.14f - sweep * 2f));
        return wing ? 0.72f : 0f;
    }

    private static bool Ellipse(Vector2 p, Vector2 centre, float rx, float ry)
    {
        Vector2 d = p - centre;
        return d.x * d.x / (rx * rx) + d.y * d.y / (ry * ry) <= 1f;
    }

    private static bool Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float Side(Vector2 from, Vector2 to) => (to.x - from.x) * (p.y - from.y) - (to.y - from.y) * (p.x - from.x);
        float ab = Side(a, b);
        float bc = Side(b, c);
        float ca = Side(c, a);
        return (ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f);
    }
}
